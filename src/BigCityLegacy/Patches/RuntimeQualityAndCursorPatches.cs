using System.Reflection;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

/// Small runtime fixes that must not alter the player's persistent client settings.
/// Also fixed a bug where the camera continued to rotate when the cursor was activated by clicking the middle-mouse button.
[HarmonyPatch]
internal static class RuntimeQualityAndCursorPatches
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

    [HarmonyPatch]
    private static class InputBut_BlockLeftClickFireWhenCursorUnlocked
    {
        private static readonly FieldInfo KeyBoardCodeField =
            AccessTools.Field(
                AccessTools.Method(typeof(InputBut), "GetInputKey").GetParameters()[0].ParameterType,
                "keyBoardKode"
            );

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(InputBut), "GetInputKey");
        }

        private static bool Prefix(object kv, int typeOfButton, ref float __result)
        {
            if (!IsMiddleClickCursorActive())
            {
                return true;
            }

            bool isFireButton =
                typeOfButton == (int)InputBut.But.Car_Fire ||
                typeOfButton == (int)InputBut.But.Human_Fire;

            if (!isFireButton || kv == null || KeyBoardCodeField == null)
            {
                return true;
            }

            object keyValue = KeyBoardCodeField.GetValue(kv);

            if (!(keyValue is KeyCode) || (KeyCode)keyValue != KeyCode.Mouse0)
            {
                return true;
            }

            __result = 0f;
            return false;
        }
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
            typeof(RuntimeQualityAndCursorPatches),
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

    [HarmonyPatch(typeof(FlyCamera), "Create")]
    [HarmonyPrefix]
    private static bool FlyCamera_Create_Prefix()
    {
        return !LegacyHelpers.IsCsOrSurvivalMatchRunning();
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
        if (!hCursor.NeedHideMouse())
        {
            return false;
        }

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
