using System;
using System.Xml;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

[HarmonyPatch]
internal static class WorldSpawnPatches
{
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
