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
