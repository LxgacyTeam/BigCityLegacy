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
        LegacyServerHeadlessBootstrap.Install(__instance.gameObject);

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
        LegacyServerConsole.LogAfterConsoleInit(
            "Game Server started on port " + __instance.networkPort.ToString() + ".",
            LogType.Log
        );
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

// adds console flag to disable creating m_log files
[HarmonyPatch(typeof(NetManagerTools), "OnEnable")]
public static class NetManagerTools_OnEnable_Patch
{
    static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);

        MethodInfo getIsEditor = AccessTools.PropertyGetter(
            typeof(Application),
            nameof(Application.isEditor));

        MethodInfo hasArg = AccessTools.Method(
            typeof(LegacyCommandLine),
            nameof(LegacyCommandLine.HasArg),
            new[] { typeof(string) });

        for (int i = 0; i < codes.Count; i++)
        {
            yield return codes[i];

            if (codes[i].Calls(getIsEditor))
            {
                yield return new CodeInstruction(
                    OpCodes.Ldstr,
                    "-noLogFile");

                yield return new CodeInstruction(
                    OpCodes.Call,
                    hasArg);

                yield return new CodeInstruction(
                    OpCodes.Or);
            }
        }
    }
}
