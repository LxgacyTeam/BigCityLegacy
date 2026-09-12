using System;
using System.Globalization;
using UnityEngine;

internal static class LegacyIdleKickTimeout
{
    private const float DefaultIdleTimeoutMs = 120000f;
    private const float DefaultDetachedInputExtraMs = 40000f;
    private const string ArgName = "-idleKickTimeout";

    private static bool initialized;

    internal static bool HasOverride { get; private set; }
    internal static bool TimeoutEnabled { get; private set; } = true;
    internal static float IdleTimeoutMs { get; private set; } = DefaultIdleTimeoutMs;

    internal static void UpdateFromArgs(string[] args)
    {
        string value = LegacyCommandLine.GetArgValue(args, ArgName);
        bool hasOverride = !string.IsNullOrEmpty(value);

        bool previousHasOverride = HasOverride;
        bool previousEnabled = TimeoutEnabled;
        float previousTimeout = IdleTimeoutMs;

        if (!hasOverride)
        {
            HasOverride = false;
            TimeoutEnabled = true;
            IdleTimeoutMs = DefaultIdleTimeoutMs;
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
                    IdleTimeoutMs = 0f;
                }
                else
                {
                    TimeoutEnabled = true;
                    IdleTimeoutMs = Mathf.Max(1000f, timeoutMs);
                }
            }
            else
            {
                TimeoutEnabled = true;
                IdleTimeoutMs = DefaultIdleTimeoutMs;
                Debug.LogWarning("[BigCityLegacy] Invalid " + ArgName + " value '" + value + "'. Using vanilla inactivity timeout " + FormatMs(DefaultIdleTimeoutMs) + ".");
            }
        }

        if (!initialized || previousHasOverride != HasOverride || previousEnabled != TimeoutEnabled || Math.Abs(previousTimeout - IdleTimeoutMs) > 0.5f)
        {
            initialized = true;
            if (!HasOverride)
            {
                Debug.Log("[BigCityLegacy] Idle kick timeout: vanilla " + FormatMs(DefaultIdleTimeoutMs) + ".");
            }
            else if (!TimeoutEnabled)
            {
                Debug.Log("[BigCityLegacy] Idle kick inactivity timeout: disabled by " + ArgName + ".");
            }
            else
            {
                Debug.Log("[BigCityLegacy] Idle kick inactivity timeout: " + FormatMs(IdleTimeoutMs) + ".");
            }
        }
    }

    internal static void CheckIdleDisconnect(NetInputControl input)
    {
        if (input == null)
        {
            return;
        }

        float now = input.autoDisconnectChechTime = nTime.GameThisFrame;
        if (!TimeoutEnabled)
        {
            return;
        }

        float timeoutMs = IdleTimeoutMs;
        bool shouldKick = true;

        if (now - input.lastTimeRecivePresses < timeoutMs)
        {
            shouldKick = false;
        }
        else if (input.onServerNetPlayer != null && input.onServerNetPlayer.smoothSync != null &&
                 now - input.onServerNetPlayer.smoothSync.LastTimeMoved < timeoutMs)
        {
            shouldKick = false;
        }
        else
        {
            Control parentControl = input.control;
            if (parentControl)
            {
                parentControl = parentControl.parent_control;
            }

            if (parentControl && parentControl.net_control && parentControl.net_control.smoothSync != null &&
                now - parentControl.net_control.smoothSync.LastTimeMoved < timeoutMs)
            {
                shouldKick = false;
            }
        }

        if (!shouldKick && !input.netConnect)
        {
            float detachedTimeoutMs = GetDetachedInputTimeoutMs();
            if (now - input.removeNetConnectTime > timeoutMs && now - input.lastTimeAskThisConnectID > 10000f)
            {
                shouldKick = true;
            }
            else if (now - input.removeNetConnectTime > detachedTimeoutMs)
            {
                shouldKick = true;
            }
        }

        if (shouldKick && NetManager.me != null)
        {
            NetManager.me.needKikByNotMoveTime.Add(input);
        }
    }

    private static float GetDetachedInputTimeoutMs()
    {
        if (!TimeoutEnabled)
        {
            return float.PositiveInfinity;
        }

        return IdleTimeoutMs + DefaultDetachedInputExtraMs;
    }

    private static bool TryParseTimeoutMs(string raw, out float timeoutMs)
    {
        timeoutMs = DefaultIdleTimeoutMs;
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
