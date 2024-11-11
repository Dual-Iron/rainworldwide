using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;

namespace Server.Hooks;

sealed class SessionHooks
{
    public void Hook()
    {
        // Fix number of story players
        new Hook(typeof(RainWorldGame).GetMethod("get_StoryPlayerCount"), getStoryPlayerCount);

        // Fix SlugcatStats access
        new Hook(typeof(Player).GetMethod("get_slugcatStats"), getSlugcatStats);
        new Hook(typeof(Player).GetMethod("get_Malnourished"), getMalnourished);

        // Fix rain
        new Hook(typeof(RainCycle).GetMethod("get_RainApproaching"), getRainApproaching);

        // Fix SlugcatWorld
        On.RoomSettings.ctor += RoomSettings_ctor;

        // Jump right into the game immediately (because lobbies aren't implemented)
        On.RainWorld.LoadSetupValues += ImmediateStart;

        // Do not game-over
        On.RainWorldGame.GameOver += delegate { };

        // Prevent errors and abnormal behavior with custom session type
        On.RainWorldGame.SpawnPlayers_bool_bool_bool_bool_WorldCoordinate += SpawnNobody;
        On.OverWorld.ctor += OverWorld_ctor;
        On.OverWorld.LoadFirstWorld += OverWorld_LoadFirstWorld;
        On.World.ctor += World_ctor;

        // Fix RoomCamera.followAbstractCreature
        IL.RainWorldGame.Update += RainWorldGame_Update;
        IL.RainWorldGame.ctor += RainWorldGame_ctor;
    }

    private readonly Func<Func<RainWorldGame, int>, RainWorldGame, int> getStoryPlayerCount = (orig, self) => {
        if (self.session is ServerSession session) {
            return session.Players.Count;
        }
        return orig(self);
    };

    private readonly Func<Func<Player, SlugcatStats>, Player, SlugcatStats> getSlugcatStats = (orig, self) => {
        if (Game().session is ServerSession session) {
            return session.GetStatsFor(self.ID());
        }
        return orig(self);
    };

    private readonly Func<Func<Player, bool>, Player, bool> getMalnourished = (orig, self) => self.slugcatStats.malnourished;

    private readonly Func<Func<RainCycle, float>, RainCycle, float> getRainApproaching = (orig, self) => Mathf.InverseLerp(0f, 2400f, self.TimeUntilRain);

    private void RoomSettings_ctor(On.RoomSettings.orig_ctor orig, RoomSettings self, string name, Region region, bool template, bool firstTemplate, SlugcatStats.Name playerChar)
    {
        orig(self, name, region, template, firstTemplate, new(ServerConfig.SlugcatWorld));
    }

    // TODO: remove "worldCreaturesSpawn = false"
    private RainWorldGame.SetupValues ImmediateStart(On.RainWorld.orig_LoadSetupValues orig, bool distributionBuild)
    {
        return orig(distributionBuild) with { startScreen = false, playMusic = false, worldCreaturesSpawn = false };
    }

    private AbstractCreature SpawnNobody(On.RainWorldGame.orig_SpawnPlayers_bool_bool_bool_bool_WorldCoordinate orig, RainWorldGame self, bool player1, bool player2, bool player3, bool player4, WorldCoordinate location)
    {
        return orig(self, false, false, false, false, location);
    }

    private void OverWorld_ctor(On.OverWorld.orig_ctor orig, OverWorld self, RainWorldGame game)
    {
        game.session = new ServerSession(game);
        game.startingRoom = ServerConfig.StartingRoom;

        orig(self, game);
    }

    private void OverWorld_LoadFirstWorld(On.OverWorld.orig_LoadFirstWorld orig, OverWorld self)
    {
        string startingRoom = self.game.startingRoom;
        string[] split = startingRoom.Split('_');
        if (split.Length < 2) {
            throw new InvalidOperationException($"Starting room is invalid: {startingRoom}");
        }
        string startingRegion = split[0];

        if (DirExistsAt(Custom.RootFolderDirectory(), "world", startingRegion)) {
            // Do nothing
        } else if (split.Length > 2 && DirExistsAt(Custom.RootFolderDirectory(), "world", split[1])) {
            startingRegion = split[1];
        } else {
            throw new InvalidOperationException($"Starting room has no matching region: {startingRoom}");
        }

        self.LoadWorld(startingRegion, new(ServerConfig.SlugcatWorld), false);
        self.FIRSTROOM = startingRoom;
    }

    private void World_ctor(On.World.orig_ctor orig, World self, RainWorldGame game, Region region, string name, bool singleRoomWorld)
    {
        orig(self, null, region, name, singleRoomWorld);

        self.game = game;

        if (game != null) {
            int seconds = UnityEngine.Random.Range(ServerConfig.CycleTimeSecondsMin, ServerConfig.CycleTimeSecondsMax + 1);

            self.rainCycle = new(self, minutes: 10) { cycleLength = seconds * 40 };

            TimeSpan time = TimeSpan.FromSeconds(seconds);
            Log($"Cycle length is {time}");
        }
    }

    private void RainWorldGame_Update(ILContext il)
    {
        ILCursor cursor = new(il);

        // Realize rooms with custom logic
        cursor.GotoNext(MoveType.Before, i => i.MatchLdfld<RainWorldGame>("roomRealizer"));
        cursor.EmitDelegate(RoomRealizerHook);

        static RainWorldGame RoomRealizerHook(RainWorldGame game)
        {
            if (game.session is ServerSession session) {
                session.Update();
            }
            return game;
        }
    }

    private void RainWorldGame_ctor(ILContext il)
    {
        ILCursor cursor = new(il);

        // Seek from the end.
        cursor.Index = cursor.Body.Instructions.Count - 1;

        // Don't initialize RoomRealizer. Room realization logic must be redone.
        // - if (!world.singleRoomWorld)
        // + if (!true)
        cursor.GotoPrev(MoveType.After, i => i.MatchLdfld<World>("singleRoomWorld"));
        cursor.Emit(OpCodes.Pop);
        cursor.Emit(OpCodes.Ldc_I4_1);

        // Remove check that throws an exception if no creatures are found for followAbstractCreature.
        // - if (!ModManager.MSC && this.cameras[0].followAbstractCreature == null)
        // + if (!ModManager.MSC && 1 == null)
        cursor.GotoPrev(MoveType.After, i => i.MatchLdfld<RoomCamera>("followAbstractCreature"));
        cursor.Emit(OpCodes.Pop);
        cursor.Emit(OpCodes.Ldc_I4_1);
    }
}
