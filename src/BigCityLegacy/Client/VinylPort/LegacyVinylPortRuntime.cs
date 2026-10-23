using System;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

internal static class LegacyVinylPortRuntime
{
    private const string TuningId = "com.bigcitylegacy.enhancedcartuning";
    private static Type Bridge
    {
        get
        {
            if (!Chainloader.PluginInfos.TryGetValue(TuningId, out var plugin) || !plugin.Instance ||
                !plugin.Instance.isActiveAndEnabled || !Harmony.HasAnyPatches(TuningId)) return null;
            return plugin.Instance.GetType().Assembly.GetType("EnhancedCarTuning.VinylPortBridge");
        }
    }
    internal static bool SupportsText => Chainloader.PluginInfos.TryGetValue(TuningId, out var plugin) &&
        plugin.Instance && plugin.Instance.isActiveAndEnabled && Harmony.HasAnyPatches(TuningId) &&
        plugin.Instance.GetType().Assembly.GetType("EnhancedCarTuning.Appearance.Vinyls.TextVinylData") != null;
    internal static bool SupportsParts => Bridge != null;
    internal static bool NeedsPaintUpdate => !SupportsParts && Chainloader.PluginInfos.TryGetValue(TuningId, out var plugin) &&
        plugin.Instance && plugin.Instance.isActiveAndEnabled && Harmony.HasAnyPatches(TuningId) &&
        plugin.Instance.GetType().Assembly.GetType("EnhancedCarTuning.Appearance.Body.BodyPaintSession") != null;
    internal static string Capture(CarGarageUI garage, CarSaveLoad saveLoad)
    {
        var method = Bridge?.GetMethod("CaptureGarageXml", BindingFlags.Public | BindingFlags.Static);
        return method != null ? (string)method.Invoke(null, new object[] { garage }) : saveLoad.SaveXml().OuterXml;
    }
    internal static void Apply(CarGarageUI garage, CarSaveLoad saveLoad, string xml)
    {
        var method = Bridge?.GetMethod("ApplyGarageXml", BindingFlags.Public | BindingFlags.Static);
        if (method != null) method.Invoke(null, new object[] { garage, xml });
        else { garage.CarPaint_Finish(); saveLoad.LoadXml(xml); }
    }
    internal static void ApplyPaint(CarGarageUI garage, CarSaveLoad saveLoad, string xml)
    {
        var method = Bridge?.GetMethod("ApplyGaragePaintXml", BindingFlags.Public | BindingFlags.Static);
        if (method != null) { method.Invoke(null, new object[] { garage, xml }); return; }
        // Do not load CarUpgrade, suspension, engine, drive or other SaveLoad components.
        var root = LegacyVinylPackCodec.ReadXml(xml).DocumentElement;
        garage.CarPaint_Finish();
        var material = saveLoad.car.GetComponent<CarMaterial>();
        if (material) material.SaveLoad(root, false);
        if (saveLoad.car.paintItems) { saveLoad.car.paintItems.SaveLoad(root, false); saveLoad.car.paintItems.UpImage(); }
    }
    internal static string Primitive()
    {
        if (!CarPaintSprites.me) return null;
        var preferred = CarPaintSprites.me.FindSpriteByName("Primitive_1");
        if (preferred != null) return preferred.texutreName;
        foreach (var folder in CarPaintSprites.me.folders)
        foreach (var sprite in folder.sprites)
            if (sprite != null && !string.IsNullOrEmpty(sprite.texutreName) &&
                sprite.type == CarPaintSprites.TypeVinil.Vinil && !string.IsNullOrEmpty(sprite.fullPath) &&
                sprite.fullPath.Replace('\\', '/').IndexOf("/primitive/", StringComparison.OrdinalIgnoreCase) >= 0)
                return sprite.texutreName;
        return null;
    }
}
