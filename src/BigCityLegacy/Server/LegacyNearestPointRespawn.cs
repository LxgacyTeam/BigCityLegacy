using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

internal static class LegacyNearestPointRespawn
{
    private const string ConfigArg = "-respawnPoints";
    private const string ConfigFileName = "RespawnPoints.json";

    private static bool wasTried;
    private static bool isReady;
    private static string loadedConfigPath;
    private static RespawnPoint[] points = new RespawnPoint[0];

    internal static bool IsReady
    {
        get { return isReady && points != null && points.Length > 0; }
    }

    internal static int Count
    {
        get { return points != null ? points.Length : 0; }
    }

    internal static string LoadedConfigPath
    {
        get { return loadedConfigPath; }
    }

    internal static bool Reload()
    {
        wasTried = false;
        isReady = false;
        points = new RespawnPoint[0];
        return TryLoad();
    }

    internal static bool TryLoad()
    {
        if (!NetManagerTools.isCommandLineArgHaveServerStr())
        {
            return false;
        }

        if (LegacyCommandLine.HasArg("-useVanillaRespawn"))
        {
            Debug.LogWarning("Respawn points are disabled. Vanilla spawn points will be used.");
            return false;
        }

        if (wasTried)
        {
            return IsReady;
        }

        wasTried = true;
        isReady = false;
        points = new RespawnPoint[0];

        string jsonPath = LegacyHelpers.GetJsonPath(ConfigArg, ConfigFileName);
        loadedConfigPath = jsonPath;

        if (!File.Exists(jsonPath))
        {
            Debug.LogWarning("Respawn points config not found: " + jsonPath + ". Vanilla spawn points will be used.");
            return false;
        }

        try
        {
            string json = File.ReadAllText(jsonPath);
            RespawnPointDTO[] configPoints = ReadPoints(json, jsonPath);
            if (configPoints == null || configPoints.Length == 0)
            {
                Debug.LogWarning("Respawn points config is empty: " + jsonPath + ". Vanilla spawn points will be used.");
                return false;
            }

            points = new RespawnPoint[configPoints.Length];
            for (int i = 0; i < configPoints.Length; i++)
            {
                RespawnPointDTO point = configPoints[i];
                points[i] = new RespawnPoint
                {
                    id = point.id,
                    position = new Vector3(point.x, point.y, point.z)
                };
            }

            isReady = points.Length > 0;
            Debug.Log("Respawn points config loaded: " + jsonPath + ", count: " + points.Length.ToString());
            return isReady;
        }
        catch (Exception ex)
        {
            points = new RespawnPoint[0];
            isReady = false;
            Debug.LogError("Failed to load respawn points config: " + jsonPath + "\n" + ex.ToString());
            return false;
        }
    }

    internal static bool TryGetNearestRespawnPoint(Vector3 currentPlayerPos, out Vector3 respawnPoint)
    {
        respawnPoint = Vector3.zero;

        if (!IsReady)
        {
            return false;
        }

        RespawnPoint nearest = points[0];
        float minDistanceSqr = (currentPlayerPos - nearest.position).sqrMagnitude;

        for (int i = 1; i < points.Length; i++)
        {
            float distanceSqr = (currentPlayerPos - points[i].position).sqrMagnitude;
            if (distanceSqr < minDistanceSqr)
            {
                minDistanceSqr = distanceSqr;
                nearest = points[i];
            }
        }

        respawnPoint = nearest.position;
        return true;
    }


    private static RespawnPointDTO[] ReadPoints(string json, string configName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new RespawnPointDTO[0];
        }

        try
        {
            RespawnPointDTO[] rawArray = JsonConvert.DeserializeObject<RespawnPointDTO[]>(json);
            if (rawArray != null)
            {
                return rawArray;
            }
        }
        catch
        {
             Debug.LogWarning("Respawn points parser failed for " + configName);
        }

        return new RespawnPointDTO[0];
    }

    private struct RespawnPoint
    {
        public int id;
        public Vector3 position;
    }

    [Serializable]
    private sealed class RespawnPointDTO
    {
        public int id;
        public float x;
        public float y;
        public float z;
    }

    [Serializable]
    private sealed class RespawnPointsConfig
    {
        public RespawnPointDTO[] points;
    }
}
