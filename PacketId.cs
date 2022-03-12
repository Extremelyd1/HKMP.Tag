namespace HkmpTag {
    /// <summary>
    /// Enumeration for server packet IDs.
    /// </summary>
    public enum ServerPacketId {
        StartRequest,
        EndRequest,
        PlayerTag
    }

    /// <summary>
    /// Enumeration for client packet IDs.
    /// </summary>
    public enum ClientPacketId {
        GameStart,
        GameEnd,
        GameInProgress,
        PlayerTag
    }
}
