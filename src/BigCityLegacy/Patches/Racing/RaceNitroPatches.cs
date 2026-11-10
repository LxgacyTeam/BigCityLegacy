using HarmonyLib;

[HarmonyPatch]
internal static class RaceNitroPatches
{
    private static bool IsBlocked(WeaponeSelectItem item)
    {
        if (item.subType != WeaponeSelectItem.SubTypes.Car_Nitro) return false;
        var race = Net_BaseEvent.curUserInRaceInst as NetRace;
        if (!race || !race.racePoints) return false;
        var runtime = race.racePoints.GetComponent<LegacyOnlineRacePoints>();
        return runtime && !runtime.AllowNitroPurchase;
    }

    [HarmonyPatch(typeof(WeaponeSelectItem), "isShowForCar")]
    [HarmonyPostfix]
    private static void HidePurchase(WeaponeSelectItem __instance, ref bool __result)
    {
        if (IsBlocked(__instance)) __result = false;
    }

    [HarmonyPatch(typeof(WeaponeSelectItem), "doEvent")]
    [HarmonyPrefix]
    private static bool RejectPurchase(WeaponeSelectItem __instance)
    {
        return !IsBlocked(__instance);
    }
}
