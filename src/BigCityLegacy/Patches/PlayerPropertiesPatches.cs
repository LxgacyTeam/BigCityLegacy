using HarmonyLib;
using Smooth;
using UnityEngine;

[HarmonyPatch]
internal static class PlayerPropertiesPatches
{
    [HarmonyPatch(typeof(NetSyncPlayer), "Cmd_Shot")]
    [HarmonyPrefix]
    private static bool Shot(NetSyncPlayer __instance)
    { return !__instance.netControl || !LegacyPlayerProperties.Has(__instance.netControl.netInputControl, "noweapon"); }

    [HarmonyPatch(typeof(NetInputControl), "Cmd_DropBomb")]
    [HarmonyPatch(typeof(NetInputControl), "Cmd_BombActivate")]
    [HarmonyPrefix]
    private static bool Bomb(NetInputControl __instance) { return !LegacyPlayerProperties.Has(__instance, "noweapon"); }

    [HarmonyPatch(typeof(NetControlWeapones), "SetSelectWepone")]
    [HarmonyPrefix]
    private static void SelectWeapon(NetControlWeapones __instance, ref Weapone.Type setSelect)
    {
        if (__instance.netControl && LegacyPlayerProperties.Has(__instance.netControl.netInputControl, "noweapon")) setSelect = Weapone.Type.Player_Hand;
    }

    [HarmonyPatch(typeof(InputBut), "ButChange")]
    [HarmonyPrefix]
    private static void Buttons(InputBut __instance, int but_index, ref float set_val)
    {
        if (!__instance.input) return;
        InputBut.But button = (InputBut.But)but_index;
        bool movement = button == InputBut.But.EnterToVehice || button == InputBut.But.Human_Jump || button == InputBut.But.Human_Walk ||
            (button >= InputBut.But.Human_Up && button <= InputBut.But.Human_Right) ||
            (button >= InputBut.But.Car_Gaz && button <= InputBut.But.Car_Right);
        if (movement && LegacyPlayerProperties.Has(__instance.input.netInput, "nomove")) set_val = 0f;
        if ((button == InputBut.But.Human_Fire || button == InputBut.But.Car_Fire || button == InputBut.But.Car_Mine || button == InputBut.But.Ditonator) &&
            LegacyPlayerProperties.Has(__instance.input.netInput, "noweapon")) set_val = 0f;
    }

    [HarmonyPatch(typeof(NetControl), "Cmd_TrySetParent")]
    [HarmonyPrefix]
    private static bool EnterVehicle(NetControl __instance)
    { return !LegacyPlayerProperties.Has(__instance.netInputControl, "nomove"); }

    [HarmonyPatch(typeof(NetInputControl), "Cmd_Move2Point")]
    [HarmonyPrefix]
    private static void MoveToPoint(NetInputControl __instance, ref InputBut.Move2Point setMove2Point)
    { if (LegacyPlayerProperties.Has(__instance, "nomove")) setMove2Point.enabled = false; }

    [HarmonyPatch(typeof(SmoothSync), "addState")]
    [HarmonyPrefix]
    private static void Movement(SmoothSync __instance, ref State state) { LegacyPlayerProperties.FilterState(__instance, ref state); }

    [HarmonyPatch(typeof(PlayerControl), "mFixedUpdate")]
    [HarmonyPostfix]
    private static void FixedMovement(PlayerControl __instance) { LegacyPlayerProperties.PinPlayer(__instance); }

    [HarmonyPatch(typeof(NetSpawn), "Spawn")]
    [HarmonyPostfix]
    private static void Spawn(NetSpawn __instance, Vector3 p, Quaternion r) { LegacyPlayerProperties.OnSpawn(__instance, p, r); }

    [HarmonyPatch(typeof(NetSpawn), "Target_StartSpawn")]
    [HarmonyPostfix]
    private static void ClientSpawn(NetSpawn __instance, Vector3 setSpawnPos, Quaternion setSpawnRot)
    { LegacyPlayerProperties.OnSpawn(__instance, setSpawnPos, setSpawnRot); }

    [HarmonyPatch(typeof(NetSyncPlayer), "Target_EventMessageHello")]
    [HarmonyPostfix]
    private static void Receive(NetSyncPlayer __instance, string command)
    {
        // A normal client command can relay a string on its own player object. Accept UI
        // hints only on this recipient's authority object, which other clients cannot own.
        if (__instance.netControl && __instance.netControl.netInputControl && __instance.netControl.netInputControl.hasAuthority)
            LegacyPlayerProperties.Receive(command);
    }

    [HarmonyPatch(typeof(NetSyncPlayer), "Cmd_EventMessageHello")]
    [HarmonyPrefix]
    private static bool RejectForgedHints(string command)
    { return command == null || !command.StartsWith("BCL.admin.props.v1|", System.StringComparison.Ordinal); }

    [HarmonyPatch(typeof(NetManager), "OnStopClient")]
    [HarmonyPostfix]
    private static void Disconnect() { LegacyPlayerProperties.ClearClient(); }

    [HarmonyPatch(typeof(NetInputControlUI), "DisableIconByEventIfNeed")]
    [HarmonyPostfix]
    private static void Icons(NetInputControlUI __instance)
    {
        if (!LegacyPlayerProperties.Has(__instance.netInput, "invisible")) return;
        __instance.isHideIconAndNikName = true;
        if (__instance.icon) Object.Destroy(__instance.icon);
        __instance.icon = null;
        if (__instance.cretaedPlayerUI) __instance.cretaedPlayerUI.SetActive(false);
    }

    [HarmonyPatch(typeof(NetInputControlUI), "UpdateNikNamePosImp")]
    [HarmonyPrefix]
    private static bool Overhead(NetInputControlUI __instance, ref bool __result)
    {
        if (!LegacyPlayerProperties.Has(__instance.netInput, "invisible")) return true;
        __result = false;
        return false;
    }
}
