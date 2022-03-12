using HutongGames.PlayMaker;
using Modding;

namespace HkmpTag.Client {
    /// <summary>
    /// Static class for handling "title" text that shows up as dream nail dialogue.
    /// </summary>
    public static class Title {
        /// <summary>
        /// The conversation title for the dream nail FSM.
        /// </summary>
        private const string DreamNailConvoTitle = "HKMP_TAG_DREAMNAIL_CONVO_TITLE";

        /// <summary>
        /// The conversation title that displays as text.
        /// </summary>
        private static string _convoTitle = "";

        /// <summary>
        /// Initialize this class by registering a language get hook.
        /// </summary>
        public static void Initialize() {
            ModHooks.LanguageGetHook += (key, title, orig) => {
                if (key.Equals(DreamNailConvoTitle)) {
                    return _convoTitle;
                }

                return orig;
            };
        }

        /// <summary>
        /// Show the given text as dream nail dialogue.
        /// </summary>
        /// <param name="text">The text to display.</param>
        public static void Show(string text) {
            _convoTitle = text;

            var fsmGameObject = FsmVariables.GlobalVariables.GetFsmGameObject("Enemy Dream Msg").Value;
            var fsm = PlayMakerFSM.FindFsmOnGameObject(fsmGameObject, "Display");
            fsm.FsmVariables.GetFsmInt("Convo Amount").Value = 1;
            fsm.FsmVariables.GetFsmString("Convo Title").Value = DreamNailConvoTitle;

            fsm.SendEvent("DISPLAY DREAM MSG");
        }
    }
}
