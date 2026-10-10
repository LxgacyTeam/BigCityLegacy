using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml;

internal static class Program
{
    private static int checks;
    private const string Stock = "<Item pos='-0.9 0.1 0' rot='0 90 0' size_y='0.246' acpect='4.16' depth='2.5' color='1 1 1 1' sprite.name='Car_Vinyl_Aplication_11'/>";
    private const string Text = "<Item pos='-0.9 0.1 0' rot='0 90 0' size_y='0.246' acpect='4.16' depth='2.5' color='0 1 0 1' sprite.name='ECT.Text' ect.text='MAD OUT 2' ect.font='1' ect.style='3' ect.symmetry='1'/>";
    private const string Material = "<CarMaterial Texture=''><Body Color='0.2 0.3 0.4 1' Mettalic='0.2' Gloss='0.5'/><Wheels Color='1 1 1 1' Mettalic='0.5' Gloss='0'/></CarMaterial>";
    private const string Parts = "<EnhancedBodyPaint version='1'><Part id='Hood' color='1 0 0' metallic='0.7' gloss='0.8'/><Part id='Doors' color='0 0 1' metallic='0.2' gloss='0.4'/></EnhancedBodyPaint>";
    private const string Engine = "<ExtendedCarTuning engine='original'/>";
    private static string Pack(string items = Stock, string other = "", string version = "4") => "<VinylPack version='" + version + "' car='Gaz21'><CarPaint><Items>" + items + "</Items></CarPaint>" + other + "</VinylPack>";
    private static string Save(string other = Material + Parts + Engine) => "<CarMat Name='Gaz21'><CarPaint><Items>" + Stock + "</Items></CarPaint>" + other + "</CarMat>";
    private static LegacyVinylPack Read(string xml) => LegacyVinylPackCodec.Read(xml, "Gaz21");
    private static void Check(bool valid, string label) { checks++; if (!valid) throw new Exception(label); }
    private static void Reject(string xml, string code)
    {
        try { Read(xml); throw new Exception("Accepted invalid pack: " + code); }
        catch (LegacyVinylPackException error) { Check(error.Code == code, code + " rejection: " + error.Code); }
    }
    private static int Main(string[] args)
    {
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
            Versions(); TextFields(); SignedDimensions(); Materials(); Merging(); XmlSafety();
            foreach (string path in args) ExternalPack(path);
            Console.WriteLine("PASS: " + checks + " vinyl format/import/export assertions."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    private static void Versions()
    {
        for (int i = 1; i <= 4; i++) Check(Read(Pack(version: i.ToString())).Version == i, "supported version " + i);
        foreach (string value in new[] { "0", "5", "999" }) Reject(Pack(version: value), "UnsupportedVersion");
        foreach (string value in new[] { "v4", "1.4.0", "-1", "04", " 4", "2147483648" }) Reject(Pack(version: value), "Version");
        Reject(Pack().Replace("version='4'", ""), "MissingVersion");
        Reject(Pack().Replace("VinylPack", "CarMat"), "Root");
        Reject(Pack().Replace("Gaz21", "BMW_X5"), "Car");
        Check(Read(Pack().Replace("Gaz21", "gaz21")).Car == "gaz21", "case insensitive car match");
        Reject(Pack(Stock + new string(' ', 0)).Replace("<Items>", "" ).Replace("</Items>", ""), "Structure");
        Reject(Pack().Replace("</CarPaint>", "</CarPaint><CarPaint><Items/></CarPaint>"), "Duplicate");
        Reject(Pack().Replace("</Items>", "</Items><Items/>"), "Duplicate");
        Reject(Pack(string.Concat(Enumerable.Repeat(Stock, 257))), "Items");
        Check(Read(Pack(string.Concat(Enumerable.Repeat(Stock, 256)))).TextCount == 0, "layer limit inclusive");
    }
    private static void TextFields()
    {
        Check(Read(Pack(Stock + Text)).TextCount == 1, "mixed native/text layers");
        Check(Read(Pack(Stock.Replace("0.246", "-0.246"))).TextCount == 0, "legacy signed height preserved");
        for (int font = 0; font < 3; font++)
        for (int style = 0; style < 4; style++)
            Check(Read(Pack(Text.Replace("ect.font='1'", "ect.font='" + font + "'").Replace("ect.style='3'", "ect.style='" + style + "'"))).TextCount == 1, "all supported font/styles");
        foreach (string field in new[] { "ect.text='MAD OUT 2'", "ect.font='1'", "ect.style='3'" }) Reject(Pack(Text.Replace(field, "")), "Text");
        foreach (string value in new[] { "-1", "3", "1.0", "01", "" }) Reject(Pack(Text.Replace("ect.font='1'", "ect.font='" + value + "'")), "Text");
        foreach (string value in new[] { "4", "-1", "03", "" }) Reject(Pack(Text.Replace("ect.style='3'", "ect.style='" + value + "'")), "Text");
        Reject(Pack(Text.Replace("ect.symmetry='1'", "ect.symmetry='true'")), "Text");
        Reject(Pack(Text.Replace("ECT.Text", "Primitive_1")), "Text");
        Reject(Pack(Text.Replace("MAD OUT 2", new string('x', 81))), "Text");
        Reject(Pack(Text.Replace("MAD OUT 2", "line&#10;break")), "Text");
        Reject(Pack(Text.Replace("ect.font=", "ect.extra='foo' ect.font=")), "Text");
        Check(Read(Pack(Text.Replace("MAD OUT 2", new string('Ж', 80)))).TextCount == 1, "UTF8 text length boundary");
        string encoded = "ect.text.v1:2:1:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("Привет & мир"));
        var old = Read(Pack(Stock.Replace("Car_Vinyl_Aplication_11", encoded)));
        var item = (XmlElement)old.Document.SelectSingleNode("VinylPack/CarPaint/Items/Item");
        Check(old.TextCount == 1 && item.GetAttribute("sprite.name") == "ECT.Text" && item.GetAttribute("ect.text") == "Привет & мир" && item.GetAttribute("ect.font") == "2", "legacy encoded text migration");
        foreach (string name in new[] { "ect.text.v2:0:0:YWJj", "ect.text.v1:0:0:%%", "ect.text.v1:0:0:/w==", "ect.text.v1:3:0:YWJj", "ect.text.v1:0:4:YWJj" })
            Reject(Pack(Stock.Replace("Car_Vinyl_Aplication_11", name)), "Text");
        Reject(Pack(Text.Replace("ECT.Text", encoded)), "Text");
        Check(Read(Pack(Stock.Replace("/>", " ect.symmetry='1'/>"))).TextCount == 0, "stock symmetry extension accepted");
    }
    private static void SignedDimensions()
    {
        string signed = Stock.Replace("4.16", "-4.16");
        for (int version = 1; version <= 4; version++)
        {
            var pack = Read(Pack(signed, version: version.ToString()));
            Check(pack.Document.SelectSingleNode("VinylPack/CarPaint/Items/Item/@acpect").Value == "-4.16", "signed aspect supported in version " + version);
        }
        var both = Read(Pack(signed.Replace("0.246", "-0.246")));
        var saved = LegacyVinylPackCodec.Merge(both, Save(), null);
        Check(saved.SelectSingleNode("CarMat/CarPaint/Items/Item/@size_y").Value == "-0.246" && saved.SelectSingleNode("CarMat/CarPaint/Items/Item/@acpect").Value == "-4.16", "import preserves both dimension signs");
        var text = Read(Pack(Text.Replace("4.16", "-4.16")));
        var fallback = LegacyVinylPackCodec.Merge(text, Save(), "Primitive_1");
        var roundtrip = Read(LegacyVinylPackCodec.Export(fallback, "Gaz21").OuterXml);
        Check(roundtrip.Document.SelectSingleNode("VinylPack/CarPaint/Items/Item/@acpect").Value == "-4.16", "fallback and export preserve native reflection");
    }
    private static void ExternalPack(string path)
    {
        string xml = System.IO.File.ReadAllText(path);
        var original = LegacyVinylPackCodec.ReadXml(xml);
        string car = original.DocumentElement.GetAttribute("car");
        var pack = LegacyVinylPackCodec.ReadFile(path, car);
        var save = new XmlDocument(); var root = save.CreateElement("CarMat"); root.SetAttribute("Name", car); save.AppendChild(root);
        var merged = LegacyVinylPackCodec.Merge(pack, save.OuterXml, null);
        Check(merged.DocumentElement["CarPaint"].OuterXml == original.DocumentElement["CarPaint"].OuterXml, "real pack import preserves every layer");
        var fallback = LegacyVinylPackCodec.Merge(pack, save.OuterXml, "Primitive_1");
        var before = original.SelectNodes("VinylPack/CarPaint/Items/Item").Cast<XmlElement>().ToArray();
        var after = fallback.SelectNodes("CarMat/CarPaint/Items/Item").Cast<XmlElement>().ToArray();
        Check(before.Length == after.Length && before.Select((item, i) => new[] { "pos", "rot", "size_y", "acpect", "depth", "color" }.All(field => item.GetAttribute(field) == after[i].GetAttribute(field))).All(valid => valid), "real pack fallback preserves signed dimensions and placement");
        var restored = LegacyVinylPackCodec.Read(LegacyVinylPackCodec.Export(merged, car).OuterXml, car);
        Check(restored.TextCount == pack.TextCount && restored.Document.SelectNodes("VinylPack/CarPaint/Items/Item").Count == before.Length, "real pack export roundtrip preserves text and layer count");
        Console.WriteLine("Real pack: version " + pack.Version + ", " + before.Length + " layers, " + pack.TextCount + " text layers; import/fallback/export passed.");
    }

    private static void Materials()
    {
        Check(Read(Pack(other: Material + Parts)).PartCount == 2, "part colors recognized separately");
        foreach (string invalid in new[] {
            Parts.Replace("version='1'", "version='2'"), Parts.Replace("Hood", "Glass"),
            Parts.Replace("Doors", "Hood"), Parts.Replace("1 0 0", "NaN 0 0"),
            Parts.Replace("1 0 0", "1 0"), Parts.Replace("0.7", "1.1"),
            Parts.Replace("gloss='0.8'", ""), Parts.Replace("1 0 0", "-0.1 0 0") }) Reject(Pack(other: Material + invalid), "Parts");
        Reject(Pack(other: Parts), "Parts");
        Reject(Pack(other: Material + Parts + Parts), "Duplicate");
        Reject(Pack(other: Material.Replace("0.2 0.3 0.4 1", "0.2 0.3 0.4")), "Material");
        Reject(Pack(other: Material.Replace("Gloss='0.5'", "Gloss='Infinity'")), "Material");
        Reject(Pack(other: Material.Replace("<Wheels", "<Body")), "Duplicate");
        foreach (string invalid in new[] { Stock.Replace("0.246", "NaN"), Stock.Replace("4.16", "0"), Stock.Replace("depth='2.5'", "depth='-1'"), Stock.Replace("0 90 0", "0 90"), Stock.Replace("1 1 1 1", "1 1 1 1.5") }) Reject(Pack(invalid), "Geometry");
    }
    private static void Merging()
    {
        var pack = Read(Pack(Stock + Text, Material + Parts));
        string untouched = pack.Document.OuterXml;
        var merged = LegacyVinylPackCodec.Merge(pack, Save(), "Primitive_1");
        var items = merged.SelectNodes("CarMat/CarPaint/Items/Item");
        var text = (XmlElement)items[1];
        Check(items.Count == 2 && text.GetAttribute("sprite.name") == "Primitive_1", "fallback replaces each text layer");
        foreach (string field in new[] { "pos", "rot", "size_y", "acpect", "depth", "color" })
            Check(text.GetAttribute(field) == ((XmlElement)Read(Pack(Text)).Document.SelectSingleNode("VinylPack/CarPaint/Items/Item")).GetAttribute(field), "fallback preserves " + field);
        Check(!text.Attributes.Cast<XmlAttribute>().Any(a => a.Name.StartsWith("ect.")), "fallback removes text-only fields");
        Check(merged.DocumentElement["CarMaterial"].OuterXml == Read(Pack(other: Material)).Document.DocumentElement["CarMaterial"].OuterXml, "native material stays native");
        Check(merged.DocumentElement["EnhancedBodyPaint"].ChildNodes.Count == 2 && merged.DocumentElement["ExtendedCarTuning"].GetAttribute("engine") == "original", "parts imported and engine untouched");
        Check(pack.Document.OuterXml == untouched, "source pack never modified by fallback");
        var full = LegacyVinylPackCodec.Merge(pack, Save(), null);
        Check(full.SelectNodes("CarMat/CarPaint/Items/Item[@sprite.name='ECT.Text']").Count == 1, "plugin load keeps text");
        var clear = LegacyVinylPackCodec.Merge(Read(Pack(other: Material)), Save(), null);
        Check(clear.DocumentElement["EnhancedBodyPaint"] == null, "stock color import clears stale part overrides");
        var vinylOnly = LegacyVinylPackCodec.Merge(Read(Pack()), Save(), null);
        Check(vinylOnly.DocumentElement["CarMaterial"].OuterXml == full.DocumentElement["CarMaterial"].OuterXml && vinylOnly.DocumentElement["EnhancedBodyPaint"].OuterXml == full.DocumentElement["EnhancedBodyPaint"].OuterXml, "vinyl-only pack preserves existing colors");
        var exported = LegacyVinylPackCodec.Export(full, "Gaz21");
        var roundTrip = Read(exported.OuterXml);
        Check(roundTrip.Version == 4 && roundTrip.TextCount == 1 && roundTrip.PartCount == 2, "version 4 export/roundtrip");
        Check(exported.DocumentElement["ExtendedCarTuning"] == null, "export includes paint only");
        try { LegacyVinylPackCodec.Merge(pack, Save().Replace("Gaz21", "BMW_X5"), null); throw new Exception("wrong save accepted"); }
        catch (LegacyVinylPackException error) { Check(error.Code == "Car", "reject wrong save car"); }
        try { LegacyVinylPackCodec.Merge(pack, Save(), ""); throw new Exception("empty primitive accepted"); }
        catch (LegacyVinylPackException error) { Check(error.Code == "Primitive", "reject missing primitive"); }
    }
    private static void XmlSafety()
    {
        var normalized = Read("<?xml version='1.0'?><?ignored test?>" + Pack().Replace("<Items>", "<Items><!--ignored-->\n"));
        Check(normalized.Document.FirstChild is XmlElement && normalized.Document.SelectNodes("//comment()").Count == 0, "declaration/comments removed for native loader");
        foreach (string bad in new[] { "<!DOCTYPE VinylPack [<!ENTITY x 'foo'>]>" + Pack(), Pack().Replace("Gaz21", new string('x', LegacyVinylPackCodec.MaxCharacters)) })
        {
            try { Read(bad); throw new Exception("unsafe XML accepted"); }
            catch (XmlException) { Check(true, "XML limits and DTD prohibited"); }
        }
    }
}
