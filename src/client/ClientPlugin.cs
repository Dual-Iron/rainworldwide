using BepInEx;
using System.Net.Sockets;
using System.Net;
using System.Security.Permissions;
using System.Net.NetworkInformation;
using Client.Hooks;

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
        string ip = GetLocalIPAddress();
        Log(ip);

        new MenuHooks().Hook();
        new SessionHooks().Hook();
        new ObjectHooks().Hook();
    }
}
