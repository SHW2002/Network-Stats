$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$privateSdk = Join-Path $env:LOCALAPPDATA 'NetworkStats\dotnet\dotnet.exe'
$sdk = if (Test-Path -LiteralPath $privateSdk) { $privateSdk } else { (Get-Command dotnet).Source }
$artifacts = [System.IO.Path]::GetFullPath((Join-Path $workspace 'artifacts'))
$identifier = [Guid]::NewGuid().ToString('N')
$staging = Join-Path $artifacts ('.windows-publish-' + $identifier)
$backup = Join-Path $artifacts ('.windows-previous-' + $identifier)
$destination = Join-Path $artifacts 'windows'

function Assert-PublishDirectory([string]$path) {
    $resolved = [System.IO.Path]::GetFullPath($path)
    if (-not $resolved.StartsWith($artifacts + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Publish directory is outside this project artifacts directory.'
    }
    if ((Test-Path -LiteralPath $resolved) -and
        ((Get-Item -LiteralPath $resolved -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
        throw 'Refusing to replace or remove a linked publish directory.'
    }
}

Push-Location $workspace
try {
    Assert-PublishDirectory $staging
    Assert-PublishDirectory $backup
    Assert-PublishDirectory $destination
    $framework = 'net10.0-windows10.0.19041.0'
    & $sdk publish src/NetworkStats.App/NetworkStats.App.csproj `
        -p:NetworkStatsTargetFramework=$framework -f $framework -c Release -r win-x64 `
        -p:PublishProfile=WindowsPortable -p:DebugType=None -p:DebugSymbols=false -o $staging
    if ($LASTEXITCODE -ne 0) { throw 'Windows publish failed; previous package is unchanged.' }

    $publishedExe = Join-Path $staging 'Network-Stats.exe'
    $files = @(Get-ChildItem -LiteralPath $staging -File -Recurse)
    if ($files.Count -ne 1 -or -not (Test-Path -LiteralPath $publishedExe)) {
        throw 'Expected a single self-contained EXE; previous package is unchanged.'
    }
    & (Join-Path $PSScriptRoot 'test-windows-package.ps1') -ExecutablePath $publishedExe

    $publishedDirectory = $destination
    if (Test-Path -LiteralPath $destination) {
        try { Move-Item -LiteralPath $destination -Destination $backup }
        catch {
            # 运行中的 EXE 会锁住发布目录。保留已验证的新包，不终止用户的进程。
            $publishedDirectory = Join-Path $artifacts 'windows-update'
            if (Test-Path -LiteralPath $publishedDirectory) {
                $publishedDirectory = Join-Path $artifacts ('windows-update-' + $identifier)
            }
            Assert-PublishDirectory $publishedDirectory
            Move-Item -LiteralPath $staging -Destination $publishedDirectory
            Write-Warning "Could not replace the existing package: $($_.Exception.Message) Close the old app and use the new EXE at $publishedDirectory."
        }
    }
    if ($publishedDirectory -eq $destination) {
        try { Move-Item -LiteralPath $staging -Destination $destination }
        catch {
            if (Test-Path -LiteralPath $backup) { Move-Item -LiteralPath $backup -Destination $destination }
            throw
        }
    }
    if (Test-Path -LiteralPath $backup) {
        Assert-PublishDirectory $backup
        try { Remove-Item -LiteralPath $backup -Recurse -Force }
        catch { Write-Warning "Previous package is still in use and was kept at $backup" }
    }
    $result = Get-Item -LiteralPath (Join-Path $publishedDirectory 'Network-Stats.exe')
    Write-Output ("Published: {0} ({1:N1} MB, one file)" -f $result.FullName, ($result.Length / 1MB))
} finally {
    if (Test-Path -LiteralPath $staging) {
        Assert-PublishDirectory $staging
        Remove-Item -LiteralPath $staging -Recurse -Force
    }
    Pop-Location
}
