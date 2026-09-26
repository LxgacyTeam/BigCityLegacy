using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
internal static class NetConnectAndControlPatches
{
    private static readonly HashSet<NetConnect> WaitingForWorld = new HashSet<NetConnect>();


    [HarmonyPatch(typeof(NetInputControl), "CheckDisconnect")]
    [HarmonyPrefix]
    private static bool NetInputControl_CheckDisconnect_Prefix(NetInputControl __instance)
    {
        if (!NetManager.isServer || !LegacyIdleKickTimeout.HasOverride)
        {
            return true;
        }

        LegacyIdleKickTimeout.CheckIdleDisconnect(__instance);
        return false;
    }

    [HarmonyPatch(typeof(NetConnect), "Cmd_SetPrivateID")]
    [HarmonyPrefix]
    private static bool NetConnect_CmdSetPrivateID_Prefix(NetConnect __instance, string setPrivateID, string setNikName, string setPlayerXml, RuntimePlatform platform)
    {
        string originalNikName = setNikName;
        string sanitizedNikName = LegacyServerLimits.SanitizeNikName(setNikName);

        if (NetManager.isServer && LegacyServerAdminService.TryRejectBannedConnection(__instance, setPrivateID, sanitizedNikName))
        {
            return false;
        }

        __instance.privateGUID = setPrivateID;
        __instance.nikName = sanitizedNikName;

        if (NetManager.isServer)
        {
            LegacyServerAdminService.ObserveAuthenticatedConnection(__instance, setPrivateID, sanitizedNikName, platform);
        }


        if (NetManager.isLogType(NetManager.mLogType.Info))
        {
            Debug.Log("Player: " + __instance.nikName + " guid: " + __instance.privateGUID + " PlayerXml: " + setPlayerXml);
        }
        if (NetManagerTools.isCommandLineArgHaveServerStr())
        {
            if (!WaitingForWorld.Contains(__instance))
            {
                WaitingForWorld.Add(__instance);
                __instance.StartCoroutine(InitInputControlWhenWorldReady(__instance, setPlayerXml, platform));
            }
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(NetInputControl), "Cmd_InitNikName")]
    [HarmonyPrefix]
    private static void NetInputControl_CmdInitNikName_Prefix(NetInputControl __instance, ref string set, string reasone)
    {
        if (!NetManager.isServer)
        {
            return;
        }

        string originalNikName = set;
        set = LegacyServerLimits.SanitizeNikName(set);
        LegacyServerAdminService.ObserveNickname(__instance, set);

    }

    [HarmonyPatch(typeof(NetInputControl), "DeliveryCar")]
    [HarmonyPrefix]
    private static bool NetInputControl_DeliveryCar_Prefix(NetInputControl __instance, string fileName)
    {
        if (!NetManager.isServer)
        {
            return true;
        }

        Net_BaseEvent curEvent = __instance.curEvent;
        if (curEvent is Net_CS && curEvent.state != Net_BaseEvent.CurState.Lobbi && curEvent.state != Net_BaseEvent.CurState.Complite)
        {
            Debug.LogWarning(
                "[Server] Car spawn rejected for '" + LegacyServerAdminService.GetNicknameForInput(__instance) +
                "': active CS/CS_Sur round, state=" + curEvent.state.ToString()
            );
            return false;
        }

        int currentCars;
        int playerCars;

        if (!LegacyServerLimits.CanSpawnNetworkCar(__instance, out currentCars, out playerCars))
        {
            Debug.LogWarning(
                "[Server] Car spawn rejected for '" + LegacyServerAdminService.GetNicknameForInput(__instance) +
                "': global=" + currentCars.ToString() + "/" + LegacyServerLimits.MaxCars.ToString() +
                ", player=" + playerCars.ToString() + "/" + LegacyServerLimits.MaxCarsByPlayer.ToString()
            );
            return false;
        }

        LegacyServerAdminService.RegisterCarSpawnRequest(__instance);
        return true;
    }

    [HarmonyPatch(typeof(NetConnect), "InitInputControl")]
    [HarmonyPrefix]
    private static bool NetConnect_InitInputControl_Prefix(NetConnect __instance)
    {
        string reason;
        if (NetManagerTools.isCommandLineArgHaveServerStr() && !IsServerPlayerCreateReady(out reason))
        {
            Debug.LogWarning("InitInputControl cancelled: server is not ready, reason: " + reason);
            if (__instance.conn != null)
            {
                __instance.conn.Disconnect();
            }
            return false;
        }
        LegacyHelpers.EnsureServerNetChat(NetManager.me);
        return true;
    }

    private static IEnumerator InitInputControlWhenWorldReady(NetConnect connect, string setPlayerXml, RuntimePlatform platform)
    {
        float started = nTime.Ms;
        float nextLogAt = started;
        float timeoutAt = started + 60000f;
        string reason;
        bool logged = false;

        while (!IsServerPlayerCreateReady(out reason) && nTime.Ms < timeoutAt)
        {
            if (!logged)
            {
                logged = true;
                Debug.Log("Waiting server world for player '" + connect.nikName + "': " + reason);
            }
            yield return null;
        }
        WaitingForWorld.Remove(connect);
        if (!IsServerPlayerCreateReady(out reason))
        {
            Debug.LogError("Server world was not ready for player '" + connect.nikName + "' in " + (nTime.Ms - started).ToString() + " ms. Reason: " + reason);
            if (connect.conn != null)
            {
                connect.conn.Disconnect();
            }
            yield break;
        }
        Debug.Log("Server world ready for player '" + connect.nikName + "' in " + (nTime.Ms - started).ToString() + " ms");
        connect.InitInputControl(setPlayerXml, platform);
    }

    private static bool IsServerPlayerCreateReady(out string reason)
    {
        reason = string.Empty;
        bool spawnsReady = RaceAndWorldPatches.TryEnsureNetSpawns();
        bool groundReady = GroundLoader.me == null || GroundLoader.isAllLoaded;
        bool mapReady = MapProLoader.me == null || MapProLoader.canControlsLoad();
        if (!spawnsReady || !groundReady || !mapReady)
        {
            reason = "SpawnPos/Map/Ground is not ready";
            return false;
        }
        if (!NetManager.me)
        {
            reason = "NetManager.me is null";
            return false;
        }
        if (!NetManager.me.netControl)
        {
            reason = "NetManager.me.netControl prefab is null";
            return false;
        }
        if (!NetManager.me.player_prefab)
        {
            reason = "NetManager.me.player_prefab is null";
            return false;
        }
        if (!Created.me)
        {
            reason = "Created.me is null";
            return false;
        }
        if (!Created.me.inputControls)
        {
            reason = "Created.me.inputControls is null";
            return false;
        }
        if (!Created.me.Humans)
        {
            reason = "Created.me.Humans is null";
            return false;
        }
        ControlArray.InitControlItems();
        ControlArray humans = ControlArray.byType(ControlArray.Type.Human);
        if (!humans)
        {
            reason = "ControlArray.Human is null";
            return false;
        }
        if (humans.controls == null || humans.controls.Count == 0)
        {
            reason = "ControlArray.Human has no controls";
            return false;
        }
        ControlArray.ControlItem male = humans.GetByName("Male");
        if (male == null)
        {
            if (humans.GetReadyControl() == null)
            {
                reason = "No ready human control";
                return false;
            }
            return true;
        }
        if (!male.control)
        {
            reason = "Human control 'Male' is not loaded";
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(NetInputControl), "Cmd_AddMessage")]
    [HarmonyPrefix]
    private static bool NetInputControl_CmdAddMessage_Prefix(NetInputControl __instance, string s)
    {
        LegacyHelpers.EnsureServerNetChat(NetManager.me);
        if (NetChat.me)
        {
            NetChat.me.AddMessage(__instance, s, true, 0f);
        }
        else
        {
            Debug.LogWarning("Chat message ignored because NetChat.me is null");
        }
        return false;
    }

    [HarmonyPatch(typeof(NetControl), "ActualCreateControl")]
    [HarmonyPrefix]
    private static bool NetControl_ActualCreateControl_Prefix(NetControl __instance)
    {
        __instance.move2cacheCount = 0;
        if (__instance.objItem)
        {
            if (NetManager.isLogType(NetManager.mLogType.Info))
            {
                Debug.Log("Create By name: " + __instance.objName + " but alredy created");
            }
            return false;
        }
        if (NetManager.isLogType(NetManager.mLogType.Info))
        {
            NetControl parent = __instance.parentNetControl ? __instance.parentNetControl.GetComponent<NetControl>() : null;
            Debug.Log("NetControl.CreateByName, objName: " + __instance.objName + (parent ? ("   parent: " + parent.objName) : null), __instance);
        }
        ControlArray humans = ControlArray.byType(ControlArray.Type.Human);
        if (humans)
        {
            __instance.objItem = humans.GetOrCreateByName(__instance.objName, __instance);
            if (!__instance.objItem && __instance.objName == "Male")
            {
                ControlArray.ControlItem ready = humans.GetReadyControl();
                if (ready != null)
                {
                    Debug.LogWarning("NetControl.CreateByName fallback human: Male -> " + ready.name);
                    __instance.NetworkobjName = ready.name;
                    __instance.objItem = humans.GetOrCreateByName(ready.name, __instance);
                }
            }
        }
        else
        {
            Debug.LogError("NetControl.CreateByName failed: Human ControlArray is null, objName: " + __instance.objName, __instance);
        }
        if (__instance.objItem)
        {
            if (__instance.syncPlayer)
            {
                __instance.syncPlayer.enabled = true;
            }
        }
        else
        {
            ControlArray cars = ControlArray.byType(ControlArray.Type.Car);
            if (cars)
            {
                __instance.objItem = cars.GetOrCreateByName(__instance.objName, __instance);
            }
            else
            {
                Debug.LogError("NetControl.CreateByName failed: Car ControlArray is null, objName: " + __instance.objName, __instance);
            }
        }
        if (!__instance.objItem || !__instance.objItem.control)
        {
            Debug.LogError("NetControl.CreateByName failed: object not found or not loaded, objName: " + __instance.objName, __instance);
            __instance.objItem = null;
            __instance.control = null;
            return false;
        }
        __instance.control = __instance.objItem.control;
        __instance.objItem.holdNotBackToCach = true;
        __instance.lastCheckDist = nTime.GameThisFrame;
        if (__instance.smoothSync)
        {
            __instance.smoothSync.Init(__instance.control);
            if (NetManager.isServer && __instance.objItem.control is CarControl)
            {
                __instance.radioSelect = Radio.GetRandomRadio();
            }
            __instance.InitAllDataToControl_IfTheyCreated();
            __instance.UpObjNameInEditor();
        }
        else
        {
            Debug.LogError("NetControl.CreateByName failed: smoothSync is null, objName: " + __instance.objName, __instance);
        }
        return false;
    }

    [HarmonyPatch(typeof(NetControl), "ActualCreateControl")]
    [HarmonyPostfix]
    private static void NetControl_ActualCreateControl_Postfix(NetControl __instance)
    {
        LegacyServerAdminService.ObserveCreatedNetworkControl(__instance);
    }

    [HarmonyPatch(typeof(NetSpawn), "CheckOnDead_OnServer")]
    [HarmonyPrefix]
    private static bool NetSpawn_CheckOnDeadOnServer_Prefix(NetSpawn __instance)
    {
        if (!LegacyNearestPointRespawn.IsReady)
        {
            return true;
        }

        if (__instance == null || __instance.netInput == null || __instance.netInput.input == null)
        {
            return true;
        }

        PlayerControl currentPlayer = __instance.netInput.input.currentPlayer;
        if (!currentPlayer || !currentPlayer.isDead || nTime.GameThisFrame - currentPlayer.timeLiveZero <= 2600f)
        {
            return false;
        }

        Control currentOrParent = __instance.netInput.input.currentOrParent;
        Vector3 currentPlayerPos = currentOrParent ? currentOrParent.body_pos : currentPlayer.body_pos;

        Vector3 spawnPos;
        if (!LegacyNearestPointRespawn.TryGetNearestRespawnPoint(currentPlayerPos, out spawnPos))
        {
            return true;
        }

        Quaternion identity = Quaternion.identity;
        Net_BaseEvent.isNeedAutoRespaumWhenDeadStatic(__instance.netInput, ref spawnPos, ref identity);
        __instance.Spawn(spawnPos, identity, true, true);
        return false;
    }

    [HarmonyPatch(typeof(Nuligine), "SpeedHukDetected")]
    [HarmonyPrefix]
    private static bool SpeedHukDetected_Prefix()
    {
        return false;
    }

    [HarmonyPatch(typeof(PlayerPhysics), "can_catapulta_do", MethodType.Getter)]
    [HarmonyPrefix]
    private static bool PlayerPhysics_can_catapulta_do_Prefix(PlayerPhysics __instance, ref bool __result)
    {
        if (__instance == null || __instance.player == null)
        {
            return true;
        }

        try
        {

            bool isInCar = __instance.player.InCar != null && __instance.player.InCar.carState == PlayerInCar.CarState.InCar;
            bool hasSitData = __instance.player.enterTo != null && __instance.player.enterTo.sit_data != null;
            if (isInCar && hasSitData)
            {
                __result = true; 
                return false; 
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error in Catapult Prefix Patch: " + ex.Message);
            return true; 
        }

        return true;
    }

    [HarmonyPatch(typeof(NetManagerTools), "FindColorForUser")]
    [HarmonyPrefix]
    private static bool NetManagerTools_FindColorForUser_Prefix(NetInputControl netInput, ref Color __result)
    {
        float hue = UnityEngine.Random.Range(0f, 1f);
        float saturation = 0.8f;
        float value = 0.9f;

        __result = Color.HSVToRGB(hue, saturation, value);
        return false; 
    }

    [HarmonyPatch(typeof(WeatherManager), "DoRandom")]
    [HarmonyPrefix]
    private static bool WeatherManager_DoRandom_Prefix()
    {
        return !LegacyWeatherLock.IsEnabled;
    }

    [HarmonyPatch(typeof(WeatherManager), "RandomIfNeed")]
    [HarmonyPrefix]
    private static bool WeatherManager_RandomIfNeed_Prefix()
    {
        return !LegacyWeatherLock.IsEnabled;
    }

    [HarmonyPatch(typeof(WeatherManager), "UpdateSwitchByTime")]
    [HarmonyPrefix]
    private static bool WeatherManager_UpdateSwitchByTime_Prefix()
    {
        return !LegacyWeatherLock.IsEnabled;
    }

    [HarmonyPatch(typeof(WeatherManager), "Start")]
    [HarmonyPostfix]
    private static void WeatherManager_Start_Postfix()
    {
        LegacyWeatherLock.Bootstrapped = true;
    }

    [HarmonyPatch(typeof(WeatherManager), "MakeCurrentByName")]
    [HarmonyPrefix]
    private static bool WeatherManager_MakeCurrentByName_Prefix()
    {
        return !LegacyWeatherLock.IsEnabled || !LegacyWeatherLock.Bootstrapped;
    }

    [HarmonyPatch(typeof(WeatherManager.It), "MakeCurrent")]
    [HarmonyPrefix]
    private static bool WeatherManager_It_MakeCurrent_Prefix()
    {
        return !LegacyWeatherLock.IsEnabled || !LegacyWeatherLock.Bootstrapped;
    }

}
