namespace HkmpTag.Server {
    /// <summary>
    /// Simple data class that keeps track of infected status for players.
    /// </summary>
    public class ServerTagPlayer {
        /// <summary>
        /// The ID of the player.
        /// </summary>
        public ushort Id { get; set; }
        /// <summary>
        /// The state of the player.
        /// </summary>
        public PlayerState State { get; set; }
    }
    
    /// <summary>
    /// Enumeration of possible states.
    /// </summary>
    public enum PlayerState {
        Infected,
        Uninfected
    }
}
