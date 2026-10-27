function Get-MoneyCropSafeArtifactName {
    param([Parameter(Mandatory)] [string] $Value)
    return ($Value -replace '[^a-zA-Z0-9._-]', '_')
}

function Assert-MoneyCropNativeRunPlan {
    param([Parameter(Mandatory)] $Plan)

    if ([string]$Plan.schema_version -ne
            "money_revenue_crop_native_production_run_plan.v3" -or
        [string]$Plan.status -ne "native_crop_production_run_ready" -or
        -not [bool]$Plan.native_production_run_ready -or
        [bool]$Plan.formal_product_training_authorized -or
        [string]::IsNullOrWhiteSpace([string]$Plan.source_state_hash) -or
        [int]$Plan.source_total_day -ne [int]$Plan.first_total_day -or
        @($Plan.days).Count -ne [int]$Plan.day_count -or
        @($Plan.covered_commitment_ids).Count -eq 0 -or
        @($Plan.seed_acquisitions).Count -ne
            [int]$Plan.seed_acquisition_count) {
        throw "Money crop native-production run plan is not executable."
    }

    foreach ($acquisition in @($Plan.seed_acquisitions)) {
        if ([int]$acquisition.acquisition_index -le 0 -or
            [string]::IsNullOrWhiteSpace(
                [string]$acquisition.commitment_id) -or
            [string]::IsNullOrWhiteSpace(
                [string]$acquisition.seed_qualified_item_id) -or
            [string]::IsNullOrWhiteSpace(
                [string]$acquisition.shop_id) -or
            [int]$acquisition.max_unit_price -le 0 -or
            [int]$acquisition.required_quantity -le 0 -or
            [int]$acquisition.deadline_total_day -lt
                [int]$Plan.source_total_day -or
            [string]$acquisition.execution_policy -ne
                "economy.buy_supplies_one_unit_fresh_snapshot_slices") {
            throw "Money crop native-production seed acquisition is invalid."
        }
    }

    $acquisitionIndexes = @($Plan.seed_acquisitions | ForEach-Object {
        [int]$_.acquisition_index
    })
    $expectedAcquisitionIndexes = if ($acquisitionIndexes.Count -eq 0) {
        @()
    }
    else {
        @(1..$acquisitionIndexes.Count)
    }
    if ([string]::Join(",", $acquisitionIndexes) -ne
        [string]::Join(",", $expectedAcquisitionIndexes) -or
        @($Plan.seed_acquisitions.commitment_id |
            Select-Object -Unique).Count -ne
            @($Plan.seed_acquisitions).Count) {
        throw "Money crop native-production seed acquisitions are duplicated or out of order."
    }

    $days = @($Plan.days)
    for ($index = 0; $index -lt $days.Count; $index++) {
        if ([int]$days[$index].total_day -ne
            [int]$Plan.first_total_day + $index) {
            throw "Money crop native-production run plan is not contiguous."
        }
    }
}

function Assert-MoneyCropEvidenceFiles {
    param([Parameter(Mandatory)] [object[]] $Paths)

    $resolved = [Collections.Generic.List[string]]::new()
    foreach ($path in @($Paths)) {
        if ([string]::IsNullOrWhiteSpace([string]$path) -or
            -not (Test-Path -LiteralPath ([string]$path) -PathType Leaf)) {
            throw "Required Money crop evidence file is missing: $path"
        }
        $resolved.Add([IO.Path]::GetFullPath([string]$path))
    }
    $duplicates = @($resolved | Group-Object | Where-Object Count -gt 1)
    if ($duplicates.Count -gt 0) {
        throw "Money crop evidence paths contain duplicates."
    }
}

function New-MoneyCropSeedReservationProof {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $BootstrapDll,
        [Parameter(Mandatory)] [string] $ProductionPlanPath,
        [Parameter(Mandatory)] [string] $CommitmentId,
        [Parameter(Mandatory)] [string] $ReservationSnapshotPath,
        [Parameter(Mandatory)] [string] $EvidenceDirectory
    )

    Assert-MoneyCropEvidenceFiles -Paths @(
        $BootstrapDll,
        $ProductionPlanPath,
        $ReservationSnapshotPath)
    if ([string]::IsNullOrWhiteSpace($CommitmentId)) {
        throw "Money crop seed-reservation commitment id is missing."
    }
    $snapshot = Get-Content -LiteralPath $ReservationSnapshotPath -Raw |
        ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace([string]$snapshot.real_timestamp)) {
        throw "Money crop seed-reservation snapshot timestamp is missing."
    }

    New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
    $safeId = Get-MoneyCropSafeArtifactName -Value $CommitmentId
    $commitRequestPath = Join-Path $EvidenceDirectory `
        "seed-reservation-commit-request-$safeId.json"
    $commitResultPath = Join-Path $EvidenceDirectory `
        "seed-reservation-commit-result-$safeId.json"
    $committedLedgerPath = Join-Path $EvidenceDirectory `
        "seed-reservation-committed-ledger-$safeId.json"
    $proofRequestPath = Join-Path $EvidenceDirectory `
        "seed-reservation-request-$safeId.json"
    $proofPath = Join-Path $EvidenceDirectory `
        "seed-reservation-proof-$safeId.json"

    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments @(
        "build-money-revenue-crop-seed-reservation-commit-request",
        "--crop-production-plan", $ProductionPlanPath,
        "--commitment-id", $CommitmentId,
        "--reservation-snapshot", $ReservationSnapshotPath,
        "--output", $commitRequestPath)
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments @(
        "replay-money-revenue-crop-seed-reservation-commit",
        "--crop-production-plan", $ProductionPlanPath,
        "--commitment-id", $CommitmentId,
        "--reservation-snapshot", $ReservationSnapshotPath,
        "--request", $commitRequestPath,
        "--updated-at", ([string]$snapshot.real_timestamp),
        "--output", $commitResultPath)
    $commitResultRaw = Get-Content -LiteralPath $commitResultPath -Raw
    Write-RawJsonProperty -Path $committedLedgerPath `
        -RawJson $commitResultRaw -PropertyName "ledger"
    Write-JsonFile -Path $proofRequestPath -Value ([ordered]@{
        schema_version =
            "money_revenue_crop_seed_reservation_proof_request.v2"
        crop_production_plan_path = $ProductionPlanPath
        commitment_id = $CommitmentId
        reservation_snapshot_path = $ReservationSnapshotPath
        committed_strategy_ledger_path = $committedLedgerPath
        atomic_commit_request_path = $commitRequestPath
        atomic_commit_result_path = $commitResultPath
        formal_product_training_authorized = $false
    })
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments @(
        "build-money-revenue-crop-seed-reservation-proof",
        "--request", $proofRequestPath,
        "--output", $proofPath)
    return [IO.Path]::GetFullPath($proofPath)
}

function New-MoneyCropOutputRetentionProofs {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $BootstrapDll,
        [Parameter(Mandatory)] [string] $ProductionPlanPath,
        [Parameter(Mandatory)] $Commitment,
        [Parameter(Mandatory)] [string] $LifecycleProofPath,
        [Parameter(Mandatory)] [string] $EvidenceDirectory
    )

    Assert-MoneyCropEvidenceFiles -Paths @(
        $BootstrapDll,
        $ProductionPlanPath,
        $LifecycleProofPath)
    $commitmentId = [string]$Commitment.commitment_id
    if ([string]::IsNullOrWhiteSpace($commitmentId)) {
        throw "Money crop retention commitment id is missing."
    }
    $waves = @($Commitment.harvest_waves)
    if ($waves.Count -eq 0) {
        throw "Money crop retention commitment has no harvest waves: $commitmentId"
    }
    $waveIndexes = @($waves | ForEach-Object { [int]$_.wave_index })
    if (@($waveIndexes | Where-Object { $_ -le 0 }).Count -gt 0 -or
        @($waveIndexes | Select-Object -Unique).Count -ne $waves.Count) {
        throw "Money crop retention harvest waves are invalid: $commitmentId"
    }

    New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
    $safeId = Get-MoneyCropSafeArtifactName -Value $commitmentId
    $proofPaths = [Collections.Generic.List[string]]::new()
    foreach ($wave in $waves) {
        $waveIndex = [int]$wave.wave_index
        $requestPath = Join-Path $EvidenceDirectory `
            "retention-request-$safeId-$waveIndex.json"
        $proofPath = Join-Path $EvidenceDirectory `
            "retention-proof-$safeId-$waveIndex.json"
        Write-JsonFile -Path $requestPath -Value ([ordered]@{
            schema_version =
                "money_revenue_crop_output_retention_proof_request.v1"
            crop_production_plan_path = $ProductionPlanPath
            native_lifecycle_proof_path = $LifecycleProofPath
            commitment_id = $commitmentId
            wave_index = $waveIndex
            formal_product_training_authorized = $false
        })
        Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments @(
            "build-money-revenue-crop-output-retention-proof",
            "--request", $requestPath,
            "--output", $proofPath)
        $proofPaths.Add([IO.Path]::GetFullPath($proofPath))
    }

    return $proofPaths.ToArray()
}

function Complete-MoneyCropProductionProofCheckpoint {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] [string] $BootstrapDll,
        [Parameter(Mandatory)] [string] $ProductionPlanPath,
        [Parameter(Mandatory)] [string] $EvidenceDirectory,
        [Parameter(Mandatory)] [object[]] $PlantingLayoutProofPaths,
        [object[]] $SeedReservationProofPaths = @(),
        [object[]] $SeedAcquisitionProofPaths = @(),
        [Parameter(Mandatory)] [object[]] $DailyCapacityProofPaths,
        [Parameter(Mandatory)] [object[]] $NativeLifecycleProofPaths,
        [Parameter(Mandatory)] [object[]] $OutputRetentionProofPaths,
        [object[]] $TerminalSettlementProofPaths = @(),
        [object[]] $SettlementCapacityProofPaths = @(),
        [object[]] $SharedShippingSettlementProofPaths = @()
    )

    $allProofPaths = @(
        @($PlantingLayoutProofPaths)
        @($SeedReservationProofPaths)
        @($SeedAcquisitionProofPaths)
        @($DailyCapacityProofPaths)
        @($NativeLifecycleProofPaths)
        @($OutputRetentionProofPaths)
        @($TerminalSettlementProofPaths)
        @($SettlementCapacityProofPaths)
        @($SharedShippingSettlementProofPaths)
    )
    Assert-MoneyCropEvidenceFiles -Paths @(
        $BootstrapDll,
        $ProductionPlanPath)
    Assert-MoneyCropEvidenceFiles -Paths $allProofPaths

    New-Item -ItemType Directory -Force -Path $EvidenceDirectory | Out-Null
    $fragmentIndexPath = Join-Path $EvidenceDirectory "fragment-index.json"
    $checkpointRequestPath = Join-Path $EvidenceDirectory `
        "checkpoint-request.json"
    $checkpointPath = Join-Path $EvidenceDirectory "checkpoint.json"
    Write-JsonFile -Path $fragmentIndexPath -Value ([ordered]@{
        schema_version =
            "money_revenue_crop_production_proof_fragment_index.v13"
        crop_production_plan_path = $ProductionPlanPath
        crop_production_plan_sha256 =
            (Get-RuntimeFileSha256 -Path $ProductionPlanPath)
        planting_layout_proof_paths = @($PlantingLayoutProofPaths)
        seed_reservation_proof_paths = @($SeedReservationProofPaths)
        seed_acquisition_proof_paths = @($SeedAcquisitionProofPaths)
        daily_capacity_proof_paths = @($DailyCapacityProofPaths)
        native_lifecycle_proof_paths = @($NativeLifecycleProofPaths)
        output_retention_proof_paths = @($OutputRetentionProofPaths)
        terminal_settlement_proof_paths = @($TerminalSettlementProofPaths)
        settlement_capacity_proof_paths = @($SettlementCapacityProofPaths)
        shared_shipping_settlement_proof_paths =
            @($SharedShippingSettlementProofPaths)
        formal_product_training_authorized = $false
    })
    Write-JsonFile -Path $checkpointRequestPath -Value ([ordered]@{
        schema_version =
            "money_revenue_crop_production_proof_checkpoint_request.v12"
        crop_production_plan_path = $ProductionPlanPath
        fragment_index_path = $fragmentIndexPath
        formal_product_training_authorized = $false
    })
    Invoke-RuntimeBootstrap -BootstrapDll $BootstrapDll -Arguments @(
        "build-money-revenue-crop-production-proof-checkpoint",
        "--request", $checkpointRequestPath,
        "--output", $checkpointPath)

    return [pscustomobject]@{
        FragmentIndexPath = [IO.Path]::GetFullPath($fragmentIndexPath)
        CheckpointRequestPath = [IO.Path]::GetFullPath($checkpointRequestPath)
        CheckpointPath = [IO.Path]::GetFullPath($checkpointPath)
        Checkpoint = Get-Content -LiteralPath $checkpointPath -Raw |
            ConvertFrom-Json
    }
}
