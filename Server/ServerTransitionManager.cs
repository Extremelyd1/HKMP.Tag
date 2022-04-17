using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Hkmp;
using Newtonsoft.Json;

namespace HkmpTag.Server {
    /// <summary>
    /// Manager class for server-side transition restrictions.
    /// </summary>
    public class ServerTransitionManager : TransitionManager {
        /// <summary>
        /// The file name of the transition preset file.
        /// </summary>
        private const string PresetFileName = "transition_presets.json";

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
        /// Boolean indicating whether the preset file has changed.
        /// </summary>
        private bool _presetFileChanged;

        /// <summary>
        /// The current preset or null if no preset is set.
        /// </summary>
        private GamePreset _currentPreset;

        public ServerTransitionManager(ILogger logger, Random random) : base(logger) {
            _random = random;

            _gamePresets = new Dictionary<string, GamePreset>();
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

            Logger.Info(this, "Loading transition restriction presets");

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
                Logger.Info(this, "No preset file found");
                return;
            }

            string fileContents;

            try {
                fileContents = File.ReadAllText(filePath);
            } catch (Exception e) {
                Logger.Info(this, $"Could not read preset file: {e.GetType()}, {e.Message}");
                return;
            }

            var transitionRestrictions = JsonConvert.DeserializeObject<List<GamePreset>>(fileContents);
            if (transitionRestrictions == null) {
                Logger.Warn(this, "Could not read transition presets file");
                return;
            }

            if (reset) {
                _gamePresets.Clear();
            }

            var loadedPresetNames = new List<string>();

            foreach (var restriction in transitionRestrictions) {
                var name = restriction.Name;

                if (!_gamePresets.ContainsKey(name)) {
                    loadedPresetNames.Add(name);
                    _gamePresets[name] = restriction;
                } else {
                    Logger.Warn(this, $"Transition restriction preset with name '{name}' was already defined");
                }
            }

            if (loadedPresetNames.Count == 0) {
                Logger.Info(this, "Could not load any transition restriction presets!");
            } else {
                Logger.Info(this, $"Loaded transition restriction presets: {string.Join(", ", loadedPresetNames)}");
            }
        }

        /// <summary>
        /// Set the current preset to the one with the given name. Will check whether the name is of a valid
        /// preset.
        /// </summary>
        /// <param name="name">The name of the preset.</param>
        public void SetPreset(string name) {
            if (!_gamePresets.TryGetValue(name, out var preset)) {
                Logger.Warn(this, $"Tried to set preset '{name}', while it did not exist");
                return;
            }

            _currentPreset = preset;
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

            Logger.Info(this, $"Picked random preset '{randomPreset.Name}'");

            _currentPreset = randomPreset;
            return true;
        }

        /// <summary>
        /// Get the current raw transition restriction information including the warp scene index. 
        /// </summary>
        /// <returns>A pair of warp scene index and a dictionary containing a scene index to transition index
        /// array mapping.</returns>
        public (ushort, Dictionary<ushort, byte[]>) GetTransitionRestrictions() {
            if (_currentPreset == null) {
                return (0, new Dictionary<ushort, byte[]>());
            }

            var warpSceneIndex = Array.IndexOf(SceneNames, _currentPreset.WarpSceneName);
            if (warpSceneIndex == -1) {
                Logger.Warn(this,
                    $"Could not get scene index of warp scene '{_currentPreset.WarpSceneName}', falling back to default value");

                warpSceneIndex = 0;
            }

            var sceneTransitionIndices = new Dictionary<ushort, byte[]>();
            foreach (var sceneTransitionPair in _currentPreset.SceneTransitions) {
                var sceneName = sceneTransitionPair.Key;

                var sceneIndex = Array.IndexOf(SceneNames, sceneName);
                if (sceneIndex == -1) {
                    Logger.Warn(this, $"Could not get scene index of restriction '{sceneName}'");

                    continue;
                }

                if (!SceneTransitions.TryGetValue(sceneName, out var transitionNames)) {
                    Logger.Warn(this, $"Could not get transition names for scene name '{sceneName}'");

                    continue;
                }

                var transitionIndices = new List<byte>();
                foreach (var transition in sceneTransitionPair.Value) {
                    var transitionIndex = Array.IndexOf(transitionNames, transition);
                    if (transitionIndex == -1) {
                        Logger.Warn(this,
                            $"Could not get transition index for transition '{transition}' in scene '{sceneName}'");

                        continue;
                    }

                    transitionIndices.Add((byte)transitionIndex);
                }

                sceneTransitionIndices[(ushort)sceneIndex] = transitionIndices.ToArray();
            }

            return ((ushort)warpSceneIndex, sceneTransitionIndices);
        }
    }

    /// <summary>
    /// Data class for a preset that has info on transition restrictions.
    /// </summary>
    public class GamePreset {
        /// <summary>
        /// The name of the preset.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// The name of the scene to warp to.
        /// </summary>
        [JsonProperty("warp_scene")]
        public string WarpSceneName { get; set; }

        /// <summary>
        /// Minimum number of players required for this preset.
        /// </summary>
        [JsonProperty("min_players")]
        public int? MinPlayers { get; set; }

        /// <summary>
        /// Maximum number of players required for this preset.
        /// </summary>
        [JsonProperty("max_players")]
        public int? MaxPlayers { get; set; }

        /// <summary>
        /// The dictionary mapping scene names to transition name arrays.
        /// </summary>
        [JsonProperty("transitions")]
        public Dictionary<string, string[]> SceneTransitions { get; set; }
    }
}
