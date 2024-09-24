using LiteNetLib;

namespace Client;

enum ClientState
{
    Disconnected, Connecting, Connected
}

sealed class ClientNetState
{
    public static ClientNetState Get()
    {
        return ns;
    }

    private static readonly ClientNetState ns = new();
    private ClientNetState()
    {
        EventBasedNetListener lis = new();
        lis.NetworkReceiveEvent += Lis_NetworkReceiveEvent;
        lis.PeerConnectedEvent += Lis_PeerConnectedEvent;
        lis.PeerDisconnectedEvent += Lis_PeerDisconnectedEvent;
        Client = new(lis);

        On.RainWorld.Update += RainWorld_Update;
    }

    public NetManager Client { get; }
    public NetPeer Server => Client.FirstPeer;
    public ClientState ClientState { get; private set; }

    public void Disconnect()
    {
        ClientState = ClientState.Disconnected;
        Client.Stop(sendDisconnectMessages: true);
    }

    public void Connect(string address, int port)
    {
        Disconnect();
        ClientState = ClientState.Connecting;
        Client.Start();
        Client.Connect(address, port, "RWW");
        Log($"Connecting to server at {address}:{port}");
    }

    private void Lis_PeerDisconnectedEvent(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        ClientState = ClientState.Disconnected;
        Log($"Disconnected from server: {disconnectInfo.Reason}");
    }

    private void Lis_PeerConnectedEvent(NetPeer peer)
    {
        ClientState = ClientState.Connected;
        Log($"Connected to server");
    }

    private void Lis_NetworkReceiveEvent(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
    {
        int serverTick = reader.GetInt();
        byte type = reader.GetByte();
        switch (type) {
            case 0x10: EnterGame.Queue.Enqueue(serverTick, new(reader.GetByte(), reader.GetInt(), reader.GetString())); break;
        }
        AssumeEqual(reader.AvailableBytes, 0, twoExpr: "expected");
    }

    private void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
    {
        try {
            Client.PollEvents();
        } catch (Exception e) {
            LogError($"Error in client logic: {e}");
        }

        orig(self);
    }
}
