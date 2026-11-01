using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using Newtonsoft.Json;
using UnityEngine;

internal static class LegacyGameRestart
{
    internal static bool Running { get; private set; }
    internal static void Start(TaskCompletionSource<bool> completion)
    {
        if(Running)throw new InvalidOperationException("A game restart is already pending.");
        if(Application.isEditor || LegacyCommandLine.HasServerArg() || !BigCityLegacyPlugin.Instance)throw new InvalidOperationException("Restart is available in the standalone client only.");
        Running=true;BigCityLegacyPlugin.Instance.StartCoroutine(Run(completion));
    }
    internal static string UnixScript(int pid,string directory,string executable,string[] arguments,string gameRoot)
    {
        Func<string,string> q=LegacyProfileRestart.ShellQuote;string ready=Path.Combine(directory,"ready");
        var s=new StringBuilder("#!/bin/sh\nset -eu\nprintf ready > "+q(ready)+"\nn=0\n");
        s.Append("while kill -0 ").Append(pid).Append(" 2>/dev/null; do n=$((n+1)); [ \"$n\" -lt 300 ] || exit 1; sleep 0.2; done\n");
        s.Append("cd ").Append(q(gameRoot)).Append("\nunset DOORSTOP_INITIALIZED DOORSTOP_DISABLE\n");
        s.Append("rm -f -- ").Append(q(ready)).Append(' ').Append(q(Path.Combine(directory,"launch.sh"))).Append("\nrmdir -- ").Append(q(directory)).Append('\n');
        string wrapper=Path.Combine(gameRoot,"run_bepinex.sh");
        s.Append("if [ -f ").Append(q(wrapper)).Append(" ]; then exec /bin/sh ").Append(q(wrapper)).Append(' ').Append(q(executable));
        foreach(string arg in arguments)s.Append(' ').Append(q(arg));
        s.Append("; fi\nexec ").Append(q(executable));foreach(string arg in arguments)s.Append(' ').Append(q(arg));return s.Append('\n').ToString();
    }
    private static IEnumerator Run(TaskCompletionSource<bool> completion)
    {
        Process helper=null;string directory=null;Exception error=null;bool marker=false;
        try
        {
            directory=Path.Combine(Path.GetTempPath(),"BigCityLegacy.Restart."+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            if(!LegacyProfilePrefs.Windows)
                using(var chmod=Process.Start(new ProcessStartInfo("/bin/chmod","700 "+LegacyProfileRestart.QuoteArgument(directory)){UseShellExecute=false,CreateNoWindow=true}))
                { chmod.WaitForExit();if(chmod.ExitCode!=0)throw new IOException("Cannot protect the restart directory."); }
            using(var current=Process.GetCurrentProcess())
            {
                string executable=Paths.ExecutablePath;if(string.IsNullOrEmpty(executable))executable=current.MainModule.FileName;
                string[] args=Environment.GetCommandLineArgs().Skip(1).ToArray();ProcessStartInfo start;
                if(LegacyProfilePrefs.Windows)
                {
                    string script=Path.Combine(directory,"launch.ps1"),task=Path.Combine(directory,"task.json");
                    File.WriteAllText(script,WindowsScript,new UTF8Encoding(false));
                    File.WriteAllText(task,JsonConvert.SerializeObject(new {OldPid=current.Id,OldProcessStart=current.StartTime.ToUniversalTime().Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture),Executable=executable,Arguments=string.Join(" ",args.Select(LegacyProfileRestart.QuoteArgument).ToArray()),WorkingDirectory=Paths.GameRootPath,RegistryPath=LegacyProfilePrefs.RegistryPath}),new UTF8Encoding(false));
                    start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell/v1.0/powershell.exe"),"-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -File "+LegacyProfileRestart.QuoteArgument(script)+" -TaskPath "+LegacyProfileRestart.QuoteArgument(task));
                }
                else
                {
                    string script=Path.Combine(directory,"launch.sh");File.WriteAllText(script,UnixScript(current.Id,directory,executable,args,Paths.GameRootPath),new UTF8Encoding(false));
                    start=new ProcessStartInfo("/bin/sh",LegacyProfileRestart.QuoteArgument(script));
                }
                start.UseShellExecute=false;start.CreateNoWindow=true;start.WindowStyle=ProcessWindowStyle.Hidden;LegacyProfileRestart.PrepareRestartEnvironment(start);
                helper=Process.Start(start);if(helper==null)throw new IOException("Cannot start restart helper.");
            }
        }
        catch(Exception ex){error=ex;}
        if(error==null)
        {
            float deadline=Time.realtimeSinceStartup+15f;
            while(!File.Exists(Path.Combine(directory,"ready")) && !helper.HasExited && Time.realtimeSinceStartup<deadline)yield return null;
            if(File.Exists(Path.Combine(directory,"ready")))
            {
                try { marker=PlayerPrefs.HasKey("maybe_bad_settings");PlayerPrefs.DeleteKey("maybe_bad_settings");PlayerPrefs.Save();Application.Quit();completion.TrySetResult(true); }
                catch(Exception ex) { error=ex; }
                if(error==null) { deadline=Time.realtimeSinceStartup+5f;while(Time.realtimeSinceStartup<deadline)yield return null;error=new IOException("The game did not quit. Restart was cancelled."); }
            }
            else error=new IOException("Restart helper did not become ready.");
        }
        if(helper!=null){try{if(!helper.HasExited)helper.Kill();}catch{}helper.Dispose();}
        if(directory!=null)
        {
            foreach(string name in new[]{"ready","task.json","launch.ps1","launch.sh","error"})try{File.Delete(Path.Combine(directory,name));}catch{}
            try{Directory.Delete(directory);}catch{}
        }
        Running=false;if(marker){PlayerPrefs.SetInt("maybe_bad_settings",1);PlayerPrefs.Save();}
        completion.TrySetException(error);BigCityLegacy.Api.LegacyAlerts.ShowLocalized("Restart failed: "+error.Message,"Перезапуск не удался: "+error.Message);
    }
    internal const string WindowsScript = @"param([Parameter(Mandatory=$true)][string]$TaskPath, [switch]$ValidateOnly)
$ErrorActionPreference='Stop'
$task=Get-Content -LiteralPath $TaskPath -Raw -Encoding UTF8 | ConvertFrom-Json
try {
    if (!$task.Executable -or $task.RegistryPath -notlike 'Software\*\*') { throw 'Invalid restart task.' }
    if ($ValidateOnly) { Write-Output 'Validated restart task; no game launched.'; exit 0 }
    $parent=Get-Process -Id $task.OldPid -ErrorAction Stop
    if ($parent.StartTime.ToUniversalTime().Ticks.ToString() -ne $task.OldProcessStart) { throw 'Original game process changed.' }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ready'),'ready')
    if (!$parent.WaitForExit(60000)) { throw 'Game did not quit.' }
    $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($task.RegistryPath,$true)
    if ($key) {
        try { foreach ($name in $key.GetValueNames()) { if ($name -match '^maybe_bad_settings(_h[0-9]+)?$') { $key.DeleteValue($name,$false) } }; $key.Flush() } finally { $key.Dispose() }
    }
    foreach ($name in @('DOORSTOP_INITIALIZED','DOORSTOP_DISABLE')) { [Environment]::SetEnvironmentVariable($name,$null,[EnvironmentVariableTarget]::Process) }
    $start=@{FilePath=$task.Executable;WorkingDirectory=$task.WorkingDirectory}
    if ($task.Arguments) { $start.ArgumentList=$task.Arguments }
    Start-Process @start
    foreach ($name in @('ready','task.json','launch.ps1')) { Remove-Item -LiteralPath (Join-Path $PSScriptRoot $name) -ErrorAction SilentlyContinue }
    Remove-Item -LiteralPath $PSScriptRoot -ErrorAction SilentlyContinue
} catch { [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'error'),$_.Exception.ToString()); exit 1 }
";
}
