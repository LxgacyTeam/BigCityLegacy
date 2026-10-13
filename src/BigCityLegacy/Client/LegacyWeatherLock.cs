using System;
using UnityEngine;

internal static class LegacyWeatherLock
{
    internal const string PlayerPrefsKey = "BigCityLegacy.WeatherLock.Enabled";

    internal static bool Bootstrapped;

    internal static bool IsEnabled
    {
        get { return PlayerPrefs.GetInt(PlayerPrefsKey, 0) != 0; }
    }

    internal static void EnsureDefault()
    {
        if (!PlayerPrefs.HasKey(PlayerPrefsKey))
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, 0);
            PlayerPrefs.Save();
        }
    }

    internal static void SetEnabled(bool enabled)
    {
        PlayerPrefs.SetInt(PlayerPrefsKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }
}