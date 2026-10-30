using HarmonyLib;
using UnityEngine.Networking;

[HarmonyPatch]
internal static class PluginAdmissionPatches
{
    [HarmonyPatch(typeof(hCursor), "NeedHideMouse")]
    [HarmonyPostfix]
    private static void Cursor(ref bool __result) { if (LegacyAlertUI.ModalVisible) __result = false; }
    [HarmonyPatch(typeof(NetManager), "OnStartServer")]
    [HarmonyPostfix]
    private static void ServerStart() { LegacyPluginNetwork.StartServer(); }
    [HarmonyPatch(typeof(NetManager), "OnStopServer")]
    [HarmonyPostfix]
    private static void ServerStop() { LegacyPluginNetwork.StopServer(); }
    [HarmonyPatch(typeof(NetManager), "OnServerDisconnect")]
    [HarmonyPostfix]
    private static void Disconnect(NetworkConnection conn) { LegacyPluginNetwork.Forget(conn); }
    [HarmonyPatch(typeof(NetManager), "OnStartClient")]
    [HarmonyPostfix]
    private static void ClientStart(NetworkClient client) { LegacyPluginNetwork.StartClient(client); }
    [HarmonyPatch(typeof(NetManager), "OnStopClient")]
    [HarmonyPostfix]
    private static void ClientStop() { LegacyWelcomeClient.Clear(); }
    [HarmonyPatch(typeof(NetConnect), "CallCmd_SetPrivateID")]
    [HarmonyPrefix]
    private static bool Advertise(ref string setPlayerXml) { return LegacyPluginNetwork.Attach(ref setPlayerXml); }
}
