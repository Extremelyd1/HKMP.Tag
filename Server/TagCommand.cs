using System;
using System.Reflection;
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
        /// The server settings instance to read values from and write values to.
        /// </summary>
        private readonly ServerSettings _settings;

        /// <summary>
        /// The transition manager instance.
        /// </summary>
        private readonly ServerTransitionManager _transitionManager;

        public TagCommand(
            ServerTagManager tagManager,
            ServerSettings settings,
            ServerTransitionManager transitionManager
        ) {
            _tagManager = tagManager;
            _settings = settings;
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

                _transitionManager.SetPreset(presetName);
                _tagManager.WarpToPreset(commandSender.SendMessage);
            } else if (action == "auto") {
                _tagManager.ToggleAuto(commandSender.SendMessage);
            } else if (action == "set") {
                HandleSet(commandSender, args);
            } else {
                SendUsage(commandSender);
            }
        }

        /// <summary>
        /// Handle the set sub-command.
        /// </summary>
        /// <param name="commandSender">The command sender that executed this command.</param>
        /// <param name="args">A string array containing the arguments for this command. The first argument is
        /// the command trigger or alias.</param>
        private void HandleSet(ICommandSender commandSender, string[] args) {
            if (args.Length < 3) {
                commandSender.SendMessage($"Invalid usage: {Trigger} set [setting name]");
                return;
            }

            var settingName = args[2];

            var propertyInfos = typeof(ServerSettings).GetProperties();

            PropertyInfo settingProperty = null;
            foreach (var prop in propertyInfos) {
                if (prop.Name.ToLower().Equals(settingName.ToLower())) {
                    settingProperty = prop;
                    break;
                }
            }

            if (settingProperty == null || !settingProperty.CanRead) {
                commandSender.SendMessage($"Could not find setting with name: {settingName}");
                return;
            }

            if (args.Length < 4) {
                // User did not provide value to write setting, so we print the value
                var currentValue = settingProperty.GetValue(_settings);

                commandSender.SendMessage($"Setting '{settingName}' currently has value: {currentValue}");
                return;
            }

            if (!settingProperty.CanWrite) {
                commandSender.SendMessage($"Could not change value of setting with name: {settingName} (non-writable)");
                return;
            }

            var newValueString = args[3];
            object newValueObject;

            if (settingProperty.PropertyType == typeof(int)) {
                if (!int.TryParse(newValueString, out var newValueInt)) {
                    commandSender.SendMessage("Please provide an integer value for this setting");
                    return;
                }

                newValueObject = newValueInt;
            } else {
                commandSender.SendMessage(
                    $"Could not change value of setting with name: {settingName} (unhandled type)");
                return;
            }

            settingProperty.SetValue(_settings, newValueObject);

            commandSender.SendMessage($"Changed setting '{settingName}' to: {newValueObject}");
            
            _settings.SaveToFile();
        }

        /// <summary>
        /// Sends the command usage to the given command sender.
        /// </summary>
        /// <param name="commandSender">The command sender.</param>
        private void SendUsage(ICommandSender commandSender) {
            commandSender.SendMessage($"Invalid usage: {Trigger} <start|stop|preset|auto|set>");
        }
    }
}
