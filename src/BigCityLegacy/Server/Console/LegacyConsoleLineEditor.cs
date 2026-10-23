using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

internal enum LegacyConsoleKey { Up, Down, Left, Right, Home, End, Backspace, Delete, Clear }

// Text/history only. No Unity, terminal queries or absolute cursor positions.
internal sealed class LegacyConsoleLineEditor
{
    internal const int HistoryLimit = 100;
    internal const int LineLimit = 4096;
    private readonly List<string> history = new List<string>();
    private int historyIndex;
    private string draft = "";
    internal string Text { get; private set; } = "";
    internal int Cursor { get; private set; }

    internal void BeginLine()
    {
        Text = draft = "";
        Cursor = 0;
        historyIndex = history.Count;
    }

    internal string Commit()
    {
        string command = Text;
        if (!string.IsNullOrWhiteSpace(command) && (history.Count == 0 || history[history.Count - 1] != command))
        {
            history.Add(command);
            if (history.Count > HistoryLimit) history.RemoveAt(0);
        }
        return command;
    }

    internal void Insert(string value)
    {
        if (Text.Length + value.Length > LineLimit) return;
        Text = Text.Insert(Cursor, value);
        Cursor += value.Length;
    }

    internal void Edit(LegacyConsoleKey key)
    {
        int[] indices = StringInfo.ParseCombiningCharacters(Text);
        int previous = 0, next = Text.Length;
        foreach (int index in indices)
        {
            if (index < Cursor) previous = index;
            else if (index > Cursor) { next = index; break; }
        }
        switch (key)
        {
            case LegacyConsoleKey.Up:
                if (historyIndex == 0) break;
                if (historyIndex == history.Count) draft = Text;
                Text = history[--historyIndex]; Cursor = Text.Length;
                break;
            case LegacyConsoleKey.Down:
                if (historyIndex >= history.Count) break;
                Text = ++historyIndex == history.Count ? draft : history[historyIndex]; Cursor = Text.Length;
                break;
            case LegacyConsoleKey.Left: Cursor = previous; break;
            case LegacyConsoleKey.Right: Cursor = next; break;
            case LegacyConsoleKey.Home: Cursor = 0; break;
            case LegacyConsoleKey.End: Cursor = Text.Length; break;
            case LegacyConsoleKey.Backspace:
                if (Cursor != 0) { Text = Text.Remove(previous, Cursor - previous); Cursor = previous; }
                break;
            case LegacyConsoleKey.Delete:
                if (Cursor < Text.Length) Text = Text.Remove(Cursor, next - Cursor);
                break;
            case LegacyConsoleKey.Clear: Text = ""; Cursor = 0; break;
        }
    }

    internal string Render(int columns)
    {
        // Keep a spare terminal cell and scroll horizontally rather than wrap.
        // Recompute from the current width on every draw, including log output.
        int available = Math.Max(0, Math.Min(columns, LineLimit) - 3);
        int[] indices = StringInfo.ParseCombiningCharacters(Text);
        int cursorWidth = 0;
        foreach (int index in indices) if (index < Cursor) cursorWidth += CellWidth(Text, index);
        int first = 0, skippedWidth = 0;
        while (first < indices.Length && cursorWidth - skippedWidth > available)
            skippedWidth += CellWidth(Text, indices[first++]);
        var visible = new StringBuilder();
        int width = 0;
        for (int i = first; i < indices.Length; i++)
        {
            int size = CellWidth(Text, indices[i]);
            if (width + size > available) break;
            int end = i + 1 < indices.Length ? indices[i + 1] : Text.Length;
            visible.Append(Text, indices[i], end - indices[i]);
            width += size;
        }
        int left = Math.Max(0, width - (cursorWidth - skippedWidth));
        return "\r\u001b[2K> " + visible + (left == 0 ? "" : "\u001b[" + left.ToString(CultureInfo.InvariantCulture) + "D");
    }

    private static int CellWidth(string text, int index)
    {
        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(text, index);
        if (category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.EnclosingMark || category == UnicodeCategory.Format) return 0;
        int code = char.ConvertToUtf32(text, index);
        return code >= 0x1100 && (code <= 0x115f || code == 0x2329 || code == 0x232a ||
            (code >= 0x2e80 && code <= 0xa4cf && code != 0x303f) || (code >= 0xac00 && code <= 0xd7a3) ||
            (code >= 0xf900 && code <= 0xfaff) || (code >= 0xfe10 && code <= 0xfe19) ||
            (code >= 0xfe30 && code <= 0xfe6f) || (code >= 0xff00 && code <= 0xff60) ||
            (code >= 0xffe0 && code <= 0xffe6) || (code >= 0x1f300 && code <= 0x1faff) || code >= 0x20000) ? 2 : 1;
    }
}
