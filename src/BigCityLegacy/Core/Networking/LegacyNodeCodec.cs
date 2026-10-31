using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

internal static class LegacyNodeCodec
{
    internal const int MaxBytes = 2048;
    internal sealed class Record
    {
        internal byte Platform;
        internal readonly SortedDictionary<byte, string> Parts = new SortedDictionary<byte, string>();
    }
    internal static string Normalize(string value)
    {
        if (value == null) return null;
        value = new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
        if (value.Length < 4 || value.Length > 256 || value.Any(c => c < 33 || c > 126)) return null;
        string simple = new string(value.Where(char.IsLetterOrDigit).ToArray());
        if (simple.Length < 4 || simple.All(c => c == '0') || simple.All(c => c == 'F') ||
            new[] { "UNKNOWN", "NONE", "NULL", "DEFAULTSTRING", "TOBEFILLEDBYOEM", "NOTSPECIFIED", "NOTAPPLICABLE", "SYSTEMSERIALNUMBER", "SERIALNUMBER", "123456789", "INVALID", "OEM" }.Contains(simple)) return null;
        return value;
    }
    internal static void Put(Record record, byte key, string value)
    { value = Normalize(value); if (value != null) record.Parts[key] = value; }
    internal static int Group(byte key)
    { switch (key) { case 1: case 4: return 1; case 2: return 2; case 3: case 5: return 3; case 6: return 4; default: throw new FormatException("Invalid node field."); } }
    internal static int Groups(Record record) { return record.Parts.Keys.Select(Group).Distinct().Count(); }
    internal static bool Usable(string value)
    { try { return !string.IsNullOrEmpty(value) && Groups(Decode(value)) >= 2; } catch { return false; } }
    internal static bool Matches(string left, string right)
    {
        if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right)) return false;
        Record a, b;
        try { a = Decode(left); b = Decode(right); } catch { return false; }
        if (Groups(a) < 2 || Groups(b) < 2) return false;
        var groups = new HashSet<int>();
        foreach (var p in a.Parts) { string v; if (b.Parts.TryGetValue(p.Key, out v) && p.Value == v) groups.Add(Group(p.Key)); }
        return groups.Count >= Math.Max(2, Math.Max(Groups(a), Groups(b)) - 1);
    }
    internal static string Encode(Record record)
    {
        if (record.Parts.Count == 0) return null;
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Encoding.UTF8))
        {
            writer.Write((byte)0xB1); writer.Write((byte)1); writer.Write(record.Platform); writer.Write((byte)record.Parts.Count);
            foreach (var p in record.Parts) { byte[] value = Encoding.UTF8.GetBytes(p.Value); writer.Write(p.Key); writer.Write((ushort)value.Length); writer.Write(value); }
            byte[] body = stream.ToArray(); using (var hash = SHA256.Create()) writer.Write(hash.ComputeHash(body), 0, 8);
            byte[] bytes = stream.ToArray(); if (bytes.Length > MaxBytes) throw new FormatException("Node record too large.");
            for (int i = 0; i < bytes.Length; i++) bytes[i] ^= Mask(i);
            string result = BitConverter.ToString(bytes).Replace("-", ""); Decode(result); return result;
        }
    }
    private static byte Mask(int index) { return (byte)(0x6D ^ ((index * 73 + 41) & 255)); }
    internal static Record Decode(string value)
    {
        if (value == null || value.Length < 24 || value.Length > MaxBytes * 2 || value.Length % 2 != 0) throw new FormatException("Invalid node length.");
        byte[] data = new byte[value.Length / 2];
        for (int i = 0; i < data.Length; i++)
        {
            int hi = Hex(value[i*2]), lo = Hex(value[i*2+1]); if (hi < 0 || lo < 0) throw new FormatException("Invalid node hex.");
            data[i] = (byte)((hi * 16 + lo) ^ Mask(i));
        }
        using (var hash = SHA256.Create())
        { byte[] check = hash.ComputeHash(data, 0, data.Length - 8); for (int i = 0; i < 8; i++) if (check[i] != data[data.Length-8+i]) throw new FormatException("Invalid node checksum."); }
        using (var stream = new MemoryStream(data, 0, data.Length-8, false))
        using (var reader = new BinaryReader(stream, new UTF8Encoding(false,true)))
        {
            if (reader.ReadByte() != 0xB1 || reader.ReadByte() != 1) throw new FormatException("Unsupported node record.");
            var result = new Record { Platform = reader.ReadByte() }; int count = reader.ReadByte();
            if ((result.Platform != 1 && result.Platform != 2) || count < 1 || count > 6) throw new FormatException("Invalid node header.");
            byte previous = 0;
            for (int i=0; i<count; i++)
            {
                byte key = reader.ReadByte(); Group(key);
                if (key <= previous) throw new FormatException("Duplicate or unordered node field."); previous = key;
                int length = reader.ReadUInt16(); if (length < 4 || length > 256 || length > stream.Length-stream.Position) throw new FormatException("Invalid node field length.");
                string part = new UTF8Encoding(false,true).GetString(reader.ReadBytes(length));
                if (Normalize(part) != part) throw new FormatException("Invalid node field value."); result.Parts.Add(key,part);
            }
            if (stream.Position != stream.Length) throw new FormatException("Trailing node data."); return result;
        }
    }
    private static int Hex(char c) { return c >= '0' && c <= '9' ? c-'0' : c >= 'a' && c <= 'f' ? c-'a'+10 : c >= 'A' && c <= 'F' ? c-'A'+10 : -1; }
}
