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
        LegacyUserSettingsScroll.Ensure(__instance);
    }
}

[HarmonyPatch(typeof(Reporter), "isGestureDone")]
internal static class SuppressReporterGesturePatch
{
    [HarmonyPrefix]
    private static bool Reporter_IsGestureDone_Prefix(ref bool __result)
    {
        var property = AccessTools.Property(typeof(BigCityLegacy.UI.LegacyUIWindow), "AnyWindowOpen");
        var blocker = typeof(BigCityLegacy.UI.LegacyUIWindow).Assembly.GetType("BigCityLegacy.UI.LegacyUIInputBlocker");
        bool blocked = property != null ? (bool)property.GetValue(null, null) :
            blocker != null && (bool)AccessTools.Method(blocker, "IsMouseBlockedForGameInput").Invoke(null, null);
        if (!blocked)
        {
            return true;
        }

        __result = false;
        return false;
    }
}
