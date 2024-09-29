using LiteNetLib;
using LiteNetLib.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetworkShared;
using NetworkShared.Registries;
using System;
using System.Net;
using System.Net.Sockets;
using TTT.Server.Games;

namespace TTT.Server
{
    public class NetworkServer : INetEventListener
    {
        NetManager netManager;
        private readonly ILogger<NetworkServer> logger;
        private readonly IServiceProvider serviceProvider;
        private UsersManager usersManager;
        private readonly NetDataWriter cachedWriter = new NetDataWriter();

        public NetworkServer(
            ILogger<NetworkServer> logger,
            IServiceProvider provider)
        {
            this.logger = logger;
            serviceProvider = provider;
        }

        public void Start()
        {
            netManager = new NetManager(this)
            {
                DisconnectTimeout = 100000
            };

            netManager.Start(8070);
            usersManager = serviceProvider.GetRequiredService<UsersManager>();

            Console.WriteLine("Server listening on port 8070");
        }

        public void PollEvents()
        {
            netManager.PollEvents();
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            Console.WriteLine($"Incomming connection from {request.RemoteEndPoint}");
            request.Accept();
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                try
                {
                    var packetType = (PacketType)reader.GetByte();
                    var packet = ResolvePacket(packetType, reader);
                    var handler = ResolveHandler(packetType);

                    handler.Handle(packet, peer.Id);

                    reader.Recycle();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error processing message of type XX");
                }
            }
        }

        public void OnPeerConnected(NetPeer peer)
        {
            logger.LogInformation($"Client connected to server: {peer.EndPoint}. Id: {peer.Id}");
            usersManager.AddConnection(peer);
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            var connection = usersManager.GetConnection(peer.Id);
            netManager.DisconnectPeer(peer);
            usersManager.Disconnect(peer.Id);
            logger.LogInformation($"{connection?.User?.Id} disconnected: {peer.EndPoint}");
        }

        public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            //throw new NotImplementedException();
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            //throw new NotImplementedException();
        }

        public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            //throw new NotImplementedException();
        }

        public void SendClient(int peerId, INetPacket packet, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        {
            var peer = usersManager.GetConnection(peerId).Peer;
            peer.Send(WriteSerializable(packet), method);
        }

        public IPacketHandler ResolveHandler(PacketType packetType)
        {
            var registry = serviceProvider.GetRequiredService<HandlerRegistry>();
            var type = registry.Handlers[packetType];
            return (IPacketHandler)serviceProvider.GetRequiredService(type);
        }

        private INetPacket ResolvePacket(PacketType packetType, NetPacketReader reader)
        {
            var registry = serviceProvider.GetRequiredService<PacketRegistry>();
            var type = registry.PacketTypes[packetType];
            var packet = (INetPacket)Activator.CreateInstance(type);
            packet.Deserialize(reader);
            return packet;
        }

        private NetDataWriter WriteSerializable(INetPacket packet)
        {
            cachedWriter.Reset();
            packet.Serialize(cachedWriter);
            return cachedWriter;
        }
    }
}
