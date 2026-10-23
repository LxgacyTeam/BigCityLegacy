using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx;
using Microsoft.Win32;
using Newtonsoft.Json;
using UnityEngine;

internal static class LegacyProfileRestart
{
    private sealed class NativeValue { public string Name, Kind, Data; public string[] Strings; }
    private sealed class RestoreTask
    {
        public string RegistryPath, Executable, Arguments, WorkingDirectory, OldProcessStart;
        public int OldPid;
        public List<NativeValue> Values;
    }
    internal static IEnumerator Import(LegacyProfileData profile, string backupPassword, Action<string> fail)
    {
        LegacyProfileData backup = null;
        string directory = null;
        Process helper = null;
        Exception error = null;
        try
        {
            if (Application.isEditor) throw new InvalidOperationException("Restart import is available in the standalone game.");
            backup = LegacyProfilePrefs.Capture();
            string backupPath = Path.Combine(LegacyHelpers.ModDataPath, "ProfileBackup_" + DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss-fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".profile");
            LegacyProfileFiles.Write(backupPath, LegacyProfileCodec.Encode(backup, backupPassword));
            LegacyProfilePrefs.Apply(profile, clearCrashMarker: true);
            directory = Path.Combine(Path.GetTempPath(), "BigCityLegacy.Profile." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            if (!LegacyProfilePrefs.Windows)
            {
                using (var chmod = Process.Start(new ProcessStartInfo("/bin/chmod", "700 " + QuoteArgument(directory)) { UseShellExecute = false }))
                { chmod.WaitForExit(); if (chmod.ExitCode != 0) throw new IOException("Cannot protect the profile restore directory."); }
            }
            using (var current = Process.GetCurrentProcess())
            {
                string executable = Paths.ExecutablePath;
                if (string.IsNullOrEmpty(executable)) executable = current.MainModule.FileName;
                string[] args = Environment.GetCommandLineArgs().Skip(1).ToArray();
                var task = new RestoreTask { Executable = executable, Arguments = string.Join(" ", args.Select(QuoteArgument).ToArray()),
                    WorkingDirectory = Paths.GameRootPath, OldPid = current.Id };
                ProcessStartInfo start;
                if (LegacyProfilePrefs.Windows)
                {
                    task.OldProcessStart = current.StartTime.ToUniversalTime().Ticks.ToString(CultureInfo.InvariantCulture);
                    task.RegistryPath = LegacyProfilePrefs.RegistryPath;
                    task.Values = CaptureRegistry();
                    string taskFile = Path.Combine(directory, "task.json"), script = Path.Combine(directory, "restore.ps1");
                    File.WriteAllText(taskFile, JsonConvert.SerializeObject(task), new UTF8Encoding(false));
                    using (var input = Assembly.GetExecutingAssembly().GetManifestResourceStream("BigCityLegacy.ProfileRestore.ps1"))
                    using (var output = File.Create(script))
                    { if (input == null) throw new FileNotFoundException("Profile restore helper is missing."); input.CopyTo(output); }
                    string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
                    start = new ProcessStartInfo(powershell, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File " + QuoteArgument(script) + " -TaskPath " + QuoteArgument(taskFile));
                }
                else
                {
                    string prefs = LegacyProfilePrefs.PrefsPath(), snapshot = Path.Combine(directory, "prefs.snapshot");
                    File.Copy(prefs, snapshot);
                    string script = Path.Combine(directory, "restore.sh");
                    File.WriteAllText(script, UnixScript(task.OldPid, directory, prefs, executable, args, LegacyProfilePrefs.Mac), new UTF8Encoding(false));
                    start = new ProcessStartInfo("/bin/sh", QuoteArgument(script));
                }
                start.UseShellExecute = false; start.CreateNoWindow = true; start.WindowStyle = ProcessWindowStyle.Hidden;
                PrepareRestartEnvironment(start);
                helper = Process.Start(start);
                if (helper == null) throw new IOException("Cannot start the profile restore helper.");
            }
        }
        catch (Exception ex) { error = ex; }
        if (error == null)
        {
            float until = Time.realtimeSinceStartup + 20f;
            while (!File.Exists(Path.Combine(directory, "ready")) && !helper.HasExited && Time.realtimeSinceStartup < until) yield return null;
            if (File.Exists(Path.Combine(directory, "ready")))
            {
                PlayerPrefs.Save(); Application.Quit();
                // Another plugin may cancel quitting. Do not leave a restore task
                // waiting to overwrite a later, unrelated game session.
                until = Time.realtimeSinceStartup + 5f;
                while (Time.realtimeSinceStartup < until) yield return null;
                error = new IOException("The game did not quit. Profile import was cancelled.");
            }
            else error = new IOException("The profile restore helper did not become ready. " +
                (File.Exists(Path.Combine(directory, "error")) ? File.ReadAllText(Path.Combine(directory, "error")) : ""));
        }
        if (helper != null)
        {
            try { if (!helper.HasExited) helper.Kill(); } catch (Exception ex) { UnityEngine.Debug.LogWarning("[BCL profile] " + ex.Message); }
            helper.Dispose();
        }
        if (backup != null)
            try { LegacyProfilePrefs.Apply(backup); } catch (Exception ex) { UnityEngine.Debug.LogError("[BCL profile] Rollback failed: " + ex); }
        if (directory != null) Clean(directory);
        fail(error.Message);
    }
    internal static void PrepareRestartEnvironment(ProcessStartInfo start)
    {
        // Doorstop's re-entry guards belong to the old process. Inheriting them
        // makes a newly launched Unity process skip BepInEx initialization.
        // Keep loader configuration and Unix preload paths for the new game.
        start.EnvironmentVariables.Remove("DOORSTOP_INITIALIZED");
        start.EnvironmentVariables.Remove("DOORSTOP_DISABLE");
    }
    private static List<NativeValue> CaptureRegistry()
    {
        var result = new List<NativeValue>();
        using (var key = Registry.CurrentUser.OpenSubKey(LegacyProfilePrefs.RegistryPath))
        {
            if (key == null) throw new IOException("PlayerPrefs registry key is unavailable.");
            foreach (string name in key.GetValueNames())
            {
                RegistryValueKind kind = key.GetValueKind(name);
                object data = key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                var value = new NativeValue { Name = name, Kind = kind.ToString() };
                if (data is byte[] bytes) value.Data = Convert.ToBase64String(bytes);
                else if (data is string[] array) value.Strings = array;
                else value.Data = Convert.ToString(data, CultureInfo.InvariantCulture);
                result.Add(value);
            }
        }
        return result;
    }
    internal static string QuoteArgument(string value)
    {
        var result = new StringBuilder("\""); int slash = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slash++; continue; }
            result.Append('\\', c == '"' ? slash * 2 + 1 : slash); result.Append(c); slash = 0;
        }
        return result.Append('\\', slash * 2).Append('"').ToString();
    }
    internal static string ShellQuote(string value) { return "'" + value.Replace("'", "'\"'\"'") + "'"; }
    internal static string UnixScript(int pid, string directory, string prefs, string executable, string[] args, bool mac)
    {
        string snapshot = Path.Combine(directory, "prefs.snapshot"), staging = prefs + ".bcl-import-" + Path.GetFileName(directory);
        var script = new StringBuilder("#!/bin/sh\nset -eu\n");
        script.Append("trap ").Append(ShellQuote("printf profile-restore-failed > " + ShellQuote(Path.Combine(directory, "error")))).Append(" 0\n");
        script.Append("printf ready > ").Append(ShellQuote(Path.Combine(directory, "ready"))).Append("\n");
        script.Append("while kill -0 ").Append(pid).Append(" 2>/dev/null; do sleep 0.2; done\n");
        if (mac)
            script.Append("/usr/bin/defaults import ").Append(ShellQuote(Path.GetFileNameWithoutExtension(prefs))).Append(' ').Append(ShellQuote(snapshot)).Append('\n');
        else
        {
            script.Append("cp -- ").Append(ShellQuote(snapshot)).Append(' ').Append(ShellQuote(staging)).Append('\n');
            script.Append("chmod 600 ").Append(ShellQuote(staging)).Append('\n');
            script.Append("mv -f -- ").Append(ShellQuote(staging)).Append(' ').Append(ShellQuote(prefs)).Append('\n');
        }
        script.Append("cd ").Append(ShellQuote(Paths.GameRootPath)).Append('\n');
        foreach (string file in new[] { "ready", "prefs.snapshot", "restore.sh" }) script.Append("rm -f -- ").Append(ShellQuote(Path.Combine(directory, file))).Append('\n');
        script.Append("rmdir -- ").Append(ShellQuote(directory)).Append("\ntrap - 0\nunset DOORSTOP_INITIALIZED DOORSTOP_DISABLE\nexec ").Append(ShellQuote(executable));
        foreach (string argument in args) script.Append(' ').Append(ShellQuote(argument));
        return script.Append('\n').ToString();
    }
    private static void Clean(string directory)
    {
        foreach (string name in new[] { "ready", "task.json", "restore.ps1", "prefs.snapshot", "restore.sh", "error" })
            try { File.Delete(Path.Combine(directory, name)); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        try { Directory.Delete(directory); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }
}
