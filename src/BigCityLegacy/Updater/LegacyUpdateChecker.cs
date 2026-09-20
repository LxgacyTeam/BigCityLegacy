using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json.Linq;

/// <summary>
/// GitHub release checker for both client and dedicated-server modes.
/// </summary>
internal static class LegacyUpdateChecker
{
    internal const string EnabledPlayerPrefsKey = "BigCityLegacy.Updater.Enabled";

    private const float StartupDelaySeconds = 2.0f;
    private const int RequestTimeoutSeconds = 10;

    private static bool started;
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

    internal static string GitHubOwner = BigCityLegacyPlugin.GitHubOwner;
    internal static string GitHubRepo = BigCityLegacyPlugin.GitHubRepo;

    internal static void StartIfNeeded(MonoBehaviour owner)
    {
        if (started || owner == null)
        {
            return;
        }

        started = true;
        owner.StartCoroutine(CheckOnStartup());
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

        string owner = (GitHubOwner ?? string.Empty).Trim();
        string repo = (GitHubRepo ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(owner) || string.IsNullOrEmpty(repo))
        {
            LogWarning("[BCL Updater] GitHub updater skipped: repository owner/repo is not configured.");
            requestInProgress = false;
            yield break;
        }

        string url = "https://api.github.com/repos/" + owner + "/" + repo + "/releases/latest";
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = RequestTimeoutSeconds;
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            request.SetRequestHeader("User-Agent", BigCityLegacyPlugin.PluginName + "/" + VersionInfo.ModVersionName);

            yield return request.SendWebRequest();

            if (IsRequestError(request))
            {
                LogWarning("[BCL Updater] GitHub update check failed: " + GetRequestError(request));
                requestInProgress = false;
                yield break;
            }

            string json = request.downloadHandler != null ? request.downloadHandler.text : null;
            LegacyGitHubReleaseResponse release;
            string parseError;
            if (!TryParseGitHubRelease(json, out release, out parseError))
            {
                LogWarning("[BCL Updater] GitHub update check JSON parse failed: " + parseError);
                requestInProgress = false;
                yield break;
            }

            if (release == null || string.IsNullOrEmpty(release.tag_name))
            {
                LogWarning("[BCL Updater] GitHub update check failed: latest release response has no tag_name.");
                requestInProgress = false;
                yield break;
            }

            LegacyUpdateInfo update;
            if (TryCreateUpdateInfo(owner, repo, release, out update))
            {
                latestAvailableUpdate = update;
                ShowUpdateNotification(update);
            }
            else if (showNoUpdateLog)
            {
                LogInfo("[BCL Updater] No stable BigCityLegacy update found. Current=" + VersionInfo.ModVersionName + ", latest tag=" + release.tag_name);
            }
        }

        requestInProgress = false;
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

    private static bool TryParseGitHubRelease(string json, out LegacyGitHubReleaseResponse release, out string error)
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

            release = new LegacyGitHubReleaseResponse
            {
                tag_name = ReadString(root, "tag_name"),
                html_url = ReadString(root, "html_url"),
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

    private static LegacyGitHubReleaseAsset[] ReadAssets(JToken assetsToken)
    {
        JArray assetsArray = assetsToken as JArray;
        if (assetsArray == null || assetsArray.Count == 0)
        {
            return new LegacyGitHubReleaseAsset[0];
        }

        List<LegacyGitHubReleaseAsset> assets = new List<LegacyGitHubReleaseAsset>();
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

            assets.Add(new LegacyGitHubReleaseAsset
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

    private static bool TryCreateUpdateInfo(string owner, string repo, LegacyGitHubReleaseResponse release, out LegacyUpdateInfo update)
    {
        update = null;

        LegacySemanticVersion remoteVersion;
        if (!LegacySemanticVersion.TryParseStableTag(release.tag_name, out remoteVersion))
        {
            LogInfo("[BCL Updater] GitHub latest release tag is not a stable X.Y.Z tag, skipped: " + release.tag_name);
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
            : "https://github.com/" + owner + "/" + repo + "/releases/tag/" + release.tag_name;

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

    private static string GetDownloadUrlFallback(LegacyGitHubReleaseResponse release, string releaseUrl)
    {
        if (release != null && release.assets != null)
        {
            for (int i = 0; i < release.assets.Length; i++)
            {
                LegacyGitHubReleaseAsset asset = release.assets[i];
                if (asset != null && !string.IsNullOrEmpty(asset.browser_download_url))
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
internal sealed class LegacyGitHubReleaseResponse
{
    public string tag_name;
    public string html_url;
    public LegacyGitHubReleaseAsset[] assets;
}

[Serializable]
internal sealed class LegacyGitHubReleaseAsset
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
