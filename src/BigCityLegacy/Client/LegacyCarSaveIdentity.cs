using System;
using UnityEngine;

internal static class LegacyCarSaveIdentity
{
    internal static bool Ensure(CarControl car)
    {
        if (!car)
            return false;
        if (!string.IsNullOrWhiteSpace(car.prefabName))
            return true;

        TrafficItem item = car.GetComponent<TrafficItem>();
        if (item && item.control_arr_item != null && Assign(car, item.control_arr_item.name))
            return true;

        EditCar_Preview preview = EditCar_Preview.me;
        ControlLinks catalogue = ControlLinks.car;
        if (preview && preview.car == car && catalogue && catalogue.links != null)
        {
            foreach (ControlLinks.Link link in catalogue.links)
            {
                if (link != null && !string.IsNullOrEmpty(preview.useCarGUID)
                    && string.Equals(link.GUID, preview.useCarGUID, StringComparison.Ordinal)
                    && Assign(car, link.name))
                    return true;
            }
        }

        if (car.net_control && Assign(car, car.net_control.NetworkobjName))
            return true;

        string objectName = car.name;
        const string cloneSuffix = "(Clone)";
        if (!string.IsNullOrEmpty(objectName) && objectName.EndsWith(cloneSuffix, StringComparison.Ordinal))
            objectName = objectName.Substring(0, objectName.Length - cloneSuffix.Length).TrimEnd();
        if (catalogue && catalogue.links != null)
        {
            foreach (ControlLinks.Link link in catalogue.links)
            {
                if (link != null && string.Equals(link.name, objectName, StringComparison.Ordinal)
                    && Assign(car, link.name))
                    return true;
            }
        }
        return false;
    }

    internal static bool Assign(CarControl car, string model)
    {
        if (!car || string.IsNullOrWhiteSpace(model))
            return false;
        if (!string.IsNullOrWhiteSpace(car.prefabName))
            return true;

        car.prefabName = model;
        Debug.Log("[BigCityLegacy] Restored car save identity: " + car.name + " -> " + model);
        return true;
    }
}
