using HarmonyLib;
using UnityEngine.Networking;

[HarmonyPatch]
internal static class PlayerHealthPatches
{
    [HarmonyPatch(typeof(NetControl), "Cmd_FixIt")]
    [HarmonyPrefix]
    private static bool Repair(NetControl __instance)
    { return !NetworkServer.active || __instance.control is CarControl; }

    [HarmonyPatch(typeof(Control), "_ActualSetLive")]
    [HarmonyPrefix]
    private static void Health(Control __instance, ref float value)
    {
        if (NetworkServer.active && __instance is PlayerControl) value = LegacyPlayerHealth.Sanitize(value, __instance.live);
    }

    [HarmonyPatch(typeof(Control), "SetLive")]
    [HarmonyPrefix]
    private static void HealthBeforeRpc(Control __instance, ref float value) { Health(__instance, ref value); }

    [HarmonyPatch(typeof(NetSyncPlayer), "Cmd_Shot")]
    [HarmonyPrefix]
    private static bool Shot(NetSyncPlayer __instance)
    { return !NetworkServer.active || !__instance.netControl || !(__instance.netControl.control is PlayerControl player) || player.isAlive; }

}
