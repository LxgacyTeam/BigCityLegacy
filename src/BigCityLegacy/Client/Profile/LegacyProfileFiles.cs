using System;
using System.IO;
using System.Text;

internal static class LegacyProfileFiles
{
    internal static string Filename(string nickname)
    {
        var name = new StringBuilder();
        foreach (char c in nickname ?? "Player")
            if (!char.IsControl(c) && "<>:\"/\\|?*".IndexOf(c) < 0 && name.Length < 80) name.Append(c);
        string result = name.ToString().Trim().TrimEnd('.');
        if (result.Length > 0 && char.IsHighSurrogate(result[result.Length - 1])) result = result.Substring(0, result.Length - 1);
        string stem = result.Split('.')[0].ToUpperInvariant();
        if (string.IsNullOrEmpty(result) || stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" ||
            System.Text.RegularExpressions.Regex.IsMatch(stem, @"^(COM|LPT)[1-9]$")) result = "Player";
        return result + ".profile";
    }
    internal static void Write(string path, byte[] bytes)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { file.Write(bytes, 0, bytes.Length); file.Flush(true); }
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    internal static LegacyProfileData Read(string path, string password)
    {
        if (!string.Equals(Path.GetExtension(path), ".profile", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Select a .profile file.");
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            if (file.Length == 0 || file.Length > LegacyProfileCodec.MaxBytes) throw new InvalidDataException("Invalid player profile size.");
            byte[] bytes = new byte[(int)file.Length]; int offset = 0;
            while (offset < bytes.Length)
            { int count = file.Read(bytes, offset, bytes.Length - offset); if (count == 0) throw new EndOfStreamException(); offset += count; }
            return LegacyProfileCodec.Decode(bytes, password);
        }
    }
}
