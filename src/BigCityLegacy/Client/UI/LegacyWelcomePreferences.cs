using System;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using BigCityLegacy.Api;
using UnityEngine;

internal static class LegacyWelcomePreferences
{
    internal static string Endpoint(string address, int port)
    {
        IPAddress ip;
        if (port < 1 || port > 65535 || string.IsNullOrWhiteSpace(address)) return null;
        address = address.Trim().Trim('[', ']');
        if (IPAddress.TryParse(address, out ip)) address = ip.ToString();
        else address = address.ToLowerInvariant();
        return (address.IndexOf(':') >= 0 ? "[" + address + "]" : address) + ":" + port.ToString(CultureInfo.InvariantCulture);
    }
    private static string Key(string server)
    {
        using (var hash = SHA256.Create())
            return "BigCityLegacy.Welcome." + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(server))).Replace("-", "");
    }
    internal static bool ShouldShow(string server, long updatedAt)
    {
        if (string.IsNullOrEmpty(server)) return true;
        string key = Key(server), timestamp = updatedAt.ToString(CultureInfo.InvariantCulture);
        if (PlayerPrefs.HasKey(key + ".updatedAt") && PlayerPrefs.GetString(key + ".updatedAt") != timestamp)
        {
            PlayerPrefs.SetInt(key + ".hidden", 0); PlayerPrefs.SetString(key + ".updatedAt", timestamp); PlayerPrefs.Save();
            return true;
        }
        return PlayerPrefs.GetInt(key + ".hidden", 0) == 0;
    }
    internal static void SetHidden(string server, long updatedAt, bool hidden)
    {
        if (string.IsNullOrEmpty(server)) return;
        string key = Key(server); PlayerPrefs.SetInt(key + ".hidden", hidden ? 1 : 0);
        PlayerPrefs.SetString(key + ".updatedAt", updatedAt.ToString(CultureInfo.InvariantCulture)); PlayerPrefs.Save();
    }
}

