using System;
using System.Text;
using UnityEngine;

internal static class LegacyServerLimits
{
    internal const int DefaultMaxCars = 100;
    internal const int DefaultMaxNikLength = 20;
    internal const int DefaultMaxCarsByPlayer = 0;
    // 0 means unlimited

    internal static int MaxCars { get; private set; } = DefaultMaxCars;

    internal static int MaxCarsByPlayer { get; private set; } = DefaultMaxCarsByPlayer;

    internal static int MaxNikLength { get; private set; } = DefaultMaxNikLength;

    private static bool initialized;

    internal static void UpdateFromArgs(string[] args)
    {
        int previousMaxCars = MaxCars;
        int previousMaxCarsByPlayer = MaxCarsByPlayer;
        int previousMaxNikLength = MaxNikLength;

        MaxCars = ReadLimit(args, "-maxCars", DefaultMaxCars);
        MaxCarsByPlayer = ReadLimit(args, "-maxCarsByPlayer", DefaultMaxCarsByPlayer);
        MaxNikLength = ReadLimit(args, "-maxNikLength", DefaultMaxNikLength);

        if (!initialized || previousMaxCars != MaxCars || previousMaxCarsByPlayer != MaxCarsByPlayer || previousMaxNikLength != MaxNikLength)
        {
            initialized = true;
            Debug.Log(
                "Server limits: maxCars=" + FormatLimit(MaxCars) +
                ", maxCarsByPlayer=" + FormatLimit(MaxCarsByPlayer) +
                ", maxNikLength=" + FormatLimit(MaxNikLength)
            );
        }
    }

    internal static string SanitizeNikName(string rawNikName)
    {
        string value = NetManagerTools.RemoveRitchText(rawNikName ?? string.Empty) ?? string.Empty;
        value = RemoveLineBreakCharacters(value);

        if (MaxNikLength > 0 && value.Length > MaxNikLength)
        {
            value = TruncateUtf16Safely(value, MaxNikLength);
        }

        return value;
    }

    internal static bool CanSpawnNetworkCar(NetInputControl input, out int currentCars, out int playerCars)
    {
        currentCars = CountActiveNetworkCars();
        playerCars = 0;

        int pendingCars = LegacyServerAdminService.CountAllPendingCars();
        if (MaxCars > 0 && currentCars + pendingCars >= MaxCars)
        {
            return false;
        }

        int reservedCars;
        if (!LegacyServerAdminService.CanSpawnCarForPlayer(input, out playerCars, out reservedCars))
        {
            return false;
        }

        return true;
    }

    internal static int CountActiveNetworkCars()
    {
        NetControl[] netControls = Resources.FindObjectsOfTypeAll<NetControl>();
        int count = 0;

        for (int i = 0; i < netControls.Length; i++)
        {
            NetControl netControl = netControls[i];

            if (!IsActiveServerCar(netControl))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private static bool IsActiveServerCar(NetControl netControl)
    {
        if (!netControl || !netControl.gameObject || !netControl.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (!netControl.isServer)
        {
            return false;
        }

        // The original NetControl uses this exact distinction:
        // objName == "Male" is a player; every other NetControl object is a vehicle.
        // The admin service additionally excludes exploded/destroyed cars which
        // remain active in the game's object cache until it is unloaded.
        return !string.Equals(netControl.objName, "Male", StringComparison.Ordinal) &&
               LegacyServerAdminService.IsActiveServerVehicle(netControl);
    }

    private static int ReadLimit(string[] args, string optionName, int defaultValue)
    {
        string text = GetArgumentValue(args, optionName);

        if (string.IsNullOrEmpty(text))
        {
            return defaultValue;
        }

        int value;

        if (!int.TryParse(text, out value) || value < 0)
        {
            Debug.LogWarning(
                "Invalid " + optionName + " value '" + text +
                "'. Using default " + defaultValue.ToString() + "."
            );

            return defaultValue;
        }

        return value;
    }

    private static string GetArgumentValue(string[] args, string optionName)
    {
        return LegacyCommandLine.GetArgValue(args, optionName);
    }

    private static string RemoveLineBreakCharacters(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder builder = null;

        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];

            if (!IsLineBreakCharacter(character))
            {
                if (builder != null)
                {
                    builder.Append(character);
                }

                continue;
            }

            if (builder == null)
            {
                builder = new StringBuilder(value.Length);
                builder.Append(value, 0, i);
            }
        }

        return builder == null ? value : builder.ToString();
    }

    private static bool IsLineBreakCharacter(char character)
    {
        return character == '\r' ||
               character == '\n' ||
               character == '\u0085' ||
               character == '\u2028' ||
               character == '\u2029';
    }

    private static string TruncateUtf16Safely(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength || maxLength <= 0)
        {
            return value;
        }

        int length = maxLength;

        // Never leave an unpaired high surrogate at the end of a nickname.
        if (length > 0 &&
            length < value.Length &&
            char.IsHighSurrogate(value[length - 1]) &&
            char.IsLowSurrogate(value[length]))
        {
            length--;
        }

        return value.Substring(0, length);
    }

    private static string FormatLimit(int value)
    {
        return value == 0 ? "unlimited" : value.ToString();
    }

    //internal static void LogNicknameWasSanitized(string original, string sanitized, string source)
    //{
    //    if (string.Equals(original ?? string.Empty, sanitized ?? string.Empty, StringComparison.Ordinal))
    //    {
    //        return;
    //    }

    //    LogInfo(
    //        "Nickname sanitized (" + source + "): '" + (original ?? string.Empty) +
    //        "' -> '" + (sanitized ?? string.Empty) + "'."
    //    );
    //}

    //internal static void LogCarLimitReached(NetInputControl input, string fileName, int currentCars)
    //{
    //    string playerName = input ? input.nikName : "unknown";

    //    Debug.LogWarning(
    //        "Car spawn rejected for player '" + playerName +
    //        "', car '" + (fileName ?? string.Empty) +
    //        "': limit reached (" + currentCars.ToString() +
    //        "/" + MaxCars.ToString() + ")."
    //    );
    //}

    private static void LogInfo(string message)
    {
        if (BigCityLegacyPlugin.Log != null)
        {
            BigCityLegacyPlugin.Log.LogInfo(message);
            return;
        }

        Debug.Log("[BigCityLegacy] " + message);
    }

    private static void LogWarning(string message)
    {
        if (BigCityLegacyPlugin.Log != null)
        {
            BigCityLegacyPlugin.Log.LogWarning(message);
            return;
        }

        Debug.LogWarning("[BigCityLegacy] " + message);
    }
}
