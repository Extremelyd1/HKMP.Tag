using System.Collections.Generic;
using System.Diagnostics;
using Hkmp.Api.Client;
using Hkmp.Game;
using Modding;
using UnityEngine;
using UnityEngine.SceneManagement;
using ILogger = Hkmp.Logging.ILogger;

namespace HkmpTag.Client {
    /// <summary>
    /// The client-side tag manager.
    /// </summary>
    public class ClientTagManager {
        /// <summary>
        /// The time in milliseconds for which non-infected players are invulnerable after round start.
        /// </summary>
        private const int RoundStartInvulnerabilityTime = 1000;
        
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
        /// The patch manager instance.
        /// </summary>
        private readonly PatchManager _patchManager;

        /// <summary>
        /// Whether the game is started.
        /// </summary>
        private bool _gameStarted;

        /// <summary>
        /// Whether the local player is tagged.
        /// </summary>
        private bool _isTagged;

        /// <summary>
        /// Packet of last game end information.
        /// </summary>
        private GameEndPacket _lastGameEnd;

        /// <summary>
        /// Stopwatch to keep track of time since round started to apply invulnerability for a brief period.
        /// </summary>
        private readonly Stopwatch _roundStartStopwatch;

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

            _patchManager = new PatchManager(_logger);

            _roundStartStopwatch = new Stopwatch();
        }

        /// <summary>
        /// Initialize the manager by setting default values and registering callbacks.
        /// </summary>
        public void Initialize() {
            Title.Initialize();
            _iconManager.Initialize();
            _transitionManager.Initialize();

            Enable();

            _gameStarted = false;
            _isTagged = false;
            _lastGameEnd = new GameEndPacket();

            _netManager.GameInfoEvent += OnGameInfo;
            _netManager.GameStartedEvent += OnGameStart;
            _netManager.GameEndedEvent += OnGameEnd;
            _netManager.GameInProgressEvent += OnGameInProgress;
            _netManager.PlayerTaggedEvent += OnPlayerTagged;
        }

        /// <summary>
        /// Enable the functionality of tag.
        /// </summary>
        public void Enable() {
            _transitionManager.Enable();
            _saveManager.Enable();
            _patchManager.Enable();
            
            _clientApi.UiManager.DisableTeamSelection();
            _clientApi.UiManager.DisableSkinSelection();
            
            _clientApi.ClientManager.PlayerEnterSceneEvent += OnPlayerEnterScene;
            
            ModHooks.TakeDamageHook += OnTakeDamage;
            ModHooks.AfterTakeDamageHook += OnAfterTakeDamage;
            ModHooks.TakeHealthHook += OnTakeHealth;
            ModHooks.OnEnableEnemyHook += OnEnableEnemy;

            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnActiveSceneChanged;

            if (GameManager.instance.IsGameplayScene()) {
                // Since we enabled Tag while in a save, we need to go back to the main menu to load the completed save
                UIManager.instance.UIReturnToMainMenu();
            }
        }

        /// <summary>
        /// Disable the functionality of tag.
        /// </summary>
        public void Disable() {
            _iconManager.Hide();
            _transitionManager.Disable();
            _saveManager.Disable();
            _patchManager.Disable();

            _clientApi.UiManager.EnableTeamSelection();
            _clientApi.UiManager.EnableSkinSelection();
            
            _clientApi.ClientManager.PlayerEnterSceneEvent -= OnPlayerEnterScene;
            
            ModHooks.TakeDamageHook -= OnTakeDamage;
            ModHooks.AfterTakeDamageHook -= OnAfterTakeDamage;
            ModHooks.TakeHealthHook -= OnTakeHealth;
            ModHooks.OnEnableEnemyHook -= OnEnableEnemy;
            
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        /// <summary>
        /// Method on the TakeDamageHook to prevent all (non-hazard) damage when the game is not started.
        /// </summary>
        /// <param name="type">The damage type as an int.</param>
        /// <param name="damage">The original damage that would be taken.</param>
        /// <returns>The new damage that should be taken.</returns>
        private int OnTakeDamage(ref int type, int damage) {
            // If the damage type is any other than combat, we return the normal damage
            if (type != 1) {
                return damage;
            }

            // If we are not connected or the game is not started, we ignore all damage
            if (!_clientApi.NetClient.IsConnected || !_gameStarted) {
                return 0;
            }

            // If the player is tagged already, they take normal damage
            if (_isTagged) {
                return damage;
            }

            // If the round start stopwatch is running, we need to check for invulnerability
            if (_roundStartStopwatch.IsRunning) {
                if (_roundStartStopwatch.ElapsedMilliseconds < RoundStartInvulnerabilityTime) {
                    // Still invulnerable
                    return 0;
                }
                
                // No longer invulnerable, so we can also stop the stopwatch
                _roundStartStopwatch.Stop();
                return damage;
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
        /// Method to prevent the player losing any health.
        /// </summary>
        /// <param name="damage">The damage that was going to be taken.</param>
        /// <returns>The new damage, in this case 0.</returns>
        private int OnTakeHealth(int damage) {
            return 0;
        }
        
        /// <summary>
        /// Method to prevent enemies from existing.
        /// </summary>
        /// <param name="enemy">The enemy that is checked whether it is dead.</param>
        /// <param name="dead">Whether this enemy was already dead.</param>
        /// <returns>Whether the enemy should be dead, in this case true.</returns>
        private bool OnEnableEnemy(GameObject enemy, bool dead) {
            return true;
        }
        
        /// <summary>
        /// Callback method for when the active scene changes.
        /// </summary>
        /// <param name="oldScene">The old scene.</param>
        /// <param name="newScene">The new scene.</param>
        private void OnActiveSceneChanged(Scene oldScene, Scene newScene) {
            if (GameManager.instance.IsNonGameplayScene() && _clientApi.NetClient.IsConnected) {
                _logger.Info("Changed to non-gameplay scene, disconnecting from server");

                _clientApi.ClientManager.Disconnect();
            }
            
            if (!_gameStarted && _lastGameEnd.IsWinner) {
                _iconManager.ShowOnPlayer(HeroController.instance.gameObject);
            }
        }

        /// <summary>
        /// Makes the local player an infected.
        /// </summary>
        private void BecomeInfected() {
            LoadoutManager.BecomeInfected();

            _clientApi.ClientManager.ChangeTeam(Team.Hive);
            _clientApi.ClientManager.ChangeSkin(1);

            _isTagged = true;
        }

        /// <summary>
        /// Makes the local player a normal (non-infected) player.
        /// </summary>
        private void BecomeNormal() {
            LoadoutManager.BecomeNormal();

            _clientApi.ClientManager.ChangeTeam(Team.Moss);
            _clientApi.ClientManager.ChangeSkin(0);

            _isTagged = false;
        }

        /// <summary>
        /// Callback method for when game info is received.
        /// </summary>
        /// <param name="packet">The GameInfoPacket data.</param>
        private void OnGameInfo(GameInfoPacket packet) {
            LoadoutManager.Loadouts = packet.Loadouts;

            _transitionManager.OnReceiveGameInfo(packet.Preset.SceneTransitions);
            _transitionManager.WarpToScene(packet.Preset.WarpSceneIndex, packet.Preset.WarpTransitionIndex);
        }

        /// <summary>
        /// Callback method for when game start data is received.
        /// </summary>
        /// <param name="packet">The GameStartPacket data.</param>
        private void OnGameStart(GameStartPacket packet) {
            _gameStarted = true;

            _iconManager.Hide();

            _roundStartStopwatch.Restart();

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

            _logger.Info($"Game has started, the following IDs are infected: {string.Join(", ", packet.InfectedIds)}");
            _logger.Info($"  Usernames: {usernameString}");

            if (packet.IsInfected) {
                Title.Show("The game has started, you are infected!");

                var msg = "The game has started, you are infected";
                if (packet.InfectedIds.Count <= 1) {
                    msg += "!";
                } else {
                    msg += $" with: {usernameString}.";
                }

                SendMessage(msg);
            } else {
                if (
                    packet.InfectedIds.Count == 1 && 
                    _clientApi.ClientManager.TryGetPlayer(packet.InfectedIds[0], out var player)
                ) {
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
            _lastGameEnd = packet;

            if (packet.HasWinner) {
                _logger.Info($"Game has ended, winner: {packet.WinnerId}");
                if (packet.IsWinner) {
                    _iconManager.ShowOnPlayer(HeroController.instance.gameObject);

                    SendTitleMessage("You won the game!");
                    return;
                }
                
                if (_clientApi.ClientManager.TryGetPlayer(packet.WinnerId, out var player)) {
                    if (player.IsInLocalScene) {
                        _iconManager.ShowOnPlayer(player.PlayerContainer);
                    }

                    SendTitleMessage($"{player.Username} has won the game!");
                    return;
                }
            }

            _logger.Info("Game has ended without winner");

            SendTitleMessage("The game has ended!");
        }

        /// <summary>
        /// Callback method for when game in progress data is received.
        /// </summary>
        /// <param name="packet">The game info packet for the game that is in progress.</param>
        private void OnGameInProgress(GameInfoPacket packet) {
            _gameStarted = true;

            LoadoutManager.Loadouts = packet.Loadouts;

            _transitionManager.OnReceiveGameInfo(packet.Preset.SceneTransitions);
            _transitionManager.WarpToScene(packet.Preset.WarpSceneIndex, packet.Preset.WarpTransitionIndex);
            
            BecomeInfected();

            _logger.Info("Game is in progress");
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
                _logger.Info($"Local player is infected, number of uninfected left: {packet.NumLeft}");

                SendTitleMessage($"You are infected, {packet.NumLeft} players remain!");
                return;
            }

            _logger.Info($"Player {packet.TaggedId} is infected, number of uninfected left: {packet.NumLeft}");

            if (_clientApi.ClientManager.TryGetPlayer(packet.TaggedId, out var player)) {
                SendTitleMessage($"Player '{player.Username}' was infected, {packet.NumLeft} players remain!");
            } else {
                _logger.Warn($"Could not find player with ID: {packet.TaggedId}");
            }
        }

        /// <summary>
        /// Callback method for when a player enters the local scene.
        /// </summary>
        /// <param name="player">The player that entered the scene.</param>
        private void OnPlayerEnterScene(IClientPlayer player) {
            if (
                _gameStarted || 
                !_lastGameEnd.HasWinner || 
                _lastGameEnd.IsWinner || 
                _lastGameEnd.WinnerId != player.Id
            ) {
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
