using System.Globalization;
using HarmonyLib;

internal static class LegacyOnlineProtocol
{
    internal static int Resolve(string value, bool server)
    {
        int selected;
        return !server && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out selected) && selected > 0
            ? System.Math.Min(selected, BigCityLegacyPlugin.OnlineProtocolVersion) : BigCityLegacyPlugin.OnlineProtocolVersion;
    }
    internal static void Apply()
    {
        AlwaysOnline.onlineVersion = Resolve(LegacyCommandLine.GetArgValue(LegacyCommandLine.GetRawProcessArgs(), "-onlineVersion"), LegacyCommandLine.HasServerArg());
    }
}

[HarmonyPatch(typeof(AlwaysOnline), "Read")]
internal static class OnlineProtocolPatches
{
    private static void Postfix() { LegacyOnlineProtocol.Apply(); }
}
