using UnityEngine;

internal static class LegacyHotkeys
{
    internal static string LastSelectedCarNameShop;
    internal static string LastSelectedCarNameApp;

    internal static void Update()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.F5))
        {
            LegacyHelpers.SafeInvoke(typeof(WeatherManager), "DoRandom");
        }

        if (Input.GetKeyDown(KeyCode.F10))
        {
            NativeErrorDialog.Show("Title", "Message");
        }

        if (Input.GetKeyDown(KeyCode.F6) && (!string.IsNullOrEmpty(LastSelectedCarNameApp) || !string.IsNullOrEmpty(LastSelectedCarNameShop)))
        {
            int F6SpawnMode = PlayerPrefs.GetInt("BigCityLegacy.F6_SpawnMode");

            if (F6SpawnMode == 1)
            {
                CarShop_Item.CreateDeliveryCar(LastSelectedCarNameShop, false);
            }
            else
            {
                App_DeliveryCar.CreateDeliveryCar(LastSelectedCarNameApp, false);
            }
        }

        if (NetManager.isOnlineClient && Input.GetKeyDown(KeyCode.F12))
        {
            AskBoxUI.Show("Disconnect from server?", delegate(AskBoxUI box)
            {
                if (box.isYes && NetManager.me)
                {
                    NetManager.me.StopHost();
                }
            });
        }
    }
}
