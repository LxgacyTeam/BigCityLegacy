using System;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
internal static class ServerConfigPatches
{
    [HarmonyPatch(typeof(ArgParcer), "GetArgValue")]
    [HarmonyPrefix]
    private static bool Argument(string argName, ref ArgParcer.ArgValue __result)
    {
        if (string.IsNullOrEmpty(argName) || !argName.StartsWith("-")) return true;
        string value = LegacyCommandLine.GetArgValue(argName);
        bool exists = value != null || LegacyCommandLine.HasArg(argName);
        __result = new ArgParcer.ArgValue { isExists = exists, strValue = value };
        int number;
        if (int.TryParse(value, out number)) __result.value = number;
        return false;
    }
    [HarmonyPatch(typeof(NetManagerTools), "isCommandLineArgHaveServerStr")]
    [HarmonyPostfix]
    private static void ServerMode(ref bool __result) { __result = __result || LegacyCommandLine.HasServerArg(); }
    [HarmonyPatch(typeof(NetManagerTools), "isCommandLineArgBatchmode")]
    [HarmonyPostfix]
    private static void BatchMode(ref bool __result) { __result = __result || LegacyCommandLine.HasBatchMode(); }
    [HarmonyPatch(typeof(NetManagerTools), "UpdateCommandLines")]
    [HarmonyPostfix]
    private static void NetworkOptions()
    {
        if (!LegacyCommandLine.HasServerArg()) return;
        int port, max;
        if (int.TryParse(LegacyCommandLine.GetArgValue("-port"), out port) && port >= 1 && port <= 65535) NetManager.usePort = port;
        string language = LegacyCommandLine.GetArgValue("-lang");
        if (!string.IsNullOrEmpty(language)) NetManager.useLang = language;
        if (int.TryParse(LegacyCommandLine.GetArgValue("-maxConn"), out max)) NetManager.maxPlayersAllowed = Mathf.Clamp(max, 1, 100);
    }
}
