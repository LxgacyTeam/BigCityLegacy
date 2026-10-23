using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;

[HarmonyPatch]
internal static class ServerCarAudioPatches
{
    private static IEnumerable<MethodBase> TargetMethods()
    {
        foreach (string name in new[] { "Update", "SetEnableDamperSound", "SetEnableWindSound",
            "SetEnableEngineNoiseForCar", "SetEnableJetSound", "SetEnableNotRisingSound", "SetEnablePoliceSound", "SetEnableDirtSound" })
            yield return AccessTools.DeclaredMethod(typeof(CarSound), name);
    }

    [HarmonyPrefix]
    private static bool SetAudioEnabled() { return !LegacyServerLog.IsServer; }
}
