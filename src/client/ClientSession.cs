using Common;

namespace Client;

sealed class ClientSession(JoinClient c, RainWorldGame game) : StoryGameSession(new(c.SlugcatWorld), game)
{
    public int PlayerID { get; } = c.PlayerID;
    public string StartingRoom { get; } = c.StartingRoom;
    public SlugcatStats.Name SlugcatWorld { get; } = new(c.SlugcatWorld);

    public readonly ByID<PhysicalObject> Objects = [];
    public readonly ByID<UpdatePlayer> PlayerUpdates = [];

    public AbstractCreature MyPlayer => Players.FirstOrDefault(p => p.ID() == PlayerID);

    public override void AddPlayer(AbstractCreature player)
    {
        int pid = PlayerID;

        if (player.ID() == pid) {
            Players.Add(player);

            if (playerSessionRecords.Length < pid + 1) {
                Array.Resize(ref playerSessionRecords, pid + 1);
                playerSessionRecords[pid] = new(pid);
            }

            Log($"Added player ID.{player.ID()} (self) to session");
        }
    }

    public SlugcatStats GetStatsFor(int _playerID) => new(SlugcatStats.Name.White, false);

    readonly ClientRoomLogic clientRooms = new(game);
    public void UpdateRoomLogic()
    {
        clientRooms.UpdateRoomLogic();
    }

    internal void PostUpdate()
    {
        clientRooms.PostUpdate();
    }
}
