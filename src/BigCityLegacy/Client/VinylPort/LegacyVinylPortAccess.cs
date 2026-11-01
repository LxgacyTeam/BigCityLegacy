using UnityEngine;

internal static class LegacyVinylPortAccess
{
    internal static bool CanUse
    {
        get
        {
            var garage = CarGarageUI.me;
            if (Application.isBatchMode || NetManager.isServer || !garage || !garage.isActiveAndEnabled ||
                !garage.root || !garage.root.gameObject.activeInHierarchy || !garage.top_panel ||
                garage.top_panel.cur != garage || !garage.car || !garage.car.gameObject.activeInHierarchy) return false;
            if (!NetManager.isOnlineClient) return true;
            var preview = EditCar_Preview.me;
            return !MenuEsc.editCarInSceneGarage && preview && preview.isActiveAndEnabled &&
                preview.car == garage.car && garage.car.isUsedInMenu && !garage.car.net_control;
        }
    }
}
