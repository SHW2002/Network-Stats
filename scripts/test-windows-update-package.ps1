param(
    [Parameter(Mandatory = $true)][string]$ExecutablePath,
    [string]$OldFixturePath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts/update-fixtures/1.0.0/Network-Stats.exe')
)
$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$privateSdk = Join-Path $env:LOCALAPPDATA 'NetworkStats/dotnet/dotnet.exe'
$sdkDirectory = if (Test-Path -LiteralPath $privateSdk) { Split-Path -Parent $privateSdk } else { Split-Path -Parent (Get-Command dotnet).Source }
if (-not ('WindowsDesktopProcess' -as [type])) { Add-Type -Path (Join-Path $PSScriptRoot 'WindowsDesktopProcess.cs') }
$source = (Resolve-Path -LiteralPath $ExecutablePath).Path
$fixture = (Resolve-Path -LiteralPath $OldFixturePath).Path
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$testRoot = [IO.Path]::GetFullPath((Join-Path $temporaryRoot ('NetworkStats update package ' + [Guid]::NewGuid().ToString('N'))))
$dataDirectory = Join-Path $testRoot 'data'
$sessionDirectory = Join-Path $dataDirectory ('updates/' + [Guid]::NewGuid().ToString('N'))
$target = Join-Path $testRoot 'Network-Stats.exe'
$helperExecutable = Join-Path $sessionDirectory 'helper/Network-Stats.exe'
$report = Join-Path $testRoot 'verification.txt'
$requestPath = Join-Path $sessionDirectory 'install.json'
$results = [Collections.Generic.List[string]]::new()
$oldProcess = $null
$helperProcess = $null
$passed = $false
try {
    New-Item -ItemType Directory -Path (Join-Path $sessionDirectory 'helper'), (Join-Path $sessionDirectory 'payload') -Force | Out-Null
    Copy-Item -LiteralPath $fixture -Destination $target
    Copy-Item -LiteralPath $source -Destination $helperExecutable
    Copy-Item -LiteralPath $source -Destination (Join-Path $sessionDirectory 'payload/Network-Stats.exe')
    $packageHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    $fixtureHash = (Get-FileHash -LiteralPath $fixture -Algorithm SHA256).Hash
    @{
        theme = 'Dark'; intervalSeconds = 60; timeoutSeconds = 1; slowThresholdMs = 250
        maxConcurrency = 12; retentionHours = 168; proxies = @()
        sites = @('Baidu', 'Google', 'GitHub', 'Pixiv') | ForEach-Object { @{name=$_; url=('http://127.0.0.1:1/' + $_)} }
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $dataDirectory 'settings.json') -Encoding utf8
    $environment = @{}
    foreach ($entry in [Environment]::GetEnvironmentVariables().GetEnumerator()) { $environment[$entry.Key] = $entry.Value }
    $environment['DOTNET_ROOT'] = $sdkDirectory
    $environment['DOTNET_ROOT_X64'] = $environment['DOTNET_ROOT']
    $environment['DOTNET_BUNDLE_EXTRACT_BASE_DIR'] = Join-Path $testRoot 'bundle'
    $environment['NETWORKSTATS_DIAGNOSTICS_DIRECTORY'] = Join-Path $testRoot 'logs'
    $environment['NETWORKSTATS_REQUIRE_UPDATER'] = '1'
    $environment.Remove('NETWORKSTATS_SCREENSHOT_DIRECTORY')
    $block = (($environment.Keys | Sort-Object | ForEach-Object { $_ + '=' + $environment[$_] }) -join "`0") + "`0`0"
    $oldProcess = [WindowsDesktopProcess]::new($target, ('--hold "' + $sessionDirectory + '"'), $testRoot, $block)
    $oldNative = Get-Process -Id $oldProcess.Id
    @{
        ProcessId = $oldProcess.Id; ProcessStartedUtcTicks = $oldNative.StartTime.ToUniversalTime().Ticks
        TargetPath = $target; Sha256 = $packageHash; Version = (Get-Item -LiteralPath $source).VersionInfo.FileVersion
        RestartArguments = @('--verify-package', $report)
    } | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $requestPath -Encoding utf8
    $helperProcess = [WindowsDesktopProcess]::new($helperExecutable, ('--apply-update "' + $requestPath + '"'), $sessionDirectory, $block)
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while (-not (Test-Path -LiteralPath (Join-Path $sessionDirectory 'ready'))) {
        if ($helperProcess.WaitForExit(100) -or [DateTime]::UtcNow -gt $deadline) { throw 'Real package helper did not become ready.' }
    }
    [IO.File]::WriteAllText((Join-Path $sessionDirectory ('exit-' + $oldProcess.Id)), '')
    if (-not $oldProcess.WaitForExit(10000)) { throw 'Old fixture did not exit.' }
    if (-not $helperProcess.WaitForExit(55000) -or $helperProcess.ExitCode -ne 0) { throw 'Real package replacement failed.' }
    if (-not (Test-Path -LiteralPath (Join-Path $sessionDirectory 'complete'))) { throw 'New GUI did not acknowledge startup.' }
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $packageHash) { throw 'Updated executable hash mismatch.' }
    if ((Get-FileHash -LiteralPath (Join-Path $sessionDirectory 'previous.exe') -Algorithm SHA256).Hash -ne $fixtureHash) { throw 'Old executable backup mismatch.' }
    $deadline = [DateTime]::UtcNow.AddSeconds(45)
    while (-not (Test-Path -LiteralPath $report)) {
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Updated GUI verification timed out.' }
        Start-Sleep -Milliseconds 100
    }
    foreach ($updatedProcess in @(Get-Process -Name 'Network-Stats' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $target })) {
        if (-not $updatedProcess.WaitForExit(10000)) { throw 'Updated GUI did not exit after verification.' }
    }
    $guiReport = Get-Content -LiteralPath $report
    $results.AddRange([string[]]$guiReport)
    if ($guiReport[0] -ne 'PASS') { throw 'Updated GUI verification failed.' }
    $results.Add('Real single-file helper: atomic replacement, GUI restart acknowledgement, backup and isolated data verified')
    $passed = $true
} catch {
    $results.Add($_.Exception.ToString())
    $failure = Join-Path $sessionDirectory 'failed'
    if (Test-Path -LiteralPath $failure) { $results.Add((Get-Content -LiteralPath $failure -Raw)) }
    throw
} finally {
    foreach ($testProcess in @(Get-Process -Name 'Network-Stats' -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $target })) {
        if (-not $testProcess.WaitForExit(5000)) { $testProcess.Kill(); $testProcess.WaitForExit(5000) | Out-Null }
    }
    if ($helperProcess) { $helperProcess.Dispose() }
    if ($oldProcess) { $oldProcess.Dispose() }
    $results.Insert(0, $(if ($passed) { 'PASS' } else { 'FAIL' }))
    $results | Set-Content -LiteralPath (Join-Path $workspace 'artifacts/windows-update-check.txt') -Encoding utf8
    $results | Write-Output
    if (-not $testRoot.StartsWith($temporaryRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase) -or
        -not [IO.Path]::GetFileName($testRoot).StartsWith('NetworkStats update package ', [StringComparison]::Ordinal)) {
        throw 'Unexpected update test cleanup directory.'
    }
    if (Test-Path -LiteralPath $testRoot) {
        if ((Get-Item -LiteralPath $testRoot -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing linked test directory cleanup.' }
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
