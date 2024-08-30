global using System;
global using System.Linq;
global using System.Collections.Generic;
global using UnityEngine;
global using static Rww.Utils;
using System.Collections;
using System.Text;

namespace Rww;

static class Utils
{
    private static readonly BepInEx.Logging.ManualLogSource logger = BepInEx.Logging.Logger.CreateLogSource("RWW");

    private static string PrependTime(object message) => DateTime.UtcNow.ToString("HH:mm:ss.fff") + " | " + (message ?? null);

    public static void LogDebug(object message) => logger.LogDebug(PrependTime(message));
    public static void LogInfo(object message) => logger.LogInfo(PrependTime(message));
    public static void LogWarning(object message) => logger.LogWarning(PrependTime(message));
    public static void LogError(object message) => logger.LogError(PrependTime(message));
    public static void LogFatal(object message) => logger.LogFatal(PrependTime(message));

    public static string ToDebugString(this object obj)
    {
        return obj switch
        {
            null => "null",
            string s => '"' + s + '"',
            IDictionary dict => ToDebugString(dict),
            IEnumerable objs => ToDebugString(objs),
            _ => obj.ToString(),
        };
    }
    static string ToDebugString(this IDictionary dict)
    {
        StringBuilder sb = new("{ ");
        bool first = true;
        foreach (object key in dict.Keys) {
            if (!first) {
                first = false;
                sb.Append(", ");
            }
            sb.Append(key.ToDebugString());
            sb.Append(": ");
            sb.Append(dict[key]?.ToDebugString() ?? "null");
        }
        sb.Append(" }");
        return sb.ToString();
    }
    static string ToDebugString(this IEnumerable objects)
    {
        StringBuilder sb = new("[");
        bool first = true;
        foreach (object obj in objects) {
            if (!first) {
                first = false;
                sb.Append(", ");
            }
            sb.Append(obj?.ToDebugString() ?? "null");
        }
        sb.Append("]");
        return sb.ToString();
    }
}
