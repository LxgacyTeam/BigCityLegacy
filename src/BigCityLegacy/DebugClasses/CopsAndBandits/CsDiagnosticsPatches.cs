using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

// Observers only: never replace a result, modify an argument, call a damage
// check twice, advance animation, synchronize Physics, or consume packets.
[HarmonyPatch]
internal static class CsDiagnosticsPatches
{
    private sealed class Health { internal float Before; }

    [HarmonyPatch(typeof(NetSyncPlayer), "Cmd_Shot")]
    [HarmonyPrefix]
    private static void Receive(NetSyncPlayer __instance, Vector3 pos, Vector3 dir, float bulletStartSpeed, float bulletMass,
        Weapone.Type type, out LegacyCsTraceSession.Scope __state)
    {
        __state = LegacyCsDiagnostics.Begin(__instance.netControl ? __instance.netControl.control : null,
            pos, dir, bulletStartSpeed, bulletMass, type, "COMMAND");
    }
    [HarmonyPatch(typeof(NetSyncPlayer), "Cmd_Shot")]
    [HarmonyFinalizer]
    private static Exception ReceiveEnd(LegacyCsTraceSession.Scope __state, Exception __exception)
    {
        LegacyCsDiagnostics.End(__state, __exception); return __exception;
    }

    [HarmonyPatch(typeof(NetSyncPlayer), "CallCmd_Shot")]
    [HarmonyPrefix]
    private static void Send(NetSyncPlayer __instance, Vector3 pos, Vector3 dir, float bulletStartSpeed, float bulletMass, Weapone.Type type)
    {
        LegacyCsDiagnostics.Safe(() =>
        {
            var actor = __instance.netControl ? __instance.netControl.control : null;
            if (!LegacyCsDiagnostics.Relevant(actor)) return;
            string wire = LegacyCsTraceSession.Fingerprint(new[] { pos.x, pos.y, pos.z, dir.x, dir.y, dir.z, bulletStartSpeed, bulletMass }, (int)type);
            LegacyCsDiagnostics.Emit("SHOT_SEND", "shooter=" + LegacyCsDiagnostics.Id(actor) + " weapon=" + type +
                " pos=" + LegacyCsDiagnostics.V(pos) + " dir=" + LegacyCsDiagnostics.V(dir), wire);
        });
    }

    [HarmonyPatch(typeof(Shots), "Add")]
    [HarmonyPrefix]
    private static void Simulate(Vector3 pos, Vector3 dir, float speed, float mass, WhoKill who, Weapone.Type weaponeType,
        out LegacyCsTraceSession.Scope __state)
    {
        __state = LegacyCsDiagnostics.Begin(who.control, pos, dir, speed, mass, weaponeType, "SIMULATE");
    }
    [HarmonyPatch(typeof(Shots), "Add")]
    [HarmonyFinalizer]
    private static Exception SimulateEnd(LegacyCsTraceSession.Scope __state, Exception __exception)
    {
        LegacyCsDiagnostics.End(__state, __exception); return __exception;
    }

    [HarmonyPatch(typeof(NetControlWeapones), "MinusOne")]
    [HarmonyPostfix]
    private static void Ammo(NetControlWeapones __instance, bool __result)
    {
        if (!LegacyCsDiagnostics.InShot) return;
        LegacyCsDiagnostics.Safe(() => LegacyCsDiagnostics.Emit("AMMO", "accepted=" + __result + " selected=" + __instance.selectWeapone));
    }

    [HarmonyPatch(typeof(hPhysics), "RaycastShot")]
    [HarmonyPrefix]
    private static void RayBegin(Ray ray, float dist, Control ourControl)
    {
        LegacyCsDiagnostics.Safe(() => LegacyCsDiagnostics.RayStart(ray, dist, ourControl));
    }
    [HarmonyPatch(typeof(hPhysics), "RaycastShot")]
    [HarmonyPostfix]
    private static void RayEnd(Control ourControl, hPhysics.RayShotResult __result)
    {
        if (!LegacyCsDiagnostics.InShot) return;
        LegacyCsDiagnostics.Safe(() =>
        {
            if (LegacyCsDiagnostics.Relevant(ourControl)) LegacyCsDiagnostics.Emit("RAY_RESULT", LegacyCsDiagnostics.Hit(__result));
        });
    }

    [HarmonyPatch(typeof(PlayerShotsBones), "ProcessShot")]
    [HarmonyPrefix]
    private static void BonesBefore(PlayerShotsBones __instance, hPhysics.RayShotResult shot)
    {
        if (!LegacyCsDiagnostics.InShot) return;
        LegacyCsDiagnostics.Safe(() =>
        {
            if (!LegacyCsDiagnostics.Relevant(__instance.playerControl)) return;
            LegacyCsDiagnostics.Emit("BONES_TEST", "target=" + LegacyCsDiagnostics.Id(__instance.playerControl) + " inputLimit=" + LegacyCsDiagnostics.F(shot.hitDist));
            LegacyCsDiagnostics.Snapshot(__instance.playerControl, "bones_test", true);
        });
    }
    [HarmonyPatch(typeof(PlayerShotsBones), "ProcessShot")]
    [HarmonyPostfix]
    private static void BonesAfter(PlayerShotsBones __instance, hPhysics.RayShotResult shot, bool __result)
    {
        if (!LegacyCsDiagnostics.InShot) return;
        LegacyCsDiagnostics.Safe(() =>
        {
            if (LegacyCsDiagnostics.Relevant(__instance.playerControl)) LegacyCsDiagnostics.Emit("BONES_RESULT",
                "target=" + LegacyCsDiagnostics.Id(__instance.playerControl) + " accepted=" + __result + " " + LegacyCsDiagnostics.Hit(shot));
        });
    }

    [HarmonyPatch(typeof(PlayerControl), "ImpactCallBack")]
    [HarmonyPrefix]
    private static void Impact(PlayerControl __instance, ImpactHandler.Data data)
    {
        LegacyCsDiagnostics.Safe(() =>
        {
            if (LegacyCsDiagnostics.Relevant(__instance)) LegacyCsDiagnostics.Emit("IMPACT", "target=" + LegacyCsDiagnostics.Id(__instance) +
                " shooter=" + LegacyCsDiagnostics.Id(data.who.control) + " type=" + data.type + " damage=" + LegacyCsDiagnostics.F(data.humanDamage) +
                " impulse=" + LegacyCsDiagnostics.F(data.impVal) + " position=" + LegacyCsDiagnostics.V(data.pos));
        });
    }

    [HarmonyPatch(typeof(PlayerControl), "MinusLive")]
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void DamageBefore(PlayerControl __instance, float val, WhoKill who, out Health __state)
    {
        Health state = null;
        LegacyCsDiagnostics.Safe(() =>
        {
            if (!LegacyCsDiagnostics.Relevant(__instance)) return;
            state = new Health { Before = __instance.live };
            LegacyCsDiagnostics.Emit("DAMAGE_ATTEMPT", "target=" + LegacyCsDiagnostics.Id(__instance) + " shooter=" + LegacyCsDiagnostics.Id(who.control) +
                " requested=" + LegacyCsDiagnostics.F(val) + " " + LegacyCsDiagnostics.State(__instance));
        });
        __state = state;
    }
    [HarmonyPatch(typeof(PlayerControl), "MinusLive")]
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void DamageAfter(PlayerControl __instance, Health __state, bool __result, bool __runOriginal)
    {
        if (__state == null) return;
        LegacyCsDiagnostics.Safe(() => LegacyCsDiagnostics.Emit("DAMAGE_RESULT", "target=" + LegacyCsDiagnostics.Id(__instance) +
            " before=" + LegacyCsDiagnostics.F(__state.Before) + " after=" + LegacyCsDiagnostics.F(__instance.live) +
            " originalRan=" + __runOriginal + " returnedAlive=" + __result));
    }

    [HarmonyPatch(typeof(Control), "SetLive")]
    [HarmonyPrefix]
    private static void SetLiveBefore(Control __instance, float value, bool isInitLive, out Health __state)
    {
        Health state = null;
        LegacyCsDiagnostics.Safe(() =>
        {
            if (!LegacyCsDiagnostics.Relevant(__instance)) return;
            state = new Health { Before = __instance.live };
            LegacyCsDiagnostics.Emit("SET_LIVE", "target=" + LegacyCsDiagnostics.Id(__instance) + " requested=" + LegacyCsDiagnostics.F(value) + " init=" + isInitLive);
        });
        __state = state;
    }
    [HarmonyPatch(typeof(Control), "SetLive")]
    [HarmonyPostfix]
    private static void SetLiveAfter(Control __instance, Health __state)
    {
        if (__state != null) LegacyCsDiagnostics.Safe(() => LegacyCsDiagnostics.Emit("SET_LIVE_RESULT", "target=" + LegacyCsDiagnostics.Id(__instance) +
            " before=" + LegacyCsDiagnostics.F(__state.Before) + " after=" + LegacyCsDiagnostics.F(__instance.live)));
    }

    [HarmonyPatch(typeof(NetControl), "isFriendlyFire")]
    [HarmonyPostfix]
    private static void Friendly(NetControl __instance, bool __result)
    {
        LegacyCsDiagnostics.Safe(() =>
        {
            if (LegacyCsDiagnostics.Relevant(__instance.control)) LegacyCsDiagnostics.Emit("FRIENDLY_FIRE_DECISION", "target=" + LegacyCsDiagnostics.Id(__instance.control) + " blocked=" + __result);
        });
    }

    [HarmonyPatch(typeof(NetControl), "Rpc_SetLive")]
    [HarmonyPostfix]
    private static void RpcLive(NetControl __instance, float __0, bool __1)
    {
        LegacyCsDiagnostics.Safe(() =>
        {
            if (LegacyCsDiagnostics.Relevant(__instance.control)) LegacyCsDiagnostics.Emit("HEALTH_RPC", "target=" + LegacyCsDiagnostics.Id(__instance.control) +
                " advertised=" + LegacyCsDiagnostics.F(__0) + " init=" + __1 + " " + LegacyCsDiagnostics.State(__instance.control as PlayerControl));
        });
    }

    [HarmonyPatch(typeof(NetControl), "CallRpc_SetLive")]
    [HarmonyPrefix]
    private static void RpcSend(NetControl __instance, float __0, bool __1)
    {
        LegacyCsDiagnostics.Safe(() =>
        {
            if (LegacyCsDiagnostics.Relevant(__instance.control)) LegacyCsDiagnostics.Emit("HEALTH_RPC_SEND", "target=" + LegacyCsDiagnostics.Id(__instance.control) +
                " advertised=" + LegacyCsDiagnostics.F(__0) + " init=" + __1);
        });
    }

    [HarmonyPatch(typeof(PlayerArmor), "GetBulletHitMul")]
    [HarmonyPostfix]
    private static void Armor(PlayerArmor __instance, HumanBodyBones hitHitBone, float damage, float __result)
    {
        if (!LegacyCsDiagnostics.InShot) return;
        LegacyCsDiagnostics.Safe(() =>
        {
            if (LegacyCsDiagnostics.Relevant(__instance.player)) LegacyCsDiagnostics.Emit("ARMOR", "target=" + LegacyCsDiagnostics.Id(__instance.player) +
                " bone=" + hitHitBone + " damage=" + LegacyCsDiagnostics.F(damage) + " multiplier=" + LegacyCsDiagnostics.F(__result) +
                " remaining=" + LegacyCsDiagnostics.F(__instance.bodyArmorLive) + " level=" + __instance.bodyArmorLevel);
        });
    }
}

[HarmonyPatch]
internal static class CsDiagnosticsDamageCalculationPatch
{
    private static MethodBase TargetMethod() { return AccessTools.DeclaredMethod(AccessTools.Inner(typeof(Shots), "Item"), "CaclHumanDamage"); }
    [HarmonyPostfix]
    private static void Calculated(hPhysics.RayShotResult hit, float __result)
    {
        if (!LegacyCsDiagnostics.InShot) return;
        LegacyCsDiagnostics.Safe(() => LegacyCsDiagnostics.Emit("CALCULATED_DAMAGE", "damage=" + LegacyCsDiagnostics.F(__result) + " " + LegacyCsDiagnostics.Hit(hit)));
    }
}

[HarmonyPatch]
internal static class CsDiagnosticsImpactDeliveryPatch
{
    private static MethodBase TargetMethod() { return AccessTools.DeclaredMethod(AccessTools.Inner(typeof(Shots), "Item"), "ShotTo"); }
    [HarmonyPostfix]
    private static void Delivered(hPhysics.RayShotResult hit, ImpactHandler.Data ___data)
    {
        if (!LegacyCsDiagnostics.InShot) return;
        LegacyCsDiagnostics.Safe(() => LegacyCsDiagnostics.Emit("IMPACT_DELIVERY", LegacyCsDiagnostics.Hit(hit) +
            " sharedDamage=" + LegacyCsDiagnostics.F(___data.humanDamage) + " sharedRagdollInside=" + ___data.isHitRagdollInside +
            " sharedRagdollTarget=" + LegacyCsDiagnostics.Id(___data.hitRagDollItem ? ___data.hitRagDollItem.player : null)));
    }
}

[HarmonyPatch]
internal static class CsDiagnosticsLifecyclePatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "Spawn", "SetRespawn", "Cmd_GroundUnderSpawnLoadedCanRemoveHold" }) yield return AccessTools.DeclaredMethod(typeof(NetSpawn), name);
        foreach (string name in new[] { "ResetAll", "Dist2CamWasChanget", "EventAllMeshesLoaded" }) yield return AccessTools.DeclaredMethod(typeof(PlayerControl), name);
        yield return AccessTools.DeclaredMethod(typeof(Net_BaseEvent), "SetState");
        yield return AccessTools.DeclaredMethod(typeof(Net_BaseEvent), "SetStateClient");
    }
    [HarmonyPostfix, HarmonyPriority(Priority.Last)]
    private static void Changed(object __instance, MethodBase __originalMethod)
    {
        LegacyCsDiagnostics.Safe(() =>
        {
            var cs = __instance as Net_CS;
            if (cs != null) LegacyCsDiagnostics.Emit("MATCH_STATE", "match=" + cs.netId.Value + " method=" + __originalMethod.Name + " state=" + cs.state + " kills=" + cs.kill_count + ":" + cs.kill_count_terrors);
            PlayerControl player = __instance as PlayerControl;
            var spawn = __instance as NetSpawn;
            if (spawn && spawn.netInput && spawn.netInput.input) player = spawn.netInput.input.currentPlayer;
            if (player) LegacyCsDiagnostics.Snapshot(player, __originalMethod.Name, true);
        });
    }
}
