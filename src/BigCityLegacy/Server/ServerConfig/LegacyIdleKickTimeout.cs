using System;
using System.Globalization;
using UnityEngine;

internal static class LegacyIdleKickTimeout
{
    private const float DefaultIdleTimeoutMs = 120000f;
    private const float DefaultDisconnectedTimeoutMs = 30000f;
    private const string ArgName = "-idleKickTimeout";
    private const string DisconnectedArgName = "-disconnectedKickTimeout";

    private static bool initialized;
    private static bool disconnectedInitialized;

    internal static bool HasOverride { get; private set; }
    internal static bool TimeoutEnabled { get; private set; } = true;
    internal static float IdleTimeoutMs { get; private set; } = DefaultIdleTimeoutMs;
    internal static float DisconnectedTimeoutMs { get; private set; } = DefaultDisconnectedTimeoutMs;
    internal static bool DisconnectedTimeoutEnabled { get; private set; } = true;

    internal static void UpdateFromArgs(string[] args)
    {
        UpdateDisconnectedTimeout(args);
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
                LegacyServerConsole.LogAfterConsoleInit(
                    "Invalid " + ArgName + " value '" + value + "'. Using vanilla inactivity timeout " + FormatMs(DefaultIdleTimeoutMs) + ".",
                    LogType.Warning
                );
            }
        }

        if (!initialized || previousHasOverride != HasOverride || previousEnabled != TimeoutEnabled || Math.Abs(previousTimeout - IdleTimeoutMs) > 0.5f)
        {
            initialized = true;
            if (!HasOverride)
            {
                LegacyServerConsole.LogAfterConsoleInit(
                    "Idle kick timeout: vanilla " + FormatMs(DefaultIdleTimeoutMs) + ".",
                    LogType.Log
                );
            }
            else if (!TimeoutEnabled)
            {
                LegacyServerConsole.LogAfterConsoleInit(
                    "Idle kick inactivity timeout: disabled by " + ArgName + ".",
                    LogType.Log
                );
            }
            else
            {
                LegacyServerConsole.LogAfterConsoleInit(
                    "Idle kick inactivity timeout: " + FormatMs(IdleTimeoutMs) + ".",
                    LogType.Log
                );
            }
        }
    }

    private static void UpdateDisconnectedTimeout(string[] args)
    {
        string value = LegacyCommandLine.GetArgValue(args, DisconnectedArgName);
        float timeout = DefaultDisconnectedTimeoutMs;
        if (!string.IsNullOrEmpty(value) && !TryParseTimeoutMs(value, out timeout))
        {
            timeout = DefaultDisconnectedTimeoutMs;
            LegacyServerConsole.LogAfterConsoleInit("Invalid " + DisconnectedArgName + " value '" + value +
                "'. Using disconnected session timeout " + FormatMs(timeout) + ".", LogType.Warning);
        }
        bool enabled = timeout > 0f;
        timeout = enabled ? Mathf.Max(1000f, timeout) : 0f;
        bool changed = !disconnectedInitialized || enabled != DisconnectedTimeoutEnabled ||
            Math.Abs(timeout - DisconnectedTimeoutMs) > 0.5f;
        DisconnectedTimeoutEnabled = enabled;
        DisconnectedTimeoutMs = timeout;
        disconnectedInitialized = true;
        if (changed) LegacyServerConsole.LogAfterConsoleInit("Disconnected session timeout: " +
            (enabled ? FormatMs(timeout) : "disabled by " + DisconnectedArgName) + ".", LogType.Log);
    }

    internal static void CheckIdleDisconnect(NetInputControl input)
    {
        if (input == null)
        {
            return;
        }

        float now = input.autoDisconnectChechTime = nTime.GameThisFrame;
        // Transport timeout/removal and NetConnect.OnDestroy set netConnect to
        // null and record removeNetConnectTime. A detached session must not be
        // retained by a long/disabled AFK timer, stale movement or reconnect asks.
        if (!input.netConnect)
        {
            if (DisconnectedTimeoutEnabled && now - input.removeNetConnectTime >= DisconnectedTimeoutMs)
                QueueKick(input);
            return;
        }
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

        if (shouldKick) QueueKick(input);
    }

    private static void QueueKick(NetInputControl input)
    {
        if (NetManager.me != null && !NetManager.me.needKikByNotMoveTime.Contains(input))
            NetManager.me.needKikByNotMoveTime.Add(input);
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
        return !float.IsNaN(timeoutMs) && !float.IsInfinity(timeoutMs);
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
