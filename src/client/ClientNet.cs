using Common;
using LiteNetLib;

namespace Client;

enum ConnectionProgress
{
    Disconnected, Connecting, Connected
}

sealed class ClientNet
{
    public static ClientNet State { get; } = new();
    private ClientNet()
    {
        EventBasedNetListener lis = new();
        lis.NetworkReceiveEvent += Packets.Receive;
        lis.PeerConnectedEvent += PeerConnectedEvent;
        lis.PeerDisconnectedEvent += PeerDisconnectedEvent;
        client = new(lis);

        On.RainWorld.Update += RainWorld_Update;
    }

    private readonly NetManager client;

    public ConnectionProgress Progress { get; private set; }
    public JoinClient? IntroPacket { get; private set; }

    public void Disconnect()
    {
        IntroPacket = null;
        Progress = ConnectionProgress.Disconnected;
        client.Stop(sendDisconnectMessages: true);
    }

    public void Connect(string address, int port)
    {
        // When client connects and server is ready to join them into the game world,
        //   the server sends an RealizePlayer packet. The client checks for this in the menu.
        // The packet is stored in IntroPacket, the game switches to RainWorldGame,
        //   and hooks check for IntroPacket's existence.
        Disconnect();
        Progress = ConnectionProgress.Connecting;
        client.Start();
        client.Connect(address, port, Packets.ConnectionKey);
        Log($"Connecting to server at {address}:{port}");
    }

    public void IntroducedToSession(JoinClient packet)
    {
        IntroPacket = packet;
    }

    private void PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Progress = ConnectionProgress.Disconnected;
        Log($"Disconnected from server: {disconnectInfo.Reason}");
    }

    private void PeerConnectedEvent(NetPeer peer)
    {
        Progress = ConnectionProgress.Connected;
        Log($"Connected to server");
    }

    private void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
    {
        try {
            if (SyncTick.Queue.Latest(out int tick, out _)) {
                Packets.LocalTick = tick;
            } else {
                Packets.LocalTick += 1;
            }
            client.PollEvents();
        } catch (Exception e) {
            LogError($"Error in client logic: {e}");
        }

        orig(self);
    }
}
