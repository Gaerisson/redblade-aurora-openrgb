<#
.SYNOPSIS
Run RedBlade+ host benchmarks and optionally read-only HID timings.
.DESCRIPTION
Without -Hid, RGB applications keep running. -Hid temporarily stops the RGB
service/clients, measures request/reply timing, and restores them in finally.
The report does not measure physical key latency or MCU execution time.
#>
[CmdletBinding()]
param(
    [switch]$Hid,
    [ValidateRange(10,100000)][int]$Samples=1000,
    [string]$NativeExe,
    [string]$OutputDirectory,
    [switch]$ElevatedChild
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $NativeExe) { $NativeExe=Join-Path $root 'dist/redblade-plus-benchmark-windows.exe' }
if(-not $OutputDirectory) { $OutputDirectory=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'RedBladePlus/Measurements' }
$NativeExe=[IO.Path]::GetFullPath($NativeExe)
$OutputDirectory=[IO.Path]::TrimEndingDirectorySeparator([IO.Path]::GetFullPath($OutputDirectory))
if($Hid) {
    $admin=([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if(-not $admin) {
        # Interactive measurement window: the user can see the report and errors.
        $arguments=@('-NoProfile','-File',('"'+$PSCommandPath+'"'),'-Hid','-Samples',$Samples,
            '-NativeExe',('"'+$NativeExe+'"'),'-OutputDirectory',('"'+$OutputDirectory+'"'),'-ElevatedChild')
        $child=Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList $arguments -Verb RunAs -WindowStyle Normal -PassThru
        $child.WaitForExit()
        exit $child.ExitCode
    }
}
$service=Get-Service OpenRGB -ErrorAction SilentlyContinue
$serviceWasRunning=$service -and $service.Status -eq 'Running'
$session=[Diagnostics.Process]::GetCurrentProcess().SessionId
$restart=@()
$stopped=$false
$failed=$false
try {
    if($Hid) {
        foreach($process in @(Get-Process OpenRGB,AuroraRgb -ErrorAction SilentlyContinue | Where-Object SessionId -eq $session)) {
            $command=(Get-CimInstance Win32_Process -Filter ('ProcessId='+$process.Id)).CommandLine
            $exe=$process.Path
            if(-not $exe -or -not $command) { throw 'Cannot preserve an RGB application startup command.' }
            if($command.StartsWith('"')) { $arguments=$command.Substring($command.IndexOf('"',1)+1).TrimStart() }
            elseif($command.StartsWith($exe,[StringComparison]::OrdinalIgnoreCase)) { $arguments=$command.Substring($exe.Length).TrimStart() }
            else { throw 'Cannot safely preserve the RGB application arguments.' }
            # Keep original arguments in memory; never include them in the report.
            $restart+=@{Name=$process.ProcessName;Exe=$exe;Arguments=$arguments}
        }
        $stopped=$true
        if($serviceWasRunning) { Stop-Service OpenRGB }
        Get-Process AuroraRgb,AuroraDeviceManager,OpenRGB -ErrorAction SilentlyContinue |
            Where-Object SessionId -eq $session | Stop-Process
    }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $report=Join-Path $OutputDirectory ('redblade-plus-'+(Get-Date -Format 'yyyyMMdd-HHmmss-ffff')+'.json')
    $arguments=@((Join-Path $PSScriptRoot 'benchmark.py'),'--output',$report)
    if(Test-Path -LiteralPath $NativeExe) { $arguments+=@('--native-exe',$NativeExe) }
    elseif(-not $Hid) { throw 'Download the native benchmark executable from the release, or pass -NativeExe with its path.' }
    if($Hid) { $arguments+=@('--hid','--samples',$Samples) }
    & python @arguments
    if($LASTEXITCODE -ne 0) { throw 'Performance measurement failed; RGB applications will be restored.' }
    Write-Host "Report saved: $report"
} catch {
    $failed=$true
    Write-Warning $_.Exception.Message
} finally {
    if($stopped) {
        if($serviceWasRunning) {
            try { Start-Service OpenRGB } catch { $failed=$true; Write-Warning 'OpenRGB service could not be restarted.' }
        }
        # Get-Process sorts by name, so AuroraRgb was captured before OpenRGB.
        # Aurora connects to the OpenRGB SDK once at startup: restart OpenRGB
        # first and wait for its SDK port before starting Aurora again.
        $openRgb=@($restart | Where-Object { $_.Name -eq 'OpenRGB' })
        $others=@($restart | Where-Object { $_.Name -ne 'OpenRGB' })
        foreach($app in @($openRgb + $others)) {
            if($app -eq $others[0] -and ($serviceWasRunning -or $openRgb.Count)) {
                $port=6742
                foreach($candidate in $openRgb) { if($candidate.Arguments -match '--server-port\s+(\d+)') { $port=[int]$Matches[1] } }
                $listening=$false
                for($attempt=0;$attempt -lt 60;$attempt++) {
                    $client=[Net.Sockets.TcpClient]::new()
                    try { $client.Connect('127.0.0.1',$port); $listening=$true; break } catch { Start-Sleep -Milliseconds 500 } finally { $client.Dispose() }
                }
                if(-not $listening) { Write-Warning "OpenRGB SDK is not listening on port $port; Aurora may need a manual restart." }
            }
            try {
                $start=@{FilePath=$app.Exe;WindowStyle='Hidden'}
                if($app.Arguments) { $start.ArgumentList=$app.Arguments }
                Start-Process @start
            } catch { $failed=$true; Write-Warning ('Could not restart '+[IO.Path]::GetFileName($app.Exe)) }
        }
    }
}
if($ElevatedChild) { Read-Host 'Press Enter to close this measurement window' | Out-Null }
if($failed) { exit 1 }
