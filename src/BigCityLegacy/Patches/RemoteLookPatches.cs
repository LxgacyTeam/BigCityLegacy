using HarmonyLib;

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

    [HarmonyPatch(typeof(PlayerControl), "OnDisable")]
    [HarmonyPrefix]
    private static void BeforeDisable(PlayerControl __instance)
    {
        var look = __instance.GetComponent<LegacyRemoteLook>();
        if (look) look.ResetPose();
    }
}
