using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
internal static class RemoteLookPatches
{
    [HarmonyPatch(typeof(PlayerControl), "SetInputState")]
    [HarmonyPostfix]
    private static void AfterInput(PlayerControl __instance) { LegacyRemoteLook.Attach(__instance); }

    [HarmonyPatch(typeof(PlayerControl), "EventAllMeshesLoaded")]
    [HarmonyPostfix]
    private static void AfterMeshes(PlayerControl __instance) { LegacyRemoteLook.Attach(__instance); }

    [HarmonyPatch(typeof(NetControl), "ActualCreateControl")]
    [HarmonyPostfix]
    private static void AfterNetworkCreation(NetControl __instance) { LegacyRemoteLook.Attach(__instance.control as PlayerControl); }

    [HarmonyPatch(typeof(NetControl), "Target_CamDir")]
    [HarmonyPostfix]
    private static void AfterCameraPacket(NetControl __instance) { LegacyRemoteLook.Receive(__instance); }

    [HarmonyPatch(typeof(Control), "SetCamDir")]
    [HarmonyPrefix]
    private static bool SetCameraDirection(Control __instance, Quaternion setCurDir)
    {
        if (!LegacyCameraLookSync.OwnsCamera(__instance as PlayerControl)) return true;
        __instance.cur_cam_dir = setCurDir;
        return false;
    }

    [HarmonyPatch(typeof(CamApply), "LateUpdate")]
    [HarmonyPostfix]
    private static void AfterCameraApplied() { LegacyCameraLookSync.Publish(); }

    [HarmonyPatch(typeof(PlayerControl), "OnDisable")]
    [HarmonyPrefix]
    private static void BeforeDisable(PlayerControl __instance)
    {
        var look = __instance.GetComponent<LegacyRemoteLook>();
        if (look) look.ResetPose();
    }
}
