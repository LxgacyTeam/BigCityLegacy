using System;
using System.Collections;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Fix the server's HLAPI reliable pending-buffer limit during new-player joins.
///
/// NetworkManager.OnServerConnectInternal reapplies its serialized
/// m_MaxBufferedPackets setting AFTER NetworkConnection.Initialize. Merely
/// changing the channel in Initialize is not sufficient (it can revert to 32).
/// The actual network topology also may contain more reliable channels than
/// NetworkManager.channels suggests.
///
/// -noServerNetworkBufferTuning : vanilla behavior
/// -serverMaxPendingBuffers:N  : 64..511, default 256 (512 is exclusive in HLAPI)
/// </summary>
[HarmonyPatch]
internal static class ServerNetworkBufferPatches
{
    private static readonly FieldInfo ManagerBufferLimitField =
        AccessTools.Field(typeof(NetworkManager), "m_MaxBufferedPackets");

    private static readonly FieldInfo ConnectionChannelsField =
        AccessTools.Field(typeof(NetworkConnection), "m_Channels");

    private static bool? enabled;
    private static int? pendingLimit;
    private static bool startupLogged;
    private static bool reflectionWarningLogged;
    private static int configuredConnections;

    internal static bool Enabled
    {
        get
        {
            if (!enabled.HasValue)
                enabled = LegacyServerHeadlessBootstrap.IsHeadlessServer &&
                          !LegacyCommandLine.HasArg("-noServerNetworkBufferTuning");
            return enabled.Value;
        }
    }

    internal static int PendingLimit
    {
        get
        {
            if (!pendingLimit.HasValue)
            {
                int value;
                if (!int.TryParse(LegacyCommandLine.GetArgValue("-serverMaxPendingBuffers"), out value))
                    value = 256;
                // ChannelBuffer.SetOption rejects values >= 512 in the Unity HLAPI.
                pendingLimit = Math.Max(64, Math.Min(511, value));
            }
            return pendingLimit.Value;
        }
    }

    // Number of accepted server connection events (cumulative, not active conns).
    internal static int ConfiguredConnections { get { return configuredConnections; } }

    // Called BEFORE the game's NetManager.Start calls StartServer().
    [HarmonyPatch(typeof(NetManager), "Start")]
    [HarmonyPrefix]
    private static void NetManager_Start_Prefix(NetManager __instance)
    {
        if (!Enabled || __instance == null)
            return;

        int previous;
        bool success = ConfigureManagerLimit(__instance, out previous);
        if (!startupLogged)
        {
            startupLogged = true;
            if (success)
            {
                Debug.Log("[BigCityLegacy] UNet server max buffered packets: " +
                          previous + " -> " + PendingLimit +
                          " (NetworkManager.m_MaxBufferedPackets, before StartServer). " +
                          "Rollback: -noServerNetworkBufferTuning.");
            }
            else
            {
                Debug.LogWarning("[BigCityLegacy] Unable to set NetworkManager.m_MaxBufferedPackets; " +
                                 "connection-level fallback will be used.");
            }
        }
    }

    // NetworkConnection.Initialize creates ChannelBuffer[] according to the
    // actual HostTopology, not NetworkManager.channels (which may be empty).
    [HarmonyPatch(typeof(NetworkConnection), "Initialize", new Type[] {
        typeof(string), typeof(int), typeof(int), typeof(HostTopology) })]
    [HarmonyPostfix]
    private static void NetworkConnection_Initialize_Postfix(NetworkConnection __instance,
                                                             HostTopology hostTopology)
    {
        if (!Enabled || !NetManager.isServer || __instance == null || hostTopology == null)
            return;

        try
        {
            ConfigureChannels(__instance, hostTopology.DefaultConfig.ChannelCount);
        }
        catch (Exception ex)
        {
            LogWarningOnce("UNet Initialize channel tuning failed: " + ex.Message);
        }
    }

    // This HLAPI callback reapplies m_MaxBufferedPackets before calling the
    // game's OnServerConnect. Ensure the manager's value is correct HERE too,
    // even if something else changed it since NetManager.Start().
    [HarmonyPatch(typeof(NetworkManager), "OnServerConnectInternal")]
    [HarmonyPrefix]
    private static void NetworkManager_OnServerConnectInternal_Prefix(NetworkManager __instance)
    {
        if (!Enabled || !NetManager.isServer || __instance == null)
            return;

        int previous;
        ConfigureManagerLimit(__instance, out previous);
    }

    // Called after UNet's own OnServerConnectInternal sets the channel options.
    // This also makes the fix independent of how m_MaxBufferedPackets is stored.
    [HarmonyPatch(typeof(NetManager), "OnServerConnect")]
    [HarmonyPrefix]
    private static void NetManager_OnServerConnect_Prefix(NetworkConnection conn)
    {
        if (!Enabled || !NetManager.isServer || conn == null)
            return;

        ConfigureAndReport(conn, "connect");
    }

    // Ready triggers the expensive initial spawn/state flush. Reapply just
    // before NetworkManager.OnServerReady can start it; do not report twice.
    [HarmonyPatch(typeof(NetManager), "OnServerReady")]
    [HarmonyPrefix]
    private static void NetManager_OnServerReady_Prefix(NetworkConnection conn)
    {
        if (!Enabled || !NetManager.isServer || conn == null)
            return;

        ConfigureChannels(conn, NetworkServer.numChannels);
    }

    private static bool ConfigureManagerLimit(NetworkManager manager, out int previous)
    {
        previous = -1;
        if (ManagerBufferLimitField == null)
            return false;

        try
        {
            object raw = ManagerBufferLimitField.GetValue(manager);
            if (raw is int)
                previous = (int)raw;
            ManagerBufferLimitField.SetValue(manager, PendingLimit);
            return (int)ManagerBufferLimitField.GetValue(manager) == PendingLimit;
        }
        catch (Exception ex)
        {
            LogWarningOnce("UNet m_MaxBufferedPackets reflection failed: " + ex.Message);
            return false;
        }
    }

    private static void ConfigureAndReport(NetworkConnection conn, string phase)
    {
        int channelCount = NetworkServer.numChannels;
        int configured = ConfigureChannels(conn, channelCount);
        if (configured > 0)
            configuredConnections++;

        // Read the *effective* value inside each ChannelBuffer; do not confuse
        // successful SetChannelOption calls with proof of final configuration.
        Debug.Log("[BigCityLegacy] UNet " + phase + " connection=" + conn.connectionId +
                  ", reliable channels configured=" + configured + "/" + channelCount +
                  ", requested=" + PendingLimit +
                  ", effective: " + InspectChannelBuffers(conn));

        if (configured == 0)
            LogWarningOnce("UNet did not accept MaxPendingBuffers on any server channel. " +
                           "Check actual HostTopology and Unity HLAPI variant.");
    }

    private static int ConfigureChannels(NetworkConnection conn, int channelCount)
    {
        int count = 0;
        if (channelCount < 0 || conn == null)
            return 0;

        for (int channelId = 0; channelId < channelCount; channelId++)
        {
            try
            {
                // False on unreliable channels is normal; leave them unchanged.
                if (conn.SetChannelOption(channelId, ChannelOption.MaxPendingBuffers, PendingLimit))
                    count++;
            }
            catch (Exception ex)
            {
                LogWarningOnce("UNet channel " + channelId + " tuning failed: " + ex.Message);
            }
        }
        return count;
    }

    private static string InspectChannelBuffers(NetworkConnection conn)
    {
        try
        {
            if (ConnectionChannelsField == null)
                return "unavailable (m_Channels field not found)";

            Array channels = ConnectionChannelsField.GetValue(conn) as Array;
            if (channels == null)
                return "unavailable (no channel array)";

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < channels.Length; i++)
            {
                object channel = channels.GetValue(i);
                if (channel == null)
                    continue;

                if (builder.Length > 0)
                    builder.Append("; ");

                Type type = channel.GetType();
                FieldInfo reliableField = AccessTools.Field(type, "m_IsReliable");
                FieldInfo limitField = AccessTools.Field(type, "m_MaxPendingPacketCount");
                FieldInfo queueField = AccessTools.Field(type, "m_PendingPackets");

                bool reliable = reliableField != null && (bool)reliableField.GetValue(channel);
                builder.Append("ch").Append(i).Append(reliable ? " reliable" : " unreliable");
                if (reliable)
                {
                    builder.Append(" limit=");
                    builder.Append(limitField != null ? limitField.GetValue(channel).ToString() : "?");
                    ICollection queue = queueField != null ? queueField.GetValue(channel) as ICollection : null;
                    if (queue != null)
                        builder.Append(" pending=").Append(queue.Count);
                }
            }
            return builder.Length == 0 ? "no channels" : builder.ToString();
        }
        catch (Exception ex)
        {
            return "inspection failed (" + ex.GetType().Name + ")";
        }
    }

    private static void LogWarningOnce(string message)
    {
        if (reflectionWarningLogged)
            return;
        reflectionWarningLogged = true;
        Debug.LogWarning("[BigCityLegacy] " + message);
    }
}
