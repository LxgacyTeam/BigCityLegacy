using System.IO;
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

        if (Input.GetKeyDown(KeyCode.F1) && LegacyHelpers.IsGameplayRunning)
        {
            LegacyHudToggle.Toggle();
        }

        if (Input.GetKeyDown(KeyCode.F2))
        {
            string merctest = PlayerPrefs.GetString("CarSaved_Mercedes_600");
            File.WriteAllText(Path.Combine(LegacyHelpers.ModDataPath, "CarSaved_Mercedes_600.txt"), merctest);
        }


        if (NetManager.isOnlineClient && Input.GetKeyDown(KeyCode.F12))
        {
            AskBoxUI.Show(LegacyLocalizer.Text("Disconnect from server?", "Отключиться с сервера?"), delegate(AskBoxUI box)
            {
                if (box.isYes && NetManager.me)
                {
                    NetManager.me.StopHost();
                }
            });
        }
    }
}