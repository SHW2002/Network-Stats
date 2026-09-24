$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$privateSdk = Join-Path $env:LOCALAPPDATA 'NetworkStats\dotnet\dotnet.exe'
$sdk = if (Test-Path -LiteralPath $privateSdk) { $privateSdk } else { (Get-Command dotnet).Source }
Push-Location $workspace
try {
    $arguments = @('run', '--project', 'tests/NetworkStats.DesktopTests/NetworkStats.DesktopTests.csproj')
    & $sdk @arguments
    if ($LASTEXITCODE -ne 0) { throw 'Windows startup checks failed.' }
} finally { Pop-Location }
