using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;

/// <summary>
/// Server TOML schema: named sections with scalar CLI option keys. Arrays,
/// inline tables and multiline values have no meaning in this schema.
/// Values are merged once, before any server subsystem reads its options.
/// </summary>
internal static class LegacyServerConfig
{
    private static string[] effective;
    internal static string LoadedPath { get; private set; }
    internal static string[] Apply(string[] raw)
    {
        if (effective != null) return effective;
        string explicitPath = raw.FirstOrDefault(a => a.StartsWith("-serverConfig:", StringComparison.OrdinalIgnoreCase));
        bool server = raw.Any(a => a.Equals("-bend_GameServer", StringComparison.OrdinalIgnoreCase));
        if (!server)
        {
            if (explicitPath != null) throw new ArgumentException("Server config requires the -bend_GameServer launch flag.");
            return effective = raw;
        }
        LoadedPath = explicitPath == null
            ? Path.Combine(LegacyHelpers.ModDataPath, "config", "ServerConfig.toml")
            : LegacyCommandLine.StripQuotes(explicitPath.Substring("-serverConfig:".Length));
        if (string.IsNullOrWhiteSpace(LoadedPath)) throw new ArgumentException("Server config path is empty.");
        if (!Path.IsPathRooted(LoadedPath)) LoadedPath = Path.Combine(Paths.GameRootPath, LoadedPath);
        if (!File.Exists(LoadedPath))
        {
            if (explicitPath != null) throw new FileNotFoundException("Server config not found.", LoadedPath);
            Directory.CreateDirectory(Path.GetDirectoryName(LoadedPath));
            File.WriteAllText(LoadedPath, DefaultText, new UTF8Encoding(false));
        }
        try { effective = Merge(raw, Parse(File.ReadAllText(LoadedPath))); }
        catch (Exception ex)
        {
            // Fail closed instead of silently launching a server with defaults.
            Debug.LogError("[BCL ServerConfig] " + LoadedPath + ": " + ex.Message);
            throw;
        }
        Debug.Log("[BCL ServerConfig] Loaded " + LoadedPath + "; explicit launch arguments take priority.");
        return effective;
    }
    internal static Dictionary<string, object> Parse(string text)
    {
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var tables = new HashSet<string>(StringComparer.Ordinal);
        bool performanceSection = false;
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = StripComment(lines[i]).Trim().TrimStart('\uFEFF');
            if (line.Length == 0) continue;
            if (Regex.IsMatch(line, @"^\[[A-Za-z0-9_-]+\]$"))
            {
                performanceSection = line.Equals("[performance]", StringComparison.OrdinalIgnoreCase);
                if (performanceSection) Debug.LogWarning("[BCL ServerConfig] Ignoring obsolete performance section; use launch flags.");
                if (!tables.Add(line)) throw Error(i, "Duplicate table " + line);
                continue;
            }
            if (performanceSection) continue;
            int equal = line.IndexOf('=');
            if (equal < 1) throw Error(i, "Expected key = value.");
            string key = line.Substring(0, equal).Trim();
            if (!Regex.IsMatch(key, @"^[A-Za-z0-9_-]+$")) throw Error(i, "Invalid option key.");
            key = "-" + key.TrimStart('-');
            if (key.Equals("-mlog", StringComparison.OrdinalIgnoreCase))
                throw Error(i, "mlog is a launch-argument-only option.");
            if (key.Equals("-bend_GameServer", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("-noServerObject", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("-noServerCpu", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("-noServerCar", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("-noServerClientCar", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("-noServerNetwork", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("-serverCarSleepDelayMs", StringComparison.OrdinalIgnoreCase) ||
                key.Equals("-serverMaxPendingBuffers", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[BCL ServerConfig] Ignoring command-line-only option " + key);
                continue;
            }
            if (values.ContainsKey(key)) throw Error(i, "Duplicate option " + key + " (option names are unique across sections).");
            string token = line.Substring(equal + 1).Trim();
            object value;
            if (token.StartsWith("\"") || token.StartsWith("'")) value = ParseString(token, i);
            else if (token == "true" || token == "false") value = token == "true";
            else
            {
                string number = token.Replace("_", "");
                long integer;
                double real;
                if (Regex.IsMatch(token, @"^[+-]?(0|[1-9][0-9]*(?:_[0-9]+)*)$") &&
                    long.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer)) value = integer;
                else if (Regex.IsMatch(token, @"^[+-]?[0-9]+(?:\.[0-9]+)?[eE][+-]?[0-9]+$|^[+-]?[0-9]+\.[0-9]+$") &&
                    double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out real) && !double.IsInfinity(real)) value = real;
                else throw Error(i, "Expected a quoted string, boolean or decimal number.");
            }
            if ((key.Equals("-bend_GameServer", StringComparison.OrdinalIgnoreCase) ||
                 key.Equals("-serverDiagnostics", StringComparison.OrdinalIgnoreCase) ||
                 key.Equals("-csDiagnostics", StringComparison.OrdinalIgnoreCase) ||
                 key.StartsWith("-no", StringComparison.OrdinalIgnoreCase)) && !(value is bool))
                throw Error(i, key + " must be a boolean.");
            if (key.Equals("-logsDir", StringComparison.OrdinalIgnoreCase) && !(value is string))
                throw Error(i, "logsDir must be a string.");
            if (key.Equals("-logLevel", StringComparison.OrdinalIgnoreCase))
            {
                if (!(value is string)) throw Error(i, "logLevel must be a string.");
                try { LegacyServerLogWriter.ParseLevel((string)value); }
                catch (ArgumentException ex) { throw Error(i, ex.Message); }
            }
            if (new[] { "-port", "-masterPort", "-connectPort" }.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                if (!(value is long) || (long)value < 1 || (long)value > 65535) throw Error(i, key + " must be an integer in 1..65535.");
            }
            if (new[] { "-maxConn", "-maxCars", "-maxCarsByPlayer", "-maxNikLength", "-serverMaxPendingBuffers" }.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                if (!(value is long) || (long)value < 0 || (key.Equals("-maxConn", StringComparison.OrdinalIgnoreCase) &&
                    ((long)value < 1 || (long)value > 100))) throw Error(i, "Invalid limit " + key);
            }
            if (key.Equals("-csDiagnosticsMaxShotsPerSecond", StringComparison.OrdinalIgnoreCase) &&
                (!(value is long) || (long)value < 1 || (long)value > 1000))
                throw Error(i, "csDiagnosticsMaxShotsPerSecond must be an integer in 1..1000.");
            if (key.Equals("-CarSpawnTimeout", StringComparison.OrdinalIgnoreCase) &&
                (!(value is long) || (long)value < 0 || (long)value > int.MaxValue))
                throw Error(i, "CarSpawnTimeout must be an integer in 0..2147483647 milliseconds.");
            values.Add(key, value);
        }
        return values;
    }
    private static FormatException Error(int line, string message) { return new FormatException("Line " + (line + 1) + ": " + message); }
    private static string StripComment(string text)
    {
        char quote = '\0'; bool escaped = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (quote != '\0')
            {
                if (quote == '"' && !escaped && ch == '\\') { escaped = true; continue; }
                if (!escaped && ch == quote) quote = '\0';
                escaped = false;
            }
            else if (ch == '"' || ch == '\'') quote = ch;
            else if (ch == '#') return text.Substring(0, i);
        }
        return text;
    }
    private static string ParseString(string token, int line)
    {
        char quote = token[0];
        var value = new StringBuilder();
        int i;
        for (i = 1; i < token.Length; i++)
        {
            char ch = token[i];
            if (ch == quote)
            {
                if (token.Substring(i + 1).Trim().Length != 0) throw Error(line, "Unexpected text after string.");
                return value.ToString();
            }
            if (quote == '"' && ch == '\\')
            {
                if (++i >= token.Length) break;
                char escape = token[i];
                switch (escape)
                {
                    case 'b': value.Append('\b'); break;
                    case 't': value.Append('\t'); break;
                    case 'n': value.Append('\n'); break;
                    case 'f': value.Append('\f'); break;
                    case 'r': value.Append('\r'); break;
                    case '"': value.Append('"'); break;
                    case '\\': value.Append('\\'); break;
                    case 'u': case 'U':
                        int length = escape == 'u' ? 4 : 8;
                        if (i + length >= token.Length) throw Error(line, "Incomplete Unicode escape.");
                        uint code;
                        if (!uint.TryParse(token.Substring(i + 1, length), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code) ||
                            code > 0x10ffff || (code >= 0xd800 && code <= 0xdfff)) throw Error(line, "Invalid Unicode escape.");
                        value.Append(char.ConvertFromUtf32((int)code)); i += length; break;
                    default: throw Error(line, "Invalid string escape. Use single quotes for Windows paths.");
                }
            }
            else
            {
                if (char.IsControl(ch) && ch != '\t') throw Error(line, "Control character in string.");
                value.Append(ch);
            }
        }
        throw Error(line, "Unclosed string.");
    }
    internal static string[] Merge(string[] raw, Dictionary<string, object> config)
    {
        var result = new List<string>(raw);
        var explicitKeys = new HashSet<string>(raw.Select(a => a.Split(':')[0]), StringComparer.OrdinalIgnoreCase);
        string[][] aliases =
        {
            new[] { "-bindIP", "-ip" }, new[] { "-masterIP", "-masterHost" },
            new[] { "-master", "-masterServer" }, new[] { "-noUpdCheck", "-noUpdateCheck" },
            new[] { "-noServerCli", "-noServerCLI" }, new[] { "-hideWindow", "-hideBatchWindow" },
            new[] { "-masterDebug", "-debugMaster" }
        };
        foreach (var item in config)
        {
            if (explicitKeys.Contains(item.Key) || aliases.Any(group => group.Contains(item.Key, StringComparer.OrdinalIgnoreCase) &&
                group.Any(explicitKeys.Contains))) continue;
            if (item.Value is bool) { if ((bool)item.Value) result.Add(item.Key); }
            else
            {
                string value = Convert.ToString(item.Value, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(value)) result.Add(item.Key + ":" + value);
            }
        }
        return result.ToArray();
    }
    internal const string DefaultText = "# BigCityLegacy server configuration. CLI arguments override these values. \n# Keys match launch options without the leading '-'.\n\n[server]\nport = 7800\nmaxConn = 16\nMode = \"None\"\nlang = \"en\"\nmaxCars = 100\nmaxCarsByPlayer = 15\nCarSpawnTimeout = 1000\nmaxNikLength = 20\nidleKickTimeout = \"120s\"\ndisconnectedKickTimeout = \"30s\"\nnoChatEvents = false\nnoKillChat = false\nnoCsDamageCooldown = false\nuseVanillaRespawn = false\n\n[mirror]\nmasterServer = true\nmasterPort = 35000\nmasterDebug = false\n\n[files]\n# Relative paths resolve from the game directory. Empty values use defaults.\nserversList = ''\neventsDir = ''\nrespawnPoints = ''\nbanList = ''\n\n[console]\nnoServerConsole = false\nnoServerCli = false\nverboseServerConsole = false\nnoUpdCheck = false\n\n[logging]\n# Empty logsDir uses BigCityLegacy/logs; relative paths resolve from the game directory.\nlogsDir = ''\nnoLogging = false\nlogLevel = \"Log\"\ncsDiagnostics = false\ncsDiagnosticsMaxShotsPerSecond = 60\n";
}
