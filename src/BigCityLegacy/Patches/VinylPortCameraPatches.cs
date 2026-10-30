using HarmonyLib;
using UnityEngine;

/// <summary>Use the same native preview-camera exclusion path as ECT.</summary>
[HarmonyPatch]
internal static class VinylPortCameraPatches
{
    private static bool IsGarageActive()
    {
        var garage = CarGarageUI.me;
        return garage && garage.gameObject.activeInHierarchy && garage.top_panel && garage.top_panel.cur == garage;
    }

    [HarmonyPatch(typeof(Menu_RotCamIgnore), "isIntersect", new[] { typeof(Vector2) })]
    [HarmonyPostfix]
    private static void IgnoreDragHandle(Vector2 mouseOrTouchPos, ref bool __result)
    {
        if (__result || !IsGarageActive()) return;
        // Native Update sees MouseDown before IMGUI captures the header. Exclude
        // that initial press too, then keep blocking throughout the drag gesture.
        if (LegacyVinylPort.IsDraggingWindow || LegacyVinylPort.IsDragHandleAtScreenPoint(mouseOrTouchPos))
            __result = true;
    }

    [HarmonyPatch(typeof(Menu_RotCamUI), "RotateByMouse")]
    [HarmonyPrefix]
    private static bool BlockRotationWhileDragging()
    {
        return !IsGarageActive() || !LegacyVinylPort.IsDraggingWindow;
    }
}
