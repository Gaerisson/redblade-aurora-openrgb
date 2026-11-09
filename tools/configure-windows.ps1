#requires -Version 7.0
[CmdletBinding()]
param(
    [switch]$Apply,
    [string]$BackupDirectory = (Join-Path $env:LOCALAPPDATA 'RedBladeAurora\Backups'),
    [switch]$NoAutostart
)
$ErrorActionPreference = 'Stop'
$openRgbExe = Join-Path $env:ProgramFiles 'OpenRGB\OpenRGB.exe'
$auroraExe = Join-Path $env:ProgramFiles 'Aurora\AuroraRgb.exe'
$openRgbFile = Join-Path $env:APPDATA 'OpenRGB\OpenRGB.json'
$openRgbUserFile = $openRgbFile
$service = Get-Service -Name OpenRGB -ErrorAction SilentlyContinue
$serviceConfig = Join-Path $env:ProgramFiles 'OpenRGB\service_config\OpenRGB.json'
if ($service) {
    if (-not (Test-Path -LiteralPath $serviceConfig)) { throw 'OpenRGB service found but its expected configuration file is missing; refusing to configure a different instance.' }
    $openRgbFile = $serviceConfig
}
$deviceFile = Join-Path $env:APPDATA 'Aurora\DeviceConfig.json'
$auroraFile = Join-Path $env:APPDATA 'Aurora\Config.json'
foreach ($path in @($openRgbExe,$auroraExe,$openRgbFile,$deviceFile,$auroraFile)) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing installed application/configuration: $path" }
}
if (-not $Apply) {
    Write-Output 'Ready: add only Red Blade FS VialRGB detector; SDK on 127.0.0.1:6742; Aurora at ~29 FPS; RGB application startup.'
    Write-Output 'Use -Apply to back up configuration, restart RGB applications and apply these settings. No firmware is flashed.'
    return
}

$backup = Join-Path $BackupDirectory (Get-Date -Format 'yyyyMMdd-HHmmss')
New-Item -ItemType Directory -Path $backup -Force | Out-Null
Copy-Item -LiteralPath $openRgbFile -Destination (Join-Path $backup 'OpenRGB.json')
Copy-Item -LiteralPath $deviceFile -Destination (Join-Path $backup 'DeviceConfig.json')
Copy-Item -LiteralPath $auroraFile -Destination (Join-Path $backup 'AuroraConfig.json')
if ($service -and (Test-Path -LiteralPath $openRgbUserFile)) {
    Copy-Item -LiteralPath $openRgbUserFile -Destination (Join-Path $backup 'OpenRGB-user.json')
}

# Remember what was running so a failed run can return to that state.
$serviceWasRunning = $service -and $service.Status -eq 'Running'
$runningProcesses = @(Get-Process -Name AuroraRgb,OpenRGB -ErrorAction SilentlyContinue)
$wasRunning = @($runningProcesses | Select-Object -ExpandProperty ProcessName -Unique)
$previousApps = @{}
$previousSdkPort = 6742
foreach ($process in $runningProcesses) {
    if ($process.ProcessName -eq 'OpenRGB' -and $serviceWasRunning) { continue }
    $exe = $process.Path
    $command = (Get-CimInstance Win32_Process -Filter ('ProcessId='+$process.Id)).CommandLine
    if (-not $exe -or -not $command) { throw 'Cannot preserve an RGB application startup command; no applications have been stopped.' }
    if ($command.StartsWith('"')) { $arguments = $command.Substring($command.IndexOf('"',1)+1).TrimStart() }
    elseif ($command.StartsWith($exe,[StringComparison]::OrdinalIgnoreCase)) { $arguments = $command.Substring($exe.Length).TrimStart() }
    else { throw 'Cannot safely preserve RGB application arguments; no applications have been stopped.' }
    # Keep original commands in memory, never in logs or configuration backups.
    $previousApps[$process.ProcessName] = @{ Exe=$exe; Arguments=$arguments }
    if ($process.ProcessName -eq 'OpenRGB' -and $arguments -match '--server-port(?:\s+|=)"?(\d+)"?(?:\s|$)') {
        $previousSdkPort = [int]$Matches[1]
    }
}
$appsStopped = $false
$configsDirty = $false
try {
    # Prevent the applications from overwriting their configuration on exit.
    $appsStopped = $true
    if ($service) { Stop-Service -Name OpenRGB }
    Get-Process -Name AuroraRgb,AuroraDeviceManager,OpenRGB -ErrorAction SilentlyContinue | Stop-Process

    # Some OpenRGB / RGB.NET combinations stream colors without selecting the
    # keyboard's Direct effect. Establish it explicitly while Raw HID is idle,
    # then persist this lighting setting once for the next hardware boot.
    $directConfigured=$false
    $directTool=Join-Path $PSScriptRoot 'set-direct-mode.py'
    if (Test-Path -LiteralPath $directTool) {
        & python $directTool --save
        $directResult=$LASTEXITCODE
        if ($directResult -eq 0) { $directConfigured=$true }
        elseif ($directResult -ne 2) { throw 'Could not establish Direct RGB mode; see the preserved configuration backup.' }
    }

    $configsDirty = $true
    $rgb = Get-Content -Raw -LiteralPath $openRgbFile | ConvertFrom-Json -AsHashtable
    if (-not $rgb.Contains('QMKVialRGBDevices')) { $rgb['QMKVialRGBDevices'] = @{devices=@()} }
    $otherDevices = @($rgb['QMKVialRGBDevices']['devices'] | Where-Object {
        -not (([string]$_.usb_vid -replace '^0x','') -ieq '320f' -and ([string]$_.usb_pid -replace '^0x','') -ieq '5136')
    })
    $rgb['QMKVialRGBDevices']['devices'] = @($otherDevices) + @(@{name='Designed by GG Red Blade FS (VialRGB)';usb_vid='0x320F';usb_pid='0x5136'})
    if (-not $rgb.Contains('Server')) { $rgb['Server']=@{} }
    $rgb['Server']['default_host']='127.0.0.1'
    $rgb['Server']['default_port']=6742
    $rgb['Server']['all_controllers']=$false
    $rgb['Detectors']['detectors']['QMK VialRGB Devices']=$true
    $rgb | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $openRgbFile -Encoding utf8NoBOM

    $device = Get-Content -Raw -LiteralPath $deviceFile | ConvertFrom-Json -AsHashtable
    $device['EnabledDevices'] = @($device['EnabledDevices'] | Where-Object {$_ -ne 'OpenRGB (RGB.NET)'}) + @('OpenRGB (RGB.NET)')
    $device['DangerousOpenRgbNonDirectEnable']=$false
    $device['VarRegistry']['Variables']['OpenRGB (RGB.NET)_ip']['Value']='127.0.0.1'
    $device['VarRegistry']['Variables']['OpenRGB (RGB.NET)_port']['Value']=6742
    $device | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $deviceFile -Encoding utf8NoBOM

    $aurora = Get-Content -Raw -LiteralPath $auroraFile | ConvertFrom-Json -AsHashtable
    $aurora['UpdateDelay']=34
    $aurora['Fps']=1000.0/34.0
    $aurora | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $auroraFile -Encoding utf8NoBOM

    if (-not $NoAutostart) {
        $startup=[Environment]::GetFolderPath('Startup')
        $shell=New-Object -ComObject WScript.Shell
        $run=Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -ErrorAction SilentlyContinue
        foreach ($app in @(
            @{Name='OpenRGB';Exe=$openRgbExe;Args='--server --server-host 127.0.0.1 --server-port 6742 --startminimized'},
            @{Name='Aurora';Exe=$auroraExe;Args='-silent'}
        )) {
            if ($app.Name -eq 'OpenRGB' -and $service) { continue }
            $registered=@($run.PSObject.Properties | Where-Object { [string]$_.Value -like ('*'+$app.Exe+'*') }).Count -gt 0
            $shortcutExists=@(Get-ChildItem -LiteralPath $startup -Filter ('*'+$app.Name+'*.lnk') -File).Count -gt 0
            if (-not $registered -and -not $shortcutExists) {
                $shortcut=$shell.CreateShortcut((Join-Path $startup ($app.Name+' - Red Blade.lnk')))
                $shortcut.TargetPath=$app.Exe
                $shortcut.Arguments=$app.Args
                $shortcut.WorkingDirectory=Split-Path -Parent $app.Exe
                $shortcut.WindowStyle=7
                $shortcut.Save()
            }
        }
    }

    if ($service) {
        Start-Service -Name OpenRGB
    } else {
        Start-Process -FilePath $openRgbExe -ArgumentList @('--server','--server-host','127.0.0.1','--server-port','6742','--startminimized') -WindowStyle Hidden
    }
    $listening=$false
    for($attempt=0;$attempt -lt 60;$attempt++) {
        $client=[Net.Sockets.TcpClient]::new()
        try { $client.Connect('127.0.0.1',6742); $listening=$true; break } catch { Start-Sleep -Milliseconds 500 } finally { $client.Dispose() }
    }
    if (-not $listening) { throw "Configuration saved and backed up at $backup; OpenRGB SDK did not start." }
    Start-Process -FilePath $auroraExe -ArgumentList '-silent' -WindowStyle Hidden
    $configsDirty = $false
    $appsStopped = $false
    if ($directConfigured) {
        Write-Output "Configured. Backup: $backup. OpenRGB SDK is listening on loopback; keyboard Direct RGB is saved."
    } else {
        Write-Output "Configured. Backup: $backup. OpenRGB SDK is listening on loopback. Keyboard RGB still requires the prepared firmware."
    }
} catch {
    $configurationError = $_
    # A failure between stopping and restarting the applications must not
    # leave half-written configuration files or the RGB stack switched off.
    if ($configsDirty) {
        # A restart may have partially succeeded. Stop those instances before
        # restoring their files so they cannot overwrite the rollback on exit.
        if ($service) {
            try { Stop-Service -Name OpenRGB }
            catch { Write-Warning ('Could not stop OpenRGB for rollback: ' + $_.Exception.Message) -WarningAction Continue }
        }
        try { Get-Process -Name AuroraRgb,AuroraDeviceManager,OpenRGB -ErrorAction SilentlyContinue | Stop-Process }
        catch { Write-Warning ('Could not stop RGB applications for rollback: ' + $_.Exception.Message) -WarningAction Continue }
        foreach ($config in @(
            @{ Backup='OpenRGB.json'; Destination=$openRgbFile },
            @{ Backup='DeviceConfig.json'; Destination=$deviceFile },
            @{ Backup='AuroraConfig.json'; Destination=$auroraFile }
        )) {
            try { Copy-Item -LiteralPath (Join-Path $backup $config.Backup) -Destination $config.Destination -Force }
            catch { Write-Warning ('Could not restore ' + $config.Backup + ': ' + $_.Exception.Message) -WarningAction Continue }
        }
        Write-Warning "Configuration recovery attempted from $backup." -WarningAction Continue
    }
    if ($appsStopped) {
        try {
            if ($serviceWasRunning) {
                if ((Get-Service -Name OpenRGB).Status -ne 'Running') { Start-Service -Name OpenRGB }
            } elseif ($wasRunning -contains 'OpenRGB' -and -not (Get-Process -Name OpenRGB -ErrorAction SilentlyContinue)) {
                $previous = $previousApps['OpenRGB']
                $start = @{ FilePath=$previous.Exe; WindowStyle='Hidden' }
                if ($previous.Arguments) { $start.ArgumentList=$previous.Arguments }
                Start-Process @start
            }
        } catch { Write-Warning ('Could not restart OpenRGB: ' + $_.Exception.Message) -WarningAction Continue }
        try {
            if ($wasRunning -contains 'AuroraRgb' -and -not (Get-Process -Name AuroraRgb -ErrorAction SilentlyContinue)) {
                # Aurora connects to the OpenRGB SDK once at startup.
                if ($serviceWasRunning -or $wasRunning -contains 'OpenRGB') {
                    for ($attempt=0; $attempt -lt 60; $attempt++) {
                        $client=[Net.Sockets.TcpClient]::new()
                        try { $client.Connect('127.0.0.1',$previousSdkPort); break } catch { Start-Sleep -Milliseconds 500 } finally { $client.Dispose() }
                    }
                }
                $previous = $previousApps['AuroraRgb']
                $start = @{ FilePath=$previous.Exe; WindowStyle='Hidden' }
                if ($previous.Arguments) { $start.ArgumentList=$previous.Arguments }
                Start-Process @start
            }
        } catch { Write-Warning ('Could not restart the RGB applications: ' + $_.Exception.Message) -WarningAction Continue }
    }
    throw $configurationError
}
