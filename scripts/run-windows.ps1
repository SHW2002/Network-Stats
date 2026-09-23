param([switch]$Publish, [switch]$Development, [switch]$CloseRunning)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
if ($Publish) {
    & (Join-Path $PSScriptRoot 'publish-windows.ps1') -CloseRunning:$CloseRunning
    return
}
$published = Join-Path $workspace 'artifacts\windows\Network-Stats.exe'
if (-not $Development -and (Test-Path -LiteralPath $published)) {
    Start-Process -FilePath $published -WindowStyle Normal
    return
}
$privateSdk = Join-Path $env:LOCALAPPDATA 'NetworkStats\dotnet\dotnet.exe'
$sdk = if (Test-Path -LiteralPath $privateSdk) { $privateSdk } else { (Get-Command dotnet).Source }
$env:DOTNET_ROOT = Split-Path -Parent $sdk
Push-Location $workspace
try {
    $project = 'src/NetworkStats.App/NetworkStats.App.csproj'
    $framework = 'net10.0-windows10.0.19041.0'
    & $sdk build $project -p:NetworkStatsTargetFramework=$framework -f $framework
    if ($LASTEXITCODE -ne 0) { throw 'Windows build failed.' }
    $executable = Join-Path $workspace "src\NetworkStats.App\bin\Debug\$framework\win-x64\Network-Stats.exe"
    Start-Process -FilePath $executable -WindowStyle Normal
} finally { Pop-Location }
