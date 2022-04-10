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
        /// Event that is called when the game info is received.
        /// </summary>
        public event Action<GameInfoPacket> GameInfoEvent;

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
        public event Action<GameInfoPacket> GameInProgressEvent;

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

            netReceiver.RegisterPacketHandler<GameInfoPacket>(
                ClientPacketId.GameInfo,
                packetData => GameInfoEvent?.Invoke(packetData)
            );
            netReceiver.RegisterPacketHandler<GameStartPacket>(
                ClientPacketId.GameStart,
                packetData => GameStartedEvent?.Invoke(packetData)
            );
            netReceiver.RegisterPacketHandler<GameEndPacket>(
                ClientPacketId.GameEnd,
                packetData => GameEndedEvent?.Invoke(packetData)
            );
            netReceiver.RegisterPacketHandler<GameInfoPacket>(
                ClientPacketId.GameInProgress,
                packetData => GameInProgressEvent?.Invoke(packetData)
            );
            netReceiver.RegisterPacketHandler<ClientTagPacket>(
                ClientPacketId.PlayerTag,
                packetData => PlayerTaggedEvent?.Invoke(packetData)
            );
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
                case ClientPacketId.GameInfo:
                case ClientPacketId.GameInProgress:
                    return new GameInfoPacket();
                case ClientPacketId.GameStart:
                    return new GameStartPacket();
                case ClientPacketId.GameEnd:
                    return new GameEndPacket();
                case ClientPacketId.PlayerTag:
                    return new ClientTagPacket();
            }

            return null;
        }
    }
}
