using System.Collections.Generic;
using Hkmp.Api.Client;
using Hkmp.Game;
using Modding;
using ILogger = Hkmp.ILogger;

namespace HkmpTag.Client {
    /// <summary>
    /// The client-side tag manager.
    /// </summary>
    public class ClientTagManager {
        /// <summary>
        /// The logger instance for logging information.
        /// </summary>
        private readonly ILogger _logger;

        /// <summary>
        /// The client API instance.
        /// </summary>
        private readonly IClientApi _clientApi;

        /// <summary>
        /// The network manager instance.
        /// </summary>
        private readonly ClientNetManager _netManager;

        /// <summary>
        /// The icon manager instance.
        /// </summary>
        private readonly IconManager _iconManager;

        /// <summary>
        /// Whether the game is started.
        /// </summary>
        private bool _gameStarted;

        /// <summary>
        /// Whether the local player is tagged.
        /// </summary>
        private bool _isTagged;

        /// <summary>
        /// Integer representing the ID of the last winner.
        /// </summary>
        private int _lastWinner;

        /// <summary>
        /// Construct the tag manager with the client addon and API.
        /// </summary>
        /// <param name="addon">The client addon instance.</param>
        /// <param name="clientApi">The client API.</param>
        public ClientTagManager(TagClientAddon addon, IClientApi clientApi) {
            _logger = addon.Logger;
            _clientApi = clientApi;

            _netManager = new ClientNetManager(addon, clientApi.NetClient);

            _iconManager = new IconManager();
        }

        /// <summary>
        /// Initialize the manager by setting default values and registering callbacks.
        /// </summary>
        public void Initialize() {
            Title.Initialize();
            _iconManager.Initialize();

            // Disable team and skin selection, which is handled by this addon automatically
            _clientApi.UiManager.DisableTeamSelection();
            _clientApi.UiManager.DisableSkinSelection();

            _gameStarted = false;
            _isTagged = false;
            _lastWinner = -1;

            _clientApi.ClientManager.PlayerEnterSceneEvent += OnPlayerEnterScene;

            TagModMenu.StartButtonPressed += SendStartRequest;
            TagModMenu.EndButtonPressed += SendEndRequest;

            _netManager.GameStartedEvent += OnGameStart;
            _netManager.GameEndedEvent += OnGameEnd;
            _netManager.GameInProgressEvent += OnGameInProgress;
            _netManager.PlayerTaggedEvent += OnPlayerTagged;

            On.HeroController.TakeDamage += (orig, self, go, side, amount, type) => {
                orig(self, go, side, amount, type);
                if (!_clientApi.NetClient.IsConnected || !_gameStarted || _isTagged || amount == 0) {
                    return;
                }

                _netManager.SendTagged();

                BecomeInfected();
            };
            ModHooks.TakeHealthHook += damage => 0;

            PlayerData.instance.isInvincible = true;
        }

        /// <summary>
        /// Send a request to start the game with the given number of infected.
        /// </summary>
        /// <param name="numInfected">The number of initial infected.</param>
        private void SendStartRequest(ushort numInfected) {
            if (!_clientApi.NetClient.IsConnected) {
                return;
            }

            _netManager.SendStartRequest(numInfected);
        }

        /// <summary>
        /// Send a request to end the game.
        /// </summary>
        private void SendEndRequest() {
            if (!_clientApi.NetClient.IsConnected) {
                return;
            }

            _netManager.SendEndRequest();
        }

        /// <summary>
        /// Makes the local player an infected.
        /// </summary>
        private void BecomeInfected() {
            LoadoutUtil.BecomeInfected();

            _clientApi.ClientManager.ChangeTeam(Team.Hive);
            _clientApi.ClientManager.ChangeSkin(1);

            _isTagged = true;
        }

        /// <summary>
        /// Makes the local player a normal (non-infected) player.
        /// </summary>
        private void BecomeNormal() {
            LoadoutUtil.BecomeNormal();

            _clientApi.ClientManager.ChangeTeam(Team.Moss);
            _clientApi.ClientManager.ChangeSkin(0);

            _isTagged = false;
        }

        /// <summary>
        /// Callback method for when game start data is received.
        /// </summary>
        /// <param name="packet">The GameStartPacket data.</param>
        private void OnGameStart(GameStartPacket packet) {
            _gameStarted = true;

            _iconManager.Hide();

            if (packet.IsInfected) {
                BecomeInfected();
            } else {
                BecomeNormal();
            }

            var infectedUsernames = new List<string>();
            foreach (var infectedId in packet.InfectedIds) {
                if (_clientApi.ClientManager.TryGetPlayer(infectedId, out var player)) {
                    infectedUsernames.Add(player.Username);
                }
            }

            var usernameString = string.Join(", ", infectedUsernames);

            _logger.Info(this,
                $"Game has started, the following IDs are infected: {string.Join(", ", packet.InfectedIds)}");
            _logger.Info(this, $"  Usernames: {usernameString}");

            if (packet.IsInfected) {
                Title.Show("The game has started, you are the infected!");
                SendMessage($"The game has started, the infected are: {usernameString}");
            } else {
                if (packet.InfectedIds.Count == 1
                    && _clientApi.ClientManager.TryGetPlayer(packet.InfectedIds[0], out var player)) {
                    SendTitleMessage($"The game has started, '{player.Username}' is infected!");
                } else {
                    Title.Show("The game has started!");
                    SendMessage($"The game has started, the infected are: {usernameString}");
                }
            }

            PlayerData.instance.isInvincible = false;
        }

        /// <summary>
        /// Callback method for when game end data is received.
        /// </summary>
        /// <param name="packet">The GameEndPacket data.</param>
        private void OnGameEnd(GameEndPacket packet) {
            _gameStarted = false;

            PlayerData.instance.isInvincible = true;

            if (packet.HasWinner) {
                _logger.Info(this, $"Game has ended, winner: {packet.WinnerId}");
                if (_clientApi.ClientManager.TryGetPlayer(packet.WinnerId, out var player)) {
                    _lastWinner = player.Id;

                    if (player.IsInLocalScene) {
                        _iconManager.ShowOnPlayer(player.PlayerContainer);
                    }

                    SendTitleMessage($"{player.Username} has won the game!");
                } else {
                    _lastWinner = -1;

                    _iconManager.ShowOnPlayer(HeroController.instance.gameObject);

                    SendTitleMessage("You won the game!");
                }

                return;
            }

            _logger.Info(this, "Game has ended without winner");

            SendTitleMessage("The game has ended!");
        }

        /// <summary>
        /// Callback method for when game in progress data is received.
        /// </summary>
        private void OnGameInProgress() {
            _gameStarted = true;

            BecomeInfected();

            _logger.Info(this, "Game is in progress");
            SendTitleMessage("The game is in progress!");

            PlayerData.instance.isInvincible = false;
        }

        /// <summary>
        /// Callback method for when another player tag is received.
        /// </summary>
        /// <param name="packet">The ClientTagPacket data.</param>
        private void OnPlayerTagged(ClientTagPacket packet) {
            if (!_gameStarted) {
                return;
            }

            _logger.Info(this, $"Player {packet.TaggedId} is infected, number of uninfected left: {packet.NumLeft}");

            if (_clientApi.ClientManager.TryGetPlayer(packet.TaggedId, out var player)) {
                SendTitleMessage($"Player '{player.Username}' was infected, {packet.NumLeft} players remain!");
            } else {
                SendTitleMessage($"You are infected, {packet.NumLeft} players remain!");
            }
        }

        /// <summary>
        /// Callback method for when a player enters the local scene.
        /// </summary>
        /// <param name="player">The player that entered the scene.</param>
        private void OnPlayerEnterScene(IClientPlayer player) {
            if (_gameStarted || _lastWinner != player.Id) {
                return;
            }

            _iconManager.ShowOnPlayer(player.PlayerContainer);
        }

        /// <summary>
        /// Show the given message as a title and as a message in chat.
        /// </summary>
        /// <param name="message">The string message.</param>
        private void SendTitleMessage(string message) {
            Title.Show(message);
            _clientApi.UiManager.ChatBox.AddMessage(message);
        }

        /// <summary>
        /// Send a message to the chat.
        /// </summary>
        /// <param name="message">The string message.</param>
        private void SendMessage(string message) {
            _clientApi.UiManager.ChatBox.AddMessage(message);
        }
    }
}
