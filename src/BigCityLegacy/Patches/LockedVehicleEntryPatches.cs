using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

[HarmonyPatch]
internal static class LockedVehicleEntryPatches
{
    [HarmonyPatch(typeof(NetControl), "SetParentControl", new[] { typeof(Control.SetParentData) })]
    [HarmonyPrefix]
    private static bool BeforeSetParent(NetControl __instance, Control.SetParentData data)
    {
        if (!NetManager.isServer || !NetworkServer.active || !__instance) return true;

        PlayerControl player = __instance.control as PlayerControl;
        CarControl car = data.control as CarControl;
        if (!player || !car || player.parent_control == car) return true;

        NetControl carNet = car.net_control;
        if (!carNet || !carNet.isLocked) return true;

        NetInputControl input = player.input ? player.input.netInput : null;
        if (!input) return true;
        if (!input.isConnected || !input.netConnect) return false;

        bool nearCar = (player.body_pos - car.body_pos).sqrMagnitude <= 225f;
        bool validSeat = false;
        if (car.sitdowns != null)
            foreach (Sitdown seat in car.sitdowns)
                if (seat && seat.sitID == data.sitID) { validSeat = true; break; }

        Debug.LogWarning("[Server] Locked vehicle entry rejected: player='" + input.nikName +
            "' GUID=" + input.pirvateConnectID + " car='" + carNet.objName +
            "' netId=" + carNet.netId.Value + " near=" + nearCar + " seat=" + validSeat + ".");

        //if (nearCar && validSeat)
        //{
        //    int ejected;
        //    string error;
        //    if (!LegacyServerAdminService.TryDeleteCar(carNet, out ejected, out error))
        //        Debug.LogError("[Server] Failed to delete locked vehicle after invalid entry: " + error);
        //}

        LegacyServerAdminService.KickForLockedVehicle(input);
        return false;
    }
}
