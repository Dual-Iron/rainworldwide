using Common;
using LiteNetLib;

namespace Server;

sealed class ServerNet
{
    public static ServerNet State { get; } = new();
    private ServerNet()
    {
        ProcessArgs(out int port);

        string ip = GetLocalIPAddress();
        Log(ip);

        // Port forwarding
        Upnp.Open(port);

        // Start server
        EventBasedNetListener lis = new();

        server = new(lis) { AutoRecycle = true };
        lis.NetworkReceiveEvent += Packets.Receive;
        lis.ConnectionRequestEvent += Lis_ConnectionRequestEvent;
        lis.PeerConnectedEvent += Lis_PeerConnectedEvent;
        lis.PeerDisconnectedEvent += Lis_PeerDisconnectedEvent;

        On.RainWorld.Update += RainWorld_Update;

        server.Start(port);

        Log("Ready for client connections");
    }

    readonly NetManager server;

    public readonly List<NetPeer> waitingToConnect = [];

    public void Stop()
    {
        server.Stop(sendDisconnectMessages: true);
    }

    private static void Lis_PeerDisconnectedEvent(NetPeer peer, DisconnectInfo info)
    {
        Log($"Lost connection to {peer.Address}: {info.Reason}");

        if (Game()?.session is ServerSession session) {
            session.Leave(peer);
        }
    }

    private void Lis_PeerConnectedEvent(NetPeer peer)
    {
        Log($"Client {peer.Address} connected");

        if (Game()?.session is ServerSession session) {
            session.Connect(peer);
        } else {
            waitingToConnect.Add(peer);
        }
    }

    private void Lis_ConnectionRequestEvent(ConnectionRequest request)
    {
        if (server.ConnectedPeersCount < Packets.ConnectionMaximum)
            request.AcceptIfKey(Packets.ConnectionKey);
        else
            request.Reject();
    }

    private static void ProcessArgs(out int port)
    {
        port = Packets.DefaultPort;

        string[] args = Environment.GetCommandLineArgs();
        Log(args);

        var portStr = args.FirstOrDefault(a => a.StartsWith("-port="));
        if (portStr != null && ushort.TryParse(portStr.Substring(6), out ushort _port) && port != _port) {
            port = _port;
            Log("Port set to " + port);
        }

        if (!args.Contains("-batchmode")) {
            LogError("Server not in batchmode");
        }
    }

    private void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
    {
        try {
            Packets.LocalTick++;
            server.PollEvents();
        }
        catch (Exception e) {
            LogError($"Server logic error: {e}");
        }

        orig(self);
    }
}
