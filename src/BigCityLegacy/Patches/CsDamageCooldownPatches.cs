using HarmonyLib;

[HarmonyPatch]
internal static class CsDamageCooldownPatches
{
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
}
