using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Net;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

internal sealed class LegacyMirrorAddress
{
    internal string Host;
    internal int Port;
    internal long UpdatedAt;
}

internal static class LegacyPublicMirror
{
    internal const string FallbackHost = "46.8.197.224";
    internal const int FallbackPort = 3050;
    private static LegacyMirrorAddress address;
    private static MonoBehaviour owner;
    private static bool refreshing;
    internal static bool StartupComplete { get; private set; }
    internal static string CachePath { get { return Path.Combine(LegacyHelpers.ModDataPath, "config", "PublicMirror.json"); } }
    internal static LegacyMirrorAddress Address
    {
        get
        {
            if (address == null)
            {
                address = new LegacyMirrorAddress { Host = FallbackHost, Port = FallbackPort, UpdatedAt = -1 };
                try
                {
                    LegacyMirrorAddress cached;
                    if (File.Exists(CachePath) && TryParse(File.ReadAllText(CachePath), out cached)) address = cached;
                }
                catch (Exception ex) { Debug.LogWarning("[BCL Mirror] Cannot read cache: " + ex.Message); }
            }
            return address;
        }
    }
    internal static void Start(MonoBehaviour coroutineOwner)
    {
        owner = coroutineOwner;
        var cached = Address;
        LegacyRepository.Changed += Refresh;
        Refresh();
    }
    internal static void Refresh()
    {
        if (owner && !refreshing) owner.StartCoroutine(RefreshRoutine());
    }
    private static IEnumerator RefreshRoutine()
    {
        refreshing = true;
        try
        {
            // If the user changes providers during a request, discard that response
            // and perform the request against the newly selected provider.
            LegacyRepositorySource source;
            do
            {
                source = LegacyRepository.Current;
                using (var request = UnityWebRequest.Get(LegacyRepository.RawUrl("remote_config/PublicMirror.json", source)))
                {
                    LegacyRepository.ConfigureRequest(request);
                    yield return request.SendWebRequest();
                    if (source != LegacyRepository.Current) continue;
                    if (request.isNetworkError || request.isHttpError)
                    {
                        Debug.LogWarning("[BCL Mirror] Remote unavailable; using cached/fallback mirror. " + request.error);
                        break;
                    }
                    LegacyMirrorAddress remote;
                    if (!TryParse(request.downloadHandler.text, out remote))
                    {
                        Debug.LogWarning("[BCL Mirror] Invalid PublicMirror.json; cached/fallback mirror retained.");
                        break;
                    }
                    if (remote.UpdatedAt > Address.UpdatedAt)
                    {
                        try { LegacyFileStorage.WriteTextAtomically(CachePath, Serialize(remote)); }
                        catch (Exception ex) { Debug.LogWarning("[BCL Mirror] Cannot save cache: " + ex.Message); }
                        address = remote;
                    }
                }
            } while (source != LegacyRepository.Current);
        }
        finally { refreshing = false; StartupComplete = true; }
    }
    internal static bool TryParse(string json, out LegacyMirrorAddress result)
    {
        result = null;
        try
        {
            var root = JObject.Parse(json);
            if (root["masterIp"] == null || root["masterIp"].Type != JTokenType.String ||
                root["masterPort"] == null || root["masterPort"].Type != JTokenType.Integer) return false;
            string host = ((string)root["masterIp"] ?? "").Trim();
            int port = (int)root["masterPort"];
            if (host.Length == 0 || host.IndexOfAny(new[] { ' ', '\r', '\n', '\t' }) >= 0 ||
                (Uri.CheckHostName(host) == UriHostNameType.Unknown && !IPAddress.TryParse(host, out _)) || port < 1 || port > 65535)
                return false;
            JToken timestamp = root["updated_at"];
            long seconds;
            if (timestamp != null && timestamp.Type == JTokenType.Date)
                seconds = timestamp.ToObject<DateTimeOffset>().ToUnixTimeSeconds();
            else if (timestamp == null || !long.TryParse(timestamp.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
            {
                DateTimeOffset parsed;
                if (timestamp == null || !DateTimeOffset.TryParse(timestamp.ToString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed)) return false;
                seconds = parsed.ToUnixTimeSeconds();
            }
            if (seconds < 0) return false;
            result = new LegacyMirrorAddress { Host = host, Port = port, UpdatedAt = seconds };
            return true;
        }
        catch { return false; }
    }
    internal static string Serialize(LegacyMirrorAddress value)
    {
        return new JObject { ["masterIp"] = value.Host, ["masterPort"] = value.Port, ["updated_at"] = value.UpdatedAt }.ToString();
    }
}
