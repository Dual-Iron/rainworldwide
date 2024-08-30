using MonoMod.RuntimeDetour;
using System.IO;
using BepInEx;
using System.Security.Permissions;

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
        // Prevent accidentally enabling MSC
        if (ModManager.MSC) {
            LogFatal("DISABLE MORE SLUGCATS EXPANSION. Server closing.");
            Application.Quit();
            return;
        }

        LogDebug("Hello Server!");

        LogDebug("Env vars: " + Environment.GetCommandLineArgs().ToDebugString());

        On.RainWorld.Start += RainWorld_Start;
        On.RainWorld.Update += RainWorld_Update;
        On.RainWorldSteamManager.ctor_ProcessManager += IgnoreSteam;

        // Ignore all controller and keyboard input for server
        new Hook(typeof(Rewired.Player).GetMethod("GetButton", [typeof(int)]), GetInput);

        // No need to hook Application.PersistentDataPath, that is included in the modified Assembly-CSharp.dll file.
    }

    private static void RainWorld_Start(On.RainWorld.orig_Start orig, RainWorld self)
    {
        try {
            orig(self);
        }
        catch (Exception e) {
            LogFatal(e);

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
            LogError($"Exception in update logic. {e}");
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
