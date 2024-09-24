using LiteNetLib;
using LiteNetLib.Utils;

static class Packets
{
    private static readonly NetDataWriter writer = new();

    public const int DefaultPort = 10933;
    public const int ConnectionMaximum = 100;
    public const string ConnectionKey = "RWWide";

    /// <summary>The local tick value.</summary>
    public static int LocalTick;

    public static void Send<T>(this NetPeer peer, T value, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered) where T : IPacket
    {
        writer.Reset();
        writer.Put(LocalTick);
        writer.Put((byte)value.PacketCode());
        writer.Put(value);
        peer.Send(writer, deliveryMethod);
    }

    public static void Receive(NetPeer _peer, NetPacketReader reader, byte _channel, DeliveryMethod _deliveryMethod)
    {
        int senderTick = reader.GetInt();
        byte type = reader.GetByte();
        switch (type) {
            case 0x10: IntroduceClient.Queue.Enqueue(senderTick, reader.Get<IntroduceClient>()); break;
            case 0x11: SyncTick.Queue.Enqueue(senderTick, reader.Get<SyncTick>()); break;
        }
        AssumeEqual(reader.AvailableBytes, 0, twoExpr: "expected");
    }
}

sealed class PacketQueue<T> where T : struct
{
    readonly Queue<(int, T)> packets = new(16);

    public void Enqueue(int senderTick, T packet)
    {
        packets.Enqueue((senderTick, packet));
    }

    public bool Latest(out T packet)
    {
        bool any = false;
        while (Dequeue(out _, out packet)) {
            // Drain all the packets currently queued
            any = true;
        }
        return any;
    }

    public bool Latest(out int senderTick, out T packet)
    {
        bool any = false;
        while (Dequeue(out senderTick, out packet)) {
            // Drain all the packets currently queued
            any = true;
        }
        return any;
    }

    public bool Dequeue(out int senderTick, out T packet)
    {
        if (packets.Count == 0) {
            senderTick = 0;
            packet = default;
            return false;
        }
        (senderTick, packet) = packets.Dequeue();
        return true;
    }
}

interface IPacket : INetSerializable
{
    int PacketCode();
}

#pragma warning disable IDE0250
#pragma warning disable IDE0251 // Make member 'readonly'

record struct IntroduceClient(int PlayerID, string SlugcatWorld, string StartingRoom) : IPacket
{
    public static readonly PacketQueue<IntroduceClient> Queue = new();

    public int PacketCode() => 0x10;

    public void Deserialize(NetDataReader reader)
    {
        PlayerID = reader.GetInt();
        SlugcatWorld = reader.GetString();
        StartingRoom = reader.GetString();
    }

    public void Serialize(NetDataWriter writer)
    {
        writer.Put(PlayerID);
        writer.Put(SlugcatWorld);
        writer.Put(StartingRoom);
    }
}

record struct SyncTick() : IPacket
{
    public static readonly PacketQueue<SyncTick> Queue = new();

    public int PacketCode() => 0x11;

    public void Deserialize(NetDataReader reader) { }

    public void Serialize(NetDataWriter writer) { }
}
