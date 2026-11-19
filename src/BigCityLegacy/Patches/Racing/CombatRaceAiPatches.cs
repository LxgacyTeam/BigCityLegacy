using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
internal static class CombatRaceAiPatches
{
    private static AI_Race GetCombatBot(InputControl input)
    {
        var race = Race.cur;
        if (!race || !race.isHaveFireZone() || !input || input.type != InputControl.Type.Ai ||
            !input.ai.isAiRacing() || !race.racers.Contains(input))
            return null;

        var bot = input.ai.GetAiRacing();
        return bot && !bot.is_finish ? bot : null;
    }

    [HarmonyPatch(typeof(CarControl), "UpNeedSkipUpPhysics")]
    [HarmonyPrefix]
    private static bool RunPhysicsOffCamera(CarControl __instance, ref bool __result)
    {
        if (__instance.net_control || !GetCombatBot(__instance.GetHandleInput()))
            return true;

        __result = false;
        return false;
    }

    [HarmonyPatch(typeof(AI_Race_MoveTo), "Update")]
    [HarmonyPrefix]
    private static void RecalculateDrivingLine(AI_Race_MoveTo __instance)
    {
        if (!GetCombatBot(__instance.input)) return;

        if (__instance.last_up_inex >= 4)
            __instance.last_up_inex = 9;
    }

    [HarmonyPatch(typeof(AI_Race_CalcSpeed), "GetValueWaitCarByDist")]
    [HarmonyPrefix]
    private static bool SkipWaiting(AI_Race_CalcSpeed __instance, ref float __result)
    {
        if (!GetCombatBot(__instance.input))
            return true;

        __result = 1f;
        return false;
    }

    [HarmonyPatch(typeof(AI_Race_CalcSpeed), "CaclFakeSpeed")]
    [HarmonyPrefix]
    private static bool KeepWeaponsAvailable(AI_Race_CalcSpeed __instance, AI_Race racing)
    {
        if (!GetCombatBot(__instance.input))
            return true;

        __instance.add_fake_speed = 0f;
        racing.is_real_far = false;
        return false;
    }
}
