using System;
using System.Xml;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

[HarmonyPatch]
internal static class RacePatches
{
    [HarmonyPatch(typeof(RaceDirectionPoints), "SaveRace")]
    [HarmonyPostfix]
    private static void RaceDirectionPoints_SaveRace_Postfix(RaceDirectionPoints __instance, ref string __result)
    {
        try
        {
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(__result);
            XmlElement root = (XmlElement)doc.FirstChild;
            if (root.GetCanNull("modPoints") == null)
            {
                LegacyRaceMaps.SaveRuntimeRacePointsToXml(__instance, root);
            }
            __result = doc.OuterXml;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BigCityLegacy] SaveRace postfix failed: " + ex.Message);
        }
    }

    [HarmonyPatch(typeof(RaceDirectionPoints), "LoadXml")]
    [HarmonyPostfix]
    private static void RaceDirectionPoints_LoadXml_Postfix(RaceDirectionPoints __instance, string xmlData)
    {
        try
        {
            if (string.IsNullOrEmpty(xmlData))
            {
                return;
            }
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(xmlData);
            XmlElement root = (XmlElement)doc.FirstChild;
            LegacyRaceMaps.LoadRuntimeRacePointsFromXml(__instance, root);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BigCityLegacy] LoadXml postfix failed: " + ex.Message);
        }
    }

    [HarmonyPatch(typeof(NetRace), "CloneTrassaOnServer")]
    [HarmonyPrefix]
    private static bool NetRace_CloneTrassaOnServer_Prefix(NetRace __instance)
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[Server] function 'System.Void NetRace::CloneTrassaOnServer()' called on client");
            return false;
        }
        if (!NetManager.isServer)
        {
            return false;
        }
        RaceDirectionPoints points = __instance.eventCopyFrom.GetComponent<RaceDirectionPoints>();
        if (!points)
        {
            Debug.LogError("Not found RaceDirectionPoints in event, name " + __instance.eventCopyFrom.name, __instance.eventCopyFrom);
            return false;
        }
        int onlineLaps = LegacyRaceDirectionMetadata.GetOnlineLapsCount(points);
        if (onlineLaps > 0)
        {
            __instance.NetworklapsCount = onlineLaps;
        }
        else
        {
            __instance.NetworklapsCount = points.isSprint ? 1 : __instance.GetRndLapsCount();
        }
        var raceDocument = new XmlDocument();
        raceDocument.LoadXml(points.SaveRace());
        raceDocument.DocumentElement.SetAttribute("allowNitroPurchase", LegacyRaceCarRules.AllowNitroPurchase ? "true" : "false");
        __instance.NetworkeventDataXml = raceDocument.OuterXml;
        __instance.startPoses = points.GetComponent<StartPoses>();
        var info = points.GetComponent<UI_Event_Item_Info>();
        __instance.NetworkmapName = info && !string.IsNullOrWhiteSpace(info.txt) ? info.txt : points.name;
        return false;
    }

    [HarmonyPatch(typeof(NetRace), "BeginRaceOnClient")]
    [HarmonyPrefix]
    private static bool NetRace_BeginRaceOnClient_Prefix(NetRace __instance)
    {
        if (!NetworkClient.active)
        {
            Debug.LogWarning("[Client] function 'System.Void NetRace::BeginRaceOnClient()' called on server");
            return false;
        }
        GameObject go = new GameObject();
        __instance.racePoints = go.AddComponent<RaceDirectionPoints>();
        var runtime = go.AddComponent<LegacyOnlineRacePoints>();
        runtime.Owner = __instance;
        var raceDocument = new XmlDocument();
        raceDocument.LoadXml(__instance.eventDataXml);
        runtime.AllowNitroPurchase = raceDocument.DocumentElement.GetAttribute("allowNitroPurchase") != "false";
        __instance.racePoints.LoadXml(__instance.eventDataXml);
        go.name = " Used RacePoints, for NetRace# " + __instance.index;
        __instance.racePoints.Init(__instance.lapsCount);
        __instance.racePoints.LapsCompliteCallBack = delegate
        {
            if (__instance.raceUI && __instance.racePoints)
            {
                int lap = __instance.racePoints.lapsComplite + 1;
                if (lap > __instance.lapsCount)
                {
                    lap = __instance.lapsCount;
                }
                __instance.raceUI.SetLaps(lap, __instance.lapsCount);
            }
        };
        __instance.racePoints.FinishCallBack = new RaceDirectionPoints.CallBack(__instance.PlayerFinished_CallBack);
        __instance.raceUI = UnityEngine.Object.Instantiate<RaceUI>(RaceSettings.me.raceUi, GameUI.me.rectUI);
        __instance.raceUI.Init(InputControl.firstUser);
        __instance.raceUI.SetLaps(1, __instance.lapsCount);
        __instance.raceUI.SetPos(__instance.players.Count, __instance.players.Count);
        RaceSettings.me.PlayStart(delegate { }, delegate(InputControl input)
        {
            if (__instance.state == Net_BaseEvent.CurState.Race && __instance.racePoints)
            {
                __instance.racePoints.ActivateFirstPoint();
            }
        });
        return false;
    }

}
