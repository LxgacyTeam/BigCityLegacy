using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine.UI;

internal static class LegacyPatchManager
{
    private static Harmony _harmony;
    private static ManualLogSource _log;

    internal static void PatchForCurrentMode(Harmony harmony, ManualLogSource log)
    {
        _harmony = harmony;
        _log = log;

        if (LegacyCompatibility.IsCompatibilityMode)
        {
            PatchCompatibilityMode();
            return;
        }

        PatchFullMode();
    }

    private static void PatchFullMode()
    {
        PatchClass(typeof(GeneralPatches));
        PatchClass(typeof(InAppPatches));
        PatchClass(typeof(NetConnectAndControlPatches));
        PatchClass(typeof(NetUIPatches));
        PatchClass(typeof(NetworkPatches));
        PatchClass(typeof(RaceAndWorldPatches));
        PatchClass(typeof(RuntimeQualityAndCursorPatches));
        PatchClass(typeof(ChatEventsPatches));
        PatchClass(typeof(ShadowFixPatches));
        PatchClass(typeof(SettingsMenuPatches));
        PatchClass(typeof(SuppressReporterGesturePatch));
    }

    private static void PatchCompatibilityMode()
    {
        // For older game builds, ONLY offline functionality remains.
        // Do not touch networking, master server, events config, direct connect UI, target FPS or debug overlay here.
        SafePrefix("AlwaysOnline.Start", "AlwaysOnline", "Start", typeof(GeneralPatches), "AlwaysOnline_Start");
        SafePostfix("FirstScene.OnGUI", "FirstScene", "OnGUI", typeof(GeneralPatches), "FirstScene_OnGUI_Postfix");
        SafeConstructorPostfix("LocalizationManager.ctor", "LocalizationManager", typeof(GeneralPatches), "LocalizationManager_Ctor_Postfix");
        SafePrefix("MenuEsc.RestorePurchases", "MenuEsc", "RestorePurchases", typeof(GeneralPatches), "MenuEsc_RestorePurchases");
        SafePrefix("MenuEsc.EditTouches", "MenuEsc", "EditTouches", typeof(GeneralPatches), "MenuEsc_EditTouches");
        SafePrefix("SteamManager.Awake", "SteamManager", "Awake", typeof(GeneralPatches), "SteamManager_Awake");
        SafePrefix("MenuShop.OnEnable", "MenuShop", "OnEnable", typeof(GeneralPatches), "MenuShop_OnEnable");
        SafePrefix("MenuShop.Update", "MenuShop", "Update", typeof(GeneralPatches), "MenuShop_Update");
        SafePostfix("App_DeliveryCar.Awake", "App_DeliveryCar", "Awake", typeof(GeneralPatches), "AppDeliveryCar_Awake_Postfix");
        SafePrefix("App_DeliveryCar.Delivery", "App_DeliveryCar", "Delivery", typeof(GeneralPatches), "AppDeliveryCar_Delivery_Prefix");
        SafePostfix("CarShop_Item.Awake", "CarShop_Item", "Awake", typeof(GeneralPatches), "CarShopItem_Awake_Postfix");
        SafePostfix("CarShop_Item.SetCar", "CarShop_Item", "SetCar", typeof(GeneralPatches), "CarShopItem_SetCar_Postfix");
        SafePrefix("CarShop_Item.Delivery", "CarShop_Item", "Delivery", typeof(GeneralPatches), "CarShopItem_Delivery_Prefix");

        SafeGetterPrefix("InApp_But.isTest", "InApp_But", "isTest", typeof(InAppPatches), "InAppBut_IsTest_Prefix");
        SafePrefix("InApp_But.GetPriceAndValuta(string,int)", "InApp_But", "GetPriceAndValuta", new[] { typeof(string), typeof(int) }, typeof(InAppPatches), "InAppBut_GetPriceFloat_Prefix");
        SafePrefix("InApp_But.GetPriceAndValuta(texts)", "InApp_But", "GetPriceAndValuta", new[] { typeof(string), typeof(Text), typeof(Text), typeof(Text), typeof(Text) }, typeof(InAppPatches), "InAppBut_GetPriceTexts_Prefix");
        SafePrefix("InApp_But.UpPercentTxt", "InApp_But", "UpPercentTxt", typeof(InAppPatches), "InAppBut_UpPercentTxt_Prefix");
        SafePrefix("InApp_But.Pressed", "InApp_But", "Pressed", typeof(InAppPatches), "InAppBut_Pressed_Prefix");
    }

    private static void PatchClass(Type patchClass)
    {
        try
        {
            _harmony.CreateClassProcessor(patchClass).Patch();
            _log.LogDebug("Patched " + patchClass.Name);
        }
        catch (Exception ex)
        {
            _log.LogError("Failed to patch " + patchClass.Name + ": " + ex);
        }
    }

    private static void SafePrefix(string label, string typeName, string methodName, Type patchClass, string patchMethodName)
    {
        SafePatch(label, typeName, methodName, null, patchClass, patchMethodName, prefix: true);
    }

    private static void SafePrefix(string label, string typeName, string methodName, Type[] parameters, Type patchClass, string patchMethodName)
    {
        SafePatch(label, typeName, methodName, parameters, patchClass, patchMethodName, prefix: true);
    }

    private static void SafePostfix(string label, string typeName, string methodName, Type patchClass, string patchMethodName)
    {
        SafePatch(label, typeName, methodName, null, patchClass, patchMethodName, prefix: false);
    }

    private static void SafeConstructorPostfix(string label, string typeName, Type patchClass, string patchMethodName)
    {
        try
        {
            Type targetType = FindType(typeName);
            if (targetType == null)
            {
                LogSkipped(label, "target type not found");
                return;
            }
            ConstructorInfo ctor = AccessTools.Constructor(targetType, Type.EmptyTypes);
            if (ctor == null)
            {
                LogSkipped(label, "constructor not found");
                return;
            }
            Patch(ctor, patchClass, patchMethodName, prefix: false, label);
        }
        catch (Exception ex)
        {
            LogSkipped(label, ex.Message);
        }
    }

    private static void SafeGetterPrefix(string label, string typeName, string propertyName, Type patchClass, string patchMethodName)
    {
        try
        {
            Type targetType = FindType(typeName);
            if (targetType == null)
            {
                LogSkipped(label, "target type not found");
                return;
            }
            MethodInfo getter = AccessTools.PropertyGetter(targetType, propertyName);
            if (getter == null)
            {
                getter = AccessTools.Method(targetType, "get_" + propertyName);
            }
            if (getter == null)
            {
                LogSkipped(label, "getter not found");
                return;
            }
            Patch(getter, patchClass, patchMethodName, prefix: true, label);
        }
        catch (Exception ex)
        {
            LogSkipped(label, ex.Message);
        }
    }

    private static void SafePatch(string label, string typeName, string methodName, Type[] parameters, Type patchClass, string patchMethodName, bool prefix)
    {
        try
        {
            Type targetType = FindType(typeName);
            if (targetType == null)
            {
                LogSkipped(label, "target type not found");
                return;
            }

            MethodInfo target = parameters == null
                ? AccessTools.DeclaredMethod(targetType, methodName)
                : AccessTools.DeclaredMethod(targetType, methodName, parameters);

            if (target == null)
            {
                LogSkipped(label, "target method not found");
                return;
            }

            Patch(target, patchClass, patchMethodName, prefix, label);
        }
        catch (Exception ex)
        {
            LogSkipped(label, ex.Message);
        }
    }

    private static void Patch(MethodBase target, Type patchClass, string patchMethodName, bool prefix, string label)
    {
        MethodInfo patch = AccessTools.DeclaredMethod(patchClass, patchMethodName);
        if (patch == null)
        {
            LogSkipped(label, "patch method not found: " + patchMethodName);
            return;
        }

        HarmonyMethod harmonyMethod = new HarmonyMethod(patch);
        if (prefix)
        {
            _harmony.Patch(target, prefix: harmonyMethod);
        }
        else
        {
            _harmony.Patch(target, postfix: harmonyMethod);
        }
        _log.LogDebug("Patched " + label);
    }

    private static Type FindType(params string[] names)
    {
        for (int i = 0; i < names.Length; i++)
        {
            Type type = AccessTools.TypeByName(names[i]);
            if (type != null)
            {
                return type;
            }
        }

        // Support both reflection and C# nested type separators for older decompiler output.
        List<string> variants = new List<string>();
        for (int i = 0; i < names.Length; i++)
        {
            if (names[i].IndexOf('+') >= 0)
            {
                variants.Add(names[i].Replace('+', '.'));
            }
            if (names[i].IndexOf('.') >= 0)
            {
                variants.Add(names[i].Replace('.', '+'));
            }
        }
        for (int i = 0; i < variants.Count; i++)
        {
            Type type = AccessTools.TypeByName(variants[i]);
            if (type != null)
            {
                return type;
            }
        }
        return null;
    }

    private static void LogSkipped(string label, string reason)
    {
        if (_log != null)
        {
            _log.LogDebug("Skipped optional patch " + label + ": " + reason);
        }
    }
}
