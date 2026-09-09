using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

[HarmonyPatch]
internal static class SettingsMenuPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        string[] methodNames =
        {
            "Awake",
            "Start",
            "OnEnable",
            "LateUpdate"
        };

        for (int i = 0; i < methodNames.Length; i++)
        {
            MethodInfo method = AccessTools.DeclaredMethod(typeof(MenuGroup), methodNames[i]);
            if (method != null)
            {
                yield return method;
            }
        }
    }

    private static void Postfix(MenuGroup __instance)
    {
        LegacySettingsMenuHelper.ApplyToGroup(__instance);
    }
}

[HarmonyPatch(typeof(Reporter), "isGestureDone")]
internal static class SuppressReporterGesturePatch
{
    [HarmonyPrefix]
    private static bool Reporter_IsGestureDone_Prefix(ref bool __result)
    {
        if (!LegacyAboutWindow.SuppressReporterGesture)
        {
            return true;
        }

        __result = false;
        return false;
    }
}
