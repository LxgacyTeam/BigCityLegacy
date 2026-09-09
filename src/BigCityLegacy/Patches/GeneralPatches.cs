using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

[HarmonyPatch]
internal static class GeneralPatches
{
    [HarmonyPatch(typeof(AlwaysOnline), "Start")]
    [HarmonyPrefix]
    private static bool AlwaysOnline_Start(AlwaysOnline __instance)
    {
        AlwaysOnline.me = __instance;
        __instance.gameObject.SetActive(false);
        return false;
    }

    [HarmonyPatch(typeof(FirstScene), "OnGUI")]
    [HarmonyPostfix]
    private static void FirstScene_OnGUI_Postfix()
    {
        LegacyWatermark.Draw();
    }

    [HarmonyPatch(typeof(LocalizationManager), MethodType.Constructor)]
    [HarmonyPostfix]
    private static void LocalizationManager_Ctor_Postfix(LocalizationManager __instance)
    {
        LegacyLocalizer.ApplyLocalizationManagerPatch(__instance);
    }



    //[HarmonyPatch(typeof(MenuEsc), "RestorePurchases")]
    //[HarmonyPrefix]
    //private static bool MenuEsc_RestorePurchases()
    //{
    //    return false;
    //}

    //[HarmonyPatch(typeof(MenuEsc), "EditTouches")]
    //[HarmonyPrefix]
    //private static bool MenuEsc_EditTouches()
    //{
    //    return false;
    //}

    [HarmonyPatch(typeof(SteamManager), "Awake")]
    [HarmonyPrefix]
    private static bool SteamManager_Awake(SteamManager __instance)
    {
        SteamManager.s_instance = __instance;
        if (SteamManager.s_EverInialized)
        {
            throw new Exception("Tried to Initialize the SteamAPI twice in one session!");
        }
        LegacyHelpers.SafeInvoke(__instance, "Init");
        return false;
    }

    [HarmonyPatch(typeof(MenuShop), "OnEnable")]
    [HarmonyPrefix]
    private static bool MenuShop_OnEnable(MenuShop __instance)
    {
        __instance.needMakeCur = true;
        InvokeMenuSetTopGroupOnEnable(__instance);
        MenuShop.me = __instance;
        return false;
    }

    [HarmonyPatch(typeof(MenuShop), "Update")]
    [HarmonyPrefix]
    private static bool MenuShop_Update(MenuShop __instance)
    {
        if (__instance.needMakeCur)
        {
            __instance.needMakeCur = false;
            if (MenuShop.lastShowNeedDiamods)
            {
                __instance.diamodsBut.MakeActive(true);
            }
            else
            {
                __instance.coinsBut.MakeActive(true);
            }
            MenuShop.lastShowNeedDiamods = false;
        }
        if (!__instance.isDisableAds25MinSetted && MenuShop.SetTextDisableAds25Min(__instance.disableAds25Min))
        {
            __instance.isDisableAds25MinSetted = true;
        }
        return false;
    }

    private static void InvokeMenuSetTopGroupOnEnable(MenuShop instance)
    {
        MethodInfo baseOnEnable = AccessTools.DeclaredMethod(typeof(MenuSetTopGroup), "OnEnable");
        if (baseOnEnable != null)
        {
            baseOnEnable.Invoke(instance, null);
        }
    }

    [HarmonyPatch(typeof(App_DeliveryCar), "Awake")]
    [HarmonyPostfix]
    private static void AppDeliveryCar_Awake_Postfix()
    {
        SetStaticFloat(typeof(App_DeliveryCar), "deliveryCloseTime", 1f);
    }

    [HarmonyPatch(typeof(App_DeliveryCar), "Delivery")]
    [HarmonyPrefix]
    private static bool AppDeliveryCar_Delivery_Prefix(App_DeliveryCar __instance, MenuButton menu)
    {
        if (menu == __instance.delivery_fast && Diamonds.MinusIfHave(25, "DeliveryFast", true))
        {
            AccessTools.Method(typeof(App_DeliveryCar), "DoDelivery").Invoke(__instance, new object[] { 0f });
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(CarShop_Item), "SetCar")]
    [HarmonyPostfix]
    private static void CarShopItem_SetCar_Postfix(CarShop_Item __instance)
    {
        LegacyHotkeys.LastSelectedCarNameShop = __instance.file_name;
    }

    [HarmonyPatch(typeof(App_DeliveryCar), "SelectCarChanget")]
    [HarmonyPostfix]
    private static void AppDeliveryCar_SelectCarChanget_Postfix(App_DeliveryCar __instance)
    {
        LegacyHotkeys.LastSelectedCarNameApp = __instance.getSelectCarName;
    }

    [HarmonyPatch(typeof(CarShop_Item), "Awake")]
    [HarmonyPostfix]
    private static void CarShopItem_Awake_Postfix()
    {
        SetStaticFloat(typeof(CarShop_Item), "deliveryCloseTime", 1f);
    }

    [HarmonyPatch(typeof(CarShop_Item), "Delivery")]
    [HarmonyPrefix]
    private static bool CarShopItem_Delivery_Prefix(CarShop_Item __instance, MenuButton menu)
    {
        if (menu == __instance.delivery_fast && Diamonds.MinusIfHave(25, "DeliveryFast", true))
        {
            AccessTools.Method(typeof(CarShop_Item), "DoDelivery").Invoke(__instance, new object[] { 0f });
            return false;
        }
        return true;
    }

    private static void SetStaticFloat(Type type, string name, float value)
    {
        FieldInfo f = AccessTools.Field(type, name);
        if (f != null)
        {
            f.SetValue(null, value);
        }
    }

    [HarmonyPatch(typeof(SettingsValues.User.TargetFPS), "Init")]
    [HarmonyPrefix]
    private static bool TargetFps_Init_Prefix(MenuButton it)
    {
        MenuSettingText setting = it as MenuSettingText;
        if (setting)
        {
            setting.values = new[] { "20", "30", "45", "60", "90", "120", "240", "10000" };
        }
        return false;
    }

    [HarmonyPatch(typeof(SettingsValues.User.TargetFPS), "SetValue")]
    [HarmonyPrefix]
    private static bool TargetFps_SetValue_Prefix(SettingsValues.User.TargetFPS __instance, string val)
    {
        if (__instance.strValue == val)
        {
            return false;
        }
        int fps = val.ToIntFafe(0);
        if (fps < 20)
        {
            fps = 20;
        }
        __instance.strValue = fps.ToString();
        SettingsValues.User.TargetFPS.targetFps = fps;
        SettingsValues.User.TargetFPS.CheckState();
        return false;
    }


}

internal static class LegacyWatermark
{
    internal static void Draw()
    {
        string text = LegacyHelpers.VersionLine;
        if (LegacyCompatibility.IsCompatibilityMode)
        {
            text += " (COMPATIBILITY MODE *beta*)";
        }
        int fontSize = 16;
        GUIStyle style = new GUIStyle();
        style.fontSize = fontSize;
        style.normal.textColor = Color.white;
        Vector2 size = style.CalcSize(new GUIContent(text));
        GUI.Label(new Rect(10f, Screen.height - size.y - 10f, size.x, size.y), text, style);
    }
}

[HarmonyPatch]
internal static class InAppPatches
{
    [HarmonyPatch(typeof(InApp_But), "get_isTest")]
    [HarmonyPrefix]
    private static bool InAppBut_IsTest_Prefix(ref bool __result)
    {
        __result = true;
        return false;
    }

    [HarmonyPatch(typeof(InApp_But), "GetPriceAndValuta", new[] { typeof(string), typeof(int) })]
    [HarmonyPrefix]
    private static bool InAppBut_GetPriceFloat_Prefix(ref float __result)
    {
        __result = 0f;
        return false;
    }

    [HarmonyPatch(typeof(InApp_But), "GetPriceAndValuta", new[] { typeof(string), typeof(Text), typeof(Text), typeof(Text), typeof(Text) })]
    [HarmonyPrefix]
    private static bool InAppBut_GetPriceTexts_Prefix(InApp_But __instance, Text text, Text priceAfterDot, Text valuta1)
    {
        if (text)
        {
            text.text = "0";
        }
        if (priceAfterDot)
        {
            priceAfterDot.text = string.Empty;
        }
        if (valuta1)
        {
            valuta1.text = string.Empty;
        }
        return false;
    }

    [HarmonyPatch(typeof(InApp_But), "UpPercentTxt")]
    [HarmonyPrefix]
    private static bool InAppBut_UpPercentTxt_Prefix(InApp_But __instance)
    {
        if (__instance.percent)
        {
            __instance.percent.text = "+ 0 %";
        }
        return false;
    }

    [HarmonyPatch(typeof(InApp_But), nameof(InApp_But.Pressed))]
    [HarmonyPrefix]
    private static bool InAppBut_Pressed_Prefix(InApp_But __instance)
    {
        if (__instance.type != InApp_But.Types.Coin &&
            __instance.type != InApp_But.Types.Diamond)
        {
            return true;
        }

        if (__instance.index == -1)
        {
            return false;
        }

        if (__instance.type == InApp_But.Types.Coin)
        {
            Money.Add(
                Money.AmoutByIndex(__instance.index),
                "InApp_" + __instance.index.ToString(),
                true
            );
        }

        if (__instance.type == InApp_But.Types.Diamond)
        {
            Diamonds.Add(
                Diamonds.AmoutByIndex(__instance.index),
                "InApp_" + __instance.index.ToString()
            );
        }

        return false;
    }
}
