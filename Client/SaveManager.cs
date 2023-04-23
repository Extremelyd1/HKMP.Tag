using System;
using System.IO;
using System.Reflection;
using Modding;
using Newtonsoft.Json;
using ILogger = Hkmp.Logging.ILogger;

namespace HkmpTag.Client {
    /// <summary>
    /// Class that manages saving and loading save game data for the Tag game.
    /// It will make sure that no matter what save file is loaded, it will use a completed save and
    /// not commit the completed save to the save file, so the save file is untouched.
    /// </summary>
    public class SaveManager {
        /// <summary>
        /// The logger to log important information.
        /// </summary>
        private readonly ILogger _logger;

        public SaveManager(ILogger logger) {
            _logger = logger;
        }

        /// <summary>
        /// Enable the save manager.
        /// </summary>
        public void Enable() {
            On.UIManager.UIMainStartGame += OnUiMainStartGame;
            On.GameManager.SaveGame += OnSaveGame;
        }

        /// <summary>
        /// Disable the save manager.
        /// </summary>
        public void Disable() {
            On.UIManager.UIMainStartGame -= OnUiMainStartGame;
            On.GameManager.SaveGame -= OnSaveGame;
        }

        /// <summary>
        /// Called when the user click "Start game" in the main menu. Will load the completed save and continue
        /// the game, bypassing the save selection. 
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The UIManager instance.</param>
        private void OnUiMainStartGame(On.UIManager.orig_UIMainStartGame orig, UIManager self) {
            _logger.Debug("User clicked start game, loading completed save");
            
            LoadCompletedSave();
            
            GameManager.instance.ContinueGame();
        }
        
        /// <summary>
        /// Called when the game is saved. Will remove game saving as long as the Tag addon is enabled.
        /// </summary>
        /// <param name="orig">The original method.</param>
        /// <param name="self">The GameManager instance.</param>
        private void OnSaveGame(On.GameManager.orig_SaveGame orig, GameManager self) {
        }

        /// <summary>
        /// Load a completed save by deserializing a completed save JSON file and overwriting player data
        /// and scene data.
        /// </summary>
        private void LoadCompletedSave() {
            var saveResStream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("HkmpTag.Client.Resource.completed_save.json");
            if (saveResStream == null) {
                _logger.Error("Resource stream for save file is null");
                return;
            }

            var saveFileString = new StreamReader(saveResStream).ReadToEnd();

            // Deserialize the JSON file to a SaveGameData instance
            SaveGameData completedSaveGameData;
            try {
                completedSaveGameData = JsonConvert.DeserializeObject<SaveGameData>(saveFileString);
            } catch (Exception e) {
                _logger.Error($"Could not deserialize completed save file, {e.GetType()}, {e.Message}");
                return;
            }

            // Overwrite the player data and scene data instances
            var gameManager = GameManager.instance;
            gameManager.playerData = PlayerData.instance = completedSaveGameData?.playerData;
            gameManager.sceneData = SceneData.instance = completedSaveGameData?.sceneData;
        }
    }
}
