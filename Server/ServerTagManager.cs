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
        /// The instance of random used to decide the initial infected.
        /// </summary>
        private readonly Random _random;

        /// <summary>
        /// Whether the game has started.
        /// </summary>
        private bool _gameStarted;
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

            _random = new Random();

            _lastChosenIds = new List<ushort>();
        }

        /// <summary>
        /// Initializes the tag manager by setting default values and registering commands and callbacks.
        /// </summary>
        public void Initialize() {
            _gameStarted = false;
            
            _serverApi.CommandManager.RegisterCommand(new TagCommand(this));

            _netManager.StartRequestEvent += OnStartRequest;
            _netManager.EndRequestEvent += OnEndRequest;
            _netManager.TaggedEvent += playerId => OnTagged(playerId);

            _serverApi.ServerManager.PlayerConnectEvent += OnPlayerConnect;
            _serverApi.ServerManager.PlayerDisconnectEvent += OnPlayerDisconnect;
        }

        /// <summary>
        /// Callback method for when a start request is received.
        /// </summary>
        /// <param name="id">The ID of the player that sent the request.</param>
        /// <param name="packet">The packet data.</param>
        private void OnStartRequest(ushort id, StartRequestPacket packet) {
            _logger.Info(this, $"Received start request from {id}, numInfected: {packet.NumInfected}");
            
            if (!_serverApi.ServerManager.TryGetPlayer(id, out var player)) {
                _logger.Warn(this, "Could not find player that sent start request");
                return;
            }

            if (!player.IsAuthorized) {
                _serverApi.ServerManager.SendMessage(id, "You are not authorized to do this");
                _logger.Info(this, $"Player with ID {id} is not authorized to start the game");
                return;
            }
            
            StartGame(
                s => _serverApi.ServerManager.SendMessage(id, s), 
                packet.NumInfected
            );
        }

        /// <summary>
        /// Start the game with the given number of infected. Will provide feedback by sending a message using
        /// the given action.
        /// </summary>
        /// <param name="sendMessageAction">The action to send feedback messages.</param>
        /// <param name="numInfected">The number of initial infected.</param>
        public void StartGame(Action<string> sendMessageAction, ushort numInfected) {
            if (_gameStarted) {
                const string alreadyStartedMsg = "Game is already started";
                sendMessageAction.Invoke(alreadyStartedMsg);
                _logger.Info(this, alreadyStartedMsg);
                return;
            }

            // We cannot start without at least 3 players
            if (_serverApi.ServerManager.Players.Count < 3) {
                const string notEnoughPlayersMsg = "Could not start game with less than 3 players";
                sendMessageAction.Invoke(notEnoughPlayersMsg);
                _logger.Info(this, notEnoughPlayersMsg);
                return;
            }

            var serverPlayers = _serverApi.ServerManager.Players;

            // We cannot start the game with 0 or too many players
            if (numInfected < 1 || numInfected >= serverPlayers.Count - 1) {
                const string invalidNumPlayersMsg = "Could not start game with invalid number of players";
                sendMessageAction.Invoke(invalidNumPlayersMsg);
                _logger.Info(this, invalidNumPlayersMsg);
                return;
            }

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

            _gameStarted = true;
        }

        /// <summary>
        /// Callback method for when an end request is received.
        /// </summary>
        /// <param name="id">The ID of the player that sent the request.</param>
        private void OnEndRequest(ushort id) {
            _logger.Info(this, $"Received end request from {id}");
            
            if (!_serverApi.ServerManager.TryGetPlayer(id, out var player)) {
                _logger.Warn(this, "Could not find player that sent start request");
                return;
            }

            if (!player.IsAuthorized) {
                _serverApi.ServerManager.SendMessage(id, "You are not authorized to do this");
                _logger.Info(this, $"Player with ID {id} is not authorized to end the game");
                return;
            }
            
            EndGame(s => _serverApi.ServerManager.SendMessage(id, s));
        }
        
        /// <summary>
        /// Ends the game and provides feedback by sending a message using the given action.
        /// </summary>
        /// <param name="sendMessageAction">The action to send feedback messages.</param>
        public void EndGame(Action<string> sendMessageAction) {
            if (!_gameStarted) {
                const string notStartedMsg = "Game is not started";
                sendMessageAction.Invoke(notStartedMsg);
                _logger.Info(this, notStartedMsg);
                return;
            }

            _gameStarted = false;

            _netManager.SendGameEnd(false);
        }

        /// <summary>
        /// Callback method for when a player is tagged.
        /// </summary>
        /// <param name="playerId">The ID of the tagged player.</param>
        /// <param name="disconnect">Whether they were tagged due to disconnect.</param>
        private void OnTagged(ushort playerId, bool disconnect = false) {
            _logger.Info(this, $"Received tag from: {playerId}");

            if (!_gameStarted) {
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

            _gameStarted = false;
        }

        /// <summary>
        /// Callback method for when a player connects to the server.
        /// </summary>
        /// <param name="player">The player that connects.</param>
        private void OnPlayerConnect(IServerPlayer player) {
            _logger.Info(this, $"Player with ID {player.Id} connected");

            if (_gameStarted) {
                _players[player.Id] = new ServerTagPlayer {
                    Id = player.Id,
                    State = PlayerState.Infected
                };
                _netManager.SendGameInProgress(player.Id);
            }
        }

        /// <summary>
        /// Callback method for when a player disconnects from the server.
        /// </summary>
        /// <param name="player">The player that disconnects.</param>
        private void OnPlayerDisconnect(IServerPlayer player) {
            _logger.Info(this, $"Player with ID {player.Id} disconnected");

            if (_gameStarted) {
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
                        _gameStarted = false;
                        _netManager.SendGameEnd(false);
                    }
                }

                _players.Remove(player.Id);
            }
        }
    }
}
