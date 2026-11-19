using System;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

[HarmonyPatch]
internal static class CarXmlNetworkPatches
{
    [HarmonyPatch(typeof(NetInputControl), "DeliveryCar")]
    [HarmonyPrefix]
    private static bool Spawn(string xmlData) { return !NetworkServer.active || LegacyServerVinylLimit.Accept(xmlData); }

    [HarmonyPatch(typeof(NetControl), "SetXmlDataServer")]
    [HarmonyPrefix]
    private static bool Paint(NetControl __instance, string setXmlData)
    { return !NetworkServer.active || !(__instance.control is CarControl) || LegacyServerVinylLimit.Accept(setXmlData); }

    [HarmonyPatch(typeof(NetManager), "OnStartServer")]
    [HarmonyPostfix]
    private static void ServerStart() { LegacyCarXmlNetwork.StartServer(); }

    [HarmonyPatch(typeof(NetManager), "OnStopServer")]
    [HarmonyPostfix]
    private static void ServerStop() { LegacyCarXmlNetwork.StopServer(); }

    [HarmonyPatch(typeof(NetManager), "OnStartClient")]
    [HarmonyPostfix]
    private static void ClientStart(NetworkClient client) { LegacyCarXmlNetwork.StartClient(client); }

    [HarmonyPatch(typeof(NetManager), "OnStopClient")]
    [HarmonyPostfix]
    private static void ClientStop() { LegacyCarXmlNetwork.StopClient(); }

    [HarmonyPatch(typeof(NetConnect), "InitInputControl")]
    [HarmonyPostfix]
    private static void Advertise(NetConnect __instance) { LegacyCarXmlNetwork.Advertise(__instance); }

    [HarmonyPatch(typeof(NetSyncPlayer), "Target_EventMessageHello")]
    [HarmonyPostfix]
    private static void Capability(NetSyncPlayer __instance, string command) { LegacyCarXmlNetwork.AcceptCapability(__instance, command); }

    [HarmonyPatch(typeof(NetSyncPlayer), "Cmd_EventMessageHello")]
    [HarmonyPrefix]
    private static bool PreventForgedCapability(string command) { return command != LegacyCarXmlNetwork.Capability; }

    [HarmonyPatch(typeof(NetInputControl), "CallCmd_DeliveryCar")]
    [HarmonyPrefix]
    private static bool UploadCar(NetInputControl __instance, string fileName, string xmlData, bool isTestDrive)
    { return !LegacyCarXmlNetwork.Upload(__instance, null, fileName, xmlData, isTestDrive); }

    [HarmonyPatch(typeof(NetControl), "CallCmd_XmlData")]
    [HarmonyPrefix]
    private static bool UploadPaint(NetControl __instance, string setXmlData)
    {
        InputControl user = InputControl.GetFirstUser();
        return !LegacyCarXmlNetwork.Upload(user ? user.netInput : null, __instance, "", setXmlData, false);
    }

    [HarmonyPatch(typeof(NetControl), "NetworkxmlData", MethodType.Setter)]
    [HarmonyPrefix]
    private static bool Wire(NetControl __instance, ref string value)
    {
        try { LegacyCarXmlNetwork.SetWire(__instance, ref value); return true; }
        catch (Exception ex) { Debug.LogWarning("[BCL vinyl] Car XML rejected: " + ex.Message); return false; }
    }

    [HarmonyPatch(typeof(NetControl), "LoadFromXmlIfExists")]
    [HarmonyPrefix]
    private static void Resolve(NetControl __instance, out string __state)
    {
        __state = null;
        string full = LegacyCarXmlNetwork.Resolve(__instance);
        if (full != null) { __state = __instance.xmlData; __instance.xmlData = full; }
    }

    [HarmonyPatch(typeof(NetControl), "LoadFromXmlIfExists")]
    [HarmonyFinalizer]
    private static Exception RestoreWire(NetControl __instance, string __state, Exception __exception)
    {
        if (__state != null) __instance.xmlData = __state;
        return __exception;
    }

    [HarmonyPatch(typeof(DeliveryCarUI), "StartDelivery")]
    [HarmonyPrefix]
    private static void DeliveryTime(ref float seconds)
    {
        // Zero-duration delivery is legitimate with unrestricted spawning. Its
        // first frame otherwise divides 0 by 0, displaying Int32.MinValue %.
        if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0.001f) seconds = 0.001f;
    }

    [HarmonyPatch(typeof(DeliveryCarUI), "Update")]
    [HarmonyPrefix]
    private static void DeliveryProgress(DeliveryCarUI __instance)
    {
        float value = __instance.cur_alpha;
        __instance.cur_alpha = float.IsNaN(value) || float.IsInfinity(value) ? 0f : Mathf.Clamp01(value);
    }
}
