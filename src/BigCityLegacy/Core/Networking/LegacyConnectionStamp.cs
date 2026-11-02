using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.Networking;

internal static class LegacyConnectionStamp
{
    private sealed class Item { internal NetConnect Owner; internal string Value; }
    private static readonly Dictionary<NetworkConnection,Item> entries = new Dictionary<NetworkConnection,Item>();
    internal static string Attribute { get { return Encoding.ASCII.GetString(new byte[] {66,67,76,78,111,100,101,86,49}); } }
    internal static void Clear() { entries.Clear(); }
    internal static void Forget(NetworkConnection connection) { if (connection != null) entries.Remove(connection); }
    internal static string Get(NetConnect connect)
    { Item item; return connect && connect.conn != null && entries.TryGetValue(connect.conn,out item) && item.Owner == connect ? item.Value : null; }
    internal static string Attach(string xml)
    {
        var document = LegacyPluginManifest.LoadXml(xml); string value = LegacyRuntimeStamp.Read();
        document.DocumentElement.RemoveAttribute(Attribute);
        if (!string.IsNullOrEmpty(value)) document.DocumentElement.SetAttribute(Attribute,value);
        return document.OuterXml;
    }
    internal static void Capture(NetConnect connect, ref string xml)
    {
        var document = LegacyPluginManifest.LoadXml(xml);
        string value = document.DocumentElement.HasAttribute(Attribute) ? document.DocumentElement.GetAttribute(Attribute) : null;
        if (value != null) { LegacyNodeCodec.Decode(value); value = value.ToUpperInvariant(); document.DocumentElement.RemoveAttribute(Attribute); }
        if (LegacyCommandLine.HasArg("-requireFingerprint") && !LegacyNodeCodec.Usable(value))
            throw new FormatException("Required connection fingerprint is unavailable.");
        Item old;
        if (entries.TryGetValue(connect.conn,out old) && old.Owner == connect && old.Value != value) throw new FormatException("Connection metadata changed during admission.");
        entries[connect.conn] = new Item { Owner=connect,Value=value }; xml=document.OuterXml;
    }
}
