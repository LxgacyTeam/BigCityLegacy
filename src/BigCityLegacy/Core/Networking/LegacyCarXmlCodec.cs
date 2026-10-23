using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

// Keep the stock SyncVar a stock-readable XML document. Large originals travel
// separately; changing UNet's string limit would break its 16-bit wire framing.
internal static class LegacyCarXmlCodec
{
    internal const int WireLimit = 24000, MaxBytes = 4 * 1024 * 1024;
    internal const string Marker = "bcl.xml.v1";
    internal static readonly Encoding Utf8 = new UTF8Encoding(false, true);

    internal static bool IsLarge(string xml) { return xml != null && Utf8.GetByteCount(xml) > WireLimit; }
    internal static bool IsCarXml(string xml)
    {
        if (string.IsNullOrEmpty(xml)) return false;
        try
        {
            using (var source = new StringReader(xml))
            using (var reader = XmlReader.Create(source, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null, MaxCharactersInDocument = LegacyVinylPackCodec.MaxCharacters }))
                return reader.MoveToContent() == XmlNodeType.Element && reader.Name == "CarMat";
        }
        catch (XmlException) { return false; }
    }
    internal static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    internal static bool IsHash(string value)
    {
        if (value == null || value.Length != 64) return false;
        foreach (char c in value) if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
        return true;
    }
    internal static string Token(string xml)
    {
        if (string.IsNullOrEmpty(xml) || xml.Length > WireLimit || xml.IndexOf(Marker, StringComparison.Ordinal) < 0) return null;
        try
        {
            using (var source = new StringReader(xml))
            using (var reader = XmlReader.Create(source, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null, MaxCharactersInDocument = WireLimit }))
            {
                if (reader.MoveToContent() != XmlNodeType.Element || reader.Name != "CarMat") return null;
                string token = reader.GetAttribute(Marker);
                return IsHash(token) ? token : null;
            }
        }
        catch (XmlException) { return null; }
    }
    internal static string MakeWire(string xml, out string token)
    {
        byte[] bytes = Utf8.GetBytes(xml);
        if (bytes.Length > MaxBytes) throw new InvalidDataException("Car XML exceeds 4 MiB.");
        XmlDocument doc = LegacyVinylPackCodec.ReadXml(xml);
        XmlElement root = doc.DocumentElement;
        if (root == null || root.Name != "CarMat" || root.HasAttribute(Marker)) throw new InvalidDataException("Invalid car XML root.");
        XmlElement items = root.SelectSingleNode("CarPaint/Items") as XmlElement;
        if (root.SelectNodes("CarPaint/Items/Item | EnhancedGlassPaint/Items/Item").Count > LegacyVinylPackCodec.MaxLayers) throw new InvalidDataException("Car XML exceeds 4096 vinyl layers.");
        token = Hash(bytes);
        root.SetAttribute(Marker, token);
        string wire = doc.OuterXml;
        // A vanilla client can still show as many original layers as fit safely.
        int size = Utf8.GetByteCount(wire);
        while (size > WireLimit && items != null && items.HasChildNodes)
        {
            size -= Utf8.GetByteCount(items.LastChild.OuterXml);
            items.RemoveChild(items.LastChild);
        }
        wire = doc.OuterXml;
        if (IsLarge(wire)) throw new InvalidDataException("Car XML outside the vinyl list exceeds the network limit.");
        return wire;
    }
    internal static byte[] Pack(string xml)
    {
        byte[] input = Utf8.GetBytes(xml);
        if (input.Length > MaxBytes) throw new InvalidDataException("Car XML exceeds 4 MiB.");
        using (var output = new MemoryStream())
        {
            using (var zip = new DeflateStream(output, CompressionMode.Compress, true)) zip.Write(input, 0, input.Length);
            if (output.Length > MaxBytes) throw new InvalidDataException("Compressed car XML exceeds 4 MiB.");
            return output.ToArray();
        }
    }
    internal static string Unpack(byte[] packed, string hash)
    {
        if (packed == null || packed.Length > MaxBytes || !IsHash(hash)) throw new InvalidDataException("Invalid car XML payload.");
        using (var input = new MemoryStream(packed, false))
        using (var zip = new DeflateStream(input, CompressionMode.Decompress))
        using (var output = new MemoryStream())
        {
            byte[] buffer = new byte[8192];
            int count;
            while ((count = zip.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (output.Length + count > MaxBytes) throw new InvalidDataException("Expanded car XML exceeds 4 MiB.");
                output.Write(buffer, 0, count);
            }
            byte[] bytes = output.ToArray();
            if (Hash(bytes) != hash) throw new InvalidDataException("Car XML checksum mismatch.");
            string xml = Utf8.GetString(bytes);
            string unused;
            MakeWire(xml, out unused); // Validate XML and layer limits before handing it to the game.
            return xml;
        }
    }
}
