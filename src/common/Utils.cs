global using System;
global using System.Linq;
global using System.Collections.Generic;
global using UnityEngine;
global using static Rww.Utils;
using System.Runtime.CompilerServices;
using System.Collections;
using System.Text;
using RWCustom;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;

namespace Rww;

static class Utils
{
    #region LOGGING
    private static readonly BepInEx.Logging.ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource("RWW");

    public static void Log([CallerLineNumber] int line = 0, [CallerFilePath] string file = "", [CallerMemberName] string mem = "")
    {
        logger.LogDebug($"{DateTime.UtcNow:HH:mm:ss.fff} | {Path.GetFileName(file)}:{line} in {mem}()");
    }
    public static void LogMessage(string msg = "")
    {
        logger.LogDebug($"{DateTime.UtcNow:HH:mm:ss.fff} | {msg}");
    }
    public static void LogError(object msg)
    {
        logger.LogError($"{DateTime.UtcNow:HH:mm:ss.fff} | {(msg is string ? msg : ToDebugString(msg))}");
    }
    public static T LogValue<T>(T value, [CallerArgumentExpression(nameof(value))] string expression = "")
    {
        if (expression.Length > 15) {
            expression = expression.Substring(0, 12).Trim(',', ' ', '.') + "...";
        }
        logger.LogDebug($"{DateTime.UtcNow:HH:mm:ss.fff} | {expression} = {ToDebugString(value)}");
        return value;
    }

    private static readonly RainWorld rw = UnityEngine.Object.FindObjectOfType<RainWorld>();
    public static RainWorldGame Game() => rw.processManager.currentMainLoop as RainWorldGame;

    public static string ToDebugString(this object obj)
    {
        return obj switch
        {
            null => "null",
            char c => $"'{c}'",
            string s => '"' + s + '"',
            sbyte or byte or short or ushort or int or uint or long or ulong => obj.ToString(),
            float or double or decimal => string.Format("F2", obj),
            Vector2 v => $"({v.x:F2}, {v.y:F2})",
            IntVector2 v => $"({v.x}, {v.y})",
            PhysicalObject p => $"R#{p.abstractPhysicalObject?.ToDebugString() ?? p.GetType().ToString()}",
            AbstractCreature c => DebugC(c),
            AbstractPhysicalObject p => $"{p.type}[ID.{p.ID.number}, {p.pos.ToDebugString()}]",
            WorldCoordinate wc => DebugWc(wc),
            EntityID e => $"ID.{e.number}",
            IDictionary dict => DebugDict(dict),
            IEnumerable objs => DebugEnum(objs),
            _ => obj.ToString(),
        };
    }

    static string DebugC(AbstractCreature c)
    {
        string extra = "";
        if (c.state is HealthState h && c.state.alive) {
            extra = ", H=" + h.health.ToDebugString();
        } else if (c.state.dead) {
            extra = ", Dead";
        }
        return $"{c.creatureTemplate.type}[ID.{c.ID.number}{extra}, {c.pos.ToDebugString()}]";
    }
    static string DebugWc(WorldCoordinate wc)
    {
        if (wc.NodeDefined && Game()?.world?.GetAbstractRoom(wc) is AbstractRoom r) {
            return $"({r.name}: {r.GetNode(wc).type} {wc.abstractNode})";
        } else if (wc.NodeDefined) {
            return $"({wc.ResolveRoomName() ?? $"Room {wc.room}"}: Node {wc.abstractNode})";
        }
        return $"({wc.ResolveRoomName() ?? $"Room {wc.room}"}: {wc.x}, {wc.y})";
    }
    static string DebugDict(IDictionary dict)
    {
        StringBuilder sb = new("{ ");
        bool first = true;
        foreach (object key in dict.Keys) {
            if (first) {
                first = false;
            } else {
                sb.Append(", ");
            }
            sb.Append(key.ToDebugString());
            sb.Append(": ");
            sb.Append(dict[key]?.ToDebugString() ?? "null");
        }
        sb.Append(" }");
        return sb.ToString();
    }
    static string DebugEnum(IEnumerable objects)
    {
        StringBuilder sb = new("[");
        bool first = true;
        foreach (object obj in objects) {
            if (first) {
                first = false;
            } else {
                sb.Append(", ");
            }
            sb.Append(obj?.ToDebugString() ?? "null");
        }
        sb.Append("]");
        return sb.ToString();
    }
    #endregion

    public static string GetLocalIPAddress()
    {
        if (NetworkInterface.GetIsNetworkAvailable()) {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList) {
                if (ip.AddressFamily == AddressFamily.InterNetwork) {
                    return ip.ToString();
                }
            }
        }
        LogError("No internet connection");
        return null;
    }

}
