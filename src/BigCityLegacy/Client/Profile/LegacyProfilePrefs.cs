using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Win32;
using UnityEngine;

internal static class LegacyProfilePrefs
{
    internal static bool Windows => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor;
    internal static bool Mac => Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor;
    internal static string RegistryPath => (Application.isEditor ? @"Software\Unity\UnityEditor\" : @"Software\") + Application.companyName + "\\" + Application.productName;
    internal static string PrefsPath()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (Mac)
        {
            string folder = Path.Combine(home, "Library", "Preferences");
            foreach (string name in new[] { Application.identifier, "unity." + Application.companyName + "." + Application.productName })
            {
                string candidate = Path.Combine(folder, name + ".plist");
                if (File.Exists(candidate)) return candidate;
            }
            throw new FileNotFoundException("Unity preferences plist was not found.");
        }
        string config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrEmpty(config)) config = Path.Combine(home, ".config");
        string path = Path.Combine(config, "unity3d", Application.companyName, Application.productName, "prefs");
        if (!File.Exists(path))
        {
            string fallback = Path.Combine(home, ".config", "unity3d", Application.companyName, Application.productName, "prefs");
            if (File.Exists(fallback)) return fallback;
        }
        return path;
    }
    internal static LegacyProfileData Capture()
    {
        PlayerPrefs.Save();
        var result = new LegacyProfileData { Version = 1, Company = Application.companyName, Product = Application.productName,
            Nickname = PlayerPrefs.GetString("OnlineNikName", "Player"), Values = new List<LegacyProfileValue>() };
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (string raw in ReadNames())
        {
            string key = raw;
            if (Windows)
            {
                string unhashed = Regex.Replace(raw, @"_h[0-9]+$", "");
                if (PlayerPrefs.HasKey(unhashed)) key = unhashed;
            }
            if (!PlayerPrefs.HasKey(key) || !names.Add(key)) continue;
            string missing = "BCL.missing." + Guid.NewGuid().ToString("N");
            string text = PlayerPrefs.GetString(key, missing);
            if (text != missing)
            { result.Values.Add(new LegacyProfileValue { Key = key, Type = "string", Value = text }); continue; }
            float first = PlayerPrefs.GetFloat(key, float.MinValue), second = PlayerPrefs.GetFloat(key, float.MaxValue);
            if (first.Equals(second))
            { result.Values.Add(new LegacyProfileValue { Key = key, Type = "float", Value = first.ToString("R", CultureInfo.InvariantCulture) }); continue; }
            result.Values.Add(new LegacyProfileValue { Key = key, Type = "int", Value = PlayerPrefs.GetInt(key).ToString(CultureInfo.InvariantCulture) });
        }
        result.Values.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
        LegacyProfileCodec.Validate(result);
        return result;
    }
    internal static void Apply(LegacyProfileData profile, bool clearCrashMarker = false)
    {
        LegacyProfileCodec.Validate(profile);
        if (!string.Equals(profile.Company, Application.companyName, StringComparison.Ordinal) ||
            !string.Equals(profile.Product, Application.productName, StringComparison.Ordinal))
            throw new InvalidDataException("The profile belongs to another game.");
        PlayerPrefs.DeleteAll();
        foreach (var entry in profile.Values)
        {
            if (clearCrashMarker && entry.Key == "maybe_bad_settings") continue;
            if (entry.Type == "int") PlayerPrefs.SetInt(entry.Key, int.Parse(entry.Value, CultureInfo.InvariantCulture));
            else if (entry.Type == "float") PlayerPrefs.SetFloat(entry.Key, float.Parse(entry.Value, CultureInfo.InvariantCulture));
            else PlayerPrefs.SetString(entry.Key, entry.Value);
        }
        PlayerPrefs.Save();
    }
    private static IEnumerable<string> ReadNames()
    {
        if (Windows)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RegistryPath))
            { if (key == null) throw new IOException("PlayerPrefs registry key was not found."); return key.GetValueNames(); }
        }
        string path = PrefsPath();
        if (!File.Exists(path)) throw new FileNotFoundException("PlayerPrefs file was not found.", path);
        if (new FileInfo(path).Length > LegacyProfileCodec.MaxBytes) throw new InvalidDataException("Preferences file exceeds 128 MiB.");
        string xml = Mac ? ReadPlist(path) : File.ReadAllText(path);
        var document = new XmlDocument { XmlResolver = null };
        using (var input = new StringReader(xml))
        using (var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = Mac ? DtdProcessing.Ignore : DtdProcessing.Prohibit,
            XmlResolver = null, MaxCharactersInDocument = LegacyProfileCodec.MaxBytes })) document.Load(reader);
        var names = new List<string>();
        foreach (XmlNode value in document.SelectNodes(Mac ? "/plist/dict/key" : "/unity_prefs/pref"))
        {
            string name = Mac ? value.InnerText : ((XmlElement)value).GetAttribute("name");
            if (PlayerPrefs.HasKey(name)) names.Add(name);
            else if (!Mac)
            {
                // Some Unity Linux releases encode key names as Base64 too.
                try { string decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(name)); if (PlayerPrefs.HasKey(decoded)) names.Add(decoded); }
                catch (FormatException) { }
            }
        }
        return names;
    }
    private static string ReadPlist(string path)
    {
        var info = new ProcessStartInfo("/usr/bin/plutil", "-convert xml1 -o - " + LegacyProfileRestart.QuoteArgument(path)) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using (var process = Process.Start(info))
        {
            string output = process.StandardOutput.ReadToEnd(); string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0) throw new IOException("Cannot read preferences: " + error);
            return output;
        }
    }
}
