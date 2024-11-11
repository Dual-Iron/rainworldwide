global using System;
global using System.Linq;
global using System.Collections.Generic;
global using UnityEngine;
global using static Common.Utils;

using System.Runtime.CompilerServices;
using System.Collections;
using System.Text;
using RWCustom;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net;
using LiteNetLib;

namespace Common;

sealed class Field<K, V>(Func<K, V> lazyConstructor) where K : class where V : class
{
    private readonly ConditionalWeakTable<K, V> cwt = new();

    private V InitializeField(K key) => lazyConstructor(key);

    public V this[K obj] => cwt.GetValue(obj, InitializeField);
}

sealed class ByID<V>(Func<int, V> lazyConstructor) where V : class
{
    private readonly Dictionary<int, V> dict = [];

    public V this[int id] => dict.TryGetValue(id, out var v) ? v : dict[id] = lazyConstructor(id);

    public override string ToString() => Utils.ToDebugString(dict);
}

static class Utils
{
    #region LOGGING
    private static readonly BepInEx.Logging.ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource("RWW");

    public static void LogError(object msg)
    {
        logger.LogError($"{DateTime.UtcNow:HH:mm:ss.fff} | {(msg is string ? msg : ToDebugString(msg))}");
    }
    public static T Log<T>(T value, [CallerArgumentExpression(nameof(value))] string expression = "")
    {
        // Log string literals literally
        if (value is string s && (expression.StartsWith("\"") || expression.StartsWith("$"))) {
            logger.LogDebug($"{DateTime.UtcNow:HH:mm:ss.fff} | {s}");
            return value;
        }
        const int maxExprLen = 20;
        if (expression.Length > maxExprLen) {
            expression = expression[..(maxExprLen - 3)].Trim(',', ' ', '.') + "...";
        }
        logger.LogDebug($"{DateTime.UtcNow:HH:mm:ss.fff} | {expression} = {ToDebugString(value)}");
        return value;
    }

    private sealed class AssumptionInvalidException(string message) : Exception(message) { }
    public static T AssumeEqual<T>(T one, T two,
        [CallerArgumentExpression(nameof(one))] string oneExpr = "",
        [CallerArgumentExpression(nameof(two))] string twoExpr = "")
    {
        if (ReferenceEquals(one, two) || one != null && two != null && one.Equals(two)) {
            return one;
        }
        throw new AssumptionInvalidException("Assumption failed for equality between:" +
            $"\n{oneExpr} = {ToDebugString(one)}\n{twoExpr} = {ToDebugString(two)}");
    }

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
            AbstractPhysicalObject p => $"{p.type}[ID.{p.ID.number} @ {p.pos.ToDebugString()}]",
            WorldCoordinate wc => DebugWc(wc),
            EntityID e => $"ID.{e.number}",
            IDictionary dict => DebugDict(dict),
            IEnumerable objs => DebugEnumerable(objs),
            Exception e => DebugException(e),
            StringBuilder sb => $"sb\"{sb}\"",
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
        return $"{c.creatureTemplate.type}[ID.{c.ID.number}{extra} @ {c.pos.ToDebugString()}]";
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
    static string DebugEnumerable(IEnumerable objects)
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
    static string DebugException(Exception e)
    {
        StringBuilder sb = new(e.GetType().Name + " | " + e.Message + "\n");
        string[] lines = e.ToString().Split('\n');
        for (int i = 1; i < lines.Length; i++) {
            string l = lines[i];
            if (!l.StartsWith("  at ") || l.Contains(".Trampoline") || l.Contains("ThrowHelper")) {
                continue;
            }
            l = l.Replace("(wrapper dynamic-method) MonoMod.Utils.DynamicMethodDefinition.", "");
            if (l.Contains(" [0x")) {
                l = l[..l.IndexOf(" [0x")];
            }
            sb.AppendLine(l);
        }
        return sb.ToString();
    }
    #endregion

    private static RainWorld rw;
    private static RainWorld Rw => rw ??= UnityEngine.Object.FindObjectOfType<RainWorld>();
    public static RainWorldGame Game() => Rw?.processManager?.currentMainLoop as RainWorldGame;

    public static int ID(this PhysicalObject o) => o.abstractPhysicalObject.ID.number;
    public static int ID(this AbstractPhysicalObject o) => o.ID.number;

    public static bool DirExistsAt(params string[] path)
    {
        return Directory.Exists(BepInEx.Utility.CombinePaths(path));
    }

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
