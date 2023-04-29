using System;
using System.Collections.Generic;
using Hkmp.Networking.Packet;

namespace HkmpTag {
    /// <summary>
    /// Packet data for client-bound game information. Where to warp to, which transitions in which scenes
    /// are restricted.
    /// </summary>
    public class GameInfoPacket : IPacketData {
        /// <summary>
        /// The maximum number of scenes to have transition restrictions for.
        /// </summary>
        private const byte MaxScenes = byte.MaxValue;

        /// <summary>
        /// The maximum number of transitions in one scene that can be restricted.
        /// </summary>
        private const byte MaxTransitions = byte.MaxValue;

        /// <summary>
        /// The <see cref="CondensedGamePreset"/> to use in the game.
        /// </summary>
        public CondensedGamePreset Preset { get; set; }
        
        /// <summary>
        /// The <see cref="Loadouts"/> to use for this game.
        /// </summary>
        public Loadouts Loadouts { get; set; }

        public GameInfoPacket() {
            Preset = new CondensedGamePreset();
            Loadouts = new Loadouts();
        }

        /// <inheritdoc />
        public void WriteData(IPacket packet) {
            packet.Write(Preset.WarpSceneIndex);
            packet.Write(Preset.WarpTransitionIndex);

            var dictCount = (byte)Math.Min(MaxScenes, Preset.SceneTransitions.Count);
            packet.Write(dictCount);

            foreach (var sceneTransitionsPair in Preset.SceneTransitions) {
                var sceneIndex = sceneTransitionsPair.Key;
                var transitions = sceneTransitionsPair.Value;

                packet.Write(sceneIndex);

                var transitionLength = (byte)Math.Min(MaxTransitions, transitions.Length);
                packet.Write(transitionLength);

                for (var i = 0; i < transitionLength; i++) {
                    packet.Write(transitions[i]);
                }
            }

            Loadouts.NormalLoadout.WriteData(packet);
            Loadouts.InfectedLoadout.WriteData(packet);
        }

        /// <inheritdoc />
        public void ReadData(IPacket packet) {
            Preset.WarpSceneIndex = packet.ReadUShort();
            Preset.WarpTransitionIndex = packet.ReadByte();

            var dictCount = packet.ReadByte();
            for (var i = 0; i < dictCount; i++) {
                var sceneIndex = packet.ReadUShort();

                var transitionLength = packet.ReadByte();
                var transitions = new byte[transitionLength];

                for (var j = 0; j < transitionLength; j++) {
                    transitions[j] = packet.ReadByte();
                }

                Preset.SceneTransitions[sceneIndex] = transitions;
            }

            Loadouts.NormalLoadout.ReadData(packet);
            Loadouts.InfectedLoadout.ReadData(packet);
        }

        /// <inheritdoc />
        public bool IsReliable => true;

        /// <inheritdoc />
        public bool DropReliableDataIfNewerExists => true;
    }

    /// <summary>
    /// Packet data for client-bound loadout information. Which charms to have equipped and which skills are
    /// enabled.
    /// </summary>
    public class LoadoutPacket : IPacketData {
        /// <summary>
        /// Loadout for non-infected players.
        /// </summary>
        public Loadout NormalLoadout { get; set; }
        
        /// <summary>
        /// Loadout for infected players.
        /// </summary>
        public Loadout InfectedLoadout { get; set; }

        public LoadoutPacket() {
            NormalLoadout = new Loadout();
            InfectedLoadout = new Loadout();
        }
        
        /// <inheritdoc />
        public void WriteData(IPacket packet) {
            NormalLoadout.WriteData(packet);
            InfectedLoadout.WriteData(packet);
        }

        /// <inheritdoc />
        public void ReadData(IPacket packet) {
            NormalLoadout.ReadData(packet);
            InfectedLoadout.ReadData(packet);
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

            packet.Write((ushort)InfectedIds.Count);

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
        /// Whether the receiving player was tagged.
        /// </summary>
        public bool WasTagged { get; set; }
        
        /// <summary>
        /// The number of uninfected left.
        /// </summary>
        public ushort NumLeft { get; set; }
    
        /// <summary>
        /// The ID of the tagged player.
        /// </summary>
        public ushort TaggedId { get; set; }

        /// <summary>
        /// Whether the tag was caused by a disconnect.
        /// </summary>
        public bool Disconnect { get; set; }

        /// <inheritdoc />
        public void WriteData(IPacket packet) {
            packet.Write(WasTagged);
            packet.Write(NumLeft);
            
            if (!WasTagged) {
                packet.Write(TaggedId);
                packet.Write(Disconnect);
            }
        }

        /// <inheritdoc />
        public void ReadData(IPacket packet) {
            WasTagged = packet.ReadBool();
            NumLeft = packet.ReadUShort();

            if (!WasTagged) {
                TaggedId = packet.ReadUShort();
                Disconnect = packet.ReadBool();
            }
        }

        /// <inheritdoc />
        public bool IsReliable => true;

        /// <inheritdoc />
        public bool DropReliableDataIfNewerExists => true;
    }
}
