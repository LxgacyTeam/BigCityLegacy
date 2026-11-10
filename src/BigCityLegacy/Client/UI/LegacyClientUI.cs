using BigCityLegacy.UI;
using BigCityLegacy.UI.Blur;
using UnityEngine;
using BepInEx.Configuration;
using BepInEx.Logging;
using System.Collections.Generic;

internal static class LegacyClientUI
{
    internal static readonly LegacyUIAnimationSettings Animations = new LegacyUIAnimationSettings();
    private static LegacyBclBlur backdrop;
    private static ConfigEntry<bool> diagnostics;
    private static ManualLogSource log;
    private static readonly HashSet<long> reportedScopes = new HashSet<long>();
    private static bool DiagnosticsEnabled { get { return diagnostics != null && diagnostics.Value; } }

    internal static void ConfigureDiagnostics(ConfigFile config, ManualLogSource logger)
    {
        log = logger;
        if (LegacyCommandLine.HasServerArg()) return;
        diagnostics = config.Bind("UI", "BlurDiagnostics", true,
            "Log backdrop-blur initialization, window requests, camera/shader/capture results and a throttled health summary. Disable after diagnosis.");
        if (DiagnosticsEnabled)
            Write("[BCL Blur] stage=bootstrap game=" + Application.version + " mode=" + LegacyCompatibility.Mode +
                " BCL=" + VersionInfo.ModVersionName + " build=" + VersionInfo.BuildTimestamp +
                " framework=" + typeof(LegacyUI).Assembly.GetName().Version + " path=" + AssemblyPath(typeof(LegacyUI)) +
                " blur=" + typeof(LegacyBclBlur).Assembly.GetName().Version + " path=" + AssemblyPath(typeof(LegacyBclBlur)));
    }
    private static string AssemblyPath(System.Type type)
    { try { return type.Assembly.Location; } catch { return "<unavailable>"; } }
    private static void Write(string message) { if (log != null) log.LogInfo(message); else Debug.Log(message); }

    internal static void Initialize(GameObject host)
    {
        if (!backdrop) backdrop = LegacyBclBlur.Create(host, Write, DiagnosticsEnabled);
        if (DiagnosticsEnabled)
            Write("[BCL Blur] stage=initialize provider=" + (backdrop ? backdrop.GetInstanceID().ToString() : "<none>"));
    }
    internal static void Panel(Rect rect, float alpha)
    {
        bool blurred = backdrop && backdrop.Draw(rect);
        if (backdrop)
            backdrop.ReportWindow(new LegacyUIBackdropDiagnosticInfo
            {
                Title = "BCL Performance Monitor", Rect = rect,
                EntryEvent = Event.current != null ? Event.current.type : EventType.Ignore,
                CurrentEvent = Event.current != null ? Event.current.type : EventType.Ignore,
                RawEvent = Event.current != null ? Event.current.rawType : EventType.Ignore,
                Requested = true, Drawn = blurred, BackgroundAlpha = blurred ? alpha * 0.5f : alpha,
                TintMultiplier = 0.5f, Opacity = GUI.color.a
            });
        LegacyUI.Panel(rect, blurred ? alpha * 0.5f : alpha);
    }
    internal static void Shutdown() { if (backdrop) Object.Destroy(backdrop); backdrop = null; }
    internal static LegacyUIGuiScope Scope(int depth, bool useBlur = true)
    {
        if (backdrop) backdrop.DiagnosticsEnabled = DiagnosticsEnabled;
        long scope = ((long)depth << 1) | (useBlur ? 1L : 0L);
        if (DiagnosticsEnabled && reportedScopes.Add(scope))
            Write("[BCL Blur] stage=scope depth=" + depth + " requested=" + useBlur +
                " provider=" + (backdrop ? backdrop.GetInstanceID().ToString() : "<none>") +
                " event=" + (Event.current != null ? Event.current.type.ToString() : "<none>") +
                " raw=" + (Event.current != null ? Event.current.rawType.ToString() : "<none>"));
        return new LegacyUIGuiScope(depth, null, Animations, useBlur ? backdrop : null);
    }
}
