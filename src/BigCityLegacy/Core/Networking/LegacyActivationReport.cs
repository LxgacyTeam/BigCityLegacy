using System;
using System.Collections.Generic;

internal static class LegacyActivationReport
{
    internal const string Attribute="BCLActivationV1";
    internal static string Encode(IList<LegacyPluginRecord> plugins)
    {
        if(plugins==null || plugins.Count>256)throw new FormatException("Invalid activation inventory.");
        byte[] data=new byte[3+plugins.Count];data[0]=1;data[1]=(byte)plugins.Count;data[2]=(byte)(plugins.Count>>8);
        for(int i=0;i<plugins.Count;i++)if(plugins[i].SupportsActivation)data[i+3]=(byte)(1|(plugins[i].IsEnabled?2:0)|(plugins[i].RequiresRestart?4:0));
        return Convert.ToBase64String(data);
    }
    internal static void Apply(string value,IList<LegacyPluginRecord> plugins)
    {
        if(value==null)return;
        if(value.Length>348)throw new FormatException("Activation inventory is too large.");
        byte[] data=Convert.FromBase64String(value);
        if(plugins==null || data.Length!=3+plugins.Count || data[0]!=1 || data[1]+(data[2]<<8)!=plugins.Count)throw new FormatException("Invalid activation inventory.");
        for(int i=0;i<plugins.Count;i++)
        {
            byte flags=data[i+3];if(flags>7 || (flags&1)==0 && flags!=0)throw new FormatException("Invalid activation flags.");
            plugins[i].SupportsActivation=(flags&1)!=0;plugins[i].IsEnabled=(flags&2)!=0;plugins[i].RequiresRestart=(flags&4)!=0;
        }
    }
}
