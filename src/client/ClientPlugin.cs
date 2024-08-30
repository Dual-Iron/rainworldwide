using BepInEx;
using System.Security.Permissions;

// Allows access to private members
#pragma warning disable CS0618
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618

namespace Client;

[BepInPlugin("com.github.dual-iron.rain-worldwide-client", "Rain Worldwide", "0.1.0")]
sealed class ClientPlugin : BaseUnityPlugin
{
    public void OnEnable()
    {
        LogDebug("Hello Client!");

        new MenuChanges().Hook();
    }
}
