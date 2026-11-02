using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using BepInEx.Logging;
using UnityEngine;
using BepLogger = BepInEx.Logging.Logger;

internal static class LegacyServerLog
{
    private sealed class Pending
    {
        internal LegacyServerLogLevel Level;
        internal string Source, Text, Stack;
    }
    private static readonly object sync = new object();
    private static readonly Queue<Pending> pending = new Queue<Pending>();
    private static LegacyServerLogWriter writer;
    private static Listener listener;
    private static bool configured, subscribed, failureReported;
    private static int pendingCharacters, dropped;
    [ThreadStatic] private static bool inLog;
    internal static bool IsServer { get; private set; }
    internal static bool DiagnosticsEnabled { get; private set; }
    internal static bool NativeMLogEnabled { get; private set; }
    internal static string CurrentPath { get { return writer?.FilePath; } }

    internal static void Begin(string[] raw)
    {
        NativeMLogEnabled = raw.Any(a => a.Equals("-mlog", StringComparison.OrdinalIgnoreCase));
        IsServer = raw.Any(a => a.Equals("-bend_GameServer", StringComparison.OrdinalIgnoreCase));
        if (!IsServer || subscribed) return;
        // These handlers never query LegacyCommandLine/ServerConfig: config loading
        // itself logs messages and querying it here would recursively reload it.
        Application.logMessageReceivedThreaded += UnityLog;
        listener = new Listener(); BepLogger.Listeners.Add(listener);
        subscribed = true;
    }
    internal static void Configure(string[] args, bool startupFailed = false)
    {
        if (!IsServer || configured) return;
        bool disabled = args.Any(a => a.Equals("-noLogging", StringComparison.OrdinalIgnoreCase));
        var level = startupFailed ? LegacyServerLogLevel.Log : LegacyServerLogWriter.ParseLevel(Value(args, "-logLevel"));
        DiagnosticsEnabled = !disabled && LegacyHelpers.isDebugBuild && args.Any(a => a.Equals("-serverDiagnostics", StringComparison.OrdinalIgnoreCase));
        lock (sync)
        {
            if (!disabled)
            {
                try
                {
                    string directory = Value(args, "-logsDir");
                    if (string.IsNullOrWhiteSpace(directory)) directory = Path.Combine(LegacyHelpers.ModDataPath, "logs");
                    else if (!Path.IsPathRooted(directory)) directory = Path.Combine(BepInEx.Paths.GameRootPath, directory);
                    int port;
                    if (!int.TryParse(Value(args, "-port"), NumberStyles.Integer, CultureInfo.InvariantCulture, out port) || port < 1 || port > 65535)
                        port = 7800;
                    writer = new LegacyServerLogWriter(Path.GetFullPath(directory), level, port);
                    using (var process = Process.GetCurrentProcess())
                        writer.Write(LegacyServerLogLevel.Log, "Lifecycle", "BCL " + VersionInfo.ModVersionName + "; pid=" + process.Id +
                            "; port=" + port + "; serverConfig=" + LegacyServerConfig.LoadedPath + "; diagnostics=" + DiagnosticsEnabled + "; mlog=" + NativeMLogEnabled, null, true);
                }
                catch (Exception ex) { ReportFailure(ex.GetType().Name + ": " + ex.Message); }
            }
            if (!startupFailed && args.Any(a => a.Equals("-chatLogging", StringComparison.OrdinalIgnoreCase)))
            {
                string directory = Value(args, "-logsDir");
                if (string.IsNullOrWhiteSpace(directory)) directory = Path.Combine(LegacyHelpers.ModDataPath, "logs");
                else if (!Path.IsPathRooted(directory)) directory = Path.Combine(BepInEx.Paths.GameRootPath, directory);
                int port; if (!int.TryParse(Value(args, "-port"), out port) || port < 1 || port > 65535) port = 7800;
                LegacyChatLog.Initialize(directory, port);
            }
            configured = true;
            while (pending.Count != 0)
            {
                var entry = pending.Dequeue();
                WriteCore(entry.Level, entry.Source, entry.Text, entry.Stack);
            }
            pendingCharacters = 0;
            if (dropped != 0) WriteCore(LegacyServerLogLevel.Warning, "Logger", "Early log buffer limit reached; omitted entries=" + dropped, null);
        }
        if (disabled) Unsubscribe();
    }
    private static string Value(string[] args, string key)
    {
        string prefix = key + ":";
        string arg = args.FirstOrDefault(a => a.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return arg == null ? null : LegacyCommandLine.StripQuotes(arg.Substring(prefix.Length));
    }
    private static void UnityLog(string text, string stack, LogType type)
    {
        Record(type == LogType.Exception ? LegacyServerLogLevel.Exception :
            type == LogType.Error || type == LogType.Assert ? LegacyServerLogLevel.Error :
            type == LogType.Warning ? LegacyServerLogLevel.Warning : LegacyServerLogLevel.Log, "Unity", text, stack);
    }
    internal static void Record(LegacyServerLogLevel level, string source, string text, string stack = null)
    {
        if (!IsServer || inLog) return;
        inLog = true;
        try
        {
            lock (sync)
            {
                if (!configured)
                {
                    int size = (text?.Length ?? 0) + (stack?.Length ?? 0);
                    if (pending.Count >= 256 || size > 1024 * 1024 - pendingCharacters) { dropped++; return; }
                    pending.Enqueue(new Pending { Level = level, Source = source, Text = text, Stack = stack });
                    pendingCharacters += size;
                }
                else WriteCore(level, source, text, stack);
            }
        }
        finally { inLog = false; }
    }
    private static void WriteCore(LegacyServerLogLevel level, string source, string text, string stack)
    {
        if (writer == null) return;
        writer.Write(level, source, text, stack);
        if (writer.Failure != null) ReportFailure(writer.Failure);
    }
    private static void ReportFailure(string reason)
    {
        if (failureReported) return;
        failureReported = true;
        try { Console.Error.WriteLine("[BCL Logger] File logging unavailable: " + reason); } catch { }
    }
    private sealed class Listener : ILogListener
    {
        public void LogEvent(object sender, LogEventArgs e)
        {
            if (e == null || inLog) return;
            string source = e.Source?.SourceName ?? "BepInEx";
            if (source.Equals("Unity Log", StringComparison.OrdinalIgnoreCase) || source.Equals("Unity", StringComparison.OrdinalIgnoreCase)) return;
            if ((e.Level & (LogLevel.Debug | LogLevel.Info | LogLevel.Message | LogLevel.Warning | LogLevel.Error | LogLevel.Fatal)) == 0) return;
            // Serialize outside Record with the reentrancy guard already raised.
            inLog = true;
            try
            {
                var level = e.Data is Exception ? LegacyServerLogLevel.Exception :
                    (e.Level & (LogLevel.Fatal | LogLevel.Error)) != 0 ? LegacyServerLogLevel.Error :
                    (e.Level & LogLevel.Warning) != 0 ? LegacyServerLogLevel.Warning : LegacyServerLogLevel.Log;
                string text = e.Data?.ToString() ?? "";
                inLog = false;
                Record(level, source, text);
            }
            catch { }
            finally { inLog = false; }
        }
        public void Dispose() { }
    }
    private static void Unsubscribe()
    {
        if (!subscribed) return;
        Application.logMessageReceivedThreaded -= UnityLog;
        BepLogger.Listeners.Remove(listener); listener.Dispose(); listener = null;
        subscribed = false;
    }
    internal static void Shutdown()
    {
        LegacyChatLog.Shutdown();
        Unsubscribe();
        lock (sync)
        {
            writer?.Write(LegacyServerLogLevel.Log, "Lifecycle", "Session ended normally.", null, true);
            writer?.Dispose(); writer = null;
            pending.Clear(); configured = true;
        }
    }
}
