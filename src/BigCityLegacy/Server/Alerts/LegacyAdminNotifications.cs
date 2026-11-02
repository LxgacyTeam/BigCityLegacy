using System;
using BigCityLegacy.Api;

internal static class LegacyAdminNotifications
{
    internal static bool Silent { get; private set; }
    // CLI commands execute synchronously on the Unity thread. Restore the previous
    // state even after validation failures or exceptions; never mute later events.
    internal static IDisposable Begin(bool silent) { return new Scope(silent); }
    private sealed class Scope : IDisposable
    {
        private readonly bool previous = Silent;
        internal Scope(bool silent) { Silent |= silent; }
        public void Dispose() { Silent = previous; }
    }
    internal static void Broadcast(string english, string russian)
    {
        if (Silent) return;
        foreach (var player in LegacyServerAdminService.GetPlayers()) Player(player.PlayerId, english, russian);
    }
    internal static void Player(int playerId, string english, string russian)
    {
        if (Silent || !LegacyAdmissionConfig.Enabled) return;
        NetInputControl input;
        if (!LegacyServerAdminService.TryGetInput(playerId, out input) || !input || !input.netConnect) return;
        LegacyPluginNetwork.Send(input.netConnect.conn, new LegacyAlertMessage {
            Title = "Server notification", TitleRu = "Уведомление сервера",
            Message = english, MessageRu = russian, Display = LegacyAlertDisplay.Hint, Duration = 6f
        });
    }
    internal static void PropertyChanged(int playerId, string prop, bool enabled, long? until)
    {
        string en, ru;
        switch (prop)
        {
            case "noweapon": en = "Weapon restriction"; ru = "Запрет использования оружия"; break;
            case "nomove": en = "Freeze"; ru = "Заморозка"; break;
            case "nocars": en = "Car spawn restriction"; ru = "Запрет спавна автомобилей"; break;
            case "nochat": en = "Chat restriction"; ru = "Запрет использования чата"; break;
            case "invisible": en = "Invisible mode"; ru = "Режим невидимки"; break;
            default: return;
        }
        string duration = until.HasValue ? LegacyAdminTime.Remaining(until) : null;
        Player(playerId, enabled ? "Enabled for you: \"" + en + "\". Duration: " + (duration ?? "permanent") :
                "Removed for you: \"" + en + "\"",
            enabled ? "Для вас активирован \"" + ru + "\". Длительность: " + (duration ?? "постоянно") :
                "Для вас был снят \"" + ru + "\"");
    }
}
