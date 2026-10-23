using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

// Linux standalone players use the Linux termios ABI.
// Reading /dev/stdin directly bypasses Mono's TermInfoDriver line editor,
// whose cached cursor coordinates can become invalid after terminal output
// or tmux resizing. Interactive editing/history use only relative ANSI moves.
internal sealed class LegacyUnixConsoleInput : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct Termios
    {
        internal uint Input, Output, Control, Local;
        internal byte Line;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
        internal byte[] Characters;
        internal uint InputSpeed, OutputSpeed;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowSize { internal ushort Rows, Columns, X, Y; }

    private const uint ICanonical = 0x2, Echo = 0x8, EchoErase = 0x10, EchoNl = 0x40, Signals = 0x1, CrToNl = 0x100;
    private readonly object sync = new object();
    private readonly TextReader reader;
    private readonly object outputSync;
    private readonly Action<string> output;
    private readonly Func<int> columns;
    private readonly bool interactive;
    private readonly LegacyConsoleLineEditor editor = new LegacyConsoleLineEditor();
    private bool editing;
    private Termios original;
    private bool restore;

    internal LegacyUnixConsoleInput(object outputSync, Action<string> output, bool ansi)
    {
        this.outputSync = outputSync;
        this.output = output;
        columns = TerminalColumns;
        try
        {
            bool terminal = isatty(0) == 1;
            interactive = terminal && ansi;
            if (terminal)
            {
                if (tcgetattr(0, out original) != 0) throw NativeError("tcgetattr");
                Termios cooked = original;
                cooked.Characters = (byte[])original.Characters.Clone();
                if (interactive)
                {
                    cooked.Local = (cooked.Local | Signals) & ~(ICanonical | Echo | EchoNl);
                    cooked.Characters[6] = 1; // VMIN: deliver individual keys.
                    cooked.Characters[5] = 0; // VTIME: blocking read, no polling.
                }
                else cooked.Local |= ICanonical | Echo | EchoErase | Signals;
                cooked.Input |= CrToNl;
                // TCSANOW changes modes without discarding typed input.
                if (tcsetattr(0, 0, ref cooked) != 0) throw NativeError("tcsetattr");
                restore = true;
            }
            reader = new StreamReader(new FileStream("/dev/stdin", FileMode.Open, FileAccess.Read, FileShare.ReadWrite),
                new UTF8Encoding(false), false, 1024);
        }
        catch
        {
            RestoreTerminal();
            throw;
        }
    }

    // The editor also accepts streams independently of native terminal setup.
    internal LegacyUnixConsoleInput(TextReader reader, object outputSync, Action<string> output, Func<int> columns, bool interactive = true)
    {
        this.reader = reader; this.outputSync = outputSync; this.output = output; this.columns = columns;
        this.interactive = interactive;
    }

    internal string ReadLine()
    {
        if (!interactive) return reader.ReadLine();
        lock (outputSync) { editor.BeginLine(); editing = true; Draw(); }
        string escape = null;
        while (true)
        {
            int value = reader.Read(); // Never hold the output lock while waiting for input.
            string text = value < 0 ? "" : ((char)value).ToString();
            if (value >= 0 && char.IsHighSurrogate((char)value))
                text = char.IsLowSurrogate((char)reader.Peek()) ? text + (char)reader.Read() : "\ufffd";
            lock (outputSync)
            {
                if (value < 0 || value == 4 && editor.Text.Length == 0)
                {
                    editing = false;
                    output("\r\u001b[2K");
                    return editor.Text.Length == 0 ? null : editor.Commit();
                }
                char key = (char)value;
                if (EscapeKey(ref escape, key)) { Draw(); continue; }
                if (key == '\r' || key == '\n')
                {
                    string command = editor.Commit();
                    // End at the visible line's right edge before printing a newline.
                    editor.Edit(LegacyConsoleKey.End); Draw(); output("\r\n");
                    editing = false;
                    return command;
                }
                switch (key)
                {
                    case '\b': case '\x7f': editor.Edit(LegacyConsoleKey.Backspace); break;
                    case '\x01': editor.Edit(LegacyConsoleKey.Home); break;
                    case '\x05': editor.Edit(LegacyConsoleKey.End); break;
                    case '\x04': editor.Edit(LegacyConsoleKey.Delete); break;
                    case '\x15': editor.Edit(LegacyConsoleKey.Clear); break;
                    default: if (!char.IsControl(key)) editor.Insert(text); break;
                }
                Draw();
            }
        }
    }

    // Called under the console's shared output lock so logs cannot consume or
    // overwrite a partly typed command. The input text/cursor remain unchanged.
    internal bool WriteOutput(string text)
    {
        lock (outputSync)
        {
            if (!interactive || !editing) return false;
            output("\r\u001b[2K" + text);
            Draw();
            return true;
        }
    }

    private void Draw() { output(editor.Render(columns())); }

    private bool EscapeKey(ref string sequence, char key)
    {
        if (key == '\u001b') { sequence = "\u001b"; return true; }
        if (sequence == null) return false;
        if (sequence.Length == 1)
        {
            if (key == '[' || key == 'O') { sequence += key; return true; }
            sequence = null; return false;
        }
        if (key < ' ' || key > '~') { sequence = null; return false; }
        sequence += key;
        if (key >= '@' && key <= '~')
        {
            switch (key)
            {
                case 'A': editor.Edit(LegacyConsoleKey.Up); break;
                case 'B': editor.Edit(LegacyConsoleKey.Down); break;
                case 'C': editor.Edit(LegacyConsoleKey.Right); break;
                case 'D': editor.Edit(LegacyConsoleKey.Left); break;
                case 'H': editor.Edit(LegacyConsoleKey.Home); break;
                case 'F': editor.Edit(LegacyConsoleKey.End); break;
                case '~':
                    if (sequence == "\u001b[3~") editor.Edit(LegacyConsoleKey.Delete);
                    else if (sequence == "\u001b[1~" || sequence == "\u001b[7~") editor.Edit(LegacyConsoleKey.Home);
                    else if (sequence == "\u001b[4~" || sequence == "\u001b[8~") editor.Edit(LegacyConsoleKey.End);
                    break;
            }
            sequence = null;
        }
        else if (sequence.Length > 16) sequence = null;
        return true;
    }

    private static int TerminalColumns()
    {
        WindowSize size;
        return ioctl(0, new UIntPtr(0x5413), out size) == 0 && size.Columns != 0 ? size.Columns : 80;
    }

    internal void RestoreTerminal()
    {
        lock (sync)
        {
            if (!restore) return;
            tcsetattr(0, 0, ref original);
            restore = false;
        }
    }

    public void Dispose()
    {
        lock (outputSync) editing = false;
        RestoreTerminal();
        try { reader?.Dispose(); } catch (IOException) { }
    }

    private static IOException NativeError(string operation)
    {
        return new IOException(operation + ": " + new Win32Exception(Marshal.GetLastWin32Error()).Message);
    }

    [DllImport("libc")] private static extern int isatty(int fd);
    [DllImport("libc")] private static extern int ioctl(int fd, UIntPtr request, out WindowSize size);
    [DllImport("libc", SetLastError = true)] private static extern int tcgetattr(int fd, out Termios attributes);
    [DllImport("libc", SetLastError = true)] private static extern int tcsetattr(int fd, int action, ref Termios attributes);
}
