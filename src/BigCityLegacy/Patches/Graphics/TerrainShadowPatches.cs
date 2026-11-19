using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;

[HarmonyPatch(typeof(GroundItem), "SetObject")]
internal static class TerrainShadowPatches
{
    private static void Postfix(GroundItem __instance)
    {
        if (!__instance.Instanciated) return;
        Apply(__instance.Instanciated.transform);
    }
    internal static bool Excluded(Transform value)
    {
        for (Transform parent = value; parent; parent = parent.parent)
            if (parent.name.StartsWith("Minimap", System.StringComparison.OrdinalIgnoreCase) ||
                parent.name.Replace("_", "").StartsWith("GroundLod", System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
    internal static void Apply(Transform chunk)
    {
        foreach (MeshRenderer renderer in chunk.GetComponentsInChildren<MeshRenderer>(true))
            if (!Excluded(renderer.transform)) renderer.shadowCastingMode = ShadowCastingMode.On;
    }
}

[HarmonyPatch(typeof(GroundLoader), "Start")]
internal static class ExistingTerrainShadowPatches
{
    private static void Postfix(GroundLoader __instance) { TerrainShadowPatches.Apply(__instance.transform); }
}
