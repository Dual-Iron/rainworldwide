static class PacketStandards
{
    public const string ConnectionKey = "RWW";
}

sealed class PacketQueue<T> where T : struct
{
    readonly Queue<(int, T)> packets = new(16);

    public void Enqueue(int serverTick, T packet)
    {
        packets.Enqueue((serverTick, packet));
    }

    public bool Dequeue(out int serverTick, out T packet)
    {
        if (packets.Count == 0) {
            serverTick = 0;
            packet = default;
            return false;
        }
        (serverTick, packet) = packets.Dequeue();
        return true;
    }
}

record struct EnterGame(byte SlugcatWorld, int ClientPid, string StartingRoom)
{
    public static readonly PacketQueue<EnterGame> Queue = new();
    public readonly int ID => 0x10;
}
