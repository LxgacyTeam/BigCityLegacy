using System;
using Newtonsoft.Json;
using UnityEngine;

internal static class LegacyJsonConfig
{
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        MissingMemberHandling = MissingMemberHandling.Ignore,
        NullValueHandling = NullValueHandling.Include,
        ObjectCreationHandling = ObjectCreationHandling.Replace
    };

    internal static T FromJson<T>(string json, string configName) where T : class, new()
    {
        if (string.IsNullOrEmpty(json))
        {
            return new T();
        }

        try
        {
            T value = JsonConvert.DeserializeObject<T>(json, Settings);
            if (value != null)
            {
                return value;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BigCityLegacy] Newtonsoft config parser failed for " + configName + ": " + ex.Message + ". Falling back to Unity JsonUtility.");
        }

        try
        {
            T value = JsonUtility.FromJson<T>(json);
            return value ?? new T();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BigCityLegacy] Unity JsonUtility config parser failed for " + configName + ": " + ex.Message);
            return new T();
        }
    }
}
