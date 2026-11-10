using System;
using System.Xml;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

[HarmonyPatch]
internal static class CsWorldPatches
{
    [HarmonyPatch(typeof(NetInputControl), "DeliveryCar")]
    [HarmonyPrefix]
    private static bool RestrictCarDelivery(NetInputControl __instance)
    {
        if (!NetManager.isServer) return true;
        Net_BaseEvent curEvent = __instance.curEvent;
        if (curEvent is Net_CS && curEvent.state != Net_BaseEvent.CurState.Lobbi && curEvent.state != Net_BaseEvent.CurState.Complite)
        {
            Debug.LogWarning(
                "[Server] Car spawn rejected for '" + LegacyServerAdminService.GetNicknameForInput(__instance) +
                "': active CS/CS_Sur round, state=" + curEvent.state.ToString()
            );
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(Net_CS), "GetRandomMap")]
    [HarmonyPostfix]
    private static void Net_CS_GetRandomMap_Postfix(Net_CS __instance, ref Transform __result)
    {
        LegacyServerDropCleanup.RememberSelectedCsMap(__instance, __result);
    }

    [HarmonyPatch(typeof(Net_CS), "StateChanget")]
    [HarmonyPrefix]
    private static void Net_CS_StateChanget_Prefix(Net_CS __instance, Net_BaseEvent.CurState was)
    {
        LegacyServerDropCleanup.HandleCsStateChanged(__instance, was);
    }

}
