using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace HkmpTag {
    /// <summary>
    /// Data class for a preset that has info on transition restrictions.
    /// </summary>
    public class GamePreset {
        /// <summary>
        /// The name of the preset.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// The names of scenes mapped to the names of the transitions that players can be warped to.
        /// </summary>
        [JsonProperty("warp_transitions")]
        public Dictionary<string, string[]> WarpTransitions { get; set; }

        /// <summary>
        /// Minimum number of players required for this preset.
        /// </summary>
        [JsonProperty("min_players")]
        public int? MinPlayers { get; set; }

        /// <summary>
        /// Maximum number of players required for this preset.
        /// </summary>
        [JsonProperty("max_players")]
        public int? MaxPlayers { get; set; }

        /// <summary>
        /// Dictionary mapping scene names to transition name arrays.
        /// </summary>
        [JsonProperty("transitions")]
        public Dictionary<string, string[]> SceneTransitions { get; set; }
        
        /// <summary>
        /// The <see cref="Loadouts"/> for this game preset.
        /// </summary>
        [JsonProperty("loadouts"), CanBeNull]
        public Loadouts Loadouts { get; set; }
    }

    /// <summary>
    /// Data class for a preset that has info on transition restrictions.
    /// This is condensed into indices to send over the network.
    /// </summary>
    public class CondensedGamePreset {
        /// <summary>
        /// The index of the scene to warp to.
        /// </summary>
        public ushort WarpSceneIndex { get; set; }

        /// <summary>
        /// The index of the transition within the scene to warp to.
        /// </summary>
        public byte WarpTransitionIndex { get; set; }
        
        /// <summary>
        /// Dictionary mapping scene indices to transition index arrays.
        /// </summary>
        public Dictionary<ushort, byte[]> SceneTransitions { get; set; }

        public CondensedGamePreset() {
            SceneTransitions = new Dictionary<ushort, byte[]>();
        }
    }
}
