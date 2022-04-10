using System;
using Hkmp.Api.Command.Server;

namespace HkmpTag.Server {
    /// <summary>
    /// Command to manage the Tag games.
    /// </summary>
    public class TagCommand : IServerCommand {
        /// <inheritdoc />
        public string Trigger => "/tag";

        /// <inheritdoc />
        public string[] Aliases => new[] { "/hkmptag" };

        /// <inheritdoc />
        public bool AuthorizedOnly => true;

        /// <summary>
        /// The tag manager instance to relay information back to.
        /// </summary>
        private readonly ServerTagManager _tagManager;

        /// <summary>
        /// The transition manager instance.
        /// </summary>
        private readonly ServerTransitionManager _transitionManager;

        public TagCommand(ServerTagManager tagManager, ServerTransitionManager transitionManager) {
            _tagManager = tagManager;
            _transitionManager = transitionManager;
        }

        /// <inheritdoc />
        public void Execute(ICommandSender commandSender, string[] args) {
            if (args.Length < 2) {
                SendUsage(commandSender);
                return;
            }

            var action = args[1];
            if (action == "start") {
                ushort numInfected = 1;
                if (args.Length > 2) {
                    if (!ushort.TryParse(args[2], out numInfected)) {
                        commandSender.SendMessage("Please provide an integer as the number of infected");
                        return;
                    }
                }

                _tagManager.StartGame(numInfected, commandSender.SendMessage);
            } else if (action == "stop") {
                _tagManager.EndGame(commandSender.SendMessage);
            } else if (action == "preset") {
                if (args.Length < 3) {
                    commandSender.SendMessage($"Invalid usage: {Trigger} preset [name]");
                    return;
                }

                var presetName = args[2];
                var presetNames = _transitionManager.GetPresetNames();
                if (Array.IndexOf(presetNames, presetName) == -1) {
                    commandSender.SendMessage(
                        $"Preset with name '{presetName}' does not exists, options: {string.Join(", ", presetNames)}");
                    return;
                }

                _tagManager.WarpToPreset(presetName, commandSender.SendMessage);
            } else if (action == "auto") {
                _tagManager.ToggleAuto(commandSender.SendMessage);
            } else {
                SendUsage(commandSender);
            }
        }

        /// <summary>
        /// Sends the command usage to the given command sender.
        /// </summary>
        /// <param name="commandSender">The command sender.</param>
        private void SendUsage(ICommandSender commandSender) {
            commandSender.SendMessage($"Invalid usage: {Trigger} <start|stop|preset|auto>");
        }
    }
}
