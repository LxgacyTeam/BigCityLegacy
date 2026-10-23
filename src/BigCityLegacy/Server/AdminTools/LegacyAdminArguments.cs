using System;
using System.Globalization;
using Newtonsoft.Json.Linq;
using UnityEngine;

internal static class LegacyAdminTime
{
    internal static long Now { get { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); } }
    internal static bool IsActive(long? until, long now) { return !until.HasValue || until.Value > now; }

    internal static bool TryParse(string value, out long? until)
    {
        return TryParse(value, Now, out until);
    }

    internal static bool TryParse(string value, long now, out long? until)
    {
        until = null;
        if (string.IsNullOrEmpty(value)) return true;
        if (value.Length < 2) return false;
        long multiplier;
        switch (char.ToLowerInvariant(value[value.Length - 1]))
        {
            case 's': multiplier = 1; break;
            case 'm': multiplier = 60; break;
            case 'h': multiplier = 3600; break;
            case 'd': multiplier = 86400; break;
            default: return false;
        }
        long count;
        if (!long.TryParse(value.Substring(0, value.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out count) || count <= 0) return false;
        try { until = checked(now + checked(count * multiplier)); }
        catch (OverflowException) { return false; }
        // Keep persisted timestamps usable by DateTime-based tools too.
        return until <= 253402300799L;
    }

    internal static string Remaining(long? until) { return until.HasValue ? FormatSeconds(Math.Max(1, until.Value - Now)) : "perm"; }
    internal static string FormatSeconds(long seconds)
    {
        if (seconds % 86400 == 0) return (seconds / 86400).ToString(CultureInfo.InvariantCulture) + "d";
        if (seconds % 3600 == 0) return (seconds / 3600).ToString(CultureInfo.InvariantCulture) + "h";
        if (seconds % 60 == 0) return (seconds / 60).ToString(CultureInfo.InvariantCulture) + "m";
        // Round up for an easily readable remaining duration without showing an expired zero.
        if (seconds > 86400) return ((seconds + 86399) / 86400).ToString(CultureInfo.InvariantCulture) + "d";
        if (seconds > 3600) return ((seconds + 3599) / 3600).ToString(CultureInfo.InvariantCulture) + "h";
        if (seconds > 60) return ((seconds + 59) / 60).ToString(CultureInfo.InvariantCulture) + "m";
        return Math.Max(1, seconds).ToString(CultureInfo.InvariantCulture) + "s";
    }
}

internal static class LegacyAdminTeleportParser
{
    internal const string PresetNames = "police, park, airport, sto1, sto2, sto3, sto4, desert, mountain, cemetry, derbyarena, mansion, farm";

    internal static bool StripWithCar(ref string text)
    {
        text = (text ?? "").Trim();
        int split = text.LastIndexOfAny(new[] { ' ', '\t' });
        if (split < 0 || !string.Equals(text.Substring(split + 1), "wcar", StringComparison.OrdinalIgnoreCase)) return false;
        text = text.Substring(0, split).TrimEnd();
        return true;
    }

    internal static bool TryParse(string text, out Vector3 position, out float? yaw)
    {
        position = Vector3.zero;
        yaw = null;
        if (string.IsNullOrWhiteSpace(text)) return false;
        text = text.Trim();
        switch (text.ToLowerInvariant())
        {
            case "police": position = new Vector3(-2204.523f, -2.732f, 2259.899f); yaw = 158.882f; return true;
            case "park": position = new Vector3(-1724.992f, -2.973f, 2150.122f); yaw = 142.503f; return true;
            case "airport": position = new Vector3(-1168.726f, -21.871f, 191.786f); yaw = 260.141f; return true;
            case "sto1": position = new Vector3(175.315f, 9.861f, 70.301f); yaw = 257.819f; return true;
            case "sto2": position = new Vector3(-1766.753f, 2.38f, 2679.624f); yaw = 301.47f; return true;
            case "sto3": position = new Vector3(-2059.275f, -16.363f, 432.42f); yaw = 72.827f; return true;
            case "sto4": position = new Vector3(-754.179f, 8.891f, 2031.091f); yaw = 92.544f; return true;
            case "desert": position = new Vector3(51.679f, -30.625f, 790.927f); yaw = 190.66f; return true;
            case "mountain": position = new Vector3(-765.703f, 85.939f, -554.006f); yaw = 348.065f; return true;
            case "cemetry": position = new Vector3(-620.653f, -19.103f, 1201.56f); yaw = 88.573f; return true;
            case "derbyarena": position = new Vector3(-160.268f, -27.222f, -385.677f); yaw = 183.371f; return true;
            case "mansion": position = new Vector3(-418.916f, -14.227f, 2318.561f); yaw = 343.576f; return true;
            case "farm": position = new Vector3(-1829.054f, -23.318f, 765.427f); yaw = 96.496f; return true;
        }
        try
        {
            float x, y, z;
            if (text.StartsWith("{", StringComparison.Ordinal))
            {
                JObject json = JObject.Parse(text);
                if (!ReadNumber(json["x"], out x) || !ReadNumber(json["y"], out y) || !ReadNumber(json["z"], out z)) return false;
                if (json["yaw"] != null)
                {
                    float angle;
                    if (!ReadNumber(json["yaw"], out angle)) return false;
                    yaw = angle;
                }
            }
            else
            {
                string[] parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 3 || !Number(parts[0], out x) || !Number(parts[1], out y) || !Number(parts[2], out z)) return false;
            }
            position = new Vector3(x, y, z);
            return true;
        }
        catch (Newtonsoft.Json.JsonException) { return false; }
    }

    private static bool ReadNumber(JToken value, out float result)
    {
        result = 0f;
        return value != null && (value.Type == JTokenType.Float || value.Type == JTokenType.Integer) && Number(value.ToString(Newtonsoft.Json.Formatting.None), out result);
    }
    private static bool Number(string value, out float result)
    {
        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && !float.IsNaN(result) && !float.IsInfinity(result) && Math.Abs(result) <= 1000000f;
    }
}
