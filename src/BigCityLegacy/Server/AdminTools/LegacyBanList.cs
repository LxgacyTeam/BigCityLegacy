using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using UnityEngine;

/// <summary>
/// Server-local ban storage. The list is intentionally enforced only by the server;
/// no new messages or client-side code are required by legacy clients.
/// </summary>
internal static class LegacyBanList
{
    internal const int BanTypeGuid = 1;
    internal const int BanTypeIp = 2;
    internal const int BanTypeNode = 3, BanTypeNodeIp = 4;
    private const int MaxIPs = 256, MaxNodes = 128, MaxGuids = 256;

    private const string ConfigArg = "-banList";
    private const string ConfigFileName = "BanList.json";

    private static readonly object Sync = new object();
    private static BanListConfig config = new BanListConfig();
    private static string loadedPath;
    private static bool loaded;

    internal static string LoadedPath
    {
        get { return loadedPath; }
    }

    internal static int Count
    {
        get
        {
            lock (Sync)
            {
                return config != null && config.Bans != null ? config.Bans.Count : 0;
            }
        }
    }

    internal static bool TryLoad()
    {
        if (!NetManagerTools.isCommandLineArgHaveServerStr())
        {
            return false;
        }

        lock (Sync)
        {
            return LoadLocked();
        }
    }

    internal static bool Reload()
    {
        lock (Sync)
        {
            loaded = false;
            return LoadLocked();
        }
    }

    internal static List<BanEntry> ActiveBans()
    {
        EnsureLoaded(); lock (Sync)
        {
            long now = LegacyAdminTime.Now;
            var result = new List<BanEntry>();
            foreach (var entry in config.Bans)
                if (entry != null && (entry.Type >= BanTypeGuid && entry.Type <= BanTypeNodeIp) && LegacyAdminTime.IsActive(entry.Until,now))
                    result.Add(new BanEntry { BanID=entry.BanID,Type=entry.Type,GUID=entry.GUID,IP=entry.IP,Nickname=entry.Nickname,Until=entry.Until,Reason=entry.Reason,GUIDs=entry.GUIDs == null ? null : new List<string>(entry.GUIDs),IPs=entry.IPs == null ? null : new List<string>(entry.IPs),Fingerprints=entry.Fingerprints == null ? null : new List<string>(entry.Fingerprints) });
            result.Sort((a,b)=>a.BanID.CompareTo(b.BanID)); return result;
        }
    }

    internal static bool IsBanned(string guid, string ip, out BanEntry matchedEntry)
    { return IsBanned(guid, ip, null, out matchedEntry); }

    internal static bool IsBanned(string guid, string ip, string fingerprint, out BanEntry matchedEntry)
    {
        EnsureLoaded();
        string normalizedGuid = NormalizeGuid(guid), normalizedIp = NormalizeIp(ip);
        bool usable = LegacyNodeCodec.Usable(fingerprint);
        if (usable) fingerprint = fingerprint.ToUpperInvariant();
        long now = LegacyAdminTime.Now;
        lock (Sync)
        {
            matchedEntry = null; BanListConfig rollback = null;
            foreach (BanEntry entry in config.Bans)
            {
                if (entry == null || entry.Type < BanTypeGuid || entry.Type > BanTypeNodeIp || !LegacyAdminTime.IsActive(entry.Until, now)) continue;
                bool byGuid = MatchesGuid(entry,normalizedGuid);
                bool byIp = normalizedIp.Length > 0 && (string.Equals(entry.IP, normalizedIp, StringComparison.OrdinalIgnoreCase) ||
                    entry.IPs != null && entry.IPs.Contains(normalizedIp));
                bool byNode = usable && entry.Fingerprints != null && entry.Fingerprints.Exists(f => LegacyNodeCodec.Matches(f,fingerprint));
                bool match = byGuid ||
                    entry.Type == BanTypeIp && byIp || entry.Type == BanTypeNode && byNode || entry.Type == BanTypeNodeIp && (byNode || byIp);
                if (!match) continue;
                if (matchedEntry == null) matchedEntry = entry;
                if (entry.Type != BanTypeNodeIp) continue;
                bool addGuid = normalizedGuid.Length > 0 && !byGuid && (entry.GUIDs == null || entry.GUIDs.Count < MaxGuids);
                bool addIp = normalizedIp.Length > 0 && !byIp && (entry.IPs == null || entry.IPs.Count < MaxIPs);
                bool addNode = usable && (entry.Fingerprints == null || !entry.Fingerprints.Contains(fingerprint) && entry.Fingerprints.Count < MaxNodes);
                if (!addGuid && !addIp && !addNode) continue;
                if (rollback == null) rollback = CloneConfig();
                if (entry.GUIDs == null) entry.GUIDs = new List<string>();
                if (entry.IPs == null) entry.IPs = new List<string>();
                if (entry.Fingerprints == null) entry.Fingerprints = new List<string>();
                if (addGuid) entry.GUIDs.Add(normalizedGuid);
                if (addIp) entry.IPs.Add(normalizedIp);
                if (addNode) entry.Fingerprints.Add(fingerprint);
            }
            if (rollback != null)
            {
                string error;
                if (!SaveLocked(out error)) { config = rollback; Debug.LogError("[Server] Failed to persist learned ban aliases: " + error); }
            }
            return matchedEntry != null;
        }
    }

    internal static List<PlayerProp> GetProps(string guid)
    {
        EnsureLoaded();
        var result = new List<PlayerProp>();
        guid = NormalizeGuid(guid);
        if (guid.Length == 0) return result;
        long now = LegacyAdminTime.Now;
        lock (Sync)
        {
            foreach (BanEntry entry in config.Bans)
            {
                if (entry == null || !SameGuid(entry.GUID, guid) || entry.Props == null) continue;
                foreach (PlayerProp prop in entry.Props)
                {
                    if (prop == null || !IsKnownProp(prop.Prop) || !LegacyAdminTime.IsActive(prop.Until, now)) continue;
                    PlayerProp previous = result.Find(p => p.Prop == prop.Prop);
                    if (previous == null) result.Add(new PlayerProp { Prop = prop.Prop, Until = prop.Until });
                    else if (!prop.Until.HasValue || (previous.Until.HasValue && prop.Until > previous.Until)) previous.Until = prop.Until;
                }
            }
        }
        return result;
    }

    internal static bool HasProp(string guid, string prop)
    {
        return GetProps(guid).Exists(p => p.Prop == prop);
    }

    internal static bool IsKnownProp(string prop)
    {
        return prop == "noweapon" || prop == "nomove" || prop == "nocars" || prop == "nochat" || prop == "invisible";
    }

    private static bool SameGuid(string a, string b)
    {
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesGuid(BanEntry entry, string guid)
    {
        return guid.Length > 0 && (SameGuid(entry.GUID,guid) || entry.Type == BanTypeNodeIp &&
            entry.GUIDs != null && entry.GUIDs.Exists(value => SameGuid(value,guid)));
    }

    internal static bool AddBan(LegacyServerAdminService.ServerPlayerSnapshot player, int banType, long? until, out BanEntry addedEntry, out string error, string reason = null)
    {
        addedEntry = null;
        error = null;
        if (player == null || (banType < BanTypeGuid || banType > BanTypeNodeIp))
        { error = "Ban type must be 1 (GUID), 2 (GUID/IP), 3 (GUID/fingerprint) or 4 (linked GUID/IP/fingerprint)."; return false; }
        string guid = NormalizeGuid(player.Guid), ip = NormalizeIp(player.Ip);
        string fingerprint = player.Fingerprint;
        if (banType >= BanTypeNode && !LegacyNodeCodec.Usable(fingerprint))
        { error = "Client has no usable fingerprint (at least two independent groups required)."; return false; }
        if (banType >= BanTypeNode) fingerprint = fingerprint.ToUpperInvariant();
        if (guid.Length == 0 || ((banType == BanTypeIp || banType == BanTypeNodeIp) && ip.Length == 0))
        { error = "The player has no identifier for this ban type."; return false; }
        EnsureLoaded();
        lock (Sync)
        {
            foreach (BanEntry entry in config.Bans)
                if (entry != null && entry.Type == banType && LegacyAdminTime.IsActive(entry.Until, LegacyAdminTime.Now) &&
                    (MatchesGuid(entry,guid) || (banType == BanTypeGuid ? false : banType == BanTypeIp ? string.Equals(entry.IP, ip, StringComparison.OrdinalIgnoreCase) :
                        entry.Fingerprints != null && entry.Fingerprints.Exists(f => LegacyNodeCodec.Matches(f,fingerprint)) ||
                        banType == BanTypeNodeIp && (entry.IP == ip || entry.IPs != null && entry.IPs.Contains(ip)))))
                { error = "This ban already exists as BanID " + entry.BanID + "."; return false; }
            BanListConfig previous = CloneConfig();
            BanEntry added = config.Bans.Find(e => e != null && e.Type == 0 && guid.Length > 0 && SameGuid(e.GUID, guid));
            if (added == null)
            {
                added = new BanEntry { BanID = GetNextBanIdLocked(), GUID = guid, IP = ip };
                config.Bans.Add(added);
            }
            added.Type = banType;
            added.GUIDs = banType == BanTypeNodeIp ? new List<string> { guid } : null;
            added.IPs = banType >= BanTypeNode ? new List<string> { ip } : null;
            added.Fingerprints = banType >= BanTypeNode ? new List<string> { fingerprint } : null;
            added.Nickname = player.Nickname ?? string.Empty;
            added.Until = until;
            added.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason;
            if (!SaveLocked(out error)) { config = previous; return false; }
            addedEntry = added;
            return true;
        }
    }

    internal static bool SetProp(LegacyServerAdminService.ServerPlayerSnapshot player, string name, bool enabled, long? until, out string error)
    {
        error = null;
        if (player == null || string.IsNullOrEmpty(NormalizeGuid(player.Guid)))
        { error = "The player has no GUID."; return false; }
        if (!IsKnownProp(name)) { error = "Unknown property."; return false; }
        EnsureLoaded();
        lock (Sync)
        {
            BanListConfig previous = CloneConfig();
            BanEntry destination = null;
            foreach (BanEntry entry in config.Bans)
            {
                if (entry == null || !SameGuid(entry.GUID, NormalizeGuid(player.Guid))) continue;
                if (destination == null) destination = entry;
                if (entry.Props != null) entry.Props.RemoveAll(p => p != null && p.Prop == name);
            }
            if (enabled)
            {
                if (destination == null)
                {
                    destination = new BanEntry { BanID = GetNextBanIdLocked(), GUID = NormalizeGuid(player.Guid), IP = NormalizeIp(player.Ip), Type = 0 };
                    config.Bans.Add(destination);
                }
                destination.Nickname = player.Nickname ?? string.Empty;
                if (destination.Props == null) destination.Props = new List<PlayerProp>();
                destination.Props.Add(new PlayerProp { Prop = name, Until = until });
            }
            PruneLocked(LegacyAdminTime.Now);
            if (!SaveLocked(out error)) { config = previous; return false; }
            return true;
        }
    }

    internal static bool RemoveBan(int banId, out BanEntry removedEntry, out string error)
    {
        removedEntry = null;
        error = null;
        EnsureLoaded();
        lock (Sync)
        {
            BanEntry entry = config.Bans.Find(e => e != null && e.BanID == banId);
            if (entry == null || entry.Type == 0) { error = "Active ban not found for BanID " + banId + "."; return false; }
            BanListConfig previous = CloneConfig();
            entry.Type = 0;
            entry.Until = null;
            PruneLocked(LegacyAdminTime.Now);
            if (!SaveLocked(out error)) { config = previous; return false; }
            removedEntry = entry;
            return true;
        }
    }

    internal static void ExpireRestrictions()
    {
        EnsureLoaded();
        lock (Sync)
        {
            long now = LegacyAdminTime.Now;
            if (!config.Bans.Exists(e => e != null &&
                ((e.Type != 0 && !LegacyAdminTime.IsActive(e.Until, now)) ||
                 (e.Type == 0 && (e.Props == null || e.Props.Count == 0)) ||
                 (e.Props != null && e.Props.Exists(p => p == null || !LegacyAdminTime.IsActive(p.Until, now)))))) return;
            BanListConfig previous = CloneConfig();
            PruneLocked(now);
            string error;
            if (!SaveLocked(out error))
            {
                config = previous; // Reads still ignore expired restrictions; retry persistence on the next tick.
                Debug.LogWarning("[Server] Failed to save expired restrictions: " + error);
            }
        }
    }

    private static bool PruneLocked(long now)
    {
        bool changed = false;
        for (int i = config.Bans.Count - 1; i >= 0; i--)
        {
            BanEntry e = config.Bans[i];
            if (e == null) continue;
            if (e.Type != 0 && !LegacyAdminTime.IsActive(e.Until, now)) { e.Type = 0; e.Until = null; changed = true; }
            if (e.Props != null && e.Props.RemoveAll(p => p == null || !LegacyAdminTime.IsActive(p.Until, now)) > 0) changed = true;
            if (e.Type == 0 && (e.Props == null || e.Props.Count == 0)) { config.Bans.RemoveAt(i); changed = true; }
        }
        return changed;
    }

    private static BanListConfig CloneConfig()
    {
        // Mutations are infrequent. A full rollback also preserves properties spread over multiple entries.
        return JsonConvert.DeserializeObject<BanListConfig>(JsonConvert.SerializeObject(config));
    }

    internal static string NormalizeGuid(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
    }

    internal static string NormalizeIp(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        string normalized = value.Trim();

        // Some UNet transports expose an IPv4-mapped IPv6 address.
        IPAddress parsed;
        if (IPAddress.TryParse(normalized, out parsed))
        {
            if (parsed.IsIPv4MappedToIPv6)
            {
                parsed = parsed.MapToIPv4();
            }
            return parsed.ToString();
        }

        return normalized;
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        lock (Sync)
        {
            if (!loaded)
            {
                LoadLocked();
            }
        }
    }

    private static bool LoadLocked()
    {
        string path = LegacyHelpers.GetJsonPath(ConfigArg, ConfigFileName);
        loadedPath = path;

        try
        {
            EnsureDirectory(path);

            if (!File.Exists(path))
            {
                config = new BanListConfig();
                string createError;
                if (!SaveLocked(out createError))
                {
                    loaded = false;
                    Debug.LogError("Failed to create ban list: " + path + "\n" + createError);
                    return false;
                }

                loaded = true;
                Debug.Log("Ban list not found. Created an empty config: " + path);
                return true;
            }

            string json = File.ReadAllText(path);
            // A broken moderation file must never silently become an empty list.
            // Unlike cosmetic configs, preserve the previous in-memory bans on parse errors.
            BanListConfig parsed = JsonConvert.DeserializeObject<BanListConfig>(json);
            if (parsed == null)
            {
                throw new InvalidDataException("Ban list JSON resolved to null.");
            }
            if (parsed.Bans == null)
            {
                parsed.Bans = new List<BanEntry>();
            }

            NormalizeLoadedEntries(parsed.Bans);
            config = parsed;
            loaded = true;
            Debug.Log("Ban list loaded: " + path + ", entries: " + config.Bans.Count.ToString());
            return true;
        }
        catch (Exception ex)
        {
            // Keep the previous in-memory list if a manual edit produced invalid JSON.
            loaded = config != null;
            Debug.LogError("Failed to load ban list: " + path + "\n" + ex);
            return false;
        }
    }

    private static void NormalizeLoadedEntries(List<BanEntry> entries)
    {
        int fallbackId = 1;
        HashSet<int> usedIds = new HashSet<int>();

        for (int i = 0; i < entries.Count; i++)
        {
            BanEntry entry = entries[i];
            if (entry == null)
            {
                continue;
            }

            entry.GUID = NormalizeGuid(entry.GUID);
            entry.IP = NormalizeIp(entry.IP);
            entry.Nickname = entry.Nickname ?? string.Empty;
            if (entry.Props != null)
                foreach (PlayerProp prop in entry.Props)
                    if (prop != null) prop.Prop = (prop.Prop ?? string.Empty).Trim().ToLowerInvariant();

            if (entry.Type == BanTypeNodeIp)
            {
                if (entry.GUIDs != null && entry.GUIDs.Count > MaxGuids) throw new InvalidDataException("Too many ban GUID aliases.");
                var guids = new List<string>();
                if (entry.GUID.Length > 0) guids.Add(entry.GUID);
                if (entry.GUIDs != null) foreach (string alias in entry.GUIDs)
                { string value = NormalizeGuid(alias); if (value.Length > 0 && !guids.Exists(g => SameGuid(g,value))) guids.Add(value); }
                if (guids.Count > MaxGuids) throw new InvalidDataException("Too many ban GUID aliases.");
                entry.GUIDs = guids;
            }

            if (entry.Type == BanTypeNode || entry.Type == BanTypeNodeIp)
            {
                if (entry.Fingerprints == null || entry.Fingerprints.Count == 0 || entry.Fingerprints.Count > MaxNodes ||
                    entry.IPs != null && entry.IPs.Count > MaxIPs) throw new InvalidDataException("Invalid hardware ban aliases for BanID " + entry.BanID);
                var nodes = new List<string>();
                foreach (string node in entry.Fingerprints)
                { if (!LegacyNodeCodec.Usable(node)) throw new InvalidDataException("Invalid hardware ban fingerprint."); string value = node.ToUpperInvariant(); if (!nodes.Contains(value)) nodes.Add(value); }
                entry.Fingerprints = nodes;
                var addresses = new List<string>();
                if (!string.IsNullOrEmpty(entry.IP)) addresses.Add(entry.IP);
                if (entry.IPs != null) foreach (string address in entry.IPs)
                { string value = NormalizeIp(address); if (value.Length > 0 && !addresses.Contains(value)) addresses.Add(value); }
                if (addresses.Count > MaxIPs) throw new InvalidDataException("Too many hardware ban IP aliases.");
                entry.IPs = addresses;
            }

            if (entry.Type < 0 || entry.Type > BanTypeNodeIp)
            {
                Debug.LogWarning("Ignoring invalid ban type for BanID " + entry.BanID.ToString() + ". It will never match until fixed.");
            }

            if (entry.BanID <= 0 || usedIds.Contains(entry.BanID))
            {
                while (usedIds.Contains(fallbackId))
                {
                    fallbackId++;
                }
                entry.BanID = fallbackId++;
            }

            usedIds.Add(entry.BanID);
        }
    }

    private static int GetNextBanIdLocked()
    {
        int max = 0;
        for (int i = 0; i < config.Bans.Count; i++)
        {
            BanEntry entry = config.Bans[i];
            if (entry != null && entry.BanID > max)
            {
                max = entry.BanID;
            }
        }
        return max + 1;
    }

    private static bool SaveLocked(out string error)
    {
        error = null;

        try
        {
            string path = loadedPath;
            if (string.IsNullOrEmpty(path))
            {
                path = LegacyHelpers.GetJsonPath(ConfigArg, ConfigFileName);
                loadedPath = path;
            }

            EnsureDirectory(path);
            string json = JsonConvert.SerializeObject(config, Formatting.Indented);
            string tempPath = path + ".tmp";

            File.WriteAllText(tempPath, json);
            if (File.Exists(path))
            {
                try
                {
                    File.Replace(tempPath, path, path + ".bak", true);
                }
                catch
                {
                    // File.Replace is not supported by every Mono filesystem.
                    // Copy keeps the old file present until the replacement bytes are ready.
                    File.Copy(tempPath, path, true);
                    File.Delete(tempPath);
                }
            }
            else
            {
                File.Move(tempPath, path);
            }
            loaded = true;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static void EnsureDirectory(string path)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    [Serializable]
    internal sealed class BanListConfig
    {
        [JsonProperty("Version")]
        public int Version = 1;

        [JsonProperty("Bans")]
        public List<BanEntry> Bans = new List<BanEntry>();
    }

    [Serializable]
    internal sealed class BanEntry
    {
        [JsonProperty("BanID")]
        public int BanID;

        [JsonProperty("GUID")]
        public string GUID;

        [JsonProperty("IP")]
        public string IP;

        [JsonProperty("GUIDs", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> GUIDs;

        [JsonProperty("IPs", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> IPs;

        [JsonProperty("Fingerprints", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Fingerprints;

        [JsonProperty("Type")]
        public int Type;

        [JsonProperty("Nickname")]
        public string Nickname;

        [JsonProperty("Reason", NullValueHandling = NullValueHandling.Ignore)]
        public string Reason;

        [JsonProperty("until", NullValueHandling = NullValueHandling.Ignore)]
        public long? Until;

        [JsonProperty("Props", NullValueHandling = NullValueHandling.Ignore)]
        public List<PlayerProp> Props;
    }

    [Serializable]
    internal sealed class PlayerProp
    {
        [JsonProperty("Prop")]
        public string Prop;

        [JsonProperty("until", NullValueHandling = NullValueHandling.Ignore)]
        public long? Until;
    }
}
