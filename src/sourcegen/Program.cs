// Rye © 2024
// Defines all the packets used by RainWorldwide
//   0x1X Session Management
//   0x2X Syncing
//   0x3X Realizing creatures
//   0x4X Updating 
string packets = """
    0x10 JoinClient
        i32 PlayerID
        str SlugcatWorld
        str StartingRoom
    0x20 SyncTick
    0x21 DestroyObject
        i32 ID
    0x22 KillCreature
        i32 ID
    0x23 Grab
        i32 GrabberID
        i32 GrabbedID
        f32 Dominance
        u8  GraspUsed
        u8  ChunkGrabbed
        u8  Pacifying
        str Shareability
    0x24 Release
        i32 GrabberID
        i32 GrabbedID
        u8  GraspUsed
    0x30 RealizePlayer
        i32 PlayerID
        i32 Room
    0x40 UpdatePlayer
        i32 PlayerID
        i32 Input
        fvec HeadPos
        fvec HeadVel
        fvec BodyPos
        fvec BodyVel
    """;

#region GENERATION CODE
string generated = """
    using LiteNetLib;
    using LiteNetLib.Utils;
    
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
    "u8" => "byte",
    "i32" => "int",
    "f32" => "float",
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
