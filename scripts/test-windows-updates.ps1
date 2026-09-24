$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$sdkDirectory = Join-Path $env:LOCALAPPDATA 'NetworkStats\dotnet'
$sdk = if (Test-Path (Join-Path $sdkDirectory 'dotnet.exe')) { Join-Path $sdkDirectory 'dotnet.exe' } else { (Get-Command dotnet).Source }
$env:DOTNET_ROOT = Split-Path -Parent $sdk
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
Push-Location $workspace
try {
    foreach ($version in @('1.0.0', '2.0.0')) {
        & $sdk publish tests/NetworkStats.UpdateFixture/NetworkStats.UpdateFixture.csproj -c Release -r win-x64 `
            --self-contained false -p:PublishSingleFile=true -p:Version=$version -o "artifacts/update-fixtures/$version" -v:quiet
        if ($LASTEXITCODE -ne 0) { throw 'Update fixture build failed.' }
    }
    & $sdk run --project tests/NetworkStats.DesktopTests/NetworkStats.DesktopTests.csproj -- `
        --test-updater (Join-Path $workspace 'artifacts/update-fixtures/1.0.0/Network-Stats.exe') `
        (Join-Path $workspace 'artifacts/update-fixtures/2.0.0/Network-Stats.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Update installation checks failed.' }
} finally { Pop-Location }
