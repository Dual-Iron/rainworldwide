namespace Client;

sealed class ClientSession(IntroduceClient c, RainWorldGame game) : StoryGameSession(new(c.SlugcatWorld), game)
{
    public int PID { get; } = c.PlayerID;
    public string StartingRoom { get; } = c.StartingRoom;
    public SlugcatStats.Name World { get; } = new(c.SlugcatWorld);

    public override void AddPlayer(AbstractCreature player)
    {
        if (player.ID() == PID) {
            base.AddPlayer(player);

            Log($"Added my player ({player.ID()}) to session");
        }
    }

    public SlugcatStats GetStatsFor(int _playerID)
    {
        return new(SlugcatStats.Name.White, false);
    }

    public AbstractCreature MyPlayer => Players.Count > 0 ? Players[0] : null;
}
