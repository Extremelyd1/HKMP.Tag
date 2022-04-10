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
        /// Dictionary mapping preset names to their transition restriction instance.
        /// </summary>
        private readonly Dictionary<string, TransitionRestriction> _transitionRestrictions;

        /// <summary>
        /// The name of the current preset or null if no preset is set.
        /// </summary>
        private string _currentPreset;

        public ServerTransitionManager(ILogger logger) : base(logger) {
            _transitionRestrictions = new Dictionary<string, TransitionRestriction>();
        }

        /// <summary>
        /// Load the scene transitions and the preset file.
        /// </summary>
        protected override void LoadSceneTransitions() {
            base.LoadSceneTransitions();

            Logger.Info(this, "Loading transition restriction presets");

            var dirName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (dirName == null) {
                return;
            }

            var filePath = Path.Combine(dirName, PresetFileName);
            if (!File.Exists(filePath)) {
                Logger.Info(this, "No preset file found");
                return;
            }

            var fileContents = File.ReadAllText(filePath);
            var transitionRestrictions = JsonConvert.DeserializeObject<List<TransitionRestriction>>(fileContents);
            if (transitionRestrictions == null) {
                Logger.Warn(this, "Could not read transition presets file");
                return;
            }

            foreach (var restriction in transitionRestrictions) {
                var name = restriction.Name;

                if (!_transitionRestrictions.ContainsKey(name)) {
                    Logger.Info(this, $"Loaded transition restriction preset '{name}'");
                    _transitionRestrictions[name] = restriction;
                } else {
                    Logger.Warn(this, $"Transition restriction preset with name '{name}' was already defined");
                }
            }
        }

        /// <summary>
        /// Set the current preset to the one with the given name. Will check whether the name is of a valid
        /// preset.
        /// </summary>
        /// <param name="name">The name of the preset.</param>
        public void SetPreset(string name) {
            if (!_transitionRestrictions.ContainsKey(name)) {
                Logger.Warn(this, $"Tried to set preset '{name}', while it did not exist");
                return;
            }

            _currentPreset = name;
        }

        /// <summary>
        /// Get the names of all presets.
        /// </summary>
        /// <returns>A string array containing all preset names.</returns>
        public string[] GetPresetNames() {
            return _transitionRestrictions.Keys.ToArray();
        }

        /// <summary>
        /// Get the current raw transition restriction information including the warp scene index. 
        /// </summary>
        /// <returns>A pair of warp scene index and a dictionary containing a scene index to transition index
        /// array mapping.</returns>
        public (ushort, Dictionary<ushort, byte[]>) GetTransitionRestrictions() {
            if (string.IsNullOrEmpty(_currentPreset) ||
                !_transitionRestrictions.TryGetValue(_currentPreset, out var transitionRestriction)) {
                return (0, new Dictionary<ushort, byte[]>());
            }

            var warpSceneIndex = Array.IndexOf(SceneNames, transitionRestriction.WarpSceneName);
            if (warpSceneIndex == -1) {
                Logger.Warn(this,
                    $"Could not get scene index of warp scene '{transitionRestriction.WarpSceneName}', falling back to default value");

                warpSceneIndex = 0;
            }

            var sceneTransitionIndices = new Dictionary<ushort, byte[]>();
            foreach (var sceneTransitionPair in transitionRestriction.SceneTransitions) {
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
    /// Data class for a single transition restriction preset.
    /// </summary>
    public class TransitionRestriction {
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
        /// The dictionary mapping scene names to transition name arrays.
        /// </summary>
        [JsonProperty("transitions")]
        public Dictionary<string, string[]> SceneTransitions { get; set; }
    }
}
