using System;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;

// Attached to the network input, so timestamps cannot leak into a new connection.
internal sealed class LegacyCarSpawnTimeout : MonoBehaviour
{
    internal const int DefaultMilliseconds = 1000;
    internal static int Milliseconds { get; private set; } = DefaultMilliseconds;
    private readonly CarSpawnCooldown timer = new CarSpawnCooldown();

    internal static void UpdateFromArgs(string[] args)
    {
        string text = LegacyCommandLine.GetArgValue(args, "-CarSpawnTimeout");
        int value;
        Milliseconds = DefaultMilliseconds;
        if (text == null) return;
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) && value >= 0)
            Milliseconds = value;
        else LegacyServerConsole.LogAfterConsoleInit("Invalid -CarSpawnTimeout; using 1000 ms.", LogType.Warning);
    }

    internal static bool CanSpawn(NetInputControl input)
    {
        var cooldown = input.GetComponent<LegacyCarSpawnTimeout>();
        return !cooldown || cooldown.timer.CanSpawn(Stopwatch.GetTimestamp(), Milliseconds);
    }

    internal static void Spawned(NetInputControl input)
    {
        if (Milliseconds == 0) return;
        var cooldown = input.GetComponent<LegacyCarSpawnTimeout>() ?? input.gameObject.AddComponent<LegacyCarSpawnTimeout>();
        cooldown.timer.Spawned(Stopwatch.GetTimestamp());
    }
}

internal sealed class CarSpawnCooldown
{
    private long? lastSpawn;
    internal bool CanSpawn(long now, int milliseconds) => milliseconds == 0 || !lastSpawn.HasValue ||
        (now - lastSpawn.Value) * 1000.0 / Stopwatch.Frequency >= milliseconds;
    internal void Spawned(long now) { lastSpawn = now; }
}
