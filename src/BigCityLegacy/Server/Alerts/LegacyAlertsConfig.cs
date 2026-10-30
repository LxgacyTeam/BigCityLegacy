using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using BigCityLegacy.Api;

internal sealed class LegacyAlertsConfig
{
    internal sealed class Template
    {
        internal bool Enabled = true;
        internal string Title = "Server notification", Message = "", TitleRu, MessageRu;
        internal long UpdatedAt;
        internal LegacyAlertDisplay Display = LegacyAlertDisplay.Small;
        internal float Duration = 6f;
        internal LegacyAlertMessage Render(IDictionary<string, string> variables)
        {
            var result = new LegacyAlertMessage { Title = Clip(Format(Title, variables), 160), Message = Clip(Format(Message, variables), 8000), Display = Display, Duration = Duration, TitleRu = TitleRu == null ? null : Clip(Format(TitleRu, RussianVariables(variables)), 160),
                MessageRu = MessageRu == null ? null : Clip(Format(MessageRu, RussianVariables(variables)), 8000), UpdatedAt = UpdatedAt };
            result.Validate(); return result;
        }
        private static string Clip(string text, int limit) { return text.Length <= limit ? text : text.Substring(0, limit - 3) + "..."; }
    }
    internal readonly Dictionary<string, Template> Templates = new Dictionary<string, Template>(StringComparer.OrdinalIgnoreCase);
    internal static string Format(string text, IDictionary<string, string> variables)
    {
        return Regex.Replace(text ?? "", @"\{([A-Za-z][A-Za-z0-9]*)\}", match => {
            string value; return variables.TryGetValue(match.Groups[1].Value, out value) ? value ?? "" : match.Value;
        });
    }
    internal static Dictionary<string,string> RussianVariables(IDictionary<string,string> original)
    {
        var values = new Dictionary<string,string>(original, StringComparer.OrdinalIgnoreCase);
        var replacements = new Dictionary<string,string> {
            {"Not specified", "Не указана"}, {"perm", "постоянно"}, {"permanent", "постоянно"},
            {"not reported", "не передана"}, {"not installed", "не установлен"}, {"installed", "установлен"},
            {"Required plugin is missing.", "Не установлен обязательный плагин."},
            {"Required plugin version does not match.", "Версия обязательного плагина не соответствует требованиям."},
            {"Installed plugin/version is prohibited.", "Установленный плагин или его версия запрещены."},
            {"Client BigCityLegacy version does not match the server policy.", "Версия BigCityLegacy не соответствует требованиям сервера."}
        };
        foreach (var key in new List<string>(values.Keys)) { string translated; if (values[key] != null && replacements.TryGetValue(values[key], out translated)) values[key] = translated; }
        return values;
    }
    internal static string Escapes(string text)
    {
        var result = new StringBuilder();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '\\' || i + 1 == text.Length) { result.Append(text[i]); continue; }
            char next = text[++i];
            switch (next) { case 'n': result.Append('\n'); break; case 't': result.Append('\t'); break; case 'r': result.Append('\r'); break;
                case '\\': result.Append('\\'); break; case '"': result.Append('"'); break; default: result.Append('\\').Append(next); break; }
        }
        return result.ToString();
    }
    internal static LegacyAlertsConfig Parse(string text)
    {
        var config = new LegacyAlertsConfig(); string section = null;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string[] lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = Comment(lines[i]).Trim().TrimStart('\uFEFF');
            if (line.Length == 0) continue;
            if (line.StartsWith("[") && line.EndsWith("]"))
            {
                section = line.Substring(1, line.Length - 2);
                if (section != "plugins" && section != "missingManifest" && section != "missingBclVersion" && section != "invalidManifest" && section != "banRejected" && section != "banned" && section != "kicked" && section != "welcome") throw new FormatException("Unknown Alerts.toml section: " + section);
                if (config.Templates.ContainsKey(section)) throw new FormatException("Duplicate alert section: " + section);
                config.Templates.Add(section, new Template()); continue;
            }
            int equal = line.IndexOf('=');
            if (section == null || equal < 1) throw new FormatException("Alerts.toml expects [section] and key = value (line " + (i + 1) + ").");
            string key = line.Substring(0, equal).Trim(), token = line.Substring(equal + 1).Trim();
            if (!keys.Add(section + "." + key)) throw new FormatException("Duplicate alert key: " + key);
            var template = config.Templates[section];
            if (key == "updatedAt")
            {
                if (section != "welcome" || !long.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out template.UpdatedAt) || template.UpdatedAt > 253402300799L) throw new FormatException("welcome.updatedAt must be a non-negative Unix timestamp.");
                continue;
            }
            if (key == "enabled")
            { if (token != "true" && token != "false") throw new FormatException("enabled must be true/false."); template.Enabled = token == "true"; continue; }
            if (key == "duration")
            {
                if (!float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out template.Duration) ||
                    float.IsNaN(template.Duration) || template.Duration < .1f || template.Duration > 3600f) throw new FormatException("Invalid alert duration."); continue;
            }
            string value;
            if (token.StartsWith("\"\"\"", StringComparison.Ordinal))
            {
                string content = token.Substring(3); bool first = content.Length == 0; var body = new StringBuilder();
                while (true)
                {
                    int end = content.IndexOf("\"\"\"", StringComparison.Ordinal);
                    if (end >= 0)
                    { body.Append(content.Substring(0, end)); if (Comment(content.Substring(end + 3)).Trim().Length != 0) throw new FormatException("Text after multiline alert."); break; }
                    body.Append(content); if (!first) body.Append('\n'); first = false;
                    if (++i == lines.Length) throw new FormatException("Unclosed multiline alert string."); content = lines[i];
                }
                value = Escapes(body.ToString());
            }
            else
            {
                if (token.Length < 2 || (token[0] != '"' && token[0] != '\'') || token[token.Length - 1] != token[0]) throw new FormatException("Alert text must be quoted.");
                if (token[0] == '"') value = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(token);
                else { if (token.IndexOf('\'', 1) != token.Length - 1) throw new FormatException("Unexpected quote in literal alert string."); value = token.Substring(1, token.Length - 2); }
            }
            switch (key)
            {
                case "title": template.Title = value; break;
                case "titleRu": template.TitleRu = value; break;
                case "messageRu": template.MessageRu = value; break;
                case "message": template.Message = value; break;
                case "display": LegacyAlertDisplay display; if (!Enum.TryParse(value, true, out display) || !Enum.IsDefined(typeof(LegacyAlertDisplay), display)) throw new FormatException("display must be small, large or hint."); template.Display = display; break;
                default: throw new FormatException("Unknown alert key: " + key);
            }
        }
        // Partial files inherit the remaining templates, but malformed values never replace live config.
        foreach (var pair in Defaults().Templates) if (!config.Templates.ContainsKey(pair.Key)) config.Templates.Add(pair.Key, pair.Value);
        foreach (var template in config.Templates.Values)
        {
            if (template.Title.Length > 160 || template.Message.Length > 8000 || (template.TitleRu ?? "").Length > 160 || (template.MessageRu ?? "").Length > 8000) throw new FormatException("Alert template exceeds title/body limit.");
            template.Render(new Dictionary<string, string>());
        }
        return config;
    }
    private static string Comment(string line)
    {
        char quote = '\0'; bool escape = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (quote != '\0') { if (quote == '"' && !escape && ch == '\\') { escape = true; continue; } if (!escape && ch == quote) quote = '\0'; escape = false; }
            else if (ch == '"' || ch == '\'') quote = ch; else if (ch == '#') return line.Substring(0, i);
        }
        return line;
    }
    internal static LegacyAlertsConfig Defaults()
    {
        var config = new LegacyAlertsConfig();
        config.Templates.Add("plugins", new Template { Title = "Plugin compatibility", Message = "{reason}\nPlugin: {pluginName} ({pluginGuid})\nInstalled: {pluginVersion}\nRule: {rule} {requiredVersion}" });
        config.Templates.Add("missingManifest", new Template { Title = "Client update required", Message = "{nickname}, this server requires a plugin list. Use BigCityLegacy 1.4.2 or newer." });
        config.Templates.Add("missingBclVersion", new Template { Title = "Client update required", Message = "{nickname}, this server requires the client BigCityLegacy version. Use BCL 1.4.2 or newer." });
        config.Templates.Add("invalidManifest", new Template { Title = "Connection rejected", Message = "The client plugin list is invalid: {reason}" });
        config.Templates.Add("banned", new Template { Title = "Banned from server", Message = "{nickname}, you are banned.\nDuration: {duration}\nBan ID: {banId}\nReason: {reason}" });
        config.Templates.Add("banRejected", new Template { Title = "Connection rejected", Message = "{nickname}, you cannot join while banned.\nDuration: {duration}\nBan ID: {banId}\nReason: {reason}" });
        config.Templates.Add("kicked", new Template { Title = "Kicked from server", Message = "{nickname}, you have been kicked.\nReason: {reason}" });
        config.Templates.Add("welcome", new Template { Enabled = false, Display = LegacyAlertDisplay.Large, Title = "Welcome", Message = "Welcome, {nickname}!\nPlease follow the server rules." });
        config.Templates["plugins"].TitleRu = "Совместимость плагинов";
        config.Templates["plugins"].MessageRu = "{reason}\nПлагин: {pluginName} ({pluginGuid})\nУстановлено: {pluginVersion}\nТребование: {rule} {requiredVersion}";
        config.Templates["missingManifest"].TitleRu = "Требуется обновление клиента";
        config.Templates["missingManifest"].MessageRu = "{nickname}, сервер требует список плагинов. Используйте BigCityLegacy 1.4.2 или новее.";
        config.Templates["missingBclVersion"].TitleRu = "Требуется обновление клиента";
        config.Templates["missingBclVersion"].MessageRu = "{nickname}, сервер требует версию BigCityLegacy. Используйте BCL 1.4.2 или новее.";
        config.Templates["invalidManifest"].TitleRu = "Подключение отклонено";
        config.Templates["invalidManifest"].MessageRu = "Некорректный список плагинов: {reason}";
        config.Templates["banRejected"].TitleRu = "Подключение отклонено";
        config.Templates["banRejected"].MessageRu = "{nickname}, вход заблокирован баном.\nСрок: {duration}\nID бана: {banId}\nПричина: {reason}";
        config.Templates["banned"].TitleRu = "Вы забанены";
        config.Templates["banned"].MessageRu = "{nickname}, вам выдан бан.\nСрок: {duration}\nID бана: {banId}\nПричина: {reason}";
        config.Templates["kicked"].TitleRu = "Отключение администратором";
        config.Templates["kicked"].MessageRu = "{nickname}, вы исключены с сервера.\nПричина: {reason}";
        config.Templates["welcome"].TitleRu = "Добро пожаловать";
        config.Templates["welcome"].MessageRu = "Добро пожаловать, {nickname}!\nСоблюдайте правила сервера.";
        return config;
    }
    internal static readonly string DefaultText = BuildDefaultText();
    private static string BuildDefaultText()
    {
        var builder = new StringBuilder("# BCL alerts. Optional titleRu/messageRu provide Russian text; otherwise English is used.\n# welcome.updatedAt is a Unix timestamp; update it when changing the rules.\n# Variables: {nickname}, {playerId}, {bclVersion}, {reason}, {duration}, {until}, {banId}, {pluginGuid}, {pluginName}, {pluginVersion}, {requiredVersion}, {rule}.\n");
        foreach (var pair in Defaults().Templates)
        {
            var value = pair.Value; builder.Append("\n[").Append(pair.Key).Append("]\n");
            builder.Append("enabled = ").Append(value.Enabled ? "true" : "false").Append('\n');
            builder.Append("title = ").Append(Newtonsoft.Json.JsonConvert.SerializeObject(value.Title)).Append('\n');
            builder.Append("titleRu = ").Append(Newtonsoft.Json.JsonConvert.SerializeObject(value.TitleRu)).Append('\n');
            builder.Append("message = ").Append(Newtonsoft.Json.JsonConvert.SerializeObject(value.Message)).Append('\n');
            builder.Append("messageRu = ").Append(Newtonsoft.Json.JsonConvert.SerializeObject(value.MessageRu)).Append('\n');
            builder.Append("display = \"").Append(value.Display.ToString().ToLowerInvariant()).Append("\"\n");
            builder.Append("duration = ").Append(value.Duration.ToString(CultureInfo.InvariantCulture)).Append('\n');
            if (pair.Key == "welcome") builder.Append("updatedAt = 0\n");
        }
        return builder.ToString();
    }
}
