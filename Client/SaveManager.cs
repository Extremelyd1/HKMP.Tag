using System;
using System.IO;
using System.Reflection;
using Modding;
using Newtonsoft.Json;
using ILogger = Hkmp.ILogger;

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

        /// <summary>
        /// The save game data that was loaded from the save file.
        /// </summary>
        private SaveGameData _loadedSaveGameData;

        public SaveManager(ILogger logger) {
            _logger = logger;
        }

        /// <summary>
        /// Initialize the save manager by registering callback methods for the save game load and save hooks.
        /// </summary>
        public void Initialize() {
            ModHooks.NewGameHook += OnNewGame;
            ModHooks.AfterSavegameLoadHook += OnAfterSavegameLoad;
            ModHooks.BeforeSavegameSaveHook += OnBeforeSavegameSave;
        }

        /// <summary>
        /// Callback method for when a new game is started.
        /// </summary>
        private void OnNewGame() {
            _logger.Info(this, "Started new game, overwriting save game data");

            LoadCompletedSave();
        }

        /// <summary>
        /// Callback method for just before a save game is saved.
        /// </summary>
        /// <param name="saveGameData">The SaveGameData instance.</param>
        private void OnBeforeSavegameSave(SaveGameData saveGameData) {
            _logger.Info(this, "Restoring loaded save game data");

            // Reset the player data and scene data of the stored save game data
            if (_loadedSaveGameData != null) {
                saveGameData.playerData = _loadedSaveGameData.playerData;
                saveGameData.sceneData = _loadedSaveGameData.sceneData;
            }
        }

        /// <summary>
        /// Callback method for after the save game is loaded.
        /// </summary>
        /// <param name="saveGameData">The SaveGameData instance.</param>
        private void OnAfterSavegameLoad(SaveGameData saveGameData) {
            _logger.Info(this, "Storing loaded save game data");

            // Store the save game data that was loaded from the save file
            _loadedSaveGameData = saveGameData;

            // Now load the completed save file that is embedded as resource
            LoadCompletedSave();
        }

        /// <summary>
        /// Load a completed save by deserializing a completed save JSON file and overwriting player data
        /// and scene data.
        /// </summary>
        private void LoadCompletedSave() {
            var saveResStream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("HkmpTag.Client.Resource.completed_save.json");
            if (saveResStream == null) {
                _logger.Error(this, "Resource stream for save file is null");
                return;
            }

            var saveFileString = new StreamReader(saveResStream).ReadToEnd();

            // Deserialize the JSON file to a SaveGameData instance
            SaveGameData completedSaveGameData;
            try {
                completedSaveGameData = JsonConvert.DeserializeObject<SaveGameData>(saveFileString);
            } catch (Exception e) {
                _logger.Error(this, $"Could not deserialize completed save file, {e.GetType()}, {e.Message}");
                return;
            }

            // Overwrite the player data and scene data instances
            var gameManager = GameManager.instance;
            gameManager.playerData = PlayerData.instance = completedSaveGameData?.playerData;
            gameManager.sceneData = SceneData.instance = completedSaveGameData?.sceneData;
        }
    }
}
