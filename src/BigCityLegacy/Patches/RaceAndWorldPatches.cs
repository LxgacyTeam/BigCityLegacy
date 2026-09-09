using System;
using System.Xml;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

[HarmonyPatch]
internal static class RaceAndWorldPatches
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
                LegacyEventsConfig.SaveRuntimeRacePointsToXml(__instance, root);
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
            LegacyEventsConfig.LoadRuntimeRacePointsFromXml(__instance, root);
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
        __instance.NetworkeventDataXml = points.SaveRace();
        __instance.startPoses = points.GetComponent<StartPoses>();
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
            __instance.state = Net_BaseEvent.CurState.Race;
            if (__instance.racePoints)
            {
                __instance.racePoints.ActivateFirstPoint();
            }
        });
        return false;
    }

    // Net_CS.OnStartServer selects one configured map through GetRandomMap().
    // Keep that exact transform so arena cleanup never tries to infer a map from
    // display names, object names or unrelated event instances.
    [HarmonyPatch(typeof(Net_CS), "GetRandomMap")]
    [HarmonyPostfix]
    private static void Net_CS_GetRandomMap_Postfix(Net_CS __instance, ref Transform __result)
    {
        LegacyServerDropCleanup.RememberSelectedCsMap(__instance, __result);
    }

    // In the original Net_CS implementation, Spawn_AfterLobby is where
    // StateChanget invokes SpawnEvent(). Clear leftover Loot immediately before
    // that original round-start work runs, for both CS and CS_Sur.
    [HarmonyPatch(typeof(Net_CS), "StateChanget")]
    [HarmonyPrefix]
    private static void Net_CS_StateChanget_Prefix(Net_CS __instance, Net_BaseEvent.CurState was)
    {
        LegacyServerDropCleanup.HandleCsStateChanged(__instance, was);
    }

    [HarmonyPatch(typeof(SpawnPos), "GetSpawnPos")]
    [HarmonyPrefix]
    private static bool SpawnPos_GetSpawnPos_Prefix(ref Vector3 __result)
    {
        if (TryEnsureNetSpawns())
        {
            return true;
        }
        __result = GetFallbackSpawnPos();
        Debug.LogWarning("SpawnPos.GetSpawnPos fallback: " + __result.ToString());
        return false;
    }

    internal static bool TryEnsureNetSpawns()
    {
        if (HasReadyNetSpawns())
        {
            return true;
        }
        int count = 0;
        for (int i = 0; i < SpawnPos.instances.Count; i++)
        {
            SpawnPos spawn = SpawnPos.instances[i];
            if (spawn != null && spawn.type == SpawnPos.Type.NetZone && spawn.transform.childCount > 0)
            {
                count++;
            }
        }
        if (count <= 0)
        {
            return false;
        }
        SpawnPos.InitSpawnZoneName();
        return HasReadyNetSpawns();
    }

    internal static bool HasReadyNetSpawns()
    {
        return SpawnPos.spawnPoses != null && SpawnPos.spawnPoses.Length != 0;
    }

    internal static Vector3 GetFallbackSpawnPos()
    {
        for (int i = 0; i < SpawnPos.instances.Count; i++)
        {
            if (SpawnPos.instances[i].type == SpawnPos.Type.NetPos)
            {
                return SpawnPos.instances[i].transform.position;
            }
        }
        for (int i = 0; i < SpawnPos.instances.Count; i++)
        {
            if (SpawnPos.instances[i].type == SpawnPos.Type.Only_Player1)
            {
                return SpawnPos.instances[i].transform.position;
            }
        }
        return Vector3.zero;
    }
}
