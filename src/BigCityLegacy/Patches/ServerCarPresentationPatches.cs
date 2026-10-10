using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;

/// <summary>
/// Dedicated headless-server presentation-only car update pruning.
/// No change to CarPh, collision physics, network state, AI or damage handlers.
/// Rollback: -noServerCarPresentationOptimization
/// </summary>
[HarmonyPatch]
internal static class ServerCarPresentationPatches
{
    private sealed class ViewUpdateState
    {
        internal float NextRefresh;
    }

    private static readonly ConditionalWeakTable<CarControl, ViewUpdateState> ViewUpdates =
        new ConditionalWeakTable<CarControl, ViewUpdateState>();

    private const float SleepingCarViewRefreshSeconds = 0.5f;
    private static bool? enabled;

    internal static bool Enabled
    {
        get
        {
            if (!enabled.HasValue)
            {
                enabled = LegacyServerHeadlessBootstrap.IsCpuOptimizationEnabled &&
                          !LegacyCommandLine.HasArg("-noServerCarPresentationOptimization");
            }
            return enabled.Value;
        }
    }

    // This is called by Nuligine.LateUpdate for each Control every frame.
    // Leave direct calls to SetViewPosOnBody (teleports, initialization) untouched.
    // Dynamic/occupied/woken cars retain the original behavior.
    [HarmonyPatch(typeof(Control), "isNeedUpdateViewPos")]
    [HarmonyPrefix]
    private static bool Control_isNeedUpdateViewPos_Prefix(Control __instance, ref bool __result)
    {
        if (!Enabled || !NetManager.isServer)
            return true;

        CarControl car = __instance as CarControl;
        if (car == null || car.isUsedInMenu || car.isDead || car.body == null ||
            !car.body.IsSleeping() || car.input != null || car.parent_control != null ||
            (car.child_controls != null && car.child_controls.Count > 0) ||
            ServerCarSleepPatches.IsClientOwnedCar(car))
            return true;

        ViewUpdateState state = ViewUpdates.GetOrCreateValue(car);
        float now = Time.realtimeSinceStartup;
        if (now < state.NextRefresh && state.NextRefresh - now < 5f)
        {
            __result = false;
            return false;
        }
        state.NextRefresh = now + SleepingCarViewRefreshSeconds;
        return true;
    }

    // Unlike the initial scene/map pass, cars are instantiated dynamically.
    // Disable presentation components after CarControl has initialized its
    // dependency references. Do NOT Destroy components or their GameObjects:
    // meshes/transforms can still be referenced by damage and seating logic.
    // Physics colliders and NavMeshObstacle are intentionally untouched.
    [HarmonyPatch(typeof(CarControl), "Start")]
    [HarmonyPostfix]
    private static void CarControl_Start_Postfix(CarControl __instance)
    {
        if (!Enabled || !NetManager.isServer || __instance == null)
            return;

        Renderer[] renderers = __instance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = false;

        Light[] lights = __instance.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
            if (lights[i] != null) lights[i].enabled = false;

        AudioSource[] audioSources = __instance.GetComponentsInChildren<AudioSource>(true);
        for (int i = 0; i < audioSources.Length; i++)
        {
            if (audioSources[i] != null)
            {
                audioSources[i].Stop();
                audioSources[i].enabled = false;
            }
        }

        ParticleSystem[] particles = __instance.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
            if (particles[i] != null) particles[i].Stop();
    }

    // These callbacks only drive client-side audio and visual presentation.
    // We deliberately do NOT disable CarDamage itself, as damage entry points
    // may be called from gameplay and collisions.
    [HarmonyPatch(typeof(CarSound), "Update")]
    [HarmonyPrefix]
    private static bool CarSound_Update_Prefix() { return !Enabled; }

    [HarmonyPatch(typeof(CarDamage), "Update")]
    [HarmonyPrefix]
    private static bool CarDamage_Update_Prefix() { return !Enabled; }

    [HarmonyPatch(typeof(CarDetail), "FixedUpdate")]
    [HarmonyPrefix]
    private static bool CarDetail_FixedUpdate_Prefix() { return !Enabled; }

    [HarmonyPatch(typeof(CarLight), "FixedUpdate")]
    [HarmonyPrefix]
    private static bool CarLight_FixedUpdate_Prefix() { return !Enabled; }

    // Inactive sirens only animate LensFlare brightness; active sirens retain
    // vanilla timer/state transitions so server gameplay state is unchanged.
    [HarmonyPatch(typeof(CarSirena), "Update")]
    [HarmonyPrefix]
    private static bool CarSirena_Update_Prefix(CarSirena __instance)
    {
        return !Enabled || __instance.GetCurrentState() != CarSirena.State.NotActive;
    }
}
