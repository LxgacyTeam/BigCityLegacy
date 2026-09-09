using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

[HarmonyPatch]
internal static class ChatEventsPatches
{
    [HarmonyPatch(typeof(NetInputControl), "Cmd_InitNikName")]
    [HarmonyPostfix]
    private static void NetInputControl_CmdInitNikName_Postfix(NetInputControl __instance, string reasone)
    {
        SendChatMessage(__instance, reasone);
    }

    [HarmonyPatch(typeof(NetConnect), "InitInputControl")]
    [HarmonyPostfix]
    private static void NetConnect_InitInputControl_Postfix(NetConnect __instance)
    {
        LegacyChatEventQueue.Enqueue(__instance.netInput, " + connected");
    }

    [HarmonyPatch(typeof(NetManager), "OnServerDisconnect")]
    [HarmonyPrefix]
    private static void NetManager_OnServerDisconnect_Prefix(NetworkConnection conn)
    {
        if (conn == null || conn.clientOwnedObjects == null)
        {
            return;
        }

        foreach (NetworkInstanceId netId in new List<NetworkInstanceId>(conn.clientOwnedObjects))
        {
            GameObject go = NetworkServer.FindLocalObject(netId);
            if (go == null)
            {
                continue;
            }

            NetConnect netConnect = go.GetComponent<NetConnect>();
            if (netConnect == null || netConnect.netInput == null)
            {
                continue;
            }

            SendChatMessage(netConnect.netInput, "- disconnected");
        }
    }

    internal static void SendChatMessage(NetInputControl netInput, string message)
    {
        if (netInput == null || !LegacyCommandLine.ChatEventsEnabled)
        {
            return;
        }

        LegacyHelpers.EnsureServerNetChat(NetManager.me);
        if (NetChat.me)
        {
            NetChat.me.AddMessage(netInput, message, false, 0f);
        }
    }
}
