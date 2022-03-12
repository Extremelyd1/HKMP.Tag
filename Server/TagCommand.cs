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

        public TagCommand(ServerTagManager tagManager) {
            _tagManager = tagManager;
        }

        /// <inheritdoc />
        public void Execute(ICommandSender commandSender, string[] arguments) {
            if (arguments.Length < 2) {
                SendUsage(commandSender);
                return;
            }

            var action = arguments[1];
            if (action == "start") {
                ushort numInfected = 1;
                if (arguments.Length > 2) {
                    if (!ushort.TryParse(arguments[2], out numInfected)) {
                        commandSender.SendMessage("Please provide an integer as the number of infected");
                        return;
                    }
                }

                _tagManager.StartGame(commandSender.SendMessage, numInfected);
            } else if (action == "stop") {
                _tagManager.EndGame(commandSender.SendMessage);
            } else {
                SendUsage(commandSender);
            }
        }

        /// <summary>
        /// Sends the command usage to the given command sender.
        /// </summary>
        /// <param name="commandSender">The command sender.</param>
        private void SendUsage(ICommandSender commandSender) {
            commandSender.SendMessage($"Invalid usage: {Trigger} <start|stop> [number of infected]");
        }
    }
}
