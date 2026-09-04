param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $Root = "artifacts\daily-plan-live-loop",
    [string] $BackendUrl = "http://localhost:5108",
    [string] $BridgeSnapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot",
    [string] $ExecutorUrl = "http://127.0.0.1:8768",
    [string] $ManifestPath,
    [string] $PolicyCheckpointPath,
    [string] $RunId = $env:STARDEWAI_TRAINING_RUN_ID,
    [string] $SaveIsolationPath = $env:STARDEWAI_SAVE_ISOLATION_PATH,
    [string] $SaveSlot = $env:STARDEWAI_SAVE_SLOT,
    [string] $CandidateOptions = "",
    [int] $MaxCandidates = 4,
    [int] $MaxAttempts = 1000000,
    [int] $RequiredVerifiedActions = 0,
    [ValidateRange(1, 128)]
    [int] $SaveBoundaryMaxAttempts = 16
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($RunId)) {
    throw "RunId is required. Pass -RunId or set STARDEWAI_TRAINING_RUN_ID."
}

if ([string]::IsNullOrWhiteSpace($SaveIsolationPath)) {
    throw "SaveIsolationPath is required. Pass -SaveIsolationPath or set STARDEWAI_SAVE_ISOLATION_PATH."
}

if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    throw "SaveSlot is required. Pass -SaveSlot or set STARDEWAI_SAVE_SLOT."
}

if ([string]::IsNullOrWhiteSpace($ManifestPath) -or -not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
    throw "ManifestPath must reference the prepared formal training manifest."
}

if ([string]::IsNullOrWhiteSpace($PolicyCheckpointPath) -or -not (Test-Path -LiteralPath $PolicyCheckpointPath -PathType Leaf)) {
    throw "PolicyCheckpointPath must reference the frozen structured policy checkpoint."
}

$arguments = @(
    "run", "--project", (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj"), "--",
    "--root", $Root,
    "--backend-url", $BackendUrl,
    "--bridge-snapshot-url", $BridgeSnapshotUrl,
    "--executor-url", $ExecutorUrl,
    "--manifest-path", $ManifestPath,
    "--run-id", $RunId,
    "--save-isolation-path", $SaveIsolationPath,
    "--save-slot", $SaveSlot,
    "--max-attempts", $MaxAttempts,
    "--required-verified-actions", $RequiredVerifiedActions,
    "--require-native-save-boundary",
    "--save-boundary-max-attempts", $SaveBoundaryMaxAttempts,
    "--train-every", 1,
    "--use-product-executor",
    "--use-daily-plan",
    "--daily-plan-max-candidates", $MaxCandidates,
    "--policy-checkpoint-path", $PolicyCheckpointPath,
    "--require-structured-policy"
)
if (-not [string]::IsNullOrWhiteSpace($CandidateOptions)) {
    $arguments += @("--daily-plan-candidate-options", $CandidateOptions)
}

& dotnet $arguments
if ($LASTEXITCODE -ne 0) {
    throw "LiveTrainingLoop exited with code $LASTEXITCODE."
}
