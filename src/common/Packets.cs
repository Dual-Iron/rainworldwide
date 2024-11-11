using LiteNetLib;
using LiteNetLib.Utils;

namespace Common;

public interface IPacket : INetSerializable
{
    int PacketCode();
}

static partial class Packets
{
    private static readonly NetDataWriter writer = new();

    public const int DefaultPort = 10933;
    public const int ConnectionMaximum = 100;
    public const string ConnectionKey = "RWWide";

    /// <summary>The local tick value.</summary>
    public static int LocalTick;

    public static void Send<T>(this NetPeer peer, T value, DeliveryMethod deliveryMethod = DeliveryMethod.ReliableOrdered)
        where T : IPacket
    {
        writer.Reset();
        writer.Put((byte)value.PacketCode());
        writer.Put(LocalTick);
        writer.Put(value);
        peer.Send(writer, deliveryMethod);
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
        return Latest(out int _, out packet);
    }

    public bool Latest(out int senderTick, out T packet)
    {
        (senderTick, packet) = (-1, default);
        while (Dequeue(out int s, out T p)) {
            // Drain all the packets currently queued
            (senderTick, packet) = (s, p);
        }
        return senderTick != -1;
    }

    public bool Dequeue(out int senderTick, out T packet)
    {
        if (packets.Count == 0) {
            (senderTick, packet) = (-1, default);
            return false;
        }
        (senderTick, packet) = packets.Dequeue();
        return true;
    }
}
