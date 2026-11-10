using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;

internal static class LegacyCsDiagnostics
{
    private sealed class Stamp { internal float Full = -1000f; }
    private static readonly ConditionalWeakTable<PlayerControl, Stamp> stamps = new ConditionalWeakTable<PlayerControl, Stamp>();
    private static LegacyCsTraceSession session;
    private static float nextSnapshot;
    private static bool faultReported;
    internal static bool Enabled { get { return session != null && session.Active; } }
    internal static bool Capturing { get { return Enabled && session.Capturing; } }
    internal static bool InShot { get { return Enabled && session.InShot; } }

    internal static void Initialize()
    {
        if (!LegacyCommandLine.HasArg("-csDiagnostics") || LegacyCommandLine.HasArg("-noLogging")) return;
        try
        {
            int limit, port;
            if (!int.TryParse(LegacyCommandLine.GetArgValue("-csDiagnosticsMaxShotsPerSecond"), out limit) || limit < 1 || limit > 1000) limit = 60;
            if (!int.TryParse(LegacyCommandLine.GetArgValue("-port"), out port) || port < 1 || port > 65535) port = 7800;
            string directory = LegacyCommandLine.GetArgValue("-logsDir");
            if (string.IsNullOrWhiteSpace(directory)) directory = Path.Combine(LegacyHelpers.ModDataPath, "logs");
            else if (!Path.IsPathRooted(directory)) directory = Path.Combine(BepInEx.Paths.GameRootPath, directory);
            bool server = LegacyCommandLine.HasServerArg();
            var writer = new LegacyServerLogWriter(Path.GetFullPath(directory), LegacyServerLogLevel.Log,
                server ? port.ToString(CultureInfo.InvariantCulture) + "_cs_server" : "cs_client");
            session = new LegacyCsTraceSession(writer, limit);
            if (!session.Active) throw new IOException(writer.Failure);
            int processId;
            using (var process = System.Diagnostics.Process.GetCurrentProcess()) processId = process.Id;
            Emit("SESSION_START", "format=1 role=" + (server ? "server" : "client") + " bcl=" + VersionInfo.ModVersionName +
                " game=" + LegacyCompatibility.CurrentGameVersion + " maxShotsPerSecond=" + limit +
                " process=" + processId +
                "; shot IDs are local; wire hashes can repeat for identical shot payloads");
            BigCityLegacyPlugin.Log.LogInfo("CS diagnostics: " + session.FilePath);
        }
        catch (Exception ex)
        {
            session?.Dispose(); session = null;
            BigCityLegacyPlugin.Log.LogWarning("CS diagnostics unavailable: " + ex.Message);
        }
    }

    internal static void Safe(Action action)
    {
        if (!Capturing) return;
        try { action(); }
        catch (Exception ex)
        {
            if (faultReported) return;
            faultReported = true;
            session.Record("DIAGNOSTIC_ERROR", ex.ToString());
        }
    }

    internal static void Emit(string kind, string details, string wire = null)
    {
        if (Capturing) session.Record(kind, "gameMs=" + F(nTime.GameThisFrame) + " netMs=" + NetworkTime.ServerTime +
            " netReady=" + NetworkTime.isReadyToUse + " " + details, wire);
    }

    internal static LegacyCsTraceSession.Scope Begin(Control shooter, Vector3 pos, Vector3 dir, float speed, float mass, Weapone.Type weapon, string stage)
    {
        if (!Enabled) return null;
        LegacyCsTraceSession.Scope scope = null;
        try
        {
            if (Event(shooter) == null) return null;
            string wire = LegacyCsTraceSession.Fingerprint(new[] { pos.x, pos.y, pos.z, dir.x, dir.y, dir.z, speed, mass }, (int)weapon);
            scope = session.Begin(wire, stage, stage == "SIMULATE");
            // A malformed actor snapshot must not discard the surrounding shot
            // context: its ray and damage observations may still be readable.
            Safe(() => Emit(stage + "_DATA", "shooter=" + Id(shooter) + " weapon=" + weapon + " pos=" + V(pos) +
                " dir=" + V(dir) + " dirLength=" + F(dir.magnitude) + " speed=" + F(speed) + " mass=" + F(mass) + " " + State(shooter as PlayerControl)));
            return scope;
        }
        catch { scope?.Dispose(); return null; }
    }

    internal static void End(LegacyCsTraceSession.Scope scope, Exception exception)
    {
        if (scope == null) return;
        // Restoration is unconditional even if a formatter or disk has failed.
        scope.Session.End(scope, exception);
    }

    internal static bool Relevant(Control player) { return Capturing && Event(player) != null; }
    internal static NetInputControl Input(Control player)
    {
        if (!player) return null;
        if (player.input && player.input.netInput) return player.input.netInput;
        return player.net_control ? player.net_control.netInputControl : null;
    }
    private static Net_CS Event(Control player) { var input = Input(player); return input ? input.curEvent as Net_CS : null; }
    internal static string Id(Control player) { return player && player.net_control ? player.net_control.netId.Value.ToString(CultureInfo.InvariantCulture) : "null"; }
    internal static string F(float value) { return value.ToString("R", CultureInfo.InvariantCulture); }
    internal static string V(Vector3 value) { return "(" + F(value.x) + "," + F(value.y) + "," + F(value.z) + ")"; }
    internal static string Q(string value) { return LegacyCsTraceSession.Escape(value); }

    internal static string State(PlayerControl player)
    {
        if (!player) return "player=null";
        var input = Input(player);
        var cs = input ? input.curEvent as Net_CS : null;
        var member = cs && input ? cs.GetPlayerByInput(input) as Net_CS.CsBasePlayer : null;
        var a = player.animator;
        var b = player.body;
        var r = player.ragdoll;
        var armor = player.armor;
        string animation = "none";
        if (a && a.isInitialized && a.layerCount > 0)
        {
            var state = a.GetCurrentAnimatorStateInfo(0);
            animation = state.fullPathHash + ":" + F(state.normalizedTime);
        }
        return "control=" + Id(player) + " input=" + (input ? input.netId.Value.ToString() : "null") +
            " nick=" + Q(input ? input.nikName : null) + " reconnectCount=" + (input ? input.connectionCreateCount : 0) +
            " match=" + (cs ? cs.netId.Value.ToString() : "null") + " state=" + (cs ? cs.state.ToString() : "none") +
            " survival=" + (cs && cs.isSurvivalMode) + " team=" + (member == null || member.data == null ? "null" : member.data.commandID.ToString()) +
            " member=" + (member == null ? "null" : member.id.ToString()) +
            " kills=" + (cs ? cs.kill_count + ":" + cs.kill_count_terrors : "none") +
            " authority=" + (player.net_control && player.net_control.hasAuthority) +
            " live=" + F(player.live) + " body=" + (b ? V(b.position) : "null") + " cachedBody=" + V(player.body_pos) +
            " view=" + (player.tr_view ? V(player.tr_view.position) : "null") + " offset=" + V(OffsetManager.globalAdd) +
            " parent=" + Id(player.parent_control) + " carState=" + (player.InCar == null ? "null" : player.InCar.carState.ToString()) +
            " ragdoll=" + (r ? r.state.ToString() : "null") + " hips=" + (r && r.Hips ? V(r.Hips.position) : "null") +
            " animatorPosition=" + (a ? V(a.transform.position) : "null") + " bodyTrigger=" + (player.body_collider && player.body_collider.isTrigger) +
            " bodyColliderEnabled=" + (player.body_collider && player.body_collider.enabled) + " kinematic=" + (b && b.isKinematic) +
            " viewActive=" + (player.tr_view && player.tr_view.gameObject.activeInHierarchy) + " distType=" + player.Dist2CamType +
            " animatorEnabled=" + (a && a.enabled) + " animatorActive=" + (a && a.gameObject.activeInHierarchy) +
            " culling=" + (a ? a.cullingMode.ToString() : "null") + " updateMode=" + (a ? a.updateMode.ToString() : "null") +
            " animState=" + animation + " animServerAge=" + F(nTime.GameThisFrame - player.lastTimeEnableAnimOnServer) +
            " armor=" + (armor == null ? "null" : armor.bodyArmorLevel + ":" + F(armor.bodyArmorLive)) +
            " protection=" + LegacyCsDamageCooldown.DiagnosticState(player);
    }

    internal static void Snapshot(PlayerControl player, string reason, bool full)
    {
        if (!Relevant(player)) return;
        Emit("PLAYER_STATE", "reason=" + reason + " " + State(player));
        if (!full) return;
        var stamp = stamps.GetValue(player, p => new Stamp());
        if (Time.realtimeSinceStartup - stamp.Full < 1f) return;
        stamp.Full = Time.realtimeSinceStartup;
        var renderers = player.GetComponentsInChildren<Renderer>(true);
        int visible = 0, enabled = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!renderers[i]) continue;
            if (renderers[i].isVisible) visible++;
            if (renderers[i].enabled) enabled++;
        }
        Emit("RENDER_STATE", "control=" + Id(player) + " count=" + renderers.Length + " enabled=" + enabled + " visible=" + visible);
        var bones = player.ShotBones ? player.ShotBones.bones : null;
        Emit("BONE_SET", "control=" + Id(player) + " count=" + (bones == null ? -1 : bones.Length) +
            " componentActive=" + (player.ShotBones && player.ShotBones.isActiveAndEnabled));
        if (bones == null) return;
        for (int i = 0; i < bones.Length && i < 128; i++)
        {
            var bone = bones[i];
            var text = new StringBuilder("control=" + Id(player) + " index=" + i + " bone=" + bone.bone + " radius=" + F(bone.radius));
            text.Append(" transform=").Append(bone.tr ? Q(bone.tr.name) : "null");
            text.Append(" active=").Append(bone.tr && bone.tr.gameObject.activeInHierarchy);
            text.Append(" scale=").Append(bone.tr ? V(bone.tr.lossyScale) : "null");
            text.Append(" offsets=").Append(bone.offsets == null ? -1 : bone.offsets.Length);
            if (bone.tr && bone.offsets != null)
                for (int j = 0; j < bone.offsets.Length && j < 16; j++) text.Append(" center").Append(j).Append('=').Append(V(bone.tr.TransformPoint(bone.offsets[j])));
            Emit("BONE", text.ToString());
        }
    }

    internal static void RayStart(Ray ray, float distance, Control shooter)
    {
        if (!InShot || !Relevant(shooter)) return;
        Emit("RAY_BEGIN", "shooter=" + Id(shooter) + " origin=" + V(ray.origin) + " direction=" + V(ray.direction) + " limit=" + F(distance));
        int nearCount = 0, emitted = 0;
        // Read-only geometric probes: no extra Physics raycasts or bone queries
        // through ProcessShot, which would modify game hit-result structures.
        for (int i = 0; i < Control.controls.Count; i++)
        {
            var target = Control.controls[i] as PlayerControl;
            if (!target || target == shooter || Event(target) != Event(shooter) || !target.tr_view) continue;
            Vector3 center = target.tr_view.TransformPoint(new Vector3(0f, 0.9f, 0f));
            float length = ray.direction.sqrMagnitude;
            float along = length > 0f ? Vector3.Dot(center - ray.origin, ray.direction) / length : -1f;
            float separation = Vector3.Distance(center, ray.GetPoint(Mathf.Clamp(along, 0f, distance)));
            if (separation > 4f) continue;
            nearCount++;
            if (emitted++ >= 8) continue;
            float entry;
            bool gate = ProbeSphere(ray, center, 1f, out entry);
            Emit("CANDIDATE", "target=" + Id(target) + " broadSphere=" + V(center) + " gate=" + gate + " entry=" + F(entry) +
                " perpendicular=" + F(separation) + " parent=" + Id(target.parent_control));
            Snapshot(target, "ray_candidate", true);
        }
        if (nearCount > 8) Emit("CANDIDATES_TRUNCATED", "total=" + nearCount + " shown=8");
    }

    // Same scalar sphere test used by the game, without changing a hit result.
    internal static bool ProbeSphere(Ray ray, Vector3 center, float radius, out float distance)
    {
        Vector3 delta = ray.origin - center;
        float dot = Vector3.Dot(delta, ray.direction), c = Vector3.Dot(delta, delta) - radius * radius;
        float discriminant = dot * dot - c;
        distance = 0f;
        if (c > 0f && dot > 0f || discriminant < 0f) return false;
        distance = Mathf.Max(0f, -dot - Mathf.Sqrt(discriminant));
        return true;
    }

    internal static string Hit(hPhysics.RayShotResult hit)
    {
        var collider = hit.collider;
        return "haveResult=" + hit.haveResult + " target=" + Id(hit.hitPlayer) + " isHitPlayer=" + hit.isHitPlayer +
            " bone=" + hit.hitBone + " dist=" + F(hit.hitDist) + " point=" + V(hit.point) +
            " ragdollDist=" + F(hit.ragDollDist) + " ragdollBone=" + (hit.ragDollItem ? hit.ragDollItem.boneType.ToString() : "null") +
            " collider=" + (collider ? Q(collider.name) : "null") + " colliderType=" + (collider ? collider.GetType().Name : "null") +
            " layer=" + (collider ? collider.gameObject.layer : -1) + " trigger=" + (collider && collider.isTrigger) +
            " colliderActive=" + (collider && collider.gameObject.activeInHierarchy) +
            " impactHandler=" + (collider && collider.GetComponent<ImpactHandler>() != null);
    }

    internal static void Tick()
    {
        if (!Enabled) return;
        Safe(() =>
        {
            session.Tick();
            if (Time.realtimeSinceStartup < nextSnapshot) return;
            nextSnapshot = Time.realtimeSinceStartup + 5f;
            for (int i = 0; i < Control.controls.Count; i++)
            {
                var player = Control.controls[i] as PlayerControl;
                if (Relevant(player)) Snapshot(player, "periodic", true);
            }
        });
    }

    internal static void Shutdown() { session?.Dispose(); session = null; }
}
