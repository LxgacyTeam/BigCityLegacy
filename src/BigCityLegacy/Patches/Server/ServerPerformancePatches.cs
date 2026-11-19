using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Conservative dedicated-server CPU optimizations.
///
/// This patch set intentionally avoids physics, animation, GroundLoader and the
/// gameplay Control update loop. It only fixes a confirmed server-side polling
/// bug and suppresses client presentation/input/analytics work in headless mode.
///
/// Emergency rollback: -noServerCpuOptimization
/// </summary>
[HarmonyPatch]
internal static class ServerPerformancePatches
{
    private sealed class DistanceCheckState
    {
        internal bool Initialized;
        internal float LastCheckTime;
    }

    private static readonly ConditionalWeakTable<NetControl, DistanceCheckState> DistanceCheckStates =
        new ConditionalWeakTable<NetControl, DistanceCheckState>();

    private static bool enabledLogWritten;

    private static bool Enabled
    {
        get { return LegacyServerHeadlessBootstrap.IsCpuOptimizationEnabled; }
    }

    private static DistanceCheckState CreateDistanceCheckState(NetControl control)
    {
        return new DistanceCheckState();
    }

    private static void LogEnabledOnce()
    {
        if (enabledLogWritten || !Enabled)
        {
            return;
        }

        enabledLogWritten = true;
        Debug.Log(
            "[BigCityLegacy] Headless server CPU optimizations enabled. " +
            "Use -noServerCpuOptimization to disable them."
        );
    }

    // ---------------------------------------------------------------------
    // Confirmed vanilla bug: NetControl.lastCheckDist is used both as a
    // timestamp and as the minimum distance. After the first real distance
    // check it stops being a timestamp, so Server_ManualUpdate can run every
    // frame instead of roughly once per 2 seconds.
    //
    // Keep vanilla lastCheckDist as the last measured distance for diagnostics,
    // while the throttle timestamp lives in an external weak state table.
    // ---------------------------------------------------------------------
    [HarmonyPatch(typeof(NetControl), "Server_ManualUpdate")]
    [HarmonyPrefix]
    private static bool NetControl_Server_ManualUpdate_Prefix(
        NetControl __instance,
        ref bool __result,
        ref float ___lastCheckDist)
    {
        if (!Enabled)
        {
            return true;
        }

        LogEnabledOnce();

        DistanceCheckState state = DistanceCheckStates.GetValue(__instance, CreateDistanceCheckState);
        float now = nTime.GameThisFrame;

        if (!state.Initialized)
        {
            state.Initialized = true;
            state.LastCheckTime = now;
            __result = false;
            return false;
        }

        // Be robust to a game-time reset when a server is restarted in the same
        // process/session.
        if (now < state.LastCheckTime)
        {
            state.LastCheckTime = now;
            __result = false;
            return false;
        }

        if (now - state.LastCheckTime < 2000f)
        {
            __result = false;
            return false;
        }

        state.LastCheckTime = now;

        if (!__instance.objItem)
        {
            __result = false;
            return false;
        }

        Control control = __instance.objItem.control;
        if (!control || control is PlayerControl || control.child_controls.NotEmpty<Control>() || NetManager.curCount == 0)
        {
            __result = false;
            return false;
        }

        // A connection can exist briefly before its InputControl is completely
        // created. Do not destroy world objects during that transient window.
        if (InputControl.inputs == null || InputControl.inputs.Count == 0)
        {
            __result = false;
            return false;
        }

        Vector3 bodyPos = control.body_pos;
        float minDistance = float.MaxValue;

        for (int i = 0; i < InputControl.inputs.Count; i++)
        {
            InputControl input = InputControl.inputs[i];
            if (!input || !input.currentPlayer)
            {
                continue;
            }

            float distance = hVec3.FastLen(ref input.currentPlayer.body_pos, ref bodyPos);
            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }

        // No usable player position was available this pass. Try again after the
        // normal two-second interval instead of treating infinity as a distance.
        if (minDistance == float.MaxValue)
        {
            __result = false;
            return false;
        }

        ___lastCheckDist = minDistance;

        float destroyDistance = control.isDead ? 50f : 100f;
        if (minDistance <= destroyDistance)
        {
            __result = false;
            return false;
        }

        if (NetManager.isLogType(NetManager.mLogType.Info))
        {
            Debug.Log(
                "DestroyControlByDist: " + minDistance.ToString(CultureInfo.InvariantCulture) +
                " name:  " + __instance.objName
            );
        }

        if (NetManager.isLogType(NetManager.mLogType.All))
        {
            Debug.Log("\tInputsCount: " + InputControl.inputs.Count.ToString());
            for (int i = 0; i < InputControl.inputs.Count; i++)
            {
                InputControl input = InputControl.inputs[i];
                PlayerControl player = input ? input.currentPlayer : null;
                if (!player)
                {
                    continue;
                }

                Debug.Log(
                    "\t\t" + i.ToString() +
                    " player: " + player.name +
                    " body_pos " + player.body_pos.ToString() +
                    " dist " + hVec3.FastLen(ref player.body_pos, ref bodyPos).ToString(CultureInfo.InvariantCulture)
                );
            }
        }

        NetworkServer.Destroy(__instance.gameObject);
        __result = true;
        return false;
    }

    // ---------------------------------------------------------------------
    // Nuligine.OnEnable mixes required shared initialization with purely client
    // setup. On a dedicated server preserve the shared data initialization and
    // deliberately skip resolution/phone setup and CodeStage anti-cheat startup.
    // ---------------------------------------------------------------------
    [HarmonyPatch(typeof(Nuligine), "OnEnable")]
    [HarmonyPrefix]
    private static bool Nuligine_OnEnable_Prefix(Nuligine __instance)
    {
        if (!Enabled)
        {
            return true;
        }

        LogEnabledOnce();

        ArgParcer.GetArgValue(null);

        Nuligine.Weapones weapones = __instance.weapones;
        if (weapones != null)
        {
            weapones.humanWeapones = new WeaponeInfo[]
            {
                weapones.hand,
                weapones.ak47,
                weapones.uzi,
                weapones.gun,
                weapones.bomb,
                weapones.m4a1,
                weapones.mp5,
                weapones.deagle,
                weapones.famas,
                weapones.bison,
                weapones.usp
            };
        }

        Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("en-US");

        // Intentionally skipped in headless mode:
        //   SettingsResolution.CheckResolution();
        //   GamePhone.Hide();
        //   ObscuredPrefs.OnAlterationDetected += ...;
        //   ObscuredCheatingDetector.StartDetection(...);
        return false;
    }

    // ---------------------------------------------------------------------
    // Client-only manual update calls made from Nuligine.Update/LateUpdate.
    // These systems have no authoritative server responsibility.
    // ---------------------------------------------------------------------
    [HarmonyPatch(typeof(Cam), "ManualUpdate")]
    [HarmonyPrefix]
    private static bool Cam_ManualUpdate_Prefix()
    {
        return !Enabled;
    }

    [HarmonyPatch(typeof(Cam), "FixedUpdate")]
    [HarmonyPrefix]
    private static bool Cam_FixedUpdate_Prefix()
    {
        return !Enabled;
    }

    [HarmonyPatch(typeof(SettingsValues), "Update")]
    [HarmonyPrefix]
    private static bool SettingsValues_Update_Prefix()
    {
        return !Enabled;
    }

    [HarmonyPatch(typeof(ProfilerFrame), "NuligineUpdate")]
    [HarmonyPrefix]
    private static bool ProfilerFrame_NuligineUpdate_Prefix()
    {
        return !Enabled;
    }

    [HarmonyPatch(typeof(hCursor), "UpMiddleMouseButton")]
    [HarmonyPrefix]
    private static bool hCursor_UpMiddleMouseButton_Prefix()
    {
        return !Enabled;
    }

    [HarmonyPatch(typeof(Probe), "Check")]
    [HarmonyPrefix]
    private static bool Probe_Check_Prefix()
    {
        return !Enabled;
    }

    [HarmonyPatch(typeof(hFPS), "mUpdate")]
    [HarmonyPrefix]
    private static bool hFPS_mUpdate_Prefix()
    {
        return !Enabled;
    }

    [HarmonyPatch(typeof(UI_EventIcon), "ManualLateUpdate")]
    [HarmonyPrefix]
    private static bool UI_EventIcon_ManualLateUpdate_Prefix()
    {
        return !Enabled;
    }

    [HarmonyPatch(typeof(DistanceView), "UpdateAllInstances")]
    [HarmonyPrefix]
    private static bool DistanceView_UpdateAllInstances_Prefix()
    {
        return !Enabled;
    }

    // Input/menu helpers are private Nuligine methods called every frame even on
    // a dedicated server. Skipping them avoids Legacy Input polling entirely.
    [HarmonyPatch(typeof(Nuligine), "NextControl")]
    [HarmonyPrefix]
    private static bool Nuligine_NextControl_Prefix()
    {
        return !Enabled;
    }

    [HarmonyPatch(typeof(Nuligine), "UpdatePause")]
    [HarmonyPrefix]
    private static bool Nuligine_UpdatePause_Prefix()
    {
        return !Enabled;
    }

    [HarmonyPatch(typeof(Nuligine), "UpEsc")]
    [HarmonyPrefix]
    private static bool Nuligine_UpEsc_Prefix()
    {
        return !Enabled;
    }

    // GameUI still exists as a tiny compatibility shell in headless mode. None
    // of its Unity callbacks are required by server gameplay.
    [HarmonyPatch(typeof(GameUI), "Update")]
    [HarmonyPrefix]
    private static bool GameUI_Update_Prefix()
    {
        return !Enabled;
    }

    [HarmonyPatch(typeof(GameUI), "FixedUpdate")]
    [HarmonyPrefix]
    private static bool GameUI_FixedUpdate_Prefix()
    {
        return !Enabled;
    }

    [HarmonyPatch(typeof(GameUI), "LateUpdate")]
    [HarmonyPrefix]
    private static bool GameUI_LateUpdate_Prefix()
    {
        return !Enabled;
    }

    // Weather presentation can otherwise continue loading/updating render-only
    // bundles even though Weather.MakeCurrent() itself already ignores servers.
    [HarmonyPatch(typeof(WeatherManager), "Update")]
    [HarmonyPrefix]
    private static bool WeatherManager_Update_Prefix()
    {
        return !Enabled;
    }

    [HarmonyPatch(typeof(WeatherManager), "UpdateSwitchByTime")]
    [HarmonyPrefix]
    private static bool WeatherManager_UpdateSwitchByTime_Prefix()
    {
        return !Enabled;
    }

    // Client telemetry/FPS/"where" tracking has no dedicated-server purpose.
    [HarmonyPatch(typeof(Analitics), "Update")]
    [HarmonyPrefix]
    private static bool Analitics_Update_Prefix()
    {
        return !Enabled;
    }

    // Extra UI safety nets. Normally these objects are no longer created by the
    // headless UI path, but if a scene contains one already, its Update is inert.
    [HarmonyPatch(typeof(GamePhone), "Update")]
    [HarmonyPrefix]
    private static bool GamePhone_Update_Prefix()
    {
        return !Enabled;
    }

    [HarmonyPatch(typeof(MenuEsc), "Update")]
    [HarmonyPrefix]
    private static bool MenuEsc_Update_Prefix()
    {
        return !Enabled;
    }
}
