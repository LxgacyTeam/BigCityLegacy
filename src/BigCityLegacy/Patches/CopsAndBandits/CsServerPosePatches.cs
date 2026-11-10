using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
internal static class CsServerPosePatches
{
    [HarmonyPatch(typeof(PlayerControl), "CheckAnimCulling")]
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void KeepAnimating(PlayerControl __instance) { LegacyCsServerPose.KeepAnimating(__instance); }

    [HarmonyPatch(typeof(PlayerControl), "mFixedUpdate")]
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void AfterFixedUpdate(PlayerControl __instance) { LegacyCsServerPose.KeepAnimating(__instance); }

    [HarmonyPatch(typeof(PlayerControl), "Dist2CamWasChanget")]
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void AfterVisibilityChange(PlayerControl __instance) { LegacyCsServerPose.KeepAnimating(__instance); }

    [HarmonyPatch(typeof(PlayerControl), "EventAllMeshesLoaded")]
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void ModelLoaded(PlayerControl __instance) { LegacyCsServerPose.ModelLoaded(__instance); }

    [HarmonyPatch(typeof(NetSpawn), "Spawn")]
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Spawned(NetSpawn __instance, bool isNeedReset) { LegacyCsServerPose.Spawned(__instance, isNeedReset); }

    [HarmonyPatch(typeof(NetSpawn), "Cmd_GroundUnderSpawnLoadedCanRemoveHold")]
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Ready(NetSpawn __instance) { LegacyCsServerPose.SpawnReady(__instance); }

    [HarmonyPatch(typeof(PlayerRagDoll), "real_ChangeState")]
    [HarmonyPrefix]
    private static void BeforeRagdoll(PlayerRagDoll __instance, out PlayerRagDoll.State __state) { __state = __instance.state; }

    [HarmonyPatch(typeof(PlayerRagDoll), "real_ChangeState")]
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void AfterRagdoll(PlayerRagDoll __instance, PlayerRagDoll.State __state)
    {
        if (__state != __instance.state && __instance.state == PlayerRagDoll.State.no_used)
            LegacyCsServerPose.ReturnedFromRagdoll(__instance.player);
    }

    [HarmonyPatch(typeof(PlyaerAnimEventsReciver), "OnAnimatorMove")]
    [HarmonyPrefix]
    private static bool RootMotion(PlyaerAnimEventsReciver __instance) { return !LegacyCsServerPose.IsEvaluating(__instance.player); }

    [HarmonyPatch(typeof(Nuligine), "LateUpdate")]
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void LateUpdate() { LegacyCsServerPose.Tick(); }

    [HarmonyPatch(typeof(NetManager), "OnStartServer")]
    [HarmonyPrefix]
    private static void StartServer() { LegacyCsServerPose.Reset(); }

    [HarmonyPatch(typeof(NetManager), "NowStoped")]
    [HarmonyPostfix]
    private static void StopServer() { LegacyCsServerPose.Reset(); }
}
