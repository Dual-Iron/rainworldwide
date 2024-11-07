// Rye © 2024
// Defines all the packets used by RainWorldwide
//   0x1X - Session Management
//   0x2X - Syncing
//   0x3X - Syncing
string packets = """
    0x10 RealizePlayer
        i32 PlayerID
        str SlugcatWorld
        str StartingRoom
    0x20 SyncTick
    """;

#region GENERATION CODE
string generated = """
    using LiteNetLib;
    using LiteNetLib.Utils;
    using RWCustom;
    
    namespace Common;
    
    """;

string SplitWord()
{
    string[] split = packets.Split([' ', '\n'], 2);
    packets = split.Length == 1 ? "" : split[1].Trim();
    return split[0].Trim();
}

static string ToCSharp(string ty) => ty switch
{
    "i32" => "int",
    "str" => "string",
    "fvec" => "Vector2",
    "ivec" => "IntVector2",
    _ => ty,
};

List<(string n, string c)> packetNames = [];

while (packets.Length > 0) {
    string packetCode = SplitWord();
    string packetName = SplitWord();
    List<(string ty, string name)> fields = [];

    while (!packets.StartsWith('0') && packets.Length > 0) {
        fields.Add((ToCSharp(SplitWord()), SplitWord()));
    }

    string fieldString = string.Join(", ", fields.Select(t => $"{t.ty} {t.name}"));
    string fieldDeserialize = string.Join("\n        ", fields.Select(t => $"{t.name} = reader.Get{char.ToUpper(t.ty[0]) + t.ty[1..]}();"));
    string fieldSerialize = string.Join("\n        ", fields.Select(t => $"writer.Put({t.name});"));

    generated += $$"""

        record struct {{packetName}}({{fieldString}}) : IPacket
        {
            public static readonly PacketQueue<{{packetName}}> Queue = new();

            public int PacketCode() => {{packetCode}};

            public void Deserialize(NetDataReader reader)
            {
                {{fieldDeserialize}}
            }

            public void Serialize(NetDataWriter writer)
            {
                {{fieldSerialize}}
            }
        }

        """;

    packetNames.Add((packetName, packetCode));
}

string cases = string.Join(
    "\n            ", 
    packetNames.Select(t => $"case {t.c}: {t.n}.Queue.Enqueue(senderTick, reader.Get<{t.n}>()); break;")
    );

generated += $$"""

    static partial class Packets
    {
        public static void Receive(NetPeer _peer, NetPacketReader reader, byte _channel, DeliveryMethod _deliveryMethod)
        {
            byte type = reader.GetByte();
            int senderTick = reader.GetInt();
            switch (type) {
                {{cases}}
            }
            AssumeEqual(reader.AvailableBytes, 0, twoExpr: "expected");
        }

        public static Vector2 GetVector2(this NetDataReader reader) => new(x: reader.GetFloat(), y: reader.GetFloat());
        public static IntVector2 GetIntVector2(this NetDataReader reader) => new(p1: reader.GetInt(), p2: reader.GetInt());
    }

    """;

// Normalize line endings.
generated = generated.Replace("\r\n", "\n").Replace("\n", "\r\n");

// Write it all to the src/common folder.
string path = Environment.ProcessPath;
path = path[..path.LastIndexOf("\\src\\")];
path += "/src/common/Packets.generated.cs";

File.WriteAllText(path, generated);
#endregion
