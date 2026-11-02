using System;
using Newtonsoft.Json;

internal static class LegacyMasterReload
{
    internal static LegacyMasterServer.Config Parse(string json)
    {
        if (json == null || json.Length > 1024 * 1024) throw new FormatException("ServersList exceeds 1 MiB.");
        var parsed = JsonConvert.DeserializeObject<LegacyMasterServer.Config>(json, new JsonSerializerSettings { MaxDepth = 16 });
        if (parsed == null || parsed.servers == null || parsed.version < 0 || parsed.version > 1 || parsed.servers.Length > 4096)
            throw new FormatException("Invalid ServersList schema/version.");
        foreach (var entry in parsed.servers)
            if (entry != null && (entry.port < 0 || entry.port > 65534)) throw new FormatException("Server port must permit its query port (+1).");
        return parsed;
    }
}
