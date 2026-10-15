using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine.Networking;

internal static class LegacyServerDiagnostics
{
    internal sealed class Operation
    {
        internal long Id, Parent;
        internal string Name;
        internal int Depth;
        internal Operation Previous;
    }
    [ThreadStatic] private static Operation current;
    private static long sequence, nextHeartbeat;
    internal static Operation Enter(MethodBase method, object instance, object[] args)
    {
        if (!LegacyServerLog.DiagnosticsEnabled) return null;
        var operation = new Operation { Id = Interlocked.Increment(ref sequence), Parent = current?.Id ?? 0,
            Depth = (current?.Depth ?? 0) + 1, Previous = current, Name = method.DeclaringType.Name + "." + method.Name };
        var detail = new StringBuilder();
        var connection = instance as NetworkConnection;
        if (connection != null) detail.Append(" conn=").Append(connection.connectionId);
        if (instance is NetworkBehaviour behaviour) detail.Append(" netId=").Append(behaviour.netId.Value);
        foreach (object arg in args)
        {
            if (arg is string text) detail.Append(" stringChars=").Append(text.Length);
            else if (arg is byte[] bytes) detail.Append(" bufferBytes=").Append(bytes.Length);
            else if (arg is NetworkReader reader) detail.Append(" readerPosition=").Append(reader.Position).Append(" readerBytes=").Append(reader.Length);
            else if (arg is NetworkMessage message)
            {
                detail.Append(" msgType=").Append(message.msgType).Append(" channel=").Append(message.channelId);
                if (message.reader != null) detail.Append(" readerBytes=").Append(message.reader.Length).Append(" readerPosition=").Append(message.reader.Position);
            }
            else if (arg is int || arg is short || arg is bool) detail.Append(" value=").Append(arg);
        }
        current = operation;
        LegacyServerLog.Record(LegacyServerLogLevel.Log, "Diagnostics", "ENTER id=" + operation.Id + " parent=" + operation.Parent +
            " depth=" + operation.Depth + " " + operation.Name + detail);
        if (operation.Depth == 32 || operation.Depth == 64)
            LegacyServerLog.Record(LegacyServerLogLevel.Warning, "Diagnostics", "Deep nested operation id=" + operation.Id,
                new StackTrace(1, true).ToString());
        return operation;
    }
    internal static void ResetContext() { current = null; }
    internal static void Exit(Operation operation, Exception exception)
    {
        if (operation == null) return;
        LegacyServerLog.Record(exception == null ? LegacyServerLogLevel.Log : LegacyServerLogLevel.Exception, "Diagnostics",
            (exception == null ? "EXIT" : "THROW") + " id=" + operation.Id + " depth=" + operation.Depth + " " + operation.Name,
            exception?.ToString());
        current = operation.Previous;
    }
    internal static void Tick()
    {
        if (!LegacyServerLog.DiagnosticsEnabled || Stopwatch.GetTimestamp() < nextHeartbeat) return;
        nextHeartbeat = Stopwatch.GetTimestamp() + 30 * Stopwatch.Frequency;
        string memory = "";
        try { using (var process = Process.GetCurrentProcess()) memory = "; workingSetBytes=" + process.WorkingSet64 + "; threads=" + process.Threads.Count; }
        catch { }
        LegacyServerLog.Record(LegacyServerLogLevel.Log, "Diagnostics", "Heartbeat: players=" + NetManager.curCount +
            "; managedBytes=" + GC.GetTotalMemory(false) + "; gc=" + GC.CollectionCount(0) + "/" + GC.CollectionCount(1) + "/" + GC.CollectionCount(2) + memory);
    }
}
