using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RootMotion;
using UnityEngine;

/// Small runtime fixes that must not alter the player's persistent client settings.
/// Also fixed a bug where the camera continued to rotate when the cursor was activated by clicking the middle-mouse button.
[HarmonyPatch]
internal static class InputPatches
{
    private static bool _serverSettingsSkipLogged;
    private static bool _transientGraphicsApplied;

    private static readonly FieldInfo _hiddenByMiddleMouseField = AccessTools.Field(typeof(hCursor), "hiddentByMiddleMouse");

    [HarmonyPatch(typeof(Settings), "SettingsMayBeBad")]
    [HarmonyPrefix]
    private static bool Settings_SettingsMayBeBad_Prefix()
    {
        if (!IsDedicatedServerProcess())
        {
            return true;
        }

        LogServerSettingsSkipOnce();
        return false;
    }

    [HarmonyPatch(typeof(Settings), "SettingsAreOk")]
    [HarmonyPrefix]
    private static bool Settings_SettingsAreOk_Prefix()
    {
        return !IsDedicatedServerProcess();
    }

    [HarmonyPatch(typeof(Settings), "CheckWasCrashLastTime")]
    [HarmonyPrefix]
    private static bool Settings_CheckWasCrashLastTime_Prefix()
    {
        if (!IsDedicatedServerProcess())
        {
            return true;
        }

        LogServerSettingsSkipOnce();
        return false;
    }

    [HarmonyPatch(typeof(SettingsValues), "Save")]
    [HarmonyPrefix]
    private static bool SettingsValues_Save_Prefix()
    {
        if (!IsDedicatedServerProcess())
        {
            return true;
        }

        LogServerSettingsSkipOnce();
        return false;
    }

    [HarmonyPatch(typeof(SettingsValues.Render), "Apply")]
    [HarmonyPrefix]
    private static bool SettingsValues_Render_Apply_Prefix()
    {
        return !IsDedicatedServerProcess();
    }

    [HarmonyPatch(typeof(NetManager), "OnStartServer")]
    [HarmonyPostfix]
    private static void NetManager_OnStartServer_Postfix()
    {
        if (!IsDedicatedServerProcess() || _transientGraphicsApplied)
        {
            return;
        }

        _transientGraphicsApplied = true;

        if (SettingsValues.me != null && SettingsValues.me.render != null && SettingsValues.me.render.quality != null)
        {
            SettingsValues.me.render.quality.ForceResetToFastest();
        }

        QualitySettings.streamingMipmapsActive = false;
        QualitySettings.masterTextureLimit = 6;
    }

    [HarmonyPatch(typeof(InputBut), "GetAxis", new[] { typeof(bool) })]
    [HarmonyPostfix]
    private static void InputBut_GetAxis_Postfix(InputBut __instance, bool isHorizontal, ref float __result)
    {
        if (__instance == null || !__instance.isInputKeyBoard || !IsMiddleClickCursorActive())
        {
            return;
        }

        if (!Application.isEditor && InputBut.touchs)
        {
            return;
        }

        float mouseAxis = Nuligine.nowInFocus
            ? Input.GetAxis(isHorizontal ? "Mouse X" : "Mouse Y") * 1.3f
            : 0f;

        if (!isHorizontal && SettingsValues.Touches.InvMouseY.isInvert)
        {
            mouseAxis = -mouseAxis;
        }

        __result -= mouseAxis;
    }

    // Suppress the game binding, leaving Unity's raw mouse events available to IMGUI.
    [HarmonyPatch(typeof(InputBut), "GetInputKey")]
    [HarmonyPrefix]
    private static bool InputBut_GetInputKey_Prefix(InputBut.KeyValue kv, ref float __result)
    {
        if (kv == null || kv.keyBoardKode != KeyCode.Mouse0 || (!IsCursorUnlockedByMiddleMouse() && !LegacyAlertUI.ModalVisible)) return true;
        __result = 0f;
        return false;
    }

    [HarmonyPatch(typeof(FlyCamera), "UpRotate")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> FlyCamera_UpRotate_Transpiler(
    IEnumerable<CodeInstruction> instructions)
    {
        MethodInfo inputGetAxis = AccessTools.Method(
            typeof(Input),
            nameof(Input.GetAxis),
            new[] { typeof(string) }
        );

        MethodInfo filteredGetAxis = AccessTools.Method(
            typeof(InputPatches),
            nameof(GetFlyCameraMouseAxis)
        );

        foreach (CodeInstruction instruction in instructions)
        {
            if (instruction.Calls(inputGetAxis))
            {
                instruction.operand = filteredGetAxis;
            }

            yield return instruction;
        }
    }

    [HarmonyPatch(typeof(FlyCamera), "GetMove")]
    [HarmonyTranspiler]
    private static IEnumerable<CodeInstruction> FlyCamera_GetMove_Transpiler(
    IEnumerable<CodeInstruction> instructions)
    {
        var speedField = AccessTools.Field(
            typeof(FlyCamera), "cam_fly_speed_normal");

        var getSpeed = AccessTools.Method(
            typeof(InputPatches), nameof(GetCapsLockSpeed));

        foreach (var instruction in instructions)
        {
            yield return instruction;

            if (instruction.LoadsField(speedField))
                yield return new CodeInstruction(OpCodes.Call, getSpeed);
        }
    }
    

    [HarmonyPatch(typeof(FlyCamera), "Create")]
    [HarmonyPrefix]
    private static bool FlyCamera_Create_Prefix()
    {
        return !LegacyHelpers.IsCsOrSurvivalMatchRunning;
    }

    private static float GetCapsLockSpeed(float normalSpeed)
    {
        return Input.GetKey(KeyCode.CapsLock) ? 4f : normalSpeed;
    }

    private static float GetFlyCameraMouseAxis(string axisName)
    {
        if (IsMiddleClickCursorActive() &&
            (axisName == "Mouse X" || axisName == "Mouse Y"))
        {
            return 0f;
        }

        return Input.GetAxis(axisName);
    }

    private static bool IsMiddleClickCursorActive()
    {
        return hCursor.NeedHideMouse() && IsCursorUnlockedByMiddleMouse();
    }

    private static bool IsCursorUnlockedByMiddleMouse()
    {
        if (_hiddenByMiddleMouseField == null)
        {
            return false;
        }

        object value = _hiddenByMiddleMouseField.GetValue(null);
        return value is bool && !(bool)value;
    }

    private static bool IsDedicatedServerProcess()
    {
        return NetManagerTools.isCommandLineArgHaveServerStr();
    }

    private static void LogServerSettingsSkipOnce()
    {
        if (_serverSettingsSkipLogged)
        {
            return;
        }

        _serverSettingsSkipLogged = true;
    }
}
