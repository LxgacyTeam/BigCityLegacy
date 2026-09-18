using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using HarmonyLib;
using Net.UI.MenuEsc;
using UnityEngine;
using UnityEngine.UI;

[HarmonyPatch]
internal static class NetUIPatches
{
    private static readonly Dictionary<NetUI_ClinetChoiseServer, LegacyDirectConnectGui> DirectGuis = new Dictionary<NetUI_ClinetChoiseServer, LegacyDirectConnectGui>();
    private static readonly Dictionary<NetPlayerUI, Text> TypingAdTexts = new Dictionary<NetPlayerUI, Text>();

    [HarmonyPatch(typeof(NetUI_ClinetChoiseServer), "Awake")]
    [HarmonyPrefix]
    private static void NetUI_Awake_Prefix()
    {
        LegacyCommandLine.UpdateFromArgs();
        LegacyMasterClient.LoadFromPrefsAndArgs();
    }

    [HarmonyPatch(typeof(NetUI_ClinetChoiseServer), "Awake")]
    [HarmonyPostfix]
    private static void NetUI_Awake_Postfix(NetUI_ClinetChoiseServer __instance)
    {
        if (__instance.onlineVersion)
        {
            __instance.onlineVersion.text = "Online Version: " + AlwaysOnline.onlineVersion.ToString();
        }
    }

    [HarmonyPatch(typeof(NetUI_ClinetChoiseServer), "OnEnable")]
    [HarmonyPostfix]
    private static void NetUI_OnEnable_Postfix(NetUI_ClinetChoiseServer __instance)
    {
        RegisterDirectGui(__instance);
        LegacyMasterClient.ResetListRequestTime();
    }

    [HarmonyPatch(typeof(NetUI_ClinetChoiseServer), "OnDisable")]
    [HarmonyPrefix]
    private static void NetUI_OnDisable_Prefix(NetUI_ClinetChoiseServer __instance)
    {
        UnregisterDirectGui(__instance);
    }

    [HarmonyPatch(typeof(NetUI_ClinetChoiseServer), "UpdateServerListReq")]
    [HarmonyPrefix]
    private static bool NetUI_UpdateServerListReq_Prefix(NetUI_ClinetChoiseServer __instance, ref Task __result)
    {
        __result = UpdateServerListReqReplacement(__instance);
        return false;
    }

    private static async Task UpdateServerListReqReplacement(NetUI_ClinetChoiseServer owner)
    {
        if (!owner || !owner.gameObject.activeInHierarchy)
        {
            return;
        }
        if (LegacyMasterClient.IsMasterListEnabled())
        {
            IPEndPoint endpoint = LegacyMasterClient.GetMasterEndPoint();
            MasterDebugLog("UpdateServerListReq: master enabled, endpoint=" + (endpoint != null ? endpoint.ToString() : "null") + ", selectedMode=" + owner.selectMode.ToString());
            if (endpoint != null && !RequestUDP.isInRequest(endpoint, "/match/list/", 2000f))
            {
                if (owner.updateList)
                {
                    owner.updateList.SetActive(true);
                }
                bend_MatchMaking.AnsverItem[] servers = await LegacyMasterClient.RequestServerList();
                if (owner.updateList)
                {
                    owner.updateList.SetActive(false);
                }
                if (servers != null)
                {
                    owner.lastData = servers;
                    owner.SetServersData(owner.lastData);
                }
            }
        }
        else if (!string.IsNullOrEmpty(LegacyCommandLine.ConnectIP) && LegacyCommandLine.ConnectPort > 0)
        {
            owner.lastData = new bend_MatchMaking.AnsverItem[]
            {
                new bend_MatchMaking.AnsverItem
                {
                    serverGUID = "direct",
                    ip = LegacyCommandLine.ConnectIP,
                    port = LegacyCommandLine.ConnectPort,
                    playerCount = 0,
                    connectsCount = 0,
                    playersMaxCount = (NetManager.maxPlayersAllowed > 0 ? NetManager.maxPlayersAllowed : 16),
                    connState = 0,
                    playModeGroup = owner.selectMode.ToString(),
                    playModeDesc = "DIRECT",
                    isCanConnect = true
                }
            };
            owner.SetServersData(owner.lastData);
        }
    }

    [HarmonyPatch(typeof(NetUI_ClinetChoiseServer), "SetServersData")]
    [HarmonyPrefix]
    private static bool NetUI_SetServersData_Prefix(NetUI_ClinetChoiseServer __instance, bend_MatchMaking.AnsverItem[] servers)
    {
        if (servers == null)
        {
            return false;
        }
        foreach (NetUI_ServerItem item in __instance.items)
        {
            item.markAsUnused = true;
        }
        foreach (Net.UI.MenuEsc.NetUI_OnlineModeGroup group in __instance.modeGroups)
        {
            group.tmpCountPlayers = group.tmpCountServers = 0;
        }
        int sort = 20;
        int displayed = 0;
        int totalPlayers = 0;
        string selectedMode = __instance.selectMode.ToString();
        foreach (bend_MatchMaking.AnsverItem server in servers)
        {
            server.playModeGroup = NormalizeModeGroup(server.playModeGroup);
            if (server.playModeGroup == selectedMode)
            {
                NetUI_ServerItem row = __instance.GetOrCreatItem(server.serverGUID);
                row.gameObject.SetActive(true);
                row.sortIdx = ++sort;
                row.serverGUID = server.serverGUID;
                row.SetIPAndPort(server.ip, server.port, ++displayed);
                row.SetData(server.playerCount, server.connectsCount, server.playersMaxCount, server.connState, server.lang, server.playModeDesc, server.isCanConnect);
            }
            Net.UI.MenuEsc.NetUI_OnlineModeGroup uiGroup = __instance.TryGetModeGroup(server.playModeGroup);
            if (uiGroup != null)
            {
                uiGroup.tmpCountServers++;
                uiGroup.tmpCountPlayers += server.playerCount;
            }
            totalPlayers += server.playerCount;
        }
        if (__instance.totalPlayersTxt)
        {
            __instance.totalPlayersTxt.text = totalPlayers.ToString().SeparateByStep(3U, " ", true);
        }
        foreach (Net.UI.MenuEsc.NetUI_OnlineModeGroup group in __instance.modeGroups)
        {
            group.UpPlayersAndServersCount();
        }
        foreach (NetUI_ServerItem item in __instance.items)
        {
            item.gameObject.SetActive(!item.markAsUnused);
        }
        __instance.UpdateSort();
        return false;
    }

    [HarmonyPatch(typeof(NetUI_ClinetChoiseServer), "Async_ConnectToServer")]
    [HarmonyPrefix]
    private static bool NetUI_AsyncConnect_Prefix(NetUI_ClinetChoiseServer __instance, string serverGUID, ref Task __result)
    {
        __result = AsyncConnectReplacement(__instance, serverGUID);
        return false;
    }

    private static async Task AsyncConnectReplacement(NetUI_ClinetChoiseServer owner, string serverGUID)
    {
        if (LegacyMasterClient.IsMasterListEnabled())
        {
            owner.connectingToServer.gameObject.SetActive(true);
            float pressTime = nTime.Ms;
            bend_MatchMaking.ConnectToGameServerAnswer answer = await LegacyMasterClient.RequestConnect(serverGUID, owner.selectMode.ToString());
            float elapsed = nTime.Ms - pressTime;
            if (elapsed < 1000f)
            {
                await Task.Delay((int)(1000f - elapsed));
            }
            owner.connectingToServer.gameObject.SetActive(false);
            if (answer != null && !string.IsNullOrEmpty(answer.ip) && answer.port > 0)
            {
                NetManager.me.ConnectTo(answer.ip, answer.port);
            }
        }
        else
        {
            owner.connectingToServer.gameObject.SetActive(true);
            if (!string.IsNullOrEmpty(LegacyCommandLine.ConnectIP) && LegacyCommandLine.ConnectPort > 0)
            {
                NetManager.me.ConnectTo(LegacyCommandLine.ConnectIP, LegacyCommandLine.ConnectPort);
            }
            await Task.Delay(300);
            owner.connectingToServer.gameObject.SetActive(false);
        }
    }

    private static void RegisterDirectGui(NetUI_ClinetChoiseServer owner)
    {
        if (!owner)
        {
            return;
        }
        LegacyDirectConnectGui gui;
        if (!DirectGuis.TryGetValue(owner, out gui) || gui == null)
        {
            gui = new LegacyDirectConnectGui(owner);
            DirectGuis[owner] = gui;
        }
        OnGuiGlobal.items.Remove(gui);
        OnGuiGlobal.items.Add(gui);
    }

    private static void UnregisterDirectGui(NetUI_ClinetChoiseServer owner)
    {
        LegacyDirectConnectGui gui;
        if (owner && DirectGuis.TryGetValue(owner, out gui))
        {
            OnGuiGlobal.items.Remove(gui);
            DirectGuis.Remove(owner);
        }
    }

    internal static void RefreshServerListNow(NetUI_ClinetChoiseServer owner)
    {
        LegacyMasterClient.ResetListRequestTime();
        owner.UpdateServerListReq();
    }

    private static bool IsMasterDebugEnabled()
    {
        return ArgParcer.GetArgValue("-masterDebug").isExists || ArgParcer.GetArgValue("-debugMaster").isExists;
    }

    private static void MasterDebugLog(string text)
    {
        if (IsMasterDebugEnabled())
        {
            Debug.Log("[NetUI_Master] " + text);
        }
    }

    internal static string NormalizeModeGroup(string mode)
    {
        if (string.IsNullOrEmpty(mode))
        {
            return Net.UI.MenuEsc.NetUI_OnlineModeGroup.ModeGroup.FreeRoam.ToString();
        }
        if (string.Equals(mode, "None", StringComparison.OrdinalIgnoreCase) || string.Equals(mode, "FreeRoam", StringComparison.OrdinalIgnoreCase))
        {
            return Net.UI.MenuEsc.NetUI_OnlineModeGroup.ModeGroup.FreeRoam.ToString();
        }
        if (string.Equals(mode, "RP", StringComparison.OrdinalIgnoreCase))
        {
            return Net.UI.MenuEsc.NetUI_OnlineModeGroup.ModeGroup.RP.ToString();
        }
        if (mode.IndexOf("Race", StringComparison.OrdinalIgnoreCase) != -1 || mode.IndexOf("Park", StringComparison.OrdinalIgnoreCase) != -1)
        {
            return Net.UI.MenuEsc.NetUI_OnlineModeGroup.ModeGroup.Race.ToString();
        }
        if (mode.IndexOf("CS", StringComparison.OrdinalIgnoreCase) != -1 || mode.IndexOf("Sur", StringComparison.OrdinalIgnoreCase) != -1)
        {
            return Net.UI.MenuEsc.NetUI_OnlineModeGroup.ModeGroup.CS.ToString();
        }
        return mode;
    }

    [HarmonyPatch(typeof(NetPlayerUI), "UpControlData")]
    [HarmonyPostfix]
    private static void NetPlayerUI_UpControlData_Postfix(NetPlayerUI __instance, bool viewAds, float dist_for_player)
    {
        if (!viewAds || dist_for_player >= 50f) return;
        if (!__instance || !__instance.watchAd) return;
        Text t;
        if (!TypingAdTexts.TryGetValue(__instance, out t))
        {
            t = __instance.watchAd.GetComponentInChildren<Text>(true);
            TypingAdTexts[__instance] = t;
        }
        if (t && t.text != "...")
        {
            t.text = "...";
        }
    }
}
