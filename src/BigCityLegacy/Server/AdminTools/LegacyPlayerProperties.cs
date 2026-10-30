using System;
using System.Collections.Generic;
using System.Globalization;
using Smooth;
using UnityEngine;
using UnityEngine.Networking;

// Moderation is authoritative on the server. The existing harmless event RPC carries UI hints
// for BCL clients; stock clients ignore that string and retain their original wire format.
internal static class LegacyPlayerProperties
{
    private const string WirePrefix = "BCL.admin.props.v1|";
    private static readonly Dictionary<NetInputControl, RuntimeState> states = new Dictionary<NetInputControl, RuntimeState>();
    private static readonly Dictionary<uint, int> clientMasks = new Dictionary<uint, int>();
    private static readonly Dictionary<NetworkConnection, Dictionary<uint, int>> sent = new Dictionary<NetworkConnection, Dictionary<uint, int>>();
    private static readonly Dictionary<PlayerControl, Pose> clientAnchors = new Dictionary<PlayerControl, Pose>();

    internal static int Bit(string name)
    {
        switch (name) { case "noweapon": return 1; case "nomove": return 2; case "nocars": return 4; case "nochat": return 8; case "invisible": return 16; default: return 0; }
    }

    internal static bool Has(NetInputControl input, string name)
    {
        if (!input) return false;
        if (NetManager.isServer)
        {
            // GUID is taken from the authenticated input, never from nickname or IP.
            RuntimeState state;
            if (states.TryGetValue(input, out state))
            {
                long? until;
                return state.Props.TryGetValue(name, out until) && LegacyAdminTime.IsActive(until, LegacyAdminTime.Now);
            }
            return LegacyBanList.HasProp(input.pirvateConnectID, name);
        }
        int mask;
        return NetManager.isOnlineClient && clientMasks.TryGetValue(input.netId.Value, out mask) && (mask & Bit(name)) != 0;
    }

    internal static void Reset()
    {
        states.Clear(); sent.Clear(); ClearClient(); ChatEventsPatches.ResetAdminDisconnects();
    }
    internal static void ClearClient() { clientMasks.Clear(); clientAnchors.Clear(); }

    internal static void Receive(string command)
    {
        if (!NetManager.isOnlineClient || command == null || !command.StartsWith(WirePrefix, StringComparison.Ordinal)) return;
        string[] parts = command.Substring(WirePrefix.Length).Split('|');
        uint id; int mask;
        if (parts.Length != 2 || !uint.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out id) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out mask) || mask < 0 || mask > 31) return;
        int oldMask;
        bool changed = !clientMasks.TryGetValue(id, out oldMask) ? mask != 0 : oldMask != mask;
        if (mask == 0) clientMasks.Remove(id); else clientMasks[id] = mask;
        GameObject go = ClientScene.FindLocalObject(new NetworkInstanceId(id));
        NetInputControl input = go ? go.GetComponent<NetInputControl>() : null;
        if (input && input.ui) input.ui.DisableIconByEventIfNeed();
        if (changed && ((oldMask ^ mask) & Bit("invisible")) != 0)
        {
            NetUI_PlayersList.UpdateListIfEnabled();
            NetUI_ServerInfo.UpdateListIfEnabled();
        }
    }

    internal static void Refresh()
    {
        if (!NetManager.isServer) return;
        var present = new HashSet<NetInputControl>();
        foreach (InputControl input in InputControl.inputs)
        {
            if (!input || !input.netInput) continue;
            NetInputControl netInput = input.netInput;
            present.Add(netInput);
            RuntimeState state;
            if (!states.TryGetValue(netInput, out state)) { state = new RuntimeState(); states.Add(netInput, state); }
            int mask = 0;
            state.Props.Clear();
            foreach (LegacyBanList.PlayerProp prop in LegacyBanList.GetProps(netInput.pirvateConnectID))
            { mask |= Bit(prop.Prop); state.Props[prop.Prop] = prop.Until; }
            state.Mask = mask;
            if ((mask & 2) == 0) state.Player = null;
            if ((mask & 1) != 0)
            {
                if (netInput.netControl && netInput.netControl.NetWeapones)
                    netInput.netControl.NetWeapones.SetSelectWepone(Weapone.Type.Player_Hand);
                if (input.buts)
                {
                    input.buts.ButChange((int)InputBut.But.Human_Fire, 0f);
                    input.buts.ButChange((int)InputBut.But.Car_Fire, 0f);
                    input.buts.ButChange((int)InputBut.But.Car_Mine, 0f);
                    input.buts.ButChange((int)InputBut.But.Ditonator, 0f);
                }
            }
        }
        var removed = new List<NetInputControl>();
        foreach (NetInputControl input in states.Keys) if (!input || !present.Contains(input)) removed.Add(input);
        foreach (NetInputControl input in removed) states.Remove(input);

        var liveConnections = new HashSet<NetworkConnection>();
        foreach (NetInputControl input in present)
        {
            if (!input.isConnected || !input.onServerNetPlayer || !input.onServerNetPlayer.syncPlayer) continue;
            NetworkConnection conn = input.connect2player;
            if (conn == null || !conn.isReady) continue;
            liveConnections.Add(conn);
            Dictionary<uint, int> previous;
            if (!sent.TryGetValue(conn, out previous)) { previous = new Dictionary<uint, int>(); sent.Add(conn, previous); }
            foreach (NetInputControl subject in present)
            {
                uint id = subject.netId.Value;
                if (id == 0) continue;
                int mask = states[subject].Mask, old;
                if (previous.TryGetValue(id, out old) ? old == mask : mask == 0) continue;
                input.onServerNetPlayer.syncPlayer.CallTarget_EventMessageHello(conn, WirePrefix +
                    id.ToString(CultureInfo.InvariantCulture) + "|" + mask.ToString(CultureInfo.InvariantCulture));
                previous[id] = mask;
            }
            // Stop retaining despawned input IDs on long-running servers and clients.
            var stale = new List<uint>();
            foreach (uint id in previous.Keys)
            {
                bool exists = false;
                foreach (NetInputControl subject in present) if (subject.netId.Value == id) { exists = true; break; }
                if (!exists) stale.Add(id);
            }
            foreach (uint id in stale)
            {
                input.onServerNetPlayer.syncPlayer.CallTarget_EventMessageHello(conn, WirePrefix + id.ToString(CultureInfo.InvariantCulture) + "|0");
                previous.Remove(id);
            }
        }
        var closed = new List<NetworkConnection>();
        foreach (NetworkConnection conn in sent.Keys) if (!liveConnections.Contains(conn)) closed.Add(conn);
        foreach (NetworkConnection conn in closed) sent.Remove(conn);
        Update();
    }

    internal static void Update()
    {
        if (!NetManager.isServer) return;
        foreach (KeyValuePair<NetInputControl, RuntimeState> pair in states)
        {
            NetInputControl input = pair.Key;
            RuntimeState state = pair.Value;
            if (!input || !input.input || (state.Mask & 2) == 0 || !Has(input, "nomove")) continue;
            PlayerControl player = input.input.currentPlayer;
            if (!player || !player.isAlive) { state.Player = null; continue; }
            if (state.Player != player)
            {
                if (player.parent_control) player.SetParentControl(null);
                if (!ValidPose(player.body_pos, player.body_rot)) continue;
                state.Player = player;
                state.Position = player.body_pos;
                state.Rotation = player.body_rot;
                state.NeedsCorrection = true;
            }
            if (!ValidPose(state.Position, state.Rotation)) continue;
            Pin(player, state.Position, state.Rotation);
            if (state.NeedsCorrection && Time.realtimeSinceStartup >= state.NextCorrection && input.spawn && !input.spawn.nowRespawn && input.isConnected)
            {
                state.NeedsCorrection = false;
                state.NextCorrection = Time.realtimeSinceStartup + 0.5f;
                // The native terrain/teleport handshake also corrects an unmodified owner's position.
                input.spawn.Spawn(state.Position, state.Rotation, false, true);
            }
        }
    }

    internal static void OnSpawn(NetSpawn spawn, Vector3 position, Quaternion rotation)
    {
        RuntimeState state;
        if (NetManager.isServer && spawn && spawn.netInput && states.TryGetValue(spawn.netInput, out state) && (state.Mask & 2) != 0)
        { state.Position = position; state.Rotation = rotation; state.Player = spawn.netInput.input.currentPlayer; }
        if (spawn && spawn.netInput && spawn.netInput.input && spawn.netInput.input.currentPlayer)
        {
            PlayerControl player = spawn.netInput.input.currentPlayer;
            if (NetManager.isOnlineClient && Has(spawn.netInput, "nomove"))
                clientAnchors[player] = new Pose { Position = position, Rotation = rotation };
            else clientAnchors.Remove(player);
        }
    }

    internal static void FilterState(SmoothSync sync, ref State incoming)
    {
        if (!NetManager.isServer || !sync || !sync.control || !sync.control.net_control) return;
        NetInputControl input = sync.control.net_control.netInputControl;
        RuntimeState state;
        if (!input || !states.TryGetValue(input, out state) || !state.Player || !Has(input, "nomove")) return;
        if ((incoming.position - state.Position).sqrMagnitude > 0.01f) state.NeedsCorrection = true;
        incoming.position = state.Position; incoming.rotation = state.Rotation;
        incoming.velocity = Vector3.zero; incoming.angularVelocity = Vector3.zero;
    }

    internal static void PinPlayer(PlayerControl player)
    {
        if (!player || !player.input || !Has(player.input.netInput, "nomove") || !player.isAlive)
        { if (player) clientAnchors.Remove(player); return; }
        if (player.input.buts) player.input.buts.move2point.enabled = false;
        if (NetManager.isServer)
        {
            RuntimeState state;
            if (states.TryGetValue(player.input.netInput, out state) && state.Player == player) Pin(player, state.Position, state.Rotation);
        }
        else if (player.input.netInput.hasAuthority)
        {
            Pose pose;
            if (!clientAnchors.TryGetValue(player, out pose))
            { pose = new Pose { Position = player.body_pos, Rotation = player.body_rot }; clientAnchors.Add(player, pose); }
            if (!player.parent_control) Pin(player, pose.Position, pose.Rotation);
        }
    }

    private static void Pin(PlayerControl player, Vector3 position, Quaternion rotation)
    {
        if (player.parent_control || !player.body || !player.tr_view || !ValidPose(position, rotation)) return;
        player.SetPosAndRot(position, rotation);
        player.body_linearW = Vector3.zero;
        player.body.velocity = Vector3.zero;
        player.body.angularVelocity = Vector3.zero;
    }

    private static bool ValidPose(Vector3 position, Quaternion rotation)
    {
        return Finite(position.x) && Finite(position.y) && Finite(position.z) && position.sqrMagnitude < 4000000000000f &&
            Finite(rotation.x) && Finite(rotation.y) && Finite(rotation.z) && Finite(rotation.w);
    }
    private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }

    private sealed class RuntimeState
    {
        internal int Mask;
        internal readonly Dictionary<string, long?> Props = new Dictionary<string, long?>();
        internal PlayerControl Player;
        internal Vector3 Position;
        internal Quaternion Rotation;
        internal bool NeedsCorrection;
        internal float NextCorrection;
    }
    private struct Pose { internal Vector3 Position; internal Quaternion Rotation; }
}
