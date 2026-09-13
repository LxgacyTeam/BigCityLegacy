using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

internal static class LegacyCommandLine
{
    internal static bool AutoConnectFromArgs;
    internal static string ConnectIP;
    internal static int ConnectPort;
    internal static string BindIP;
    internal static bool BindToSpecificIP;
    internal static bool ChatEventsEnabled = true;
    internal static bool KillChatEnabled = true;

    private static string[] cachedProcessArgs;

    internal static void UpdateFromArgs()
    {
        string[] args = GetProcessArgs();
        ConnectIP = GetArgValue(args, "-connectIP") ?? ConnectIP;
        int port;
        string portText = GetArgValue(args, "-connectPort");
        if (!string.IsNullOrEmpty(portText) && int.TryParse(portText, out port) && port > 0)
        {
            ConnectPort = port;
        }

        BindIP = GetArgValue(args, "-bindIP") ?? GetArgValue(args, "-ip") ?? BindIP;
        BindToSpecificIP = !string.IsNullOrEmpty(BindIP) || HasArg("-bindToIP") || HasArg("-bindSpecificIP");
        AutoConnectFromArgs = !string.IsNullOrEmpty(ConnectIP) && ConnectPort > 0;
        ChatEventsEnabled = !HasArg("-noChatEvents");
        KillChatEnabled = !HasArg("-noKillChat");

        LegacyServerLimits.UpdateFromArgs(args);
        LegacyIdleKickTimeout.UpdateFromArgs(args);

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
        if (string.IsNullOrEmpty(arg))
        {
            return false;
        }

        return GetProcessArgs().Any(t => string.Equals(StripQuotes(t), arg, StringComparison.OrdinalIgnoreCase));
    }

    internal static bool HasServerArg()
    {
        return GetProcessArgs().Any(t => t.IndexOf("bend_GameServer", StringComparison.OrdinalIgnoreCase) >= 0);
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
        return GetArgValue(GetProcessArgs(), optionName);
    }

    internal static string GetArgValue(string[] args, string optionName)
    {
        if (string.IsNullOrEmpty(optionName))
        {
            return null;
        }

        string value = FindArgValue(NormalizeArgTokens(args), optionName);
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        // Some Unity/Mono builds can split -key:"value with spaces" incorrectly in
        // Environment.GetCommandLineArgs(). The raw command line still contains the
        // original quotes, so use it as a fallback while keeping the strict -key:value
        // / -key:"value" format.
        return FindArgValue(GetProcessArgs(), optionName);
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

    private static string[] GetProcessArgs()
    {
        if (cachedProcessArgs != null)
        {
            return cachedProcessArgs;
        }

        string raw = null;
        try
        {
            raw = Environment.CommandLine;
        }
        catch
        {
        }

        if (!string.IsNullOrEmpty(raw))
        {
            string[] parsed = SplitCommandLine(raw);
            if (parsed != null && parsed.Length > 0)
            {
                cachedProcessArgs = parsed;
                return cachedProcessArgs;
            }
        }

        try
        {
            cachedProcessArgs = Environment.GetCommandLineArgs() ?? new string[0];
        }
        catch
        {
            cachedProcessArgs = new string[0];
        }

        return cachedProcessArgs;
    }

    private static string FindArgValue(string[] args, string optionName)
    {
        if (args == null || args.Length == 0)
        {
            return null;
        }

        string colonPrefix = optionName + ":";

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i] ?? string.Empty;

            if (arg.StartsWith(colonPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return StripQuotes(arg.Substring(colonPrefix.Length).Trim());
            }
        }

        return null;
    }

    private static string[] NormalizeArgTokens(string[] args)
    {
        if (args == null || args.Length == 0)
        {
            return args;
        }

        List<string> normalized = new List<string>(args.Length);

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i] ?? string.Empty;
            char closingQuote;

            if (HasUnclosedQuotedValue(arg, out closingQuote))
            {
                StringBuilder builder = new StringBuilder(arg);

                while (i + 1 < args.Length && !EndsWithQuote(builder.ToString(), closingQuote))
                {
                    i++;
                    builder.Append(' ');
                    builder.Append(args[i] ?? string.Empty);
                }

                arg = builder.ToString();
            }

            normalized.Add(arg);
        }

        return normalized.ToArray();
    }

    private static bool HasUnclosedQuotedValue(string arg, out char closingQuote)
    {
        closingQuote = '\0';
        if (string.IsNullOrEmpty(arg))
        {
            return false;
        }

        int colonIndex = arg.IndexOf(':');
        if (colonIndex < 0 || colonIndex + 1 >= arg.Length)
        {
            return false;
        }

        char firstValueChar = arg[colonIndex + 1];
        if (!TryGetClosingQuote(firstValueChar, out closingQuote))
        {
            return false;
        }

        return !EndsWithQuote(arg, closingQuote);
    }

    private static bool EndsWithQuote(string value, char quote)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return value.TrimEnd().EndsWith(quote.ToString(), StringComparison.Ordinal);
    }

    private static string[] SplitCommandLine(string commandLine)
    {
        List<string> result = new List<string>();
        StringBuilder current = new StringBuilder();
        bool inQuote = false;
        char closingQuote = '\0';

        for (int i = 0; i < commandLine.Length; i++)
        {
            char ch = commandLine[i];

            if (inQuote)
            {
                if (ch == closingQuote)
                {
                    inQuote = false;
                    closingQuote = '\0';
                    continue;
                }

                current.Append(ch);
                continue;
            }

            char nextClosingQuote;
            if (TryGetClosingQuote(ch, out nextClosingQuote))
            {
                inQuote = true;
                closingQuote = nextClosingQuote;
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                FlushCurrent(result, current);
                continue;
            }

            current.Append(ch);
        }

        FlushCurrent(result, current);
        return result.ToArray();
    }

    private static void FlushCurrent(List<string> result, StringBuilder current)
    {
        if (current.Length <= 0)
        {
            return;
        }

        result.Add(current.ToString());
        current.Length = 0;
    }

    private static bool TryGetClosingQuote(char quote, out char closingQuote)
    {
        closingQuote = '\0';

        if (quote == '"' || quote == '\'')
        {
            closingQuote = quote;
            return true;
        }

        if (quote == '“')
        {
            closingQuote = '”';
            return true;
        }

        if (quote == '«')
        {
            closingQuote = '»';
            return true;
        }

        return false;
    }
}
