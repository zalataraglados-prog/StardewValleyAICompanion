[CmdletBinding()]
param(
    [string] $ProjectRoot = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
} else {
    $ProjectRoot = [IO.Path]::GetFullPath($ProjectRoot)
}
$entry = Join-Path $PSScriptRoot "Get-ValidationImpactAdvisory.ps1"

function Get-Advisory {
    param([Parameter(Mandatory)] [string[]] $Files)

    $json = @(& $entry -ProjectRoot $ProjectRoot -ChangedFile $Files) `
        -join [Environment]::NewLine
    return $json | ConvertFrom-Json
}

function Assert-True {
    param(
        [Parameter(Mandatory)] [bool] $Condition,
        [Parameter(Mandatory)] [string] $Message
    )
    if (-not $Condition) {
        throw $Message
    }
}

function Assert-Contains {
    param(
        [AllowEmptyCollection()] [object[]] $Values = @(),
        [Parameter(Mandatory)] [string] $Expected,
        [Parameter(Mandatory)] [string] $Message
    )
    Assert-True -Condition ($Values -contains $Expected) -Message $Message
}

$scenarioCount = 0
$docs = Get-Advisory -Files @("docs/example.md")
Assert-True (-not $docs.uncertain_impact) "Docs-only impact must be certain."
Assert-True $docs.full_quality_still_required "Hosted quality must remain required."
Assert-True (-not $docs.evidence_reuse_authorized) "Advisory cannot authorize evidence reuse."
Assert-True ($docs.recommended_builds.Count -eq 0) "Docs-only must not recommend a build."
Assert-Contains $docs.impacted_domains "documentation" "Docs-only domain is missing."
Assert-Contains $docs.recommended_source_guards "git diff --check" "Docs guard is missing."
$scenarioCount++

$unknown = Get-Advisory -Files @("unexpected/new-surface.bin")
Assert-True $unknown.uncertain_impact "Unknown paths must remain uncertain."
Assert-True $unknown.full_quality_still_required "Unknown paths must retain hosted quality."
Assert-True (-not $unknown.native_replay_execution_authorized) "Advisory cannot execute native replay."
Assert-Contains $unknown.impacted_domains "broad_quality" "Unknown path must use broad fallback."
Assert-Contains $unknown.native_replay_candidates `
    "manual_impact_review_before_native_replay" `
    "Unknown path must request manual native impact review."
$scenarioCount++

$invalidPathRejected = $false
try {
    Get-Advisory -Files @("docs/../../outside.txt") | Out-Null
} catch {
    $invalidPathRejected = $true
}
Assert-True $invalidPathRejected "Repository-escaping changed paths must be rejected."
$scenarioCount++

$listPath = Join-Path $env:TEMP `
    ("stardewai-validation-impact-" + [Guid]::NewGuid().ToString("N") + ".txt")
try {
    [IO.File]::WriteAllText($listPath, "docs/example.md")
    $mixedSourcesRejected = $false
    try {
        & $entry -ProjectRoot $ProjectRoot -BaseRef HEAD `
            -ChangedFilesPath $listPath | Out-Null
    } catch {
        $mixedSourcesRejected = $true
    }
    Assert-True $mixedSourcesRejected `
        "Git ranges and explicit changed-file lists must not be combined."
} finally {
    [IO.File]::Delete($listPath)
}
$scenarioCount++

$representative = @(
    @{
        Files = @(
            "experiments/StardewAI.GoalConditionedBootstrap/AcquisitionCurrentRouteCandidateIndex.cs",
            "src/StardewAI.Core/Execution/MiningFloorStepPlanner.CombatDrops.cs",
            "scripts/Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1")
        Domain = "bootstrap"
        Native = "representative_mining_native_replay"
    },
    @{
        Files = @(
            "src/StardewAI.TransparentBridge/Adapters/MiningMonsterDropResolver.Probability.cs",
            "src/StardewAI.TransparentBridge/Adapters/MiningReadAdapter.MonsterRouteSources.cs")
        Domain = "transparent_bridge"
        Native = "representative_mining_native_replay"
    },
    @{
        Files = @(
            "src/StardewAI.Core/Execution/ActionQueueCompiler.Validation.DeferredPickup.Monster.cs",
            "tools/StardewAI.RuntimeTestHarness/ModEntry.MachinesAndPickup.cs")
        Domain = "runtime_test_harness"
        Native = "representative_mining_native_replay"
    },
    @{
        Files = @(
            "tools/StardewAI.RuntimeTestHarness/ModEntry.LocationFishingFixture.cs",
            "tests/StardewAI.Core.Tests/FullShipmentAcquisitionSampleSourceGuardTests.cs")
        Domain = "runtime_test_harness"
        Native = "representative_fishing_native_replay"
    },
    @{
        Files = @(
            "src/StardewAI.Core/Infrastructure/FishingTerminalProbabilitySnapshotProjector.cs",
            "src/StardewAI.TransparentBridge/Adapters/FishingReadAdapter.TilesRules.cs")
        Domain = "core"
        Native = "representative_fishing_native_replay"
    })

foreach ($scenario in $representative) {
    $report = Get-Advisory -Files $scenario.Files
    Assert-True ($report.status -eq "advisory_ready") "Representative advisory is not ready."
    Assert-Contains $report.impacted_domains $scenario.Domain "Expected impacted domain is missing."
    Assert-Contains $report.native_replay_candidates $scenario.Native "Expected native replay is missing."
    Assert-True $report.full_quality_still_required "Hosted quality must remain required."
    Assert-True (-not $report.evidence_reuse_authorized) "Advisory cannot authorize evidence reuse."
    $scenarioCount++
}

[ordered]@{
    status = "passed"
    scenario_count = $scenarioCount
} | ConvertTo-Json -Compress
