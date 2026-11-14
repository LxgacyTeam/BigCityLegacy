using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
internal static class ParkourPatches
{
    private static bool missingTrackReported;

    [HarmonyPatch(typeof(Net_EventStartManager), "StartEventIfNeed")]
    [HarmonyPrefix]
    private static bool StartEventIfNeed()
    {
        if (!NetManager.isServer || !UNet_Settings.me || UNet_Settings.me.lockMode != UNet_Settings.Mode.RaceParcur)
            return true;

        foreach (var list in EventsList.instances)
        {
            if (!list || list.savePostfix != "Parkurs") continue;
            foreach (var item in list.evenTransforms)
            {
                var track = item ? item.GetComponent<RaceDirectionPoints>() : null;
                if (!track || track.dontUseOnline) continue;
                missingTrackReported = false;
                return true;
            }
        }

        if (!missingTrackReported)
        {
            missingTrackReported = true;
            Debug.LogWarning("[BCL Events] RaceParcur needs at least one online track in Parkurs.");
        }
        return false;
    }
}
