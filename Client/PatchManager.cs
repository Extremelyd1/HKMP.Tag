using System;
using Hkmp.Logging;
using Hkmp.Util;
using HutongGames.PlayMaker;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HkmpTag.Client {
    /// <summary>
    /// Class that manages patches to FSMs that limit gameplay options, such as stags and benches.
    /// </summary>
    public class PatchManager {
        /// <summary>
        /// The logger instance for logging information.
        /// </summary>
        private readonly ILogger _logger;

        public PatchManager(ILogger logger) {
            _logger = logger;
        }

        /// <summary>
        /// Initialize the patch manager by hooking the necessary methods.
        /// </summary>
        public void Initialize() {
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged += OnActiveSceneChanged;
        }

        /// <summary>
        /// Called when the scene changes so we can check for FSMs that we need to patch.
        /// </summary>
        /// <param name="oldScene">The old scene.</param>
        /// <param name="newScene">The new scene.</param>
        private void OnActiveSceneChanged(Scene oldScene, Scene newScene) {
            foreach (var fsm in Object.FindObjectsOfType<PlayMakerFSM>()) {
                if (fsm.gameObject.scene != newScene) {
                    continue;
                }
                
                // Find "Bench Control" FSMs and disable starting and sitting on them
                if (fsm.Fsm.Name.Equals("Bench Control")) {
                    _logger.Info("Found FSM with Bench Control, patching...");

                    fsm.InsertMethod("Pause 2", 1, () => {
                        PlayerData.instance.SetBool("atBench", false);
                    });

                    var checkStartState2 = fsm.GetState("Check Start State 2");
                    var pause2State = fsm.GetState("Pause 2");
                    checkStartState2.GetTransition(1).ToFsmState = pause2State;

                    var checkStartState = fsm.GetState("Check Start State");
                    var idleStartPauseState = fsm.GetState("Idle Start Pause");
                    checkStartState.GetTransition(1).ToFsmState = idleStartPauseState;

                    var idleState = fsm.GetState("Idle");
                    idleState.Actions = new[] { idleState.Actions[0] };
                }

                // Find "Stag Control" FSMs and prevent stag travelling
                if (fsm.Fsm.Name.Equals("Stag Control")) {
                    _logger.Info("Found FSM with Stag Control, patching...");

                    var idleState = fsm.GetState("Idle");
                    idleState.Transitions = Array.Empty<FsmTransition>();
                }
                
                // Find "Get Scream" FSMs and corresponding game object and prevent the Howling Wraiths from spawning
                if (fsm.name.Equals("Scream Item") && fsm.Fsm.Name.Equals("Get Scream")) {
                    _logger.Info("Found FSM with Get Scream, patching...");

                    var checkState = fsm.GetState("Check");
                    var destroyState = fsm.GetState("Destroy");
                    checkState.GetTransition(0).ToFsmState = destroyState;
                }

                // Find "Pickup" FSMs and corresponding game object and prevent Desolate Dive from spawning
                if (fsm.name.Equals("Quake Pickup") && fsm.Fsm.Name.Equals("Pickup")) {
                    _logger.Info("Found FSM with Quake Pickup, patching...");

                    var idleState = fsm.GetState("Idle");
                    idleState.Transitions = Array.Empty<FsmTransition>();
                }
                
                // Find "Destroy if Quake" FSMs and always remove the gate
                if (fsm.Fsm.Name.Equals("Destroy if Quake")) {
                    _logger.Info("Found FSM with Destroy if Quake, patching...");

                    var pauseState = fsm.GetState("Pause");
                    var destroyState = fsm.GetState("Destroy");
                    pauseState.GetTransition(0).ToFsmState = destroyState;
                }
                
                // Find "Ruins Shaman" FSMs and prevent the Shade Soul from being collected
                if (fsm.Fsm.Name.Equals("Ruins Shaman")) {
                    _logger.Info("Found FSM with Ruins Shaman, patching...");

                    var gotSpellState = fsm.GetState("Got Spell?");
                    var activateState = fsm.GetState("Activate");
                    gotSpellState.GetTransition(0).ToFsmState = activateState;
                }
                
                // Find "Crystal Shaman" game object and corresponding FSM and prevent the Descending Dark from
                // being collected
                if (fsm.name.Equals("Crystal Shaman") && fsm.Fsm.Name.Equals("Control")) {
                    _logger.Info("Found FSM with Crystal Shaman, patching...");

                    var initState = fsm.GetState("Init");
                    var brokenState = fsm.GetState("Broken");
                    initState.GetTransition(1).ToFsmState = brokenState;
                }

                // Find "Dreamer Plaque Inspect" game object and corresponding FSM and prevent the text from popping up
                if (fsm.name.Equals("Dreamer Plaque Inspect") && fsm.Fsm.Name.Equals("npc_control")) {
                    _logger.Info("Found FSM with Dreamer Plaque Inspect, patching...");

                    var idleState = fsm.GetState("Idle");
                    idleState.Transitions = Array.Empty<FsmTransition>();
                }
                
                // Find "Binding Shield Activate" game object and corresponding FSM and prevent the shield from locking
                // the player in
                if (fsm.name.Equals("Binding Shield Activate") && fsm.Fsm.Name.Equals("FSM")) {
                    _logger.Info("Found FSM with Binding Shield Activate, patching...");

                    var pauseState = fsm.GetState("Pause");
                    var destroyState = fsm.GetState("Destroy");
                    pauseState.GetTransition(0).ToFsmState = destroyState;
                }
                
                // Find the "... Trial Board" game objects and corresponding FSMs and prevent a colosseum from being
                // started
                if (fsm.name.EndsWith(" Trial Board") && fsm.Fsm.Name.Equals("npc_control")) {
                    _logger.Info("Found FSM with Trial Board, patching...");

                    var idleState = fsm.GetState("Idle");
                    idleState.Transitions = Array.Empty<FsmTransition>();
                }
            }
        }
    }
}