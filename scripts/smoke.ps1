param([Parameter(Mandatory=$true)][string]$AppPath, [string]$TracePath)
$ErrorActionPreference = 'Stop'
$appFile = (Resolve-Path -LiteralPath $AppPath).Path
$appFolder = Split-Path -Parent $appFile
if (-not $TracePath) { $TracePath = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\portable-startup-trace.txt' }
$traceFile = [IO.Path]::GetFullPath($TracePath)
[IO.File]::WriteAllText($traceFile, '')
$start = [Diagnostics.ProcessStartInfo]::new($appFile)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.WindowStyle = [Diagnostics.ProcessWindowStyle]::Hidden
$start.EnvironmentVariables['DOTNET_HOST_TRACE'] = '1'
$start.EnvironmentVariables['DOTNET_HOST_TRACEFILE'] = $traceFile
$start.EnvironmentVariables['DOTNET_ROOT'] = Join-Path $appFolder 'no-global-runtime'
$start.EnvironmentVariables['DOTNET_MULTILEVEL_LOOKUP'] = '0'
$child = [Diagnostics.Process]::Start($start)
try {
    if (-not $child.WaitForInputIdle(10000)) { throw 'App failed to reach an idle UI.' }
    Start-Sleep -Milliseconds 700
    $child.Refresh()
    if ($child.HasExited) { throw 'App exited before opening the primary window.' }
    # Background startup may have no visible MainWindowHandle in the test desktop.
    # The lifecycle harness verifies the actual WPF window, tray, activation and exit.
    $settingsFile = Join-Path $appFolder 'Data\settings.json'
    if (-not (Test-Path -LiteralPath $settingsFile)) { throw 'Primary startup did not create settings.' }
    if ((Get-Item -LiteralPath $settingsFile).LastWriteTimeUtc -lt $child.StartTime.ToUniversalTime()) { throw 'Primary startup did not save its current settings.' }
} finally {
    # This script owns only this smoke-test process. Graceful tray exit is tested separately.
    if (-not $child.HasExited) { $child.Kill(); $child.WaitForExit() }
    $child.Dispose()
}
$trace = [IO.File]::ReadAllText($traceFile)
if ($trace.Contains('Detected Single-File app bundle')) {
    foreach ($marker in @('Using internal fxr', 'Using internal hostpolicy', 'Executing as a self-contained app')) {
        if (-not $trace.Contains($marker)) { throw "Missing bundled runtime marker: $marker" }
    }
} else {
foreach ($library in @('hostfxr.dll','hostpolicy.dll','coreclr.dll')) {
    if (-not $trace.Contains('Loaded library from ' + (Join-Path $appFolder $library))) { throw "Bundled runtime not used: $library" }
}
}
Write-Output 'PASS primary portable exe initializes its application and settings'
Write-Output 'PASS bundled hostfxr, hostpolicy and coreclr are used'
