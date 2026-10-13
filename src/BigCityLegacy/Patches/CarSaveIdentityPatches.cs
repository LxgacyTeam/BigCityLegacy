using System.Xml;
using HarmonyLib;
using UnityEngine;

[HarmonyPatch]
internal static class CarSaveIdentityPatches
{
    [HarmonyPatch(typeof(ControlArray), "InitControl")]
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void ControlArray_InitControl(Control obj, ControlArray.ControlItem control_arr_item)
    {
        CarControl car = obj as CarControl;
        if (car && control_arr_item != null)
            LegacyCarSaveIdentity.Assign(car, control_arr_item.name);
    }

    [HarmonyPatch(typeof(EditCar_Preview), "UpCarMat")]
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void EditCarPreview_UpCarMat(EditCar_Preview __instance)
    {
        LegacyCarSaveIdentity.Ensure(__instance.car);
    }

    [HarmonyPatch(typeof(CarGarageUI), "Init")]
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void CarGarageUI_Init(CarControl setCar)
    {
        LegacyCarSaveIdentity.Ensure(setCar);
    }

    [HarmonyPatch(typeof(CarSaveLoad), "Load")]
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void CarSaveLoad_Load(CarSaveLoad __instance, ref XmlDocument doc)
    {
        if (!LegacyCarSaveIdentity.Ensure(__instance.car) && doc == null)
        {
            doc = new XmlDocument();
            Debug.LogWarning("[BigCityLegacy] Cannot identify car for loading; shared CarSaved_ is ignored.");
        }
    }

    [HarmonyPatch(typeof(CarSaveLoad), "SaveXml")]
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static void CarSaveLoad_SaveXml(CarSaveLoad __instance)
    {
        LegacyCarSaveIdentity.Ensure(__instance.car);
    }

    [HarmonyPatch(typeof(CarSaveLoad), "Save")]
    [HarmonyPrefix, HarmonyPriority(Priority.First)]
    private static bool CarSaveLoad_Save(CarSaveLoad __instance)
    {
        if (LegacyCarSaveIdentity.Ensure(__instance.car))
            return true;
        Debug.LogWarning("[BigCityLegacy] Cannot identify car for saving; refused to overwrite shared CarSaved_.");
        return false;
    }
}
