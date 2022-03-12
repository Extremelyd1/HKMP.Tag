using System;
using Hkmp.Api.Client;
using Hkmp.Api.Client.Networking;
using Hkmp.Networking.Packet;
using Hkmp.Networking.Packet.Data;

namespace HkmpTag.Client {
    /// <summary>
    /// Class that manages client-side networking for Tag.
    /// </summary>
    public class ClientNetManager {
        /// <summary>
        /// Event that is called when the game is started.
        /// </summary>
        public event Action<GameStartPacket> GameStartedEvent;

        /// <summary>
        /// Event that is called when the game is ended.
        /// </summary>
        public event Action<GameEndPacket> GameEndedEvent;

        /// <summary>
        /// Event that is called when the game is in progress.
        /// </summary>
        public event Action GameInProgressEvent;

        /// <summary>
        /// Event that is called when another player is tagged.
        /// </summary>
        public event Action<ClientTagPacket> PlayerTaggedEvent;

        /// <summary>
        /// The client network sender.
        /// </summary>
        private readonly IClientAddonNetworkSender<ServerPacketId> _netSender;

        /// <summary>
        /// Construct the network manager with the given addon and net client.
        /// </summary>
        /// <param name="addon">The client addon for getting the network sender and receiver.</param>
        /// <param name="netClient">The net client interface for accessing network related methods.</param>
        public ClientNetManager(ClientAddon addon, INetClient netClient) {
            _netSender = netClient.GetNetworkSender<ServerPacketId>(addon);

            var netReceiver = netClient.GetNetworkReceiver<ClientPacketId>(addon, InstantiatePacket);

            netReceiver.RegisterPacketHandler<GameStartPacket>(
                ClientPacketId.GameStart,
                packetData => GameStartedEvent?.Invoke(packetData)
            );
            netReceiver.RegisterPacketHandler<GameEndPacket>(
                ClientPacketId.GameEnd,
                packetData => GameEndedEvent?.Invoke(packetData)
            );
            netReceiver.RegisterPacketHandler(
                ClientPacketId.GameInProgress,
                () => GameInProgressEvent?.Invoke()
            );
            netReceiver.RegisterPacketHandler<ClientTagPacket>(
                ClientPacketId.PlayerTag,
                packetData => PlayerTaggedEvent?.Invoke(packetData)
            );
        }

        /// <summary>
        /// Send a request to start the game with the given number of initial infected.
        /// </summary>
        /// <param name="numInfected">The number of initial infected.</param>
        public void SendStartRequest(ushort numInfected) {
            _netSender.SendSingleData(ServerPacketId.StartRequest, new StartRequestPacket {
                NumInfected = numInfected
            });
        }

        /// <summary>
        /// Send a request to end the game.
        /// </summary>
        public void SendEndRequest() {
            _netSender.SendSingleData(ServerPacketId.EndRequest, new ReliableEmptyData());
        }

        /// <summary>
        /// Send the server that the local player is tagged.
        /// </summary>
        public void SendTagged() {
            _netSender.SendSingleData(ServerPacketId.PlayerTag, new ReliableEmptyData());
        }

        /// <summary>
        /// Function to instantiate packet data instances given a packet ID.
        /// </summary>
        /// <param name="packetId">The client packet ID.</param>
        /// <returns>An instance of IPacketData.</returns>
        private static IPacketData InstantiatePacket(ClientPacketId packetId) {
            switch (packetId) {
                case ClientPacketId.GameStart:
                    return new GameStartPacket();
                case ClientPacketId.GameEnd:
                    return new GameEndPacket();
                case ClientPacketId.GameInProgress:
                    return new ReliableEmptyData();
                case ClientPacketId.PlayerTag:
                    return new ClientTagPacket();
            }

            return null;
        }
    }
}
