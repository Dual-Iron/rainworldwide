using MonoMod.RuntimeDetour;
using BepInEx;
using System.Security.Permissions;
using Server.Hooks;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace Server;

[BepInPlugin("com.github.dual-iron.rain-worldwide-server", "Rain Worldwide Server", "0.1.0")]
sealed class ServerPlugin : BaseUnityPlugin
{
    public void OnEnable()
    {
        // Prevent accidentally enabling MSC or Jolly
        if (ModManager.MSC || ModManager.CoopAvailable) {
            LogError("Server incompatible with [More Slugcats] and [Jolly Co-op]. Server closing.");
            Application.Quit();
            return;
        }

        On.RainWorld.Start += RainWorld_Start;
        On.RainWorld.Update += RainWorld_Update;
        On.RainWorldSteamManager.ctor_ProcessManager += IgnoreSteam;

        // Ignore all controller and keyboard input for server
        new Hook(typeof(Rewired.Player).GetMethod("GetButton", [typeof(int)]), GetInput);

        // No need to hook Application.PersistentDataPath, that is included in the modified Assembly-CSharp.dll file.

        new SessionHooks().Hook();
        new ObjectHooks().Hook();

        // Init server netcode!
        _ = ServerNet.State;
    }

    public void OnApplicationQuit()
    {
        Log("Graceful exit");
        ServerNet.State.Stop();
    }

    private static void RainWorld_Start(On.RainWorld.orig_Start orig, RainWorld self)
    {
        try {
            orig(self);
        }
        catch (Exception e) {
            LogError($"Error propagated to RainWorld.Start()\n{e.ToDebugString()}");

            Application.Quit();
        }
    }

    private static void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
    {
        // Don't play sounds from the console.
        AudioListener.pause = true;

        try {
            orig(self);
        }
        catch (Exception e) {
            LogError($"Error propagated to RainWorld.Update()\n{e.ToDebugString()}");
        }
    }

    private static void IgnoreSteam(On.RainWorldSteamManager.orig_ctor_ProcessManager orig, RainWorldSteamManager self, ProcessManager manager)
    {
        self.ID = ProcessManager.ProcessID.RainWorldSteamManager;
        self.manager = manager;
        self.shutdown = true;
    }

    private static bool GetInput(Func<Rewired.Player, int, bool> orig, Rewired.Player self, int actionId)
    {
        return false;
    }
}
