using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using Debug = UnityEngine.Debug;

internal static class NativeErrorDialog
{
    /// <summary>
    /// Shows a blocking error dialog with a single OK button.
    /// Returns false if no graphical dialog could be shown.
    /// </summary>
    public static bool Show(string title, string message)
    {
        title ??= "Error";
        message ??= string.Empty;

        try
        {
            if (Application.platform == RuntimePlatform.WindowsPlayer)
                return ShowWindows(title, message);

            if (Application.platform == RuntimePlatform.LinuxPlayer)
                return ShowLinux(title, message);

            if (Application.platform == RuntimePlatform.OSXPlayer)
                return ShowMacOS(title, message);
        }
        catch (Exception ex)
        {
            WriteFallback(title, message, ex);
            return false;
        }

        WriteFallback(title, message);
        return false;
    }

    public static void ShowAndExit(
        string title,
        string message,
        int exitCode = 1)
    {
        Show(title, message);
        Application.Quit(exitCode);
    }


    // Windows
    private const uint MB_OK = 0x00000000;
    private const uint MB_ICONERROR = 0x00000010;
    private const uint MB_SETFOREGROUND = 0x00010000;
    private const uint MB_TOPMOST = 0x00040000;

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern int MessageBoxW(
        IntPtr hWnd,
        string lpText,
        string lpCaption,
        uint uType);

    private static bool ShowWindows(string title, string message)
    {
        MessageBoxW(
            IntPtr.Zero,
            message,
            title,
            MB_OK |
            MB_ICONERROR |
            MB_SETFOREGROUND |
            MB_TOPMOST);

        return true;
    }

    // Linux
    private static bool ShowLinux(string title, string message)
    {
        // KDE
        if (TryRun(
                "kdialog",
                "--error", message,
                "--title", title))
        {
            return true;
        }

        // GTK / GNOME
        if (TryRun(
                "zenity",
                "--error",
                "--title", title,
                "--text", message))
        {
            return true;
        }

        WriteFallback(title, message);
        return false;
    }

    // macOS
    private static bool ShowMacOS(string title, string message)
    {
        // AppleScript used
        const string script =
            "on run argv\n" +
            "display alert (item 1 of argv) " +
            "message (item 2 of argv) " +
            "as critical buttons {\"OK\"} default button \"OK\"\n" +
            "end run";

        return TryRun(
            "/usr/bin/osascript",
            "-e", script,
            title,
            message);
    }

    private static bool TryRun(
        string executable,
        params string[] arguments)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = JoinArguments(arguments),

                UseShellExecute = false,
                CreateNoWindow = false,

                RedirectStandardOutput = false,
                RedirectStandardError = false
            };

            using (var process = Process.Start(startInfo))
            {
                if (process == null)
                    return false;

                process.WaitForExit();

                return process.ExitCode == 0;
            }
        }
        catch
        {
            
            return false;
        }
    }

    private static string JoinArguments(string[] arguments)
    {
        var result = new StringBuilder();

        for (int i = 0; i < arguments.Length; i++)
        {
            if (i != 0)
                result.Append(' ');

            AppendQuotedArgument(result, arguments[i] ?? string.Empty);
        }

        return result.ToString();
    }

    private static void AppendQuotedArgument(
        StringBuilder result,
        string argument)
    {
        result.Append('"');

        int backslashes = 0;

        foreach (char c in argument)
        {
            if (c == '\\')
            {
                backslashes++;
                continue;
            }

            if (c == '"')
            {
                result.Append('\\', backslashes * 2 + 1);
                result.Append('"');
                backslashes = 0;
                continue;
            }

            result.Append('\\', backslashes);
            backslashes = 0;
            result.Append(c);
        }

        result.Append('\\', backslashes * 2);
        result.Append('"');
    }

    private static void WriteFallback(
        string title,
        string message,
        Exception exception = null)
    {
        try
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("========================================");
            Console.Error.WriteLine(title);
            Console.Error.WriteLine("----------------------------------------");
            Console.Error.WriteLine(message);

            if (exception != null)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine(exception);
            }

            Console.Error.WriteLine("========================================");
            Console.Error.WriteLine();
        }
        catch
        {
        }
    }
}