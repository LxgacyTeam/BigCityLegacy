using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

internal enum LegacyServerLogLevel { Log, Warning, Error, Exception }

// No Unity calls, callbacks or retained history. Flush each complete record so a
// process crash/restart cannot discard a managed queue of the last operations.
internal sealed class LegacyServerLogWriter : IDisposable
{
    private readonly object sync = new object();
    private StreamWriter writer;
    internal string FilePath { get; private set; }
    internal LegacyServerLogLevel Minimum { get; private set; }
    internal string Failure { get; private set; }

    internal LegacyServerLogWriter(string directory, LegacyServerLogLevel minimum, int serverPort)
    {
        if (serverPort < 1 || serverPort > 65535) throw new ArgumentOutOfRangeException(nameof(serverPort));
        Directory.CreateDirectory(directory);
        Minimum = minimum;
        DateTime timestamp = DateTime.UtcNow;
        for (int attempt = 0; ; attempt++)
        {
            string path = Path.Combine(directory, serverPort.ToString(CultureInfo.InvariantCulture) + "_" + timestamp.AddMilliseconds(attempt).ToString("yyyy-MM-dd_HH-mm-ss-fff", CultureInfo.InvariantCulture) + ".log");
            FileStream stream;
            try { stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read); }
            catch (IOException) { if (attempt < 9999 && File.Exists(path)) continue; throw; }
            try { writer = new StreamWriter(stream, new UTF8Encoding(false)); }
            catch { stream.Dispose(); throw; }
            FilePath = path;
            break;
        }
        Write(LegacyServerLogLevel.Log, "Lifecycle", "Session started; timestamps=UTC; minimum=" + minimum, null, true);
    }

    internal static LegacyServerLogLevel ParseLevel(string value)
    {
        LegacyServerLogLevel level;
        if (string.IsNullOrEmpty(value)) return LegacyServerLogLevel.Log;
        if (!Enum.TryParse(value, true, out level) || !Enum.IsDefined(typeof(LegacyServerLogLevel), level) ||
            !string.Equals(value, level.ToString(), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("logLevel must be Log, Warning, Error or Exception.");
        return level;
    }

    internal bool Write(LegacyServerLogLevel level, string source, string message, string stack = null, bool always = false)
    {
        if (!always && level < Minimum) return false;
        lock (sync)
        {
            if (writer == null) return false;
            try
            {
                writer.Write(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture));
                writer.Write(" ["); writer.Write(level); writer.Write("] ["); writer.Write(source);
                writer.Write("] [thread="); writer.Write(Thread.CurrentThread.ManagedThreadId); writer.Write("] ");
                writer.WriteLine(message ?? "");
                if (!string.IsNullOrEmpty(stack)) writer.WriteLine(stack);
                writer.Flush();
                return true;
            }
            catch (Exception ex)
            {
                Failure = ex.GetType().Name + ": " + ex.Message;
                try { writer.Dispose(); } catch { }
                writer = null;
                // The caller reports once via stderr, never through Unity/BepInEx.
                return false;
            }
        }
    }
    public void Dispose()
    {
        lock (sync)
        {
            if (writer == null) return;
            try { writer.Dispose(); } catch { }
            writer = null;
        }
    }
}
