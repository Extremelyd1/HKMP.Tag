using System.Collections.Generic;
using Hkmp.Networking.Packet;

namespace HkmpTag {
    /// <summary>
    /// Packet data for the start request.
    /// </summary>
    public class StartRequestPacket : IPacketData {
        /// <summary>
        /// The number of initial infected.
        /// </summary>
        public ushort NumInfected { get; set; }
        
        /// <inheritdoc />
        public void WriteData(IPacket packet) {
            packet.Write(NumInfected);
        }

        /// <inheritdoc />
        public void ReadData(IPacket packet) {
            NumInfected = packet.ReadUShort();
        }

        /// <inheritdoc />
        public bool IsReliable => true;
        /// <inheritdoc />
        public bool DropReliableDataIfNewerExists => true;
    }
    
    /// <summary>
    /// Packet data for the game start.
    /// </summary>
    public class GameStartPacket : IPacketData {
        /// <summary>
        /// Whether the receiving player is infected.
        /// </summary>
        public bool IsInfected { get; set; }
        /// <summary>
        /// The list of IDs of infected players.
        /// </summary>
        public List<ushort> InfectedIds { get; set; }

        public GameStartPacket() {
            InfectedIds = new List<ushort>();
        }
        
        /// <inheritdoc />
        public void WriteData(IPacket packet) {
            packet.Write(IsInfected);
            
            packet.Write((ushort) InfectedIds.Count);

            foreach (var infectedId in InfectedIds) {
                packet.Write(infectedId);
            }
        }

        /// <inheritdoc />
        public void ReadData(IPacket packet) {
            IsInfected = packet.ReadBool();

            var length = packet.ReadUShort();
            for (var i = 0; i < length; i++) {
                InfectedIds.Add(packet.ReadUShort());
            }
        }

        /// <inheritdoc />
        public bool IsReliable => true;
        /// <inheritdoc />
        public bool DropReliableDataIfNewerExists => true;
    }

    /// <summary>
    /// Packet data for the game end.
    /// </summary>
    public class GameEndPacket : IPacketData {
        /// <summary>
        /// Whether the game has a winner.
        /// </summary>
        public bool HasWinner { get; set; }
        /// <summary>
        /// The ID of the winner if there is a winner.
        /// </summary>
        public ushort WinnerId { get; set; }

        /// <inheritdoc />
        public void WriteData(IPacket packet) {
            packet.Write(HasWinner);
            if (HasWinner) {
                packet.Write(WinnerId);
            }
        }

        /// <inheritdoc />
        public void ReadData(IPacket packet) {
            HasWinner = packet.ReadBool();
            if (HasWinner) {
                WinnerId = packet.ReadUShort();
            }
        }
        
        /// <inheritdoc />
        public bool IsReliable => true;
        /// <inheritdoc />
        public bool DropReliableDataIfNewerExists => true;
    }

    /// <summary>
    /// Packet data for a client-bound tag.
    /// </summary>
    public class ClientTagPacket : IPacketData {
        /// <summary>
        /// The ID of the tagged player.
        /// </summary>
        public ushort TaggedId { get; set; }
        /// <summary>
        /// The number of uninfected left.
        /// </summary>
        public ushort NumLeft { get; set; }
        /// <summary>
        /// Whether the tag was caused by a disconnect.
        /// </summary>
        public bool Disconnect { get; set; }
        
        /// <inheritdoc />
        public void WriteData(IPacket packet) {
            packet.Write(TaggedId);
            packet.Write(NumLeft);
            packet.Write(Disconnect);
        }

        /// <inheritdoc />
        public void ReadData(IPacket packet) {
            TaggedId = packet.ReadUShort();
            NumLeft = packet.ReadUShort();
            Disconnect = packet.ReadBool();
        }

        /// <inheritdoc />
        public bool IsReliable => true;
        /// <inheritdoc />
        public bool DropReliableDataIfNewerExists => true;
    }
}
