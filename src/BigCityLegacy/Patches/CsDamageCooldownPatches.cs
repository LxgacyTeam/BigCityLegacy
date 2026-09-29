using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[HarmonyPatch]
internal static class CsDamageCooldownPatches
{
    private static readonly Dictionary<int, int> CsReconnectMoneySnapshot = new Dictionary<int, int>();

    [HarmonyPatch(typeof(NetSpawn), "Cmd_GroundUnderSpawnLoadedCanRemoveHold")]
    [HarmonyPostfix]
    private static void NetSpawn_CmdGroundUnderSpawnLoadedCanRemoveHold_Postfix(NetSpawn __instance)
    {
        if (!NetManager.isServer)
        {
            return;
        }

        LegacyCsDamageCooldown.BeginServerProtection(__instance);
    }

    [HarmonyPatch(typeof(PlayerControl), "MinusLive")]
    [HarmonyPrefix]
    private static bool PlayerControl_MinusLive_Prefix(PlayerControl __instance, float val, ref bool __result)
    {
        if (!LegacyCsDamageCooldown.ShouldBlockServerDamage(__instance, val))
        {
            return true;
        }

        __result = __instance.isAlive;
        return false;
    }

    [HarmonyPatch(typeof(Net_CS.CsBasePlayer), "GetInitData")]
    [HarmonyPostfix]
    private static void CsBasePlayer_GetInitData_Postfix(Net_CS.CsBasePlayer __instance, ref string __result)
    {
        if (!NetManager.isServer)
        {
            return;
        }

        __result = LegacyCsDamageCooldown.AddServerStateToJson(__instance, __result);
    }

    [HarmonyPatch(typeof(Net_CS.CsBasePlayer), "_SetInitData")]
    [HarmonyPrefix]
    private static void CsBasePlayer_SetInitData_Prefix(Net_CS.CsBasePlayer __instance, ref string initData)
    {
        if (!NetManager.isOnlineClient)
        {
            return;
        }

        LegacyCsDamageCooldown.ReadClientStateFromJson(__instance, ref initData);
    }

    [HarmonyPatch(typeof(NetInputControlUI), "ApplyOverrideColorForPlayerControl")]
    [HarmonyPostfix]
    private static void NetInputControlUI_ApplyOverrideColorForPlayerControl_Postfix(NetInputControlUI __instance)
    {
        if (!NetManager.isOnlineClient || __instance == null)
        {
            return;
        }

        LegacyCsDamageCooldown.ApplyClientFresnelOverride(__instance.netInput);
    }

    [HarmonyPatch(typeof(NetInputControlUI), "DisableIconByEventIfNeed")]
    [HarmonyPostfix]
    private static void NetInputControlUI_DisableIconByEventIfNeed_Postfix(NetInputControlUI __instance)
    {
        if (!NetManager.isOnlineClient || __instance == null)
        {
            return;
        }

        __instance.ApplyOverrideColorForPlayerControl();
    }

    [HarmonyPatch(typeof(Net_CS), "UpdateClient")]
    [HarmonyPostfix]
    private static void NetCS_UpdateClient_Postfix(Net_CS __instance)
    {
        LegacyCsDamageCooldown.UpdateClient(__instance);
    }

    [HarmonyPatch(typeof(Net_CS.CsBasePlayer), "SetInputNull")]
    [HarmonyPostfix]
    private static void CsBasePlayer_SetInputNull_Postfix(Net_CS.CsBasePlayer __instance)
    {
        LegacyCsDamageCooldown.ClearPlayer(__instance);
    }

    [HarmonyPatch(typeof(Net_BaseEvent), "OnDestroy")]
    [HarmonyPostfix]
    private static void NetBaseEvent_OnDestroy_Postfix(Net_BaseEvent __instance)
    {
        Net_CS cs = __instance as Net_CS;
        if (cs != null)
        {
            LegacyCsDamageCooldown.ClearEvent(cs);
        }
    }

    [HarmonyPatch(typeof(Net_CS), "OnStartServer")]
    [HarmonyPostfix]
    private static void NetCS_OnStartServer_Postfix(ref int ___needKillForFinishEvent)
    {
        if (!NetManager.isServer)
        {
            return;
        }

        ___needKillForFinishEvent = int.MaxValue;
    }

    [HarmonyPatch(typeof(Net_CS), "FirstTimeSpawnPlyaer")]
    [HarmonyPrefix]
    private static bool Net_CS_FirstTimeSpawnPlyaer_Prefix(Net_CS __instance, Net_CS.CsBasePlayer it)
    {
        if (!NetManager.isServer || __instance == null)
        {
            return true;
        }
        if (__instance.state == Net_BaseEvent.CurState.Lobbi)
        {
            Debug.Log("CS FirstTimeSpawn blocked in Lobbi for '" +
                (it != null && it.input != null ? it.input.nikName : "null") + "'");
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(Net_CS), "SpawnEvent")]
    [HarmonyPostfix]
    private static void Net_CS_SpawnEvent_Postfix(Net_CS __instance)
    {
        if (!NetManager.isServer || __instance == null)
        {
            return;
        }
        const int stockMoney = 800;
        for (int i = 0; i < __instance.players.Count; i++)
        {
            Net_CS.CsBasePlayer p = __instance.players[i] as Net_CS.CsBasePlayer;
            if (p == null || p.money <= stockMoney)
            {
                continue;
            }
            int farmed = p.money;
            __instance.ChangeMoney(p.id, stockMoney - p.money);
            Debug.LogWarning("CS balance reset at round start for '" +
                (p.input != null ? p.input.nikName : "null") + "': " +
                farmed.ToString() + " -> " + stockMoney.ToString());
        }
    }

    [HarmonyPatch(typeof(Net_CS), "SendStatesForNewConnectionImp")]
    [HarmonyPrefix]
    private static void Net_CS_SendStatesForNewConnectionImp_Prefix(Net_CS __instance)
    {
        CsReconnectMoneySnapshot.Clear();
        if (!NetManager.isServer || __instance == null)
        {
            return;
        }
        for (int i = 0; i < __instance.players.Count; i++)
        {
            Net_CS.CsBasePlayer p = __instance.players[i] as Net_CS.CsBasePlayer;
            if (p != null)
            {
                CsReconnectMoneySnapshot[p.id] = p.money;
            }
        }
    }

    [HarmonyPatch(typeof(Net_CS), "SendStatesForNewConnectionImp")]
    [HarmonyPostfix]
    private static void Net_CS_SendStatesForNewConnectionImp_Postfix(Net_CS __instance)
    {
        if (!NetManager.isServer || __instance == null || CsReconnectMoneySnapshot.Count == 0)
        {
            return;
        }
        for (int i = 0; i < __instance.players.Count; i++)
        {
            Net_CS.CsBasePlayer p = __instance.players[i] as Net_CS.CsBasePlayer;
            if (p == null)
            {
                continue;
            }
            int before;
            if (!CsReconnectMoneySnapshot.TryGetValue(p.id, out before))
            {
                continue;
            }
            if (p.money == before)
            {
                continue;
            }
            int delta = p.money - before;
            __instance.ChangeMoney(p.id, -delta);
            Debug.LogWarning("CS reconnect money revert for '" +
                (p.input != null ? p.input.nikName : "null") +
                "': delta=" + delta.ToString() +
                ", restored=" + before.ToString() +
                ", state=" + __instance.state.ToString());
        }
        CsReconnectMoneySnapshot.Clear();
    }
}