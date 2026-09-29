using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
internal static class ServerHeadlessOptimizationPatches
{
    private static float nextHeadlessChatCleanupTime;
    [HarmonyPatch(typeof(MapProObjInfo), "SetLoadedObject")]
    [HarmonyPrefix]
    private static bool MapProObjInfo_SetLoadedObject_Prefix(MapProObjInfo __instance, UnityEngine.Object file)
    {
        if (!LegacyServerHeadlessBootstrap.TrySetLoadedMapObject(__instance, file))
        {
            return true;
        }
        return false;
    }

    [HarmonyPatch(typeof(MapProObjInfo), "Remove")]
    [HarmonyPrefix]
    private static void MapProObjInfo_Remove_Prefix(MapProObjInfo __instance)
    {
        LegacyServerHeadlessBootstrap.ReleaseMapTemplate(__instance);
    }

    [HarmonyPatch(typeof(GroundItem), "SetObject")]
    [HarmonyPostfix]
    private static void GroundItem_SetObject_Postfix(GroundItem __instance)
    {
        LegacyServerHeadlessBootstrap.StripGroundInstance(__instance);
    }

    [HarmonyPatch(typeof(MapPro), "RemoveTexturesAndSomeDataOnServer")]
    [HarmonyPrefix]
    private static bool MapPro_RemoveTexturesAndSomeDataOnServer_Prefix(MapPro __instance)
    {
        if (!LegacyServerHeadlessBootstrap.IsOptimizationEnabled)
        {
            return true;
        }

        LegacyServerHeadlessBootstrap.HandleMapVisualAssetCleanup(__instance);
        return false;
    }

    [HarmonyPatch(typeof(GameUI), "Init")]
    [HarmonyPrefix]
    private static bool GameUI_Init_Prefix(GameUI __instance)
    {
        if (!LegacyServerHeadlessBootstrap.IsOptimizationEnabled)
        {
            return true;
        }

        LegacyServerHeadlessBootstrap.CreateMinimalGameUI(__instance);
        return false;
    }

    [HarmonyPatch(typeof(GameUI), "UpResolution")]
    [HarmonyPrefix]
    private static bool GameUI_UpResolution_Prefix()
    {
        return !LegacyServerHeadlessBootstrap.IsOptimizationEnabled;
    }

    [HarmonyPatch(typeof(GroundLoader), "CreateLod")]
    [HarmonyPrefix]
    private static bool GroundLoader_CreateLod_Prefix()
    {
        return !LegacyServerHeadlessBootstrap.IsOptimizationEnabled;
    }

    [HarmonyPatch(typeof(NetChat), "Awake")]
    [HarmonyPrefix]
    private static bool NetChat_Awake_Prefix(NetChat __instance)
    {
        if (!LegacyServerHeadlessBootstrap.IsOptimizationEnabled)
        {
            return true;
        }

        LegacyServerHeadlessBootstrap.PrepareHeadlessNetChat(__instance);
        return false;
    }

    [HarmonyPatch(typeof(NetChat), "OnDisable")]
    [HarmonyPrefix]
    private static bool NetChat_OnDisable_Prefix()
    {
        return !LegacyServerHeadlessBootstrap.IsOptimizationEnabled;
    }

    [HarmonyPatch(typeof(NetChat), "LateUpdateManual")]
    [HarmonyPrefix]
    private static bool NetChat_LateUpdateManual_Prefix(NetChat __instance)
    {
        if (!LegacyServerHeadlessBootstrap.IsOptimizationEnabled)
        {
            if (!__instance.inputField || !__instance.inputImage || !__instance.root)
            {
                return false;
            }
            return true;
        }

        float now = nTime.GameThisFrame;
        if (now < nextHeadlessChatCleanupTime - 5000f)
        {
            nextHeadlessChatCleanupTime = 0f;
        }
        if (now >= nextHeadlessChatCleanupTime)
        {
            nextHeadlessChatCleanupTime = now + 1000f;
            __instance.RemoveUnusedLiveTimeMessages();
        }
        return false;
    }

    [HarmonyPatch(typeof(NetChat), "UpTxts")]
    [HarmonyPrefix]
    private static bool NetChat_UpTxts_Prefix()
    {
        return !LegacyServerHeadlessBootstrap.IsOptimizationEnabled;
    }

    [HarmonyPatch(typeof(NetChat), "OnDestroy")]
    [HarmonyPostfix]
    private static void NetChat_OnDestroy_Postfix(NetChat __instance)
    {
        if (NetChat.me == __instance)
        {
            NetChat.me = null;
        }
    }

    [HarmonyPatch(typeof(Nuligine), "Start")]
    [HarmonyPostfix]
    private static void Nuligine_Start_Postfix(Nuligine __instance)
    {
        if (LegacyServerHeadlessBootstrap.IsOptimizationEnabled)
        {
            LegacyServerHeadlessBootstrap.ReleaseNuligineClientUiReferences(__instance);
            LegacyServerHeadlessBootstrap.Install(__instance.gameObject);
        }

        if (LegacyServerHeadlessBootstrap.IsCpuOptimizationEnabled)
        {
            LegacyServerHeadlessBootstrap.DisableClientRuntimeBehaviours(__instance.gameObject);
        }
    }
}
