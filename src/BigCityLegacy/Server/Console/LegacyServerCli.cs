using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Commands are parsed and executed on Unity's main thread by LegacyServerConsole.
/// Never call Unity/UNet APIs from the console reader thread.
/// </summary>
internal static class LegacyServerCli
{
    internal static void Execute(string rawCommand)
    {
        if (!NetManager.isServer)
        {
            LegacyServerConsole.WriteAdminLine("[CLI] The server is not running yet.");
            return;
        }

        if (string.IsNullOrWhiteSpace(rawCommand))
        {
            return;
        }

        string command;
        string arguments;
        SplitCommand(rawCommand, out command, out arguments);
        command = command.ToLowerInvariant();

        switch (command)
        {
            case "help":
            case "?":
                PrintHelp(arguments);
                return;

            case "players":
                PrintPlayers();
                return;

            case "playerinfo":
                PrintPlayerInfo(arguments);
                return;

            case "kick":
                Kick(arguments);
                return;

            case "kill":
                Kill(arguments);
                return;

            case "ban":
                Ban(arguments);
                return;

            case "setprop":
                SetProp(arguments);
                return;

            case "tp":
                Teleport(arguments);
                return;

            case "tpto":
                TeleportTo(arguments);
                return;

            case "clearchat":
                string clearError;
                LegacyServerConsole.WriteAdminLine(LegacyServerAdminService.ClearChat(out clearError) ?
                    "[CLI] Chat cleared." : "[CLI] Chat cleanup failed: " + clearError);
                return;

            case "unban":
                Unban(arguments);
                return;

            case "showcfg":
                LegacyServerConfigCommands.ShowConfig(); return;
            case "banlist":
                LegacyServerConfigCommands.BanList(); return;
            case "reloadmirror":
                LegacyServerConfigCommands.ReloadMirror(); return;

            case "reloadcfg":
                ReloadConfigs();
                return;

            case "alert":
                LegacyAlertCommand.Execute(arguments);
                return;

            case "msg":
                SendMessage(arguments);
                return;

            case "cleardrop":
                ClearDrop();
                return;

            case "delcars":
                DeleteCars(arguments);
                return;

            case "stats":
                LegacyPerformanceMonitor.WriteServerStats();
                return;

            case "stop":
                LegacyServerShutdown.Request("CLI command");
                return;

            default:
                LegacyServerConsole.WriteAdminLine("[CLI] Unknown command '" + command + "'. Type 'help' for the command list.");
                return;
        }
    }

    private static void PrintHelp(string topic)
    {
        if (!string.IsNullOrWhiteSpace(topic))
        {
            switch (topic.Trim().ToLowerInvariant())
            {
                case "alert":
                    LegacyServerConsole.WriteAdminLine("[CLI] alert <PlayerID|all> <message> <small|large|hint> [seconds] [title=\"Text\"] [titleRu=\"Текст\"] [messageRu=\"Текст\"]");
                    LegacyServerConsole.WriteAdminLine("[CLI] Examples: alert all \"Welcome, {nickname}!\\nRead the rules.\" large | alert 15 Hello hint 10");
                    return;
                case "setprop":
                    LegacyServerConsole.WriteAdminLine("[CLI] setprop <PlayerID> <noweapon|nomove|nocars|nochat|invisible> <0|1> [time]");
                    LegacyServerConsole.WriteAdminLine("[CLI] 1 enables the restriction; 0 removes it. Optional time: 30s, 15m, 12h, 5d; omitted = permanent.");
                    LegacyServerConsole.WriteAdminLine("[CLI]");
                    LegacyServerConsole.WriteAdminLine("[CLI] \tnoweapon: block weapons;");
                    LegacyServerConsole.WriteAdminLine("[CLI] \tnomove: freeze;");
                    LegacyServerConsole.WriteAdminLine("[CLI] \tnocars: block car spawning;");
                    LegacyServerConsole.WriteAdminLine("[CLI] \tnochat: mute;");
                    LegacyServerConsole.WriteAdminLine("[CLI] \tinvisible: hide overhead UI, map marker and player-list row (BCL clients); suppress chat events about this player.");
                    LegacyServerConsole.WriteAdminLine("[CLI]");
                    LegacyServerConsole.WriteAdminLine("[CLI] Example: setprop 15 nochat 1 15m | setprop 15 nochat 0");
                    return;
                case "tp":
                    LegacyServerConsole.WriteAdminLine("[CLI] tp <PlayerID> <preset|x y z|Point Tool JSON> [wcar]");
                    LegacyServerConsole.WriteAdminLine("[CLI]");
                    LegacyServerConsole.WriteAdminLine("[CLI] Examples: tp 15 10.5 20 -30 | tp 15 {\"x\":10.5,\"y\":20,\"z\":-30,\"yaw\":90}");
                    LegacyServerConsole.WriteAdminLine("[CLI]");
                    LegacyServerConsole.WriteAdminLine("[CLI] Use decimal dots. JSON yaw is optional; without it the current rotation is kept. Add wcar to keep the vehicle and its occupants; otherwise the player exits the vehicle.");
                    LegacyServerConsole.WriteAdminLine("[CLI] Presets: " + LegacyAdminTeleportParser.PresetNames);
                    LegacyServerConsole.WriteAdminLine("[CLI] tpto <PlayerID> <to PlayerID> [wcar] teleports to another player's position.");
                    return;
                default:
                    LegacyServerConsole.WriteAdminLine("[CLI] Detailed help topics: setprop, tp, alert. Use help for the full command list.");
                    return;
            }
        }
        LegacyServerConsole.WriteAdminLine("[CLI] Commands:");
        LegacyServerConsole.WriteAdminLine("[CLI]   players\t\t\t\t\t | display a list of players");
        LegacyServerConsole.WriteAdminLine("[CLI]   playerinfo <PlayerID>\t\t\t | check info about player");
        LegacyServerConsole.WriteAdminLine("[CLI]   banlist\t\t\t\t\t | list active bans (excludes property-only entries)");
        LegacyServerConsole.WriteAdminLine("[CLI]   kick <PlayerID> [reason]\t\t | kick player");
        LegacyServerConsole.WriteAdminLine("[CLI]   kill <PlayerID>\t\t\t\t | kill player");
        LegacyServerConsole.WriteAdminLine("[CLI]   ban <PlayerID> <1|2|3|4> [time] [reason] | 1 = GUID, 2 = GUID/IP, 3 = GUID/fingerprint, 4 = linked GUID/IP/fingerprint");
        LegacyServerConsole.WriteAdminLine("[CLI]   setprop <PlayerID> <prop> <0|1> [time]\t | see 'help setprop'");
        LegacyServerConsole.WriteAdminLine("[CLI]   tp <PlayerID> <preset|x y z|json> [wcar] | see 'help tp'");
        LegacyServerConsole.WriteAdminLine("[CLI]   tpto <PlayerID> <to PlayerID> [wcar]\t | teleport to a player");
        LegacyServerConsole.WriteAdminLine("[CLI]   unban <BanID>\t\t\t\t | unban player");
        LegacyServerConsole.WriteAdminLine("[CLI]   alert <PlayerID|all> <msg> <type> [args] | see 'help alert'");
        LegacyServerConsole.WriteAdminLine("[CLI]   msg <text>\t\t\t\t | send a message to the game chat");
        LegacyServerConsole.WriteAdminLine("[CLI]   clearchat\t\t\t\t | clear the game chat");
        LegacyServerConsole.WriteAdminLine("[CLI]   cleardrop\t\t\t\t | remove all world weapon/medical drops");
        LegacyServerConsole.WriteAdminLine("[CLI]   delcars all\t\t\t\t | remove every spawned car");
        LegacyServerConsole.WriteAdminLine("[CLI]   delcars <PlayerID>\t\t\t | remove cars spawned by a player");
        LegacyServerConsole.WriteAdminLine("[CLI]   reloadcfg\t\t\t\t | reload configs (events, respawns, bans, plugin policy, alerts)");
        LegacyServerConsole.WriteAdminLine("[CLI]   reloadmirror\t\t\t\t | reload ServersList.json on an active mirror");
        LegacyServerConsole.WriteAdminLine("[CLI]   stats\t\t\t\t\t | display CPU, memory, frame and runtime statistics");
        LegacyServerConsole.WriteAdminLine("[CLI]   showcfg\t\t\t\t\t | display startup server configuration with CLI overrides");
        LegacyServerConsole.WriteAdminLine("[CLI]   stop\t\t\t\t\t | stop server");
    }

    private static void PrintPlayers()
    {
        List<LegacyServerAdminService.ServerPlayerSnapshot> players = LegacyServerAdminService.GetPlayers();
        if (players.Count == 0)
        {
            LegacyServerConsole.WriteAdminLine("[CLI] No players are connected.");
            return;
        }

        LegacyServerConsole.WriteAdminLine("[CLI] PlayerID | Nickname");
        for (int i = 0; i < players.Count; i++)
        {
            LegacyServerAdminService.ServerPlayerSnapshot player = players[i];
            LegacyServerConsole.WriteAdminLine("[CLI] " + player.PlayerId.ToString(CultureInfo.InvariantCulture) + " | " + DisplayName(player.Nickname));
        }
    }

    private static void PrintPlayerInfo(string arguments)
    {
        int playerId;
        if (!TryReadSinglePositiveInt(arguments, out playerId))
        {
            LegacyServerConsole.WriteAdminLine("[CLI] Usage: playerinfo <PlayerID>");
            return;
        }

        LegacyServerAdminService.ServerPlayerSnapshot player;
        if (!LegacyServerAdminService.TryGetPlayer(playerId, out player))
        {
            LegacyServerConsole.WriteAdminLine("[CLI] PlayerID " + playerId.ToString() + " was not found.");
            return;
        }

        LegacyServerConsole.WriteAdminLine("[CLI] Player " + player.PlayerId.ToString() + " information:");
        LegacyServerConsole.WriteAdminLine("[CLI]   Nickname: " + DisplayName(player.Nickname));
        LegacyServerConsole.WriteAdminLine("[CLI]   GUID: " + EmptyAsUnknown(player.Guid));
        LegacyServerConsole.WriteAdminLine("[CLI]   IP: " + EmptyAsUnknown(player.Ip));
        LegacyServerConsole.WriteAdminLine("[CLI]   Fingerprint: " + EmptyAsUnknown(player.Fingerprint));
        LegacyServerConsole.WriteAdminLine("[CLI]   Time on server: " + FormatDuration(player.ConnectedSeconds));
        LegacyServerConsole.WriteAdminLine("[CLI]   Cars: " + player.ActiveCars.ToString() + " active, " + player.TotalCarsSpawned.ToString() + " spawned");
        LegacyServerConsole.WriteAdminLine("[CLI]   Ping: " + (player.PingMilliseconds >= 0 ? player.PingMilliseconds.ToString() + " ms" : "unavailable"));
        LegacyServerConsole.WriteAdminLine("[CLI]   Platform: " + EmptyAsUnknown(player.Platform));
        NetInputControl pluginInput;
        if (LegacyServerAdminService.TryGetInput(playerId, out pluginInput) && pluginInput)
            LegacyServerConsole.WriteAdminLine("[CLI]   Plugins: " + LegacyPluginNetwork.Inventory(pluginInput.netConnect));
        List<LegacyBanList.PlayerProp> props = LegacyBanList.GetProps(player.Guid);
        if (props.Count > 0)
        {
            var values = new List<string>();
            foreach (LegacyBanList.PlayerProp prop in props) values.Add(prop.Prop + " (" + LegacyAdminTime.Remaining(prop.Until) + ")");
            LegacyServerConsole.WriteAdminLine("[CLI]   Props: " + string.Join(", ", values.ToArray()));
        }
    }

    private static void Kick(string arguments)
    {
        string idText, reason; SplitCommand(arguments, out idText, out reason); int playerId;
        if (!int.TryParse(idText, out playerId) || playerId <= 0 || reason.Length > 512)
        { LegacyServerConsole.WriteAdminLine("[CLI] Usage: kick <PlayerID> [reason] (maximum 512 characters)."); return; }
        string error;
        if (!LegacyServerAdminService.KickPlayer(playerId, out error, LegacyAlertsConfig.Escapes(reason)))
        { LegacyServerConsole.WriteAdminLine("[CLI] Kick failed: " + error); return; }
        LegacyServerConsole.WriteAdminLine("[CLI] Disconnect requested for PlayerID " + playerId + ".");
    }

    private static void Kill(string arguments)
    {
        int id;
        if (!TryReadSinglePositiveInt(arguments, out id))
        { LegacyServerConsole.WriteAdminLine("[CLI] Usage: kill <PlayerID>"); return; }
        string error;
        LegacyServerConsole.WriteAdminLine(LegacyServerAdminService.KillPlayer(id, out error) ?
            "[CLI] PlayerID " + id + " killed." : "[CLI] Kill failed: " + error);
    }

    private static void Ban(string arguments)
    {
        string idText, rest, typeText, tail;
        SplitCommand(arguments, out idText, out rest); SplitCommand(rest, out typeText, out tail);
        int playerId, banType;
        if (!int.TryParse(idText, out playerId) || playerId <= 0 || !int.TryParse(typeText, out banType))
        { LegacyServerConsole.WriteAdminLine("[CLI] Usage: ban <PlayerID> <1|2|3|4> [time] [reason]."); return; }
        string duration = null, reason = tail;
        if (tail.Length > 0)
        {
            string first, remainder; SplitCommand(tail, out first, out remainder);
            if (first.Length > 0 && (char.IsDigit(first[0]) || first[0] == '-' || first[0] == '+')) { duration = first; reason = remainder; }
        }
        long? until;
        if (!LegacyAdminTime.TryParse(duration, out until) || reason.Length > 512)
        { LegacyServerConsole.WriteAdminLine("[CLI] Invalid duration or reason longer than 512 characters."); return; }
        LegacyBanList.BanEntry entry;
        string error;
        if (!LegacyServerAdminService.BanAndKickPlayer(playerId, banType, until, out entry, out error, LegacyAlertsConfig.Escapes(reason)))
        {
            LegacyServerConsole.WriteAdminLine("[CLI] Ban failed: " + error);
            return;
        }

        string value = banType == LegacyBanList.BanTypeGuid ? entry.GUID : banType == LegacyBanList.BanTypeIp ? entry.IP : "fingerprint (" + entry.Fingerprints.Count + " stored)";
        LegacyServerConsole.WriteAdminLine(
            "[CLI] PlayerID " + playerId.ToString() + " banned. BanID=" + entry.BanID.ToString() +
            ", Type=" + banType.ToString() + ", Value=" + EmptyAsUnknown(value) + ", Duration=" + LegacyAdminTime.Remaining(until) + "."
        );
    }

    private static void SetProp(string arguments)
    {
        string[] tokens = Tokenize(arguments);
        int id;
        if (tokens.Length < 3 || tokens.Length > 4 || !TryReadSinglePositiveInt(tokens[0], out id) ||
            !LegacyBanList.IsKnownProp(tokens[1].ToLowerInvariant()) || (tokens[2] != "0" && tokens[2] != "1"))
        { PrintHelp("setprop"); return; }
        long? until;
        if (!LegacyAdminTime.TryParse(tokens.Length == 4 ? tokens[3] : null, out until))
        { LegacyServerConsole.WriteAdminLine("[CLI] Invalid duration. Use a positive integer with s, m, h or d."); return; }
        string error;
        bool enabled = tokens[2] == "1";
        string prop = tokens[1].ToLowerInvariant();
        if (!LegacyServerAdminService.SetPlayerProp(id, prop, enabled, until, out error))
        { LegacyServerConsole.WriteAdminLine("[CLI] Setprop failed: " + error); return; }
        LegacyServerConsole.WriteAdminLine("[CLI] PlayerID " + id + ": " + prop + (enabled ? " enabled (" + LegacyAdminTime.Remaining(until) + ")." : " removed."));
    }

    private static void Teleport(string arguments)
    {
        string idText, coordinates;
        SplitCommand(arguments, out idText, out coordinates);
        bool withCar = LegacyAdminTeleportParser.StripWithCar(ref coordinates);
        int id;
        UnityEngine.Vector3 position;
        float? yaw;
        if (!TryReadSinglePositiveInt(idText, out id) || !LegacyAdminTeleportParser.TryParse(coordinates, out position, out yaw))
        { PrintHelp("tp"); return; }
        string error;
        LegacyServerConsole.WriteAdminLine(LegacyServerAdminService.TeleportPlayer(id, position, yaw, withCar, out error) ?
            "[CLI] PlayerID " + id + " teleported to " + position + "." : "[CLI] Teleport failed: " + error);
    }

    private static void TeleportTo(string arguments)
    {
        bool withCar = LegacyAdminTeleportParser.StripWithCar(ref arguments);
        string[] tokens = Tokenize(arguments);
        int id, target;
        if (tokens.Length != 2 || !TryReadSinglePositiveInt(tokens[0], out id) || !TryReadSinglePositiveInt(tokens[1], out target))
        { LegacyServerConsole.WriteAdminLine("[CLI] Usage: tpto <PlayerID> <to PlayerID> [wcar]"); return; }
        NetInputControl input;
        if (!LegacyServerAdminService.TryGetInput(target, out input) || !input.input || !input.input.currentOrParent)
        { LegacyServerConsole.WriteAdminLine("[CLI] Target player has no spawned character."); return; }
        string error;
        LegacyServerConsole.WriteAdminLine(LegacyServerAdminService.TeleportPlayer(id, input.input.currentOrParent.body_pos, null, withCar, out error) ?
            "[CLI] PlayerID " + id + " teleported to PlayerID " + target + "." : "[CLI] Teleport failed: " + error);
    }

    private static void Unban(string arguments)
    {
        int banId;
        if (!TryReadSinglePositiveInt(arguments, out banId))
        {
            LegacyServerConsole.WriteAdminLine("[CLI] Usage: unban <BanID>");
            return;
        }

        LegacyBanList.BanEntry removed;
        string error;
        if (!LegacyBanList.RemoveBan(banId, out removed, out error))
        {
            LegacyServerConsole.WriteAdminLine("[CLI] Unban failed: " + error);
            return;
        }

        LegacyServerConsole.WriteAdminLine("[CLI] BanID " + removed.BanID.ToString() + " removed for '" + DisplayName(removed.Nickname) + "'.");
    }

    private static void ReloadConfigs()
    {
        bool eventsOk = LegacyEventsConfig.ReloadAndBuild();
        bool respawnsOk = LegacyNearestPointRespawn.Reload();
        bool bansOk = LegacyBanList.Reload();
        bool admissionOk = LegacyAdmissionConfig.Reload();

        LegacyServerConsole.WriteAdminLine(
            "[CLI] Config reload complete: events=" + eventsOk.ToString() +
            ", respawnPoints=" + respawnsOk.ToString() +
            ", banList=" + bansOk.ToString() + ", pluginPolicy/alerts=" + admissionOk.ToString() + "."
        );
    }

    private static void SendMessage(string message)
    {
        string error;
        if (!LegacyServerAdminService.TrySendServerMessage(message, out error))
        {
            LegacyServerConsole.WriteAdminLine("[CLI] Message failed: " + error);
            return;
        }

        LegacyServerConsole.WriteAdminLine("[CLI] Server message sent.");
    }

    private static void ClearDrop()
    {
        int failed;
        int removed = LegacyServerDropCleanup.ClearAllDrops(out failed);
        LegacyServerConsole.WriteAdminLine(
            "[CLI] Drop cleanup complete: removed=" + removed.ToString() +
            ", failed=" + failed.ToString() + "."
        );
    }

    private static void DeleteCars(string arguments)
    {
        string[] tokens = Tokenize(arguments);
        if (tokens.Length != 1)
        {
            LegacyServerConsole.WriteAdminLine("[CLI] Usage: delcars all | delcars <PlayerID>");
            return;
        }

        int ejected;
        int failed;
        int removed;

        if (string.Equals(tokens[0], "all", StringComparison.OrdinalIgnoreCase))
        {
            removed = LegacyServerAdminService.DeleteAllCars(out ejected, out failed);
            LegacyServerConsole.WriteAdminLine(
                "[CLI] Vehicle cleanup complete: scope=all, removed=" + removed.ToString() +
                ", ejected=" + ejected.ToString() +
                ", failed=" + failed.ToString() + "."
            );
            return;
        }

        int playerId;
        if (!int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out playerId) || playerId <= 0)
        {
            LegacyServerConsole.WriteAdminLine("[CLI] Usage: delcars all | delcars <PlayerID>");
            return;
        }

        removed = LegacyServerAdminService.DeleteCarsOwnedByPlayer(playerId, out ejected, out failed);
        LegacyServerConsole.WriteAdminLine(
            "[CLI] Vehicle cleanup complete: scope=PlayerID " + playerId.ToString() +
            ", removed=" + removed.ToString() +
            ", ejected=" + ejected.ToString() +
            ", failed=" + failed.ToString() + "."
        );
    }

    private static bool TryReadSinglePositiveInt(string value, out int result)
    {
        result = 0;
        string[] tokens = Tokenize(value);
        return tokens.Length == 1 &&
               int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out result) &&
               result > 0;
    }

    private static string[] Tokenize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? new string[0]
            : value.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static void SplitCommand(string raw, out string command, out string arguments)
    {
        string trimmed = raw.Trim();
        int separator = trimmed.IndexOfAny(new[] { ' ', '\t' });
        if (separator < 0)
        {
            command = trimmed;
            arguments = string.Empty;
            return;
        }

        command = trimmed.Substring(0, separator);
        arguments = trimmed.Substring(separator + 1).TrimStart();
    }

    private static string FormatDuration(float seconds)
    {
        TimeSpan time = TimeSpan.FromSeconds(Math.Max(0f, seconds));
        if (time.TotalDays >= 1d)
        {
            return ((int)time.TotalDays).ToString() + "d " + time.Hours.ToString("00") + ":" + time.Minutes.ToString("00") + ":" + time.Seconds.ToString("00");
        }
        return time.Hours.ToString("00") + ":" + time.Minutes.ToString("00") + ":" + time.Seconds.ToString("00");
    }

    private static string DisplayName(string value)
    {
        return string.IsNullOrEmpty(value) ? "<unknown>" : value;
    }

    private static string EmptyAsUnknown(string value)
    {
        return string.IsNullOrEmpty(value) ? "<unknown>" : value;
    }
}
