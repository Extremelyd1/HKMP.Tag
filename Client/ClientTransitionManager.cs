using System;
using System.Collections;
using System.Collections.Generic;
using GlobalEnums;
using UnityEngine;
using ILogger = Hkmp.Logging.ILogger;
using Object = UnityEngine.Object;

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

        /// <summary>
        /// The name of the scene and transition that was last warped to.
        /// </summary>
        private (string, string) _lastWarp;

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
                    Logger.Warn("Received scene index that was out of bounds");
                    return;
                }

                var sceneName = SceneNames[sceneIndex];

                if (!SceneTransitions.TryGetValue(sceneName, out var sceneTransitions)) {
                    Logger.Warn($"Scene name: '{sceneName}' was not found in all scene transitions");
                    return;
                }

                var transitionIndices = sceneTransitionIndexPair.Value;

                var transitionNames = new HashSet<string>();
                foreach (var transitionIndex in transitionIndices) {
                    if (transitionIndex >= sceneTransitions.Length) {
                        Logger.Warn("Received index of transition that was out of bounds");
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
        /// <param name="transitionIndex">The index of the transition to warp to.</param>
        public void WarpToScene(ushort sceneIndex, byte transitionIndex) {
            if (sceneIndex >= SceneNames.Length) {
                Logger.Warn($"Could not warp to scene index '{sceneIndex}', it is out of bounds");
                return;
            }

            var sceneName = SceneNames[sceneIndex];

            if (!SceneTransitions.TryGetValue(sceneName, out var transitionNames)) {
                Logger.Warn($"Could not warp to unknown scene '{sceneName}'");
                return;
            }

            if (transitionIndex >= transitionNames.Length) {
                Logger.Warn($"Could not warp to scene '{sceneName}', the transition index: '{transitionIndex}' is out of range");
                return;
            }

            GameManager.instance.StartCoroutine(WarpToSceneRoutine(sceneName, transitionNames[transitionIndex]));
        }

        /// <summary>
        /// Coroutine for warping to the given scene with the given array of transition names.
        /// </summary>
        /// <param name="sceneName">The name of the scene.</param>
        /// <param name="transitionName">The name of the transition.</param>
        /// <returns>Enumerator representing the coroutine.</returns>
        private IEnumerator WarpToSceneRoutine(string sceneName, string transitionName) {
            // Wait for hazard respawn to finish
            yield return new WaitWhile(() => HeroController.instance.cState.hazardRespawning);
            
            // Cancel any NPC conversation that was happening before warping
            PlayMakerFSM.BroadcastEvent("CONVO CANCEL");

            // Unpause the game if it was paused
            var uiManager = UIManager.instance;
            if (uiManager != null) {
                if (uiManager.uiState.Equals(UIState.PAUSED)) {
                    var gm = GameManager.instance;

                    GameCameras.instance.ResumeCameraShake();
                    gm.inputHandler.PreventPause();
                    gm.actorSnapshotUnpaused.TransitionTo(0f);
                    gm.isPaused = false;
                    gm.ui.AudioGoToGameplay(0.2f);
                    gm.ui.SetState(UIState.PLAYING);
                    gm.SetState(GameState.PLAYING);
                    if (HeroController.instance != null) {
                        HeroController.instance.UnPause();
                    }

                    MenuButtonList.ClearAllLastSelected();
                    gm.inputHandler.AllowPause();
                }
            }

            _lastWarp = (sceneName, transitionName);
            Logger.Info($"Warping to scene: '{sceneName}', transition: '{transitionName}'");

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
            // Resets the last warp variable to an empty state
            void ResetLastWarp() {
                if (sceneName == _lastWarp.Item1) {
                    _lastWarp = ("", "");
                }
            }
            
            if (_currentRestrictions == null) {
                ResetLastWarp();

                return;
            }

            if (!_currentRestrictions.TryGetValue(sceneName, out var restrictedTransitions)) {
                ResetLastWarp();

                return;
            }

            Logger.Info($"Found transition restrictions for scene: {sceneName}, applying them...");

            foreach (var transitionPoint in Object.FindObjectsOfType<TransitionPoint>()) {
                var name = transitionPoint.name;

                if (restrictedTransitions.Contains(name)) {
                    if (name.Contains("door")) {
                        // If it is a door, we disable it
                        var collider = transitionPoint.GetComponent<Collider2D>();
                        transitionPoint.gameObject.AddComponent<DisableCollider>().Target = collider;

                        Logger.Info($"  Restricting '{name}' door transition");
                    } else {
                        // If it is not a door, we set the collider to not be a trigger, which makes it collide with
                        // the player and thus prevent them from exiting
                        var collider = transitionPoint.GetComponent<Collider2D>();

                        if (sceneName == _lastWarp.Item1 && name == _lastWarp.Item2 && !name.Contains("bot")) {
                            Logger.Info($"  Restricting '{name}' transition after entering");

                            // If it is the transition we last warped into, we add a component that only makes the
                            // collider solid after we entered the transition
                            var transitionPointExit = transitionPoint.gameObject.AddComponent<ColliderExitHandler>();
                            transitionPointExit.Action = () => collider.isTrigger = false;
                        } else {
                            collider.isTrigger = false;

                            Logger.Info($"  Restricting '{name}' transition");
                        }
                    }
                }
            }
            
            ResetLastWarp();
        }
    }

    /// <summary>
    /// Class to keep a collider disabled.
    /// </summary>
    public class DisableCollider : MonoBehaviour {
        /// <summary>
        /// The target to disable.
        /// </summary>
        public Collider2D Target { get; set; }

        public void Update() {
            // Continually disable the target if it was enabled
            if (Target.enabled) {
                Target.enabled = false;
            }
        }
    }

    /// <summary>
    /// Class that executes a given action if something exits the collider on the game object.
    /// </summary>
    public class ColliderExitHandler : MonoBehaviour {
        /// <summary>
        /// The number of updates that must be passed between entering and exiting.
        /// </summary>
        private const int UpdateThreshold = 2;
        
        /// <summary>
        /// The action to execute.
        /// </summary>
        public Action Action { get; set; }

        /// <summary>
        /// Whether we have entered the collider.
        /// </summary>
        private bool _entered;
        /// <summary>
        /// The number of updates since we entered the collider.
        /// </summary>
        private int _updatesSinceEnter;

        private void Update() {
            // If we have entered the collider, increment the update count
            if (_entered) {
                _updatesSinceEnter++;
            }
        }

        /// <summary>
        /// Sent when another object enters a trigger collider attached to this object (2D physics only).
        /// This function can be a coroutine.
        /// </summary>
        /// <param name="other">The other collider.</param>
        private void OnTriggerEnter2D(Collider2D other) {
            // Reset the update count and mark that we entered
            _updatesSinceEnter = 0;
            _entered = true;
        }

        /// <summary>
        /// Sent when another object leaves a trigger collider attached to this object (2D physics only).
        /// This function can be a coroutine.
        /// </summary>
        /// <param name="other">The other collider.</param>
        private void OnTriggerExit2D(Collider2D other) {
            // Check whether the number of updates passed is above the threshold
            // Sometimes the enter and exit trigger in the same update frame, which leads
            // to buggy behaviour
            if (_updatesSinceEnter > UpdateThreshold) {
                Action?.Invoke();
                
                _entered = false;
            }
        }
    }
}
