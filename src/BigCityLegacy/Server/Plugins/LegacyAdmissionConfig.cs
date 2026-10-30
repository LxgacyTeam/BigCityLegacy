using System;
using System.IO;
using System.Text;
using UnityEngine;

internal static class LegacyAdmissionConfig
{
    internal static bool Enabled { get { return !LegacyCommandLine.HasArg("-noPluginsPolicyAndAlerts"); } }
    internal static LegacyPluginPolicy Policy { get; private set; }
    internal static LegacyAlertsConfig Alerts { get; private set; }
    internal static void Initialize()
    {
        if (!Enabled)
        { Policy = new LegacyPluginPolicy(); Alerts = LegacyAlertsConfig.Defaults(); Debug.Log("[BCL config] PluginsPolicy and Alerts are disabled by noPluginsPolicyAndAlerts."); return; }
        var policy = LoadLogged("PluginsPolicy.json", LoadPolicy); var alerts = LoadLogged("Alerts.toml", LoadAlerts); Policy = policy; Alerts = alerts;
        Debug.Log("[BCL plugins] Admission policy loaded; allow clients without manifest=" + Policy.AllowClientsWithoutManifest + ", hardlist=" + Policy.Hardlist.Count + ", blacklist=" + Policy.Blacklist.Count);
    }
    internal static bool Reload()
    {
        try { if (!Enabled) { Debug.Log("[BCL config] PluginsPolicy/Alerts reload skipped: subsystem disabled."); return true; } var policy = LoadLogged("PluginsPolicy.json", LoadPolicy); var alerts = LoadLogged("Alerts.toml", LoadAlerts); Policy = policy; Alerts = alerts; return true; }
        catch (Exception ex) { Debug.LogError("[BCL plugins] Reload rejected; keeping previous admission/alert config: " + ex.Message); return false; }
    }
    private static T LoadLogged<T>(string file, Func<T> load)
    {
        try { T result = load(); Debug.Log("[BCL config] Loaded " + file + " successfully."); return result; }
        catch (Exception ex) { Debug.LogError("[BCL config] Failed to load " + file + ": " + ex.Message); throw; }
    }
    private static string Read(string arg, string file, string defaults)
    {
        string path = LegacyHelpers.GetJsonPath(arg, file); Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (!File.Exists(path))
        {
            if (!string.IsNullOrWhiteSpace(LegacyCommandLine.GetArgValue(arg))) throw new FileNotFoundException("Explicit admission config was not found.", path);
            File.WriteAllText(path, defaults, new UTF8Encoding(false));
        }
        if (new FileInfo(path).Length > 256 * 1024) throw new FormatException(file + " exceeds 256 KiB.");
        return File.ReadAllText(path, Encoding.UTF8);
    }
    private static LegacyPluginPolicy LoadPolicy() { return LegacyPluginPolicy.Parse(Read("-pluginsPolicy", "PluginsPolicy.json", LegacyPluginPolicy.DefaultText)); }
    private static LegacyAlertsConfig LoadAlerts() { return LegacyAlertsConfig.Parse(Read("-alerts", "Alerts.toml", LegacyAlertsConfig.DefaultText)); }
}
