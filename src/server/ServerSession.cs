using LiteNetLib;

namespace Server;

sealed class ServerSession(RainWorldGame game) : StoryGameSession(new(ServerConfig.SlugcatWorld), game)
{
    // TODO: Inspect .StoryPlayerCount

    // Saved value
    int MaxPID = 0;

    // Player ID decided whenever a peer logs in.
    readonly Dictionary<NetPeer, int> peers = [];

    public void Connect(NetPeer peer)
    {
        var player = GetOrCreatePlayer(peer);
        if (player.realizedObject == null) {
            player.Room.AddEntity(player);
            player.RealizeInRoom();
        }

        IntroduceClient packet = new(player.ID(), saveStateNumber.ToString(), player.Room.name);

        peer.Send(packet, DeliveryMethod.ReliableOrdered);
        peer.Send(new SyncTick(), DeliveryMethod.ReliableOrdered);

        CatchUp(peer);
    }

    private WorldCoordinate CreateNewPlayerPos()
    {
        return new(game.world.GetAbstractRoom(ServerConfig.StartingRoom).index, 5, 5, -1);
    }

    private AbstractCreature GetOrCreatePlayer(NetPeer peer)
    {
        // Create player's PID if it doesn't exist
        if (!peers.TryGetValue(peer, out int pid)) {
            peers[peer] = pid = MaxPID++;

            // Create new abstract player and add it
            EntityID id = new(-1, pid); // TODO: other objects' IDs start at 1000, so this is a safe bet.. for now. Make IDs start at 100,000 later.
            AbstractCreature player = new(game.world, StaticWorld.GetCreatureTemplate(CreatureTemplate.Type.Slugcat), null, CreateNewPlayerPos(), id);
            player.state = new PlayerState(player, pid, SlugcatStats.Name.White, false);

            base.AddPlayer(player);
        }

        return Players[pid];
    }

    public override void AddPlayer(AbstractCreature player)
    {
        if (((PlayerState)player.state).playerNumber == -1) {
            base.AddPlayer(player);
        } else {
            throw new ArgumentException($"Players with no associated client must have a playerNumber of -1.");
        }
    }

    private void CatchUp(NetPeer peer)
    {

    }

    public void Leave(NetPeer peer)
    {
        // TODO
    }

    public SlugcatStats GetStatsFor(int playerID)
    {
        return new SlugcatStats(SlugcatStats.Name.White, false);
    }
}