using System;
using System.Globalization;
using BigCityLegacy.Api;

internal sealed class LegacyAlertCommand
{
    internal string Target, Message, MessageRu, Title = "Server notification", TitleRu = "Уведомление сервера";
    internal LegacyAlertDisplay Display;
    internal float Duration = 6f;
    private static System.Collections.Generic.List<string> Tokens(string text)
    {
        var result = new System.Collections.Generic.List<string>(); var token = new System.Text.StringBuilder(); bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (ch == '\\' && i + 1 < text.Length) { token.Append(ch).Append(text[++i]); continue; }
            if (ch == '"') { quoted = !quoted; continue; }
            if (!quoted && char.IsWhiteSpace(ch)) { if (token.Length > 0) { result.Add(token.ToString()); token.Length = 0; } }
            else token.Append(ch);
        }
        if (quoted) return null;
        if (token.Length > 0) result.Add(token.ToString()); return result;
    }
    internal static bool TryParse(string arguments, out LegacyAlertCommand result)
    {
        result = null; var tokens = Tokens(arguments ?? ""); if (tokens == null || tokens.Count < 3) return false;
        var command = new LegacyAlertCommand { Target = tokens[0] }; int player;
        if (!command.Target.Equals("all", StringComparison.OrdinalIgnoreCase) && (!int.TryParse(command.Target, out player) || player <= 0)) return false;
        var options = new System.Collections.Generic.Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        while (tokens.Count > 2)
        {
            string last = tokens[tokens.Count-1]; int equal = last.IndexOf('=');
            if (equal < 1) break; string key = last.Substring(0,equal);
            if (!key.Equals("title",StringComparison.OrdinalIgnoreCase) && !key.Equals("titleRu",StringComparison.OrdinalIgnoreCase) && !key.Equals("messageRu",StringComparison.OrdinalIgnoreCase)) return false;
            if (options.ContainsKey(key)) return false; options.Add(key,LegacyAlertsConfig.Escapes(last.Substring(equal+1))); tokens.RemoveAt(tokens.Count-1);
        }
        string value;
        if (options.TryGetValue("title",out value)) { command.Title = value; command.TitleRu = null; }
        if (options.TryGetValue("titleRu",out value)) command.TitleRu = value;
        if (options.TryGetValue("messageRu",out value)) command.MessageRu = value;
        float seconds;
        if (tokens.Count > 3 && float.TryParse(tokens[tokens.Count-1],NumberStyles.Float,CultureInfo.InvariantCulture,out seconds))
        { command.Duration = seconds; tokens.RemoveAt(tokens.Count-1); }
        if (tokens.Count < 3) return false;
        string display = tokens[tokens.Count-1].ToLowerInvariant(); tokens.RemoveAt(tokens.Count-1);
        if (display != "small" && display != "large" && display != "hint") return false;
        Enum.TryParse(display,true,out command.Display);
        tokens.RemoveAt(0); command.Message = LegacyAlertsConfig.Escapes(string.Join(" ",tokens.ToArray()));
        try { new LegacyAlertMessage { Title=command.Title,TitleRu=command.TitleRu,Message=command.Message,MessageRu=command.MessageRu,Display=command.Display,Duration=command.Duration }.Validate(); }
        catch (ArgumentException) { return false; }
        if (command.Message.Length == 0) return false; result = command; return true;
    }
    internal static void Execute(string arguments)
    {
        if (!LegacyAdmissionConfig.Enabled) { LegacyServerConsole.WriteAdminLine("[CLI] Server plugin policy/alerts are disabled."); return; }
        LegacyAlertCommand command;
        if (!TryParse(arguments, out command))
        { LegacyServerConsole.WriteAdminLine("[CLI] Usage: alert <PlayerID|all> <message> <small|large|hint> [seconds] [title=\"Text\"] [titleRu=\"Текст\"] [messageRu=\"Текст\"]. Escapes: \\n, \\t."); return; }
        int sent = 0, skipped = 0;
        foreach (var player in LegacyServerAdminService.GetPlayers())
        {
            if (!command.Target.Equals("all", StringComparison.OrdinalIgnoreCase) && command.Target != player.PlayerId.ToString(CultureInfo.InvariantCulture)) continue;
            NetInputControl input;
            if (!LegacyServerAdminService.TryGetInput(player.PlayerId, out input) || !input || !input.netConnect) { skipped++; continue; }
            var values = LegacyPluginNetwork.Context(input.netConnect, player.Nickname); values["playerId"] = player.PlayerId.ToString(CultureInfo.InvariantCulture);
            try
            {
                var alert = new LegacyAlertMessage { Title = LegacyAlertsConfig.Format(command.Title,values), TitleRu = command.TitleRu == null ? null : LegacyAlertsConfig.Format(command.TitleRu,LegacyAlertsConfig.RussianVariables(values)),
                    Message = LegacyAlertsConfig.Format(command.Message, values), MessageRu = command.MessageRu == null ? null : LegacyAlertsConfig.Format(command.MessageRu,LegacyAlertsConfig.RussianVariables(values)), Display = command.Display, Duration = command.Duration };
                if (LegacyPluginNetwork.Send(input.netConnect.conn, alert)) sent++; else skipped++;
            }
            catch (ArgumentException ex) { LegacyServerConsole.WriteAdminLine("[CLI] Alert failed: " + ex.Message); skipped++; }
        }
        LegacyServerConsole.WriteAdminLine("[CLI] Alerts sent=" + sent + ", skipped=" + skipped + " (unsupported/disconnected clients)." + (sent + skipped == 0 ? " No matching player." : ""));
    }
}
