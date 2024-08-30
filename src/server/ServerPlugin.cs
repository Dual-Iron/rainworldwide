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
        NewMethod();
    }

    private void NewMethod()
    {
        try {
            LogDebug("Env vars: " + Environment.GetCommandLineArgs().ToDebugString());

            //On.RainWorld.Start += RainWorld_Start;
            //On.RainWorld.Update += RainWorld_Update;
            //On.RainWorldSteamManager.ctor += IgnoreSteam;

            // Ignore all controller and keyboard input for server
            new Hook(typeof(Rewired.Player).GetMethod("GetButton", [typeof(int)]), GetInput);

            // No need to hook PersistentDataPath, that is included in the modified Assembly-CSharp.dll file.
        }
        catch (Exception e) {
            LogError(e);
        }
    }

    private void RainWorld_Start(On.RainWorld.orig_Start orig, RainWorld self)
    {
        try {
            orig(self);
        }
        catch (Exception e) {
            LogFatal(e);

            Application.Quit();
        }
    }

    private void RainWorld_Update(On.RainWorld.orig_Update orig, RainWorld self)
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

    private void IgnoreSteam(On.RainWorldSteamManager.orig_ctor orig, RainWorldSteamManager self, ProcessManager manager)
    {
        self.ID = ProcessManager.ProcessID.RainWorldSteamManager;
        self.manager = manager;
        self.shutdown = true;
    }

    private static string SavePath()
    {
        // Save just outside game folder in "save"
        // Servers are run inside a "server/game" folder, so "server/save" is the save path.
        return Path.Combine(Application.streamingAssetsPath, "../../../save");
    }

    private bool GetInput(Func<Rewired.Player, int, bool> orig, Rewired.Player self, int actionId)
    {
        return false;
    }
}
