using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BepInEx.Bootstrap;
using BigCityLegacy.Api;
using UnityEngine;
using UnityEngine.Networking;

internal static class LegacyPluginNetwork
{
    internal const short MessageId = 18472;
    private sealed class Peer
    {
        internal NetConnect Connect;
        internal List<LegacyPluginRecord> Plugins;
        internal bool Capable, Accepted, Welcomed, Closing;
        internal string BclVersion;
        internal uint CloseId;
        internal float Deadline;
    }
    private static readonly Dictionary<NetworkConnection, Peer> peers = new Dictionary<NetworkConnection, Peer>();
    private static uint nextMessage;
    internal static void StartServer()
    {
        peers.Clear(); LegacyConnectionStamp.Clear();
        if (LegacyAdmissionConfig.Policy == null)
        { try { LegacyAdmissionConfig.Initialize(); } catch { NetworkServer.Shutdown(); throw; } }
        if (LegacyAdmissionConfig.Enabled) NetworkServer.RegisterHandler(MessageId, ReceiveServer);
    }
    internal static void StopServer() { peers.Clear(); LegacyConnectionStamp.Clear(); }
    internal static void StartClient(NetworkClient client) { LegacyWelcomeClient.Clear(); client.RegisterHandler(MessageId, ReceiveClient); }
    internal static void Forget(NetworkConnection connection) { if (connection != null) peers.Remove(connection); LegacyConnectionStamp.Forget(connection); }

    internal static bool Attach(ref string xml)
    {
        try
        {
            var records = new List<LegacyPluginRecord>();
            foreach (var info in Chainloader.PluginInfos.Values)
            {
                if (info == null || info.Metadata == null) continue;
                records.Add(new LegacyPluginRecord { Guid = info.Metadata.GUID, Name = info.Metadata.Name, Version = info.Metadata.Version.ToString() });
            }
            records.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Guid, b.Guid));
            xml = LegacyPluginManifest.Attach(LegacyConnectionStamp.Attach(xml), records, VersionInfo.ModVersionName); return true;
        }
        catch (Exception ex)
        {
            Debug.LogError("[BCL plugins] Cannot submit complete plugin manifest: " + ex.Message);
            LegacyAlerts.ShowLocalized("Cannot send the complete plugin list.\n" + ex.Message, "Не удалось отправить полный список плагинов.\n" + ex.Message, title: "Connection cancelled", titleRu: "Подключение отменено");
            if (NetManager.me) NetManager.me.StopClient();
            return false;
        }
    }
    // Called from the existing authentication prefix, before any player/session is created.
    internal static bool Admit(NetConnect connect, string guid, string nickname, ref string xml)
    {
        if (!NetworkServer.active || !connect || connect.conn == null) return true;
        try { LegacyConnectionStamp.Capture(connect, ref xml); }
        catch { Debug.LogWarning("[Server] Invalid connection metadata; connection rejected."); LegacyServerAdminService.Disconnect(connect,true); return false; }
        if (!LegacyAdmissionConfig.Enabled) return !LegacyServerAdminService.TryRejectBannedConnection(connect, guid, nickname);
        Peer previous;
        if (peers.TryGetValue(connect.conn, out previous) && (previous.Closing || previous.Accepted)) return false;
        var peer = new Peer { Connect = connect }; peers[connect.conn] = peer;
        string invalid = null; bool advertised = false;
        try { peer.Plugins = LegacyPluginManifest.Extract(ref xml, out advertised, out peer.BclVersion); }
        catch (Exception ex) { invalid = ex.Message; }
        peer.Capable = advertised;
        if (LegacyServerAdminService.TryRejectBannedConnection(connect, guid, nickname)) return false;
        var variables = Context(connect, nickname); variables["reason"] = invalid ?? "";
        if (invalid != null)
        { Reject(peer, "invalidManifest", variables); return false; }
        var violations = LegacyAdmissionConfig.Policy.Evaluate(peer.Plugins, peer.BclVersion);
        if (violations.Count > 0)
        {
            if (violations[0].Kind == "missingManifest" || violations[0].Kind == "missingBclVersion") Reject(peer, violations[0].Kind, variables);
            else
            {
                var template = LegacyAdmissionConfig.Alerts.Templates["plugins"];
                var lines = new List<string>(); var russianLines = new List<string>();
                foreach (var violation in violations)
                {
                    if (violation.Rule == null) { lines.Add(violation.Reason); russianLines.Add(violation.Kind == "missingBclVersion" ? "Клиент не передал версию BCL." : "Клиент не передал список плагинов."); continue; }
                    var rule = violation.Rule; var plugin = violation.Plugin;
                    variables["pluginGuid"] = rule.Guid; variables["pluginName"] = rule.Name ?? (plugin == null ? rule.Guid : plugin.Name);
                    variables["pluginVersion"] = plugin == null ? "not installed" : plugin.Version;
                    variables["requiredVersion"] = rule.Requirement; variables["rule"] = violation.Kind; variables["reason"] = violation.Reason;
                    string text = LegacyAlertsConfig.Format(rule.Message ?? template.Message, variables);
                    if (!string.IsNullOrEmpty(rule.Extra)) text += "\n" + LegacyAlertsConfig.Format(rule.Extra, variables);
                    lines.Add(text);
                    bool translated = rule.MessageRu != null || rule.Message == null && template.MessageRu != null;
                    string russian = LegacyAlertsConfig.Format(rule.MessageRu ?? (rule.Message != null ? rule.Message : template.MessageRu ?? template.Message), translated ? LegacyAlertsConfig.RussianVariables(variables) : variables);
                    if (!string.IsNullOrEmpty(rule.ExtraRu ?? rule.Extra)) russian += "\n" + LegacyAlertsConfig.Format(rule.ExtraRu ?? rule.Extra, rule.ExtraRu != null ? LegacyAlertsConfig.RussianVariables(variables) : variables);
                    russianLines.Add(russian);
                    Debug.LogWarning("[BCL plugins] Reject '" + nickname + "': " + violation.Kind + " " + rule.Guid + " installed=" + variables["pluginVersion"] + " rule=" + rule.Requirement);
                }
                var alert = template.Render(variables); alert.Message = Limit(string.Join("\n\n", lines.ToArray()), 8000);
                alert.MessageRu = Limit(string.Join("\n\n", russianLines.ToArray()), 8000);
                Close(peer, template.Enabled ? alert : null);
            }
            return false;
        }
        peer.Accepted = true;
        Debug.Log("[BCL plugins] BCL version from '" + nickname + "': " + (peer.BclVersion ?? "not reported"));
        Debug.Log("[BCL plugins] Accepted '" + nickname + "': " + (peer.Plugins == null ? "no manifest (allowed by policy)" : peer.Plugins.Count + " plugins"));
        return true;
    }
    private static void Reject(Peer peer, string template, Dictionary<string, string> variables)
    {
        Debug.LogWarning("[BCL plugins] Rejected connection: " + template + " " + variables["reason"]);
        var selected = LegacyAdmissionConfig.Alerts.Templates[template]; Close(peer, selected.Enabled ? selected.Render(variables) : null);
    }
    private static void Close(Peer peer, LegacyAlertMessage alert)
    {
        if (alert != null) Send(peer.Connect.conn, alert, true);
        LegacyServerAdminService.Disconnect(peer.Connect, true);
    }
    internal static bool DelayDisconnect(NetConnect connect)
    { Peer peer; return connect && connect.conn != null && peers.TryGetValue(connect.conn, out peer) && peer.Closing && Time.realtimeSinceStartup < peer.Deadline; }
    internal static bool Send(NetworkConnection connection, LegacyAlertMessage alert, bool close = false)
    {
        Peer peer;
        if (!LegacyAdmissionConfig.Enabled) return false;
        if (connection == null || !peers.TryGetValue(connection, out peer) || !peer.Capable || peer.Closing) return false;
        alert.Validate(); uint id = ++nextMessage; if (id == 0) id = ++nextMessage;
        if (close) { peer.Closing = true; peer.CloseId = id; peer.Deadline = Time.realtimeSinceStartup + 2f; }
        bool result = connection.Send(MessageId, new Packet { Kind = 1, Id = id, Alert = alert, Close = close });
        if (!result && close) peer.Deadline = Time.realtimeSinceStartup;
        return result;
    }
    internal static void Tick()
    {
        if (!NetworkServer.active || !LegacyAdmissionConfig.Enabled) return;
        foreach (var pair in new List<KeyValuePair<NetworkConnection, Peer>>(peers))
        {
            var peer = pair.Value;
            if (peer.Closing)
            {
                if (Time.realtimeSinceStartup >= peer.Deadline) { peers.Remove(pair.Key); pair.Key.Disconnect(); }
                continue;
            }
            if (peer.Accepted && !peer.Welcomed && peer.Connect && peer.Connect.netInput && peer.Connect.netInput.onServerNetPlayer)
            {
                peer.Welcomed = true;
                var template = LegacyAdmissionConfig.Alerts.Templates["welcome"];
                if (template.Enabled)
                { var welcome = template.Render(Context(peer.Connect, peer.Connect.nikName)); welcome.IsWelcome = true; Send(pair.Key, welcome); }
            }
        }
    }
    private static void ReceiveServer(NetworkMessage message)
    {
        Peer peer;
        if (message.conn == null || !peers.TryGetValue(message.conn, out peer) || !peer.Closing) return;
        try
        {
            if (message.reader.Length > 16) return;
            var packet = message.ReadMessage<Packet>();
            if (packet.Kind == 2 && packet.Id == peer.CloseId) { peers.Remove(message.conn); message.conn.Disconnect(); }
        }
        catch (Exception) { /* Invalid acknowledgements cannot change session state. Deadline still applies. */ }
    }
    private static void ReceiveClient(NetworkMessage message)
    {
        if (!NetManager.me || NetManager.me.client == null || message.conn != NetManager.me.client.connection) return;
        try
        {
            if (message.reader.Length > 60000) throw new FormatException("Alert packet is too large.");
            var packet = message.ReadMessage<Packet>();
            if (packet.Kind != 1) return;
            packet.Alert.Validate();
            if (packet.Alert.IsWelcome) LegacyWelcomeClient.Receive(packet.Alert, message.conn.address, NetManager.me.networkPort);
            else LegacyAlerts.Enqueue(packet.Alert);
            if (packet.Close) message.conn.Send(MessageId, new Packet { Kind = 2, Id = packet.Id });
        }
        catch (Exception ex) { Debug.LogWarning("[BCL alerts] Rejected malformed server alert: " + ex.Message); }
    }
    internal static Dictionary<string, string> Context(NetConnect connect, string nickname)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            {"nickname", Limit(nickname ?? "Player", 128)}, {"playerId", ""}, {"serverPort", NetManager.usePort.ToString(CultureInfo.InvariantCulture)},
            {"reason", "Not specified"}, {"duration", "permanent"}, {"until", ""}, {"banId", ""}
        };
        Peer peer; values["bclVersion"] = connect && connect.conn != null && peers.TryGetValue(connect.conn, out peer) ? peer.BclVersion ?? "not reported" : "not reported";
        if (connect && connect.netInput)
        {
            var player = LegacyServerAdminService.SnapshotForInput(connect.netInput);
            if (player != null) values["playerId"] = player.PlayerId.ToString(CultureInfo.InvariantCulture);
        }
        return values;
    }
    internal static void Moderation(NetConnect connect, string template, LegacyBanList.BanEntry ban = null, string reason = null, string nickname = null)
    {
        if (!LegacyAdmissionConfig.Enabled || !connect || connect.conn == null) return;
        var values = Context(connect, connect.nikName);
        if (!string.IsNullOrWhiteSpace(reason)) values["reason"] = Limit(reason, 512);
        if (ban != null)
        {
            values["nickname"] = Limit(string.IsNullOrEmpty(connect.nikName) ? ban.Nickname ?? "Player" : connect.nikName, 128);
            values["banId"] = ban.BanID.ToString(CultureInfo.InvariantCulture); values["duration"] = LegacyAdminTime.Remaining(ban.Until);
            values["until"] = ban.Until.HasValue ? ban.Until.Value.ToString(CultureInfo.InvariantCulture) : "permanent";
            values["reason"] = Limit(ban.Reason ?? "Not specified", 512);
        }
        if (nickname != null) values["nickname"] = Limit(nickname, 128);
        var selected = LegacyAdmissionConfig.Alerts.Templates[template];
        if (selected.Enabled) Send(connect.conn, selected.Render(values), true);
    }
    internal static string Inventory(NetConnect connect)
    {
        Peer peer;
        if (!connect || connect.conn == null || !peers.TryGetValue(connect.conn, out peer)) return "not reported";
        var list = new List<string>();
        if (peer.Plugins != null) foreach (var plugin in peer.Plugins) list.Add(plugin.Guid + "@" + plugin.Version + " (" + plugin.Name + ")");
        return "BCL=" + (peer.BclVersion ?? "not reported") + "; plugins=" + (peer.Plugins == null ? "not reported" : string.Join(", ", list.ToArray()));
    }
    private static string Limit(string text, int limit) { return text.Length <= limit ? text : text.Substring(0, limit - 3) + "..."; }
    internal sealed class Packet : MessageBase
    {
        internal byte Kind; internal uint Id; internal bool Close; internal LegacyAlertMessage Alert;
        public override void Serialize(NetworkWriter writer)
        {
            writer.Write((byte)1); writer.Write(Kind); writer.Write(Id);
            if (Kind != 1) return;
            Alert.Validate(); writer.Write(Close); writer.Write((byte)Alert.Display); writer.Write(Alert.Duration); writer.Write(Alert.Title); writer.Write(Alert.Message);
            // Optional v2 tail; older alert clients still read the v1 English payload.
            writer.Write((byte)2); writer.Write(Alert.TitleRu ?? ""); writer.Write(Alert.MessageRu ?? ""); writer.Write(Alert.IsWelcome); writer.Write(Alert.UpdatedAt);
        }
        public override void Deserialize(NetworkReader reader)
        {
            if (reader.ReadByte() != 1) throw new FormatException("Unsupported alert wire version.");
            Kind = reader.ReadByte(); Id = reader.ReadUInt32();
            if (Kind == 2) return;
            if (Kind != 1) throw new FormatException("Unknown alert packet.");
            Close = reader.ReadBoolean(); Alert = new LegacyAlertMessage { Display = (LegacyAlertDisplay)reader.ReadByte(), Duration = reader.ReadSingle(), Title = reader.ReadString(), Message = reader.ReadString() };
            if (reader.Position < reader.Length)
            {
                if (reader.ReadByte() != 2) throw new FormatException("Unsupported alert extension.");
                Alert.TitleRu = reader.ReadString(); Alert.MessageRu = reader.ReadString(); Alert.IsWelcome = reader.ReadBoolean(); Alert.UpdatedAt = reader.ReadInt64();
            }
            Alert.Validate();
        }
    }
}
