using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;
using Logger = BepInEx.Logging.Logger;

// A BepInEx 5 preloader extension that observes startup without modifying assemblies.
public static class BootstrapProbe
{
    private static readonly HashSet<string> Observed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "BigCityLegacy", "LegacyUIFramework", "LegacyUIFramework.Blur", "0Harmony",
        "Assembly-CSharp", "UnityEngine.CoreModule"
    };

    public static IEnumerable<string> TargetDLLs => Array.Empty<string>();

    public static void Patch(AssemblyDefinition assembly) { }

    public static void Initialize()
    {
        LegacyBootstrapTrace.Mark("preloader.enter", "platform=" + Environment.OSVersion.Platform +
            " bits=" + (IntPtr.Size * 8) + " clr=" + Environment.Version +
            " bepinex=" + typeof(Paths).Assembly.GetName().Version);
        Logger.Listeners.Add(new StartupListener());
        AppDomain.CurrentDomain.AssemblyLoad += OnAssemblyLoad;
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            RecordLoadedAssembly(assembly, "assembly.already-loaded");

        try
        {
            InspectDirectory(Paths.PluginPath, SearchOption.AllDirectories);
            InspectDirectory(Paths.ManagedPath, SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex)
        {
            LegacyBootstrapTrace.Failure("preloader.inventory-failed", ex);
        }
    }

    public static void Finish() => LegacyBootstrapTrace.Mark("preloader.finish");

    private static void InspectDirectory(string directory, SearchOption search)
    {
        if (!Directory.Exists(directory))
        {
            LegacyBootstrapTrace.Mark("preloader.directory-missing", directory);
            return;
        }
        foreach (string file in Directory.EnumerateFiles(directory, "*.dll", search))
        {
            if (!Observed.Contains(Path.GetFileNameWithoutExtension(file))) continue;
            string relativePath = file.Substring(directory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            try
            {
                using (var assembly = AssemblyDefinition.ReadAssembly(file))
                    LegacyBootstrapTrace.Mark("preloader.dll", relativePath + " identity=" + assembly.Name.FullName +
                        " mvid=" + assembly.MainModule.Mvid);
            }
            catch (Exception ex)
            {
                LegacyBootstrapTrace.Failure("preloader.dll-unreadable " + relativePath, ex);
            }
        }
    }

    private static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args) =>
        RecordLoadedAssembly(args.LoadedAssembly, "assembly.loaded");

    private static void RecordLoadedAssembly(Assembly assembly, string stage)
    {
        try
        {
            if (Observed.Contains(assembly.GetName().Name))
                LegacyBootstrapTrace.Mark(stage, assembly.FullName + " mvid=" + assembly.ManifestModule.ModuleVersionId);
        }
        catch { }
    }

    private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
    {
        if (Observed.Contains(args.Name.Split(',')[0]))
            LegacyBootstrapTrace.Mark("assembly.resolve", args.Name + " requestedBy=" + args.RequestingAssembly?.FullName);
        return null;
    }

    private sealed class StartupListener : ILogListener
    {
        private volatile bool finished;

        public void LogEvent(object sender, LogEventArgs args)
        {
            if (finished || args.Source.SourceName != "BepInEx") return;
            try
            {
                string message = args.Data?.ToString() ?? "";
                LegacyBootstrapTrace.Mark("loader." + args.Level, message);
                if (message == "Chainloader startup complete")
                {
                    finished = true;
                    AppDomain.CurrentDomain.AssemblyLoad -= OnAssemblyLoad;
                    AppDomain.CurrentDomain.AssemblyResolve -= OnAssemblyResolve;
                }
            }
            catch { }
        }

        public void Dispose() { }
    }
}
