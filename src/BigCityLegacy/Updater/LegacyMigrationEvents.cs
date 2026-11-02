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
        RemoveObsoleteEventFiles();
    }

    private static void RemoveObsoleteEventFiles()
    {
        // Add obsolete default event filenames here, including the .json extension.
        // Checked in events itself and in each supported category; names are exact.
        string[] obsoleteFiles =
        {
            "airport_01.json",
            "BIG_CITY_RACE.json",
            "BIG_CITY_RACE_2.json",
            "BIG_CITY_RACE_3.json",
            "city_race_22.json",
            "city_race_23.json",
            "city_race_24.json",
            "city_sprint_01.json",
            "DESERT_RACE.json",
            "desert_race_2.json",
            "SUPER_RACE.json",
            "VERY_LONG_BIG_CITY.json",
            "Survival_01.json"
        };

        string eventsPath = Path.Combine(LegacyHelpers.ModDataPath, "events");
        foreach (string fileName in obsoleteFiles)
        {
            // This list contains filenames only, never paths or wildcard patterns.
            if (string.IsNullOrWhiteSpace(fileName) || fileName.IndexOfAny(new[] { '/', '\\', '*', '?' }) >= 0 ||
                Path.GetFileName(fileName) != fileName || !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[BigCityLegacy] Invalid obsolete event filename: " + fileName);
                continue;
            }
            foreach (string category in new[] { "", "Race", "CS", "CS_Sur" })
            {
                string directory = Path.Combine(eventsPath, category);
                string path = Path.Combine(directory, fileName);
                try
                {
                    // Do not follow directory links into another event collection.
                    if (!Directory.Exists(eventsPath) || !Directory.Exists(directory) ||
                        (File.GetAttributes(eventsPath) & FileAttributes.ReparsePoint) != 0 ||
                        (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 || !File.Exists(path)) continue;
                    File.Delete(path);
                    Debug.Log("[BigCityLegacy] Removed obsolete event: " + path);
                }
                catch (IOException ex) { Debug.LogWarning("[BigCityLegacy] Could not remove obsolete event " + path + ": " + ex.Message); }
                catch (UnauthorizedAccessException ex) { Debug.LogWarning("[BigCityLegacy] Could not remove obsolete event " + path + ": " + ex.Message); }
            }
        }
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
