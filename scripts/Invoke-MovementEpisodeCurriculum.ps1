param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $Root = "artifacts\runtime-movement-curriculum",
    [string] $BackendUrl = "http://localhost:5108",
    [string] $BridgeSnapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot",
    [string] $ExecutorUrl = "http://127.0.0.1:8767",
    [string] $RunId = $env:STARDEWAI_TRAINING_RUN_ID,
    [string] $SaveIsolationPath = $env:STARDEWAI_SAVE_ISOLATION_PATH,
    [int] $MaxSteps = 8,
    [Parameter(Mandatory = $true)] [string[]] $Targets
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RunId)) {
    throw "RunId is required. Pass -RunId or set STARDEWAI_TRAINING_RUN_ID."
}

if ([string]::IsNullOrWhiteSpace($SaveIsolationPath)) {
    throw "SaveIsolationPath is required. Pass -SaveIsolationPath or set STARDEWAI_SAVE_ISOLATION_PATH."
}

foreach ($target in $Targets) {
    if ($target -notmatch "^(-?\d+),(-?\d+)$") {
        throw "Target '$target' must be formatted as 'x,y'."
    }

    $targetX = [int]$Matches[1]
    $targetY = [int]$Matches[2]
    $targetRoot = Join-Path $Root ("target-{0}-{1}" -f $targetX, $targetY)

    dotnet run --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
        --root $targetRoot `
        --backend-url $BackendUrl `
        --bridge-snapshot-url $BridgeSnapshotUrl `
        --executor-url $ExecutorUrl `
        --no-manifest `
        --run-id $RunId `
        --save-isolation-path $SaveIsolationPath `
        --iterations 1 `
        --train-every 1 `
        --sleep-ms 0 `
        --use-plan-output `
        --target-tile-x $targetX `
        --target-tile-y $targetY `
        --max-crops $MaxSteps
}
