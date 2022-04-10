using System;
using System.Collections.Generic;
using System.Linq;
using Hkmp;
using Hkmp.Api.Server;
using Hkmp.Concurrency;

namespace HkmpTag.Server {
    /// <summary>
    /// Manager for server-side Tag.
    /// </summary>
    public class ServerTagManager {
        /// <summary>
        /// The time the countdown will take before starting the game in seconds.
        /// </summary>
        private const int CountdownTime = 30;

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
        /// Action to start the game after a countdown.
        /// </summary>
        private DelayedAction _startAction;

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

            _netManager = new ServerNetManager(addon, serverApi.NetServer);
            _transitionManager = new ServerTransitionManager(_logger);

            _random = new Random();

            _lastChosenIds = new List<ushort>();
        }

        /// <summary>
        /// Initializes the tag manager by setting default values and registering commands and callbacks.
        /// </summary>
        public void Initialize() {
            GameState = GameState.PreGame;

            _serverApi.CommandManager.RegisterCommand(new TagCommand(this, _transitionManager));

            _netManager.TaggedEvent += playerId => OnTagged(playerId);

            _serverApi.ServerManager.PlayerConnectEvent += OnPlayerConnect;
            _serverApi.ServerManager.PlayerDisconnectEvent += OnPlayerDisconnect;

            _transitionManager.Initialize();
        }

        /// <summary>
        /// Instruct all players to warp to the given preset and use the transition restrictions. Will provide
        /// feedback by sending a message using the given action.
        /// </summary>
        /// <param name="sendMessageAction">The action to send feedback messages.</param>
        /// <param name="presetName">The name of the preset.</param>
        public void WarpToPreset(Action<string> sendMessageAction, string presetName) {
            if (GameState != GameState.PreGame) {
                const string unableToWarpMsg = "Cannot warp to preset at this moment";
                sendMessageAction.Invoke(unableToWarpMsg);
                _logger.Info(this, unableToWarpMsg);
                return;
            }

            sendMessageAction.Invoke($"Using preset '{presetName}', warping players...");
            _logger.Info(this, $"Using preset '{presetName}' and sending game info");

            _transitionManager.SetPreset(presetName);

            var restriction = _transitionManager.GetTransitionRestrictions();
            _netManager.SendGameInfo(restriction.Item1, restriction.Item2);
        }

        /// <summary>
        /// Start the game with the given number of infected. Will provide feedback by sending a message using
        /// the given action.
        /// </summary>
        /// <param name="sendMessageAction">The action to send feedback messages.</param>
        /// <param name="numInfected">The number of initial infected.</param>
        public void StartGame(Action<string> sendMessageAction, ushort numInfected) {
            if (GameState != GameState.PreGame) {
                const string unableToStartMsg = "Cannot start game at this moment";
                sendMessageAction.Invoke(unableToStartMsg);
                _logger.Info(this, unableToStartMsg);
                return;
            }

            if (!CheckGameStart(sendMessageAction, numInfected)) {
                return;
            }

            var countDownSecondsString = CountdownTime.ToString();
            _serverApi.ServerManager.BroadcastMessage($"Choosing new infected in {countDownSecondsString} seconds!");

            GameState = GameState.Countdown;
            _startAction = new DelayedAction(CountdownTime * 1000, () => {
                if (!CheckGameStart(sendMessageAction, numInfected)) {
                    GameState = GameState.PreGame;
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

                _netManager.SendGameStart(_players.GetCopy().Values.ToList());

                GameState = GameState.InGame;
            });
            _startAction.Start();
        }

        /// <summary>
        /// Checks if the game can be started with the number of online players and given number of infected.
        /// Will provide feedback by sending a message using the given action.
        /// </summary>
        /// <param name="sendMessageAction">The action to send feedback messages.</param>
        /// <param name="numInfected">The number of initial infected.</param>
        /// <returns>true if the game can be started; otherwise false.</returns>
        private bool CheckGameStart(Action<string> sendMessageAction, ushort numInfected) {
            var serverPlayers = _serverApi.ServerManager.Players;

            // We cannot start without at least 3 players
            if (serverPlayers.Count < 3) {
                const string notEnoughPlayersMsg = "Could not start game with less than 3 players";
                sendMessageAction.Invoke(notEnoughPlayersMsg);
                _logger.Info(this, notEnoughPlayersMsg);
                return false;
            }

            // We cannot start the game with 0 or too many players
            if (numInfected < 1 || numInfected >= serverPlayers.Count - 1) {
                const string invalidNumPlayersMsg = "Could not start game with invalid number of players";
                sendMessageAction.Invoke(invalidNumPlayersMsg);
                _logger.Info(this, invalidNumPlayersMsg);
                return false;
            }

            return true;
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
                // If the game was in progress, we end it
                _netManager.SendGameEnd(false);
            } else if (GameState == GameState.Countdown) {
                // If the countdown was in progress, we cancel it
                _startAction.Stop();
                
                const string stoppedCountdownMsg = "Stopped start countdown";
                sendMessageAction.Invoke(stoppedCountdownMsg);
                _logger.Info(this, stoppedCountdownMsg);
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
                // If the game is not over yet, send the tag to all players
                _netManager.SendTag(playerId, (ushort)numUninfected, disconnect);
                return;
            }

            if (numUninfected == 1) {
                _netManager.SendGameEnd(
                    true,
                    players.Values.First(p => p.State == PlayerState.Uninfected).Id
                );
            } else if (numUninfected == 0) {
                _netManager.SendGameEnd(false);
            }

            GameState = GameState.PreGame;
        }

        /// <summary>
        /// Callback method for when a player connects to the server.
        /// </summary>
        /// <param name="player">The player that connects.</param>
        private void OnPlayerConnect(IServerPlayer player) {
            _logger.Info(this, $"Player with ID {player.Id} connected");

            if (GameState == GameState.InGame) {
                _players[player.Id] = new ServerTagPlayer {
                    Id = player.Id,
                    State = PlayerState.Infected
                };

                var transitionRestriction = _transitionManager.GetTransitionRestrictions();
                
                _netManager.SendGameInProgress(
                    player.Id,
                    transitionRestriction.Item1,
                    transitionRestriction.Item2
                );
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
                    // If the player was uninfected, we call the OnTagged method to check if the game should end
                    OnTagged(player.Id, true);
                } else {
                    // If they were infected, we check whether they were the last infected left
                    // Since we haven't removed them from the dictionary yet, if there is only one
                    // (or less) infected left, then the game should end
                    var numInfected = _players.GetCopy().Values.Count(p => p.State == PlayerState.Infected);
                    if (numInfected <= 1) {
                        // Last infected left, so we end the game
                        GameState = GameState.PreGame;
                        _netManager.SendGameEnd(false);
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
        InGame
    }
}
