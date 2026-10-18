using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

// Managed trace context only. No Unity callbacks or network operations.
internal sealed class LegacyCsTraceSession : IDisposable
{
    internal sealed class Scope : IDisposable
    {
        internal LegacyCsTraceSession Session;
        internal Scope Previous;
        internal Trace Trace;
        internal string Stage;
        internal bool Ended;
        public void Dispose() { Session.End(this, null); }
    }
    internal sealed class Trace
    {
        internal long Id;
        internal string Wire;
        internal bool Capture;
        internal int Simulations;
    }
    [ThreadStatic] private static Scope current;
    private readonly LegacyServerLogWriter writer;
    private readonly Func<long> milliseconds;
    private readonly int maxShots;
    private long sequence, window;
    private int shots, events, omittedShots, omittedEvents;
    private bool disposed;
    internal bool Active { get { return !disposed && writer.Failure == null; } }
    internal bool Capturing { get { return Active && (current == null || current.Session != this || current.Trace.Capture); } }
    internal bool InShot { get { return Capturing && current != null && current.Session == this; } }
    internal string FilePath { get { return writer.FilePath; } }

    internal LegacyCsTraceSession(LegacyServerLogWriter writer, int maxShots, Func<long> milliseconds = null)
    {
        if (maxShots < 1 || maxShots > 1000) throw new ArgumentOutOfRangeException(nameof(maxShots));
        this.writer = writer; this.maxShots = maxShots;
        var timer = Stopwatch.StartNew();
        this.milliseconds = milliseconds ?? (() => timer.ElapsedMilliseconds);
        window = this.milliseconds() / 1000;
    }

    internal Scope Begin(string wire, string stage, bool simulation)
    {
        Tick();
        var previous = current;
        Trace trace;
        if (previous != null && previous.Session == this && previous.Trace.Wire == wire) trace = previous.Trace;
        else
        {
            bool capture = Active && shots++ < maxShots;
            if (!capture) omittedShots++;
            trace = new Trace { Id = Interlocked.Increment(ref sequence), Wire = wire, Capture = capture };
        }
        if (simulation) trace.Simulations++;
        var scope = new Scope { Session = this, Previous = previous, Trace = trace, Stage = stage };
        current = scope;
        Record(stage + "_BEGIN", "");
        return scope;
    }

    internal void End(Scope scope, Exception exception)
    {
        if (scope == null || scope.Ended) return;
        try {
            if (exception != null && scope.Trace.Capture)
                writer.Write(LegacyServerLogLevel.Exception, "CS", "event=SHOT_EXCEPTION shot=" + scope.Trace.Id + " wire=" + scope.Trace.Wire, exception.ToString());
            Record(scope.Stage + "_END", "simulations=" + scope.Trace.Simulations +
            (exception == null ? "" : " exception=" + exception.GetType().Name + " message=" + Escape(exception.Message))); }
        finally { scope.Ended = true; current = scope.Previous; }
    }

    internal void Record(string kind, string details, string wire = null)
    {
        if (!Capturing) return;
        Tick();
        Scope scope = current != null && current.Session == this ? current : null;
        // Whole shots are selected at their root. Never drop individual events
        // inside a selected trace. Bound unrelated state/snapshot events too.
        if (scope == null && events++ >= 600) { omittedEvents++; return; }
        writer.Write(LegacyServerLogLevel.Log, "CS", "event=" + kind + " seq=" + Interlocked.Increment(ref sequence) +
            " shot=" + (scope == null ? "0" : scope.Trace.Id.ToString(CultureInfo.InvariantCulture)) +
            " wire=" + (scope == null ? wire ?? "-" : scope.Trace.Wire) + " " + details);
    }

    internal void Tick()
    {
        long next = milliseconds() / 1000;
        if (next == window) return;
        if (omittedShots != 0 || omittedEvents != 0)
            writer.Write(LegacyServerLogLevel.Warning, "CS", "event=TRACE_LIMIT omittedShots=" + omittedShots +
                " omittedStandaloneEvents=" + omittedEvents + "; missing traces in this window cannot establish packet loss");
        window = next; shots = events = omittedShots = omittedEvents = 0;
    }

    internal static string Fingerprint(float[] values, int weapon)
    {
        ulong hash = 14695981039346656037UL;
        foreach (float value in values)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            for (int i = 0; i < 4; i++) hash = unchecked((hash ^ bytes[BitConverter.IsLittleEndian ? i : 3 - i]) * 1099511628211UL);
        }
        for (int i = 0; i < 4; i++) hash = unchecked((hash ^ (byte)(weapon >> (8 * i))) * 1099511628211UL);
        return hash.ToString("x16", CultureInfo.InvariantCulture);
    }

    internal static string Escape(string text)
    {
        if (text == null) return "null";
        text = text.Replace("\\", "\\\\").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\"", "\\\"");
        return "\"" + (text.Length > 256 ? text.Substring(0, 256) : text) + "\"";
    }

    public void Dispose()
    {
        if (disposed) return;
        Tick();
        writer.Write(LegacyServerLogLevel.Log, "CS", "event=SESSION_END omittedShots=" + omittedShots + " omittedStandaloneEvents=" + omittedEvents, null, true);
        disposed = true;
        if (current != null && current.Session == this) current = null;
        writer.Dispose();
    }
}
