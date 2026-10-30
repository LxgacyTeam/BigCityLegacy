using System;
using System.Globalization;

internal static class LegacyServerConfigCommands
{
    internal static void ShowConfig()
    {
        LegacyServerConsole.WriteAdminLine("[CLI] ServerConfig: " + LegacyServerConfig.LoadedPath + " (startup values with launch-argument overrides)");
        foreach (var pair in LegacyServerConfig.CurrentSettings())
        {
            string value = pair.Value is bool ? ((bool)pair.Value ? "true" : "false") :
                pair.Value is string ? Newtonsoft.Json.JsonConvert.SerializeObject(pair.Value) : Convert.ToString(pair.Value, CultureInfo.InvariantCulture);
            LegacyServerConsole.WriteAdminLine("[CLI]   " + pair.Key.TrimStart('-') + " = " + value);
        }
    }
    internal static void BanList()
    {
        var bans = LegacyBanList.ActiveBans();
        LegacyServerConsole.WriteAdminLine("[CLI] Active bans: " + bans.Count);
        foreach (var ban in bans)
            LegacyServerConsole.WriteAdminLine("[CLI] BanID=" + ban.BanID + " type=" + ban.Type + " nickname=" + LegacyChatLog.Escape(ban.Nickname) +
                " GUID=" + ban.GUID + " IP=" + ban.IP + " remaining=" + LegacyAdminTime.Remaining(ban.Until) +
                " until=" + (ban.Until.HasValue ? ban.Until.Value.ToString(CultureInfo.InvariantCulture) : "permanent") + " reason=" + LegacyChatLog.Escape(ban.Reason));
    }
    internal static void ReloadMirror()
    {
        string error;
        LegacyServerConsole.WriteAdminLine(LegacyMasterServer.ReloadList(out error) ? "[CLI] Mirror server list reloaded; new entries are active." : "[CLI] reloadmirror failed: " + error);
    }
}
