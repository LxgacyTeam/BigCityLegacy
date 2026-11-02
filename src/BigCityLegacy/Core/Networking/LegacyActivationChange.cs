using System;
using System.Collections.Generic;
using System.Text;

internal sealed class LegacyActivationChange
{
    internal string Guid,Name,Version;
    internal bool Enabled;
    internal static void Validate(IList<LegacyActivationChange> changes)
    {
        if(changes==null || changes.Count<1 || changes.Count>32)throw new FormatException("Invalid activation request count.");
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int bytes=0;
        foreach(var change in changes)
        {
            if(change==null || string.IsNullOrWhiteSpace(change.Guid) || change.Guid!=change.Guid.Trim() || change.Guid.Length>128 ||
                change.Name==null || change.Name.Length>128 || string.IsNullOrEmpty(change.Version) || change.Version.Length>64 || !seen.Add(change.Guid))throw new FormatException("Invalid activation request.");
            foreach(char c in change.Guid+change.Name+change.Version)if(char.IsControl(c))throw new FormatException("Invalid activation request text.");
            LegacyPluginVersion.Parse(change.Version);bytes+=Encoding.UTF8.GetByteCount(change.Guid+change.Name+change.Version);
        }
        if(bytes>24000)throw new FormatException("Activation request is too large.");
    }
}
