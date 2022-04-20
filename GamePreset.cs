using System.Collections.Generic;
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
        /// The name of the scene to warp to.
        /// </summary>
        [JsonProperty("warp_scene")]
        public string WarpSceneName { get; set; }
        
        /// <summary>
        /// The name of the transition to warp to.
        /// </summary>
        [JsonProperty("warp_transition")]
        public string WarpTransitionName { get; set; }

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
    }

    /// <summary>
    /// Data class for a preset that has info on transition restrictions.
    /// This is condensed into indices to send over the network.
    /// </summary>
    public class CondensedGamePreset {
        /// <summary>
        /// The index of the scene to warp to.
        /// </summary>
        public ushort WarpSceneIndex;
        /// <summary>
        /// The index of the transition within the scene to warp to.
        /// </summary>
        public byte WarpTransitionIndex;
        
        /// <summary>
        /// Dictionary mapping scene indices to transition index arrays.
        /// </summary>
        public Dictionary<ushort, byte[]> SceneTransitions { get; set; }
    }
}
