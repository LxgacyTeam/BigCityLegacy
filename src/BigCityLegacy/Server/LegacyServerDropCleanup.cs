using System;
using System.Collections.Generic;
using UnityEngine;

internal static class LegacyServerDropCleanup
{
    private const float MinimumZoneRadius = 25f;

    private static readonly Dictionary<int, LegacyEventDropZone> zonesByEventId =
        new Dictionary<int, LegacyEventDropZone>();

    private static readonly HashSet<int> clearedEventIds = new HashSet<int>();

    internal static void ResetForServerStart()
    {
        zonesByEventId.Clear();
        clearedEventIds.Clear();
    }

    internal static int ClearAllDrops(out int failed)
    {
        return ClearDropsInternal(null, 0f, out failed);
    }

    internal static int ClearDropsInSphere(Vector3 center, float radius, out int failed)
    {
        return ClearDropsInternal(center, Mathf.Max(MinimumZoneRadius, radius), out failed);
    }

    internal static void RememberSelectedCsMap(Net_CS currentEvent, Transform selectedMap)
    {
        if (!NetManager.isServer || !currentEvent || !selectedMap)
        {
            return;
        }

        LegacyEventDropZone zone = selectedMap.GetComponent<LegacyEventDropZone>();
        if (zone == null)
        {
            zone = selectedMap.GetComponentInChildren<LegacyEventDropZone>(true);
        }

        int eventId = currentEvent.GetInstanceID();
        if (zone != null && zone.IsConfigured)
        {
            zonesByEventId[eventId] = zone;
            return;
        }

        zonesByEventId.Remove(eventId);
        Debug.LogWarning(
            "[Server] CS drop cleanup: selected map '" + selectedMap.name +
            "' has no configured LegacyEventDropZone."
        );
    }

    internal static void HandleCsStateChanged(Net_CS currentEvent, Net_BaseEvent.CurState previousState)
    {
        if (!NetManager.isServer || !currentEvent)
        {
            return;
        }

        int eventId = currentEvent.GetInstanceID();

        if (currentEvent.state == Net_BaseEvent.CurState.Complite)
        {
            zonesByEventId.Remove(eventId);
            clearedEventIds.Remove(eventId);
            return;
        }

        if (currentEvent.state != Net_BaseEvent.CurState.Spawn_AfterLobby)
        {
            return;
        }

        if (!clearedEventIds.Add(eventId))
        {
            return;
        }

        LegacyEventDropZone zone;
        if (!zonesByEventId.TryGetValue(eventId, out zone) || zone == null || !zone.IsConfigured)
        {
            Debug.LogWarning(
                "[Server] CS drop cleanup skipped: no arena zone was registered for event '" +
                currentEvent.name + "'."
            );
            return;
        }

        int failed;
        int removed = ClearDropsInSphere(zone.Center, zone.Radius, out failed);
        Debug.Log(
            "[Server] CS drop cleanup: mode=" + zone.Mode +
            ", state=" + currentEvent.state.ToString() +
            ", center=" + zone.Center.ToString("F1") +
            ", radius=" + zone.Radius.ToString("F1") +
            ", removed=" + removed.ToString() +
            ", failed=" + failed.ToString() + "."
        );
    }

    private static int ClearDropsInternal(Vector3? center, float radius, out int failed)
    {
        failed = 0;

        if (!NetManager.isServer)
        {
            return 0;
        }

        Loot[] allLoot = Resources.FindObjectsOfTypeAll<Loot>();
        List<Loot> remove = new List<Loot>();
        Transform lootRoot = Created.me != null ? Created.me.Loots : null;

        for (int i = 0; i < allLoot.Length; i++)
        {
            Loot loot = allLoot[i];
            if (!IsRuntimeLoot(loot, lootRoot))
            {
                continue;
            }

            if (center.HasValue &&
                (loot.transform.position - center.Value).sqrMagnitude > radius * radius)
            {
                continue;
            }

            remove.Add(loot);
        }

        int removed = 0;
        for (int i = 0; i < remove.Count; i++)
        {
            Loot loot = remove[i];
            try
            {
                UnityEngine.Object.Destroy(loot.gameObject);
                removed++;
            }
            catch (Exception ex)
            {
                failed++;
                Debug.LogWarning(
                    "[Server] Loot cleanup failed for '" + loot.name + "': " + ex.Message
                );
            }
        }

        return removed;
    }

    private static bool IsRuntimeLoot(Loot loot, Transform lootRoot)
    {
        if (!loot || !loot.gameObject || !loot.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (lootRoot == null || loot.transform.parent != lootRoot)
        {
            return false;
        }

        return true;
    }
}

internal sealed class LegacyEventDropZone : MonoBehaviour
{
    public string mode;
    public Vector3 center;
    public float radius;
    public bool isConfigured;

    internal string Mode { get { return mode ?? string.Empty; } }
    internal Vector3 Center { get { return center; } }
    internal float Radius { get { return radius; } }
    internal bool IsConfigured { get { return isConfigured; } }

    internal void Configure(string mode, Vector3[] positions, float configuredRadius)
    {
        this.mode = mode ?? string.Empty;
        center = Vector3.zero;
        radius = 0f;
        isConfigured = false;

        if (positions == null || positions.Length == 0)
        {
            return;
        }

        for (int i = 0; i < positions.Length; i++)
        {
            center += positions[i];
        }
        center /= positions.Length;

        float furthest = 0f;
        for (int i = 0; i < positions.Length; i++)
        {
            furthest = Mathf.Max(furthest, Vector3.Distance(center, positions[i]));
        }

        radius = configuredRadius > 0f
            ? configuredRadius
            : Mathf.Max(100f, furthest + 80f);
        isConfigured = true;
    }
}
