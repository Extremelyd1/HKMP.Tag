using System;
using System.Collections.Generic;
using Hkmp.Api.Client;
using Hkmp.Game;
using Hkmp.Util;
using HutongGames.PlayMaker;
using Modding;
using UnityEngine.SceneManagement;
using ILogger = Hkmp.ILogger;
using Object = UnityEngine.Object;

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
        /// The transition manager instance.
        /// </summary>
        private readonly ClientTransitionManager _transitionManager;

        /// <summary>
        /// The save manager instance.
        /// </summary>
        private readonly SaveManager _saveManager;

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
            _transitionManager = new ClientTransitionManager(_logger);
            _saveManager = new SaveManager(_logger);
        }

        /// <summary>
        /// Initialize the manager by setting default values and registering callbacks.
        /// </summary>
        public void Initialize() {
            Title.Initialize();
            _iconManager.Initialize();
            _transitionManager.Initialize();
            _saveManager.Initialize();

            // Disable team and skin selection, which is handled by this addon automatically
            _clientApi.UiManager.DisableTeamSelection();
            _clientApi.UiManager.DisableSkinSelection();

            _gameStarted = false;
            _isTagged = false;
            _lastWinner = -1;

            _clientApi.ClientManager.PlayerEnterSceneEvent += OnPlayerEnterScene;

            _netManager.GameInfoEvent += OnGameInfo;
            _netManager.GameStartedEvent += OnGameStart;
            _netManager.GameEndedEvent += OnGameEnd;
            _netManager.GameInProgressEvent += OnGameInProgress;
            _netManager.PlayerTaggedEvent += OnPlayerTagged;

            ModHooks.TakeDamageHook += OnTakeDamage;
            ModHooks.AfterTakeDamageHook += OnAfterTakeDamage;
            ModHooks.TakeHealthHook += damage => 0;
            ModHooks.OnEnableEnemyHook += (enemy, dead) => true;

            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        /// <summary>
        /// Method on the TakeDamageHook to prevent all (non-hazard) damage when the game is not started.
        /// </summary>
        /// <param name="type">The damage type as an int.</param>
        /// <param name="damage">The original damage that would be taken.</param>
        /// <returns>The new damage that should be taken.</returns>
        private int OnTakeDamage(ref int type, int damage) {
            if (type == 1 && (!_clientApi.NetClient.IsConnected || !_gameStarted)) {
                return 0;
            }

            return damage;
        }

        /// <summary>
        /// Method on the AfterTakeDamageHook to check when the player is tagged.
        /// </summary>
        /// <param name="type">The damage type as an int.</param>
        /// <param name="amount">The original damage that would be taken.</param>
        /// <returns>The new damage that should be taken.</returns>
        private int OnAfterTakeDamage(int type, int amount) {
            if (!_clientApi.NetClient.IsConnected || !_gameStarted || _isTagged) {
                return amount;
            }

            _netManager.SendTagged();

            BecomeInfected();

            return amount;
        }

        /// <summary>
        /// Callback method for when the active scene changes.
        /// </summary>
        /// <param name="oldScene">The old scene.</param>
        /// <param name="newScene">The new scene.</param>
        private void OnActiveSceneChanged(Scene oldScene, Scene newScene) {
            foreach (var fsm in Object.FindObjectsOfType<PlayMakerFSM>()) {
                // Find FSMs with Bench Control and disable starting and sitting on them
                if (fsm.Fsm.Name.Equals("Bench Control")) {
                    _logger.Info(this, "Found FSM with Bench Control, patching...");

                    fsm.InsertMethod("Pause 2", 1, () => { PlayerData.instance.SetBool("atBench", false); });

                    var checkStartState2 = fsm.GetState("Check Start State 2");
                    var pause2State = fsm.GetState("Pause 2");
                    checkStartState2.GetTransition(1).ToFsmState = pause2State;

                    var checkStartState = fsm.GetState("Check Start State");
                    var idleStartPauseState = fsm.GetState("Idle Start Pause");
                    checkStartState.GetTransition(1).ToFsmState = idleStartPauseState;

                    var idleState = fsm.GetState("Idle");
                    idleState.Actions = new[] { idleState.Actions[0] };
                }

                // Find FSMs with Stag Control and prevent stag travelling
                if (fsm.Fsm.Name.Equals("Stag Control")) {
                    _logger.Info(this, "Found FSM with Stag Control, patching...");

                    var idleState = fsm.GetState("Idle");
                    idleState.Transitions = Array.Empty<FsmTransition>();
                }
            }
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
        /// Callback method for when game info is received.
        /// </summary>
        /// <param name="packet">The GameInfoPacket data.</param>
        private void OnGameInfo(GameInfoPacket packet) {
            _transitionManager.OnReceiveGameInfo(packet.RestrictedTransitions);
            _transitionManager.WarpToScene(packet.WarpIndex);
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
        }

        /// <summary>
        /// Callback method for when game end data is received.
        /// </summary>
        /// <param name="packet">The GameEndPacket data.</param>
        private void OnGameEnd(GameEndPacket packet) {
            _gameStarted = false;

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
        /// <param name="packet">The game info packet for the game that is in progress.</param>
        private void OnGameInProgress(GameInfoPacket packet) {
            _gameStarted = true;

            BecomeInfected();

            _transitionManager.OnReceiveGameInfo(packet.RestrictedTransitions);
            _transitionManager.WarpToScene(packet.WarpIndex);

            _logger.Info(this, "Game is in progress");
            SendTitleMessage("The game is in progress!");
        }

        /// <summary>
        /// Callback method for when another player tag is received.
        /// </summary>
        /// <param name="packet">The ClientTagPacket data.</param>
        private void OnPlayerTagged(ClientTagPacket packet) {
            if (!_gameStarted) {
                return;
            }

            if (packet.WasTagged) {
                _logger.Info(this, $"Local player is infected, number of uninfected left: {packet.NumLeft}");

                SendTitleMessage($"You are infected, {packet.NumLeft} players remain!");
                return;
            }

            _logger.Info(this, $"Player {packet.TaggedId} is infected, number of uninfected left: {packet.NumLeft}");

            if (_clientApi.ClientManager.TryGetPlayer(packet.TaggedId, out var player)) {
                SendTitleMessage($"Player '{player.Username}' was infected, {packet.NumLeft} players remain!");
            } else {
                _logger.Warn(this, $"Could not find player with ID: {packet.TaggedId}");
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
