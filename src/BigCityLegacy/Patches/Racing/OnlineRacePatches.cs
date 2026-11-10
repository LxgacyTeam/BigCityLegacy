using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
internal static class OnlineRacePatches
{
    [HarmonyPatch(typeof(NetRace), "PlayerTryChangeCar")]
    [HarmonyPrefix]
    private static void SelectStockCar(ref string xmlCar)
    {
        xmlCar = LegacyRaceCarRules.StockWithPaint(xmlCar);
    }

    [HarmonyPatch(typeof(NetRace), "AddCarToUser")]
    [HarmonyPrefix]
    private static void SpawnStockCar(NetRace.Racer racer)
    {
        racer.data.carXml = LegacyRaceCarRules.StockWithPaint(racer.data.carXml);
    }

    [HarmonyPatch(typeof(NetControl), "SetXmlDataServer")]
    [HarmonyPrefix]
    private static void KeepRaceCarStock(NetControl __instance, ref string setXmlData)
    {
        if (!NetManager.isServer) return;
        foreach (var current in Net_BaseEvent.instances)
        {
            var race = current as NetRace;
            if (!race || race.state == Net_BaseEvent.CurState.Complite) continue;
            foreach (var player in race.players)
                if (player is NetRace.Racer racer && racer.takeCar == __instance)
                {
                    setXmlData = LegacyRaceCarRules.StockWithPaint(setXmlData);
                    return;
                }
        }
    }

    [HarmonyPatch(typeof(NetRace), "StateChanget")]
    [HarmonyPostfix]
    private static void StartAtServerTime(NetRace __instance)
    {
        if (__instance.state == Net_BaseEvent.CurState.Race && __instance.racePoints && !__instance.racePoints.isFinish)
            __instance.racePoints.ActivateFirstPoint();
    }

    [HarmonyPatch(typeof(NetRace), "UpdateClient")]
    [HarmonyPrefix]
    private static void HoldCar(NetRace __instance)
    {
        if (__instance.state != Net_BaseEvent.CurState.BeginRace &&
            __instance.state != Net_BaseEvent.CurState.Spawn_AfterLobby) return;
        var user = InputControl.firstUser;
        var car = user ? user.currentOrParent as CarControl : null;
        if (car && car.body)
        {
            car.body.velocity = Vector3.zero;
            car.body.angularVelocity = Vector3.zero;
        }
    }

    [HarmonyPatch(typeof(NetRace), "Finish")]
    [HarmonyPrefix]
    private static bool RejectEarlyFinish(NetRace __instance, int finishTime)
    {
        return __instance.state == Net_BaseEvent.CurState.Race && finishTime >= __instance.beginRaceTime &&
            nTime.NetworkTimeCached >= __instance.beginRaceTime;
    }

    [HarmonyPatch(typeof(NetRace), "SetPlayerDist2Finish")]
    [HarmonyPrefix]
    private static bool UpdateProgress(NetRace __instance, NetInputControl netInput, float distToFinish)
    {
        if (!NetManager.isServer || __instance.state != Net_BaseEvent.CurState.Race ||
            float.IsNaN(distToFinish) || float.IsInfinity(distToFinish) || distToFinish < 0) return false;
        var racer = __instance.GetPlayerByInput(netInput) as NetRace.Racer;
        if (racer == null) return false;
        racer.distToFinish = distToFinish;
        racer.wasMinDist2Finish = Mathf.Min(distToFinish, racer.wasMinDist2Finish);
        __instance.needSortRacers = true;
        return false;
    }

    [HarmonyPatch(typeof(Race_CarRespaum), "RespaumForRadeDirectional")]
    [HarmonyPrefix]
    private static bool RespawnOnOwnCourse(CarControl carControl, ref bool __result)
    {
        var race = Net_BaseEvent.curUserInRaceInst as NetRace;
        if (!race || !race.racePoints || !race.racePoints.GetComponent<LegacyOnlineRacePoints>()) return true;
        __result = true;
        if (race.state != Net_BaseEvent.CurState.Race || !carControl) return false;
        var point = race.racePoints.lastSpawnPoint;
        var position = point ? point.transform.position : race.holdPosRot.pos;
        var rotation = point ? point.spawnRot : race.holdPosRot.rot;
        carControl.ResetBodyForces();
        carControl.Align(position, rotation, 2f, true);
        carControl.WakeUpBody();
        return false;
    }

    [HarmonyPatch(typeof(RaceDirectionPoints), "Init")]
    [HarmonyPrefix]
    private static void ResetLinks(RaceDirectionPoints __instance)
    {
        if (!__instance.GetComponent<LegacyOnlineRacePoints>()) return;
        foreach (var point in __instance.GetComponentsInChildren<RaceDirection>(true)) point.next = null;
    }

    [HarmonyPatch(typeof(RaceDirectionPoints), "Init")]
    [HarmonyPostfix]
    private static void InitializePoints(RaceDirectionPoints __instance)
    {
        if (!__instance.GetComponent<LegacyOnlineRacePoints>()) return;
        var all = __instance.GetComponentsInChildren<RaceDirection>(true);
        float distance = 0;
        for (int i = 0; i < all.Length; i++)
        {
            var point = all[i];
            point.racePoints = __instance;
            LegacyRaceCheckpointTrigger.Configure(point, LegacyRaceCheckpointTrigger.GetRadius(point));
            point.SetNext(i + 1 < all.Length ? all[i + 1] : null);
            if (i > 0) distance += Vector3.Distance(all[i - 1].transform.position, point.transform.position);
            point.distToStart = distance;
            LegacyRaceCheckpointVisual.Apply(point);
        }
        foreach (var point in all) point.distToFinish = distance - point.distToStart;
        __instance.distWithLaps = distance;
    }

    [HarmonyPatch(typeof(RaceDirection), "TakeIt")]
    [HarmonyPrefix]
    private static bool TakeCheckpoint(RaceDirection __instance)
    {
        var runtime = LegacyOnlineRacePoints.Find(__instance);
        if (!runtime) return true;
        if (!runtime.CanTake(__instance)) return false;
        if (__instance.ourPoint) __instance.ourPoint.TakeIt(true);
        __instance.gameObject.SetActive(false);
        __instance.takeCallBack?.Invoke(__instance);
        if (__instance.next) __instance.next.gameObject.SetActive(true);
        if (__instance.TakeParticles && CachParticles.me)
            CachParticles.me.PlayAll(__instance.transform, __instance.TakeParticles.gameObject);
        return false;
    }

    [HarmonyPatch(typeof(RaceDirection), "OnTriggerEnter")]
    [HarmonyPrefix]
    private static bool EnterCheckpoint(RaceDirection __instance, Collider coll)
    {
        if (!LegacyOnlineRacePoints.Find(__instance)) return true;
        __instance.TriggerEnter(coll);
        return false;
    }

    [HarmonyPatch(typeof(RaceDirection), "OnEnable")]
    [HarmonyPrefix]
    private static bool EnableCheckpoint(RaceDirection __instance)
    {
        if (!LegacyOnlineRacePoints.Find(__instance)) return true;
        if (__instance.nextPosMiniMapIcon)
        {
            __instance.nextPosMiniMapIcon.gameObject.SetActive(__instance.next);
            if (__instance.next)
                __instance.nextPosMiniMapIcon.SetPositionAndRotation(__instance.next.transform.position, __instance.next.transform.rotation);
        }
        if (__instance.racePoints) __instance.racePoints.curPoint = __instance;
        return false;
    }

    [HarmonyPatch(typeof(RaceDirection), "UpdateDirection")]
    [HarmonyPrefix]
    private static bool UpdateDirection(RaceDirection __instance)
    {
        if (!LegacyOnlineRacePoints.Find(__instance)) return true;
        if (__instance.lookAtCam) __instance.lookAtCam.LookAt(hCamera.firstCamPos);
        if (!__instance.rotByDirection) return false;
        __instance.rotByDirection.gameObject.SetActive(__instance.next);
        if (!__instance.next) return false;
        var camera = hCamera.firstCamPos - __instance.transform.position;
        var direction = __instance.transform.position - __instance.next.transform.position;
        camera.y = direction.y = 0;
        float angle = Vector3.SignedAngle(camera, direction, Vector3.up);
        __instance.rotByDirection.localRotation = Quaternion.Euler(0, 0, angle);
        return false;
    }
}
