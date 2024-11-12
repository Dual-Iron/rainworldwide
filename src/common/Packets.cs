using LiteNetLib;
using LiteNetLib.Utils;
using RWCustom;

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

    public static void Put(this NetDataWriter writer, Vector2 vec)
    {
        writer.Put(vec.x);
        writer.Put(vec.y);
    }
    public static void Put(this NetDataWriter writer, IntVector2 vec)
    {
        writer.Put(vec.x);
        writer.Put(vec.y);
    }
    public static Vector2 GetVector2(this NetDataReader reader) => new(x: reader.GetFloat(), y: reader.GetFloat());
    public static IntVector2 GetIntVector2(this NetDataReader reader) => new(p1: reader.GetInt(), p2: reader.GetInt());
}

sealed class PacketQueue<T> where T : struct
{
    readonly Queue<(int, T)> packets = new(16);

    public void Enqueue(int senderTick, T packet) => packets.Enqueue((senderTick, packet));

    public IEnumerable<T> All()
    {
        while (Dequeue(out _, out T p)) {
            yield return p;
        }
    }

    public bool Latest(out T packet) => Latest(out int _, out packet);
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
