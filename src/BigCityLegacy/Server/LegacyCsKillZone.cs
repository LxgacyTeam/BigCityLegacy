using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

internal static class LegacyCsKillZone
{
    private const float KillDelayMs = 5000f;
    private const float RespawnMargin = 5f;
    private static readonly Dictionary<int, EventState> events = new Dictionary<int, EventState>();

    internal static void Check(Net_CS cs, float now)
    {
        if (!cs)
        {
            return;
        }

        if (!NetManager.isServer || !NetworkServer.active ||
            cs.state != Net_BaseEvent.CurState.Race || !IsFinite(now))
        {
            ClearEvent(cs);
            return;
        }

        Vector3 center;
        float radius;
        if (!TryGetZone(cs, out center, out radius))
        {
            ClearEvent(cs);
            return;
        }
        float radiusSquared = radius * radius;

        int eventId = cs.GetInstanceID();
        EventState state;
        if (!events.TryGetValue(eventId, out state))
        {
            state = new EventState();
            events.Add(eventId, state);
        }

        if (now < state.LastCheckTime)
        {
            state.Intruders.Clear();
        }
        state.LastCheckTime = now;
        state.Participants.Clear();
        for (int i = 0; i < cs.players.Count; i++)
        {
            Net_BaseEvent.BasePlayer participant = cs.players[i];
            if (participant != null && participant.input)
            {
                state.Participants.Add(participant.input);
            }
        }
        state.Seen.Clear();
        state.Present.Clear();

        for (int i = 0; i < InputControl.inputs.Count; i++)
        {
            InputControl input = InputControl.inputs[i];
            if (!input || !input.netInput)
            {
                continue;
            }

            NetInputControl netInput = input.netInput;
            if (!netInput.isConnected || !netInput.netControl || netInput.curEvent ||
                state.Participants.Contains(netInput))
            {
                continue;
            }
            state.Present.Add(netInput);
            if (netInput.spawn && netInput.spawn.nowRespawn)
            {
                continue;
            }

            PlayerControl player = input.currentPlayer;
            if (!player || !player.isAlive)
            {
                continue;
            }

            Control current = input.currentOrParent;
            if (!current)
            {
                continue;
            }
            float distanceSquared = (current.body_pos - center).sqrMagnitude;
            if (!IsFinite(distanceSquared))
            {
                continue;
            }
            if (distanceSquared >= radiusSquared)
            {
                state.WaitingForExit.Remove(netInput);
                continue;
            }
            if (state.WaitingForExit.Contains(netInput))
            {
                continue;
            }

            state.Seen.Add(netInput);
            Exposure exposure;
            if (!state.Intruders.TryGetValue(netInput, out exposure) || exposure.Player != player)
            {
                state.Intruders[netInput] = new Exposure(player, now);
                continue;
            }

            if (now - exposure.EnteredAt < KillDelayMs)
            {
                continue;
            }

            state.Intruders.Remove(netInput);
            player.SetLive(0f, false, WhoKill.Init(player, Weapone.Type.Unknow));
            Debug.Log("[CsKillZone] FreeRoam player '" + netInput.nikName +
                "' killed after 5 seconds in arena '" + cs.name + "'.");
        }

        state.Remove.Clear();
        foreach (NetInputControl netInput in state.Intruders.Keys)
        {
            if (!state.Seen.Contains(netInput))
            {
                state.Remove.Add(netInput);
            }
        }
        for (int i = 0; i < state.Remove.Count; i++)
        {
            state.Intruders.Remove(state.Remove[i]);
        }
        state.Remove.Clear();
        foreach (NetInputControl netInput in state.WaitingForExit)
        {
            if (!state.Present.Contains(netInput))
            {
                state.Remove.Add(netInput);
            }
        }
        for (int i = 0; i < state.Remove.Count; i++)
        {
            state.WaitingForExit.Remove(state.Remove[i]);
        }
        state.Remove.Clear();
        state.Seen.Clear();
        state.Present.Clear();
        state.Participants.Clear();
    }

    internal static bool IsInsideActiveZone(Vector3 position)
    {
        return IsInsideActiveZone(position, 0f);
    }

    internal static bool IsSafeRespawnPosition(Vector3 position)
    {
        return IsFinite(position.x) && IsFinite(position.y) && IsFinite(position.z) &&
            !IsInsideActiveZone(position, RespawnMargin);
    }

    private static bool IsInsideActiveZone(Vector3 position, float margin)
    {
        if (!NetManager.isServer || !NetworkServer.active)
        {
            return false;
        }
        for (int i = 0; i < Net_BaseEvent.instances.Count; i++)
        {
            Net_CS cs = Net_BaseEvent.instances[i] as Net_CS;
            Vector3 center;
            float radius;
            if (TryGetZone(cs, out center, out radius) &&
                (position - center).sqrMagnitude < (radius + margin) * (radius + margin))
            {
                return true;
            }
        }
        return false;
    }

    internal static void PrepareSpawn(NetSpawn spawn, ref Vector3 position, bool isNeedReset)
    {
        if (!NetManager.isServer || !NetworkServer.active || !spawn || !spawn.netInput)
        {
            return;
        }
        NetInputControl input = spawn.netInput;
        foreach (EventState state in events.Values)
        {
            state.Intruders.Remove(input);
            state.WaitingForExit.Remove(input);
        }
        if (!isNeedReset || IsParticipant(input) || !IsInsideActiveZone(position))
        {
            return;
        }

        Vector3 safePosition;
        if (TryGetSafeSpawn(position, out safePosition))
        {
            Debug.Log("[CsKillZone] Respawn for '" + input.nikName + "' moved outside active arenas: " +
                position + " -> " + safePosition + ".");
            position = safePosition;
            return;
        }

        for (int i = 0; i < Net_BaseEvent.instances.Count; i++)
        {
            Net_CS cs = Net_BaseEvent.instances[i] as Net_CS;
            Vector3 center;
            float radius;
            if (!TryGetZone(cs, out center, out radius) ||
                (position - center).sqrMagnitude >= radius * radius)
            {
                continue;
            }
            EventState state;
            int id = cs.GetInstanceID();
            if (!events.TryGetValue(id, out state))
            {
                state = new EventState();
                events.Add(id, state);
            }
            state.WaitingForExit.Add(input);
        }
        Debug.LogWarning("[CsKillZone] No safe respawn point for '" + input.nikName +
            "'. Arena countdown will resume after leaving and reentering the zone.");
    }

    private static bool IsParticipant(NetInputControl input)
    {
        if (input.curEvent)
        {
            return true;
        }
        for (int i = 0; i < Net_BaseEvent.instances.Count; i++)
        {
            Net_BaseEvent currentEvent = Net_BaseEvent.instances[i];
            if (!currentEvent)
            {
                continue;
            }
            for (int j = 0; j < currentEvent.players.Count; j++)
            {
                Net_BaseEvent.BasePlayer participant = currentEvent.players[j];
                if (participant != null && participant.input == input)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool TryGetSafeSpawn(Vector3 requested, out Vector3 safe)
    {
        if (LegacyNearestPointRespawn.TryGetNearestSafeRespawnPoint(requested, out safe))
        {
            return true;
        }
        float nearestDistance = float.PositiveInfinity;
        if (SpawnPos.spawnPoses != null)
        {
            for (int i = 0; i < SpawnPos.spawnPoses.Length; i++)
            {
                ConsiderSpawn(SpawnPos.spawnPoses[i], requested, ref safe, ref nearestDistance);
            }
        }
        for (int i = 0; i < SpawnPos.instances.Count; i++)
        {
            SpawnPos spawn = SpawnPos.instances[i];
            if (!spawn)
            {
                continue;
            }
            if (spawn.type == SpawnPos.Type.NetZone)
            {
                for (int j = 0; j < spawn.transform.childCount; j++)
                {
                    ConsiderSpawn(spawn.transform.GetChild(j).position, requested, ref safe, ref nearestDistance);
                }
            }
            else if (spawn.type == SpawnPos.Type.NetPos || spawn.type == SpawnPos.Type.Hospital)
            {
                ConsiderSpawn(spawn.transform.position, requested, ref safe, ref nearestDistance);
            }
        }
        return IsFinite(nearestDistance);
    }

    private static void ConsiderSpawn(Vector3 candidate, Vector3 requested, ref Vector3 safe, ref float nearestDistance)
    {
        float distance = (candidate - requested).sqrMagnitude;
        if (IsFinite(distance) && distance < nearestDistance && IsSafeRespawnPosition(candidate))
        {
            nearestDistance = distance;
            safe = candidate;
        }
    }

    private static bool TryGetZone(Net_CS cs, out Vector3 center, out float radius)
    {
        center = Vector3.zero;
        radius = 0f;
        if (!cs || cs.state != Net_BaseEvent.CurState.Race)
        {
            return false;
        }
        center = cs.center;
        radius = cs.killDistance;
        return radius > 0f && IsFinite(radius * radius) &&
            IsFinite(center.x) && IsFinite(center.y) && IsFinite(center.z);
    }

    internal static void ClearEvent(Net_CS cs)
    {
        if (!ReferenceEquals(cs, null))
        {
            events.Remove(cs.GetInstanceID());
        }
    }

    internal static void Reset()
    {
        events.Clear();
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private sealed class EventState
    {
        internal float LastCheckTime;
        internal readonly Dictionary<NetInputControl, Exposure> Intruders =
            new Dictionary<NetInputControl, Exposure>();
        internal readonly HashSet<NetInputControl> Participants = new HashSet<NetInputControl>();
        internal readonly HashSet<NetInputControl> Seen = new HashSet<NetInputControl>();
        internal readonly HashSet<NetInputControl> Present = new HashSet<NetInputControl>();
        internal readonly HashSet<NetInputControl> WaitingForExit = new HashSet<NetInputControl>();
        internal readonly List<NetInputControl> Remove = new List<NetInputControl>();
    }

    private struct Exposure
    {
        internal readonly PlayerControl Player;
        internal readonly float EnteredAt;

        internal Exposure(PlayerControl player, float enteredAt)
        {
            Player = player;
            EnteredAt = enteredAt;
        }
    }
}
