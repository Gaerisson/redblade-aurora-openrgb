#requires -Version 7.0
<# Run real script logic with temporary JSON and simulated OS/Python boundaries.
   No installed process, service, keyboard, startup shortcut or config is changed.
   No Pester/module download is required; this also runs under pwsh on Linux CI. #>
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$sandbox = Join-Path $PSScriptRoot ('windows-fixture-' + [guid]::NewGuid().ToString('N'))
$passed = 0
$failed = 0
$savedEnvironment = @{}
foreach ($name in 'ProgramFiles','APPDATA','LOCALAPPDATA') { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }

function Assert-True($Condition, [string]$Message) { if (-not $Condition) { throw $Message } }
function Test-Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $script:passed++; Write-Host "PASS $Name" }
    catch { $script:failed++; Write-Host "FAIL ${Name}: $($_.Exception.Message)" }
}

function New-Fixture([string]$Name, [switch]$MissingVariables) {
    $dir = Join-Path $sandbox $Name
    foreach ($folder in 'programs/OpenRGB','programs/Aurora','app/OpenRGB','app/Aurora','local','backups') {
        New-Item -ItemType Directory -Path (Join-Path $dir $folder) -Force | Out-Null
    }
    foreach ($exe in 'programs/OpenRGB/OpenRGB.exe','programs/Aurora/AuroraRgb.exe') {
        Set-Content -LiteralPath (Join-Path $dir $exe) -Value 'not an executable'
    }
    Set-Content -LiteralPath (Join-Path $dir 'app/OpenRGB/OpenRGB.json') -Value '{"Server":{},"Detectors":{"detectors":{}},"Keep":"rgb-original"}'
    $variables = if ($MissingVariables) { '' } else { ',"VarRegistry":{"Variables":{"OpenRGB (RGB.NET)_ip":{"Value":"old"},"OpenRGB (RGB.NET)_port":{"Value":1}}}' }
    Set-Content -LiteralPath (Join-Path $dir 'app/Aurora/DeviceConfig.json') -Value ('{"EnabledDevices":[],"Keep":"device-original"' + $variables + '}')
    Set-Content -LiteralPath (Join-Path $dir 'app/Aurora/Config.json') -Value '{"UpdateDelay":20,"Keep":"aurora-original"}'
    return $dir
}

function Invoke-ConfigFixture([string]$Name, [switch]$DirectFailure, [switch]$MissingVariables,
                              [switch]$RestartFailure, [switch]$RollbackFailure, [switch]$Service,
                              [switch]$WarningStop, [int]$OriginalPort=6742) {
    $dir = New-Fixture $Name -MissingVariables:$MissingVariables
    $env:ProgramFiles = Join-Path $dir 'programs'
    $env:APPDATA = Join-Path $dir 'app'
    $env:LOCALAPPDATA = Join-Path $dir 'local'
    if ($Service) {
        New-Item -ItemType Directory -Path (Join-Path $dir 'programs/OpenRGB/service_config') | Out-Null
        Copy-Item (Join-Path $dir 'app/OpenRGB/OpenRGB.json') (Join-Path $dir 'programs/OpenRGB/service_config/OpenRGB.json')
    }
    $configPaths = @((Join-Path $dir $(if ($Service) { 'programs/OpenRGB/service_config/OpenRGB.json' } else { 'app/OpenRGB/OpenRGB.json' })),
                    (Join-Path $dir 'app/Aurora/DeviceConfig.json'),(Join-Path $dir 'app/Aurora/Config.json'))
    $original = @($configPaths | ForEach-Object { [IO.File]::ReadAllText($_) })
    $state = @{
        Apps = [Collections.Generic.List[string]]::new(); Events = [Collections.Generic.List[string]]::new()
        Service = [bool]$Service; ServiceRunning = [bool]$Service; DirectFailure = [bool]$DirectFailure
        RestartFailures = [int][bool]$RestartFailure; RollbackFailures = [int][bool]$RollbackFailure
        BackupRoot = Join-Path $dir 'backups'
        OpenRgbPath = Join-Path $dir 'programs/OpenRGB/OpenRGB.exe'
        AuroraPath = Join-Path $dir 'programs/Aurora/AuroraRgb.exe'
        OpenRgbArguments = "--server --server-host 127.0.0.1 --server-port $OriginalPort --startminimized"
        RestoredOpenRgbArguments = $null
    }
    $state.Apps.Add('AuroraRgb')
    if (-not $Service) { $state.Apps.Add('OpenRGB') }
    function Get-Service { [CmdletBinding()]param($Name)
        if ($state.Service) { [pscustomobject]@{ Name='OpenRGB'; Status=$(if ($state.ServiceRunning) { 'Running' } else { 'Stopped' }) } }
    }
    function Stop-Service { [CmdletBinding()]param($Name) $state.ServiceRunning=$false; $state.Events.Add('stop-service') }
    function Start-Service { [CmdletBinding()]param($Name) $state.ServiceRunning=$true; $state.Events.Add('start-OpenRGB') }
    function Get-Process { [CmdletBinding()]param([string[]]$Name)
        foreach ($app in @($state.Apps.ToArray())) {
            if ($Name -contains $app) {
                [pscustomobject]@{ ProcessName=$app; Id=$(if ($app -eq 'OpenRGB') { 2 } else { 1 });
                    Path=$(if ($app -eq 'OpenRGB') { $state.OpenRgbPath } else { $state.AuroraPath }) }
            }
        }
    }
    function Get-CimInstance { [CmdletBinding()]param($ClassName,$Filter)
        [pscustomobject]@{ CommandLine=$(if ($Filter -eq 'ProcessId=2') {
            '"'+$state.OpenRgbPath+'" '+$state.OpenRgbArguments
        } else { '"'+$state.AuroraPath+'" -silent' }) }
    }
    function Stop-Process { [CmdletBinding()]param([Parameter(ValueFromPipeline)]$InputObject)
        process { [void]$state.Apps.Remove($InputObject.ProcessName); $state.Events.Add('stop-'+$InputObject.ProcessName) }
    }
    function Start-Process { [CmdletBinding()]param($FilePath,$ArgumentList,$WindowStyle)
        $app=[IO.Path]::GetFileNameWithoutExtension($FilePath)
        $state.Events.Add('start-'+$app)
        if ($app -eq 'AuroraRgb' -and $state.RestartFailures -gt 0) { $state.RestartFailures--; throw 'Injected Aurora restart failure' }
        if ($app -eq 'OpenRGB') { $state.RestoredOpenRgbArguments=$ArgumentList -join ' ' }
        Assert-True ($WindowStyle -eq 'Hidden') 'Restart must stay hidden'
        if (-not $state.Apps.Contains($app)) { $state.Apps.Add($app) }
    }
    function Copy-Item { [CmdletBinding()]param($LiteralPath,$Destination,[switch]$Force)
        if ($state.RollbackFailures -gt 0 -and ([IO.Path]::GetDirectoryName($LiteralPath)).StartsWith($state.BackupRoot)) {
            $state.RollbackFailures--; throw 'Injected rollback-copy failure'
        }
        Microsoft.PowerShell.Management\Copy-Item -LiteralPath $LiteralPath -Destination $Destination -Force:$Force
    }
    function python { $global:LASTEXITCODE = [int]$state.DirectFailure }
    $caught = $null
    try {
        $configureArgs=@{ Apply=$true; NoAutostart=$true; BackupDirectory=$state.BackupRoot }
        if ($WarningStop) { $configureArgs.WarningAction='Stop' }
        & (Join-Path $root 'tools/configure-windows.ps1') @configureArgs | Out-Null
    }
    catch { $caught = $_ }
    return @{ State=$state; Error=$caught; Paths=$configPaths; Original=$original }
}

# A loopback listener makes the real SDK wait deterministic without RGB software.
# If the owner's SDK already owns 6742, only a TCP readiness probe reaches it.
$listener = $null
try {
    New-Item -ItemType Directory -Path $sandbox | Out-Null
    try {
        $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,6742)
        $listener.Start()
    } catch {
        if ($listener) { $listener.Stop(); $listener=$null }
        $probe=[Net.Sockets.TcpClient]::new()
        try { $probe.Connect('127.0.0.1',6742) } finally { $probe.Dispose() }
    }

    Test-Case 'Direct-mode failure restores standalone apps without config writes' {
        $result=Invoke-ConfigFixture direct -DirectFailure
        Assert-True ($result.Error.Exception.Message -like '*Could not establish Direct RGB*') 'Original Direct-mode error was lost'
        Assert-True ($result.State.Apps.Contains('OpenRGB') -and $result.State.Apps.Contains('AuroraRgb')) 'Apps left stopped'
        for ($i=0;$i -lt 3;$i++) { Assert-True ([IO.File]::ReadAllText($result.Paths[$i]) -ceq $result.Original[$i]) 'Config changed before first write' }
        $starts=@($result.State.Events | Where-Object { $_ -like 'start-*' })
        Assert-True (($starts -join ',') -eq 'start-OpenRGB,start-AuroraRgb') 'Wrong recovery order'
    }
    Test-Case 'Direct-mode failure restores the previous OpenRGB service' {
        $result=Invoke-ConfigFixture service -Service -DirectFailure
        Assert-True ($null -ne $result.Error) 'Expected Direct-mode failure'
        Assert-True ($result.State.ServiceRunning -and $result.State.Apps.Contains('AuroraRgb')) 'Service/Aurora left stopped'
        Assert-True (-not $result.State.Apps.Contains('OpenRGB')) 'Created a second standalone OpenRGB'
    }
    Test-Case 'Missing Aurora registry rolls back all three config files' {
        $result=Invoke-ConfigFixture missing -MissingVariables
        Assert-True ($null -ne $result.Error) 'Malformed config unexpectedly accepted'
        for ($i=0;$i -lt 3;$i++) { Assert-True ([IO.File]::ReadAllText($result.Paths[$i]) -ceq $result.Original[$i]) 'Partial configuration was retained' }
        Assert-True ($result.State.Apps.Count -eq 2) 'Apps left stopped after rollback'
    }
    Test-Case 'Failed Aurora restart still rolls back and recovers the previous apps' {
        $result=Invoke-ConfigFixture restart -RestartFailure
        Assert-True ($result.Error.Exception.Message -eq 'Injected Aurora restart failure') 'Original restart error was lost'
        for ($i=0;$i -lt 3;$i++) { Assert-True ([IO.File]::ReadAllText($result.Paths[$i]) -ceq $result.Original[$i]) 'Restart failure retained changed configuration' }
        Assert-True ($result.State.Apps.Contains('OpenRGB') -and $result.State.Apps.Contains('AuroraRgb')) 'Recovery disabled before restart completed'
    }
    Test-Case 'Rollback-copy failure preserves the original error and still restarts apps' {
        $result=Invoke-ConfigFixture rollback -MissingVariables -RollbackFailure
        Assert-True ($result.Error.Exception.Message -like '*null*') 'Rollback failure replaced the original config error'
        Assert-True ($result.State.RollbackFailures -eq 0) 'Rollback was never attempted'
        Assert-True ($result.State.Apps.Contains('OpenRGB') -and $result.State.Apps.Contains('AuroraRgb')) 'Rollback-copy error skipped application recovery'
        for ($i=1;$i -lt 3;$i++) { Assert-True ([IO.File]::ReadAllText($result.Paths[$i]) -ceq $result.Original[$i]) 'Remaining rollback copies were skipped' }
    }
    Test-Case 'Successful configuration retains the requested settings' {
        $result=Invoke-ConfigFixture success
        Assert-True ($null -eq $result.Error) ('Configuration failed: '+$result.Error)
        $rgb=Get-Content -Raw -LiteralPath $result.Paths[0] | ConvertFrom-Json
        $aurora=Get-Content -Raw -LiteralPath $result.Paths[2] | ConvertFrom-Json
        Assert-True ($rgb.Server.default_port -eq 6742 -and $aurora.UpdateDelay -eq 34) 'Settings not applied'
        Assert-True ($rgb.Keep -eq 'rgb-original' -and $aurora.Keep -eq 'aurora-original') 'Unrelated settings changed'
        Assert-True ($result.State.Apps.Count -eq 2) 'Apps not running after success'
    }
    Test-Case 'Recovery retains original standalone OpenRGB arguments and SDK port' {
        $sdk=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0); $sdk.Start()
        try {
            $port=$sdk.LocalEndpoint.Port
            $result=Invoke-ConfigFixture custom-port -DirectFailure -OriginalPort $port
            Assert-True ($result.Error.Exception.Message -like '*Could not establish Direct RGB*') 'Original error was lost'
            Assert-True ($result.State.RestoredOpenRgbArguments -eq "--server --server-host 127.0.0.1 --server-port $port --startminimized") 'Original OpenRGB command arguments were discarded'
            Assert-True ($sdk.Pending()) 'Recovery did not wait for the original SDK port'
            Assert-True ($result.State.Apps.Count -eq 2) 'Apps not restored'
        } finally { $sdk.Stop() }
    }
    Test-Case 'WarningAction Stop cannot interrupt rollback or replace the first error' {
        $result=Invoke-ConfigFixture warning-stop -RestartFailure -RollbackFailure -WarningStop
        Assert-True ($result.Error.Exception.Message -eq 'Injected Aurora restart failure') 'Recovery warning replaced the original restart error'
        Assert-True ($result.State.RollbackFailures -eq 0) 'Rollback was never attempted'
        Assert-True ($result.State.Apps.Count -eq 2) 'Recovery warning prevented application restart'
        for ($i=1;$i -lt 3;$i++) { Assert-True ([IO.File]::ReadAllText($result.Paths[$i]) -ceq $result.Original[$i]) 'Later changed files were not restored after first copy failure' }
    }
    Test-Case 'HID benchmark restores standalone SDK before Aurora on its original port' {
        $sdk=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0); $sdk.Start()
        try {
            $port=$sdk.LocalEndpoint.Port
            $events=[Collections.Generic.List[string]]::new()
            $session=99; $serviceWasRunning=$false; $stopped=$false; $failed=$false; $restart=@(); $Hid=$true
            $OutputDirectory=Join-Path $sandbox 'benchmark'; $NativeExe=Join-Path $sandbox 'unused.exe'
            function Get-Process { [CmdletBinding()]param([string[]]$Name)
                foreach ($app in 'AuroraRgb','OpenRGB') { [pscustomobject]@{ ProcessName=$app; Id=$(if ($app -eq 'OpenRGB') { 2 } else { 1 }); SessionId=99; Path="/$app.exe" } }
            }
            function Get-CimInstance { [CmdletBinding()]param($ClassName,$Filter)
                [pscustomobject]@{ CommandLine=$(if ($Filter -eq 'ProcessId=2') { '"/OpenRGB.exe" --server --server-port '+$port } else { '"/AuroraRgb.exe" -silent' }) }
            }
            function Stop-Process { [CmdletBinding()]param([Parameter(ValueFromPipeline)]$InputObject) process {} }
            function Start-Process { [CmdletBinding()]param($FilePath,$ArgumentList,$WindowStyle)
                $events.Add([IO.Path]::GetFileNameWithoutExtension($FilePath))
                Assert-True ($WindowStyle -eq 'Hidden') 'Restart must stay hidden'
            }
            function python { $global:LASTEXITCODE=0 }
            $tokens=$null; $parseErrors=$null
            $ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'tools/measure-performance.ps1'),[ref]$tokens,[ref]$parseErrors)
            Assert-True ($parseErrors.Count -eq 0) 'Benchmark PowerShell parse failed'
            # Execute its actual main try/finally; omit only elevation and final exit.
            $main=@($ast.EndBlock.Statements | Where-Object { $_ -is [Management.Automation.Language.TryStatementAst] })
            Assert-True ($main.Count -eq 1) 'Expected one main benchmark try/finally'
            $benchmarkScript=Join-Path $sandbox 'benchmark-main.ps1'
            Set-Content -LiteralPath $benchmarkScript -Value ('$restart=@(); $stopped=$false; $failed=$false; ' + $main[0].Extent.Text + '; [pscustomobject]@{Failed=$failed}')
            $outcome=& $benchmarkScript
            Assert-True (($events -join ',') -eq 'OpenRGB,AuroraRgb') 'Aurora restarted before OpenRGB'
            Assert-True (-not $outcome.Failed) 'Benchmark/restart failed'
            Assert-True ($sdk.Pending()) 'Custom SDK port was never probed'
        } finally { $sdk.Stop() }
    }
} finally {
    if ($listener) { $listener.Stop() }
    foreach ($name in $savedEnvironment.Keys) { [Environment]::SetEnvironmentVariable($name,$savedEnvironment[$name]) }
    if (Test-Path -LiteralPath $sandbox) {
        $resolved=[IO.Path]::GetFullPath($sandbox)
        $allowed=[IO.Path]::GetFullPath($PSScriptRoot)+[IO.Path]::DirectorySeparatorChar
        if (-not $resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)) { throw 'Refusing cleanup outside test directory' }
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
Write-Host "$passed passed; $failed failed"
if ($failed) { exit 1 }
