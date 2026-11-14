using System;
using System.Collections.Generic;
using System.Xml;
using UnityEngine;

internal static class LegacyRaceMaps
{
    internal static Transform CreateRaceMap(LegacyEventsConfig.EventDef def, string savePostfix, int index)
    {
        if (def.points == null || def.points.Length < 2)
        {
            Debug.LogWarning("Skip race event '" + def.name + "': need at least 2 points");
            return null;
        }
        if (def.start == null || def.start.Length == 0)
        {
            Debug.LogWarning("Skip race event '" + def.name + "': need at least 1 start position");
            return null;
        }
        GameObject gameObject = new GameObject(LegacyEventMapHelpers.GetSafeName(def, savePostfix, index));
        gameObject.SetActive(false);
        if (!LegacyParkourObjects.TryAttach(gameObject, def))
        {
            UnityEngine.Object.Destroy(gameObject);
            return null;
        }
        RaceDirectionPoints raceDirectionPoints = gameObject.AddComponent<RaceDirectionPoints>();
        raceDirectionPoints.isSprint = def.isSprint;

        if (def.laps > 1 || def.minLaps > 1 || def.maxLaps > 1)
        {
            raceDirectionPoints.isSprint = false;
        }

        LegacyRaceDirectionMetadata.Get(raceDirectionPoints).onlineLapsCount = def.laps;
        LegacyRaceDirectionMetadata.Get(raceDirectionPoints).onlineMinLapsCount = def.minLaps;
        LegacyRaceDirectionMetadata.Get(raceDirectionPoints).onlineMaxLapsCount = def.maxLaps;

        raceDirectionPoints.useRespaum = def.useRespawn;
        raceDirectionPoints.dontUseOnline = false;
        gameObject.AddComponent<StartPoses>().items = LegacyEventMapHelpers.ToStartItems(def.start);
        UI_Event_Item_Info ui_Event_Item_Info = gameObject.AddComponent<UI_Event_Item_Info>();
        ui_Event_Item_Info.txt = LegacyEventMapHelpers.GetDisplayName(def, gameObject.name);
        ui_Event_Item_Info.WinCoins = ((def.reward > 0) ? def.reward : 1000);
        for (int i = 0; i < def.points.Length; i++)
        {
            LegacyRaceMaps.CreateRacePoint(gameObject.transform, def.points[i], i);
        }
        return gameObject.transform;
    }

    internal static void CreateRacePoint(Transform parent, LegacyEventsConfig.PointDef point, int index)
    {
        LegacyRaceMaps.CreateFallbackRacePoint(parent, point.ToVector3(), new Vector3(0f, point.yaw, 0f), point.radius, index);
    }

    internal static Transform CreateChildTransform(Transform parent, string name)
    {
        GameObject gameObject = new GameObject(name);
        gameObject.transform.SetParent(parent, false);
        return gameObject.transform;
    }

    internal static void SaveRuntimeRacePointsToXml(RaceDirectionPoints points, XmlElement root)
    {
        RaceDirection[] componentsInChildren = points.GetComponentsInChildren<RaceDirection>(true);
        if (componentsInChildren == null || componentsInChildren.Length == 0)
        {
            return;
        }
        XmlElement xmlElement = root.Add("modPoints");
        foreach (RaceDirection raceDirection in componentsInChildren)
        {
            XmlElement xmlElement2 = xmlElement.Add("p");
            xmlElement2.SetVec3("pos", raceDirection.transform.position);
            xmlElement2.SetVec3("rot", raceDirection.transform.rotation.eulerAngles);
            float num = LegacyRaceCheckpointTrigger.GetRadius(raceDirection);
            xmlElement2.SetFloat("radius", num);
        }
    }

    internal static void LoadRuntimeRacePointsFromXml(RaceDirectionPoints points, XmlElement root)
    {
        XmlElement canNull = root.GetCanNull("modPoints");
        if (canNull == null)
        {
            return;
        }
        int num = 0;
        for (XmlElement xmlElement = (XmlElement)canNull.FirstChild; xmlElement != null; xmlElement = (XmlElement)xmlElement.NextSibling)
        {
            Vector3 vec = xmlElement.GetVec3("pos");
            Vector3 vec2 = xmlElement.GetVec3("rot");
            float @float = xmlElement.GetFloat("radius", LegacyRaceCheckpointTrigger.DefaultRadius);
            LegacyRaceMaps.CreateRacePointInternal(points.transform, vec, vec2, @float, num);
            num++;
        }
    }

    internal static RaceDirection CreateFallbackRacePoint(Transform parent, Vector3 pos, Vector3 rot, float radius, int index)
    {
        GameObject gameObject = new GameObject("Point_" + index.ToString("00"));
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.position = pos;
        gameObject.transform.rotation = Quaternion.Euler(rot);
        gameObject.SetActive(false);
        RaceDirection raceDirection = gameObject.AddComponent<RaceDirection>();
        gameObject.AddComponent<MeshRenderer>().enabled = false;
        RaceSpawnPoint raceSpawnPoint = gameObject.AddComponent<RaceSpawnPoint>();
        raceDirection.ourPoint = raceSpawnPoint;
        raceSpawnPoint.thisDirection = raceDirection;
        LegacyRaceCheckpointTrigger.Configure(raceDirection, radius);
        raceDirection.nextPosMiniMapIcon = LegacyRaceMaps.CreateChildTransform(gameObject.transform, "NextPosMiniMapIcon");
        LegacyRaceMaps.SetupFallbackMiniMapIcon(raceDirection.nextPosMiniMapIcon.gameObject);
        raceDirection.lookAtCam = LegacyRaceMaps.CreateChildTransform(gameObject.transform, "LookAtCam");
        raceDirection.rotByDirection = LegacyRaceMaps.CreateChildTransform(gameObject.transform, "Direction");
        raceDirection.lookAt_Test = LegacyRaceMaps.CreateChildTransform(gameObject.transform, "LookAtTest");
        return raceDirection;
    }

    internal static RaceDirection CreateRacePointInternal(Transform parent, Vector3 pos, Vector3 rot, float radius, int index)
    {
        RaceDirection racePointTemplate = LegacyRaceMaps.GetRacePointTemplate();
        if (racePointTemplate)
        {
            RaceDirection raceDirection = LegacyRaceMaps.CreateRacePointFromTemplate(racePointTemplate, parent, pos, rot, radius, index);
            if (raceDirection)
            {
                return raceDirection;
            }
        }
        return LegacyRaceMaps.CreateFallbackRacePoint(parent, pos, rot, radius, index);
    }

    internal static RaceDirection GetRacePointTemplate()
    {
        if (LegacyRaceMaps.racePointTemplateSearchDone)
        {
            return LegacyRaceMaps.cachedRacePointTemplate;
        }
        LegacyRaceMaps.racePointTemplateSearchDone = true;
        RaceDirection raceDirection = LegacyRaceMaps.TryFindRacePointTemplateInRaceObjects();
        if (raceDirection)
        {
            LegacyRaceMaps.cachedRacePointTemplate = raceDirection;
            Debug.Log("Events: race checkpoint template found in RaceObjects: " + LegacyRaceMaps.GetTransformPath(raceDirection.transform));
            return LegacyRaceMaps.cachedRacePointTemplate;
        }
        RaceDirection[] array = Resources.FindObjectsOfTypeAll<RaceDirection>();
        RaceDirection raceDirection2 = null;
        int num = -1;
        foreach (RaceDirection raceDirection3 in array)
        {
            if (raceDirection3 && raceDirection3.gameObject)
            {
                string transformPath = LegacyRaceMaps.GetTransformPath(raceDirection3.transform);
                if (transformPath.IndexOf("ModEvents_RuntimeDatabase") == -1 && transformPath.IndexOf("Used RacePoints") == -1)
                {
                    int racePointTemplateScore = LegacyRaceMaps.GetRacePointTemplateScore(raceDirection3);
                    if (racePointTemplateScore > num)
                    {
                        num = racePointTemplateScore;
                        raceDirection2 = raceDirection3;
                    }
                }
            }
        }
        LegacyRaceMaps.cachedRacePointTemplate = raceDirection2;
        if (raceDirection2)
        {
            Debug.Log("Events: race checkpoint template found: " + LegacyRaceMaps.GetTransformPath(raceDirection2.transform) + ", score: " + num.ToString());
        }
        else
        {
            Debug.LogWarning("Events: race checkpoint template was not found.");
        }
        return LegacyRaceMaps.cachedRacePointTemplate;
    }

    internal static int GetRacePointTemplateScore(RaceDirection point)
    {
        if (!point)
        {
            return -1;
        }
        if (!point.ourPoint)
        {
            return -1;
        }
        int num = 0;
        if (point.GetComponent<Collider>())
        {
            num += 10;
        }
        if (point.GetComponentInChildren<MeshRenderer>(true))
        {
            num += 10;
        }
        if (point.nextPosMiniMapIcon)
        {
            num += 20;
            if (point.nextPosMiniMapIcon.GetComponentInChildren<MiniMap_Icon>(true))
            {
                num += 30;
            }
            if (point.nextPosMiniMapIcon.GetComponentInChildren<TargetPointer>(true))
            {
                num += 10;
            }
        }
        if (point.lookAtCam)
        {
            num += 5;
        }
        if (point.rotByDirection)
        {
            num += 5;
        }
        if (point.lookAt_Test)
        {
            num += 5;
        }
        return num;
    }

    internal static RaceDirection CreateRacePointFromTemplate(RaceDirection template, Transform parent, Vector3 pos, Vector3 rot, float radius, int index)
    {
        if (!template)
        {
            return null;
        }
        RaceDirection raceDirection = global::UnityEngine.Object.Instantiate<RaceDirection>(template);
        if (!raceDirection)
        {
            return null;
        }
        GameObject gameObject = raceDirection.gameObject;
        gameObject.name = "Point_" + index.ToString("00");
        gameObject.transform.SetParent(parent, false);
        gameObject.transform.position = pos;
        gameObject.transform.rotation = Quaternion.Euler(rot);
        gameObject.SetActive(false);
        RaceSpawnPoint raceSpawnPoint = raceDirection.ourPoint;
        if (!raceSpawnPoint)
        {
            raceSpawnPoint = gameObject.GetComponent<RaceSpawnPoint>();
        }
        if (!raceSpawnPoint)
        {
            raceSpawnPoint = gameObject.GetComponentInChildren<RaceSpawnPoint>(true);
        }
        if (!raceSpawnPoint)
        {
            raceSpawnPoint = gameObject.AddComponent<RaceSpawnPoint>();
        }
        raceDirection.ourPoint = raceSpawnPoint;
        raceSpawnPoint.thisDirection = raceDirection;
        LegacyRaceCheckpointTrigger.Configure(raceDirection, radius);
        if (!raceDirection.nextPosMiniMapIcon)
        {
            raceDirection.nextPosMiniMapIcon = LegacyRaceMaps.CreateChildTransform(gameObject.transform, "NextPosMiniMapIcon");
            LegacyRaceMaps.SetupFallbackMiniMapIcon(raceDirection.nextPosMiniMapIcon.gameObject);
        }
        if (!raceDirection.lookAtCam)
        {
            raceDirection.lookAtCam = LegacyRaceMaps.CreateChildTransform(gameObject.transform, "LookAtCam");
        }
        if (!raceDirection.rotByDirection)
        {
            raceDirection.rotByDirection = LegacyRaceMaps.CreateChildTransform(gameObject.transform, "Direction");
        }
        if (!raceDirection.lookAt_Test)
        {
            raceDirection.lookAt_Test = LegacyRaceMaps.CreateChildTransform(gameObject.transform, "LookAtTest");
        }
        return raceDirection;
    }

    internal static void SetupFallbackMiniMapIcon(GameObject gameObject)
    {
        if (!gameObject)
        {
            return;
        }
        MiniMap_Icon miniMap_Icon = gameObject.GetComponent<MiniMap_Icon>();
        if (!miniMap_Icon)
        {
            miniMap_Icon = gameObject.AddComponent<MiniMap_Icon>();
        }
        if (RaceSettings.me)
        {
            miniMap_Icon.spriteTexture = RaceSettings.me.miniMapIcon;
        }
        miniMap_Icon.color = new Color(0.2f, 0.8f, 1f, 1f);
        miniMap_Icon.iconScale = 1.2f;
        miniMap_Icon.opacity = 100;
        miniMap_Icon.order = 20;
        miniMap_Icon.dontUseShadow = true;
        TargetPointer targetPointer = gameObject.GetComponent<TargetPointer>();
        if (!targetPointer)
        {
            targetPointer = gameObject.AddComponent<TargetPointer>();
        }
        targetPointer.customTarget = gameObject.transform;
        targetPointer.imgColor = miniMap_Icon.color;
        targetPointer.linesColor = miniMap_Icon.color;
        targetPointer.mul_color_opacity = 0.8f;
        if (RaceSettings.me)
        {
            targetPointer.custom_sprite = RaceSettings.me.miniMapIcon;
        }
        miniMap_Icon.enableDisable = targetPointer;
    }

    internal static string GetTransformPath(Transform transform)
    {
        if (!transform)
        {
            return "null";
        }
        string text = transform.name;
        while (transform.parent)
        {
            transform = transform.parent;
            text = transform.name + "/" + text;
        }
        return text;
    }

    internal static RaceDirection TryFindRacePointTemplateInRaceObjects()
    {
        LinkResource[] array = Resources.LoadAll<LinkResource>("RaceObjects");
        RaceDirection raceDirection = null;
        int num = -1;
        foreach (LinkResource linkResource in array)
        {
            if (linkResource && linkResource.resource is GameObject)
            {
                foreach (RaceDirection raceDirection2 in (linkResource.resource as GameObject).GetComponentsInChildren<RaceDirection>(true))
                {
                    int racePointTemplateScore = LegacyRaceMaps.GetRacePointTemplateScore(raceDirection2);
                    if (racePointTemplateScore > num)
                    {
                        num = racePointTemplateScore;
                        raceDirection = raceDirection2;
                    }
                }
            }
        }
        if (raceDirection)
        {
            Debug.Log("Events: loaded RaceObjects resources: " + array.Length.ToString() + ", best checkpoint score: " + num.ToString());
        }
        else
        {
            Debug.LogWarning("Events: RaceObjects resources loaded, but checkpoint template was not found. Resources count: " + array.Length.ToString());
        }
        return raceDirection;
    }

    internal static RaceDirection SpawnDebugRaceCheckpoint(Vector3 pos, bool isInteractive, float yaw = 0f, float radius = LegacyRaceCheckpointTrigger.DefaultRadius)
    {
        if (NetManager.isServer)
        {
            LegacyRaceMaps.LogDebugCheckpointWarning("Debug checkpoint spawn is client-only. Ignored on server.");
            return null;
        }

        if (!LegacyRaceMaps.debugCheckpointRoot)
        {
            LegacyRaceMaps.debugCheckpointRoot = new GameObject("BigCityLegacy_DebugRaceCheckpoints");
            global::UnityEngine.Object.DontDestroyOnLoad(LegacyRaceMaps.debugCheckpointRoot);
        }

        if (!LegacyRaceMaps.cachedRacePointTemplate)
        {
            LegacyRaceMaps.racePointTemplateSearchDone = false;
        }

        RaceDirection template = LegacyRaceMaps.GetRacePointTemplate();

        if (!template)
        {
            LegacyRaceMaps.LogDebugCheckpointWarning("Debug checkpoint was not spawned: race checkpoint template was not found.");
            return null;
        }

        int index = LegacyRaceMaps.debugCheckpointIndex++;

        RaceDirection raceDirection = LegacyRaceMaps.CreateRacePointFromTemplate(
            template,
            LegacyRaceMaps.debugCheckpointRoot.transform,
            pos,
            new Vector3(0f, yaw, 0f),
            radius,
            index
        );

        if (!raceDirection)
        {
            LegacyRaceMaps.LogDebugCheckpointWarning("Debug checkpoint was not spawned: failed to instantiate checkpoint template.");
            return null;
        }

        GameObject gameObject = raceDirection.gameObject;

        gameObject.name = "DebugRaceCheckpoint_" + index.ToString("00");
        gameObject.transform.position = pos;
        gameObject.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        raceDirection.next = null;
        raceDirection.racePoints = null;
        raceDirection.takeCallBack = null;

        if (raceDirection.ourPoint)
        {
            raceDirection.ourPoint.enabled = false;
        }

        if (!isInteractive)
        {
            LegacyRaceMaps.DisableDebugCheckpointColliders(gameObject);
        }

        gameObject.SetActive(true);

        LegacyRaceMaps.ForceDebugCheckpointVisual(raceDirection, pos, yaw);

        LegacyRaceMaps.debugCheckpoints.Add(raceDirection);

        LegacyRaceMaps.LogDebugCheckpointInfo("Debug race checkpoint spawned at " + pos.ToString() + ", yaw: " + yaw.ToString());

        return raceDirection;
    }

    internal static void ForceDebugCheckpointVisual(RaceDirection raceDirection, Vector3 pos, float yaw)
    {
        if (!raceDirection)
        {
            return;
        }

        GameObject gameObject = raceDirection.gameObject;

        if (!raceDirection.nextPosMiniMapIcon)
        {
            raceDirection.nextPosMiniMapIcon = LegacyRaceMaps.CreateChildTransform(gameObject.transform, "NextPosMiniMapIcon");
        }

        raceDirection.nextPosMiniMapIcon.position = pos;
        raceDirection.nextPosMiniMapIcon.rotation = Quaternion.Euler(0f, yaw, 0f);
        raceDirection.nextPosMiniMapIcon.gameObject.SetActive(true);

        MiniMap_Icon miniMapIcon = raceDirection.nextPosMiniMapIcon.GetComponent<MiniMap_Icon>();

        if (miniMapIcon)
        {
            miniMapIcon.customTransfrom = raceDirection.transform;
            miniMapIcon.infoText = "Debug checkpoint";
            miniMapIcon.ShowTextOnMap = true;
            miniMapIcon.RestartIcon();
        }

        TargetPointer targetPointer = raceDirection.nextPosMiniMapIcon.GetComponent<TargetPointer>();

        if (targetPointer)
        {
            targetPointer.customTarget = raceDirection.transform;
            targetPointer.enabled = true;
        }
    }

    internal static void DisableDebugCheckpointColliders(GameObject gameObject)
    {
        if (!gameObject)
        {
            return;
        }

        Collider[] componentsInChildren = gameObject.GetComponentsInChildren<Collider>(true);

        for (int i = 0; i < componentsInChildren.Length; i++)
        {
            if (componentsInChildren[i])
            {
                componentsInChildren[i].enabled = false;
            }
        }
    }

    internal static void ClearDebugRaceCheckpoints()
    {
        if (LegacyRaceMaps.debugCheckpointRoot)
        {
            global::UnityEngine.Object.Destroy(LegacyRaceMaps.debugCheckpointRoot);
            LegacyRaceMaps.debugCheckpointRoot = null;
        }

        LegacyRaceMaps.debugCheckpoints.Clear();
        LegacyRaceMaps.debugCheckpointIndex = 0;
        LegacyRaceMaps.LogDebugCheckpointInfo("Debug race checkpoints cleared.");
    }

    internal static bool RemoveDebugRaceCheckpoint(Vector3 expectedPos)
    {
        const float warnDistance = 3f;

        if (!LegacyRaceMaps.debugCheckpointRoot)
        {
            LegacyRaceMaps.LogDebugCheckpointWarning("Debug checkpoint remove failed: debug checkpoint root was not found.");
            return false;
        }

        RaceDirection checkpoint = LegacyRaceMaps.PopLastDebugCheckpoint();

        if (!checkpoint)
        {
            LegacyRaceMaps.LogDebugCheckpointWarning("Debug checkpoint remove failed: no debug checkpoints found.");
            return false;
        }

        string checkpointName = checkpoint.gameObject.name;
        Vector3 checkpointPos = checkpoint.transform.position;
        float distance = Vector3.Distance(checkpointPos, expectedPos);

        if (distance > warnDistance)
        {
            LegacyRaceMaps.LogDebugCheckpointWarning(
                "Debug checkpoint remove position mismatch: removing last spawned checkpoint " +
                checkpointName +
                " at " + checkpointPos.ToString() +
                ", requested: " + expectedPos.ToString() +
                ", distance: " + distance.ToString("0.###")
            );
        }

        global::UnityEngine.Object.Destroy(checkpoint.gameObject);

        LegacyRaceMaps.LogDebugCheckpointInfo(
            "Debug race checkpoint removed: " + checkpointName +
            " at " + checkpointPos.ToString() +
            ", requested: " + expectedPos.ToString()
        );

        return true;
    }

    internal static RaceDirection PopLastDebugCheckpoint()
    {
        for (int i = LegacyRaceMaps.debugCheckpoints.Count - 1; i >= 0; i--)
        {
            RaceDirection checkpoint = LegacyRaceMaps.debugCheckpoints[i];
            LegacyRaceMaps.debugCheckpoints.RemoveAt(i);

            if (checkpoint)
            {
                return checkpoint;
            }
        }

        RaceDirection[] checkpoints = LegacyRaceMaps.debugCheckpointRoot
            ? LegacyRaceMaps.debugCheckpointRoot.GetComponentsInChildren<RaceDirection>(true)
            : null;

        if (checkpoints == null || checkpoints.Length == 0)
        {
            return null;
        }

        RaceDirection bestCheckpoint = null;
        int bestIndex = -1;

        for (int i = 0; i < checkpoints.Length; i++)
        {
            RaceDirection checkpoint = checkpoints[i];

            if (!checkpoint || !checkpoint.gameObject)
            {
                continue;
            }

            string name = checkpoint.gameObject.name;
            const string prefix = "DebugRaceCheckpoint_";

            int index = -1;

            if (name != null && name.StartsWith(prefix, StringComparison.Ordinal))
            {
                int.TryParse(name.Substring(prefix.Length), out index);
            }

            if (index >= bestIndex)
            {
                bestIndex = index;
                bestCheckpoint = checkpoint;
            }
        }

        return bestCheckpoint;
    }

    internal static void LogDebugCheckpointInfo(string message)
    {
        if (BigCityLegacyPlugin.Log != null)
        {
            BigCityLegacyPlugin.Log.LogInfo(message);
            return;
        }

        Debug.Log(message);
    }

    internal static void LogDebugCheckpointWarning(string message)
    {
        if (BigCityLegacyPlugin.Log != null)
        {
            BigCityLegacyPlugin.Log.LogWarning(message);
            return;
        }

        Debug.LogWarning(message);
    }

    private static RaceDirection cachedRacePointTemplate;

    private static bool racePointTemplateSearchDone;

    private static GameObject debugCheckpointRoot;

    private static readonly List<RaceDirection> debugCheckpoints = new List<RaceDirection>();

    private static int debugCheckpointIndex;
}
