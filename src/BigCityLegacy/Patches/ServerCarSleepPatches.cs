using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Dedicated-server car CPU optimizations.
///
/// 1) Replaces 9.4 recursive nearby-body sleep checks with the simple local
///    sleep policy used by 4.9.
/// 2) Gives healthy, empty, server-owned sleeping cars a very small fixed-step
///    fast path instead of running the full CarControl.mFixedUpdate pipeline.
/// 3) For client-owned cars, keeps SmoothSync as the authoritative movement
///    source on the server and skips CarPh wheel/drivetrain simulation.
///
/// Physics representation, colliders, Rigidbody collision callbacks, damage,
/// network authority and SmoothSync are deliberately kept intact.
///
/// Rollback flags:
///   -noServerCarAggressiveSleep
///   -noServerCarSleepFastPath
///   -noServerClientCarPhysicsBypass
/// </summary>

[HarmonyPatch]
internal static class ServerCarSleepPatches
{
    private const float EmptyCarSleepThreshold = 0.05f;
    private const float DrivenCarSleepThreshold = 0.003f;
    // Do not sleep just because the body was created some time ago. Require
    // low angular/linear movement continuously for at least 1.5 seconds.
    private const float DefaultSleepDelayMs = 1500f;
    private static float? cachedSleepDelayMs;

    internal static float SleepDelayMs
    {
        get
        {
            if (!cachedSleepDelayMs.HasValue)
            {
                float parsed;
                string value = LegacyCommandLine.GetArgValue("-serverCarSleepDelayMs");
                if (!float.TryParse(value, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out parsed))
                    parsed = DefaultSleepDelayMs;
                cachedSleepDelayMs = Mathf.Clamp(parsed, 500f, 5000f);
            }
            return cachedSleepDelayMs.Value;
        }
    }

    private static bool aggressiveSleepLogWritten;
    private static bool sleepFastPathLogWritten;
    private static bool clientPhysicsBypassLogWritten;

    private static bool? cachedAggressiveSleepEnabled;
    private static bool? cachedSleepFastPathEnabled;
    private static bool? cachedClientPhysicsBypassEnabled;

    internal static bool Enabled
    {
        get { return AggressiveSleepEnabled; }
    }

    internal static bool AggressiveSleepEnabled
    {
        get
        {
            if (!cachedAggressiveSleepEnabled.HasValue)
            {
                cachedAggressiveSleepEnabled =
                    LegacyServerHeadlessBootstrap.IsCpuOptimizationEnabled &&
                    !LegacyCommandLine.HasArg("-noServerCarAggressiveSleep");
            }

            return cachedAggressiveSleepEnabled.Value;
        }
    }

    internal static bool SleepFastPathEnabled
    {
        get
        {
            if (!cachedSleepFastPathEnabled.HasValue)
            {
                cachedSleepFastPathEnabled =
                    LegacyServerHeadlessBootstrap.IsCpuOptimizationEnabled &&
                    !LegacyCommandLine.HasArg("-noServerCarSleepFastPath");
            }

            return cachedSleepFastPathEnabled.Value;
        }
    }

    internal static bool ClientOwnedPhysicsBypassEnabled
    {
        get
        {
            if (!cachedClientPhysicsBypassEnabled.HasValue)
            {
                cachedClientPhysicsBypassEnabled =
                    LegacyServerHeadlessBootstrap.IsCpuOptimizationEnabled &&
                    !LegacyCommandLine.HasArg("-noServerClientCarPhysicsBypass");
            }

            return cachedClientPhysicsBypassEnabled.Value;
        }
    }

    private static void LogAggressiveSleepOnce()
    {
        if (aggressiveSleepLogWritten || !AggressiveSleepEnabled)
        {
            return;
        }

        aggressiveSleepLogWritten = true;
        Debug.Log(
            "[BigCityLegacy] Legacy 4.9-style aggressive car sleep enabled. " +
            "Recursive nearby-body sleep checks are disabled; quiet period=" + SleepDelayMs + "ms. " +
            "Use -noServerCarAggressiveSleep to restore vanilla 9.4 behavior."
        );
    }

    private static void LogSleepFastPathOnce()
    {
        if (sleepFastPathLogWritten || !SleepFastPathEnabled)
        {
            return;
        }

        sleepFastPathLogWritten = true;
        Debug.Log(
            "[BigCityLegacy] Sleeping server-car fixed-update fast path enabled. " +
            "Use -noServerCarSleepFastPath to disable it."
        );
    }

    private static void LogClientPhysicsBypassOnce()
    {
        if (clientPhysicsBypassLogWritten || !ClientOwnedPhysicsBypassEnabled)
        {
            return;
        }

        clientPhysicsBypassLogWritten = true;
        Debug.Log(
            "[BigCityLegacy] Client-owned car CarPh bypass enabled. " +
            "SmoothSync remains the server movement source for owned cars. " +
            "Use -noServerClientCarPhysicsBypass to disable it."
        );
    }

    internal static bool IsClientOwnedCar(CarControl car)
    {
        if (car == null || !NetManager.isServer)
        {
            return false;
        }

        NetControl netControl = car.net_control;
        return netControl != null &&
               netControl.netIdentity != null &&
               netControl.netIdentity.clientAuthorityOwner != null;
    }

    internal static bool IsSleepingFastPathCandidate(CarControl car)
    {
        if (car == null || !NetManager.isServer || !SleepFastPathEnabled)
        {
            return false;
        }

        if (car.isUsedInMenu || car.isDead || car.live < 15f)
        {
            return false;
        }

        if (car.body == null || !car.isBodySleep || !car.body.IsSleeping())
        {
            return false;
        }

        if (IsClientOwnedCar(car))
        {
            return false;
        }

        if (car.parent_control != null || car.input != null)
        {
            return false;
        }

        if (car.child_controls != null && car.child_controls.Count != 0)
        {
            return false;
        }

        if (car.hadnle_sit != null && car.hadnle_sit.who_sit_here != null)
        {
            return false;
        }

        if (car.resp != null || car.kill != null)
        {
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(CarControl), "UpSleep")]
    [HarmonyPrefix]
    private static bool CarControl_UpSleep_Prefix(CarControl __instance)
    {
        if (!AggressiveSleepEnabled)
        {
            return true;
        }

        LogAggressiveSleepOnce();

        if (__instance == null || !NetManager.isServer)
        {
            return true;
        }

        Rigidbody body = __instance.body;
        if (body == null || body.isKinematic)
        {
            return false;
        }

        float threshold = __instance.nowHandleUser
            ? DrivenCarSleepThreshold
            : EmptyCarSleepThreshold;

        float now = nTime.GameThisFrame;
        if (__instance.angM >= threshold || __instance.linM >= threshold)
        {
            // Critical: reset the quiet-period timestamp while a car is
            // moving. The former policy measured only from the last body
            // sleep/wake transition, which could sleep an airborne car on
            // the first low-velocity sample near the apex of its trajectory.
            __instance.timeCheckBodySleep = now;
            return false;
        }

        float elapsed = now - __instance.timeCheckBodySleep;

        if (elapsed < 0f)
        {
            __instance.timeCheckBodySleep = now;
            return false;
        }

        if (elapsed <= SleepDelayMs)
        {
            return false;
        }

        body.ResetForces();
        body.Sleep();
        __instance.isBodySleep = true;
        __instance.timeCheckBodySleep = now;

        return false;
    }

    [HarmonyPatch(typeof(CarControl), "mFixedUpdate")]
    [HarmonyPrefix]
    private static bool CarControl_mFixedUpdate_Prefix(CarControl __instance, bool calledFromParent)
    {
        if (!SleepFastPathEnabled || __instance == null || !NetManager.isServer)
        {
            return true;
        }

        LogSleepFastPathOnce();

        if (!IsSleepingFastPathCandidate(__instance))
        {
            return true;
        }

        return false;
    }

    [HarmonyPatch(typeof(CarPh), "UpdateStep1")]
    [HarmonyPrefix]
    private static bool CarPh_UpdateStep1_Prefix(CarPh __instance)
    {
        if (!ClientOwnedPhysicsBypassEnabled || !NetManager.isServer || __instance == null)
        {
            return true;
        }

        CarControl car = __instance.car;
        if (!IsClientOwnedCar(car))
        {
            return true;
        }

        LogClientPhysicsBypassOnce();
        return false;
    }

    [HarmonyPatch(typeof(CarPh), "UpdateStep2")]
    [HarmonyPrefix]
    private static bool CarPh_UpdateStep2_Prefix(CarPh __instance)
    {
        if (!ClientOwnedPhysicsBypassEnabled || !NetManager.isServer || __instance == null)
        {
            return true;
        }

        CarControl car = __instance.car;
        if (!IsClientOwnedCar(car))
        {
            return true;
        }

        LogClientPhysicsBypassOnce();

        if (__instance.engine != null)
        {
            __instance.engine.UpdateSound();
        }

        return false;
    }

    [HarmonyPatch(typeof(NetControl), "SetOwner_WithChecks")]
    [HarmonyPostfix]
    private static void NetControl_SetOwner_WithChecks_Postfix(NetControl __instance, NetworkIdentity __0)
    {
        if (!ClientOwnedPhysicsBypassEnabled || !NetManager.isServer || __instance == null || __0 != null)
        {
            return;
        }

        CarControl car = __instance.control as CarControl;
        if (car == null || car.body == null || car.ph_car == null)
        {
            return;
        }

        car.UpBodyInfo();
        car.ph_car.WasMovedResetForces();

        car.CheckBodySleep();
    }
}
