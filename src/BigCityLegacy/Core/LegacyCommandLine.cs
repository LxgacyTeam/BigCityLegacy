using System;
using System.Linq;
using UnityEngine;

internal static class LegacyCommandLine
{
    internal static bool AutoConnectFromArgs;
    internal static string ConnectIP;
    internal static int ConnectPort;
    internal static string BindIP;
    internal static bool BindToSpecificIP;
    internal static bool ChatEventsEnabled = true;

    internal static void UpdateFromArgs()
    {
        string[] args = Environment.GetCommandLineArgs();
        ConnectIP = GetPrefixedArgValue(args, "-connectIP:") ?? GetPrefixedArgValue(args, "-connectIP=") ?? GetArgValue("-connectIP") ?? ConnectIP;
        int port;
        string portText = GetPrefixedArgValue(args, "-connectPort:") ?? GetPrefixedArgValue(args, "-connectPort=") ?? GetArgValue("-connectPort");
        if (!string.IsNullOrEmpty(portText) && int.TryParse(portText, out port) && port > 0)
        {
            ConnectPort = port;
        }

        BindIP = GetPrefixedArgValue(args, "-bindIP:") ?? GetPrefixedArgValue(args, "-bindIP=") ?? GetArgValue("-bindIP") ?? GetPrefixedArgValue(args, "-ip:") ?? GetArgValue("-ip") ?? BindIP;
        BindToSpecificIP = !string.IsNullOrEmpty(BindIP) || HasArg("-bindToIP") || HasArg("-bindSpecificIP");
        AutoConnectFromArgs = !string.IsNullOrEmpty(ConnectIP) && ConnectPort > 0;
        ChatEventsEnabled = !HasArg("-noChatEvents");

        LegacyServerLimits.UpdateFromArgs(args);
        LegacyAutoDropTimeout.UpdateFromArgs(args);

        if (AutoConnectFromArgs)
        {
            Debug.Log("[BigCityLegacy] direct connect from args: " + ConnectIP + ":" + ConnectPort.ToString());
        }
        if (BindToSpecificIP)
        {
            Debug.Log("[BigCityLegacy] server bind IP from args: " + BindIP);
        }
    }

    internal static bool HasArg(string arg)
    {
        return Environment.GetCommandLineArgs().Any(t => string.Equals(StripQuotes(t), arg, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool HasServerArg()
    {
        return Environment.GetCommandLineArgs().Any(t => t.IndexOf("bend_GameServer", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    internal static bool HasBatchMode()
    {
        return Application.isBatchMode || HasArg("-batchmode");
    }

    internal static bool HasNoGraphics()
    {
        return HasArg("-nographics");
    }

    internal static bool HasHideWindow()
    {
        return HasArg("-hideWindow") || HasArg("-hideBatchWindow");
    }

    internal static bool HasNoUpdateCheck()
    {
        return HasArg("-noUpdCheck") || HasArg("-noUpdateCheck");
    }

    internal static string GetArgValue(string optionName)
    {
        return GetArgValue(Environment.GetCommandLineArgs(), optionName);
    }

    internal static string GetArgValue(string[] args, string optionName)
    {
        if (args == null || string.IsNullOrEmpty(optionName))
        {
            return null;
        }

        string colonPrefix = optionName + ":";
        string equalPrefix = optionName + "=";

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i] ?? string.Empty;

            if (arg.StartsWith(colonPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return StripQuotes(arg.Substring(colonPrefix.Length).Trim());
            }

            if (arg.StartsWith(equalPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return StripQuotes(arg.Substring(equalPrefix.Length).Trim());
            }

            if (string.Equals(arg, optionName, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return StripQuotes((args[i + 1] ?? string.Empty).Trim());
            }
        }

        return null;
    }

    internal static string StripQuotes(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        value = value.Trim();

        bool changed = true;
        while (changed && value.Length >= 2)
        {
            changed = false;

            char first = value[0];
            char last = value[value.Length - 1];

            if ((first == '"' && last == '"') ||
                (first == '\'' && last == '\'') ||
                (first == '“' && last == '”') ||
                (first == '«' && last == '»'))
            {
                value = value.Substring(1, value.Length - 2).Trim();
                changed = true;
            }
        }

        return value;
    }

    private static string GetPrefixedArgValue(string[] args, string prefix)
    {
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i] ?? string.Empty;
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return StripQuotes(arg.Substring(prefix.Length).Trim());
            }
        }
        return null;
    }
}
