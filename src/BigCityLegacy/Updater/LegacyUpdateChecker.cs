using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

/// <summary>
/// Release checker using the globally selected repository source.
/// </summary>
internal static class LegacyUpdateChecker
{
    internal const string EnabledPlayerPrefsKey = "BigCityLegacy.Updater.Enabled";

    private const float StartupDelaySeconds = 2.0f;
    private const int RequestTimeoutSeconds = 10;

    private static bool started;
    private static MonoBehaviour coroutineOwner;
    private static bool requestInProgress;
    private static LegacyUpdateInfo latestAvailableUpdate;

    internal static bool IsEnabled
    {
        get { return PlayerPrefs.GetInt(EnabledPlayerPrefsKey, 1) != 0; }
        set
        {
            PlayerPrefs.SetInt(EnabledPlayerPrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    internal static void StartIfNeeded(MonoBehaviour owner)
    {
        if (started || owner == null)
        {
            return;
        }

        started = true;
        coroutineOwner = owner;
        LegacyRepository.Changed += SourceChanged;
        owner.StartCoroutine(CheckOnStartup());
    }
    private static void SourceChanged()
    {
        latestAvailableUpdate = null;
        LegacyUpdateWindow.Hide();
        if (IsEnabled && !LegacyCommandLine.HasNoUpdateCheck()) CheckNow(coroutineOwner);
    }

    internal static void CheckNow(MonoBehaviour owner)
    {
        if (owner == null || requestInProgress)
        {
            return;
        }

        owner.StartCoroutine(CheckRoutine(showNoUpdateLog: true));
    }

    internal static LegacyUpdateInfo LatestAvailableUpdate
    {
        get { return latestAvailableUpdate; }
    }

    private static IEnumerator CheckOnStartup()
    {
        if (LegacyCommandLine.HasNoUpdateCheck())
        {
            LogInfo("[BCL Updater] update check disabled by command line: -noUpdCheck");
            yield break;
        }

        if (!IsEnabled)
        {
            LogInfo("[BCL Updater] update check disabled by PlayerPrefs: " + EnabledPlayerPrefsKey);
            yield break;
        }

        if (StartupDelaySeconds > 0f)
        {
            yield return new WaitForSecondsRealtime(StartupDelaySeconds);
        }

        yield return CheckRoutine(showNoUpdateLog: false);
    }

    private static IEnumerator CheckRoutine(bool showNoUpdateLog)
    {
        if (requestInProgress)
        {
            yield break;
        }

        if (LegacyCommandLine.HasNoUpdateCheck())
        {
            yield break;
        }

        requestInProgress = true;

        const string repo = LegacyRepository.ModRepo;
        try
        {
            LegacyRepositorySource source;
            do
            {
                source = LegacyRepository.Current;
                using (UnityWebRequest request = UnityWebRequest.Get(LegacyRepository.LatestReleaseUrl(repo, source)))
                {
                    LegacyRepository.ConfigureRequest(request, RequestTimeoutSeconds);
                    yield return request.SendWebRequest();
                    if (source != LegacyRepository.Current) continue;
                    if (IsRequestError(request))
                    {
                        LogWarning("[BCL Updater] " + source + " update check failed: " + GetRequestError(request));
                        yield break;
                    }
                    string json = request.downloadHandler != null ? request.downloadHandler.text : null;
                    LegacyReleaseResponse release;
                    string parseError;
                    if (!TryParseRelease(json, out release, out parseError))
                    {
                        LogWarning("[BCL Updater] Release JSON parse failed: " + parseError);
                        yield break;
                    }
                    if (release == null || string.IsNullOrEmpty(release.tag_name))
                    {
                        LogWarning("[BCL Updater] Latest release response has no tag_name.");
                        yield break;
                    }
                    LegacyUpdateInfo update;
                    if (TryCreateUpdateInfo(repo, release, source, out update))
                    {
                        latestAvailableUpdate = update;
                        ShowUpdateNotification(update);
                    }
                    else if (showNoUpdateLog)
                        LogInfo("[BCL Updater] No stable BigCityLegacy update found. Current=" + VersionInfo.ModVersionName + ", latest tag=" + release.tag_name);
                }
            } while (source != LegacyRepository.Current);
        }
        finally { requestInProgress = false; }
    }

    private static void ShowUpdateNotification(LegacyUpdateInfo update)
    {
        if (update == null)
        {
            return;
        }

        if (IsServerMode())
        {
            PrintServerUpdateNotification(update);
            return;
        }

        LegacyUpdateWindow.Show(update);
    }

    private static bool IsServerMode()
    {
        return LegacyCommandLine.HasServerArg();
    }

    private static void PrintServerUpdateNotification(LegacyUpdateInfo update)
    {
        LegacyServerConsole.WriteAdminLineAfterConsoleInit("\n");
        WriteUpdaterLine("*****************************************");
        WriteUpdaterLine("New version is available: " + update.LatestVersion);
        WriteUpdaterLine(update.ReleaseUrl);
        WriteUpdaterLine("*****************************************");
        LegacyServerConsole.WriteAdminLineAfterConsoleInit("\n");
    }

    private static void WriteUpdaterLine(string text)
    {
        LegacyServerConsole.WriteAdminLineAfterConsoleInit("[Updater] " + (text ?? string.Empty));
    }

    private static void LogInfo(string text)
    {
        LegacyServerConsole.LogAfterConsoleInit(text, LogType.Log);
    }

    private static void LogWarning(string text)
    {
        LegacyServerConsole.LogAfterConsoleInit(text, LogType.Warning);
    }

    internal static bool TryParseRelease(string json, out LegacyReleaseResponse release, out string error)
    {
        release = null;
        error = null;

        if (string.IsNullOrEmpty(json))
        {
            error = "empty response";
            return false;
        }

        try
        {
            JObject root = JObject.Parse(json);

            release = new LegacyReleaseResponse
            {
                tag_name = ReadString(root, "tag_name"),
                html_url = ReadString(root, "html_url"),
                draft = (bool?)root["draft"] ?? false,
                prerelease = (bool?)root["prerelease"] ?? false,
                assets = ReadAssets(root["assets"])
            };

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static LegacyReleaseAsset[] ReadAssets(JToken assetsToken)
    {
        JArray assetsArray = assetsToken as JArray;
        if (assetsArray == null || assetsArray.Count == 0)
        {
            return new LegacyReleaseAsset[0];
        }

        List<LegacyReleaseAsset> assets = new List<LegacyReleaseAsset>();
        for (int i = 0; i < assetsArray.Count; i++)
        {
            JObject assetObject = assetsArray[i] as JObject;
            if (assetObject == null)
            {
                continue;
            }

            string browserDownloadUrl = ReadString(assetObject, "browser_download_url");
            if (string.IsNullOrEmpty(browserDownloadUrl))
            {
                continue;
            }

            assets.Add(new LegacyReleaseAsset
            {
                browser_download_url = browserDownloadUrl,
                name = ReadString(assetObject, "name"),
                state = ReadString(assetObject, "state")
            });
        }

        return assets.ToArray();
    }

    private static string ReadString(JObject obj, string name)
    {
        if (obj == null || string.IsNullOrEmpty(name))
        {
            return null;
        }

        JToken token = obj[name];
        return token != null && token.Type != JTokenType.Null ? token.ToString() : null;
    }

    private static bool TryCreateUpdateInfo(string repo, LegacyReleaseResponse release, LegacyRepositorySource source, out LegacyUpdateInfo update)
    {
        update = null;
        if (release.draft || release.prerelease) return false;

        LegacySemanticVersion remoteVersion;
        if (!LegacySemanticVersion.TryParseStableTag(release.tag_name, out remoteVersion))
        {
            LogInfo("[BCL Updater] Repository latest release tag is not a stable X.Y.Z tag, skipped: " + release.tag_name);
            return false;
        }

        LegacySemanticVersion localVersion;
        if (!LegacySemanticVersion.TryParseStableTag(VersionInfo.ModVersion, out localVersion))
        {
            LogWarning("[BCL Updater] Cannot parse local ModVersion as stable version: " + VersionInfo.ModVersion);
            return false;
        }

        int compare = remoteVersion.CompareTo(localVersion);
        bool newer = compare > 0 || (compare == 0 && BigCityLegacyPlugin.IsPrerelease);
        if (!newer)
        {
            return false;
        }

        string releaseUrl = !string.IsNullOrEmpty(release.html_url)
            ? release.html_url
            : LegacyRepository.WebUrl(repo, source) + "/releases/tag/" + release.tag_name;

        string downloadUrl = GetDownloadUrlFallback(release, releaseUrl);

        update = new LegacyUpdateInfo
        {
            CurrentVersion = VersionInfo.ModVersionName,
            LatestVersion = remoteVersion.ToString(),
            TagName = release.tag_name,
            ReleaseUrl = releaseUrl,
            DownloadUrl = downloadUrl,
            IsCurrentBuildPrerelease = BigCityLegacyPlugin.IsPrerelease
        };
        return true;
    }

    private static string GetDownloadUrlFallback(LegacyReleaseResponse release, string releaseUrl)
    {
        if (release != null && release.assets != null)
        {
            for (int i = 0; i < release.assets.Length; i++)
            {
                LegacyReleaseAsset asset = release.assets[i];
                if (asset != null && !string.IsNullOrEmpty(asset.name) &&
                    asset.name.StartsWith("BigCityLegacy", StringComparison.OrdinalIgnoreCase) &&
                    asset.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                    !System.Text.RegularExpressions.Regex.IsMatch(asset.name, @"src|source|offline", System.Text.RegularExpressions.RegexOptions.IgnoreCase) &&
                    Uri.IsWellFormedUriString(asset.browser_download_url, UriKind.Absolute) &&
                    asset.browser_download_url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    return asset.browser_download_url;
                }
            }
        }

        return releaseUrl;
    }

    private static bool IsRequestError(UnityWebRequest request)
    {
        return request.isNetworkError || request.isHttpError;
    }

    private static string GetRequestError(UnityWebRequest request)
    {
        string error = request.error;
        long code = request.responseCode;
        if (code > 0)
        {
            return "HTTP " + code + (string.IsNullOrEmpty(error) ? string.Empty : " - " + error);
        }
        return string.IsNullOrEmpty(error) ? "unknown error" : error;
    }
}

[Serializable]
internal sealed class LegacyReleaseResponse
{
    public string tag_name;
    public string html_url;
    public bool draft;
    public bool prerelease;
    public LegacyReleaseAsset[] assets;
}

[Serializable]
internal sealed class LegacyReleaseAsset
{
    public string browser_download_url;
    public string name;
    public string state;
}

internal sealed class LegacyUpdateInfo
{
    public string CurrentVersion;
    public string LatestVersion;
    public string TagName;
    public string ReleaseUrl;
    public string DownloadUrl;
    public bool IsCurrentBuildPrerelease;
}

internal struct LegacySemanticVersion : IComparable<LegacySemanticVersion>
{
    internal int Major;
    internal int Minor;
    internal int Patch;

    internal LegacySemanticVersion(int major, int minor, int patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    internal static bool TryParseStableTag(string text, out LegacySemanticVersion version)
    {
        version = default(LegacySemanticVersion);
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        string value = text.Trim();
        if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(1);
        }

        if (value.IndexOf('-') >= 0 || value.IndexOf('+') >= 0)
        {
            return false;
        }

        string[] parts = value.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        int major;
        int minor;
        int patch;
        if (!int.TryParse(parts[0], out major) || !int.TryParse(parts[1], out minor) || !int.TryParse(parts[2], out patch))
        {
            return false;
        }

        if (major < 0 || minor < 0 || patch < 0)
        {
            return false;
        }

        version = new LegacySemanticVersion(major, minor, patch);
        return true;
    }

    public int CompareTo(LegacySemanticVersion other)
    {
        int value = Major.CompareTo(other.Major);
        if (value != 0)
        {
            return value;
        }
        value = Minor.CompareTo(other.Minor);
        if (value != 0)
        {
            return value;
        }
        return Patch.CompareTo(other.Patch);
    }

    public override string ToString()
    {
        return Major + "." + Minor + "." + Patch;
    }
}
