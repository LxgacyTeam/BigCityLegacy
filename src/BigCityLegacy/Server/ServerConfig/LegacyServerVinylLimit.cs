using System;
using System.Globalization;
using System.IO;
using System.Xml;
using UnityEngine;

internal static class LegacyServerVinylLimit
{
    internal const int Default = 1024;
    internal static int Maximum { get; private set; } = Default;
    internal static void Configure(string[] args)
    {
        string text = LegacyCommandLine.GetArgValue(args, "-maxVinylLayers");
        int value = Default;
        if (text != null && (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < 0 || value > LegacyVinylPackCodec.MaxLayers))
        { value = Default; Debug.LogWarning("[BCL] Invalid maxVinylLayers; using " + Default + "."); }
        Maximum = value == 0 ? LegacyVinylPackCodec.MaxLayers : value;
    }
    internal static int Count(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return 0;
        XmlDocument doc = LegacyVinylPackCodec.ReadXml(xml);
        if (doc.DocumentElement == null || doc.DocumentElement.Name != "CarMat") throw new InvalidDataException("Expected CarMat XML.");
        return doc.SelectNodes("/CarMat/CarPaint/Items/Item | /CarMat/EnhancedGlassPaint/Items/Item").Count;
    }
    internal static void Validate(string xml)
    {
        int count = Count(xml);
        if (count > Maximum) throw new InvalidDataException("Vehicle vinyl layer limit exceeded: " + count + " > " + Maximum + ".");
    }
    internal static bool Accept(string xml)
    {
        try { Validate(xml); return true; }
        catch (Exception ex) { Debug.LogWarning("[BCL vinyl] " + ex.Message); return false; }
    }
}
