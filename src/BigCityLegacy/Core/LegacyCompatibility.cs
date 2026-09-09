using System;
using UnityEngine;

internal enum LegacyFeatureMode
{
    Full,
    Compatibility
}

internal static class LegacyCompatibility
{
    internal const string FullSupportedGameVersion = "9.4";

    internal static string CurrentGameVersion { get; private set; }
    internal static LegacyFeatureMode Mode { get; private set; }
    internal static bool IsFullMode { get { return Mode == LegacyFeatureMode.Full; } }
    internal static bool IsCompatibilityMode { get { return Mode == LegacyFeatureMode.Compatibility; } }

    internal static void DetectFromRuntime()
    {
        CurrentGameVersion = (Application.version ?? string.Empty).Trim();

        if (HasCommandLineArg("-forceFullMode"))
        {
            Mode = LegacyFeatureMode.Full;
            return;
        }

        if (HasCommandLineArg("-forceCompatMode"))
        {
            Mode = LegacyFeatureMode.Compatibility;
            return;
        }

        Mode = IsGameVersion94(CurrentGameVersion) ? LegacyFeatureMode.Full : LegacyFeatureMode.Compatibility;
    }

    private static bool IsGameVersion94(string version)
    {
        if (string.IsNullOrEmpty(version))
        {
            return false;
        }

        // Keep only 9.4 in full mode, but send everything older to the safe fallback.
        return version.Equals(FullSupportedGameVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasCommandLineArg(string name)
    {
        try
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch { }
        return false;
    }
}
