using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

public sealed class LegacyServerConsole : MonoBehaviour
{
    private const int MaxLogLinesCount = 1000000;

    private const uint AttachParentProcess = 0xFFFFFFFFU;
    private const int ErrorAccessDenied = 5;
    private const string AttachParentConsoleArg = "-attachParentConsole";
    private const uint Utf8CodePage = 65001U;

    private const uint GenericRead = 0x80000000U;
    private const uint GenericWrite = 0x40000000U;
    private const uint FileShareRead = 0x00000001U;
    private const uint FileShareWrite = 0x00000002U;
    private const uint OpenExisting = 3U;

    private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
    private static readonly object ConsoleWriteSync = new object();
    private static readonly object DeferredConsoleSync = new object();
    private static readonly Queue<string> PendingCommands = new Queue<string>();
    private static readonly Queue<DeferredConsoleRecord> DeferredConsoleRecords = new Queue<DeferredConsoleRecord>();
    private static readonly AutoResetEvent CommandProcessed = new AutoResetEvent(false);

    private enum DeferredConsoleRecordKind
    {
        UnityLog,
        AdminLine
    }

    private struct DeferredConsoleRecord
    {
        public DeferredConsoleRecordKind Kind;
        public string Text;
        public LogType Type;
    }

    private static LegacyServerConsole instance;
    private static bool windowsInitTried;
    private static bool windowsReady;
    private static bool linuxInitTried;
    private static bool linuxReady;
    private static bool linuxSupportsAnsiLineClear;
    private static bool inputSpacerActive;
    private static bool worldReadyForCli;
    private static bool commandReaderStarted;
    private static bool consoleInitializationCompleted;
    private static volatile bool quitRequested;
    private static ConsoleCtrlHandler consoleCtrlHandler;
    private static UnixSignalHandler unixSignalHandler;
    private static Thread commandReaderThread;

    private static IntPtr windowsConsoleInput = IntPtr.Zero;
    private static IntPtr windowsConsoleOutput = IntPtr.Zero;

    private static int linuxStdoutFd = -1;
    private static int linuxStderrFd = -1;

    private static bool readyStatusPrinted;
    private static IntPtr cachedBatchWindow = IntPtr.Zero;

    private int logLinesCount;
    private bool batchWindowHideLogged;
    private bool batchWindowHideStopped;
    private int batchWindowHideAttempts;
    private float nextBatchWindowHideTryTime;

    public static void PrepareForServerMode()
    {
        if (!LegacyCommandLine.HasServerArg())
        {
            return;
        }

        if (LegacyCommandLine.HasArg("-keepBepInExConsoleLog") ||
            LegacyCommandLine.HasArg("-verboseServerConsole"))
        {
            return;
        }

        TryDisableBepInExConsoleListener();
    }

    private static void TryDisableBepInExConsoleListener()
    {
        try
        {
            Type loggerType = typeof(BepInEx.Logging.Logger);
            PropertyInfo listenersProperty = loggerType.GetProperty("Listeners", BindingFlags.Public | BindingFlags.Static);
            if (listenersProperty == null)
            {
                return;
            }

            object listeners = listenersProperty.GetValue(null, null);
            IEnumerable enumerable = listeners as IEnumerable;
            if (enumerable == null)
            {
                return;
            }

            List<object> toRemove = new List<object>();
            foreach (object listener in enumerable)
            {
                if (listener == null)
                {
                    continue;
                }

                Type listenerType = listener.GetType();
                string typeName = listenerType.FullName ?? listenerType.Name;
                if (typeName.IndexOf("ConsoleLogListener", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    toRemove.Add(listener);
                }
            }

            MethodInfo removeMethod = listeners.GetType().GetMethod("Remove");
            if (removeMethod == null)
            {
                return;
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                removeMethod.Invoke(listeners, new object[] { toRemove[i] });
            }
        }
        catch
        {
            // BepInEx logging is optional for the dedicated-server console.
        }
    }

    public static void CreateIfNeeded()
    {
        if (instance || !LegacyCommandLine.HasServerArg())
        {
            return;
        }

        GameObject host = new GameObject("BigCityLegacy_ServerConsole");
        UnityEngine.Object.DontDestroyOnLoad(host);
        instance = host.AddComponent<LegacyServerConsole>();
    }

    private void OnEnable()
    {
        LegacyServerShutdown.Initialize();

        Application.logMessageReceived -= HandleLog;
        Application.logMessageReceived += HandleLog;

        TryInitWindowsServerConsole();
        TryInitLinuxServerConsole();
        consoleInitializationCompleted = true;
        FlushDeferredConsoleRecords();

        // Do not print a CLI prompt here. World startup continues to emit logs after
        // this component is created, which makes a prompt look broken and can visually
        // split the first input line.
        TryHideBatchModeWindow();
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
        FlushWindowsServerConsole();
        FlushLinuxServerConsole();
        inputSpacerActive = false;
        CloseWindowsConsoleHandles();
        if (instance == this)
        {
            instance = null;
        }
    }

    private void Update()
    {
        TryHideBatchModeWindow();
        ProcessPendingCommands();

        if (logLinesCount > MaxLogLinesCount)
        {
            LegacyServerShutdown.Request("log line limit exceeded (" + logLinesCount.ToString() + ")");
        }
    }

    private void HandleLog(string message, string stackTrace, LogType type)
    {
        logLinesCount++;
        TryPrintServerReadyStatus(message);

        if (ShouldSuppressServerConsoleLog(message, stackTrace, type))
        {
            return;
        }

        WriteWindowsServerConsoleLog(message, stackTrace, type);
        WriteLinuxServerConsoleLog(message, stackTrace, type);
    }

    internal static void LogAfterConsoleInit(string text, LogType type)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (ShouldDeferServerConsoleOutput())
        {
            EnqueueDeferredConsoleRecord(DeferredConsoleRecordKind.UnityLog, text, type);
            return;
        }

        EmitUnityLog(text, type);
    }

    internal static void WriteAdminLineAfterConsoleInit(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (ShouldDeferServerConsoleOutput())
        {
            EnqueueDeferredConsoleRecord(DeferredConsoleRecordKind.AdminLine, text, LogType.Log);
            return;
        }

        WriteAdminLine(text);
    }

    internal static void WriteAdminLine(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        if (WriteServerConsoleText(false, text + Environment.NewLine))
        {
            return;
        }

        Debug.Log(text);
    }

    private static bool ShouldDeferServerConsoleOutput()
    {
        return IsDedicatedServerConsoleWanted() && !consoleInitializationCompleted;
    }

    private static void EnqueueDeferredConsoleRecord(DeferredConsoleRecordKind kind, string text, LogType type)
    {
        lock (DeferredConsoleSync)
        {
            DeferredConsoleRecords.Enqueue(new DeferredConsoleRecord
            {
                Kind = kind,
                Text = text,
                Type = type
            });
        }
    }

    private static void FlushDeferredConsoleRecords()
    {
        DeferredConsoleRecord[] records;

        lock (DeferredConsoleSync)
        {
            if (DeferredConsoleRecords.Count == 0)
            {
                return;
            }

            records = DeferredConsoleRecords.ToArray();
            DeferredConsoleRecords.Clear();
        }

        for (int i = 0; i < records.Length; i++)
        {
            DeferredConsoleRecord record = records[i];

            if (record.Kind == DeferredConsoleRecordKind.AdminLine)
            {
                WriteAdminLine(record.Text);
            }
            else
            {
                EmitUnityLog(record.Text, record.Type);
            }
        }
    }

    private static void EmitUnityLog(string text, LogType type)
    {
        switch (type)
        {
            case LogType.Warning:
                Debug.LogWarning(text);
                break;

            case LogType.Error:
            case LogType.Assert:
            case LogType.Exception:
                Debug.LogError(text);
                break;

            default:
                Debug.Log(text);
                break;
        }
    }

    private static void StartCommandReaderIfNeeded()
    {
        if (commandReaderStarted ||
            LegacyCommandLine.HasArg("-noServerCli") ||
            (!windowsReady && !linuxReady))
        {
            return;
        }

        commandReaderStarted = true;
        WriteAdminLine("[CLI] Type 'help' for the command list.");

        commandReaderThread = new Thread(ReadCommandsLoop);
        commandReaderThread.IsBackground = true;
        commandReaderThread.Name = "BigCityLegacy Server CLI";
        commandReaderThread.Start();
    }

    private void ProcessPendingCommands()
    {
        const int maxCommandsPerFrame = 8;

        for (int i = 0; i < maxCommandsPerFrame; i++)
        {
            string command = null;

            lock (PendingCommands)
            {
                if (PendingCommands.Count > 0)
                {
                    command = PendingCommands.Dequeue();
                }
            }

            if (command == null)
            {
                return;
            }

            try
            {
                LegacyServerCli.Execute(command);
            }
            catch (Exception ex)
            {
                WriteAdminLine("[CLI] Command failed: " + ex.Message);
                Debug.LogError("Server CLI command failed: " + ex);
            }
            finally
            {
                CommandProcessed.Set();
            }
        }
    }

    private static void ReadCommandsLoop()
    {
        try
        {
            while (!quitRequested && !LegacyServerShutdown.IsRequested)
            {
                string line = ReadCliLine();

                if (line == null)
                {
                    break;
                }

                MarkInputLineCommitted();

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                lock (PendingCommands)
                {
                    PendingCommands.Enqueue(line);
                }

                CommandProcessed.WaitOne();

                if (quitRequested || LegacyServerShutdown.IsRequested)
                {
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            if (!quitRequested && !LegacyServerShutdown.IsRequested)
            {
                Debug.LogWarning("Server CLI input stopped: " + ex.Message);
            }
        }
    }

    private static string ReadCliLine()
    {
        if (windowsReady)
        {
            return ReadWindowsConsoleLine();
        }

        if (linuxReady)
        {
            return Console.In.ReadLine();
        }

        return null;
    }

    private static bool WriteServerConsoleText(bool error, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        string recordWithSpacer = NormalizeConsoleRecord(text) + Environment.NewLine;

        lock (ConsoleWriteSync)
        {
            if (windowsReady)
            {
                PrepareWindowsInputSpacerForOutput();

                bool written = WriteWindowsConsoleRaw(recordWithSpacer);
                if (written)
                {
                    inputSpacerActive = true;
                }

                return written;
            }

            if (linuxReady)
            {
                PrepareLinuxInputSpacerForOutput();
                WriteLinuxConsoleRaw(false, recordWithSpacer);
                inputSpacerActive = true;
                return true;
            }

            return false;
        }
    }

    private static void MarkInputLineCommitted()
    {
        lock (ConsoleWriteSync)
        {
            inputSpacerActive = false;
        }
    }

    private static string NormalizeConsoleRecord(string text)
    {
        int length = text.Length;
        while (length > 0)
        {
            char last = text[length - 1];
            if (last != '\r' && last != '\n')
            {
                break;
            }

            length--;
        }

        if (length == 0)
        {
            return Environment.NewLine;
        }

        return text.Substring(0, length) + Environment.NewLine;
    }

    private static void PrepareWindowsInputSpacerForOutput()
    {
        if (!inputSpacerActive)
        {
            return;
        }

        if (!TryReuseWindowsInputSpacer())
        {
            WriteWindowsConsoleRaw(Environment.NewLine);
        }
    }

    private static void PrepareLinuxInputSpacerForOutput()
    {
        if (!inputSpacerActive)
        {
            return;
        }

        if (linuxSupportsAnsiLineClear)
        {
            WriteLinuxConsoleRaw(false, "\r\u001b[1A\u001b[2K");
            return;
        }

        WriteLinuxConsoleRaw(false, "\n");
    }

    internal static void SignalCommandReaderShutdown()
    {
        quitRequested = true;
        CommandProcessed.Set();

        if (windowsReady && IsValidConsoleHandle(windowsConsoleInput))
        {
            try
            {
                CancelIoEx(windowsConsoleInput, IntPtr.Zero);
            }
            catch
            {
            }
        }
    }

    private static bool IsDedicatedServerConsoleWanted()
    {
        return LegacyCommandLine.HasServerArg() && !LegacyCommandLine.HasArg("-noServerConsole");
    }

    private void TryInitWindowsServerConsole()
    {
        if (windowsInitTried)
        {
            return;
        }

        windowsInitTried = true;

        if (Application.platform != RuntimePlatform.WindowsPlayer || !IsDedicatedServerConsoleWanted())
        {
            return;
        }

        try
        {
            bool consoleReady = AcquireWindowsServerConsole();
            if (!consoleReady)
            {
                throw new InvalidOperationException(
                    "Unable to acquire a Windows console. Win32 error=" +
                    Marshal.GetLastWin32Error().ToString()
                );
            }

            OpenWindowsConsoleHandles();

            if (!IsValidConsoleHandle(windowsConsoleInput) ||
                !IsValidConsoleHandle(windowsConsoleOutput))
            {
                throw new InvalidOperationException(
                    "CONIN$/CONOUT$ could not be opened. Win32 error=" +
                    Marshal.GetLastWin32Error().ToString()
                );
            }

            try
            {
                SetConsoleCP(Utf8CodePage);
                SetConsoleOutputCP(Utf8CodePage);
            }
            catch
            {
            }

            windowsReady = true;
            inputSpacerActive = false;

            if (consoleCtrlHandler == null)
            {
                consoleCtrlHandler = OnWindowsConsoleCtrl;
                SetConsoleCtrlHandler(consoleCtrlHandler, true);
            }

            WriteWindowsConsoleRaw("------------------------------------------" + Environment.NewLine);
            WriteWindowsConsoleRaw(
                "MadOut2 " + LegacyMasterServer.ReturnServerModeName() +
                " Server | Game ver " + Application.version +
                " | BigCityLegacy v" + VersionInfo.ModVersionName +
                Environment.NewLine
            );
            WriteWindowsConsoleRaw("Press Ctrl+C or type \"stop\" to stop server" + Environment.NewLine);
            WriteWindowsConsoleRaw("------------------------------------------" + Environment.NewLine);
            WriteWindowsConsoleRaw("[Status] Loading world..." + Environment.NewLine);
            WriteWindowsConsoleRaw("\n");
        }
        catch (Exception ex)
        {
            windowsReady = false;
            CloseWindowsConsoleHandles();
            Debug.LogWarning("Failed to init Windows server console: " + ex.Message);
        }
    }

    private static bool AcquireWindowsServerConsole()
    {
        // Unity's Windows player is a GUI executable. When it is started
        // directly from an interactive cmd/PowerShell session, the shell does not wait
        // for it and immediately resumes reading the parent's console input. Attaching
        // to that same console would therefore leave the shell and the server CLI racing
        // for one buffer.
        //
        // By default use a private console, which gives the server exclusive input. The
        // provided runServer.cmd executes game.exe from a command script (where cmd does
        // wait), so it opts into sharing its parent console with -attachParentConsole.
        if (LegacyCommandLine.HasArg(AttachParentConsoleArg))
        {
            bool attached = AttachConsole(AttachParentProcess);
            int attachError = attached ? 0 : Marshal.GetLastWin32Error();

            if (attached || attachError == ErrorAccessDenied)
                return true;
        }

        try
        {
            FreeConsole();
        }
        catch
        {
        }

        return AllocConsole();
    }

    private static void OpenWindowsConsoleHandles()
    {
        CloseWindowsConsoleHandles();

        windowsConsoleInput = CreateFile(
            "CONIN$",
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0U,
            IntPtr.Zero
        );

        windowsConsoleOutput = CreateFile(
            "CONOUT$",
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0U,
            IntPtr.Zero
        );
    }

    private static void CloseWindowsConsoleHandles()
    {
        if (IsValidConsoleHandle(windowsConsoleInput))
        {
            CloseHandle(windowsConsoleInput);
        }

        if (IsValidConsoleHandle(windowsConsoleOutput))
        {
            CloseHandle(windowsConsoleOutput);
        }

        windowsConsoleInput = IntPtr.Zero;
        windowsConsoleOutput = IntPtr.Zero;
    }

    private static bool IsValidConsoleHandle(IntPtr handle)
    {
        return handle != IntPtr.Zero && handle != InvalidHandleValue;
    }

    private static bool TryReuseWindowsInputSpacer()
    {
        if (!windowsReady || !IsValidConsoleHandle(windowsConsoleOutput))
        {
            return false;
        }

        ConsoleScreenBufferInfo info;
        if (!GetConsoleScreenBufferInfo(windowsConsoleOutput, out info))
        {
            return false;
        }

        if (info.CursorPosition.X != 0 || info.CursorPosition.Y <= 0)
        {
            return false;
        }

        short width = info.Size.X;
        if (width <= 0)
        {
            return false;
        }

        ConsoleCoord spacer = new ConsoleCoord();
        spacer.X = 0;
        spacer.Y = (short)(info.CursorPosition.Y - 1);

        uint ignored;
        if (!SetConsoleCursorPosition(windowsConsoleOutput, spacer) ||
            !FillConsoleOutputCharacter(windowsConsoleOutput, ' ', (uint)width, spacer, out ignored) ||
            !FillConsoleOutputAttribute(windowsConsoleOutput, info.Attributes, (uint)width, spacer, out ignored))
        {
            return false;
        }

        return SetConsoleCursorPosition(windowsConsoleOutput, spacer);
    }

    private static bool WriteWindowsConsoleRaw(string text)
    {
        if (!windowsReady || !IsValidConsoleHandle(windowsConsoleOutput) || string.IsNullOrEmpty(text))
        {
            return false;
        }

        try
        {
            uint written;
            if (WriteConsole(windowsConsoleOutput, text, (uint)text.Length, out written, IntPtr.Zero))
            {
                return true;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            uint bytesWritten;
            return WriteFile(
                windowsConsoleOutput,
                bytes,
                (uint)bytes.Length,
                out bytesWritten,
                IntPtr.Zero
            );
        }
        catch
        {
            windowsReady = false;
            return false;
        }
    }

    private static string ReadWindowsConsoleLine()
    {
        if (!windowsReady || !IsValidConsoleHandle(windowsConsoleInput))
        {
            return null;
        }

        StringBuilder buffer = new StringBuilder(2048);
        uint charsRead;

        bool ok = ReadConsole(
            windowsConsoleInput,
            buffer,
            (uint)(buffer.Capacity - 1),
            out charsRead,
            IntPtr.Zero
        );

        if (!ok)
        {
            return null;
        }

        if (charsRead == 0)
        {
            return string.Empty;
        }

        return buffer
            .ToString(0, Math.Min((int)charsRead, buffer.Length))
            .TrimEnd('\r', '\n');
    }

    private void WriteWindowsServerConsoleLog(string message, string stackTrace, LogType type)
    {
        if (!windowsReady)
        {
            return;
        }

        bool isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
        string text = "[" + type.ToString() + "] " + message + Environment.NewLine;

        if (isError && !string.IsNullOrEmpty(stackTrace))
        {
            text += stackTrace + Environment.NewLine;
        }

        WriteServerConsoleText(isError, text);
    }

    private static void FlushWindowsServerConsole()
    {
    }

    private static bool OnWindowsConsoleCtrl(uint ctrlType)
    {
        if (ctrlType == 0U || ctrlType == 1U || ctrlType == 2U || ctrlType == 5U || ctrlType == 6U)
        {
            LegacyServerShutdown.Request("Ctrl+C or Windows console close event");
            return true;
        }

        return false;
    }

    private void TryInitLinuxServerConsole()
    {
        if (linuxInitTried)
        {
            return;
        }

        linuxInitTried = true;

        if (Application.platform != RuntimePlatform.LinuxPlayer || !IsDedicatedServerConsoleWanted())
        {
            return;
        }

        try
        {
            linuxStdoutFd = dup(1);
            linuxStderrFd = dup(2);

            if (linuxStdoutFd < 0 && linuxStderrFd < 0)
            {
                return;
            }

            if (linuxStderrFd < 0)
            {
                linuxStderrFd = linuxStdoutFd;
            }

            int nullFd = open("/dev/null", 1);
            if (nullFd >= 0)
            {
                dup2(nullFd, 1);
                dup2(nullFd, 2);
                close(nullFd);
            }

            linuxReady = true;
            linuxSupportsAnsiLineClear = isatty(linuxStdoutFd) == 1;
            inputSpacerActive = false;

            if (unixSignalHandler == null)
            {
                unixSignalHandler = OnUnixSignal;
                signal(2, unixSignalHandler);
                signal(15, unixSignalHandler);
                signal(1, unixSignalHandler);
                signal(3, unixSignalHandler);
            }

            WriteLinuxConsoleRaw(false, "\u001b[3J\u001b[2J\u001b[H");
            WriteLinuxConsoleRaw(false, "\n");
            WriteLinuxConsoleRaw(false, "--------------------------------------------\n");
            WriteLinuxConsoleRaw(false, "MadOut2 " + LegacyMasterServer.ReturnServerModeName() + " Server | Game ver " + Application.version + " | BigCityLegacy v" + VersionInfo.ModVersionName + "\n");
            WriteLinuxConsoleRaw(false, "Press Ctrl+C or type \"stop\" to stop server\n");
            WriteLinuxConsoleRaw(false, "--------------------------------------------\n");
            WriteLinuxConsoleRaw(false, "[Status] Loading world...\n\n");
        }
        catch (Exception ex)
        {
            linuxReady = false;
            Debug.LogWarning("Failed to init Linux server console: " + ex.Message);
        }
    }

    private void WriteLinuxServerConsoleLog(string message, string stackTrace, LogType type)
    {
        if (!linuxReady)
        {
            return;
        }

        bool isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
        string text = "[" + type.ToString() + "] " + message + "\n";

        if (isError && !string.IsNullOrEmpty(stackTrace))
        {
            text += stackTrace + "\n";
        }

        WriteServerConsoleText(isError, text);
    }

    private static void FlushLinuxServerConsole()
    {
    }

    public static void WriteLinuxConsoleRaw(bool error, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        int fd = error ? linuxStderrFd : linuxStdoutFd;
        if (fd < 0)
        {
            return;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        write(fd, bytes, new IntPtr(bytes.Length));
    }

    private static void OnUnixSignal(int signal)
    {
        LegacyServerShutdown.Request("Unix signal " + signal.ToString());
    }

    private static bool ShouldSuppressServerConsoleLog(string message, string stackTrace, LogType type)
    {
        if (!LegacyCommandLine.HasServerArg())
        {
            return false;
        }

        if (!LegacyCommandLine.HasBatchMode() && !LegacyCommandLine.HasNoGraphics())
        {
            return false;
        }

        if (LegacyCommandLine.HasArg("-verboseServerConsole"))
        {
            return false;
        }

        if (message == null)
        {
            message = string.Empty;
        }

        if (stackTrace == null)
        {
            stackTrace = string.Empty;
        }

        return message.IndexOf("NullReferenceException") != -1
            || (message.IndexOf("Load Time") != -1 && message.IndexOf("Map.bytes") != -1)
            || message.IndexOf("Textures was remed") != -1
            || message.IndexOf("Can't remove Light because DistanceViewLight") != -1
            || message.IndexOf("GarageDoor, interCount") != -1
            || stackTrace.IndexOf("GarageDoor.OnEnable") != -1
            || (message.IndexOf("Magic number is wrong") != -1 && stackTrace.IndexOf("System.TermInfo") != -1)
            || stackTrace.IndexOf("System.ConsoleDriver") != -1
            || stackTrace.IndexOf("System.TermInfoReader") != -1
            || message.IndexOf("UnloadAsset can only be used on assets") != -1
            || message.IndexOf("Windows Unity.BatchModeWindow hidden") != -1
            || message.IndexOf("Parce:-masterServer") != -1
            || message.IndexOf("Destroy car windows") != -1
            || message.IndexOf("Vehicle slot released") != -1
            || message.IndexOf("Car spawn rejected") != -1
            || message.IndexOf("RemoveUserFromColorArray") != -1
            || message.IndexOf("FileBlocks_Loader. Fail to stop thread") != -1
            || message.IndexOf("Unknow EventType for launch: RP") != -1;
    }

    private static void TryPrintServerReadyStatus(string message)
    {
        if (readyStatusPrinted ||
            !LegacyCommandLine.HasServerArg() ||
            (!LegacyCommandLine.HasBatchMode() && !LegacyCommandLine.HasNoGraphics()) ||
            string.IsNullOrEmpty(message))
        {
            return;
        }

        if (message.IndexOf("Textures was remed") == -1)
        {
            return;
        }

        readyStatusPrinted = true;
        worldReadyForCli = true;

        string text = "[Status] Server ready. World loaded successfully.";

        WriteServerConsoleText(false, text + Environment.NewLine);

        if (worldReadyForCli)
        {
            StartCommandReaderIfNeeded();
        }
    }

    private void TryHideBatchModeWindow()
    {
        if (batchWindowHideStopped)
        {
            return;
        }

        if (Application.platform != RuntimePlatform.WindowsPlayer)
        {
            batchWindowHideStopped = true;
            return;
        }

        if (!LegacyCommandLine.HasServerArg() ||
            (!LegacyCommandLine.HasNoGraphics() && !LegacyCommandLine.HasHideWindow()))
        {
            return;
        }

        if (nextBatchWindowHideTryTime > Time.realtimeSinceStartup)
        {
            return;
        }

        nextBatchWindowHideTryTime = Time.realtimeSinceStartup + 0.25f;
        batchWindowHideAttempts++;

        if (HideUnityBatchModeWindowForThisProcess())
        {
            if (!batchWindowHideLogged)
            {
                batchWindowHideLogged = true;
                Debug.Log("Windows Unity.BatchModeWindow hidden");
            }

            batchWindowHideAttempts = 0;
            return;
        }

        if (!batchWindowHideLogged && batchWindowHideAttempts > 80)
        {
            batchWindowHideStopped = true;
            Debug.LogWarning("Windows Unity.BatchModeWindow was not found");
        }
    }

    private static bool HideUnityBatchModeWindowForThisProcess()
    {
        if (Application.platform != RuntimePlatform.WindowsPlayer)
        {
            return false;
        }

        if (cachedBatchWindow != IntPtr.Zero)
        {
            if (IsWindow(cachedBatchWindow))
            {
                HideWindow(cachedBatchWindow);
                return true;
            }

            cachedBatchWindow = IntPtr.Zero;
        }

        int currentProcessId = Process.GetCurrentProcess().Id;
        IntPtr foundWindow = IntPtr.Zero;

        EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
        {
            uint processId;
            GetWindowThreadProcessId(hWnd, out processId);

            if (processId != (uint)currentProcessId)
            {
                return true;
            }

            StringBuilder className = new StringBuilder(256);
            GetClassName(hWnd, className, className.Capacity);
            string name = className.ToString();

            if (name == "Unity.BatchModeWindow" || name.IndexOf("Unity.BatchModeWindow") >= 0)
            {
                foundWindow = hWnd;
                return false;
            }

            return true;
        }, IntPtr.Zero);

        if (foundWindow == IntPtr.Zero)
        {
            return false;
        }

        cachedBatchWindow = foundWindow;
        HideWindow(foundWindow);
        return true;
    }

    private static void HideWindow(IntPtr hWnd)
    {
        ShowWindow(hWnd, 0);
        SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, 151U);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCP(uint wCodePageID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleOutputCP(uint wCodePageID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandler handlerRoutine, bool add);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile
    );

    [StructLayout(LayoutKind.Sequential)]
    private struct ConsoleCoord
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ConsoleSmallRect
    {
        public short Left;
        public short Top;
        public short Right;
        public short Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ConsoleScreenBufferInfo
    {
        public ConsoleCoord Size;
        public ConsoleCoord CursorPosition;
        public ushort Attributes;
        public ConsoleSmallRect Window;
        public ConsoleCoord MaximumWindowSize;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleScreenBufferInfo(
        IntPtr hConsoleOutput,
        out ConsoleScreenBufferInfo lpConsoleScreenBufferInfo
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCursorPosition(
        IntPtr hConsoleOutput,
        ConsoleCoord dwCursorPosition
    );

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool FillConsoleOutputCharacter(
        IntPtr hConsoleOutput,
        char cCharacter,
        uint nLength,
        ConsoleCoord dwWriteCoord,
        out uint lpNumberOfCharsWritten
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FillConsoleOutputAttribute(
        IntPtr hConsoleOutput,
        ushort wAttribute,
        uint nLength,
        ConsoleCoord dwWriteCoord,
        out uint lpNumberOfAttrsWritten
    );

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool WriteConsole(
        IntPtr hConsoleOutput,
        string lpBuffer,
        uint nNumberOfCharsToWrite,
        out uint lpNumberOfCharsWritten,
        IntPtr lpReserved
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool WriteFile(
        IntPtr hFile,
        byte[] lpBuffer,
        uint nNumberOfBytesToWrite,
        out uint lpNumberOfBytesWritten,
        IntPtr lpOverlapped
    );


    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool ReadConsole(
        IntPtr hConsoleInput,
        StringBuilder lpBuffer,
        uint nNumberOfCharsToRead,
        out uint lpNumberOfCharsRead,
        IntPtr pInputControl
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CancelIoEx(IntPtr hFile, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("libc")]
    private static extern int dup(int oldfd);

    [DllImport("libc")]
    private static extern int dup2(int oldfd, int newfd);

    [DllImport("libc", CharSet = CharSet.Ansi)]
    private static extern int open(string pathname, int flags);

    [DllImport("libc")]
    private static extern int close(int fd);

    [DllImport("libc")]
    private static extern IntPtr write(int fd, byte[] buffer, IntPtr count);

    [DllImport("libc")]
    private static extern int isatty(int fd);

    [DllImport("libc")]
    private static extern IntPtr signal(int signum, UnixSignalHandler handler);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    private delegate bool ConsoleCtrlHandler(uint ctrlType);
    private delegate void UnixSignalHandler(int signal);
}
