using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal static class LegacyEventFiles
{
    private static readonly string[] categories = { "Race", "CS", "CS_Sur" };
    internal static string DirectoryPath
    {
        get
        {
            string specified = LegacyCommandLine.GetArgValue("-eventsDir");
            if (string.IsNullOrEmpty(specified)) return Path.Combine(LegacyHelpers.ModDataPath, "events");
            return Path.IsPathRooted(specified) ? specified : Path.Combine(BepInEx.Paths.GameRootPath, specified);
        }
    }
    internal static LegacyEventsConfig.Config Load(string directory, string legacyCollection)
    {
        Directory.CreateDirectory(directory);
        foreach (string category in categories) Directory.CreateDirectory(Path.Combine(directory, category));
        Migrate(directory, legacyCollection);
        var events = new List<LegacyEventsConfig.EventDef>();
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string category in categories)
        foreach (string file in Directory.GetFiles(Path.Combine(directory, category), "*.json").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                JObject root = JObject.Parse(File.ReadAllText(file));
                if (root["events"] != null) throw new FormatException("Each event file must contain one event object.");
                var item = root.ToObject<LegacyEventsConfig.EventDef>();
                if (item == null || string.IsNullOrWhiteSpace(item.name) || string.IsNullOrWhiteSpace(item.mode))
                    throw new FormatException("Event name and mode are required.");
                // Match the aliases used by BuildRuntimeEventLists.
                string mode = NormalizeMode(item.mode);
                string expectedCategory = CategoryForMode(mode);
                if (expectedCategory != category)
                    throw new FormatException("Event mode " + item.mode + " belongs in " + expectedCategory + "/.");
                if (!identities.Add(mode + ":" + item.name))
                    throw new FormatException("Duplicate event identity: " + mode + "/" + item.name);
                events.Add(item);
            }
            catch (Exception ex) { Debug.LogWarning("[BCL Events] Skipped " + file + ": " + ex.Message); }
        }
        return new LegacyEventsConfig.Config { version = 1, events = events.ToArray() };
    }
    private static string NormalizeMode(string mode)
    {
        if (string.Equals(mode, "Parkur", StringComparison.OrdinalIgnoreCase)) return "RaceParcur";
        if (string.Equals(mode, "Survival", StringComparison.OrdinalIgnoreCase)) return "CS_Sur";
        return mode;
    }
    private static string CategoryForMode(string mode)
    {
        mode = NormalizeMode(mode);
        if (string.Equals(mode, "Race", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(mode, "RaceParcur", StringComparison.OrdinalIgnoreCase)) return "Race";
        if (string.Equals(mode, "CS", StringComparison.OrdinalIgnoreCase)) return "CS";
        if (string.Equals(mode, "CS_Sur", StringComparison.OrdinalIgnoreCase)) return "CS_Sur";
        throw new FormatException("Unknown event mode: " + mode);
    }
    internal static void Migrate(string directory, string legacyCollection)
    {
        if (string.IsNullOrEmpty(legacyCollection) || !File.Exists(legacyCollection)) return;
        string marker = Path.Combine(directory, ".legacy-" + SafeName(Path.GetFileName(legacyCollection)) + "-migrated");
        if (File.Exists(marker)) return;
        try
        {
            var collection = JObject.Parse(File.ReadAllText(legacyCollection))["events"] as JArray;
            if (collection == null) throw new FormatException("Legacy collection has no events array.");
            foreach (var token in collection)
            {
                var item = token as JObject;
                if (item == null || string.IsNullOrWhiteSpace((string)item["name"])) throw new FormatException("Legacy event has no name.");
                string stem = SafeName((string)item["name"]);
                string categoryDirectory = Path.Combine(directory, CategoryForMode((string)item["mode"]));
                Directory.CreateDirectory(categoryDirectory);
                string path = Path.Combine(categoryDirectory, stem + ".json");
                if (File.Exists(path))
                {
                    try { if (JToken.DeepEquals(JObject.Parse(File.ReadAllText(path)), item)) continue; }
                    catch { }
                    // Keep both custom files. Manual edits already in the events
                    // directory take priority when identities conflict.
                    int index = 1;
                    bool alreadyMigrated = false;
                    do
                    {
                        path = Path.Combine(categoryDirectory, stem + ".legacy-" + index++ + ".json");
                        if (!File.Exists(path)) break;
                        try
                        {
                            if (JToken.DeepEquals(JObject.Parse(File.ReadAllText(path)), item))
                            { alreadyMigrated = true; break; }
                        }
                        catch { }
                    } while (true);
                    if (alreadyMigrated) continue;
                }
                LegacyFileStorage.WriteTextAtomically(path, item.ToString(Formatting.Indented));
            }
            LegacyFileStorage.WriteTextAtomically(marker, Path.GetFullPath(legacyCollection));
            Debug.Log("[BCL Events] Legacy collection split into " + directory + ". Original file retained: " + legacyCollection);
        }
        catch (Exception ex) { Debug.LogError("[BCL Events] Migration failed; original retained: " + ex.Message); }
    }
    internal static string SafeName(string name)
    {
        char[] invalid = "<>:\"/\\|?*".ToCharArray();
        string safe = new string((name ?? "event").Select(ch => char.IsControl(ch) || invalid.Contains(ch) ? '_' : ch).ToArray()).Trim(' ', '.');
        if (safe.Length == 0) safe = "event";
        if (safe.Length > 120) safe = safe.Substring(0, 120);
        string device = safe.Split('.')[0].ToUpperInvariant();
        if (new[] { "CON", "PRN", "AUX", "NUL" }.Contains(device) || System.Text.RegularExpressions.Regex.IsMatch(device, @"^(COM|LPT)[1-9]$"))
            safe = "_" + safe;
        return safe;
    }
}
