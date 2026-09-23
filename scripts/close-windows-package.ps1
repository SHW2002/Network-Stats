param([Parameter(Mandatory = $true)][string]$ExecutablePath)
$ErrorActionPreference = 'Stop'
$target = [System.IO.Path]::GetFullPath($ExecutablePath)
$running = @(Get-Process -Name 'Network-Stats' -ErrorAction SilentlyContinue | Where-Object {
    $_.Path -and [string]::Equals($_.Path, $target, [StringComparison]::OrdinalIgnoreCase)
})
if ($running.Count -eq 0) { return }
if (-not ('CloseNetworkStatsWindow' -as [type])) {
    Add-Type -Path (Join-Path $PSScriptRoot 'CloseNetworkStatsWindow.cs')
}
foreach ($appProcess in $running) {
    if ($appProcess.HasExited) { continue }
    $sent = $appProcess.CloseMainWindow()
    if (-not $sent) { $sent = [CloseNetworkStatsWindow]::RequestClose($appProcess.Id) }
    if (-not $sent -or -not $appProcess.WaitForExit(10000)) {
        throw "Cannot close Network Stats normally (PID $($appProcess.Id)); exit it from the tray and publish again."
    }
    Write-Output "Closed previous package normally (PID $($appProcess.Id))."
}
