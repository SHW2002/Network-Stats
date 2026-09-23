param([switch]$Publish)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$privateSdk = Join-Path $env:LOCALAPPDATA 'NetworkStats\dotnet\dotnet.exe'
$sdk = if (Test-Path -LiteralPath $privateSdk) { $privateSdk } else { (Get-Command dotnet).Source }
$env:DOTNET_ROOT = Split-Path -Parent $sdk
Push-Location $workspace
try {
    $project = 'src/NetworkStats.App/NetworkStats.App.csproj'
    $framework = 'net10.0-windows10.0.19041.0'
    if ($Publish) {
        & $sdk publish $project -p:NetworkStatsTargetFramework=$framework -f $framework -c Release -r win-x64 --self-contained true -o artifacts/windows
        if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed.' }
        Write-Output "Published to $workspace\artifacts\windows\NetworkStats.App.exe"
    } else {
        & $sdk build $project -p:NetworkStatsTargetFramework=$framework -f $framework
        if ($LASTEXITCODE -ne 0) { throw 'Windows build failed.' }
        $executable = Join-Path $workspace "src\NetworkStats.App\bin\Debug\$framework\win-x64\NetworkStats.App.exe"
        Start-Process -FilePath $executable -WindowStyle Normal
    }
} finally { Pop-Location }
