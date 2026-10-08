[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $RuntimeRoot = "F:\StardewAI-TestLab\runtime",
    [string] $ArchivedFreshSaveRoot =
        "F:\StardewAI-TestLab\inputs\fresh-save\ProofFarm_450250338",
    [string] $RequirementInventory =
        "F:\StardewAI-TestLab\inputs\authoritative-requirement-inventory-v1.json",
    [string] $AcquisitionLowering =
        "F:\StardewAI-TestLab\inputs\acquisition-route-option-lowering-v1.json",
    [string] $MasterAnglerWindows =
        "F:\StardewAI-TestLab\inputs\master-angler-stage-one-window-index-v1.json",
    [string] $RouteTimingCalibration =
        "F:\StardewAI-TestLab\route-timing-calibration.json",
    [string] $StaticInventory =
        "local-data\full-shipment-evidence-milestone\static-inventory.json",
    [string] $Plan =
        "catalogs\vanilla-1.6.15\full-shipment-runtime-sample-plan.json",
    [string] $OutputRoot =
        "artifacts\runtime-full-shipment-evidence-milestone",
    [ValidateSet("high_risk", "standard", "all_missing", "all")]
    [string] $Batch = "high_risk",
    [string[]] $ExistingSummaryPaths = @(),
    [int] $MaxScenarios = 0,
    [switch] $PlanOnly,
    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
}
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

function Resolve-MilestonePath {
    param([string] $Path)
    return Resolve-RuntimeEvidenceInputPath `
        -ProjectRoot $ProjectRoot -Path $Path
}

function Read-JsonArtifact {
    param([string] $Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Milestone artifact is missing: $Path"
    }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Get-SummaryRecord {
    param(
        [Parameter(Mandatory)] [string] $SummaryPath,
        [Parameter(Mandatory)] $PlanEntry,
        [Parameter(Mandatory)] $ReconciliationRow
    )
    $summary = Read-JsonArtifact $SummaryPath
    if ([string]$summary.status -ne "passed") {
        throw "Scenario $($PlanEntry.scenario) summary did not pass."
    }
    $schema = [string]$summary.schema_version
    if ($schema -eq "stardewai.runtime_full_shipment_acquisition_sample.v1") {
        if ([string]$summary.scenario -ne [string]$PlanEntry.scenario -or
            [string]$summary.requirement_id -ne
                [string]$PlanEntry.requirement_id -or
            [string]$summary.qualified_item_id -ne
                [string]$PlanEntry.qualified_item_id) {
            throw "Scenario $($PlanEntry.scenario) summary identity drifted."
        }
        $proofManifestPath = [string]$summary.rollout_proof_manifest_path
        $proofReceiptPath = [string]$summary.rollout_proof_receipt_path
        $recurrenceManifestPath = ""
        $prefixCheckpointPath = ""
    }
    elseif ($schema -eq
            "stardewai.runtime_full_shipment_sap_prefix_smoke.v1" -and
        [string]$PlanEntry.scenario -eq "sap_prefix") {
        $recurrenceManifestPath =
            [string]$summary.recurrence_manifest_path
        $prefixCheckpointPath = [string]$summary.prefix_checkpoint_path
        $recurrence = Read-JsonArtifact $recurrenceManifestPath
        $iterations = @($recurrence.iterations | Where-Object {
            [string]$_.acquisition_rollout_proof_receipt_path -eq
                [string]$summary.acquisition_rollout_receipt_path
        })
        if ($iterations.Count -ne 1) {
            throw "Sap summary does not bind one recurrence acquisition proof."
        }
        $proofManifestPath =
            [string]$iterations[0].acquisition_rollout_proof_manifest_path
        $proofReceiptPath =
            [string]$iterations[0].acquisition_rollout_proof_receipt_path
    }
    else {
        throw "Scenario $($PlanEntry.scenario) has an unsupported summary schema."
    }

    $routeOccurrenceId =
        [string]$summary.acquisition_route_occurrence_id
    if ($routeOccurrenceId -notin
        @($ReconciliationRow.candidate_route_occurrence_ids |
            ForEach-Object { [string]$_ })) {
        throw "Scenario $($PlanEntry.scenario) selected a route outside its reconciled stratum."
    }
    foreach ($path in @(
        $proofManifestPath,
        $proofReceiptPath,
        $recurrenceManifestPath,
        $prefixCheckpointPath
    )) {
        if (-not [string]::IsNullOrWhiteSpace($path) -and
            -not (Test-Path -LiteralPath $path -PathType Leaf)) {
            throw "Scenario $($PlanEntry.scenario) references a missing proof: $path"
        }
    }

    return [ordered]@{
        scenario = [string]$PlanEntry.scenario
        run_batch = [string]$PlanEntry.run_batch
        stratum_id = [string]$ReconciliationRow.stratum_id
        route_kind = [string]$PlanEntry.route_kind
        route_occurrence_id = $routeOccurrenceId
        requirement_id = [string]$PlanEntry.requirement_id
        qualified_item_id = [string]$PlanEntry.qualified_item_id
        summary_path = [IO.Path]::GetFullPath($SummaryPath)
        rollout_proof_manifest_path =
            [IO.Path]::GetFullPath($proofManifestPath)
        rollout_proof_receipt_path =
            [IO.Path]::GetFullPath($proofReceiptPath)
        recurrence_manifest_path = if (
            [string]::IsNullOrWhiteSpace($recurrenceManifestPath)) {
            ""
        } else { [IO.Path]::GetFullPath($recurrenceManifestPath) }
        prefix_checkpoint_path = if (
            [string]::IsNullOrWhiteSpace($prefixCheckpointPath)) {
            ""
        } else { [IO.Path]::GetFullPath($prefixCheckpointPath) }
    }
}

function Write-MilestoneState {
    param(
        [string] $Status,
        [object[]] $Records,
        [string[]] $SelectedScenarios,
        [string] $LastError = ""
    )
    $completed = @($Records.scenario | ForEach-Object { [string]$_ })
    $state = [ordered]@{
        schema_version =
            "full_shipment_runtime_evidence_milestone_checkpoint.v1"
        status = $Status
        batch = $Batch
        static_inventory_sha256 =
            [string]$reconciliation.static_inventory_sha256
        sample_plan_sha256 = [string]$reconciliation.sample_plan_sha256
        requirement_inventory_sha256 = $requirementInventorySha256
        acquisition_lowering_sha256 = $acquisitionLoweringSha256
        master_angler_windows_sha256 = $masterAnglerWindowsSha256
        route_timing_calibration_sha256 = $routeTimingCalibrationSha256
        selected_scenario_count = $SelectedScenarios.Count
        completed_scenario_count = @($completed | Where-Object {
            $_ -in $SelectedScenarios
        }).Count
        remaining_scenarios = @($SelectedScenarios | Where-Object {
            $_ -notin $completed
        })
        last_error = $LastError
        formal_training_authorized = $false
        records = @($Records)
    }
    Write-JsonFile -Path $checkpointPath -Value $state

    $sap = @($Records | Where-Object scenario -eq "sap_prefix")
    if ($sap.Count -eq 1) {
        $manifest = [ordered]@{
            schema_version =
                "full_shipment_runtime_sample_evidence_manifest.v1"
            shared_shipping_recurrence_manifest_path =
                [string]$sap[0].recurrence_manifest_path
            shared_shipping_prefix_checkpoint_path =
                [string]$sap[0].prefix_checkpoint_path
            samples = @($Records | ForEach-Object {
                [ordered]@{
                    stratum_id = [string]$_.stratum_id
                    route_occurrence_id = [string]$_.route_occurrence_id
                    acquisition_rollout_proof_manifest_path =
                        [string]$_.rollout_proof_manifest_path
                    acquisition_rollout_proof_receipt_path =
                        [string]$_.rollout_proof_receipt_path
                }
            })
            formal_training_authorized = $false
        }
        Write-JsonFile -Path $evidenceManifestPath -Value $manifest
    }
}

function Build-CurrentEvidenceIndex {
    if (-not (Test-Path -LiteralPath $evidenceManifestPath -PathType Leaf)) {
        return $null
    }
    $bootstrapDll = Join-Path $ProjectRoot `
        "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"
    if (-not (Test-Path -LiteralPath $bootstrapDll -PathType Leaf)) {
        throw "Goal-conditioned bootstrap Release DLL is missing: $bootstrapDll"
    }
    $indexPath = Join-Path $milestoneRoot `
        "runtime-sample-evidence-index.json"
    & dotnet $bootstrapDll `
        "build-full-shipment-runtime-sample-evidence-index" `
        "--static-inventory" $staticInventoryPath `
        "--evidence-manifest" $evidenceManifestPath `
        "--output" $indexPath | Out-Null
    if ($LASTEXITCODE -notin @(0, 2)) {
        throw "Full Shipment runtime evidence index rebuild failed with exit $LASTEXITCODE."
    }
    return Read-JsonArtifact $indexPath
}

$staticInventoryPath = Resolve-MilestonePath $StaticInventory
$planPath = Resolve-MilestonePath $Plan
$requirementInventoryPath = Resolve-MilestonePath $RequirementInventory
$acquisitionLoweringPath = Resolve-MilestonePath $AcquisitionLowering
$masterAnglerWindowsPath = Resolve-MilestonePath $MasterAnglerWindows
$routeTimingCalibrationPath = Resolve-MilestonePath $RouteTimingCalibration
foreach ($inputPath in @(
    $requirementInventoryPath,
    $acquisitionLoweringPath,
    $masterAnglerWindowsPath,
    $routeTimingCalibrationPath
)) {
    if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) {
        throw "Milestone evidence input is missing: $inputPath"
    }
}
$requirementInventorySha256 = (Get-FileHash -LiteralPath `
    $requirementInventoryPath -Algorithm SHA256).Hash.ToLowerInvariant()
$acquisitionLoweringSha256 = (Get-FileHash -LiteralPath `
    $acquisitionLoweringPath -Algorithm SHA256).Hash.ToLowerInvariant()
$masterAnglerWindowsSha256 = (Get-FileHash -LiteralPath `
    $masterAnglerWindowsPath -Algorithm SHA256).Hash.ToLowerInvariant()
$routeTimingCalibrationSha256 = (Get-FileHash -LiteralPath `
    $routeTimingCalibrationPath -Algorithm SHA256).Hash.ToLowerInvariant()
$milestoneRoot = Resolve-MilestonePath $OutputRoot
New-Item -ItemType Directory -Force -Path $milestoneRoot | Out-Null
$reconciliationPath = Join-Path $milestoneRoot "plan-reconciliation.json"
$checkpointPath = Join-Path $milestoneRoot "milestone-checkpoint.json"
$evidenceManifestPath = Join-Path $milestoneRoot `
    "runtime-sample-evidence-manifest.json"

& (Join-Path $PSScriptRoot "Test-FullShipmentRuntimeSamplePlan.ps1") `
    -ProjectRoot $ProjectRoot -StaticInventory $staticInventoryPath `
    -Plan $planPath -Output $reconciliationPath | Out-Null
$reconciliation = Read-JsonArtifact $reconciliationPath
$planValue = Read-JsonArtifact $planPath
$entries = @($planValue.entries)
$rowsByScenario = @{}
foreach ($row in @($reconciliation.rows)) {
    $rowsByScenario[[string]$row.scenario] = $row
}
$entriesByScenario = @{}
foreach ($entry in $entries) {
    $entriesByScenario[[string]$entry.scenario] = $entry
}

[object[]]$records = @()
if (Test-Path -LiteralPath $checkpointPath -PathType Leaf) {
    $existingCheckpoint = Read-JsonArtifact $checkpointPath
    if ([string]$existingCheckpoint.static_inventory_sha256 -ne
            [string]$reconciliation.static_inventory_sha256 -or
        [string]$existingCheckpoint.sample_plan_sha256 -ne
            [string]$reconciliation.sample_plan_sha256 -or
        [string]$existingCheckpoint.requirement_inventory_sha256 -ne
            $requirementInventorySha256 -or
        [string]$existingCheckpoint.acquisition_lowering_sha256 -ne
            $acquisitionLoweringSha256 -or
        [string]$existingCheckpoint.master_angler_windows_sha256 -ne
            $masterAnglerWindowsSha256 -or
        [string]$existingCheckpoint.route_timing_calibration_sha256 -ne
            $routeTimingCalibrationSha256) {
        throw "Milestone checkpoint belongs to different evidence inputs."
    }
    $records = @($existingCheckpoint.records)
    $validatedRecords = foreach ($record in $records) {
        $scenario = [string]$record.scenario
        if (-not $entriesByScenario.ContainsKey($scenario) -or
            -not $rowsByScenario.ContainsKey($scenario)) {
            continue
        }
        try {
            Get-SummaryRecord `
                -SummaryPath ([string]$record.summary_path) `
                -PlanEntry $entriesByScenario[$scenario] `
                -ReconciliationRow $rowsByScenario[$scenario]
        }
        catch {
            Write-Warning (
                "Discarding stale milestone checkpoint record for " +
                "${scenario}: $($_.Exception.Message)")
        }
    }
    $records = @($validatedRecords)
}

foreach ($inputPath in $ExistingSummaryPaths) {
    $summaryPath = Resolve-MilestonePath $inputPath
    $summary = Read-JsonArtifact $summaryPath
    $scenario = if ([string]$summary.schema_version -eq
        "stardewai.runtime_full_shipment_sap_prefix_smoke.v1") {
        "sap_prefix"
    }
    else { [string]$summary.scenario }
    if (-not $entriesByScenario.ContainsKey($scenario)) {
        throw "Existing summary has an unknown scenario: $scenario"
    }
    $record = Get-SummaryRecord -SummaryPath $summaryPath `
        -PlanEntry $entriesByScenario[$scenario] `
        -ReconciliationRow $rowsByScenario[$scenario]
    $records = @($records | Where-Object scenario -ne $scenario) + @($record)
}

$requiredAnchorScenarios = @($entries |
    Where-Object run_batch -eq "anchor" |
    ForEach-Object { [string]$_.scenario })
if ($Batch -ne "all") {
    $completedAnchorScenarios = @($records |
        Where-Object {
            [string]$_.scenario -in $requiredAnchorScenarios -and
            (Test-Path -LiteralPath ([string]$_.summary_path) -PathType Leaf)
        } |
        ForEach-Object { [string]$_.scenario })
    $missingAnchorScenarios = @($requiredAnchorScenarios |
        Where-Object { $_ -notin $completedAnchorScenarios })
    if ($missingAnchorScenarios.Count -gt 0) {
        throw "Milestone batch requires completed anchor evidence: " +
            ($missingAnchorScenarios -join ",")
    }
}

$selectedEntries = switch ($Batch) {
    "high_risk" { @($entries | Where-Object run_batch -eq "high_risk") }
    "standard" { @($entries | Where-Object run_batch -eq "standard") }
    "all_missing" { @($entries | Where-Object run_batch -ne "anchor") }
    "all" { @($entries) }
}
$completedScenarios = @($records.scenario | ForEach-Object { [string]$_ })
$selectedEntries = @($selectedEntries | Where-Object {
    [string]$_.scenario -notin $completedScenarios
})
if ($MaxScenarios -gt 0) {
    $selectedEntries = @($selectedEntries | Select-Object -First $MaxScenarios)
}
$selectedScenarios = @($selectedEntries.scenario |
    ForEach-Object { [string]$_ })

$runner = Join-Path $PSScriptRoot `
    "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1"
$builtOnce = [bool]$SkipBuild
Write-MilestoneState -Status "running" -Records $records `
    -SelectedScenarios $selectedScenarios
if ($PlanOnly) {
    Write-MilestoneState -Status "planned_only" -Records $records `
        -SelectedScenarios $selectedScenarios
    Read-JsonArtifact $checkpointPath | ConvertTo-Json -Depth 24
    return
}

foreach ($entry in $selectedEntries) {
    $scenario = [string]$entry.scenario
    $existing = @($records | Where-Object scenario -eq $scenario)
    if ($existing.Count -eq 1 -and
        (Test-Path -LiteralPath $existing[0].summary_path -PathType Leaf)) {
        continue
    }
    $runId = "runtime-full-shipment-evidence-$scenario-" +
        (Get-Date -Format "yyyyMMdd-HHmmss")
    $parameters = @{
        ProjectRoot = $ProjectRoot
        RuntimeRoot = $RuntimeRoot
        RunId = $runId
        OutputRoot = $milestoneRoot
        Scenario = $scenario
        SkipBuild = $builtOnce
        RequirementInventory = $requirementInventoryPath
        AcquisitionLowering = $acquisitionLoweringPath
        MasterAnglerWindows = $masterAnglerWindowsPath
        RouteTimingCalibration = $routeTimingCalibrationPath
    }
    if (-not [string]::IsNullOrWhiteSpace($ArchivedFreshSaveRoot)) {
        $parameters.ArchivedFreshSaveRoot = $ArchivedFreshSaveRoot
    }
    try {
        $stdout = & $runner @parameters
        $builtOnce = $true
        $artifactRoot = Join-Path $milestoneRoot $runId
        Write-Utf8Text -Path (Join-Path $artifactRoot "runner.stdout.log") `
            -Value ([string]::Join("`n", @($stdout)))
        $summaryPath = Join-Path $artifactRoot "summary.json"
        $record = Get-SummaryRecord -SummaryPath $summaryPath `
            -PlanEntry $entry -ReconciliationRow $rowsByScenario[$scenario]
        $records = @($records | Where-Object scenario -ne $scenario) +
            @($record)
        Write-MilestoneState -Status "running" -Records $records `
            -SelectedScenarios $selectedScenarios
    }
    catch {
        Write-MilestoneState -Status "failed" -Records $records `
            -SelectedScenarios $selectedScenarios `
            -LastError $_.Exception.Message
        throw
    }
}

try {
    $evidenceIndex = Build-CurrentEvidenceIndex
    Write-MilestoneState -Status "completed_selected_batch" -Records $records `
        -SelectedScenarios $selectedScenarios
}
catch {
    Write-MilestoneState -Status "failed" -Records $records `
        -SelectedScenarios $selectedScenarios `
        -LastError $_.Exception.Message
    throw
}
[ordered]@{
    checkpoint = Read-JsonArtifact $checkpointPath
    evidence_index = $evidenceIndex
} | ConvertTo-Json -Depth 24
