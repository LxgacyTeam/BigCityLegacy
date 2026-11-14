using System;
using System.Collections.Generic;
using UnityEngine;

internal static class LegacyParkourObjects
{
    internal static bool TryAttach(GameObject track, LegacyEventsConfig.EventDef definition)
    {
        var objects = definition.objects;
        if (objects == null || objects.Length == 0) return true;

        var items = new List<PrefabLinks.Item>(objects.Length);
        foreach (var entry in objects)
        {
            if (entry == null || !Guid.TryParseExact(entry.guid, "N", out Guid assetId))
            {
                Debug.LogWarning("[BCL Events] Invalid obstacle GUID on " + definition.name);
                return false;
            }

            string guid = assetId.ToString("N");
            var link = Resources.Load<LinkResource>("RaceObjects/" + guid);
            var prefab = link ? link.resource as GameObject : null;
            if (!prefab)
            {
                Debug.LogWarning("[BCL Events] Obstacle " + guid + " missing on " + definition.name);
                return false;
            }

            var position = entry.ToVector3();
            var rotation = new Vector3(entry.pitch, entry.yaw, entry.roll);
            var scale = new Vector3(entry.scaleX, entry.scaleY, entry.scaleZ);
            if (!IsFinite(position) || !IsFinite(rotation) || !IsFinite(scale) || scale.x <= 0f || scale.y <= 0f || scale.z <= 0f)
            {
                Debug.LogWarning("[BCL Events] Invalid obstacle transform on " + definition.name + ": " + guid);
                return false;
            }

            items.Add(new PrefabLinks.Item { from_guid = guid, from_go = prefab, pos = position, rot = rotation, scale = scale, index = items.Count });
        }

        track.AddComponent<PrefabLinks>().items.AddRange(items);
        return true;
    }

    internal static void SpawnOnServer(NetRace race, RaceDirectionPoints track)
    {
        if (!track.GetComponent<LegacyRaceDirectionMetadata>()) return;
        var source = track.GetComponent<PrefabLinks>();
        if (!source || source.items.Count == 0) return;

        var root = new GameObject("Race obstacles");
        root.SetActive(false);
        root.transform.SetParent(race.transform, true);
        root.AddComponent<PrefabLinks>().items.AddRange(source.items);
        root.SetActive(true);
        Debug.Log("[BCL Events] Spawned " + root.transform.childCount + " race obstacles on " + track.name);
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.sqrMagnitude) && !float.IsInfinity(value.sqrMagnitude);
    }
}
