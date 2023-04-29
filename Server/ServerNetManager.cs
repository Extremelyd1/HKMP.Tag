using System;
using System.Collections.Generic;
using Hkmp.Api.Server;
using Hkmp.Api.Server.Networking;
using Hkmp.Networking.Packet;
using Hkmp.Networking.Packet.Data;

namespace HkmpTag.Server {
    /// <summary>
    /// Class that manages server-side networking for Tag.
    /// </summary>
    public class ServerNetManager {
        /// <summary>
        /// Event that is called when a player tag is received.
        /// </summary>
        public event Action<ushort> TaggedEvent;

        /// <summary>
        /// The server network sender.
        /// </summary>
        private readonly IServerAddonNetworkSender<ClientPacketId> _netSender;

        /// <summary>
        /// Construct the server network manager with the given server addon and net server instance.
        /// </summary>
        /// <param name="addon">The ServerAddon instance.</param>
        /// <param name="netServer">The net server instance.</param>
        public ServerNetManager(ServerAddon addon, INetServer netServer) {
            _netSender = netServer.GetNetworkSender<ClientPacketId>(addon);

            var netReceiver = netServer.GetNetworkReceiver<ServerPacketId>(addon, InstantiatePacket);
            netReceiver.RegisterPacketHandler(
                ServerPacketId.PlayerTag,
                id => TaggedEvent?.Invoke(id)
            );
        }

        /// <summary>
        /// Broadcast a game info packet to the given client.
        /// </summary>
        /// <param name="playerId">The ID of the player to send to.</param>
        /// <param name="preset"><see cref="CondensedGamePreset"/> that contains game preset information.</param>
        /// <param name="loadouts">The <see cref="Loadouts"/> to use for this game.</param>
        public void SendGameInfo(
            ushort playerId,
            CondensedGamePreset preset,
            Loadouts loadouts
        ) {
            _netSender.SendSingleData(
                ClientPacketId.GameInfo,
                new GameInfoPacket {
                    Preset = preset,
                    Loadouts = loadouts
                },
                playerId
            );
        }

        /// <summary>
        /// Send a game start packet with the information of the given players.
        /// </summary>
        /// <param name="players">A list of ServerTagPlayer instances that store whether they are infected.</param>
        public void SendGameStart(List<ServerTagPlayer> players) {
            var infectedIds = new List<ushort>();
            foreach (var player in players) {
                if (player.State == PlayerState.Infected) {
                    infectedIds.Add(player.Id);
                }
            }

            foreach (var player in players) {
                try {
                    _netSender.SendSingleData(
                        ClientPacketId.GameStart,
                        new GameStartPacket {
                            IsInfected = player.State == PlayerState.Infected,
                            InfectedIds = infectedIds
                        },
                        player.Id
                    );
                } catch {
                    // Just in case a player is no longer connected that we are trying to send to
                }
            }
        }

        /// <summary>
        /// Send a game end packet with the information about game winner.
        /// </summary>
        /// <param name="players">A list of ServerTagPlayer instances to send the game end to.</param>
        /// <param name="hasWinner">Whether the game has a winner.</param>
        /// <param name="winnerId">The ID of the winner.</param>
        public void SendGameEnd(
            List<ServerTagPlayer> players, 
            bool hasWinner, 
            ushort winnerId = 0
        ) {
            foreach (var player in players) {
                try {
                    _netSender.SendSingleData(
                        ClientPacketId.GameEnd,
                        new GameEndPacket {
                            HasWinner = hasWinner,
                            IsWinner = winnerId == player.Id,
                            WinnerId = winnerId
                        },
                        player.Id
                    );
                } catch {
                    // Just in case a player is no longer connected that we are trying to send to
                }
            }
        }

        /// <summary>
        /// Send a game in progress packet to the game player.
        /// </summary>
        /// <param name="playerId">The ID of the player.</param>
        /// <param name="preset"><see cref="CondensedGamePreset"/> that contains game preset information.</param>
        /// <param name="loadouts">The <see cref="Loadouts"/> to use for this game.</param>
        public void SendGameInProgress(
            ushort playerId,
            CondensedGamePreset preset,
            Loadouts loadouts
        ) {
            _netSender.SendSingleData(
                ClientPacketId.GameInProgress,
                new GameInfoPacket {
                    Preset = preset,
                    Loadouts = loadouts
                },
                playerId
            );
        }

        /// <summary>
        /// Send a tag packet to all players that a player has been tagged, how many players are left and whether
        /// it was a disconnect or a normal tag.
        /// </summary>
        /// <param name="players">The list of players to send to.</param>
        /// <param name="taggedId">The ID of the tagged player.</param>
        /// <param name="numLeft">The number of uninfected left.</param>
        /// <param name="disconnect">Whether the player was tagged by disconnect.</param>
        public void SendTag(
            List<ServerTagPlayer> players, 
            ushort taggedId, 
            ushort numLeft, 
            bool disconnect
        ) {
            foreach (var player in players) {
                try {
                    _netSender.SendSingleData(ClientPacketId.PlayerTag, new ClientTagPacket {
                        WasTagged = player.Id == taggedId,
                        TaggedId = taggedId,
                        NumLeft = numLeft,
                        Disconnect = disconnect
                    }, player.Id);
                } catch {
                    // Just in case a player is no longer connected that we are trying to send to
                }
            }
        }

        /// <summary>
        /// Function to instantiate IPacketData instances given a packet ID.
        /// </summary>
        /// <param name="packetId">The server packet ID.</param>
        /// <returns>An instance of IPacketData.</returns>
        private static IPacketData InstantiatePacket(ServerPacketId packetId) {
            switch (packetId) {
                case ServerPacketId.PlayerTag:
                    return new ReliableEmptyData();
            }

            return null;
        }
    }
}
