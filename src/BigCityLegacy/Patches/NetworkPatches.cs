using System;
using System.Collections.Generic;
using System.Net;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

[HarmonyPatch]
internal static class NetworkPatches
{
    [HarmonyPatch(typeof(NetManagerTools), "UpdateCommandLines")]
    [HarmonyPostfix]
    private static void NetManagerTools_UpdateCommandLines_Postfix()
    {
        LegacyMasterServer.ChkEnt();
        LegacyCommandLine.UpdateFromArgs();
    }

    [HarmonyPatch(typeof(NetManager), "Start")]
    [HarmonyPrefix]
    private static void NetManager_Start_Prefix()
    {
        NetManagerTools.UpdateCommandLines();
        LegacyCommandLine.UpdateFromArgs();
    }

    [HarmonyPatch(typeof(NetManager), "RunServer")]
    [HarmonyPrefix]
    private static bool NetManager_RunServer_Prefix(NetManager __instance)
    {
        Debug.Log("[BigCityLegacy] NetManager.RunServer replacement active");
        __instance.StopHostIfNeed();
        SpawnPos.InitSpawnZoneName();
        NetManagerTools.UpdateCommandLines();
        LegacyCommandLine.UpdateFromArgs();
        NetManager.isServerWithGraphics = !NetManagerTools.isCommandLineArgBatchmode() && !LegacyCommandLine.HasNoGraphics();
        Debug.Log("Server graphics mode: " + NetManager.isServerWithGraphics.ToString());

        __instance.networkPort = NetManager.usePort;
        __instance.maxConnections = NetManager.maxPlayersAllowed + 5;
        if (LegacyCommandLine.BindToSpecificIP && !string.IsNullOrEmpty(LegacyCommandLine.BindIP))
        {
            __instance.serverBindToIP = true;
            __instance.serverBindAddress = LegacyCommandLine.BindIP;
            __instance.networkAddress = LegacyCommandLine.BindIP;
            Debug.Log("Server bind to specific IP: " + LegacyCommandLine.BindIP + ":" + __instance.networkPort.ToString());
        }
        else
        {
            __instance.serverBindToIP = false;
            Debug.Log("Server bind to ANY IP on port: " + __instance.networkPort.ToString());
        }

        NetManager.RemoveSingleData();
        // Ban enforcement must be ready before UNet starts accepting clients.
        LegacyBanList.TryLoad();
        LegacyServerAdminService.ResetForServerStart();
        LegacyServerDropCleanup.ResetForServerStart();
        Debug.Log("DIRECT CONNECT -> " + LegacyCommandLine.ConnectIP + ":" + LegacyCommandLine.ConnectPort.ToString());
        __instance.StartServer();

        if (!Application.isEditor || NetManager.me.enableMapInEditor)
        {
            Nuligine.me.map.gameObject.SetActive(true);
        }
        if (NetManagerTools.isCommandLineArgBatchmode() || LegacyCommandLine.HasNoGraphics())
        {
            if (Cam.me && Cam.me.unityCam)
            {
                Cam.me.unityCam.enabled = false;
            }
        }
        NetManager.me.gameObject.GetOrAddComponent<NetManagerTools>();
        LegacyServerConsole.CreateIfNeeded();
        LegacyNearestPointRespawn.TryLoad();
        LegacyMasterServer.AttachIfNeeded(NetManager.me.gameObject);
        if (!LegacyEventsConfig.TryLoadAndBuild() && __instance.loadEventsMap)
        {
            __instance.loadEventsMap.SetActive(true);
        }
        return false;
    }

    [HarmonyPatch(typeof(NetManager), "Update")]
    [HarmonyPostfix]
    private static void NetManager_Update_Postfix(NetManager __instance)
    {
        LegacyHelpers.EnsureServerNetChat(__instance);
        LegacyHelpers.EnsureServerUnetSettings(__instance);
        LegacyServerAdminService.Update();
    }

    [HarmonyPatch(typeof(NetManager), "NowStoped")]
    [HarmonyPostfix]
    private static void NetManager_NowStoped_Postfix()
    {
    }

    [HarmonyPatch(typeof(Nuligine), "Start")]
    [HarmonyPostfix]
    private static void Nuligine_Start_Postfix(Nuligine __instance)
    {
        if (LegacyServerHeadlessBootstrap.IsHeadlessServer)
        {
            LegacyServerHeadlessBootstrap.Install(__instance.gameObject);
        }
    }

    [HarmonyPatch(typeof(GameUI), "Init")]
    [HarmonyPrefix]
    private static bool GameUI_Init_Prefix(GameUI __instance)
    {
        if (!LegacyServerHeadlessBootstrap.IsHeadlessServer)
        {
            return true;
        }
        GameUI.me = __instance;
        if (__instance.ui)
        {
            UnityEngine.Object.Destroy(__instance.ui.gameObject);
        }
        __instance.ui = UnityEngine.Object.Instantiate<GameObject>(__instance.SplitScreenUI, __instance.transform);
        __instance.ui.name = "Canvas";
        __instance.ui.transform.ResetGlobal();
        __instance.img = __instance.ui.transform.GetChild(0).GetComponent<RawImage>();
        __instance.fadeUI = __instance.img.gameObject.GetOrAddComponent<FadeUI>();
        __instance.img.enabled = false;
        return false;
    }

    [HarmonyPatch(typeof(GroundLoader), "CreateLod")]
    [HarmonyPrefix]
    private static bool GroundLoader_CreateLod_Prefix()
    {
        return !LegacyServerHeadlessBootstrap.IsHeadlessServer;
    }

    [HarmonyPatch(typeof(NetChat), "Awake")]
    [HarmonyPrefix]
    private static bool NetChat_Awake_Prefix(NetChat __instance)
    {
        if (!IsHeadlessServerChat())
        {
            return true;
        }
        NetChat.me = __instance;
        __instance.isNeedFill = 0;
        return false;
    }

    [HarmonyPatch(typeof(NetChat), "LateUpdateManual")]
    [HarmonyPrefix]
    private static bool NetChat_LateUpdateManual_Prefix(NetChat __instance)
    {
        if (IsHeadlessServerChat())
        {
            __instance.RemoveUnusedLiveTimeMessages();
            return false;
        }
        if (!__instance.inputField || !__instance.inputImage || !__instance.root)
        {
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(NetChat), "UpTxts")]
    [HarmonyPrefix]
    private static bool NetChat_UpTxts_Prefix()
    {
        return !NetManager.isServer || NetManager.isServerWithGraphics;
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

    //[HarmonyPatch(typeof(RequestUDP), "get_ip_MainServer")]
    //public static class Patch_ip_MainServer
    //{
    //    static void Postfix(ref IPEndPoint __result)
    //    {
    //        __result = new IPEndPoint(IPAddress.Parse("127.0.0.1"), __result.Port);
    //    }
    //}

    private static bool IsHeadlessServerChat()
    {
        return NetManagerTools.isCommandLineArgHaveServerStr() && (NetManagerTools.isCommandLineArgBatchmode() || LegacyCommandLine.HasNoGraphics());
    }
}

// disables 'Connection Error, post: /mirror/hand_shake' log spamming
[HarmonyPatch]
internal static class AsyncRequestString_Patch
{
    static MethodBase TargetMethod()
    {
        var original = AccessTools.Method(
            typeof(RequestUDP),
            "AsyncRequestString",
            new[]
            {
                typeof(object),
                typeof(IPEndPoint),
                typeof(string),
                typeof(string),
                typeof(float)
            });

        if (original == null)
            throw new Exception("AsyncRequestString not found");

        var attr = original.GetCustomAttribute<AsyncStateMachineAttribute>();

        if (attr == null)
            throw new Exception("AsyncRequestString has no async state machine");

        var moveNext = AccessTools.Method(
            attr.StateMachineType,
            "MoveNext"
        );

        if (moveNext == null)
            throw new Exception("MoveNext not found");

        return moveNext;
    }

    static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();

        var isErrorField = AccessTools.Field(
            typeof(RequestUDP),
            "isError"
        );

        if (isErrorField == null)
            throw new Exception("RequestUDP.isError field not found");

        for (int i = 0; i < codes.Count; i++)
        {
            if (codes[i].LoadsField(isErrorField))
            {
                var replacement = new CodeInstruction(OpCodes.Ldc_I4_0);

                replacement.labels.AddRange(codes[i].labels);
                replacement.blocks.AddRange(codes[i].blocks);

                codes[i] = replacement;

                return codes;
            }
        }

        throw new Exception(
            "Could not find RequestUDP.isError check in AsyncRequestString"
        );
    }
}
