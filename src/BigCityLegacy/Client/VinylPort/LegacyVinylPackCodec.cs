using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

internal sealed class LegacyVinylPack
{
    internal XmlDocument Document;
    internal string Car;
    internal int Version, TextCount, PartCount;
}

internal sealed class LegacyVinylPackException : Exception
{
    internal readonly string Code;
    internal LegacyVinylPackException(string code) : base(code) { Code = code; }
}

internal static class LegacyVinylPackCodec
{
    internal const int CurrentVersion = 4;
    internal const string PartsNode = "EnhancedBodyPaint";
    internal const int MaxCharacters = 2 * 1024 * 1024;
    private static readonly string[] parts = { "Body", "Hood", "Front", "Trunk", "Rear", "Doors" };

    internal static XmlDocument ReadXml(string xml)
    {
        var document = new XmlDocument { XmlResolver = null };
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
            MaxCharactersInDocument = MaxCharacters, IgnoreComments = true, IgnoreWhitespace = true,
            IgnoreProcessingInstructions = true };
        using (var source = new StringReader(xml))
        using (var reader = XmlReader.Create(source, settings)) document.Load(reader);
        if (document.FirstChild is XmlDeclaration) document.RemoveChild(document.FirstChild);
        return document;
    }

    internal static LegacyVinylPack ReadFile(string path, string car)
    {
        if (new FileInfo(path).Length > MaxCharacters * 4L) Fail("Size");
        return Read(File.ReadAllText(path), car);
    }

    internal static LegacyVinylPack Read(string xml, string car)
    {
        XmlDocument document = ReadXml(xml);
        var root = document.DocumentElement;
        if (root == null || root.Name != "VinylPack") Fail("Root");
        string version = root.GetAttribute("version");
        if (version.Length == 0) Fail("MissingVersion");
        if (!int.TryParse(version, NumberStyles.None, CultureInfo.InvariantCulture, out int number) ||
            number.ToString(CultureInfo.InvariantCulture) != version) Fail("Version");
        if (number < 1 || number > CurrentVersion) Fail("UnsupportedVersion");
        string packCar = root.GetAttribute("car");
        if (string.IsNullOrWhiteSpace(packCar) || packCar.Length > 200 ||
            !string.Equals(packCar, car, StringComparison.OrdinalIgnoreCase)) Fail("Car");
        var result = new LegacyVinylPack { Document = document, Car = packCar, Version = number };
        XmlElement paint = Single(root, "CarPaint", true);
        XmlElement items = Single(paint, "Items", true);
        if (items.ChildNodes.Count > 256) Fail("Items");
        foreach (XmlNode child in items.ChildNodes)
        {
            if (!(child is XmlElement item) || item.Name != "Item") Fail("Items");
            ValidateItem((XmlElement)child, result);
        }
        XmlElement material = Single(root, "CarMaterial", false);
        if (material != null)
        {
            ValidateMaterial(Single(material, "Body", true));
            ValidateMaterial(Single(material, "Wheels", true));
        }
        XmlElement overrides = Single(root, PartsNode, false);
        if (overrides != null)
        {
            if (material == null || overrides.GetAttribute("version") != "1" || overrides.ChildNodes.Count > 6) Fail("Parts");
            int seen = 0;
            foreach (XmlNode child in overrides.ChildNodes)
            {
                if (!(child is XmlElement part) || part.Name != "Part") Fail("Parts");
                var element = (XmlElement)child;
                int index = Array.IndexOf(parts, element.GetAttribute("id"));
                if (index < 0 || (seen & (1 << index)) != 0) Fail("Parts");
                seen |= 1 << index;
                Numbers(element.GetAttribute("color"), 3, true, "Parts");
                Number(element.GetAttribute("metallic"), true, "Parts");
                Number(element.GetAttribute("gloss"), true, "Parts");
                result.PartCount++;
            }
        }
        return result;
    }

    private static void ValidateItem(XmlElement item, LegacyVinylPack pack)
    {
        string name = item.GetAttribute("sprite.name");
        if (string.IsNullOrEmpty(name) || name.Length > 512) Fail("Items");
        string symmetry = item.GetAttribute("ect.symmetry");
        if (item.HasAttribute("ect.symmetry") && symmetry != "0" && symmetry != "1") Fail("Text");
        foreach (XmlAttribute attribute in item.Attributes)
            if (attribute.Name.StartsWith("ect.", StringComparison.Ordinal) && attribute.Name != "ect.text" &&
                attribute.Name != "ect.font" && attribute.Name != "ect.style" && attribute.Name != "ect.symmetry") Fail("Text");
        bool fields = item.HasAttribute("ect.text") || item.HasAttribute("ect.font") || item.HasAttribute("ect.style");
        bool encoded = name.StartsWith("ect.text.", StringComparison.Ordinal);
        if (name == "ECT.Text" || encoded)
        {
            string text;
            int font, style;
            if (encoded)
            {
                if (!name.StartsWith("ect.text.v1:", StringComparison.Ordinal) || name.Length > 360) Fail("Text");
                string[] tokens = name.Substring("ect.text.v1:".Length).Split(':');
                if (tokens.Length != 3) Fail("Text");
                font = Index(tokens[0], 2); style = Index(tokens[1], 3);
                try
                {
                    byte[] bytes = Convert.FromBase64String(tokens[2]);
                    if (Convert.ToBase64String(bytes) != tokens[2]) Fail("Text");
                    text = new UTF8Encoding(false, true).GetString(bytes);
                }
                catch (Exception error) when (error is FormatException || error is ArgumentException) { throw new LegacyVinylPackException("Text"); }
                if (fields && (!Complete(item) || item.GetAttribute("ect.text") != text ||
                    Index(item.GetAttribute("ect.font"), 2) != font || Index(item.GetAttribute("ect.style"), 3) != style)) Fail("Text");
            }
            else
            {
                if (!Complete(item)) Fail("Text");
                text = item.GetAttribute("ect.text");
                font = Index(item.GetAttribute("ect.font"), 2); style = Index(item.GetAttribute("ect.style"), 3);
            }
            if (text.Length > 80) Fail("Text");
            foreach (char c in text) if (char.IsControl(c)) Fail("Text");
            try { new UTF8Encoding(false, true).GetBytes(text); }
            catch (ArgumentException) { throw new LegacyVinylPackException("Text"); }
            item.SetAttribute("sprite.name", "ECT.Text");
            item.SetAttribute("ect.text", text);
            item.SetAttribute("ect.font", font.ToString(CultureInfo.InvariantCulture));
            item.SetAttribute("ect.style", style.ToString(CultureInfo.InvariantCulture));
            pack.TextCount++;
        }
        else if (fields) Fail("Text");
        foreach (string vector in new[] { "pos", "rot" })
            if (item.HasAttribute(vector)) Numbers(item.GetAttribute(vector), 3, false, "Geometry");
        if (item.HasAttribute("color")) Numbers(item.GetAttribute("color"), 4, true, "Geometry");
        foreach (string scalar in new[] { "size_y", "acpect", "depth" })
            if (item.HasAttribute(scalar))
            {
                float value = Number(item.GetAttribute(scalar), false, "Geometry");
                if (scalar == "depth" ? value <= 0 : value == 0) Fail("Geometry");
            }
    }

    private static bool Complete(XmlElement item) => item.HasAttribute("ect.text") && item.HasAttribute("ect.font") && item.HasAttribute("ect.style");
    private static int Index(string text, int max)
    {
        if (text.Length != 1 || text[0] < '0' || text[0] > '0' + max) Fail("Text");
        return text[0] - '0';
    }
    private static void ValidateMaterial(XmlElement material)
    {
        if (material.HasAttribute("Color")) Numbers(material.GetAttribute("Color"), 4, true, "Material");
        foreach (string field in new[] { "Mettalic", "Gloss" })
            if (material.HasAttribute(field)) Number(material.GetAttribute(field), true, "Material");
    }
    private static float Number(string text, bool unit, string code)
    {
        float number = 0;
        if (text.Length > 32 || !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
            float.IsNaN(number) || float.IsInfinity(number) || unit && (number < 0 || number > 1)) Fail(code);
        return number;
    }
    private static void Numbers(string text, int count, bool unit, string code)
    {
        string[] values = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != count) Fail(code);
        foreach (string value in values) Number(value, unit, code);
    }
    private static XmlElement Single(XmlElement root, string name, bool required)
    {
        XmlElement found = null;
        foreach (XmlNode child in root.ChildNodes)
            if (child.Name == name)
            { if (found != null || !(child is XmlElement element)) Fail("Duplicate"); found = (XmlElement)child; }
        if (required && found == null) Fail("Structure");
        return found;
    }
    private static void Fail(string code) { throw new LegacyVinylPackException(code); }

    internal static XmlDocument Export(XmlDocument save, string car)
    {
        if (save.DocumentElement == null || save.DocumentElement.Name != "CarMat") Fail("Root");
        var document = new XmlDocument { XmlResolver = null };
        XmlElement root = document.CreateElement("VinylPack");
        root.SetAttribute("version", CurrentVersion.ToString(CultureInfo.InvariantCulture));
        root.SetAttribute("car", car); document.AppendChild(root);
        foreach (string node in new[] { "CarPaint", "CarMaterial", PartsNode })
        {
            XmlElement element = Single(save.DocumentElement, node, node == "CarPaint");
            if (element != null) root.AppendChild(document.ImportNode(element, true));
        }
        return Read(document.OuterXml, car).Document;
    }

    internal static XmlDocument Merge(LegacyVinylPack pack, string saveXml, string primitive)
    {
        var save = ReadXml(saveXml);
        var root = save.DocumentElement;
        if (root == null || root.Name != "CarMat" || root.HasAttribute("Name") &&
            !string.Equals(root.GetAttribute("Name"), pack.Car, StringComparison.OrdinalIgnoreCase)) Fail("Car");
        var copy = Read(pack.Document.OuterXml, pack.Car).Document.DocumentElement;
        if (primitive != null)
        {
            if (primitive.Length == 0 || primitive == "ECT.Text" || primitive.StartsWith("ect.", StringComparison.Ordinal)) Fail("Primitive");
            foreach (XmlElement item in copy.SelectNodes("CarPaint/Items/Item"))
                if (item.GetAttribute("sprite.name") == "ECT.Text")
                {
                    item.SetAttribute("sprite.name", primitive);
                    foreach (string field in new[] { "ect.text", "ect.font", "ect.style", "ect.symmetry" }) item.RemoveAttribute(field);
                }
        }
        Replace(root, copy["CarPaint"], "CarPaint");
        if (copy["CarMaterial"] != null)
        {
            Replace(root, copy["CarMaterial"], "CarMaterial");
            Replace(root, copy[PartsNode], PartsNode);
        }
        return save;
    }
    private static void Replace(XmlElement root, XmlElement source, string name)
    {
        for (int i = root.ChildNodes.Count - 1; i >= 0; i--)
            if (root.ChildNodes[i].Name == name) root.RemoveChild(root.ChildNodes[i]);
        if (source != null) root.AppendChild(root.OwnerDocument.ImportNode(source, true));
    }
}
