param(
    [Parameter(Mandatory = $true)][string]$ExecutablePath,
    [string]$ReportPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\windows-package-check.txt'),
    [ValidateRange(1, 5)][int]$WarmRuns = 2
)
$ErrorActionPreference = 'Stop'
if (-not ('WindowsDesktopProcess' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'WindowsDesktopProcess.cs')
}
$executable = (Resolve-Path -LiteralPath $ExecutablePath).Path
$temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$testDirectory = [System.IO.Path]::GetFullPath((Join-Path $temporaryRoot ('NetworkStats package 测试-' + [Guid]::NewGuid().ToString('N'))))
$reportFile = [System.IO.Path]::GetFullPath($ReportPath)
$results = [System.Collections.Generic.List[string]]::new()
$passed = $false

function Assert-TestDirectory {
    if (-not $testDirectory.StartsWith($temporaryRoot.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Package test directory is outside the temporary directory.'
    }
    if ((Test-Path -LiteralPath $testDirectory) -and
        ((Get-Item -LiteralPath $testDirectory -Force).Attributes -band [System.IO.FileAttributes]::ReparsePoint)) {
        throw 'Refusing to remove a linked test directory.'
    }
}

Assert-TestDirectory
New-Item -ItemType Directory -Path $testDirectory | Out-Null
try {
    # Preserve the compiler's filename: renaming WinUI executables can break resource resolution.
    $testExecutable = Join-Path $testDirectory ([System.IO.Path]::GetFileName($executable))
    Copy-Item -LiteralPath $executable -Destination $testExecutable
    $results.Add('SHA256: ' + (Get-FileHash -LiteralPath $testExecutable -Algorithm SHA256).Hash)
    $dataDirectory = Join-Path $testDirectory 'data'
    New-Item -ItemType Directory -Path $dataDirectory | Out-Null
    # Keep the real monitor and four chart rows, with loopback-only targets and isolated storage.
    $settings = @{
        intervalSeconds = 60; timeoutSeconds = 1; slowThresholdMs = 250
        maxConcurrency = 12; retentionHours = 168; proxies = @()
        sites = @('Baidu', 'Google', 'GitHub', 'Pixiv') | ForEach-Object {
            @{ name = $_; url = ('http://127.0.0.1:1/' + $_) }
        }
    }
    $settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $dataDirectory 'settings.json') -Encoding utf8
    $environment = @{}
    foreach ($entry in [Environment]::GetEnvironmentVariables().GetEnumerator()) { $environment[$entry.Key] = $entry.Value }
    $environment.Remove('DOTNET_ROOT')
    $environment.Remove('DOTNET_ROOT_X64')
    $environment.Remove('MICROSOFT_WINDOWSAPPRUNTIME_BASE_DIRECTORY')
    $environment['DOTNET_BUNDLE_EXTRACT_BASE_DIR'] = Join-Path $testDirectory 'bundle'
    $environment['NETWORKSTATS_DIAGNOSTICS_DIRECTORY'] = Join-Path $testDirectory 'logs'
    $environment['NETWORKSTATS_REQUIRE_UPDATER'] = '1'
    $block = (($environment.Keys | Sort-Object | ForEach-Object { $_ + '=' + $environment[$_] }) -join "`0") + "`0`0"
    for ($run = 0; $run -le $WarmRuns; $run++) {
        $settings['theme'] = @('Dark', 'Light', 'System')[$run % 3]
        $settings['minimizeOnClose'] = ($run % 2 -eq 0)
        $settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $dataDirectory 'settings.json') -Encoding utf8
        $label = if ($run -eq 0) { 'Cold (empty extraction cache)' } else { "Warm $run (reuse extraction cache)" }
        $results.Add($label)
        $testReport = Join-Path $testDirectory "verification-$run.txt"
        $process = [WindowsDesktopProcess]::new($testExecutable, ('--verify-package "' + $testReport + '"'), $testDirectory, $block)
        try {
            if (-not $process.WaitForExit(45000)) { throw 'Full window verification timed out on the isolated desktop.' }
            if (-not (Test-Path -LiteralPath $testReport)) {
                throw "Window verification exited without a report (exit code $($process.ExitCode))."
            }
            $report = Get-Content -LiteralPath $testReport
            $results.AddRange([string[]]$report)
            $report | Write-Output
            $exitStatus = 'Process exit code: {0} (0x{0:X8})' -f $process.ExitCode
            $results.Add($exitStatus)
            Write-Output $exitStatus
            $shutdownLog = Get-Content -LiteralPath (Join-Path $testDirectory "logs/startup-$($process.Id).log") -Raw
            if ($shutdownLog -notmatch '(?s)Monitoring stopped and history flushed before window close.*WinUI message loop returned.*Process exiting; code=0') {
                throw "Orderly shutdown was not completed. $exitStatus"
            }
            if ($process.ExitCode -ne 0 -or $report[0] -ne 'PASS' -or
                $report -notcontains 'MAUI: window loaded, native templates applied, timeline drawing completed' -or
                $report -notcontains 'Updates: proxy persistence, version check, verified download and install handoff' -or
                $report -notcontains 'URL speed: configured URL automatically measured and speed rendered on main timeline' -or
                $report -notcontains 'URL speed: single-site page measured the same configured path and query' -or
                $report -notcontains 'Download metrics: body throughput, separate timings, small-sample hints and legacy history verified' -or
                $report -notcontains 'Timeline: initial loading and previous results retained until new probes finish' -or
                $report -notcontains 'Startup: settings toggle and persistence verified without changing real login items' -or
                $report -notcontains 'Preferences: light/dark/system themes, saved settings and close-to-minimize verified') {
                throw "Full window verification failed. $exitStatus"
            }
        } finally { $process.Dispose() }
    }
    $passed = $true
} catch {
    $results.Add($_.Exception.ToString())
    throw
} finally {
    $results.Insert(0, $(if ($passed) { 'PASS' } else { 'FAIL' }))
    $logs = Join-Path $testDirectory 'logs'
    if (Test-Path -LiteralPath $logs) {
        foreach ($log in Get-ChildItem -LiteralPath $logs -Filter 'startup-*.log') {
            $results.Add($log.Name)
            $results.AddRange([string[]](Get-Content -LiteralPath $log.FullName))
        }
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $reportFile) -Force | Out-Null
    $results | Set-Content -LiteralPath $reportFile -Encoding utf8
    Assert-TestDirectory
    if (Test-Path -LiteralPath $testDirectory) { Remove-Item -LiteralPath $testDirectory -Recurse -Force }
}
