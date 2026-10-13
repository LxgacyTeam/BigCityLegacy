using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using BepInEx;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

internal static class LegacyInstallerUpdater
{
    internal static bool Busy { get; private set; }
    internal static string Status { get; private set; }
    internal static float Progress { get; private set; }
    internal static void DownloadUpdate(MonoBehaviour owner, LegacyUpdateInfo update)
    {
        if (update == null) return;
        if (Application.platform == RuntimePlatform.WindowsPlayer) DownloadAndRun(owner);
        else if (!string.IsNullOrEmpty(update.DownloadUrl)) Application.OpenURL(update.DownloadUrl);
    }
    internal static void DownloadAndRun(MonoBehaviour owner)
    {
        if (Busy || !owner) return;
        if (Application.platform != RuntimePlatform.WindowsPlayer)
        {
            Status = LegacyLocalizer.Text("The installer is available for Windows only.", "Установщик доступен только для Windows.");
            return;
        }
        owner.StartCoroutine(RunSafely());
    }
    internal static string FindInstaller(LegacyReleaseResponse release)
    {
        if (release == null || release.draft || release.prerelease || release.assets == null) return null;
        foreach (string name in new[] { "BigCityLegacy_Installer.exe", "BigCityLegacyInstaller.exe" })
            foreach (var asset in release.assets)
            {
                Uri uri;
                if (asset != null && string.Equals(asset.name, name, StringComparison.OrdinalIgnoreCase) &&
                    Uri.TryCreate(asset.browser_download_url, UriKind.Absolute, out uri) && uri.Scheme == "https")
                    return uri.AbsoluteUri;
            }
        return null;
    }
    internal static string Arguments(string gameDir, LegacyRepositorySource source)
    {
        if (string.IsNullOrEmpty(gameDir) || gameDir.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0)
            throw new ArgumentException("Invalid game directory.");
        return "/GAME=\"" + Path.GetFullPath(gameDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
            "\" /SILENT /SKIPEULA /RUNGAME /DISPOSABLE /SOURCE=" + source;
    }
    private static IEnumerator RunSafely()
    {
        var routine = Run();
        try
        {
            while (true)
            {
                bool next;
                try { next = routine.MoveNext(); }
                catch (Exception ex) { Fail(ex.Message); yield break; }
                if (!next) yield break;
                yield return routine.Current;
            }
        }
        finally { (routine as IDisposable)?.Dispose(); }
    }
    private static IEnumerator Run()
    {
        Busy = true; Progress = 0;
        string temporaryDirectory = Path.Combine(Path.GetTempPath(), "BigCityLegacy-update-" + Guid.NewGuid().ToString("N"));
        string executable = Path.Combine(temporaryDirectory, "BigCityLegacy_Installer.exe");
        bool launched = false;
        try
        {
            Status = LegacyLocalizer.Text("Downloading installer…", "Скачивание установщика…");
            var source = LegacyRepository.Current;
            string url;
            using (var api = UnityWebRequest.Get(LegacyRepository.LatestReleaseUrl(LegacyRepository.InstallerRepo, source)))
            {
                LegacyRepository.ConfigureRequest(api, 20);
                yield return api.SendWebRequest();
                if (api.isNetworkError || api.isHttpError) { Fail(api.error); yield break; }
                if (source != LegacyRepository.Current) { SourceChanged(); yield break; }
                LegacyReleaseResponse release; string parseError;
                if (!LegacyUpdateChecker.TryParseRelease(api.downloadHandler.text, out release, out parseError))
                { Fail(parseError); yield break; }
                url = FindInstaller(release);
                if (url == null) { Fail("No online installer EXE found in the latest " + source + " release."); yield break; }
            }
            try { Directory.CreateDirectory(temporaryDirectory); }
            catch (Exception ex) { Fail(ex.Message); yield break; }
            using (var download = UnityWebRequest.Get(url))
            {
                download.downloadHandler = new DownloadHandlerFile(executable);
                LegacyRepository.ConfigureRequest(download, 180);
                var operation = download.SendWebRequest();
                while (!operation.isDone) { Progress = download.downloadProgress; yield return null; }
                if (download.isNetworkError || download.isHttpError) { Fail(download.error); yield break; }
            }
            if (source != LegacyRepository.Current) { SourceChanged(); yield break; }
            try
            {
                using (var file = File.OpenRead(executable))
                    if (file.Length < 1024 || file.ReadByte() != 'M' || file.ReadByte() != 'Z')
                        throw new InvalidDataException("Downloaded file is not a Windows executable.");
                // Setup performs game shutdown, backup, installation, restart and
                // self-deletion. Leave this process running if launch/UAC fails.
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = executable, Arguments = Arguments(Paths.GameRootPath, source),
                    WorkingDirectory = Paths.GameRootPath, UseShellExecute = true
                });
                if (process == null) throw new InvalidOperationException("Installer could not be started.");
                process.Dispose();
                launched = true; Progress = 1;
                Status = LegacyLocalizer.Text("Installer started.", "Установщик запущен.");
                Application.Quit();
            }
            catch (Exception ex) { Fail(ex.Message); }
        }
        finally
        {
            Busy = false;
            if (!launched)
            {
                try { if (Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, true); }
                catch (Exception ex) { Debug.LogWarning("[BCL Updater] Temporary cleanup: " + ex.Message); }
            }
        }
    }
    private static void Fail(string message)
    {
        Status = LegacyLocalizer.Text("Update failed: ", "Ошибка обновления: ") + message;
        Debug.LogWarning("[BCL Updater] " + Status);
    }
    private static void SourceChanged()
    {
        Fail(LegacyLocalizer.Text("Repository source changed. Please retry.", "Источник данных изменён. Повторите загрузку."));
    }
}
