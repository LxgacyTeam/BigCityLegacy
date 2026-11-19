using HarmonyLib;
using Item = WeaponeSelectItem.SubTypes;
using Tab = WeaponeSelect.SubType;

[HarmonyPatch]
internal static class RadialMenuPatches
{
    [HarmonyPatch(typeof(WeaponeSelect), "Awake")]
    [HarmonyPrefix]
    private static void RegisterWeaponSlots(WeaponeSelect __instance)
    {
        foreach (var item in __instance.GetComponentsInChildren<WeaponeSelectItem>(true))
        {
            if (item.SubTypeStr == nameof(Item.Car_Weapone_Rocket) ||
                item.SubTypeStr == nameof(Item.Car_Weapone_Bullets) ||
                item.SubTypeStr == nameof(Item.Car_Weapone_Mine))
            {
                item.transform.parent.gameObject.SetActive(true);
                item.gameObject.SetActive(true);
            }
        }
    }

    [HarmonyPatch(typeof(WeaponeSelect), "Awake")]
    [HarmonyPostfix]
    private static void AddVehicleWeaponTab(WeaponeSelect __instance)
    {
        __instance.AddSubGroup("Weapone", 140f, false, Tab.CarWeapone);
    }

    [HarmonyPatch(typeof(WeaponeSelect), "SetVisible")]
    [HarmonyPrefix]
    private static void ResetUnavailableWeaponTab(WeaponeSelect __instance, bool b, InputControl input)
    {
        if (b && __instance.subType == Tab.CarWeapone && !HasCarWeapon(input))
            __instance.subType = Tab.Car;
    }

    [HarmonyPatch(typeof(WeaponeSelect), "UpVisibleBySubType")]
    [HarmonyPostfix]
    private static void HideUnavailableWeaponTab(WeaponeSelect __instance)
    {
        if (HasCarWeapon(__instance.inputOpen)) return;
        foreach (var group in __instance.subGroups)
        {
            if (group.type == Tab.CarWeapone)
                group.root.gameObject.SetActive(false);
        }
    }

    [HarmonyPatch(typeof(WeaponeSelectItem), "needFor")]
    [HarmonyPostfix]
    private static void FilterRestoredItems(WeaponeSelectItem __instance, InputControl input, ref bool __result)
    {
        if (!__result) return;
        Item item = __instance.subType;
        if (IsRemovedItem(item))
            __result = false;
        else if (CarWeaponType(item) != Weapone.Type.Unknow)
            __result = GetCarWeapon(input, item);
    }

    [HarmonyPatch(typeof(WeaponeSelectItem), "doEvent")]
    [HarmonyPrefix]
    private static bool SelectCarWeapon(WeaponeSelectItem __instance, InputControl input)
    {
        Item item = __instance.subType;
        if (IsRemovedItem(item)) return false;
        if (CarWeaponType(item) == Weapone.Type.Unknow) return true;
        var weapon = GetCarWeapon(input, item);
        if (weapon && weapon.control) weapon.control.SetSelectWeapone(weapon);
        return false;
    }

    private static bool IsRemovedItem(Item item)
    {
        return item == Item.Car_Respaum || item == Item.Car_Fix || item == Item.Car_Ghost ||
            item == Item.Car_Paint || item == Item.Human_Skin || item == Item.Car_Weapones ||
            (item == Item.Player_GunShop && NetManager.isOnlineClient);
    }

    private static bool HasCarWeapon(InputControl input)
    {
        return GetCarWeapon(input, Item.Car_Weapone_Rocket) ||
            GetCarWeapon(input, Item.Car_Weapone_Bullets) || GetCarWeapon(input, Item.Car_Weapone_Mine);
    }

    private static Weapone GetCarWeapon(InputControl input, Item item)
    {
        var type = CarWeaponType(item);
        if (type == Weapone.Type.Unknow || !input || !(input.currentOrParent is CarControl car) ||
            !car || !car.isAlive || car.GetHandleInput() != input)
            return null;
        return car.GetWeapone(type);
    }

    private static Weapone.Type CarWeaponType(Item item)
    {
        switch (item)
        {
            case Item.Car_Weapone_Rocket: return Weapone.Type.CarRocket;
            case Item.Car_Weapone_Bullets: return Weapone.Type.CarBullet;
            case Item.Car_Weapone_Mine: return Weapone.Type.CarMine;
            default: return Weapone.Type.Unknow;
        }
    }
}
