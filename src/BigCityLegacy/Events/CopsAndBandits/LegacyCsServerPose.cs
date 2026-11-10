using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Networking;

internal static class LegacyCsServerPose
{
    private sealed class Pending
    {
        internal Animator Animator;
        internal LegacyCsPoseFix Plan;
        internal string Reason;
        internal bool ErrorReported;
    }
    private sealed class SpawnTicket { internal PlayerControl Player; internal bool ReadyHandled; }

    private static readonly Dictionary<PlayerControl, Pending> pending = new Dictionary<PlayerControl, Pending>();
    private static readonly List<PlayerControl> work = new List<PlayerControl>();
    private static ConditionalWeakTable<NetSpawn, SpawnTicket> spawns = new ConditionalWeakTable<NetSpawn, SpawnTicket>();
    private static PlayerControl evaluating;
    private static bool ticking;

    internal static bool IsEvaluating(PlayerControl player) { return !ReferenceEquals(evaluating, null) && ReferenceEquals(evaluating, player); }

    internal static bool Applies(PlayerControl player)
    {
        if (!NetManager.isServer || !NetworkServer.active || !player || !player.isActiveAndEnabled || !player.net_control) return false;
        NetInputControl input = player.input ? player.input.netInput : player.net_control.netInputControl;
        Net_CS cs = input ? input.curEvent as Net_CS : null;
        return input && input.input && input.input.currentPlayer == player && cs && cs.state != Net_BaseEvent.CurState.Complite;
    }

    internal static void KeepAnimating(PlayerControl player)
    {
        if (Applies(player) && player.animator) player.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
    }

    internal static void Spawned(NetSpawn spawn, bool reset)
    {
        if (!NetManager.isServer || !spawn) return;
        // Teleports also use Spawn(false). Only a full respawn creates a new repair ticket.
        spawns.Remove(spawn);
        if (!reset || !spawn.netInput || !spawn.netInput.input) return;
        PlayerControl player = spawn.netInput.input.currentPlayer;
        if (!Applies(player)) return;
        spawns.Add(spawn, new SpawnTicket { Player = player });
        Schedule(player, LegacyCsPoseFix.Pass.Initial, "spawn", true);
    }

    internal static void SpawnReady(NetSpawn spawn)
    {
        SpawnTicket ticket;
        if (!NetManager.isServer || !spawn || !spawns.TryGetValue(spawn, out ticket) || ticket.ReadyHandled) return;
        // Stock readiness commands have no generation field. Consume at most one per server spawn.
        ticket.ReadyHandled = true;
        if (!spawn.netInput || !spawn.netInput.input || spawn.netInput.input.currentPlayer != ticket.Player) return;
        Schedule(ticket.Player, LegacyCsPoseFix.Pass.SpawnReady, "spawn_ready", false);
    }

    internal static void ReturnedFromRagdoll(PlayerControl player)
    {
        if (player && player.isAlive) Schedule(player, LegacyCsPoseFix.Pass.Initial, "ragdoll_return", false);
    }

    internal static void ModelLoaded(PlayerControl player)
    {
        Schedule(player, LegacyCsPoseFix.Pass.Initial, "model_loaded", false);
    }

    private static void Schedule(PlayerControl player, LegacyCsPoseFix.Pass pass, string reason, bool replace)
    {
        if (!Applies(player) || !player.isAlive || !player.animator) return;
        KeepAnimating(player);
        Pending entry;
        if (!replace && pending.TryGetValue(player, out entry) && entry.Animator == player.animator)
            entry.Plan.Add(Time.frameCount, Time.realtimeSinceStartup, pass);
        else
            pending[player] = new Pending { Animator = player.animator, Reason = reason,
                Plan = new LegacyCsPoseFix(Time.frameCount, Time.realtimeSinceStartup, pass) };
    }

    internal static void Tick()
    {
        if (ticking || pending.Count == 0) return;
        if (!NetManager.isServer || !NetworkServer.active) { Reset(); return; }
        ticking = true;
        try
        {
            work.Clear();
            foreach (PlayerControl player in pending.Keys) work.Add(player);
            foreach (PlayerControl player in work)
            {
                Pending entry;
                if (!pending.TryGetValue(player, out entry)) continue;
                if (!Applies(player) || !player.isAlive || player.parent_control || !player.ragdoll ||
                    player.ragdoll.state != PlayerRagDoll.State.no_used)
                { RemoveIfCurrent(player, entry); continue; }
                if (entry.Plan.Expired(Time.realtimeSinceStartup))
                {
                    Trace(player, "POSE_REBUILD_SKIPPED", "reason=not_ready_before_deadline source=" + entry.Reason);
                    RemoveIfCurrent(player, entry);
                    continue;
                }
                Animator animator = player.animator;
                if (animator != entry.Animator)
                {
                    Schedule(player, LegacyCsPoseFix.Pass.Initial, "animator_replaced", true);
                    continue;
                }
                if (!animator || !animator.enabled || !animator.gameObject.activeInHierarchy || !animator.isInitialized || !animator.runtimeAnimatorController ||
                    !player.body || !player.tr_view || !player.ShotBones || player.ShotBones.bones == null) continue;
                LegacyCsPoseFix.Pass pass;
                if (!entry.Plan.TryBegin(Time.frameCount, Time.realtimeSinceStartup, out pass)) continue;
                bool success = Rebuild(player, animator, entry, pass);
                entry.Plan.Complete(pass, success, Time.realtimeSinceStartup);
                if (entry.Plan.Finished) RemoveIfCurrent(player, entry);
            }
        }
        finally { work.Clear(); ticking = false; }
    }

    private static void RemoveIfCurrent(PlayerControl player, Pending entry)
    {
        Pending current;
        if (pending.TryGetValue(player, out current) && ReferenceEquals(current, entry)) pending.Remove(player);
    }

    private static bool Rebuild(PlayerControl player, Animator animator, Pending entry, LegacyCsPoseFix.Pass pass)
    {
        // This runs in the next frame's LateUpdate, after native body/view placement and deferred
        // ragdoll destruction. Re-evaluate the current pose with zero elapsed animation time.
        // Never rebind/play a state, change root-motion mode, or move/reset the physics body.
        bool fireEvents = animator.fireEvents;
        Vector3 rootPosition = animator.transform.position;
        Quaternion rootRotation = animator.transform.rotation;
        float headBefore = float.NaN, hipsBefore = float.NaN;
        if (LegacyCsDiagnostics.Enabled) LegacyCsDiagnostics.Safe(() => { headBefore = HeadHeight(player); hipsBefore = HipsHeight(player); });
        bool success = false;
        evaluating = player;
        try
        {
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.fireEvents = false;
            animator.Update(0f);
            success = true;
        }
        catch (Exception ex)
        {
            if (!entry.ErrorReported)
            {
                entry.ErrorReported = true;
                Debug.LogWarning("[CS Pose] Deferred skeleton rebuild failed for control " +
                    LegacyCsDiagnostics.Id(player) + ": " + ex);
            }
        }
        finally
        {
            try
            {
                if (animator)
                {
                    animator.fireEvents = fireEvents;
                    // A pose evaluation must not apply an extra root-motion displacement.
                    animator.transform.SetPositionAndRotation(rootPosition, rootRotation);
                }
            }
            catch (Exception ex)
            {
                success = false;
                if (!entry.ErrorReported)
                {
                    entry.ErrorReported = true;
                    Debug.LogWarning("[CS Pose] Could not restore animation evaluation settings for control " +
                        LegacyCsDiagnostics.Id(player) + ": " + ex);
                }
            }
            finally { evaluating = null; }
        }
        if (success && LegacyCsDiagnostics.Enabled)
            LegacyCsDiagnostics.Safe(() => LegacyCsDiagnostics.Emit("POSE_REBUILD", "control=" + LegacyCsDiagnostics.Id(player) +
                " source=" + entry.Reason + " pass=" + pass + " frame=" + Time.frameCount +
                " deltaTime=0 headBefore=" + LegacyCsDiagnostics.F(headBefore) + " headAfter=" + LegacyCsDiagnostics.F(HeadHeight(player)) +
                " hipsBefore=" + LegacyCsDiagnostics.F(hipsBefore) + " hipsAfter=" + LegacyCsDiagnostics.F(HipsHeight(player)) +
                " culling=" + animator.cullingMode));
        return success;
    }

    private static float HeadHeight(PlayerControl player)
    {
        if (!player || !player.tr_view || !player.ShotBones || player.ShotBones.bones == null) return float.NaN;
        foreach (PlayerShotsBones.Bone bone in player.ShotBones.bones)
            if (bone.bone == HumanBodyBones.Head && bone.tr && bone.offsets != null && bone.offsets.Length > 0)
                return bone.tr.TransformPoint(bone.offsets[0]).y - player.tr_view.position.y;
        return float.NaN;
    }
    private static float HipsHeight(PlayerControl player)
    {
        return player && player.tr_view && player.ragdoll && player.ragdoll.Hips
            ? player.ragdoll.Hips.position.y - player.tr_view.position.y : float.NaN;
    }
    private static void Trace(PlayerControl player, string kind, string details)
    {
        if (!LegacyCsDiagnostics.Enabled) return;
        LegacyCsDiagnostics.Safe(() => LegacyCsDiagnostics.Emit(kind, "control=" + LegacyCsDiagnostics.Id(player) + " " + details));
    }

    internal static void Reset()
    {
        pending.Clear();
        spawns = new ConditionalWeakTable<NetSpawn, SpawnTicket>();
        // Do not clear an in-progress snapshot list or manual-evaluation guard from a callback.
        if (!ticking) work.Clear();
    }
}
