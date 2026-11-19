using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

[HarmonyPatch]
internal static class ServerReliableSendPatches
{
    private static readonly LegacyReliableSendQueue<NetworkConnection> queue = new LegacyReliableSendQueue<NetworkConnection>();
    private static readonly FieldInfo channels = AccessTools.Field(typeof(NetworkConnection), "m_Channels");
    private static readonly Type channelType = typeof(NetworkConnection).Assembly.GetType("UnityEngine.Networking.ChannelBuffer", true);
    private static readonly FieldInfo pending = AccessTools.Field(channelType, "m_PendingPackets");
    private static readonly FieldInfo limit = AccessTools.Field(channelType, "m_MaxPendingPacketCount");
    private static readonly FieldInfo packetSize = AccessTools.Field(channelType, "m_MaxPacketSize");
    private static bool flushing;

    private static bool Owns(NetworkConnection conn)
    { return NetworkServer.active && conn != null && NetworkServer.connections.Contains(conn); }

    [HarmonyPatch(typeof(NetworkConnection), "SendBytes")]
    [HarmonyPrefix]
    private static bool Bytes(NetworkConnection __instance, byte[] bytes, int numBytes, int channelId, ref bool __result)
    {
        if (flushing || channelId != 0 || !Owns(__instance)) return true;
        __result = Enqueue(__instance, bytes, numBytes);
        return false;
    }

    [HarmonyPatch(typeof(NetworkConnection), "SendWriter")]
    [HarmonyPrefix]
    private static bool Writer(NetworkConnection __instance, NetworkWriter writer, int channelId, ref bool __result)
    {
        if (flushing || channelId != 0 || !Owns(__instance)) return true;
        byte[] bytes = writer.ToArray();
        __result = Enqueue(__instance, bytes, bytes.Length);
        return false;
    }
    private static bool Enqueue(NetworkConnection conn, byte[] bytes, int count)
    {
        if (queue.Enqueue(conn, bytes, count)) return true;
        Debug.LogWarning("[BCL network] Reliable send queue capacity or message size exceeded; closing connection " + conn.connectionId + ".");
        queue.Remove(conn); conn.Disconnect();
        return false;
    }
    private static bool HasRoom(NetworkConnection conn, int bytes)
    {
        var array = channels.GetValue(conn) as Array;
        if (array == null || array.Length == 0) return false;
        object channel = array.GetValue(0);
        if (channel == null) return false;
        int maximum = (int)limit.GetValue(channel);
        if (maximum < 128 && conn.SetChannelOption(0, ChannelOption.MaxPendingBuffers, 128)) maximum = (int)limit.GetValue(channel);
        int used = ((ICollection)pending.GetValue(channel)).Count;
        int fragment = Math.Max(1, (int)packetSize.GetValue(channel) - 32);
        int required = (bytes + fragment - 1) / fragment + 2; // Includes current packet and final fragment marker.
        return used + required < maximum;
    }
    [HarmonyPatch(typeof(NetManager), "Update")]
    [HarmonyPostfix]
    private static void Flush()
    {
        if (!NetworkServer.active) { queue.Clear(); return; }
        flushing = true;
        try { queue.Flush(Owns, HasRoom, (conn, bytes) => conn.SendBytes(bytes, bytes.Length, 0)); }
        finally { flushing = false; }
    }
    [HarmonyPatch(typeof(NetManager), "OnServerDisconnect")]
    [HarmonyPrefix]
    private static void Disconnect(NetworkConnection conn) { if (conn != null) queue.Remove(conn); }

    [HarmonyPatch(typeof(NetManager), "OnStartServer")]
    [HarmonyPatch(typeof(NetManager), "OnStopServer")]
    [HarmonyPostfix]
    private static void Reset() { queue.Clear(); }
}
