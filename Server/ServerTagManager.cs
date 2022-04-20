using System;
using System.Collections.Generic;
using System.Linq;
using Hkmp.Api.Server;
using Hkmp.Concurrency;
using ILogger = Hkmp.ILogger;

namespace HkmpTag.Server {
    /// <summary>
    /// Manager for server-side Tag.
    /// </summary>
    public class ServerTagManager {
        /// <summary>
        /// The logger instance.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// The server API instance.
        /// </summary>
        private readonly IServerApi _serverApi;

        /// <summary>
        /// The server network manager instance.
        /// </summary>
        private readonly ServerNetManager _netManager;

        /// <summary>
        /// The server settings instance.
        /// </summary>
        private readonly ServerSettings _settings;

        /// <summary>
        /// The transition manager instance.
        /// </summary>
        private readonly ServerTransitionManager _transitionManager;

        /// <summary>
        /// The instance of random used to decide the initial infected.
        /// </summary>
        private readonly Random _random;

        /// <summary>
        /// The current game state.
        /// </summary>
        private GameState GameState { get; set; }

        /// <summary>
        /// Current delay action that is used.
        /// </summary>
        private DelayedAction _currentDelayAction;

        /// <summary>
        /// The number of automatic games that were played on the current preset.
        /// </summary>
        private int _numGamesOnPreset = -1;

        /// <summary>
        /// Dictionary mapping player IDs to ServerTagPlayer instances.
        /// </summary>
        private ConcurrentDictionary<ushort, ServerTagPlayer> _players;

        /// <summary>
        /// List of IDs that were chosen as initial infected in the last round.
        /// </summary>
        private readonly List<ushort> _lastChosenIds;

        /// <summary>
        /// Construct the tag manager with the server addon and server API.
        /// </summary>
        /// <param name="addon">The server addon instance.</param>
        /// <param name="serverApi">The server API instance.</param>
        public ServerTagManager(TagServerAddon addon, IServerApi serverApi) {
            _logger = addon.Logger;
            _serverApi = serverApi;

            _settings = ServerSettings.LoadFromFile();

            _random = new Random();

            _netManager = new ServerNetManager(addon, serverApi.NetServer);
            _transitionManager = new ServerTransitionManager(_logger, _random);

            _lastChosenIds = new List<ushort>();
        }

        /// <summary>
        /// Initializes the tag manager by setting default values and registering commands and callbacks.
        /// </summary>
        public void Initialize() {
            GameState = GameState.PreGame;

            _serverApi.CommandManager.RegisterCommand(new TagCommand(this, _settings, _transitionManager));

            _netManager.TaggedEvent += playerId => OnTagged(playerId);

            _serverApi.ServerManager.PlayerConnectEvent += OnPlayerConnect;
            _serverApi.ServerManager.PlayerDisconnectEvent += OnPlayerDisconnect;

            _transitionManager.Initialize();

            if (_settings.Auto) {
                _logger.Info(this, "Game automation is enabled");
                
                GameState = GameState.WaitingForPlayers;
            }
        }

        /// <summary>
        /// Instruct all players to warp to the given preset and use the transition restrictions. Will provide
        /// feedback by sending a message using the given action.
        /// </summary>
        /// <param name="sendMessageAction">The action to send feedback messages.</param>
        public void WarpToPreset(Action<string> sendMessageAction = null) {
            if (GameState != GameState.PreGame) {
                const string unableToWarpMsg = "Cannot warp to preset at this moment";
                sendMessageAction?.Invoke(unableToWarpMsg);
                _logger.Info(this, unableToWarpMsg);
                return;
            }

            sendMessageAction?.Invoke("Warping players to preset...");
            _logger.Info(this, "Using preset, sending game info");

            var gamePreset = _transitionManager.GetTransitionRestrictions();
            if (gamePreset == null) {
                _logger.Warn(this, "Game preset is null, cannot warp");
                return;
            }
            
            _netManager.SendGameInfo(
                gamePreset.WarpSceneIndex, 
                gamePreset.WarpTransitionIndex,
                gamePreset.SceneTransitions
            );
        }

        /// <summary>
        /// Start the game with the given number of infected. Will provide feedback by sending a message using
        /// the given action.
        /// </summary>
        /// <param name="numInfected">The number of initial infected.</param>
        /// <param name="sendMessageAction">The action to send feedback messages.</param>
        public void StartGame(ushort numInfected, Action<string> sendMessageAction = null) {
            if (GameState != GameState.PreGame) {
                const string unableToStartMsg = "Cannot start game at this moment";
                sendMessageAction?.Invoke(unableToStartMsg);
                _logger.Info(this, unableToStartMsg);
                return;
            }

            if (!CheckGameStart(numInfected, sendMessageAction)) {
                return;
            }

            _serverApi.ServerManager.BroadcastMessage($"Choosing new infected in {_settings.CountdownTime} seconds!");

            _logger.Info(this, $"Game can be started, choosing initial infected in {_settings.CountdownTime} seconds");

            GameState = GameState.Countdown;
            _currentDelayAction = new DelayedAction(_settings.CountdownTime * 1000, () => {
                if (!CheckGameStart(numInfected, sendMessageAction)) {
                    _logger.Info(this, "Game could not be started after countdown");
                    GameState = _settings.Auto ? GameState.WaitingForPlayers : GameState.PreGame;
                    return;
                }

                var serverPlayers = _serverApi.ServerManager.Players;

                // The list of IDs to choose from to be initial infected
                // We map the players to their IDs and then filter out which players where chosen last
                var allIds = new List<ushort>(serverPlayers
                    .Select(p => p.Id)
                    .Where(id => !_lastChosenIds.Contains(id))
                );
                // The list of IDs that are chosen
                var randomIds = new List<ushort>();

                while (numInfected-- > 0) {
                    var randomIndex = _random.Next(allIds.Count);
                    randomIds.Add(allIds[randomIndex]);
                    allIds.RemoveAt(randomIndex);
                }

                // Add the chosen IDs to the list of last chosen, so they don't get chosen again
                _lastChosenIds.Clear();
                _lastChosenIds.AddRange(randomIds);

                _players = new ConcurrentDictionary<ushort, ServerTagPlayer>();

                foreach (var player in serverPlayers) {
                    _players[player.Id] = new ServerTagPlayer {
                        Id = player.Id,
                        State = randomIds.Contains(player.Id) ? PlayerState.Infected : PlayerState.Uninfected
                    };
                }

                var infectedPlayerNames = string.Join(", ", serverPlayers.Where(
                    p => randomIds.Contains(p.Id)
                ).Select(p => p.Username));
                _logger.Info(this, $"Starting game with initial infected: {infectedPlayerNames}");

                _netManager.SendGameStart(_players.GetCopy().Values.ToList());

                GameState = GameState.InGame;

                if (_settings.Auto) {
                    _logger.Info(this, $"Scheduling game timeout after {_settings.MaxGameTime} seconds");
                    _currentDelayAction = new DelayedAction(_settings.MaxGameTime, () => {
                        // After the max game time, we simply end the game
                        _logger.Info(this, "Forcefully ending game after max game time has been reached");
                        OnGameEnd(false);
                    });
                }
            });
            _currentDelayAction.Start();
        }

        /// <summary>
        /// Toggle whether the game is set to automatic mode.
        /// </summary>
        /// <param name="sendMessageAction">The action to send feedback messages.</param>
        public void ToggleAuto(Action<string> sendMessageAction) {
            _settings.Auto = !_settings.Auto;
            _settings.SaveToFile();

            if (_settings.Auto) {
                const string autoEnabledMsg = "Game automation is enabled";

                sendMessageAction.Invoke(autoEnabledMsg);
                _logger.Info(this, autoEnabledMsg);

                if (GameState == GameState.PreGame) {
                    ProcessAutoGameStart();
                }
            } else {
                const string autoDisabledMsg = "Game automation is disabled";

                sendMessageAction.Invoke(autoDisabledMsg);
                _logger.Info(this, autoDisabledMsg);

                if (GameState == GameState.WaitingForPlayers || GameState == GameState.PostGame) {
                    GameState = GameState.PreGame;

                    _currentDelayAction?.Stop();
                }
            }
        }

        /// <summary>
        /// Checks if the game can be started with the number of online players and given number of infected.
        /// Will provide feedback by sending a message using the given action.
        /// </summary>
        /// <param name="numInfected">The number of initial infected.</param>
        /// <param name="sendMessageAction">The action to send feedback messages.</param>
        /// <returns>true if the game can be started; otherwise false.</returns>
        private bool CheckGameStart(ushort numInfected, Action<string> sendMessageAction = null) {
            var serverPlayers = _serverApi.ServerManager.Players;

            // We cannot start without at least 3 players
            if (serverPlayers.Count < 3) {
                const string notEnoughPlayersMsg = "Could not start game with less than 3 players";
                sendMessageAction?.Invoke(notEnoughPlayersMsg);
                _logger.Info(this, notEnoughPlayersMsg);
                return false;
            }

            // We cannot start the game with 0 or too many players
            if (numInfected < 1 || numInfected >= serverPlayers.Count - 1) {
                const string invalidNumPlayersMsg = "Could not start game with invalid number of players";
                sendMessageAction?.Invoke(invalidNumPlayersMsg);
                _logger.Info(this, invalidNumPlayersMsg);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks whether the game can start with the number of online players for an automatic game.
        /// </summary>
        private void ProcessAutoGameStart() {
            if (!_settings.Auto) {
                return;
            }

            var serverPlayers = _serverApi.ServerManager.Players;
            var numPlayers = serverPlayers.Count;
            var numInfected = (ushort)Math.Min(ushort.MaxValue, Math.Max(1, numPlayers / 6));

            if (!CheckGameStart(numInfected)) {
                _logger.Info(this, "Could not start automatic game");

                // Not enough players to start, so we change the game state
                GameState = GameState.WaitingForPlayers;
                return;
            }

            GameState = GameState.PreGame;

            if (UseNewPreset()) {
                _logger.Info(this, "Using new preset for automatic game");

                // Either we have not chosen a preset yet, or we have played the maximum number of games on
                // this preset already so we are switching
                _numGamesOnPreset = 0;

                if (!_transitionManager.SetRandomPreset(numPlayers)) {
                    _logger.Warn(this, $"Could not find a suitable random preset for number of players ({numPlayers})");
                }
                
                WarpToPreset();

                _currentDelayAction = new DelayedAction(_settings.WarpTime * 1000, () => {
                    _logger.Info(this, "Warp time elapsed, starting game...");
                    StartGame(numInfected);
                });
                _currentDelayAction.Start();
            } else {
                _logger.Info(this, "Using existing preset for automatic game");
                StartGame(numInfected);
            }
        }

        /// <summary>
        /// Whether to use a new preset when starting the automatic game.
        /// </summary>
        /// <returns>true if a new preset should be used; otherwise false.</returns>
        private bool UseNewPreset() {
            var numPresets = _transitionManager.GetPresetNames().Length;

            if (numPresets == 0) {
                return false;
            }

            if (_numGamesOnPreset == -1) {
                return true;
            }

            return _numGamesOnPreset >= _settings.MaxGamesOnPreset && numPresets > 1;
        }

        /// <summary>
        /// Ends the game and provides feedback by sending a message using the given action.
        /// </summary>
        /// <param name="sendMessageAction">The action to send feedback messages.</param>
        public void EndGame(Action<string> sendMessageAction) {
            if (GameState != GameState.InGame && GameState != GameState.Countdown) {
                const string notStartedMsg = "Game is not started";
                sendMessageAction.Invoke(notStartedMsg);
                _logger.Info(this, notStartedMsg);
                return;
            }

            if (GameState == GameState.InGame) {
                _logger.Info(this, "Sending game end to clients");

                // If the game was in progress, we end it
                _netManager.SendGameEnd(false);
            } else if (GameState == GameState.Countdown) {
                // If the countdown was in progress, we cancel it
                _currentDelayAction.Stop();

                const string stoppedCountdownMsg = "Stopped start countdown";
                sendMessageAction.Invoke(stoppedCountdownMsg);
                _logger.Info(this, stoppedCountdownMsg);
            }
        }

        /// <summary>
        /// Method that is called when the game ends.
        /// </summary>
        /// <param name="hasWinner">Whether the game has a winner.</param>
        /// <param name="winnerId">The ID of the player that won or 0 if there is no winner.</param>
        private void OnGameEnd(bool hasWinner, ushort winnerId = 0) {
            var logMsg = $"Game has ended, has winner: {hasWinner}";
            if (hasWinner) {
                logMsg += $", winner ID: {winnerId}";
            }

            _logger.Info(this, logMsg);

            _netManager.SendGameEnd(hasWinner, winnerId);

            _currentDelayAction?.Stop();

            if (_settings.Auto) {
                // Since the game has ended, we know that we played another game on this preset
                _numGamesOnPreset++;

                _logger.Info(this,
                    $"Automation is enabled, number of games played on current preset: {_numGamesOnPreset}");

                GameState = GameState.PostGame;

                var newGameStartMsg =
                    $"Starting new game in {_settings.PostGameTime + _settings.CountdownTime} seconds";
                _serverApi.ServerManager.BroadcastMessage(newGameStartMsg);
                _logger.Info(this, newGameStartMsg);

                _currentDelayAction = new DelayedAction(_settings.PostGameTime * 1000, ProcessAutoGameStart);
                _currentDelayAction.Start();
            } else {
                GameState = GameState.PreGame;
            }
        }

        /// <summary>
        /// Callback method for when a player is tagged.
        /// </summary>
        /// <param name="playerId">The ID of the tagged player.</param>
        /// <param name="disconnect">Whether they were tagged due to disconnect.</param>
        private void OnTagged(ushort playerId, bool disconnect = false) {
            _logger.Info(this, $"Received tag from: {playerId}");

            if (GameState != GameState.InGame) {
                _logger.Info(this, "Game is not started");
                return;
            }

            // First find the player corresponding to this ID and mark them as infected
            var players = _players.GetCopy();
            if (players.TryGetValue(playerId, out var tagPlayer)) {
                tagPlayer.State = PlayerState.Infected;
            }

            // Count the number of uninfected players left
            var numUninfected = players.Values.Count(p => p.State == PlayerState.Uninfected);
            if (numUninfected > 1) {
                _logger.Info(this, $"Game is not over yet, number of uninfected left: {numUninfected}");

                // If the game is not over yet, send the tag to all players
                _netManager.SendTag(
                    players.Values.ToList(),
                    playerId,
                    (ushort)numUninfected,
                    disconnect
                );
                return;
            }

            if (numUninfected == 1) {
                OnGameEnd(true, players.Values.First(p => p.State == PlayerState.Uninfected).Id);
            } else if (numUninfected == 0) {
                OnGameEnd(false);
            }
        }

        /// <summary>
        /// Callback method for when a player connects to the server.
        /// </summary>
        /// <param name="player">The player that connects.</param>
        private void OnPlayerConnect(IServerPlayer player) {
            _logger.Info(this, $"Player with ID {player.Id} connected");

            var gamePreset = _transitionManager.GetTransitionRestrictions();
            if (gamePreset == null) {
                _logger.Info(this, "Game preset is null, cannot send info");
            } else {
                if (GameState == GameState.InGame) {
                    _logger.Info(this, "Game is in-progress, sending game in progress packet");

                    _players[player.Id] = new ServerTagPlayer {
                        Id = player.Id,
                        State = PlayerState.Infected
                    };

                    _netManager.SendGameInProgress(
                        player.Id,
                        gamePreset.WarpSceneIndex,
                        gamePreset.WarpTransitionIndex,
                        gamePreset.SceneTransitions
                    );
                } else {
                    _logger.Info(this, "Game is not in-progress, sending game info");

                    _netManager.SendGameInfo(
                        player.Id,
                        gamePreset.WarpSceneIndex,
                        gamePreset.WarpTransitionIndex,
                        gamePreset.SceneTransitions
                    );
                }
            }

            if (_settings.Auto && GameState == GameState.WaitingForPlayers) {
                _logger.Info(this, "Game is automatic and we were waiting for players, trying to start game...");

                ProcessAutoGameStart();
            }
        }

        /// <summary>
        /// Callback method for when a player disconnects from the server.
        /// </summary>
        /// <param name="player">The player that disconnects.</param>
        private void OnPlayerDisconnect(IServerPlayer player) {
            _logger.Info(this, $"Player with ID {player.Id} disconnected");

            if (GameState == GameState.InGame) {
                if (!_players.TryGetValue(player.Id, out var tagPlayer)) {
                    _logger.Warn(this, $"Could not find tag player with ID: {player.Id}");
                    return;
                }

                if (tagPlayer.State == PlayerState.Uninfected) {
                    _logger.Info(this, "Player was uninfected, forcefully tagging them");

                    // If the player was uninfected, we call the OnTagged method to check if the game should end
                    OnTagged(player.Id, true);
                } else {
                    // If they were infected, we check whether they were the last infected left
                    // Since we haven't removed them from the dictionary yet, if there is only one
                    // (or less) infected left, then the game should end
                    var numInfected = _players.GetCopy().Values.Count(p => p.State == PlayerState.Infected);

                    _logger.Info(this, $"Player was infected, number of infected left: {numInfected - 1}");

                    if (numInfected <= 1) {
                        // Last infected left, so we end the game
                        OnGameEnd(false);
                    }
                }

                _players.Remove(player.Id);
            }
        }
    }

    /// <summary>
    /// Enumeration for game states.
    /// </summary>
    public enum GameState {
        /// <summary>
        /// There aren't enough players to start a game yet (only in auto-mode).
        /// </summary>
        WaitingForPlayers = 1,

        /// <summary>
        /// The game has not started yet.
        /// </summary>
        PreGame,

        /// <summary>
        /// The countdown for choosing the infected is in progress.
        /// </summary>
        Countdown,

        /// <summary>
        /// The game is in progress.
        /// </summary>
        InGame,

        /// <summary>
        /// The game has just ended.
        /// </summary>
        PostGame
    }
}
