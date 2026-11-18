using System;
using System.Diagnostics;
using System.IO;
using BepInEx;

internal static class LegacyBootstrapTrace
{
    private const string FileName = "BCL.Bootstrap.log";
    private static readonly object Gate = new object();

    [Conditional("BCL_BOOTSTRAP_DIAGNOSTICS")]
    internal static void Mark(string step, string details = null)
    {
        string line = DateTime.UtcNow.ToString("O") + " [BCL bootstrap] " + step +
            (details == null ? "" : " " + details) + Environment.NewLine;
        lock (Gate)
        {
            try
            {
                if (!string.IsNullOrEmpty(Paths.BepInExRootPath))
                {
                    File.AppendAllText(Path.Combine(Paths.BepInExRootPath, FileName), line);
                    return;
                }
            }
            catch { }

            try { File.AppendAllText(Path.Combine(Path.GetTempPath(), FileName), line); }
            catch { }
        }
    }

    [Conditional("BCL_BOOTSTRAP_DIAGNOSTICS")]
    internal static void Failure(string step, Exception error)
    {
        Mark(step, error.ToString());
    }
}
