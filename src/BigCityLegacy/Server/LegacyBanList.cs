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

    internal static bool IsBanned(string guid, string ip, out BanEntry matchedEntry)
    {
        matchedEntry = null;
        EnsureLoaded();

        string normalizedGuid = NormalizeGuid(guid);
        string normalizedIp = NormalizeIp(ip);

        lock (Sync)
        {
            if (config == null || config.Bans == null)
            {
                return false;
            }

            for (int i = 0; i < config.Bans.Count; i++)
            {
                BanEntry entry = config.Bans[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.Type == BanTypeGuid &&
                    !string.IsNullOrEmpty(normalizedGuid) &&
                    string.Equals(NormalizeGuid(entry.GUID), normalizedGuid, StringComparison.OrdinalIgnoreCase))
                {
                    matchedEntry = entry;
                    return true;
                }

                if (entry.Type == BanTypeIp &&
                    !string.IsNullOrEmpty(normalizedIp) &&
                    string.Equals(NormalizeIp(entry.IP), normalizedIp, StringComparison.OrdinalIgnoreCase))
                {
                    matchedEntry = entry;
                    return true;
                }
            }
        }

        return false;
    }

    internal static bool AddBan(LegacyServerAdminService.ServerPlayerSnapshot player, int type, out BanEntry entry, out string error)
    {
        entry = null;
        error = null;

        if (player == null)
        {
            error = "Player was not found.";
            return false;
        }

        if (type != BanTypeGuid && type != BanTypeIp)
        {
            error = "Ban type must be 1 (GUID) or 2 (IP).";
            return false;
        }

        string guid = NormalizeGuid(player.Guid);
        string ip = NormalizeIp(player.Ip);

        if (type == BanTypeGuid && string.IsNullOrEmpty(guid))
        {
            error = "The selected player has no GUID.";
            return false;
        }

        if (type == BanTypeIp && string.IsNullOrEmpty(ip))
        {
            error = "The selected player has no IP address.";
            return false;
        }

        EnsureLoaded();

        lock (Sync)
        {
            if (config == null)
            {
                config = new BanListConfig();
            }
            if (config.Bans == null)
            {
                config.Bans = new List<BanEntry>();
            }

            for (int i = 0; i < config.Bans.Count; i++)
            {
                BanEntry existing = config.Bans[i];
                if (existing == null || existing.Type != type)
                {
                    continue;
                }

                bool alreadyExists = type == BanTypeGuid
                    ? string.Equals(NormalizeGuid(existing.GUID), guid, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(NormalizeIp(existing.IP), ip, StringComparison.OrdinalIgnoreCase);

                if (alreadyExists)
                {
                    entry = existing;
                    error = "This ban already exists (BanID " + existing.BanID.ToString() + ").";
                    return false;
                }
            }

            entry = new BanEntry
            {
                BanID = GetNextBanIdLocked(),
                GUID = guid,
                IP = ip,
                Type = type,
                Nickname = player.Nickname ?? string.Empty
            };

            config.Bans.Add(entry);
            if (!SaveLocked(out error))
            {
                config.Bans.Remove(entry);
                entry = null;
                return false;
            }
        }

        return true;
    }

    internal static bool RemoveBan(int banId, out BanEntry removed, out string error)
    {
        removed = null;
        error = null;

        if (banId <= 0)
        {
            error = "BanID must be a positive number.";
            return false;
        }

        EnsureLoaded();

        lock (Sync)
        {
            if (config == null || config.Bans == null)
            {
                error = "Ban list is empty.";
                return false;
            }

            for (int i = 0; i < config.Bans.Count; i++)
            {
                BanEntry entry = config.Bans[i];
                if (entry == null || entry.BanID != banId)
                {
                    continue;
                }

                config.Bans.RemoveAt(i);
                if (!SaveLocked(out error))
                {
                    config.Bans.Insert(i, entry);
                    return false;
                }

                removed = entry;
                return true;
            }
        }

        error = "BanID " + banId.ToString() + " was not found.";
        return false;
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

            if (entry.Type != BanTypeGuid && entry.Type != BanTypeIp)
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

        [JsonProperty("Type")]
        public int Type;

        [JsonProperty("Nickname")]
        public string Nickname;
    }
}
