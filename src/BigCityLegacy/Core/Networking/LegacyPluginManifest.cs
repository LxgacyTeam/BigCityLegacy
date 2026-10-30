using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

internal sealed class LegacyPluginRecord
{
    public string Guid, Version, Name;
}

// No JSON deserialization or compressed data on the admission path. All lengths are bounded.
internal static class LegacyPluginManifest
{
    internal const string Attribute = "BCLPluginsV1";
    internal const string VersionAttribute = "BCLVersion";
    internal const int MaxPlugins = 256, MaxBytes = 20000, MaxXmlBytes = 30000;
    private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
    internal static string Encode(IList<LegacyPluginRecord> records)
    {
        if (records == null || records.Count > MaxPlugins) throw new FormatException("Too many plugins (maximum 256).");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream, Utf8))
        {
            writer.Write((byte)1); writer.Write((ushort)records.Count);
            foreach (var record in records)
            {
                Validate(record, seen);
                Write(writer, record.Guid); Write(writer, record.Version); Write(writer, record.Name);
            }
            if (stream.Length > MaxBytes) throw new FormatException("Plugin manifest exceeds 20000 bytes.");
            return Convert.ToBase64String(stream.ToArray());
        }
    }
    internal static List<LegacyPluginRecord> Decode(string value)
    {
        if (value == null || value.Length > ((MaxBytes + 2) / 3) * 4) throw new FormatException("Plugin manifest is too large.");
        byte[] bytes = Convert.FromBase64String(value);
        if (bytes.Length > MaxBytes) throw new FormatException("Plugin manifest is too large.");
        var result = new List<LegacyPluginRecord>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using (var stream = new MemoryStream(bytes, false))
        using (var reader = new BinaryReader(stream, Utf8))
        {
            if (reader.ReadByte() != 1) throw new FormatException("Unsupported plugin manifest version.");
            int count = reader.ReadUInt16();
            if (count > MaxPlugins) throw new FormatException("Too many plugins.");
            for (int i = 0; i < count; i++)
            {
                var record = new LegacyPluginRecord { Guid = Read(reader, 128), Version = Read(reader, 64), Name = Read(reader, 128) };
                Validate(record, seen); result.Add(record);
            }
            if (stream.Position != stream.Length) throw new FormatException("Trailing plugin manifest data.");
        }
        return result;
    }
    private static void Validate(LegacyPluginRecord record, HashSet<string> seen)
    {
        if (record == null || string.IsNullOrWhiteSpace(record.Guid) || record.Guid.Length > 128 ||
            record.Version == null || record.Version.Length == 0 || record.Version.Length > 64 || record.Name == null || record.Name.Length > 128 ||
            record.Guid != record.Guid.Trim() || HasControl(record.Guid) || HasControl(record.Version) || HasControl(record.Name) || !seen.Add(record.Guid))
            throw new FormatException("Invalid or duplicate plugin metadata.");
        LegacyPluginVersion.Parse(record.Version);
    }
    private static bool HasControl(string text) { foreach (char ch in text) if (char.IsControl(ch)) return true; return false; }
    private static void Write(BinaryWriter writer, string text) { byte[] bytes = Utf8.GetBytes(text); writer.Write((ushort)bytes.Length); writer.Write(bytes); }
    private static string Read(BinaryReader reader, int maximum)
    {
        int count = reader.ReadUInt16();
        if (count > maximum * 4 || count > reader.BaseStream.Length - reader.BaseStream.Position) throw new FormatException("Invalid plugin metadata length.");
        string value = Utf8.GetString(reader.ReadBytes(count));
        if (value.Length > maximum) throw new FormatException("Plugin metadata exceeds field limit.");
        return value;
    }
    internal static XmlDocument LoadXml(string xml)
    {
        if (xml == null || Utf8.GetByteCount(xml) > MaxXmlBytes) throw new FormatException("Player XML exceeds admission limit.");
        var document = new XmlDocument { XmlResolver = null, PreserveWhitespace = true };
        using (var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxXmlBytes })) document.Load(reader);
        if (document.DocumentElement == null) throw new FormatException("Missing player XML root.");
        return document;
    }
    internal static string Attach(string xml, IList<LegacyPluginRecord> records, string bclVersion = null)
    {
        var document = LoadXml(xml); document.DocumentElement.SetAttribute(Attribute, Encode(records));
        if (bclVersion != null) { LegacyPluginVersion.Parse(bclVersion); document.DocumentElement.SetAttribute(VersionAttribute, bclVersion); }
        string result = document.OuterXml;
        if (Utf8.GetByteCount(result) > MaxXmlBytes) throw new FormatException("Player XML and plugin manifest exceed 30000 bytes.");
        return result;
    }
    internal static List<LegacyPluginRecord> Extract(ref string xml, out bool advertised, out string bclVersion)
    {
        var document = LoadXml(xml); advertised = document.DocumentElement.HasAttribute(Attribute);
        bclVersion = document.DocumentElement.HasAttribute(VersionAttribute) ? document.DocumentElement.GetAttribute(VersionAttribute) : null;
        if (bclVersion != null) { LegacyPluginVersion.Parse(bclVersion); document.DocumentElement.RemoveAttribute(VersionAttribute); }
        if (!advertised) { xml = document.OuterXml; return null; }
        string value = document.DocumentElement.GetAttribute(Attribute);
        document.DocumentElement.RemoveAttribute(Attribute); xml = document.OuterXml;
        return Decode(value);
    }
}
