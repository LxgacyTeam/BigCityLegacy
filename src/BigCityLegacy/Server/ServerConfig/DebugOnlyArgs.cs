using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using static Values;

internal static class DebugOnlyArgs
{
    private static bool _isDebug = LegacyHelpers.isDebugBuild;
    internal static bool noServerCarAggressiveSleep
    {
        get
        {
            if (!_isDebug) { return false; }
            if (LegacyCommandLine.HasArg("-noServerCarAggressiveSleep")) { return true; }
            return false;
        }
    }

    internal static bool noServerCarSleepFastPath
    {
        get
        {
            if (!_isDebug) { return false; }
            if (LegacyCommandLine.HasArg("-noServerCarSleepFastPath")) { return true; }
            return false;
        }
    }

    internal static bool noServerClientCarPhysicsBypass
    {
        get
        {
            if (!_isDebug) { return false; }
            if (LegacyCommandLine.HasArg("-noServerClientCarPhysicsBypass")) { return true; }
            return false;
        }
    }

    internal static bool noServerCpuOptimization
    {
        get
        {
            if (!_isDebug) { return false; }
            if (LegacyCommandLine.HasArg("-noServerCpuOptimization")) { return true; }
            return false;
        }
    }

    internal static bool noServerObjectStripping
    {
        get
        {
            if (!_isDebug) { return false; }
            if (LegacyCommandLine.HasArg("-noServerObjectStripping")) { return true; }
            return false;
        }
    }

    internal static bool noServerCarPresentationOptimization
    {
        get
        {
            if (!_isDebug) { return false; }
            if (LegacyCommandLine.HasArg("-noServerCarPresentationOptimization")) { return true; }
            return false;
        }
    }

    internal static bool noServerNetworkBufferTuning
    {
        get
        {
            if (!_isDebug) { return false; }
            if (LegacyCommandLine.HasArg("-noServerNetworkBufferTuning")) { return true; }
            return false;
        }
    }

    internal static float? serverCarSleepDelayMs
    {
        get
        {
            if (!_isDebug) { return null; }

            float parsed;
            string value = LegacyCommandLine.GetArgValue("-serverCarSleepDelayMs");

            if (float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out parsed))
            {
                return parsed;
            }
            else
            {
                return null;
            }
        }
    }

    internal static int? serverMaxPendingBuffers
    {
        get
        {
            if (!_isDebug) { return null; }

            int parsed;
            string value = LegacyCommandLine.GetArgValue("-serverMaxPendingBuffers");

            if (int.TryParse(value, out parsed))
            {
                return parsed;
            }
            else
            {
                return null;
            }
        }
    }
}
