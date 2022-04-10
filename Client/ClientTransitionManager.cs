using System.Collections.Generic;
using GlobalEnums;
using UnityEngine;
using ILogger = Hkmp.ILogger;

namespace HkmpTag.Client {
    /// <summary>
    /// Manager class for the client-side transition restrictions.
    /// </summary>
    public class ClientTransitionManager : TransitionManager {
        /// <summary>
        /// Dictionary containing a mapping of scene name to a set of transition names representing the current
        /// restrictions.
        /// </summary>
        private Dictionary<string, HashSet<string>> _currentRestrictions;

        public ClientTransitionManager(ILogger logger) : base(logger) {
        }

        /// <summary>
        /// Initializes the transition manager by loading the transition resource and registering callbacks.
        /// </summary>
        public override void Initialize() {
            base.Initialize();

            UnityEngine.SceneManagement.SceneManager.activeSceneChanged +=
                (oldScene, newScene) => CheckTransitions(newScene.name);
        }

        /// <summary>
        /// Callback method for when game info is received and scene transition restrictions need to be stored.
        /// </summary>
        /// <param name="sceneTransitionInfo">The dictionary containing raw scene transition indices.</param>
        public void OnReceiveGameInfo(Dictionary<ushort, byte[]> sceneTransitionInfo) {
            _currentRestrictions = new Dictionary<string, HashSet<string>>();

            foreach (var sceneTransitionIndexPair in sceneTransitionInfo) {
                var sceneIndex = sceneTransitionIndexPair.Key;
                if (sceneIndex >= SceneNames.Length) {
                    Logger.Warn(this, "Received scene index that was out of bounds");
                    return;
                }

                var sceneName = SceneNames[sceneIndex];

                if (!SceneTransitions.TryGetValue(sceneName, out var sceneTransitions)) {
                    Logger.Warn(this, $"Scene name: '{sceneName}' was not found in all scene transitions");
                    return;
                }

                var transitionIndices = sceneTransitionIndexPair.Value;

                var transitionNames = new HashSet<string>();
                foreach (var transitionIndex in transitionIndices) {
                    if (transitionIndex >= sceneTransitions.Length) {
                        Logger.Warn(this, "Received index of transition that was out of bounds");
                        return;
                    }

                    transitionNames.Add(sceneTransitions[transitionIndex]);
                }

                _currentRestrictions[sceneName] = transitionNames;
            }

            // After setting current restrictions, we can immediately start checking the transitions
            CheckTransitions(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        /// <summary>
        /// Warp the local player to the scene with the given index.
        /// </summary>
        /// <param name="sceneIndex">The index of the scene to warp to.</param>
        public void WarpToScene(ushort sceneIndex) {
            if (sceneIndex >= SceneNames.Length) {
                Logger.Warn(this, $"Could not warp to scene index '{sceneIndex}', it is out of bounds");
                return;
            }

            var sceneName = SceneNames[sceneIndex];

            if (!SceneTransitions.TryGetValue(sceneName, out var transitionNames)) {
                Logger.Warn(this, $"Could not warp to unknown scene '{sceneName}'");
                return;
            }

            if (transitionNames.Length == 0) {
                Logger.Warn(this, $"Could not warp to scene '{sceneName}', it does not have any transitions");
                return;
            }

            // Simply get the first transition
            var transitionName = transitionNames[0];

            // First kill conveyor movement since otherwise the game will think we are still on a conveyor
            // after we warp to the new scene
            var heroController = HeroController.instance;
            if (heroController != null) {
                heroController.cState.inConveyorZone = false;
                heroController.cState.onConveyor = false;
                heroController.cState.onConveyorV = false;
            }

            var gameManager = GameManager.instance;

            // Method do execute the scene transition with the given values
            void DoSceneTransition() {
                gameManager.BeginSceneTransition(new GameManager.SceneLoadInfo {
                    SceneName = sceneName,
                    EntryGateName = transitionName,
                    HeroLeaveDirection = GatePosition.door,
                    EntryDelay = 0,
                    WaitForSceneTransitionCameraFade = true,
                    Visualization = GameManager.SceneLoadVisualizations.Default,
                });
            }

            // Check if another scene is already loading, and if so wait for it to finish
            if (gameManager.IsLoadingSceneTransition) {
                // Callback method for when the scene transition is finished
                void OnFinishSceneTransition() {
                    // De-register the callback and do the transition
                    gameManager.OnFinishedSceneTransition -= OnFinishSceneTransition;
                    DoSceneTransition();
                }

                // Register a callback for when the current transition finishes
                gameManager.OnFinishedSceneTransition += OnFinishSceneTransition;
            } else {
                DoSceneTransition();
            }
        }

        /// <summary>
        /// Check whether transitions in the scene with the given name should be restricted and apply them.
        /// </summary>
        /// <param name="sceneName">The name of the scene to check.</param>
        private void CheckTransitions(string sceneName) {
            if (_currentRestrictions == null) {
                return;
            }

            if (!_currentRestrictions.TryGetValue(sceneName, out var restrictedTransitions)) {
                return;
            }

            Logger.Info(this, $"Found transition restrictions for scene: {sceneName}, applying them...");

            foreach (var transitionPoint in Object.FindObjectsOfType<TransitionPoint>()) {
                var name = transitionPoint.name;

                if (restrictedTransitions.Contains(name)) {
                    Logger.Info(this, $"  Restricting '{name}' transition");

                    if (name.Contains("door")) {
                        // If it is a door, we disable it
                        transitionPoint.GetComponent<Collider2D>().enabled = false;
                    } else {
                        // If it is not a door, we set it to not be a trigger, which makes it collide with the player
                        transitionPoint.GetComponent<Collider2D>().isTrigger = false;
                    }
                }
            }
        }
    }
}
