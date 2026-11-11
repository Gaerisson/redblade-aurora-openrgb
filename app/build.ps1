param(
    [string]$HelperDirectory,
    [string]$WebView2Bootstrapper = (Join-Path $PSScriptRoot '../../../work/studio-prerequisites/MicrosoftEdgeWebview2Setup.exe'),
    [switch]$SkipSelfTest
)
$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '.local/dotnet'
$env:NUGET_PACKAGES = Join-Path $PSScriptRoot '.local/nuget'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_NOLOGO = '1'
$dotnet = Join-Path $env:ProgramFiles 'dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) { throw '.NET 10 Windows SDK is required.' }
$output = Join-Path $PSScriptRoot 'publish'
$arguments = @('publish', (Join-Path $PSScriptRoot 'RedBladePlus.Studio.csproj'), '--nologo', '-c', 'Release', '-r', 'win-x64', '--self-contained', 'true', '-o', $output)
if ($HelperDirectory) { $arguments += ('-p:HelperDirectory=' + [IO.Path]::GetFullPath($HelperDirectory)) }
$arguments += ('-p:WebView2Bootstrapper=' + [IO.Path]::GetFullPath($WebView2Bootstrapper))
& $dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw 'Studio publishing failed.' }
if (-not $SkipSelfTest) {
    $test = Start-Process -FilePath (Join-Path $output 'RedBladePlus-Studio.exe') -ArgumentList '--self-test' -Wait -PassThru -WindowStyle Hidden
    Get-Content -LiteralPath (Join-Path $output 'self-test-results.txt')
    if ($test.ExitCode -ne 0) { throw 'Hardware-free Studio self-tests failed.' }
}
Write-Output (Join-Path $output 'RedBladePlus-Studio.exe')
