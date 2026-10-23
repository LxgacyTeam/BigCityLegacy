using System.Runtime.CompilerServices;
using UnityEngine;

internal static class LegacyRespawnHistory
{
    internal const int MaxConsecutiveRespawns = 5;
    internal const float SamePointDistanceSquared = 1f;
    private sealed class Streak
    {
        internal Vector3 Point;
        internal int Count;
        internal float DeathTime;
        internal bool HasDeath;
        public Streak() { }
    }

    private static ConditionalWeakTable<object, Streak> histories = new ConditionalWeakTable<object, Streak>();

    internal static bool TryGetExcludedPoint(object player, Vector3 nearest, out Vector3 excluded)
    {
        excluded = Vector3.zero;
        Streak streak;
        if (player == null || !histories.TryGetValue(player, out streak) ||
            streak.Count < MaxConsecutiveRespawns || !SamePoint(nearest, streak.Point)) return false;
        excluded = streak.Point;
        return true;
    }

    internal static void Record(object player, Vector3 actual, float deathTime)
    {
        if (player == null) return;
        var streak = histories.GetOrCreateValue(player);
        if (streak.HasDeath && streak.DeathTime == deathTime) return;
        streak.Count = streak.Count > 0 && SamePoint(actual, streak.Point)
            ? Mathf.Min(streak.Count + 1, MaxConsecutiveRespawns) : 1;
        streak.Point = actual;
        streak.DeathTime = deathTime;
        streak.HasDeath = true;
    }

    internal static bool SamePoint(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude < SamePointDistanceSquared;
    }

    internal static void Forget(object player) { if (player != null) histories.Remove(player); }
    internal static void Clear() { histories = new ConditionalWeakTable<object, Streak>(); }
}
