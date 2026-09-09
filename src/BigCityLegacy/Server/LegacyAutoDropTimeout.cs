using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

internal static class LegacyAutoDropTimeout
{
    private const float DefaultTimeoutMs = 4000f;
    private const string ArgName = "-autoDropTimeout";

    private static bool initialized;

    internal static bool HasOverride { get; private set; }
    internal static bool TimeoutEnabled { get; private set; } = true;
    internal static float TimeoutMs { get; private set; } = DefaultTimeoutMs;

    internal static void UpdateFromArgs(string[] args)
    {
        string value = LegacyCommandLine.GetArgValue(args, ArgName);
        bool hasOverride = !string.IsNullOrEmpty(value);

        bool previousHasOverride = HasOverride;
        bool previousEnabled = TimeoutEnabled;
        float previousTimeout = TimeoutMs;

        if (!hasOverride)
        {
            HasOverride = false;
            TimeoutEnabled = true;
            TimeoutMs = DefaultTimeoutMs;
        }
        else
        {
            HasOverride = true;

            float timeoutMs;
            if (TryParseTimeoutMs(value, out timeoutMs))
            {
                if (timeoutMs <= 0f)
                {
                    TimeoutEnabled = false;
                    TimeoutMs = 0f;
                }
                else
                {
                    TimeoutEnabled = true;
                    TimeoutMs = Mathf.Max(100f, timeoutMs);
                }
            }
            else
            {
                TimeoutEnabled = true;
                TimeoutMs = DefaultTimeoutMs;
                Debug.LogWarning("[BigCityLegacy] Invalid " + ArgName + " value '" + value + "'. Using vanilla timeout " + FormatMs(DefaultTimeoutMs) + ".");
            }
        }

        if (!initialized || previousHasOverride != HasOverride || previousEnabled != TimeoutEnabled || Math.Abs(previousTimeout - TimeoutMs) > 0.5f)
        {
            initialized = true;
            if (!HasOverride)
            {
                Debug.Log("[BigCityLegacy] Auto drop timeout: vanilla " + FormatMs(DefaultTimeoutMs) + ".");
            }
            else if (!TimeoutEnabled)
            {
                Debug.Log("[BigCityLegacy] Auto drop timeout: disabled by " + ArgName + ".");
            }
            else
            {
                Debug.Log("[BigCityLegacy] Auto drop timeout: " + FormatMs(TimeoutMs) + ".");
            }
        }
    }

    internal static bool IsNeedDropByTimeout(NetConnect connect)
    {
        if (!TimeoutEnabled || connect == null || connect.conn == null)
        {
            return false;
        }

        byte channelId;
        int incomingPacketCount = NetworkTransport.GetIncomingPacketCount(connect.conn.hostId, connect.conn.connectionId, out channelId);
        if (connect.LastIncomingPacketCount != incomingPacketCount)
        {
            connect.LastIncomingPacketCount = incomingPacketCount;
            connect.lastReciveMsgTime = nTime.GameThisFrame;
        }

        return nTime.GameThisFrame - connect.lastReciveMsgTime > TimeoutMs;
    }

    private static bool TryParseTimeoutMs(string raw, out float timeoutMs)
    {
        timeoutMs = DefaultTimeoutMs;
        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }

        string value = LegacyCommandLine.StripQuotes(raw).Trim().ToLowerInvariant();
        float multiplier = 1000f;

        if (value.EndsWith("ms", StringComparison.Ordinal))
        {
            multiplier = 1f;
            value = value.Substring(0, value.Length - 2).Trim();
        }
        else if (value.EndsWith("sec", StringComparison.Ordinal))
        {
            multiplier = 1000f;
            value = value.Substring(0, value.Length - 3).Trim();
        }
        else if (value.EndsWith("s", StringComparison.Ordinal))
        {
            multiplier = 1000f;
            value = value.Substring(0, value.Length - 1).Trim();
        }

        float numeric;
        if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out numeric))
        {
            return false;
        }

        timeoutMs = numeric * multiplier;
        return true;
    }

    private static string FormatMs(float ms)
    {
        if (Math.Abs(ms % 1000f) < 0.001f)
        {
            return (ms / 1000f).ToString("0", CultureInfo.InvariantCulture) + "s";
        }
        return (ms / 1000f).ToString("0.###", CultureInfo.InvariantCulture) + "s";
    }
}
