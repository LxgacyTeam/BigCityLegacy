using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal static class LegacyCsDamageCooldown
{
    internal const int DurationMs = 2500;

    private const string JsonProtectionUntil = "_bclAfterRespawnUntil";

    private static readonly Dictionary<Net_CS.CsBasePlayer, int> ServerProtectionUntil =
        new Dictionary<Net_CS.CsBasePlayer, int>();

    private static readonly Dictionary<Net_CS.CsBasePlayer, int> ClientProtectionUntil =
        new Dictionary<Net_CS.CsBasePlayer, int>();

    internal static bool ServerEnabled
    {
        get
        {
            return NetManager.isServer && !LegacyCommandLine.HasArg("-noCsDamageCooldown");
        }
    }

    internal static void BeginServerProtection(NetSpawn spawn)
    {
        if (!ServerEnabled || spawn == null || spawn.netInput == null)
        {
            return;
        }

        Net_CS cs = spawn.netInput.curEvent as Net_CS;
        if (cs == null || cs.state != Net_BaseEvent.CurState.Race)
        {
            return;
        }

        Net_CS.CsBasePlayer player = cs.GetPlayerByInput(spawn.netInput) as Net_CS.CsBasePlayer;
        if (player == null || !player.input)
        {
            return;
        }

        int protectionUntil = unchecked(NetworkTime.ServerTime + DurationMs);
        ServerProtectionUntil[player] = protectionUntil;

        cs.CallRpc_ChangeJSon(player.id, player.GetInitData());
    }

    internal static bool ShouldBlockServerDamage(PlayerControl player, float damage)
    {
        if (!ServerEnabled || player == null || damage <= 0f)
        {
            return false;
        }

        InputControl input = player.GetInputFromMinusLive();
        NetInputControl netInput = input ? input.netInput : null;
        if (netInput == null)
        {
            return false;
        }

        Net_CS cs = netInput.curEvent as Net_CS;
        if (cs == null || cs.state != Net_BaseEvent.CurState.Race)
        {
            return false;
        }

        if (netInput.spawn != null && netInput.spawn.nowRespawn)
        {
            return true;
        }

        Net_CS.CsBasePlayer basePlayer = cs.GetPlayerByInput(netInput) as Net_CS.CsBasePlayer;
        if (basePlayer == null)
        {
            return false;
        }

        int protectionUntil;
        if (!ServerProtectionUntil.TryGetValue(basePlayer, out protectionUntil))
        {
            return false;
        }

        if (IsBefore(NetworkTime.ServerTime, protectionUntil))
        {
            return true;
        }

        ServerProtectionUntil.Remove(basePlayer);
        return false;
    }

    internal static string AddServerStateToJson(Net_CS.CsBasePlayer player, string json)
    {
        if (!ServerEnabled || player == null || string.IsNullOrEmpty(json))
        {
            return json;
        }

        try
        {
            JObject root = JObject.Parse(json);
            root[JsonProtectionUntil] = GetServerProtectionUntil(player);
            return root.ToString(Formatting.None);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[BigCityLegacy] Failed to add CS damage cooldown state to player JSON: " + ex.Message);
            return json;
        }
    }

    internal static void ReadClientStateFromJson(Net_CS.CsBasePlayer player, ref string json)
    {
        if (player == null || string.IsNullOrEmpty(json))
        {
            return;
        }

        try
        {
            JObject root = JObject.Parse(json);
            JToken token = root[JsonProtectionUntil];

            if (token == null)
            {
                // No extension field means this packet/server does not advertise the feature.
                ClientProtectionUntil.Remove(player);
                return;
            }

            int protectionUntil;
            if (!int.TryParse(token.ToString(), out protectionUntil))
            {
                protectionUntil = 0;
            }

            ClientProtectionUntil[player] = protectionUntil;

            root.Remove(JsonProtectionUntil);
            json = root.ToString(Formatting.None);
        }
        catch (Exception ex)
        {
            ClientProtectionUntil.Remove(player);
            Debug.LogWarning("[BigCityLegacy] Failed to read CS damage cooldown state from player JSON: " + ex.Message);
        }
    }

    internal static bool IsClientProtected(Net_CS cs, NetInputControl input)
    {
        if (cs == null || input == null)
        {
            return false;
        }

        Net_CS.CsBasePlayer player = cs.GetPlayerByInput(input) as Net_CS.CsBasePlayer;
        if (player == null)
        {
            return false;
        }

        int protectionUntil;
        if (!ClientProtectionUntil.TryGetValue(player, out protectionUntil) || protectionUntil == 0)
        {
            return false;
        }

        return IsBefore(NetworkTime.ServerTime, protectionUntil);
    }

    internal static void ApplyClientFresnelOverride(NetInputControl input)
    {
        if (input == null)
        {
            return;
        }

        Net_CS cs = input.curEvent as Net_CS;
        if (!IsClientProtected(cs, input))
        {
            return;
        }

        InputControl control = input.input;
        PlayerControl player = control ? control.currentPlayer : null;
        if (!player || player == PlayerControl.curPlyaer || player.saveLoad == null)
        {
            return;
        }

        Color protectionColor = Color.green;
        protectionColor.a = 0.8f;
        player.saveLoad.SetFresnel(true, protectionColor, 1f, 1f);
    }

    internal static void UpdateClient(Net_CS cs)
    {
        if (cs == null || ClientProtectionUntil.Count == 0)
        {
            return;
        }

        int now = NetworkTime.ServerTime;
        for (int i = 0; i < cs.players.Count; i++)
        {
            Net_CS.CsBasePlayer player = cs.players[i] as Net_CS.CsBasePlayer;
            if (player == null)
            {
                continue;
            }

            int protectionUntil;
            if (!ClientProtectionUntil.TryGetValue(player, out protectionUntil) || protectionUntil == 0)
            {
                continue;
            }

            if (IsBefore(now, protectionUntil))
            {
                continue;
            }

            ClientProtectionUntil[player] = 0;

            if (player.input && player.input.ui != null)
            {
                player.input.ui.DisableIconByEventIfNeed();
            }
        }
    }

    internal static void ClearPlayer(Net_CS.CsBasePlayer player)
    {
        if (player == null)
        {
            return;
        }

        ServerProtectionUntil.Remove(player);
        ClientProtectionUntil.Remove(player);
    }

    internal static void ClearEvent(Net_CS cs)
    {
        if (cs == null)
        {
            return;
        }

        RemovePlayersForEvent(ServerProtectionUntil, cs);
        RemovePlayersForEvent(ClientProtectionUntil, cs);
    }

    private static int GetServerProtectionUntil(Net_CS.CsBasePlayer player)
    {
        int protectionUntil;
        if (!ServerProtectionUntil.TryGetValue(player, out protectionUntil))
        {
            return 0;
        }

        if (IsBefore(NetworkTime.ServerTime, protectionUntil))
        {
            return protectionUntil;
        }

        ServerProtectionUntil.Remove(player);
        return 0;
    }

    private static bool IsBefore(int now, int until)
    {
        return unchecked(now - until) < 0;
    }

    private static void RemovePlayersForEvent(Dictionary<Net_CS.CsBasePlayer, int> states, Net_CS cs)
    {
        if (states.Count == 0)
        {
            return;
        }

        List<Net_CS.CsBasePlayer> remove = null;
        foreach (KeyValuePair<Net_CS.CsBasePlayer, int> pair in states)
        {
            if (pair.Key == null || pair.Key.cs == cs)
            {
                if (remove == null)
                {
                    remove = new List<Net_CS.CsBasePlayer>();
                }
                remove.Add(pair.Key);
            }
        }

        if (remove == null)
        {
            return;
        }

        for (int i = 0; i < remove.Count; i++)
        {
            states.Remove(remove[i]);
        }
    }
}
