using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using BigCityLegacy.UI;
using Smooth;
using UnityEngine;
using DiagnosticsProcess = System.Diagnostics.Process;

/// <summary>
/// Lightweight process/Unity performance monitor shared by the client -perfmon
/// overlay and the dedicated-server "stats" command.
///
/// The sampler deliberately runs at a low rate (1 Hz). Per-frame work is limited
/// to a few float additions used for frame-time statistics.
/// </summary>
internal sealed class LegacyPerformanceMonitor : MonoBehaviour
{
    internal sealed class Snapshot
    {
        internal DateTime TimestampUtc;

        internal float FramesPerSecond;
        internal float AverageFrameMs;
        internal float WorstFrameMs;

        // CPU percent is normalized to the capacity of the whole machine/VM.
        // CpuCoresUsed is easier to compare with Linux tools that show 100% per core.
        internal double CpuPercent;
        internal double CpuCoresUsed;
        internal int ProcessorCount;
        internal int ThreadCount;

        internal long WorkingSetBytes;
        internal long PeakWorkingSetBytes;
        internal long PrivateBytes;
        internal long ManagedBytes;
        internal long LinuxPssBytes;
        internal long LinuxPrivateBytes;

        internal long UnityAllocatedBytes;
        internal long UnityReservedBytes;
        internal long UnityUnusedReservedBytes;
        internal long UnityMonoUsedBytes;
        internal long UnityMonoHeapBytes;
        internal long GraphicsDriverAllocatedBytes;

        internal long TextureMemoryBytes;
        internal long NonStreamingTextureMemoryBytes;
        internal int StreamingTextureCount;
        internal int NonStreamingTextureCount;

        internal int GcGen0;
        internal int GcGen1;
        internal int GcGen2;
        internal int GcGen0Delta;
        internal int GcGen1Delta;
        internal int GcGen2Delta;

        internal int Players;
        internal int Controls;
        internal int NetControls;
        internal int SmoothSyncs;
        internal int Inputs;
        internal int Loot;
        internal int GroundCells;
        internal int Cars;
        internal int CarsAwake;
        internal int CarsSleeping;
        internal int CarsHandled;
        internal int CarsClientOwned;
        internal int CarsServerOwned;
        internal int CarsSleepFastPath;

        internal string GpuName;
        internal string GpuApi;
        internal int GpuMemoryMb;
    }

    private const float SampleInterval = 1f;
    private const float OverlayWidth = 350f;
    private const float OverlayPadding = 9f;
    private const float OverlayRowHeight = 18f;

    private static LegacyPerformanceMonitor instance;
    private static Snapshot current = new Snapshot();
    private static bool overlayEnabled;
    private static bool overlayHotkeyEnabled;

    private static DiagnosticsProcess process;
    private static TimeSpan lastProcessCpuTime;
    private static DateTime lastCpuWallUtc;
    private static bool hasCpuBaseline;
    private static bool hasGcBaseline;
    private static int lastGcGen0;
    private static int lastGcGen1;
    private static int lastGcGen2;

    private static Type profilerType;
    private static MethodInfo profilerTotalAllocated;
    private static MethodInfo profilerTotalReserved;
    private static MethodInfo profilerTotalUnusedReserved;
    private static MethodInfo profilerMonoUsed;
    private static MethodInfo profilerMonoHeap;
    private static MethodInfo profilerGraphicsDriverAllocated;

    private static FieldInfo lootItemsField;
    private static FieldInfo groundItemsMapField;

    private static LegacyUITheme overlayHintTheme;

    private string fpsLine = string.Empty;
    private string cpuLine = string.Empty;
    private string memLine = string.Empty;
    private string unityMemLine = string.Empty;
    private string gcLine = string.Empty;
    private string gpuLine = string.Empty;
    private string texLine = string.Empty;
    private string objLine = string.Empty;

    private const uint ToolhelpSnapThread = 0x00000004;
    private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessMemoryCountersEx
    {
        internal uint cb;
        internal uint PageFaultCount;
        internal UIntPtr PeakWorkingSetSize;
        internal UIntPtr WorkingSetSize;
        internal UIntPtr QuotaPeakPagedPoolUsage;
        internal UIntPtr QuotaPagedPoolUsage;
        internal UIntPtr QuotaPeakNonPagedPoolUsage;
        internal UIntPtr QuotaNonPagedPoolUsage;
        internal UIntPtr PagefileUsage;
        internal UIntPtr PeakPagefileUsage;
        internal UIntPtr PrivateUsage;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ThreadEntry32
    {
        internal uint dwSize;
        internal uint cntUsage;
        internal uint th32ThreadID;
        internal uint th32OwnerProcessID;
        internal int tpBasePri;
        internal int tpDeltaPri;
        internal uint dwFlags;
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentProcessId();

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessMemoryInfo(
        IntPtr hProcess,
        out ProcessMemoryCountersEx counters,
        uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Thread32First(IntPtr snapshot, ref ThreadEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Thread32Next(IntPtr snapshot, ref ThreadEntry32 entry);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    private float nextSampleTime;
    private float frameTimeSum;
    private float worstFrameTime;
    private int sampledFrames;
    private float sampleWindowStart;

    internal static Snapshot Current
    {
        get { return current; }
    }

    internal static void Ensure(GameObject host)
    {
        if (instance != null || host == null)
        {
            return;
        }

        bool wanted = LegacyCommandLine.HasServerArg() || LegacyCommandLine.HasArg("-perfmon");
        if (!wanted)
        {
            return;
        }

        overlayHotkeyEnabled = !LegacyCommandLine.HasServerArg() && LegacyCommandLine.HasArg("-perfmon");
        overlayEnabled = overlayHotkeyEnabled;
        instance = host.AddComponent<LegacyPerformanceMonitor>();
    }

    private void Awake()
    {
        InitializeProcessSampler();
        InitializeProfilerSampler();
        InitializeGameCounterReflection();

        if (overlayHotkeyEnabled)
        {
            InitializeOverlayTheme();
        }

        sampleWindowStart = Time.realtimeSinceStartup;
        nextSampleTime = sampleWindowStart + SampleInterval;
        SampleNow(true);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt >= 0f && dt < 10f)
        {
            frameTimeSum += dt;
            sampledFrames++;
            if (dt > worstFrameTime)
            {
                worstFrameTime = dt;
            }

            if (overlayHotkeyEnabled && Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.R))
            {
                overlayEnabled = !overlayEnabled;
            }
        }

        float now = Time.realtimeSinceStartup;
        if (now >= nextSampleTime)
        {
            SampleNow(false);
            nextSampleTime = now + SampleInterval;
        }
    }

    private void OnGUI()
    {
        if (!overlayEnabled)
        {
            return;
        }

        Snapshot s = current;
        if (s == null)
        {
            return;
        }

        float height = 8f + OverlayRowHeight * 8f + 8f + 5f;
        Rect panel = new Rect(12f, 12f, OverlayWidth, height);

        if (overlayHintTheme == null)
        {
            InitializeOverlayTheme();
        }

        using (new LegacyUIGuiScope(-15000))
        {
            LegacyUI.Panel(panel, 0.65f);

            float x = panel.x + OverlayPadding;
            float y = panel.y + OverlayPadding;
            float btnW = 60f;
            float btnX = OverlayWidth - btnW + 5f;
            float width = panel.width - OverlayPadding * 2f;

            LegacyUI.Label(new Rect(x, y, width, OverlayRowHeight), "<b>BCL Performance Monitor</b> [Ctrl+R]");

            if (LegacyUI.Button(new Rect(btnX, y, btnW, OverlayRowHeight + 5f), "Save"))
            {
                try
                {
                    float curtime = Time.time;
                    string savetext = $"Perfmon state after {curtime}s:\n\n\n{fpsLine}\n{cpuLine}\n{memLine}\n{unityMemLine}\n{gcLine}\n{gpuLine}\n{texLine}\n{objLine}";
                    string path = Path.Combine(LegacyHelpers.ModDataPath, "perfmon_" + LegacyHelpers.GetCurTimestamp + ".log");
                    File.WriteAllText(path, savetext);
                    if (GameUI.me)
                    {
                        GameUI.me.ShowPopup($"Perfmon state after {Math.Round(curtime, 3)}s saved", 1f, true);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[BigCityLegacy] Failed to save perfmon state to file: {e.Message}");
                }
            }
            y += OverlayRowHeight + 5f;

            using (LegacyUI.WithTheme(overlayHintTheme))
            {
                LegacyUI.MiniHint(new Rect(x, y, width, OverlayRowHeight), fpsLine);
                y += OverlayRowHeight;

                LegacyUI.MiniHint(new Rect(x, y, width, OverlayRowHeight), cpuLine);
                y += OverlayRowHeight;

                LegacyUI.MiniHint(new Rect(x, y, width, OverlayRowHeight), memLine);
                y += OverlayRowHeight;

                LegacyUI.MiniHint(new Rect(x, y, width, OverlayRowHeight), unityMemLine);
                y += OverlayRowHeight;

                //LegacyUI.MiniHint(new Rect(x, y, width, OverlayRowHeight), gcLine);
                //y += OverlayRowHeight;

                LegacyUI.MiniHint(new Rect(x, y, width, OverlayRowHeight), gpuLine);
                y += OverlayRowHeight;

                LegacyUI.MiniHint(new Rect(x, y, width, OverlayRowHeight), texLine);
                y += OverlayRowHeight;

                LegacyUI.MiniHint(new Rect(x, y, width, OverlayRowHeight), objLine);
            }
        }
    }

    internal static void WriteServerStats()
    {
        if (instance == null)
        {
            LegacyServerConsole.WriteAdminLine("[Stats] Performance monitor is not initialized.");
            return;
        }

        // CPU/frame values are intentionally sampled over a stable one-second window.
        // Returning the latest snapshot avoids turning a command issued immediately
        // after a scheduled sample into a noisy millisecond-sized CPU interval.
        Snapshot s = current;

#if DEBUG
        LegacyServerConsole.WriteAdminLine(
            "[Stats] BCL optimizations: objectStripping=" +
            (LegacyServerHeadlessBootstrap.IsOptimizationEnabled ? "ON" : "OFF") +
            ", cpu=" +
            (LegacyServerHeadlessBootstrap.IsCpuOptimizationEnabled ? "ON" : "OFF") +
            ", carSleep=" +
            (ServerCarSleepPatches.Enabled ? "4.9-style" : "vanilla")
        );
#endif

        LegacyServerConsole.WriteAdminLine(
            "[Stats] Uptime: " +
            LegacyHelpers.FormatSeconds((long)Time.time)
        );

        LegacyServerConsole.WriteAdminLine(
            "[Stats] CPU: " + FormatFloat((float)s.CpuPercent, 1) + "% total, " +
            FormatFloat((float)s.CpuCoresUsed, 2) + "/" + s.ProcessorCount.ToString() + " cores, threads=" +
            (s.ThreadCount >= 0 ? s.ThreadCount.ToString() : "N/A")
        );

        string ram = "[Stats] RAM: RSS=" + FormatBytes(s.WorkingSetBytes) +
                     ", peakRSS=" + FormatBytes(s.PeakWorkingSetBytes) +
                     ", private=" + FormatBytes(s.PrivateBytes) +
                     ", managed=" + FormatBytes(s.ManagedBytes);
        if (s.LinuxPssBytes > 0)
        {
            ram += ", PSS=" + FormatBytes(s.LinuxPssBytes);
        }
        if (s.LinuxPrivateBytes > 0)
        {
            ram += ", linuxPrivate=" + FormatBytes(s.LinuxPrivateBytes);
        }
        LegacyServerConsole.WriteAdminLine(ram);

        LegacyServerConsole.WriteAdminLine(
            "[Stats] Unity memory: allocated=" + FormatOptionalBytes(s.UnityAllocatedBytes) +
            ", reserved=" + FormatOptionalBytes(s.UnityReservedBytes) +
            ", unusedReserved=" + FormatOptionalBytes(s.UnityUnusedReservedBytes) +
            ", monoUsed=" + FormatOptionalBytes(s.UnityMonoUsedBytes) +
            ", monoHeap=" + FormatOptionalBytes(s.UnityMonoHeapBytes)
        );

        LegacyServerConsole.WriteAdminLine(
            "[Stats] Frame: " + FormatFloat(s.AverageFrameMs, 2) + " ms avg, " +
            FormatFloat(s.WorstFrameMs, 2) + " ms max, " + FormatFloat(s.FramesPerSecond, 1) + " FPS"
        );

        //#if DEBUG
        //LegacyServerConsole.WriteAdminLine(
        //    "[Stats] GC: last ~1s gen0/gen1/gen2=" + s.GcGen0Delta.ToString() + "/" +
        //    s.GcGen1Delta.ToString() + "/" + s.GcGen2Delta.ToString() +
        //    ", totals=" + s.GcGen0.ToString() + "/" + s.GcGen1.ToString() + "/" + s.GcGen2.ToString()
        //);
        //#endif

        LegacyServerConsole.WriteAdminLine(
            "[Stats] Textures: total=" + FormatOptionalBytes(s.TextureMemoryBytes) +
            ", nonStreaming=" + FormatOptionalBytes(s.NonStreamingTextureMemoryBytes) +
            ", streamingCount=" + s.StreamingTextureCount.ToString() +
            ", nonStreamingCount=" + s.NonStreamingTextureCount.ToString()
        );

        LegacyServerConsole.WriteAdminLine(
            "[Stats] Game objects: players=" + s.Players.ToString() +
            ", Control=" + s.Controls.ToString() +
            ", NetControl=" + s.NetControls.ToString() +
            ", SmoothSync=" + s.SmoothSyncs.ToString() +
            ", InputControl=" + s.Inputs.ToString() +
            ", Loot=" + s.Loot.ToString() +
            ", groundCells=" + s.GroundCells.ToString()
        );

        LegacyServerConsole.WriteAdminLine(
            "[Stats] Cars: total=" + s.Cars.ToString() +
            ", awake=" + s.CarsAwake.ToString() +
            ", sleeping=" + s.CarsSleeping.ToString() +
            ", handled=" + s.CarsHandled.ToString() +
            ", clientOwned=" + s.CarsClientOwned.ToString() +
            ", serverOwned=" + s.CarsServerOwned.ToString() +
            ", sleepFastPath=" + s.CarsSleepFastPath.ToString()
        );

        LegacyServerConsole.WriteAdminLine(
            "[Stats] Server car optimization: sleepDelay=" +
            ServerCarSleepPatches.SleepDelayMs.ToString("0") + "ms, presentation=" +
            (ServerCarPresentationPatches.Enabled ? "ON" : "OFF") +
            ", UNet pendingBuffers=" + (ServerNetworkBufferPatches.Enabled
                ? ServerNetworkBufferPatches.PendingLimit.ToString() : "vanilla") +
            ", connectionsConfigured=" + ServerNetworkBufferPatches.ConfiguredConnections.ToString()
        );

        string gpu = string.IsNullOrEmpty(s.GpuName) ? "N/A" : s.GpuName;
        LegacyServerConsole.WriteAdminLine(
            "[Stats] GPU: " + gpu +
            ", API=" + (string.IsNullOrEmpty(s.GpuApi) ? "N/A" : s.GpuApi) +
            ", VRAM=" + (s.GpuMemoryMb > 0 ? s.GpuMemoryMb.ToString() + " MB" : "N/A") +
            ", driverAllocated=" + FormatOptionalBytes(s.GraphicsDriverAllocatedBytes)
        );
    }

    private void SampleNow(bool initializeCpuBaseline)
    {
        Snapshot next = new Snapshot();
        next.TimestampUtc = DateTime.UtcNow;

        SampleFrames(next);
        SampleProcess(next, initializeCpuBaseline);
        SampleUnityMemory(next);
        SampleTextureMemory(next);
        SampleGameCounters(next);
        SampleGpu(next);

        current = next;

        if (overlayHotkeyEnabled)
        {
            UpdateOverlayText(next);
        }
    }

    private static void InitializeOverlayTheme()
    {
        if (overlayHintTheme != null)
        {
            return;
        }

        LegacyUIPalette palette = new LegacyUIPalette(LegacyUI.Palette);
        palette.TextMuted = new Color(0.72f, 0.75f, 0.78f, 1f);
        overlayHintTheme = new LegacyUITheme(palette);
    }

    private void UpdateOverlayText(Snapshot s)
    {
        if (s == null)
        {
            return;
        }

        fpsLine = "FPS " + FormatFloat(s.FramesPerSecond, 1) +
                  "  |  frame " + FormatFloat(s.AverageFrameMs, 2) +
                  " ms avg / " + FormatFloat(s.WorstFrameMs, 2) + " ms max";

        cpuLine = "CPU " + FormatFloat((float)s.CpuPercent, 1) + "% total  |  " +
                  FormatFloat((float)s.CpuCoresUsed, 2) + " / " +
                  s.ProcessorCount.ToString() + " cores";

        memLine = "RAM RSS " + FormatBytes(s.WorkingSetBytes) +
                  "  |  private " + FormatBytes(s.PrivateBytes) +
                  "  |  managed " + FormatBytes(s.ManagedBytes);

        unityMemLine = "Unity alloc " + FormatOptionalBytes(s.UnityAllocatedBytes) +
                       "  |  reserved " + FormatOptionalBytes(s.UnityReservedBytes);

        if (s.LinuxPssBytes > 0)
        {
            unityMemLine += "  |  PSS " + FormatBytes(s.LinuxPssBytes);
        }

        gcLine = "GC/s gen0/1/2 " + s.GcGen0Delta.ToString() + "/" +
                 s.GcGen1Delta.ToString() + "/" + s.GcGen2Delta.ToString() +
                 "  |  threads " + (s.ThreadCount >= 0 ? s.ThreadCount.ToString() : "N/A");

        string gpu = string.IsNullOrEmpty(s.GpuName) ? "N/A" : s.GpuName;
        gpuLine = "GPU " + gpu;
        if (s.GpuMemoryMb > 0)
        {
            gpuLine += "  |  VRAM " + s.GpuMemoryMb.ToString() + " MB";
        }
        if (s.GraphicsDriverAllocatedBytes > 0)
        {
            gpuLine += "  |  driver alloc " + FormatBytes(s.GraphicsDriverAllocatedBytes);
        }

        texLine = "Textures " + FormatOptionalBytes(s.TextureMemoryBytes) +
                  "  |  streaming " + s.StreamingTextureCount.ToString() +
                  "  |  non-stream " + s.NonStreamingTextureCount.ToString();

        objLine = "Game players " + s.Players.ToString() +
                  "  |  Control " + s.Controls.ToString() +
                  "  |  Cars " + s.Cars.ToString() +
                  " (" + s.CarsSleeping.ToString() + " sleep)" +
                  "  |  SmoothSync " + s.SmoothSyncs.ToString();
    }

    private void SampleFrames(Snapshot target)
    {
        float now = Time.realtimeSinceStartup;
        float elapsed = now - sampleWindowStart;

        if (sampledFrames > 0)
        {
            target.AverageFrameMs = (frameTimeSum / sampledFrames) * 1000f;
            target.WorstFrameMs = worstFrameTime * 1000f;
            if (elapsed > 0.0001f)
            {
                target.FramesPerSecond = sampledFrames / elapsed;
            }
        }
        else if (current != null)
        {
            target.AverageFrameMs = current.AverageFrameMs;
            target.WorstFrameMs = current.WorstFrameMs;
            target.FramesPerSecond = current.FramesPerSecond;
        }

        frameTimeSum = 0f;
        worstFrameTime = 0f;
        sampledFrames = 0;
        sampleWindowStart = now;
    }

    private static void InitializeProcessSampler()
    {
        try
        {
            process = DiagnosticsProcess.GetCurrentProcess();
            lastProcessCpuTime = process.TotalProcessorTime;
            lastCpuWallUtc = DateTime.UtcNow;
            hasCpuBaseline = true;
        }
        catch
        {
            process = null;
            hasCpuBaseline = false;
        }
    }

    private static void SampleProcess(Snapshot target, bool initializeCpuBaseline)
    {
        target.ProcessorCount = Math.Max(1, Environment.ProcessorCount);
        target.ThreadCount = -1;

        try
        {
            target.ManagedBytes = GC.GetTotalMemory(false);
            target.GcGen0 = GC.CollectionCount(0);
            target.GcGen1 = GC.CollectionCount(1);
            target.GcGen2 = GC.CollectionCount(2);

            if (hasGcBaseline)
            {
                target.GcGen0Delta = Math.Max(0, target.GcGen0 - lastGcGen0);
                target.GcGen1Delta = Math.Max(0, target.GcGen1 - lastGcGen1);
                target.GcGen2Delta = Math.Max(0, target.GcGen2 - lastGcGen2);
            }

            lastGcGen0 = target.GcGen0;
            lastGcGen1 = target.GcGen1;
            lastGcGen2 = target.GcGen2;
            hasGcBaseline = true;
        }
        catch
        {
        }

        if (process == null)
        {
            InitializeProcessSampler();
        }

        if (process != null)
        {
            try
            {
                process.Refresh();

                DateTime now = DateTime.UtcNow;
                TimeSpan cpu = process.TotalProcessorTime;

                if (hasCpuBaseline && !initializeCpuBaseline)
                {
                    double wallSeconds = (now - lastCpuWallUtc).TotalSeconds;
                    double cpuSeconds = (cpu - lastProcessCpuTime).TotalSeconds;
                    if (wallSeconds > 0.0001 && cpuSeconds >= 0d)
                    {
                        target.CpuCoresUsed = Math.Max(0d, cpuSeconds / wallSeconds);
                        target.CpuPercent = Math.Max(0d, target.CpuCoresUsed * 100d / target.ProcessorCount);
                    }
                }
                else if (current != null)
                {
                    target.CpuCoresUsed = current.CpuCoresUsed;
                    target.CpuPercent = current.CpuPercent;
                }

                lastProcessCpuTime = cpu;
                lastCpuWallUtc = now;
                hasCpuBaseline = true;

                target.WorkingSetBytes = SafeProcessMemory(delegate { return process.WorkingSet64; });
                target.PeakWorkingSetBytes = SafeProcessMemory(delegate { return process.PeakWorkingSet64; });
                target.PrivateBytes = SafeProcessMemory(delegate { return process.PrivateMemorySize64; });

                try
                {
                    target.ThreadCount = process.Threads.Count;
                }
                catch
                {
                    target.ThreadCount = -1;
                }
            }
            catch
            {
            }
        }

        if (Application.platform == RuntimePlatform.WindowsPlayer)
        {
            FillWindowsProcessFallbacks(target);
        }

        if (Application.platform == RuntimePlatform.LinuxPlayer)
        {
            ReadLinuxSmapsRollup(out target.LinuxPssBytes, out target.LinuxPrivateBytes);
        }
    }

    private static void FillWindowsProcessFallbacks(Snapshot target)
    {
        // Mono's System.Diagnostics.Process implementation used by some Unity
        // Windows builds can return zero for memory/thread counters. 
        // Use Win32 only as a fallback so the normal managed path remains the default.
        if (target.WorkingSetBytes <= 0L || target.PeakWorkingSetBytes <= 0L || target.PrivateBytes <= 0L)
        {
            try
            {
                ProcessMemoryCountersEx counters = new ProcessMemoryCountersEx();
                counters.cb = (uint)Marshal.SizeOf(typeof(ProcessMemoryCountersEx));
                if (GetProcessMemoryInfo(GetCurrentProcess(), out counters, counters.cb))
                {
                    if (target.WorkingSetBytes <= 0L)
                    {
                        target.WorkingSetBytes = UIntPtrToInt64(counters.WorkingSetSize);
                    }
                    if (target.PeakWorkingSetBytes <= 0L)
                    {
                        target.PeakWorkingSetBytes = UIntPtrToInt64(counters.PeakWorkingSetSize);
                    }
                    if (target.PrivateBytes <= 0L)
                    {
                        target.PrivateBytes = UIntPtrToInt64(counters.PrivateUsage);
                    }
                }
            }
            catch
            {
            }
        }

        if (target.ThreadCount <= 0)
        {
            target.ThreadCount = CountWindowsProcessThreads();
        }
    }

    private static long UIntPtrToInt64(UIntPtr value)
    {
        try
        {
            return checked((long)value.ToUInt64());
        }
        catch
        {
            return 0L;
        }
    }

    private static int CountWindowsProcessThreads()
    {
        IntPtr snapshot = IntPtr.Zero;
        try
        {
            snapshot = CreateToolhelp32Snapshot(ToolhelpSnapThread, 0U);
            if (snapshot == IntPtr.Zero || snapshot == InvalidHandleValue)
            {
                return -1;
            }

            uint processId = GetCurrentProcessId();
            ThreadEntry32 entry = new ThreadEntry32();
            entry.dwSize = (uint)Marshal.SizeOf(typeof(ThreadEntry32));

            int count = 0;
            if (!Thread32First(snapshot, ref entry))
            {
                return -1;
            }

            do
            {
                if (entry.th32OwnerProcessID == processId)
                {
                    count++;
                }
                entry.dwSize = (uint)Marshal.SizeOf(typeof(ThreadEntry32));
            }
            while (Thread32Next(snapshot, ref entry));

            return count;
        }
        catch
        {
            return -1;
        }
        finally
        {
            if (snapshot != IntPtr.Zero && snapshot != InvalidHandleValue)
            {
                try { CloseHandle(snapshot); } catch { }
            }
        }
    }

    private delegate long MemoryGetter();

    private static long SafeProcessMemory(MemoryGetter getter)
    {
        try
        {
            return Math.Max(0L, getter());
        }
        catch
        {
            return 0L;
        }
    }

    private static void ReadLinuxSmapsRollup(out long pssBytes, out long privateBytes)
    {
        pssBytes = 0L;
        privateBytes = 0L;

        const string path = "/proc/self/smaps_rollup";
        try
        {
            if (!File.Exists(path))
            {
                return;
            }

            long privateCleanKb = 0L;
            long privateDirtyKb = 0L;
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.StartsWith("Pss:", StringComparison.Ordinal))
                {
                    pssBytes = ParseLinuxKbLine(line) * 1024L;
                }
                else if (line.StartsWith("Private_Clean:", StringComparison.Ordinal))
                {
                    privateCleanKb = ParseLinuxKbLine(line);
                }
                else if (line.StartsWith("Private_Dirty:", StringComparison.Ordinal))
                {
                    privateDirtyKb = ParseLinuxKbLine(line);
                }
            }

            privateBytes = (privateCleanKb + privateDirtyKb) * 1024L;
        }
        catch
        {
            pssBytes = 0L;
            privateBytes = 0L;
        }
    }

    private static long ParseLinuxKbLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return 0L;
        }

        int colon = line.IndexOf(':');
        if (colon < 0)
        {
            return 0L;
        }

        string value = line.Substring(colon + 1).Trim();
        int space = value.IndexOf(' ');
        if (space >= 0)
        {
            value = value.Substring(0, space);
        }

        long kb;
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out kb) ? kb : 0L;
    }

    private static void InitializeProfilerSampler()
    {
        try
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length && profilerType == null; i++)
            {
                profilerType = assemblies[i].GetType("UnityEngine.Profiling.Profiler", false)
                               ?? assemblies[i].GetType("UnityEngine.Profiler", false);
            }

            if (profilerType == null)
            {
                return;
            }

            profilerTotalAllocated = FindProfilerMethod("GetTotalAllocatedMemoryLong", "GetTotalAllocatedMemory");
            profilerTotalReserved = FindProfilerMethod("GetTotalReservedMemoryLong", "GetTotalReservedMemory");
            profilerTotalUnusedReserved = FindProfilerMethod("GetTotalUnusedReservedMemoryLong", "GetTotalUnusedReservedMemory");
            profilerMonoUsed = FindProfilerMethod("GetMonoUsedSizeLong", "GetMonoUsedSize");
            profilerMonoHeap = FindProfilerMethod("GetMonoHeapSizeLong", "GetMonoHeapSize");
            profilerGraphicsDriverAllocated = FindProfilerMethod("GetAllocatedMemoryForGraphicsDriver");
        }
        catch
        {
            profilerType = null;
        }
    }

    private static MethodInfo FindProfilerMethod(params string[] names)
    {
        if (profilerType == null || names == null)
        {
            return null;
        }

        for (int i = 0; i < names.Length; i++)
        {
            MethodInfo method = profilerType.GetMethod(
                names[i],
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            if (method != null)
            {
                return method;
            }
        }

        return null;
    }

    private static void SampleUnityMemory(Snapshot target)
    {
        target.UnityAllocatedBytes = InvokeMemoryMethod(profilerTotalAllocated);
        target.UnityReservedBytes = InvokeMemoryMethod(profilerTotalReserved);
        target.UnityUnusedReservedBytes = InvokeMemoryMethod(profilerTotalUnusedReserved);
        target.UnityMonoUsedBytes = InvokeMemoryMethod(profilerMonoUsed);
        target.UnityMonoHeapBytes = InvokeMemoryMethod(profilerMonoHeap);
        target.GraphicsDriverAllocatedBytes = InvokeMemoryMethod(profilerGraphicsDriverAllocated);
    }

    private static long InvokeMemoryMethod(MethodInfo method)
    {
        if (method == null)
        {
            return 0L;
        }

        try
        {
            object value = method.Invoke(null, null);
            if (value == null)
            {
                return 0L;
            }
            return Math.Max(0L, Convert.ToInt64(value, CultureInfo.InvariantCulture));
        }
        catch
        {
            return 0L;
        }
    }


    private static void SampleTextureMemory(Snapshot target)
    {
        try { target.TextureMemoryBytes = (long)Texture.totalTextureMemory; } catch { target.TextureMemoryBytes = 0L; }
        try { target.NonStreamingTextureMemoryBytes = (long)Texture.nonStreamingTextureMemory; } catch { target.NonStreamingTextureMemoryBytes = 0L; }
        try { target.StreamingTextureCount = (int)Texture.streamingTextureCount; } catch { target.StreamingTextureCount = 0; }
        try { target.NonStreamingTextureCount = (int)Texture.nonStreamingTextureCount; } catch { target.NonStreamingTextureCount = 0; }
    }

    private static void InitializeGameCounterReflection()
    {
        try
        {
            lootItemsField = typeof(Loot).GetField("items", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        }
        catch
        {
            lootItemsField = null;
        }

        try
        {
            groundItemsMapField = typeof(GroundLoader).GetField("itemsMap", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }
        catch
        {
            groundItemsMapField = null;
        }
    }

    private static void SampleGameCounters(Snapshot target)
    {
        try { target.Players = NetManager.curCount; } catch { target.Players = 0; }
        try { target.Controls = Control.controls != null ? Control.controls.Count : 0; } catch { target.Controls = 0; }
        try { target.NetControls = NetControl.netControls != null ? NetControl.netControls.Count : 0; } catch { target.NetControls = 0; }
        try { target.SmoothSyncs = SmoothSync.instacnes != null ? SmoothSync.instacnes.Count : 0; } catch { target.SmoothSyncs = 0; }
        try { target.Inputs = InputControl.inputs != null ? InputControl.inputs.Count : 0; } catch { target.Inputs = 0; }

        try
        {
            if (Control.controls != null)
            {
                for (int i = 0; i < Control.controls.Count; i++)
                {
                    CarControl car = Control.controls[i] as CarControl;
                    if (!car)
                    {
                        continue;
                    }

                    target.Cars++;
                    if (car.body != null && car.body.IsSleeping())
                    {
                        target.CarsSleeping++;
                    }
                    else
                    {
                        target.CarsAwake++;
                    }

                    if (car.nowHandleUser)
                    {
                        target.CarsHandled++;
                    }

                    if (ServerCarSleepPatches.IsClientOwnedCar(car))
                    {
                        target.CarsClientOwned++;
                    }
                    else
                    {
                        target.CarsServerOwned++;
                    }

                    if (ServerCarSleepPatches.IsSleepingFastPathCandidate(car))
                    {
                        target.CarsSleepFastPath++;
                    }
                }
            }
        }
        catch
        {
            target.Cars = 0;
            target.CarsAwake = 0;
            target.CarsSleeping = 0;
            target.CarsHandled = 0;
            target.CarsClientOwned = 0;
            target.CarsServerOwned = 0;
            target.CarsSleepFastPath = 0;
        }

        target.Loot = GetCollectionCount(lootItemsField, null);
        target.GroundCells = GroundLoader.me != null ? GetCollectionCount(groundItemsMapField, GroundLoader.me) : 0;
    }

    private static int GetCollectionCount(FieldInfo field, object instanceValue)
    {
        if (field == null)
        {
            return 0;
        }

        try
        {
            ICollection collection = field.GetValue(instanceValue) as ICollection;
            return collection != null ? collection.Count : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static void SampleGpu(Snapshot target)
    {
        try { target.GpuName = SystemInfo.graphicsDeviceName; } catch { target.GpuName = string.Empty; }
        try { target.GpuApi = SystemInfo.graphicsDeviceVersion; } catch { target.GpuApi = string.Empty; }
        try { target.GpuMemoryMb = SystemInfo.graphicsMemorySize; } catch { target.GpuMemoryMb = 0; }
    }

    private static string FormatOptionalBytes(long bytes)
    {
        return bytes > 0L ? FormatBytes(bytes) : "N/A";
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0L)
        {
            bytes = 0L;
        }

        const double kb = 1024d;
        const double mb = 1024d * 1024d;
        const double gb = 1024d * 1024d * 1024d;

        if (bytes >= gb)
        {
            return (bytes / gb).ToString("0.00", CultureInfo.InvariantCulture) + " GB";
        }
        if (bytes >= mb)
        {
            return (bytes / mb).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
        }
        if (bytes >= kb)
        {
            return (bytes / kb).ToString("0.0", CultureInfo.InvariantCulture) + " KB";
        }
        return bytes.ToString(CultureInfo.InvariantCulture) + " B";
    }

    private static string FormatFloat(float value, int decimals)
    {
        string format = decimals <= 0 ? "0" : "0." + new string('0', decimals);
        return value.ToString(format, CultureInfo.InvariantCulture);
    }
}
