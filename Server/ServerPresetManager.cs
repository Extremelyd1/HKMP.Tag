using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hkmp.Logging;
using Newtonsoft.Json;

namespace HkmpTag.Server {
    /// <summary>
    /// Manager class for server-side game presets.
    /// </summary>
    /// <inheritdoc />
    public class ServerPresetManager : TransitionManager {
        /// <summary>
        /// The file name of the game presets file.
        /// </summary>
        private const string PresetFileName = "tag_game_presets.json";

        /// <summary>
        /// The full path of the assembly directory.
        /// </summary>
        private string AssemblyDirPath => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        /// <summary>
        /// The random instance.
        /// </summary>
        private readonly Random _random;

        /// <summary>
        /// Dictionary mapping preset names to their game preset instances.
        /// </summary>
        private readonly Dictionary<string, GamePreset> _gamePresets;

        /// <summary>
        /// The default fallback loadouts for when a preset does not have a loadout defined.
        /// </summary>
        private Loadouts _defaultLoadouts;

        /// <summary>
        /// Boolean indicating whether the preset file has changed.
        /// </summary>
        private bool _presetFileChanged;

        /// <summary>
        /// The current preset or null if no preset is set.
        /// </summary>
        private GamePreset _currentPreset;

        /// <summary>
        /// The current loadout for the preset. Uses fallback default loadout if either current preset or its
        /// loadout is null.
        /// </summary>
        public Loadouts CurrentLoadouts => _currentPreset?.Loadouts ?? _defaultLoadouts;

        /// <summary>
        /// The list of condensed game presets currently used.
        /// </summary>
        private List<CondensedGamePreset> _currentCondensedPresets;

        /// <inheritdoc cref="_currentCondensedPresets"/>
        public List<CondensedGamePreset> CondensedGamePresets =>
            new List<CondensedGamePreset>(_currentCondensedPresets);

        public ServerPresetManager(ILogger logger, Random random) : base(logger) {
            _random = random;

            _gamePresets = new Dictionary<string, GamePreset>();
            _currentCondensedPresets = new List<CondensedGamePreset>();
        }

        /// <inheritdoc />
        public override void Initialize() {
            base.Initialize();

            // Setup file watcher
            var fileWatcher = new FileSystemWatcher(AssemblyDirPath);
            fileWatcher.Filter = PresetFileName;
            fileWatcher.IncludeSubdirectories = false;
            fileWatcher.Changed += OnFileChanged;
            fileWatcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Load the scene transitions and the preset file.
        /// </summary>
        protected override void LoadSceneTransitions() {
            base.LoadSceneTransitions();

            Logger.Info("Loading transition restriction presets");

            LoadPresets();
        }

        /// <summary>
        /// Callback for when the preset file changes.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The event args object.</param>
        private void OnFileChanged(object sender, FileSystemEventArgs e) {
            _presetFileChanged = true;
        }

        /// <summary>
        /// Load the preset file.
        /// </summary>
        private void LoadPresets(bool reset = false) {
            if (AssemblyDirPath == null) {
                return;
            }

            var filePath = Path.Combine(AssemblyDirPath, PresetFileName);
            if (!File.Exists(filePath)) {
                Logger.Info("No preset file found");
                return;
            }

            string fileContents;

            try {
                fileContents = File.ReadAllText(filePath);
            } catch (Exception e) {
                Logger.Info($"Could not read preset file: {e.GetType()}, {e.Message}");
                return;
            }

            var gamePresets = JsonConvert.DeserializeObject<GamePresets>(fileContents);
            if (gamePresets == null) {
                Logger.Warn("Could not read game presets file");
                return;
            }

            if (gamePresets.DefaultLoadouts == null) {
                Logger.Warn("Default loadout in game presets file does not exist");
                return;
            }

            if (gamePresets.Presets == null) {
                Logger.Warn("No presets are defined in the game presets file");
                return;
            }

            _defaultLoadouts = gamePresets.DefaultLoadouts;

            if (reset) {
                _gamePresets.Clear();
            }

            var loadedPresetNames = new List<string>();

            foreach (var preset in gamePresets.Presets) {
                var name = preset.Name;

                if (!_gamePresets.ContainsKey(name)) {
                    loadedPresetNames.Add(name);
                    _gamePresets[name] = preset;
                } else {
                    Logger.Warn($"Transition restriction preset with name '{name}' was already defined");
                }
            }

            if (loadedPresetNames.Count == 0) {
                Logger.Warn("Could not load any transition restriction presets!");
            } else {
                Logger.Info($"Loaded transition restriction presets: {string.Join(", ", loadedPresetNames)}");
            }
        }

        /// <summary>
        /// Set the current preset to the one with the given name. Will check whether the name is of a valid
        /// preset.
        /// </summary>
        /// <param name="name">The name of the preset.</param>
        public void SetPreset(string name) {
            if (!_gamePresets.TryGetValue(name, out var preset)) {
                Logger.Warn($"Tried to set preset '{name}', while it did not exist");
                return;
            }

            _currentPreset = preset;
            CacheCondensedGamePresets();
        }

        /// <summary>
        /// Get the names of all presets.
        /// </summary>
        /// <returns>A string array containing all preset names.</returns>
        public string[] GetPresetNames() {
            return _gamePresets.Keys.ToArray();
        }

        /// <summary>
        /// Set a random preset to be used that is not the one currently used. Will keep in mind how many
        /// players are online and pick a preset that is suitable.
        /// </summary>
        /// <param name="numPlayers">The number of players currently online.</param>
        public bool SetRandomPreset(int numPlayers) {
            // If the preset file changed, try to load the preset file again
            if (_presetFileChanged) {
                LoadPresets(true);

                _presetFileChanged = false;
            }

            var presets = _gamePresets.Values
                // Filter out the current preset
                .Where(p => p != _currentPreset)
                // Filter out the presets that have a max number of players
                .Where(p => !p.MaxPlayers.HasValue || p.MaxPlayers >= numPlayers)
                // Filter out the presets that have a min number of players
                .Where(p => !p.MinPlayers.HasValue || p.MinPlayers <= numPlayers)
                .ToList();

            // If there aren't any presets left after filtering, we return false
            if (presets.Count < 1) {
                return false;
            }

            var randomIndex = _random.Next(presets.Count);
            var randomPreset = presets[randomIndex];

            Logger.Info($"Picked random preset '{randomPreset.Name}'");

            _currentPreset = randomPreset;
            CacheCondensedGamePresets();
            return true;
        }

        /// <summary>
        /// Cache all possible condensed game presets for the current preset. This includes transition restrictions
        /// and warp indices for all possible warp locations.
        /// </summary>
        private void CacheCondensedGamePresets() {
            var presets = new List<CondensedGamePreset>();
            
            if (_currentPreset == null) {
                _currentCondensedPresets = presets;
                return;
            }

            // Create scene transition indices dictionary
            var sceneTransitionIndices = new Dictionary<ushort, byte[]>();
            foreach (var sceneTransitionPair in _currentPreset.SceneTransitions) {
                var sceneName = sceneTransitionPair.Key;

                var sceneIndex = Array.IndexOf(SceneNames, sceneName);
                if (sceneIndex == -1) {
                    Logger.Warn($"Could not get scene index of restriction '{sceneName}'");

                    continue;
                }

                if (!SceneTransitions.TryGetValue(sceneName, out var transitionNames)) {
                    Logger.Warn($"Could not get transition names for scene name '{sceneName}'");

                    continue;
                }

                var transitionIndices = new List<byte>();
                foreach (var transition in sceneTransitionPair.Value) {
                    var transitionIndex = Array.IndexOf(transitionNames, transition);
                    if (transitionIndex == -1) {
                        Logger.Warn(
                            $"Could not get transition index for transition '{transition}' in scene '{sceneName}'");

                        continue;
                    }

                    transitionIndices.Add((byte) transitionIndex);
                }

                sceneTransitionIndices[(ushort) sceneIndex] = transitionIndices.ToArray();
            }

            var warpTransitions = _currentPreset.WarpTransitions;
            foreach (var warpTransition in warpTransitions) {
                var sceneName = warpTransition.Key;

                var warpSceneIndex = Array.IndexOf(SceneNames, sceneName);
                if (warpSceneIndex == -1) {
                    Logger.Warn($"Could not get scene index of warp scene '{sceneName}'");
                    continue;
                }

                if (!SceneTransitions.TryGetValue(sceneName, out var transitionNames)) {
                    Logger.Warn($"Could not get transition index for scene: '{sceneName}'");
                    continue;
                }

                foreach (var transitionName in warpTransition.Value) {
                    var warpTransitionIndex = Array.IndexOf(transitionNames, transitionName);
                    if (warpTransitionIndex == -1) {
                        Logger.Warn(
                            $"Could not get transition index for transition: '{transitionName}' in scene: '{sceneName}");
                        continue;
                    }
                    
                    presets.Add(new CondensedGamePreset {
                        SceneTransitions = sceneTransitionIndices,
                        WarpSceneIndex = (ushort) warpSceneIndex,
                        WarpTransitionIndex = (byte) warpTransitionIndex
                    });
                }
            }

            _currentCondensedPresets = presets;
        }
    }
}