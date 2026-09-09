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
                PrintHelp();
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

            case "ban":
                Ban(arguments);
                return;

            case "unban":
                Unban(arguments);
                return;

            case "reloadcfg":
                ReloadConfigs();
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

            case "stop":
                LegacyServerShutdown.Request("CLI command");
                return;

            default:
                LegacyServerConsole.WriteAdminLine("[CLI] Unknown command '" + command + "'. Type 'help' for the command list.");
                return;
        }
    }

    private static void PrintHelp()
    {
        LegacyServerConsole.WriteAdminLine("[CLI] Commands:");
        LegacyServerConsole.WriteAdminLine("[CLI]   players\t\t\t| display a list of players");
        LegacyServerConsole.WriteAdminLine("[CLI]   playerinfo <PlayerID>\t| check info about player");
        LegacyServerConsole.WriteAdminLine("[CLI]   kick <PlayerID>\t\t| kick player");
        LegacyServerConsole.WriteAdminLine("[CLI]   ban <PlayerID> <1|2>\t| ban player; 1 = GUID, 2 = IP");
        LegacyServerConsole.WriteAdminLine("[CLI]   unban <BanID>\t\t| unban player");
        LegacyServerConsole.WriteAdminLine("[CLI]   reloadcfg\t\t| reload configs (EventsList, RespawnPoints, BanList)");
        LegacyServerConsole.WriteAdminLine("[CLI]   msg <text>\t\t| send a message to the game chat");
        LegacyServerConsole.WriteAdminLine("[CLI]   cleardrop\t\t| remove all world weapon/medical drops");
        LegacyServerConsole.WriteAdminLine("[CLI]   delcars all\t\t| remove every spawned car");
        LegacyServerConsole.WriteAdminLine("[CLI]   delcars <PlayerID>\t| remove cars spawned by a player");
        LegacyServerConsole.WriteAdminLine("[CLI]   stop\t\t\t| stop server");
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
        LegacyServerConsole.WriteAdminLine("[CLI]   Time on server: " + FormatDuration(player.ConnectedSeconds));
        LegacyServerConsole.WriteAdminLine("[CLI]   Cars: " + player.ActiveCars.ToString() + " active, " + player.TotalCarsSpawned.ToString() + " spawned");
        LegacyServerConsole.WriteAdminLine("[CLI]   Ping: " + (player.PingMilliseconds >= 0 ? player.PingMilliseconds.ToString() + " ms" : "unavailable"));
        LegacyServerConsole.WriteAdminLine("[CLI]   Platform: " + EmptyAsUnknown(player.Platform));
    }

    private static void Kick(string arguments)
    {
        int playerId;
        if (!TryReadSinglePositiveInt(arguments, out playerId))
        {
            LegacyServerConsole.WriteAdminLine("[CLI] Usage: kick <PlayerID>");
            return;
        }

        string error;
        if (!LegacyServerAdminService.KickPlayer(playerId, out error))
        {
            LegacyServerConsole.WriteAdminLine("[CLI] Kick failed: " + error);
            return;
        }

        LegacyServerConsole.WriteAdminLine("[CLI] Disconnect requested for PlayerID " + playerId.ToString() + ".");
    }

    private static void Ban(string arguments)
    {
        string[] tokens = Tokenize(arguments);
        if (tokens.Length != 2)
        {
            LegacyServerConsole.WriteAdminLine("[CLI] Usage: ban <PlayerID> <1|2>. Type 1 bans by GUID; type 2 bans by IP.");
            return;
        }

        int playerId;
        int banType;
        if (!int.TryParse(tokens[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out playerId) || playerId <= 0 ||
            !int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out banType))
        {
            LegacyServerConsole.WriteAdminLine("[CLI] PlayerID and ban type must be numeric.");
            return;
        }

        LegacyBanList.BanEntry entry;
        string error;
        if (!LegacyServerAdminService.BanAndKickPlayer(playerId, banType, out entry, out error))
        {
            LegacyServerConsole.WriteAdminLine("[CLI] Ban failed: " + error);
            return;
        }

        string value = banType == LegacyBanList.BanTypeGuid ? entry.GUID : entry.IP;
        LegacyServerConsole.WriteAdminLine(
            "[CLI] PlayerID " + playerId.ToString() + " banned. BanID=" + entry.BanID.ToString() +
            ", Type=" + banType.ToString() + ", Value=" + EmptyAsUnknown(value) + "."
        );
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

        LegacyServerConsole.WriteAdminLine(
            "[CLI] Config reload complete: events=" + eventsOk.ToString() +
            ", respawnPoints=" + respawnsOk.ToString() +
            ", banList=" + bansOk.ToString() + "."
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
