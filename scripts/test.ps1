$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$privateSdk = Join-Path $env:LOCALAPPDATA 'NetworkStats\dotnet\dotnet.exe'
$sdk = if (Test-Path -LiteralPath $privateSdk) { $privateSdk } else { (Get-Command dotnet).Source }
Push-Location $workspace
try {
    & $sdk run --project tests/NetworkStats.Tests/NetworkStats.Tests.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
} finally { Pop-Location }
