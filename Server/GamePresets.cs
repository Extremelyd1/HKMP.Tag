using System.Collections.Generic;
using Newtonsoft.Json;

namespace HkmpTag.Server {
    /// <summary>
    /// Data class for all game presets, including default loadout.
    /// </summary>
    public class GamePresets {
        /// <inheritdoc cref="ServerPresetManager._defaultLoadouts" />
        [JsonProperty("default_loadouts")]
        public Loadouts DefaultLoadouts { get; set; }

        /// <summary>
        /// List of game presets (see <see cref="GamePreset"/>).
        /// </summary>
        [JsonProperty("presets")]
        public List<GamePreset> Presets { get; set; }
    }
}