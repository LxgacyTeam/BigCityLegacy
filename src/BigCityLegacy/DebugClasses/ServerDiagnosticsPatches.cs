using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine.Networking;

// Installed only for -serverDiagnostics. No argument arrays, reflection or packet
// logging are injected into UNet's hot path on ordinary server runs.
[HarmonyPatch]
internal static class ServerDiagnosticsPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NetworkConnection), "TransportReceive", new[] { typeof(byte[]), typeof(int), typeof(int) });
        yield return AccessTools.Method(typeof(NetworkConnection), "InvokeHandler", new[] { typeof(NetworkMessage) });
        yield return AccessTools.Method(typeof(NetworkConnection), "InvokeHandler", new[] { typeof(short), typeof(NetworkReader), typeof(int) });
        yield return AccessTools.Method(typeof(NetConnect), "Cmd_SetPrivateID");
        yield return AccessTools.Method(typeof(NetConnect), "InitInputControl");
        yield return AccessTools.Method(typeof(NetInputControl), "DeliveryCar");
        yield return AccessTools.Method(typeof(NetControl), "Cmd_XmlData");
        yield return AccessTools.Method(typeof(NetControl), "SetXmlDataServer");
        yield return AccessTools.Method(typeof(NetControl), "LoadFromXmlIfExists");
        yield return AccessTools.Method(typeof(CarSaveLoad), "LoadXml", new[] { typeof(string) });
        yield return AccessTools.Method(typeof(PlayerSaveLoad), "LoadXml", new[] { typeof(string), typeof(bool) });
        yield return AccessTools.Method(typeof(NetControl), "SetParentControl", new[] { typeof(Control.SetParentData) });
        yield return AccessTools.Method(typeof(Control), "SetParentControlV", new[] { typeof(Control.SetParentData) });
    }
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void Prefix(MethodBase __originalMethod, object __instance, object[] __args, out LegacyServerDiagnostics.Operation __state)
    {
        __state = null;
        try { __state = LegacyServerDiagnostics.Enter(__originalMethod, __instance, __args); }
        catch (Exception ex)
        {
            LegacyServerDiagnostics.ResetContext();
            LegacyServerLog.Record(LegacyServerLogLevel.Warning, "Diagnostics", "Trace unavailable: " + ex.GetType().Name);
        }
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(Exception __exception, LegacyServerDiagnostics.Operation __state)
    {
        try { LegacyServerDiagnostics.Exit(__state, __exception); }
        catch { LegacyServerDiagnostics.ResetContext(); }
        return __exception; // Observe exceptions, never swallow or alter game/network data.
    }
}
