using HarmonyLib;
using Smooth;
using UnityEngine;
using UnityEngine.Networking;

[HarmonyPatch]
internal static class GazelVehiclePatches
{
    [HarmonyPatch(typeof(CarControl), "Start")]
    [HarmonyPostfix]
    private static void AddVehicleFeatures(CarControl __instance)
    {
        switch (__instance.gameObject.name.Replace("(Clone)", ""))
        {
            case "Gazel_3302":
                LegacyGazelCanopy.Attach(__instance);
                break;
            case "Gazel_Avtovoz_3302":
                if (!__instance.GetComponent<LegacyGazelCarrier>())
                    __instance.gameObject.AddComponent<LegacyGazelCarrier>();
                break;
        }
    }

    [HarmonyPatch(typeof(NetManager), "Start")]
    [HarmonyPostfix]
    private static void AddUpdateLoop(NetManager __instance)
    {
        LegacyGazelCanopy.PrepareMaterial();
        if (!__instance.GetComponent<LegacyGazelCarrierInput>())
            __instance.gameObject.AddComponent<LegacyGazelCarrierInput>();
    }

    [HarmonyPatch(typeof(CarControl), "UpNeedSkipUpPhysics")]
    [HarmonyPrefix]
    private static bool HoldCargoPhysics(CarControl __instance, ref bool __result)
    {
        if (!LegacyGazelCarrier.Attached.ContainsKey(__instance)) return true;
        __result = true;
        return false;
    }

    [HarmonyPatch(typeof(SmoothSync), "applyInterpolationOrExtrapolation")]
    [HarmonyPrefix]
    private static bool FollowCarrier(SmoothSync __instance)
    {
        return !(__instance.control is CarControl car) || !LegacyGazelCarrier.Attached.ContainsKey(car);
    }

    [HarmonyPatch(typeof(hPhysics), "CamRayCast")]
    [HarmonyPrefix]
    private static void IgnoreCoupledCar(Transform ignoreTr, ref Transform ignoreTrParent)
    {
        if (!ignoreTr || ignoreTrParent) return;
        var car = ignoreTr.GetComponent<CarControl>();
        if (!car) return;

        if (LegacyGazelCarrier.Attached.TryGetValue(car, out var carrier) && carrier && carrier.Truck)
        {
            ignoreTrParent = carrier.Truck.transform;
            return;
        }

        carrier = car.GetComponent<LegacyGazelCarrier>();
        if (carrier && carrier.Cargo)
            ignoreTrParent = carrier.Cargo.transform;
    }

    [HarmonyPatch(typeof(NetManager), "OnStartServer")]
    [HarmonyPostfix]
    private static void StartServer() { LegacyGazelCarrierNetwork.StartServer(); }

    [HarmonyPatch(typeof(NetManager), "OnStopServer")]
    [HarmonyPostfix]
    private static void StopServer() { LegacyGazelCarrierNetwork.StopServer(); }

    [HarmonyPatch(typeof(NetManager), "OnServerDisconnect")]
    [HarmonyPostfix]
    private static void Forget(NetworkConnection conn) { LegacyGazelCarrierNetwork.Forget(conn); }

    [HarmonyPatch(typeof(NetManager), "OnStartClient")]
    [HarmonyPostfix]
    private static void StartClient(NetworkClient client) { LegacyGazelCarrierNetwork.StartClient(client); }

    [HarmonyPatch(typeof(NetManager), "OnStopClient")]
    [HarmonyPostfix]
    private static void StopClient() { LegacyGazelCarrierNetwork.StopClient(); }

    [HarmonyPatch(typeof(NetConnect), "InitInputControl")]
    [HarmonyPostfix]
    private static void Advertise(NetConnect __instance)
    {
        if (!NetworkServer.active || __instance.conn == null || !__instance.netInput) return;
        var player = __instance.netInput.onServerNetPlayer;
        if (player && player.syncPlayer)
            player.syncPlayer.CallTarget_EventMessageHello(__instance.conn, LegacyGazelCarrierNetwork.Capability);
    }

    [HarmonyPatch(typeof(NetSyncPlayer), "Target_EventMessageHello")]
    [HarmonyPrefix]
    private static bool AcceptCapability(NetSyncPlayer __instance, string command)
    {
        if (command != LegacyGazelCarrierNetwork.Capability) return true;
        if (__instance.netControl && __instance.netControl.netInputControl &&
            __instance.netControl.netInputControl.hasAuthority)
            LegacyGazelCarrierNetwork.AcceptCapability();
        return false;
    }

    [HarmonyPatch(typeof(NetSyncPlayer), "Cmd_EventMessageHello")]
    [HarmonyPrefix]
    private static bool RejectClientAnnouncement(string command)
    {
        return command != LegacyGazelCarrierNetwork.Capability;
    }
}
