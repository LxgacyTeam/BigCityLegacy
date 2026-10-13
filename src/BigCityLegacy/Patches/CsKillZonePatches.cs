using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
internal static class CsKillZonePatches
{
    [HarmonyPatch(typeof(NetSpawn), "Spawn")]
    [HarmonyPrefix]
    private static void NetSpawn_Spawn_Prefix(NetSpawn __instance, ref Vector3 p, bool isNeedReset)
    {
        LegacyCsKillZone.PrepareSpawn(__instance, ref p, isNeedReset);
    }

    [HarmonyPatch(typeof(Net_CS), "UpdateServer")]
    [HarmonyPostfix]
    private static void NetCS_UpdateServer_Postfix(Net_CS __instance)
    {
        LegacyCsKillZone.Check(__instance, nTime.GameThisFrame);
    }

    [HarmonyPatch(typeof(Net_BaseEvent), "SetState")]
    [HarmonyPostfix]
    private static void NetBaseEvent_SetState_Postfix(Net_BaseEvent __instance)
    {
        Net_CS cs = __instance as Net_CS;
        if (cs != null && cs.state != Net_BaseEvent.CurState.Race)
        {
            LegacyCsKillZone.ClearEvent(cs);
        }
    }

    [HarmonyPatch(typeof(Net_BaseEvent), "OnDestroy")]
    [HarmonyPrefix]
    private static void NetBaseEvent_OnDestroy_Prefix(Net_BaseEvent __instance)
    {
        LegacyCsKillZone.ClearEvent(__instance as Net_CS);
    }

    [HarmonyPatch(typeof(NetManager), "RunServer")]
    [HarmonyPostfix]
    private static void NetManager_RunServer_Postfix()
    {
        LegacyCsKillZone.Reset();
    }

    [HarmonyPatch(typeof(NetManager), "NowStoped")]
    [HarmonyPostfix]
    private static void NetManager_NowStoped_Postfix()
    {
        LegacyCsKillZone.Reset();
    }
}
