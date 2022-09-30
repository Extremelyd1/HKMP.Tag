using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using ILogger = Hkmp.Logging.ILogger;

namespace HkmpTag {
    /// <summary>
    /// Abstract base class for managing transitions.
    /// </summary>
    public abstract class TransitionManager {
        /// <summary>
        /// The file path of the embedded resource file for all transitions.
        /// </summary>
        private const string TransitionFilePath = "HkmpTag.Resource.transitions.json";

        /// <summary>
        /// The logger to log information to.
        /// </summary>
        protected readonly ILogger Logger;
        
        /// <summary>
        /// String array containing all scene names.
        /// </summary>
        protected string[] SceneNames;
        /// <summary>
        /// Dictionary containing a mapping from scene name to an array of all its transitions.
        /// </summary>
        protected Dictionary<string, string[]> SceneTransitions;

        protected TransitionManager(ILogger logger) {
            Logger = logger;
        }

        /// <summary>
        /// Initializes the transition manager by loading the scene transitions file.
        /// </summary>
        public virtual void Initialize() {
            LoadSceneTransitions();
        }

        /// <summary>
        /// Load all scene transitions from the embedded resource.
        /// </summary>
        protected virtual void LoadSceneTransitions() {
            var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(TransitionFilePath);
            if (resourceStream == null) {
                Logger.Warn("Could not get resource stream for transitions");
                return;
            }

            using (var streamReader = new StreamReader(resourceStream)) {
                var fileString = streamReader.ReadToEnd();

                SceneTransitions = JsonConvert.DeserializeObject<Dictionary<string, string[]>>(fileString);
                if (SceneTransitions == null) {
                    Logger.Warn("Could not deserialize scene transitions");
                    return;
                }
            }
            
            SceneNames = new string[SceneTransitions.Keys.Count];

            var index = 0;
            foreach (var sceneName in SceneTransitions.Keys) {
                SceneNames[index++] = sceneName;
            }
        }
    }
}
