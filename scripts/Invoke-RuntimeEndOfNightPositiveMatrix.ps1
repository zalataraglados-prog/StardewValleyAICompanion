param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [ValidateRange(0, 29)]
    [int] $Level5ProfessionChoiceId = 0,
    [ValidateRange(0, 29)]
    [int] $Level10ProfessionChoiceId = 2,
    [int] $BackendPort = 8798,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $NoBuild,
    [switch] $VisibleGame,
    [switch] $OwnerVisualGate
)

$ErrorActionPreference = "Stop"

if ($OwnerVisualGate -and -not $VisibleGame) {
    throw "Owner visual matrix requires a visible game window."
}
if (($Level5ProfessionChoiceId % 6) -ge 2) {
    throw "Level5ProfessionChoiceId must identify one vanilla level-5 profession."
}
if (($Level10ProfessionChoiceId % 6) -lt 2) {
    throw "Level10ProfessionChoiceId must identify one vanilla level-10 profession."
}
$level5Skill = [math]::Floor($Level5ProfessionChoiceId / 6)
$level10Skill = [math]::Floor($Level10ProfessionChoiceId / 6)
$level10RequiredBranch = if (($Level10ProfessionChoiceId % 6) -lt 4) {
    0
}
else {
    1
}
if ($level5Skill -ne $level10Skill -or
    ($Level5ProfessionChoiceId % 6) -ne $level10RequiredBranch) {
    throw "Level-5 and level-10 profession IDs must form one legal vanilla branch."
}
if ($OwnerVisualGate) {
    $ownerWorktreeStatus = @(& git -C $ProjectRoot status `
        --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0) {
        throw "Owner visual matrix could not inspect the candidate worktree."
    }
    if ($ownerWorktreeStatus.Count -gt 0) {
        throw "Owner visual matrix requires one committed clean-worktree candidate."
    }
}

$matrixRunId = "runtime-end-of-night-positive-matrix-" +
    (Get-Date -Format "yyyyMMdd-HHmmss")
$matrixDirectory = Join-Path $ProjectRoot (
    "artifacts\runtime-end-of-night-matrix\" + $matrixRunId)
New-Item -ItemType Directory -Force -Path $matrixDirectory | Out-Null

$scenarios = @(
    [ordered]@{
        id = "positive-shipping"
        no_shipment = $false
        ordinary_levels = 0
        profession_choice_id = -1
        farm_event = ""
    },
    [ordered]@{
        id = "empty-bin-save"
        no_shipment = $true
        ordinary_levels = 0
        profession_choice_id = -1
        farm_event = ""
    },
    [ordered]@{
        id = "ordinary-levels-save"
        no_shipment = $true
        ordinary_levels = 2
        profession_choice_id = -1
        farm_event = ""
    },
    [ordered]@{
        id = "profession-and-remaining-save-menus"
        no_shipment = $true
        ordinary_levels = 2
        profession_choice_id = $Level5ProfessionChoiceId
        farm_event = ""
    },
    [ordered]@{
        id = "shipping-and-ordinary-levels"
        no_shipment = $false
        ordinary_levels = 2
        profession_choice_id = -1
        farm_event = ""
    },
    [ordered]@{
        id = "shipping-and-profession"
        no_shipment = $false
        ordinary_levels = 0
        profession_choice_id = $Level10ProfessionChoiceId
        farm_event = ""
    },
    [ordered]@{
        id = "day-31-full-composite"
        no_shipment = $false
        ordinary_levels = 2
        profession_choice_id = $Level10ProfessionChoiceId
        farm_event = "day_31_earthquake"
    }
)

$smokeScript = Join-Path $PSScriptRoot `
    "Invoke-RuntimeFullShipmentTerminalSmoke.ps1"
$results = [System.Collections.Generic.List[object]]::new()
$firstScenario = $true
$candidateHeadCommit = ""
$candidateWorktreeStatusSha256 = ""
$candidateSmokeScriptSha256 = ""
$candidateBinaryIdentity = ""
$candidateRuntimeBundleIdentity = ""
foreach ($scenario in $scenarios) {
    $runId = $matrixRunId + "-" + $scenario.id
    $arguments = @{
        ProjectRoot = $ProjectRoot
        RuntimeRoot = $RuntimeRoot
        SaveSlot = $SaveSlot
        RunId = $runId
        PendingProfessionChoiceId = [int]$scenario.profession_choice_id
        PendingOrdinaryLevelCount = [int]$scenario.ordinary_levels
        PendingFarmEvent = [string]$scenario.farm_event
        BackendPort = $BackendPort
        StartupTimeoutSeconds = $StartupTimeoutSeconds
    }
    if ([bool]$scenario.no_shipment) {
        $arguments.NoShipment = $true
    }
    if ($VisibleGame) {
        $arguments.VisibleGame = $true
    }
    if ($OwnerVisualGate -and
        [string]$scenario.id -eq "day-31-full-composite") {
        $arguments.OwnerComposite = $true
    }
    if ($NoBuild -or -not $firstScenario) {
        $arguments.NoBuild = $true
    }

    $stdoutPath = Join-Path $matrixDirectory ($scenario.id + ".stdout.log")
    & $smokeScript @arguments 2>&1 |
        Set-Content -LiteralPath $stdoutPath -Encoding utf8
    if ($LASTEXITCODE -ne 0) {
        throw "End-of-night matrix scenario failed: $($scenario.id)"
    }

    $scenarioSummaryPath = Join-Path $ProjectRoot (
        "artifacts\runtime-full-shipment-terminal\" +
        $runId + "\summary.json")
    if (-not (Test-Path -LiteralPath $scenarioSummaryPath -PathType Leaf)) {
        throw "End-of-night matrix scenario did not produce a summary: " +
            $scenario.id
    }
    $summary = Get-Content -LiteralPath $scenarioSummaryPath -Raw |
        ConvertFrom-Json
    if ([string]$summary.status -ne "passed" -or
        [string]::IsNullOrWhiteSpace([string]$summary.transaction_id) -or
        [string]::IsNullOrWhiteSpace(
            [string]$summary.transaction_evidence_sha256) -or
        [string]$summary.transaction_status -ne "complete") {
        throw "End-of-night matrix scenario did not produce one complete transaction: " +
            $scenario.id
    }
    $summaryBinaryIdentity = $summary.candidate_binary_sha256 |
        ConvertTo-Json -Depth 16 -Compress
    $summaryRuntimeBundleIdentity =
        $summary.candidate_runtime_bundle_sha256 |
            ConvertTo-Json -Depth 16 -Compress
    if ($firstScenario) {
        $candidateHeadCommit = [string]$summary.project_head_commit
        $candidateWorktreeStatusSha256 =
            [string]$summary.project_worktree_status_sha256
        $candidateSmokeScriptSha256 =
            [string]$summary.candidate_script_sha256
        $candidateBinaryIdentity = $summaryBinaryIdentity
        $candidateRuntimeBundleIdentity = $summaryRuntimeBundleIdentity
    }
    elseif ([string]$summary.project_head_commit -ne $candidateHeadCommit -or
        [string]$summary.project_worktree_status_sha256 -ne
            $candidateWorktreeStatusSha256 -or
        [string]$summary.candidate_script_sha256 -ne
            $candidateSmokeScriptSha256 -or
        $summaryBinaryIdentity -ne $candidateBinaryIdentity -or
        $summaryRuntimeBundleIdentity -ne $candidateRuntimeBundleIdentity) {
        throw "End-of-night matrix candidate identity changed between scenarios: " +
            $scenario.id
    }
    if ($OwnerVisualGate -and [bool]$summary.project_worktree_dirty) {
        throw "Owner visual matrix requires one committed clean-worktree candidate."
    }
    $results.Add([ordered]@{
        scenario_id = [string]$scenario.id
        run_id = $runId
        summary_path = $scenarioSummaryPath
        summary_schema_version = [string]$summary.schema_version
        summary_sha256 = ((Get-FileHash -LiteralPath $scenarioSummaryPath `
            -Algorithm SHA256).Hash).ToLowerInvariant()
        status = [string]$summary.status
        transaction_id = [string]$summary.transaction_id
        transaction_status = [string]$summary.transaction_status
        transaction_evidence_sha256 =
            [string]$summary.transaction_evidence_sha256
        no_shipment = [bool]$scenario.no_shipment
        ordinary_level_count = [int]$scenario.ordinary_levels
        profession_choice_id = [int]$scenario.profession_choice_id
        farm_event = [string]$scenario.farm_event
    })
    $firstScenario = $false
}

$matrixSummary = [ordered]@{
    schema_version = "stardewai.runtime_end_of_night_positive_matrix.v1"
    status = "passed"
    run_id = $matrixRunId
    project_head_commit = $candidateHeadCommit
    project_worktree_status_sha256 = $candidateWorktreeStatusSha256
    matrix_script_sha256 = ((Get-FileHash -LiteralPath $PSCommandPath `
        -Algorithm SHA256).Hash).ToLowerInvariant()
    smoke_script_sha256 = $candidateSmokeScriptSha256
    candidate_binary_sha256 =
        $candidateBinaryIdentity | ConvertFrom-Json
    candidate_runtime_bundle_sha256 =
        $candidateRuntimeBundleIdentity | ConvertFrom-Json
    scenario_count = $scenarios.Count
    passed_scenario_count = $results.Count
    owner_visual_gate_required = [bool]$OwnerVisualGate
    owner_visual_gate_passed = $false
    level_5_profession_choice_id = $Level5ProfessionChoiceId
    level_10_profession_choice_id = $Level10ProfessionChoiceId
    training_label_eligible = $false
    scenarios = $results.ToArray()
}
$summaryPath = Join-Path $matrixDirectory "summary.json"
$matrixSummary | ConvertTo-Json -Depth 32 |
    Set-Content -LiteralPath $summaryPath -Encoding utf8
$matrixSummary | ConvertTo-Json -Depth 32
