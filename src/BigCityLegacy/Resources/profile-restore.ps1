param([Parameter(Mandatory=$true)][string]$TaskPath, [switch]$ValidateOnly)
$ErrorActionPreference = 'Stop'
$task = Get-Content -LiteralPath $TaskPath -Raw -Encoding UTF8 | ConvertFrom-Json
try {
    if ($task.RegistryPath -notlike 'Software\*\*' -or $task.RegistryPath -match '[\x00-\x1f]' -or !$task.Executable) {
        throw 'Invalid profile restore task.'
    }
    $values = New-Object 'System.Collections.Generic.List[object]'
    foreach ($entry in $task.Values) {
        $kind = [Microsoft.Win32.RegistryValueKind]([Enum]::Parse([Microsoft.Win32.RegistryValueKind], $entry.Kind))
        $value = switch ($entry.Kind) {
            'DWord' { [int]::Parse($entry.Data, [Globalization.CultureInfo]::InvariantCulture) }
            'QWord' { [long]::Parse($entry.Data, [Globalization.CultureInfo]::InvariantCulture) }
            'Binary' { ,[Convert]::FromBase64String($entry.Data) }
            'None' { ,[Convert]::FromBase64String($entry.Data) }
            'String' { [string]$entry.Data }
            'ExpandString' { [string]$entry.Data }
            'MultiString' { ,[string[]]$entry.Strings }
            default { throw 'Unsupported registry value kind.' }
        }
        $values.Add([pscustomobject]@{ Name=[string]$entry.Name; Kind=$kind; Value=$value })
    }
    if ($ValidateOnly) { Write-Output "Validated $($values.Count) native preferences; no preferences were changed."; exit 0 }
    $parent = Get-Process -Id $task.OldPid -ErrorAction SilentlyContinue
    if (!$parent -or $parent.StartTime.ToUniversalTime().Ticks.ToString() -ne $task.OldProcessStart) { throw 'Original game process is unavailable.' }
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'ready'), 'ready')
    $parent.WaitForExit()
    $registry = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($task.RegistryPath)
    try {
        # Write the validated snapshot first. Keep existing preferences if a write
        # fails, rather than emptying the key before attempting any replacement.
        foreach ($entry in $values) { $registry.SetValue($entry.Name, $entry.Value, $entry.Kind) }
        $retained = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $values) { [void]$retained.Add($entry.Name) }
        foreach ($name in $registry.GetValueNames()) { if (!$retained.Contains($name)) { $registry.DeleteValue($name, $false) } }
        $registry.Flush()
    } finally { $registry.Dispose() }
    # The new Unity process must bootstrap Doorstop/BepInEx independently.
    foreach ($name in @('DOORSTOP_INITIALIZED', 'DOORSTOP_DISABLE')) {
        [Environment]::SetEnvironmentVariable($name, $null, [EnvironmentVariableTarget]::Process)
    }
    if ([string]::IsNullOrEmpty($task.Arguments)) {
        Start-Process -FilePath $task.Executable -WorkingDirectory $task.WorkingDirectory
    } else {
        Start-Process -FilePath $task.Executable -ArgumentList $task.Arguments -WorkingDirectory $task.WorkingDirectory
    }
    # Remove only this task's explicitly named transient files.
    foreach ($name in @('ready', 'task.json', 'restore.ps1')) {
        Remove-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Force -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath $PSScriptRoot -ErrorAction SilentlyContinue
} catch {
    [IO.File]::WriteAllText((Join-Path $PSScriptRoot 'error'), $_.Exception.ToString())
    exit 1
}
