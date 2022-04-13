﻿using System.IO;
using System.Reflection;
using Newtonsoft.Json;

namespace HkmpTag.Server {
    /// <summary>
    /// Class with server-related settings.
    /// </summary>
    public class ServerSettings {
        /// <summary>
        /// The file name of the JSON settings file.
        /// </summary>
        private const string FileName = "tag_settings.json";

        /// <summary>
        /// The time the countdown will take before starting the game in seconds.
        /// </summary>
        [JsonProperty("countdown_time")]
        public int CountdownTime { get; set; }

        /// <summary>
        /// The time after warping players before starting the automatic game in seconds.
        /// </summary>
        [JsonProperty("warp_time")]
        public int WarpTime { get; set; }

        /// <summary>
        /// The time after the game has ended to wait before starting a new one.
        /// </summary>
        [JsonProperty("post_game_time")]
        public int PostGameTime { get; set; }

        /// <summary>
        /// The maximum time a game can lasts before ending it.
        /// </summary>
        [JsonProperty("max_game_time")]
        public int MaxGameTime { get; set; }

        /// <summary>
        /// The maximum number of games to play on one preset.
        /// </summary>
        [JsonProperty("max_games_on_preset")]
        public int MaxGamesOnPreset { get; set; }

        /// <summary>
        /// Save the server settings to file.
        /// </summary>
        public void SaveToFile() {
            var dirName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (dirName == null) {
                return;
            }

            var filePath = Path.Combine(dirName, FileName);
            var settingsJson = JsonConvert.SerializeObject(this);

            try {
                File.WriteAllText(filePath, settingsJson);
            } catch {
                // TODO: log exception to logger
                // ignored
            }
        }

        /// <summary>
        /// Load the server settings from file.
        /// </summary>
        /// <returns>An instance with the loaded settings or a new instance if it could not be loaded.</returns>
        public static ServerSettings LoadFromFile() {
            var dirName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (dirName == null) {
                return new ServerSettings();
            }

            var filePath = Path.Combine(dirName, FileName);
            if (!File.Exists(filePath)) {
                return new ServerSettings();
            }

            try {
                var fileContents = File.ReadAllText(filePath);
                var settings = JsonConvert.DeserializeObject<ServerSettings>(fileContents);
                return settings ?? new ServerSettings();
            } catch {
                // TODO: log exception to logger
                return new ServerSettings();
            }
        }
    }
}