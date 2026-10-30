using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

internal sealed class LegacyPluginPolicy
{
    [JsonProperty("version")] public int Version = 1;
    [JsonProperty("allowClientsWithoutManifest")] public bool AllowClientsWithoutManifest = true;
    [JsonProperty("allowClientsWithoutBclVersion")] public bool AllowClientsWithoutBclVersion = true;
    [JsonProperty("bcl")] public Rule Bcl;
    [JsonProperty("hardlist")] public List<Rule> Hardlist = new List<Rule>();
    [JsonProperty("blacklist")] public List<Rule> Blacklist = new List<Rule>();
    internal sealed class Rule
    {
        [JsonProperty("guid")] public string Guid;
        [JsonProperty("name")] public string Name;
        [JsonProperty("version")] public string Version;
        [JsonProperty("minVersion")] public string MinVersion;
        [JsonProperty("maxVersion")] public string MaxVersion;
        [JsonProperty("message")] public string Message;
        [JsonProperty("extra")] public string Extra;
        [JsonProperty("messageRu")] public string MessageRu;
        [JsonProperty("extraRu")] public string ExtraRu;
        internal bool Matches(string version)
        {
            if (Version == null && MinVersion == null && MaxVersion == null) return true;
            LegacyPluginVersion parsed;
            if (!LegacyPluginVersion.TryParse(version, out parsed)) return false;
            return (Version == null || parsed.CompareTo(LegacyPluginVersion.Parse(Version)) == 0) &&
                (MinVersion == null || parsed.CompareTo(LegacyPluginVersion.Parse(MinVersion)) >= 0) &&
                (MaxVersion == null || parsed.CompareTo(LegacyPluginVersion.Parse(MaxVersion)) <= 0);
        }
        internal string Requirement { get { return Version ?? (MinVersion == null && MaxVersion == null ? "installed" :
            (MinVersion == null ? "" : ">= " + MinVersion) + (MinVersion != null && MaxVersion != null ? ", " : "") + (MaxVersion == null ? "" : "<= " + MaxVersion)); } }
    }
    internal sealed class Violation
    {
        internal Rule Rule; internal LegacyPluginRecord Plugin; internal string Kind, Reason;
    }
    internal static LegacyPluginPolicy Parse(string json)
    {
        var policy = JsonConvert.DeserializeObject<LegacyPluginPolicy>(json, new JsonSerializerSettings { MaxDepth = 12, MissingMemberHandling = MissingMemberHandling.Error });
        if (policy == null || policy.Version != 1 || policy.Hardlist == null || policy.Blacklist == null) throw new FormatException("Invalid PluginsPolicy schema/version.");
        if (policy.Bcl != null) { policy.Bcl.Guid = "com.bigcitylegacy.baseplugin"; policy.Bcl.Name = "BigCityLegacy"; }
        foreach (var list in new[] { policy.Hardlist, policy.Blacklist })
        {
            if (list.Count > 256) throw new FormatException("Too many plugin rules.");
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in list)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.Guid) || rule.Guid.Length > 128 || rule.Guid != rule.Guid.Trim() || !seen.Add(rule.Guid)) throw new FormatException("Invalid/duplicate policy GUID.");
                foreach (string version in new[] { rule.Version, rule.MinVersion, rule.MaxVersion }) if (version != null) LegacyPluginVersion.Parse(version);
                if (rule.Version != null && (rule.MinVersion != null || rule.MaxVersion != null)) throw new FormatException("Use version or minVersion/maxVersion, not both.");
                if (rule.MinVersion != null && rule.MaxVersion != null && LegacyPluginVersion.Parse(rule.MinVersion).CompareTo(LegacyPluginVersion.Parse(rule.MaxVersion)) > 0) throw new FormatException("Reversed plugin version range.");
                if ((rule.Name ?? "").Length > 128 || (rule.Message ?? "").Length > 4000 || (rule.Extra ?? "").Length > 4000 || (rule.MessageRu ?? "").Length > 4000 || (rule.ExtraRu ?? "").Length > 4000) throw new FormatException("Plugin policy text is too long.");
            }
        }
        if (policy.Bcl != null)
        {
            // Reuse the same validation for the independent BCL version constraint.
            var wrapper = new LegacyPluginPolicy(); wrapper.Hardlist.Add(policy.Bcl);
            Parse(JsonConvert.SerializeObject(wrapper));
        }
        return policy;
    }
    internal List<Violation> Evaluate(IList<LegacyPluginRecord> plugins, string bclVersion = null)
    {
        var result = new List<Violation>();
        if (bclVersion == null)
        { if (!AllowClientsWithoutBclVersion) result.Add(new Violation { Kind = "missingBclVersion", Reason = "This server requires the client BCL version." }); }
        else if (Bcl != null && !Bcl.Matches(bclVersion))
            result.Add(new Violation { Rule = Bcl, Plugin = new LegacyPluginRecord { Guid = Bcl.Guid, Name = "BigCityLegacy", Version = bclVersion }, Kind = "bcl", Reason = "Client BigCityLegacy version does not match the server policy." });
        if (plugins == null)
        { if (!AllowClientsWithoutManifest) result.Add(new Violation { Kind = "missingManifest", Reason = "This server requires a plugin manifest (BCL 1.4.2 or newer)." }); return result; }
        var installed = new Dictionary<string, LegacyPluginRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var plugin in plugins) installed.Add(plugin.Guid, plugin);
        foreach (var rule in Hardlist)
        {
            LegacyPluginRecord plugin; installed.TryGetValue(rule.Guid, out plugin);
            if (plugin == null || !rule.Matches(plugin.Version)) result.Add(new Violation { Rule = rule, Plugin = plugin, Kind = "hardlist", Reason = plugin == null ? "Required plugin is missing." : "Required plugin version does not match." });
        }
        foreach (var rule in Blacklist)
        {
            LegacyPluginRecord plugin;
            if (installed.TryGetValue(rule.Guid, out plugin) && rule.Matches(plugin.Version)) result.Add(new Violation { Rule = rule, Plugin = plugin, Kind = "blacklist", Reason = "Installed plugin/version is prohibited." });
        }
        return result;
    }
    internal const string DefaultText = "{\n  \"version\": 1,\n  \"allowClientsWithoutManifest\": true,\n  \"allowClientsWithoutBclVersion\": true,\n  \"bcl\": null,\n  \"hardlist\": [],\n  \"blacklist\": []\n}\n";
}

// Numeric versions (1.2 == 1.2.0), optional fourth component and SemVer prerelease/build suffix.
internal sealed class LegacyPluginVersion : IComparable<LegacyPluginVersion>
{
    private readonly long[] numbers = new long[4]; private string[] prerelease;
    internal static LegacyPluginVersion Parse(string text)
    { LegacyPluginVersion result; if (!TryParse(text, out result)) throw new FormatException("Invalid plugin version: " + text); return result; }
    internal static bool TryParse(string text, out LegacyPluginVersion result)
    {
        result = null;
        if (text == null || text.Length > 64 || !Regex.IsMatch(text, @"^\d+\.\d+(?:\.\d+){0,2}(?:-[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?(?:\+[0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*)?$")) return false;
        string clean = text.Split('+')[0]; int dash = clean.IndexOf('-'); string numeric = dash < 0 ? clean : clean.Substring(0, dash);
        var value = new LegacyPluginVersion(); string[] parts = numeric.Split('.');
        for (int i = 0; i < parts.Length; i++) if (!long.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out value.numbers[i])) return false;
        if (dash >= 0) value.prerelease = clean.Substring(dash + 1).Split('.'); result = value; return true;
    }
    public int CompareTo(LegacyPluginVersion other)
    {
        for (int i = 0; i < 4; i++) { int compare = numbers[i].CompareTo(other.numbers[i]); if (compare != 0) return compare; }
        if (prerelease == null || other.prerelease == null) return prerelease == other.prerelease ? 0 : prerelease == null ? 1 : -1;
        for (int i = 0; i < Math.Min(prerelease.Length, other.prerelease.Length); i++)
        {
            string a = prerelease[i], b = other.prerelease[i]; bool an = Regex.IsMatch(a, @"^\d+$"), bn = Regex.IsMatch(b, @"^\d+$");
            int compare;
            if (an && bn) { a = a.TrimStart('0'); b = b.TrimStart('0'); compare = a.Length == b.Length ? string.CompareOrdinal(a, b) : a.Length.CompareTo(b.Length); }
            else compare = an != bn ? (an ? -1 : 1) : string.CompareOrdinal(a, b);
            if (compare != 0) return compare;
        }
        return prerelease.Length.CompareTo(other.prerelease.Length);
    }
}
