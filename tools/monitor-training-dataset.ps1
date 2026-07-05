param(
    [string]$DatasetPath = "E:\StardewAITraining\datasets\training-feature-rows.jsonl",
    [string]$LogPath = "E:\StardewAITraining\logs\training-data-monitor.log",
    [int]$IntervalSeconds = 30
)

$ErrorActionPreference = "Stop"
$resolvedDataset = [System.IO.Path]::GetFullPath($DatasetPath)
$resolvedLog = [System.IO.Path]::GetFullPath($LogPath)
$logDir = Split-Path -Parent $resolvedLog
if (-not [string]::IsNullOrWhiteSpace($logDir)) {
    New-Item -ItemType Directory -Force -Path $logDir | Out-Null
}

"$(Get-Date -Format o) monitor_start dataset=$resolvedDataset sound=disabled game_launch=disabled" | Add-Content -LiteralPath $resolvedLog

while ($true) {
    $exists = Test-Path -LiteralPath $resolvedDataset
    $rows = 0
    $bytes = 0
    if ($exists) {
        $item = Get-Item -LiteralPath $resolvedDataset
        $bytes = $item.Length
        $rows = (Get-Content -LiteralPath $resolvedDataset | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }).Count
    }

    "$(Get-Date -Format o) dataset_exists=$exists rows=$rows bytes=$bytes" | Add-Content -LiteralPath $resolvedLog
    Start-Sleep -Seconds $IntervalSeconds
}
