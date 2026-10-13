using System;
using System.IO;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEngine;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

internal enum LegacyRepositorySource { GitHub, Codeberg }

/// <summary>One provider for every repository request made by the mod.</summary>
internal static class LegacyRepository
{
    internal const string Owner = "LxgacyTeam";
    internal const string ModRepo = "BigCityLegacy";
    internal const string InstallerRepo = "BCL-installer";
    private static bool initialized;
    private static LegacyRepositorySource current;
    internal static event Action Changed;
    internal static LegacyRepositorySource Current { get { Init(); return current; } }
    internal static string Name { get { return Current.ToString(); } }
    internal const string SourcePlayerPrefsKey = "BigCityLegacy.Repository.Source";
    private static string OldConfigPath { get { return Path.Combine(LegacyHelpers.ModDataPath, "config", "RepositorySource.json"); } }

    internal static void Init()
    {
        if (initialized) return;
        current = GetLocaleDefault();
        bool saved = PlayerPrefs.HasKey(SourcePlayerPrefsKey);
        try
        {
            if (saved) current = ParseSource(PlayerPrefs.GetString(SourcePlayerPrefsKey));
            else if (File.Exists(OldConfigPath))
                current = ParseSource((string)JObject.Parse(File.ReadAllText(OldConfigPath))["source"]);
        }
        catch (Exception ex) { Debug.LogWarning("[BCL Repository] Cannot read source: " + ex.Message); }
        string selected = LegacyCommandLine.GetArgValue("-repositorySource");
        if (!string.IsNullOrEmpty(selected)) current = ParseSource(selected);
        PlayerPrefs.SetString(SourcePlayerPrefsKey, current.ToString());
        PlayerPrefs.Save();
        initialized = true;
        // One-time migration: PlayerPrefs is the only persistent source preference.
        try { if (File.Exists(OldConfigPath)) File.Delete(OldConfigPath); }
        catch (Exception ex) { Debug.LogWarning("[BCL Repository] Cannot remove obsolete config: " + ex.Message); }
    }

    internal static LegacyRepositorySource LocaleDefault(string language, string region)
    {
        language = language ?? "";
        return (string.Equals(language, "Chinese", StringComparison.OrdinalIgnoreCase) ||
            language.StartsWith("Chinese", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(language, "zh", StringComparison.OrdinalIgnoreCase) ||
            language.StartsWith("zh-", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(region, "CN", StringComparison.OrdinalIgnoreCase))
            ? LegacyRepositorySource.Codeberg : LegacyRepositorySource.GitHub;
    }

    private static LegacyRepositorySource GetLocaleDefault()
    {
        string region = "";
        try { region = RegionInfo.CurrentRegion.TwoLetterISORegionName; } catch (ArgumentException) { }
        // Windows geographic region can differ from the display language/format locale.
        if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
        {
            try
            {
                var iso = new System.Text.StringBuilder(16);
                if (GetGeoInfo(GetUserGeoID(16), 4, iso, iso.Capacity, 0) > 0 && iso.Length > 0 && (region != "CN" || iso.ToString() == "CN")) region = iso.ToString();
            }
            catch (Exception ex) { Debug.LogWarning("[BCL Repository] Cannot read OS region: " + ex.Message); }
        }
        LegacyRepositorySource source = LocaleDefault(Application.systemLanguage.ToString(), region);
        if (source == LegacyRepositorySource.GitHub)
            source = LocaleDefault(CultureInfo.CurrentUICulture.Name, region);
        return source;
    }

    [DllImport("kernel32.dll")] private static extern int GetUserGeoID(int geoClass);
    [DllImport("kernel32.dll", EntryPoint = "GetGeoInfoW", CharSet = CharSet.Unicode)]
    private static extern int GetGeoInfo(int geoId, int geoType, System.Text.StringBuilder data, int size, int language);

    internal static LegacyRepositorySource ParseSource(string value)
    {
        if (string.Equals(value, "Codeberg", StringComparison.OrdinalIgnoreCase)) return LegacyRepositorySource.Codeberg;
        if (string.Equals(value, "GitHub", StringComparison.OrdinalIgnoreCase)) return LegacyRepositorySource.GitHub;
        throw new FormatException("Repository source must be GitHub or Codeberg.");
    }

    internal static void SetSource(LegacyRepositorySource value)
    {
        if (value != LegacyRepositorySource.GitHub && value != LegacyRepositorySource.Codeberg) throw new ArgumentOutOfRangeException("value");
        Init();
        PlayerPrefs.SetString(SourcePlayerPrefsKey, value.ToString());
        PlayerPrefs.Save();
        if (current == value) return;
        current = value;
        if (Changed != null) Changed();
    }

    internal static string WebUrl(string repo, LegacyRepositorySource source)
    {
        return (source == LegacyRepositorySource.Codeberg ? "https://codeberg.org/" : "https://github.com/") + Owner + "/" + repo;
    }
    internal static string WebUrl(string repo) { return WebUrl(repo, Current); }
    internal static string LatestReleaseUrl(string repo, LegacyRepositorySource source)
    {
        return (source == LegacyRepositorySource.Codeberg ? "https://codeberg.org/api/v1/repos/" : "https://api.github.com/repos/") +
            Owner + "/" + repo + "/releases/latest";
    }
    internal static string RawUrl(string path, LegacyRepositorySource source)
    {
        return source == LegacyRepositorySource.Codeberg
            ? WebUrl(ModRepo, source) + "/raw/branch/main/" + path
            : "https://raw.githubusercontent.com/" + Owner + "/" + ModRepo + "/main/" + path;
    }
    internal static void ConfigureRequest(UnityWebRequest request, int timeout = 10)
    {
        request.timeout = timeout;
        request.SetRequestHeader("Accept", "application/json");
        request.SetRequestHeader("User-Agent", BigCityLegacyPlugin.PluginName + "/" + VersionInfo.ModVersionName);
    }
}

internal static class LegacyFileStorage
{
    internal static void WriteTextAtomically(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, text, new System.Text.UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
