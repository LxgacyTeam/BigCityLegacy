using System;
using System.Xml;
using BepInEx.Configuration;

internal static class LegacyRaceCarRules
{
    private static ConfigEntry<bool> nitroPurchase;
    internal static bool AllowNitroPurchase => nitroPurchase == null || nitroPurchase.Value;
    internal static void Configure(ConfigFile config)
    {
        nitroPurchase = config.Bind("OnlineRace", "AllowNitroPurchase", true,
            "Allow buying nitro during online races hosted by this server.");
    }
    internal static string StockWithPaint(string xml)
    {
        if (string.IsNullOrEmpty(xml)) return string.Empty;
        try
        {
            var source = LegacyVinylPackCodec.ReadXml(xml).DocumentElement;
            if (source == null || source.Name != "CarMat") return string.Empty;
            var document = new XmlDocument { XmlResolver = null };
            var root = document.CreateElement("CarMat");
            document.AppendChild(root);
            foreach (XmlNode child in source.ChildNodes)
            {
                if (!(child is XmlElement node)) continue;
                if (node.Name == "CarPaint" || node.Name == "CarMaterial" ||
                    node.Name == "EnhancedBodyPaint" || node.Name == "EnhancedGlassPaint")
                    root.AppendChild(document.ImportNode(node, true));
            }
            return document.OuterXml;
        }
        catch (Exception ex) when (ex is XmlException || ex is ArgumentException)
        {
            return string.Empty;
        }
    }
}
