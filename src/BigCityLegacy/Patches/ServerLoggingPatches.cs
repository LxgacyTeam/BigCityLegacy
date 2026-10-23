using System;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
internal static class ServerLoggingPatches
{
    [HarmonyPatch(typeof(NetManagerTools), "OnEnable"), HarmonyPrefix]
    private static bool NativeEnable(NetManagerTools __instance)
    {
        if (LegacyServerLog.NativeMLogEnabled) return true;
        NativeDisable(__instance);
        return false;
    }
    [HarmonyPatch(typeof(NetManagerTools), "OnDisable"), HarmonyPrefix]
    private static bool NativeDisable(NetManagerTools __instance)
    {
        Application.logMessageReceived -= __instance.HandleLog;
        var writer = __instance.logWriter;
        __instance.logWriter = null;
        try { writer?.Dispose(); }
        catch (Exception ex) { LegacyServerLog.Record(LegacyServerLogLevel.Error, "m_log", ex.Message); }
        return false;
    }
    [HarmonyPatch(typeof(NetManagerTools), "HandleLog"), HarmonyPrefix]
    private static void NativeCount(NetManagerTools __instance)
    {
        if (!LegacyServerLog.IsServer) return;
        // These are lifetime counters, not the size of an actual retained log.
        if (__instance.logLinesCount >= 10000) __instance.logLinesCount = 0;
        if (NetManagerTools.logValidateLinesCount >= 10000) NetManagerTools.logValidateLinesCount = 0;
    }
    [HarmonyPatch(typeof(NetManagerTools), "Update"), HarmonyPrefix]
    private static bool NativeUpdate(NetManagerTools __instance)
    {
        if (!LegacyServerLog.IsServer) return true;
        __instance.logLinesCount = 0;
        return false; // Native Update consists solely of FORCE_EXIT at 1M records.
    }
    [HarmonyPatch(typeof(Reporter), "CaptureLogThread"), HarmonyPrefix]
    private static bool ReporterCaptureThread() { return !LegacyServerLog.IsServer; }
    [HarmonyPatch(typeof(Reporter), "CaptureLog"), HarmonyPrefix]
    private static bool ReporterCapture() { return !LegacyServerLog.IsServer; }
    [HarmonyPatch(typeof(Reporter), "AddLog"), HarmonyPrefix]
    private static bool ReporterAdd() { return !LegacyServerLog.IsServer; }

    [HarmonyPatch(typeof(Reporter), "Update"), HarmonyPrefix]
    private static bool ReporterUpdate(Reporter __instance)
    {
        if (!LegacyServerLog.IsServer) return true;
        // Headless servers use the persistent sink instead of a second GUI history.
        // Clear records captured before patch installation, without Reporter.clear's GC.Collect.
        lock (__instance.threadedLogs) __instance.threadedLogs.Clear();
        if (__instance.logs.Count != 0 || __instance.samples.Count != 0)
        {
            __instance.logs.Clear(); __instance.collapsedLogs.Clear(); __instance.currentLog.Clear();
            __instance.logsDic.Clear(); __instance.samples.Clear(); __instance.selectedLog = null;
            __instance.logsMemUsage = __instance.graphMemUsage = 0;
            __instance.numOfLogs = __instance.numOfLogsWarning = __instance.numOfLogsError = 0;
            __instance.numOfCollapsedLogs = __instance.numOfCollapsedLogsWarning = __instance.numOfCollapsedLogsError = 0;
        }
        return false;
    }
}
