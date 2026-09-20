using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using UnityEngine;
using File = System.IO.File;

internal static class LegacyMigrationEvents
{
    internal static void Init()
    {
        CheckMLPluginExist();
        MoveConfigAfterUpdate();
    }

    private static void MoveConfigAfterUpdate()
    {
        string prefsKey = "BigCityLegacy.ConfigIsMovedAfterUpdate";

        if (PlayerPrefs.HasKey(prefsKey))
        {
            return;
        }

        string oldPath = LegacyHelpers.ModDataPath;
        string newPath = Path.Combine(LegacyHelpers.ModDataPath, "config");

        List<string> filesList = new List<string>
        {
            "BanList.json",
            "Events.json",
            "RespawnPoints.json",
            "ServersList.json"
        };

        if (!Directory.Exists(newPath))
            Directory.CreateDirectory(newPath);

        int ResultID = 0;

        foreach (string fileName in filesList)
        {
            string sourceFile = Path.Combine(oldPath, fileName);
            string destFile = Path.Combine(newPath, fileName);
            
            if (!File.Exists(sourceFile)) 
            {
                ResultID = 1;
            }

            if (File.Exists(sourceFile) && !File.Exists(destFile))
            {
                File.Move(sourceFile, destFile);
                ResultID = 2;
            }
        }

        switch(ResultID)
        {
            case 0:
                Debug.LogWarning("[BigCityLegacy] MoveConfigAfterUpdate: case 0");
                return;
            
            case 1:
                PlayerPrefs.SetInt(prefsKey, 0);
                PlayerPrefs.Save();
                Debug.Log("[BigCityLegacy] MoveConfigAfterUpdate: files NOT moved");
                break;

            case 2:
                PlayerPrefs.SetInt(prefsKey, 1);
                PlayerPrefs.Save();
                Debug.Log("[BigCityLegacy] MoveConfigAfterUpdate: files moved");
                break;
        }
    }

    private static void CheckMLPluginExist()
    {
        string PluginGuid = "com.alonso.madoutlegacy";

        if (Chainloader.PluginInfos.ContainsKey(PluginGuid))
        {
            string BiePath = "BepInEx/plugins";
            if (Application.platform == RuntimePlatform.WindowsPlayer)
                BiePath = BiePath.Replace("/", "\\");
            
            if (Directory.Exists(Path.Combine(Paths.GameRootPath, "MadOutLegacy")))
                MoveMLConfig();

            NativeErrorDialog.Show("BigCityLegacy Startup Error",
                                    "Outdated MadOutLegacy plugin detected!\n" +
                                    "Game launch blocked to prevent a version conflict.\n\n" +
                                    $"Please delete the \'MadOutLegacy\' folder from \'{BiePath}\'.");

            LegacyHelpers.OpenFolder(Paths.PluginPath);
            Application.Quit();
        }
    }

    private static void MoveMLConfig()
    {
        string oldPath = Path.Combine(Paths.GameRootPath, "MadOutLegacy");
        string newPath = Path.Combine(LegacyHelpers.ModDataPath, "config");

        foreach (string filePath in Directory.GetFiles(oldPath))
        {
            string fileName = Path.GetFileName(filePath);
            string destPath = Path.Combine(newPath, fileName);

            try
            {
                if (File.Exists(destPath)) { File.Move(filePath, destPath + ".old"); }
                else { File.Move(filePath, destPath); }   
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
            }
        }

        Directory.Delete(oldPath);
    }
}
