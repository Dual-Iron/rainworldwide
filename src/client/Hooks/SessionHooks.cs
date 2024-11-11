using Common;
using HUD;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RWCustom;

namespace Client.Hooks;

sealed class SessionHooks
{
    public void Hook()
    {
        // Fix SlugcatStats access
        new Hook(typeof(Player).GetMethod("get_slugcatStats"), getSlugcatStats);
        new Hook(typeof(Player).GetMethod("get_Malnourished"), getMalnourished);

        // Fix how rain appears for clients
        new Hook(typeof(RainCycle).GetMethod("get_RainApproaching"), getRainApproaching);

        // Prevent world creatures from spawning as client
        new Hook(typeof(RainWorldGame).GetMethod("get_setupValues"), getSetupValues);

        // Prevent spawning various placed physical objects, and random objects like spears and rocks
        On.Room.ctor += Room_ctor;

        // Fix SlugcatWorld
        On.RoomSettings.ctor += RoomSettings_ctor;

        // Fix disconnections
        On.RainWorldGame.Update += ExitOnDisconnect;
        On.RainWorldGame.ExitToMenu += DisconnectOnExit;

        // Prevent errors and abnormal behavior with custom session type
        On.OverWorld.ctor += CreateClientSession;
        On.OverWorld.LoadFirstWorld += OverWorld_LoadFirstWorld;
        On.HUD.HUD.InitSinglePlayerHud += HUD_InitSinglePlayerHud;
        On.StoryGameSession.TimeTick += delegate { }; // Nullrefs

        IL.RainWorldGame.Update += FixPauseAndCrash; // Custom pause menu logic + update ClientRoomLogic
        IL.RainWorldGame.GrafUpdate += FixPause; // Custom pause menu logic

        // Decentralize RoomCamera.followAbstractCreature
        IL.RainWorldGame.ctor += RainWorldGame_ctor;
    }

    private readonly Func<Func<Player, SlugcatStats>, Player, SlugcatStats> getSlugcatStats = (orig, self) => {
        if (Game().session is ClientSession session) {
            return session.GetStatsFor(self.ID());
        }
        return orig(self);
    };

    private readonly Func<Func<Player, bool>, Player, bool> getMalnourished = (orig, self) => self.slugcatStats.malnourished;

    private readonly Func<Func<RainCycle, float>, RainCycle, float> getRainApproaching = (orig, self) => {
        if (self.world.game.session is ClientSession) {
            return Mathf.InverseLerp(0f, 2400f, self.TimeUntilRain);
        }
        return orig(self);
    };

    private readonly Func<Func<RainWorldGame, RainWorldGame.SetupValues>, RainWorldGame, RainWorldGame.SetupValues> getSetupValues = (orig, game) => {
        if (game.session is ClientSession) {
            return orig(game) with { worldCreaturesSpawn = false };
        }
        return orig(game);
    };

    SlugcatStats.Name storyCharOverride = null;
    private void Room_ctor(On.Room.orig_ctor orig, Room self, RainWorldGame game, World world, AbstractRoom abstractRoom)
    {
        if (game?.session is ClientSession session) {
            storyCharOverride = session.SlugcatWorld;
            orig(self, game, world, abstractRoom);

            // Don't spawn physical objects!!
            self.abstractRoom.firstTimeRealized = false;
        } else {
            storyCharOverride = null;
            orig(self, game, world, abstractRoom);
        }
    }
    private void RoomSettings_ctor(On.RoomSettings.orig_ctor orig, RoomSettings self, string name, Region region, bool template, bool firstTemplate, SlugcatStats.Name playerChar)
    {
        if (storyCharOverride != null) {
            playerChar = storyCharOverride;
        }
        orig(self, name, region, template, firstTemplate, playerChar);
    }

    private void ExitOnDisconnect(On.RainWorldGame.orig_Update orig, RainWorldGame self)
    {
        if (self.session is ClientSession && ClientNet.State.Progress != ConnectionProgress.Connected && self.manager.upcomingProcess == null) {
            self.ExitToMenu();
        }
        orig(self);
    }

    private void DisconnectOnExit(On.RainWorldGame.orig_ExitToMenu orig, RainWorldGame self)
    {
        if (self.session is ClientSession) {
            ClientNet.State.Disconnect();
        }
        orig(self);
    }

    private void CreateClientSession(On.OverWorld.orig_ctor orig, OverWorld self, RainWorldGame game)
    {
        if (ClientNet.State.IntroPacket is RealizePlayer r) {
            game.session = new ClientSession(r, game);
            game.startingRoom = r.StartingRoom;
        }

        orig(self, game);
    }

    private void OverWorld_LoadFirstWorld(On.OverWorld.orig_LoadFirstWorld orig, OverWorld self)
    {
        if (self.game?.session is not ClientSession || !ClientNet.State.IntroPacket.HasValue) {
            orig(self);
            return;
        }

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

        self.LoadWorld(startingRegion, new(ClientNet.State.IntroPacket.Value.SlugcatWorld), false);
        self.FIRSTROOM = startingRoom;
    }

    private void HUD_InitSinglePlayerHud(On.HUD.HUD.orig_InitSinglePlayerHud orig, HUD.HUD self, RoomCamera cam)
    {
        if (self.owner is Player p && Game().session is ClientSession) {
            self.AddPart(new TextPrompt(self));
            self.AddPart(new KarmaMeter(self, self.fContainers[1], new IntVector2(p.Karma, p.KarmaCap), p.KarmaIsReinforced));
            self.AddPart(new FoodMeter(self, p.slugcatStats.maxFood, p.slugcatStats.foodToHibernate));
            self.AddPart(new RainMeter(self, self.fContainers[1]));
            if (cam.room.abstractRoom.shelter) {
                self.karmaMeter.fade = 1f;
                self.rainMeter.fade = 1f;
                self.foodMeter.fade = 1f;
            }
        } else {
            orig(self, cam);
        }
    }

    private void FixPauseAndCrash(ILContext il)
    {
        ILCursor cursor = new(il);

        // Pretend pause menu is null
        cursor.GotoNext(MoveType.After, i => i.MatchLdfld<RainWorldGame>("pauseMenu"));
        cursor.EmitDelegate(ModifyPause);

        static Menu.PauseMenu ModifyPause(Menu.PauseMenu pauseMenu)
        {
            // Skip vanilla pause menu logic—update both the game and the pause menu at once
            if (pauseMenu?.game.session is ClientSession) {
                pauseMenu.Update();
                pauseMenu.game.lastPauseButton = true; // prevent opening multiple pause menus at once
                foreach (RoomCamera camera in pauseMenu.game.cameras) {
                    camera.hud?.Update();
                }
                return null;
            }
            return pauseMenu;
        }

        // Realize rooms with custom logic
        cursor.GotoNext(MoveType.Before, i => i.MatchLdfld<RainWorldGame>("roomRealizer"));
        cursor.EmitDelegate(RoomRealizerHook);

        static RainWorldGame RoomRealizerHook(RainWorldGame game)
        {
            if (game.session is ClientSession session) {
                session.UpdatePreRoom();
            }
            return game;
        }
    }

    private void FixPause(ILContext il)
    {
        ILCursor cursor = new(il);

        cursor.GotoNext(MoveType.After, i => i.MatchLdfld<RainWorldGame>("pauseMenu"));
        cursor.Emit(OpCodes.Ldarg_1); // timeStacker
        cursor.EmitDelegate(ModifyPause);

        static Menu.PauseMenu ModifyPause(Menu.PauseMenu pauseMenu, float timeStacker)
        {
            // Skip vanilla pause menu logic—render both the game and the pause menu at once
            if (pauseMenu?.game.session is ClientSession) {
                pauseMenu.GrafUpdate(timeStacker);
                foreach (RoomCamera camera in pauseMenu.game.cameras) {
                    camera.hud?.Draw(timeStacker);
                }
                return null;
            }
            return pauseMenu;
        }
    }

    private void RainWorldGame_ctor(ILContext il)
    {
        ILCursor cursor = new(il);

        cursor.Index = cursor.Body.Instructions.Count - 1;

        // Don't initialize RoomRealizer. Room realization logic must be redone.
        cursor.GotoPrev(MoveType.After, i => i.MatchLdfld<World>("singleRoomWorld"));
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate(ShouldNotInitRoomRealizer);

        static bool ShouldNotInitRoomRealizer(bool orig, RainWorldGame game)
        {
            return orig || game.session is ClientSession;
        }

        // Remove check that throws an exception if no creatures are found for followAbstractCreature
        cursor.GotoPrev(MoveType.After, i => i.MatchLdfld<RoomCamera>("followAbstractCreature"));
        cursor.Emit(OpCodes.Ldarg_0);
        cursor.EmitDelegate(IsFollowAbstractCreatureNull);

        static bool IsFollowAbstractCreatureNull(AbstractCreature orig, RainWorldGame game)
        {
            return orig != null || game.session is ClientSession;
        }
    }
}
