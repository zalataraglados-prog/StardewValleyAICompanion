param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $Root = "artifacts\daily-plan-live-loop",
    [string] $BackendUrl = "http://localhost:5108",
    [string] $BridgeSnapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot",
    [string] $ExecutorUrl = "http://127.0.0.1:8767",
    [string] $RunId = $env:STARDEWAI_TRAINING_RUN_ID,
    [string] $SaveIsolationPath = $env:STARDEWAI_SAVE_ISOLATION_PATH,
    [string] $CandidateOptions = "economy.buy_supplies,executor.interact,exploration.visit_location,recovery.stabilize_day",
    [int] $MaxCandidates = 4
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RunId)) {
    throw "RunId is required. Pass -RunId or set STARDEWAI_TRAINING_RUN_ID."
}

if ([string]::IsNullOrWhiteSpace($SaveIsolationPath)) {
    throw "SaveIsolationPath is required. Pass -SaveIsolationPath or set STARDEWAI_SAVE_ISOLATION_PATH."
}

dotnet run --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
    --root $Root `
    --backend-url $BackendUrl `
    --bridge-snapshot-url $BridgeSnapshotUrl `
    --executor-url $ExecutorUrl `
    --no-manifest `
    --run-id $RunId `
    --save-isolation-path $SaveIsolationPath `
    --iterations 1 `
    --train-every 1 `
    --sleep-ms 0 `
    --use-daily-plan `
    --daily-plan-max-candidates $MaxCandidates `
    --daily-plan-candidate-options $CandidateOptions
