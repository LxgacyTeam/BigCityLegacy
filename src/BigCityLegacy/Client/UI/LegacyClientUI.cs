using BigCityLegacy.UI;
using BigCityLegacy.UI.Blur;
using UnityEngine;

internal static class LegacyClientUI
{
    internal static readonly LegacyUIAnimationSettings Animations = new LegacyUIAnimationSettings();
    private static LegacyBclBlur backdrop;

    internal static void Initialize(GameObject host)
    {
        if (!backdrop) backdrop = LegacyBclBlur.Create(host, null, false);
    }

    internal static void Panel(Rect rect, float alpha)
    {
        bool blurred = backdrop && backdrop.Draw(rect);
        LegacyUI.Panel(rect, blurred ? alpha * 0.5f : alpha);
    }

    internal static void Shutdown() { if (backdrop) Object.Destroy(backdrop); backdrop = null; }

    internal static LegacyUIGuiScope Scope(int depth, bool useBlur = true)
    {
        return new LegacyUIGuiScope(depth, null, Animations, useBlur ? backdrop : null);
    }
}
