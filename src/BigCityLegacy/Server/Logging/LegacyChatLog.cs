using System;
using UnityEngine;

internal static class LegacyChatLog
{
    private static LegacyServerLogWriter writer;
    private static bool failureReported;
    internal static string CurrentPath { get { return writer == null ? null : writer.FilePath; } }
    internal static void Initialize(string directory, int port)
    {
        try { writer = new LegacyServerLogWriter(directory, LegacyServerLogLevel.Log, "chatlog_" + port); }
        catch (Exception ex) { Report(ex.Message); }
    }
    internal static string Escape(string text)
    { return (text ?? "").Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\0", "\\0"); }
    internal static void Record(NetChat.ChatMessageStruct message)
    {
        if (writer == null || !NetManager.isServer) return;
        NetInputControl input = message.netInput ? message.netInput.GetComponent<NetInputControl>() : null;
        string guid = input ? input.pirvateConnectID : "server";
        writer.Write(LegacyServerLogLevel.Log, "Chat", "nickname=" + Escape(message.nikName) + " guid=" + Escape(guid) + " | " + Escape(message.message));
        if (writer.Failure != null) Report(writer.Failure);
    }
    private static void Report(string error)
    { if (failureReported) return; failureReported = true; try { Console.Error.WriteLine("[BCL ChatLog] " + error); } catch { } }
    internal static void Shutdown() { writer?.Dispose(); writer = null; }
}
