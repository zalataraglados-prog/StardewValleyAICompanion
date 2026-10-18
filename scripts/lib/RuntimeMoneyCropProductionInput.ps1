function Test-MoneyCropInputPathEqual {
    param(
        [Parameter(Mandatory)] [string] $Left,
        [Parameter(Mandatory)] [string] $Right
    )
    return [string]::Equals(
        [IO.Path]::GetFullPath($Left),
        [IO.Path]::GetFullPath($Right),
        [StringComparison]::OrdinalIgnoreCase)
}

function Assert-MoneyCropInputCondition {
    param(
        [Parameter(Mandatory)] [bool] $Condition,
        [Parameter(Mandatory)] [string] $Message
    )
    if (-not $Condition) { throw $Message }
}

function Get-MoneyCropManifestArtifact {
    param(
        [Parameter(Mandatory)] $Manifest,
        [Parameter(Mandatory)] [string] $PathProperty,
        [Parameter(Mandatory)] [string] $HashProperty,
        [Parameter(Mandatory)] [string] $Label
    )
    $path = [string]$Manifest.$PathProperty
    $hash = [string]$Manifest.$HashProperty
    Assert-MoneyCropInputCondition `
        -Condition (-not [string]::IsNullOrWhiteSpace($path) -and
            [IO.Path]::IsPathRooted($path) -and
            [string]::Equals(
                [IO.Path]::GetFullPath($path),
                $path,
                [StringComparison]::OrdinalIgnoreCase)) `
        -Message "$Label path must be absolute and normalized."
    Assert-MoneyCropInputCondition `
        -Condition ($hash -match '^[0-9a-fA-F]{64}$') `
        -Message "$Label SHA-256 is invalid."
    return Assert-RuntimeFileIdentity -Path $path `
        -ExpectedSha256 $hash -Label $Label
}

function Assert-MoneyCropInputIdSet {
    param(
        [Parameter(Mandatory)] $Values,
        [Parameter(Mandatory)] $ExpectedValues,
        [Parameter(Mandatory)] [string] $Label
    )
    $actual = @($Values | ForEach-Object { [string]$_ } | Sort-Object)
    $expected = @($ExpectedValues | ForEach-Object { [string]$_ } |
        Sort-Object)
    $actualUnique = @($actual | Select-Object -Unique)
    Assert-MoneyCropInputCondition `
        -Condition ($actual.Count -gt 0 -and
            $actual.Count -eq $actualUnique.Count -and
            [string]::Join("`n", $actual) -eq
                [string]::Join("`n", $expected)) `
        -Message "$Label commitment IDs drifted."
}

function Read-MoneyCropNativeProductionInputManifest {
    param(
        [Parameter(Mandatory)] [string] $ProjectRoot,
        [Parameter(Mandatory)] [string] $Path
    )
    $manifestPath = Resolve-RuntimeEvidenceInputPath `
        -ProjectRoot $ProjectRoot -Path $Path
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
        throw "Money crop production input manifest is missing: $manifestPath"
    }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw |
        ConvertFrom-Json
    Assert-MoneyCropInputCondition `
        -Condition (
            [string]$manifest.schema_version -eq
                "money_crop_native_production_input_manifest.v1" -and
            [string]$manifest.status -eq
                "native_crop_production_input_ready" -and
            -not [string]::IsNullOrWhiteSpace([string]$manifest.input_id) -and
            [string]$manifest.goal_id -eq "earn_money" -and
            [int]$manifest.deadline_total_day_exclusive -eq 224 -and
            [long]$manifest.target_total_money_earned -eq 1000000 -and
            -not [string]::IsNullOrWhiteSpace(
                [string]$manifest.source_state_hash) -and
            [int]$manifest.source_total_day -ge 0 -and
            [int]$manifest.commitment_count -gt 0 -and
            [int]$manifest.max_queue_item_count -ge 1 -and
            [int]$manifest.max_queue_item_count -le 8 -and
            -not [bool]$manifest.formal_product_training_authorized) `
        -Message "Money crop production input manifest is not ready."

    $artifacts = @{}
    foreach ($binding in @(
        @("DecisionRequestPath", "decision_request_path",
            "decision_request_sha256", "Money crop decision request"),
        @("DecisionReceiptPath", "decision_receipt_path",
            "decision_receipt_sha256", "Money crop decision receipt"),
        @("SnapshotPath", "snapshot_path", "snapshot_sha256",
            "Money crop source snapshot"),
        @("StrategyLedgerPath", "strategy_ledger_path",
            "strategy_ledger_sha256", "Money crop strategy ledger"),
        @("ObligationRequestPath",
            "crop_production_obligation_request_path",
            "crop_production_obligation_request_sha256",
            "Money crop obligation request"),
        @("ObligationPlanPath", "crop_production_obligation_plan_path",
            "crop_production_obligation_plan_sha256",
            "Money crop obligation plan"),
        @("RunPlanRequestPath", "crop_native_run_plan_request_path",
            "crop_native_run_plan_request_sha256",
            "Money crop native run-plan request"),
        @("RunPlanPath", "crop_native_run_plan_path",
            "crop_native_run_plan_sha256",
            "Money crop native run plan")
    )) {
        $artifacts[$binding[0]] = Get-MoneyCropManifestArtifact `
            -Manifest $manifest -PathProperty $binding[1] `
            -HashProperty $binding[2] -Label $binding[3]
    }

    $sourceSavePath = [string]$manifest.source_save_slot_path
    Assert-MoneyCropInputCondition `
        -Condition (-not [string]::IsNullOrWhiteSpace($sourceSavePath) -and
            [IO.Path]::IsPathRooted($sourceSavePath) -and
            [string]::Equals(
                [IO.Path]::GetFullPath($sourceSavePath),
                $sourceSavePath,
                [StringComparison]::OrdinalIgnoreCase) -and
            [string]$manifest.source_save_tree_sha256 -match
                '^[0-9a-fA-F]{64}$') `
        -Message "Money crop source-save identity is invalid."
    $sourceSave = Assert-RuntimeSourceSaveSlot -Path $sourceSavePath
    Assert-MoneyCropInputCondition `
        -Condition ((Get-DirectoryContentHash -Path $sourceSave) -eq
            [string]$manifest.source_save_tree_sha256) `
        -Message "Money crop source-save tree drifted from its input manifest."

    $decisionRequest = Get-Content `
        -LiteralPath $artifacts.DecisionRequestPath -Raw | ConvertFrom-Json
    $snapshot = Get-Content -LiteralPath $artifacts.SnapshotPath -Raw |
        ConvertFrom-Json
    $decisionReceiptRaw = [IO.File]::ReadAllText(
        $artifacts.DecisionReceiptPath,
        [Text.Encoding]::UTF8)
    $decisionReceipt = $decisionReceiptRaw | ConvertFrom-Json
    $obligationRequest = Get-Content `
        -LiteralPath $artifacts.ObligationRequestPath -Raw | ConvertFrom-Json
    $obligationPlan = Get-Content `
        -LiteralPath $artifacts.ObligationPlanPath -Raw | ConvertFrom-Json
    $runPlanRequest = Get-Content `
        -LiteralPath $artifacts.RunPlanRequestPath -Raw | ConvertFrom-Json
    $runPlan = Get-Content -LiteralPath $artifacts.RunPlanPath -Raw |
        ConvertFrom-Json

    Assert-MoneyCropInputCondition `
        -Condition (
            [string]$snapshot.state_hash -eq
                [string]$manifest.source_state_hash -and
            [int]$snapshot.state.time.total_days.value -eq
                [int]$manifest.source_total_day) `
        -Message "Money crop source snapshot drifted from its manifest."

    Assert-MoneyCropInputCondition `
        -Condition (
            [string]$decisionRequest.schema_version -eq
                "money_revenue_crop_production_decision_request.v1" -and
            (Test-MoneyCropInputPathEqual `
                -Left ([string]$decisionRequest.snapshot_path) `
                -Right $artifacts.SnapshotPath) -and
            [string]$decisionRequest.decision_id -eq
                [string]$manifest.decision_id -and
            [string]$decisionRequest.goal_id -eq [string]$manifest.goal_id -and
            [int]$decisionRequest.deadline_total_day_exclusive -eq
                [int]$manifest.deadline_total_day_exclusive -and
            [long]$decisionRequest.target_total_money_earned -eq
                [long]$manifest.target_total_money_earned -and
            [int]$decisionRequest.max_queue_item_count -eq
                [int]$manifest.max_queue_item_count -and
            @($decisionRequest.commitments).Count -eq
                [int]$manifest.commitment_count -and
            -not [bool]$decisionRequest.formal_product_training_authorized) `
        -Message "Money crop decision request drifted from its manifest."

    Assert-MoneyCropInputCondition `
        -Condition (
            [string]$decisionReceipt.schema_version -eq
                "money_revenue_crop_production_decision_receipt.v1" -and
            [string]$decisionReceipt.status -eq
                "crop_production_decision_committed" -and
            [bool]$decisionReceipt.all_commitments_accepted -and
            (Test-MoneyCropInputPathEqual `
                -Left ([string]$decisionReceipt.request_path) `
                -Right $artifacts.DecisionRequestPath) -and
            [string]$decisionReceipt.request_sha256 -eq
                [string]$manifest.decision_request_sha256 -and
            (Test-MoneyCropInputPathEqual `
                -Left ([string]$decisionReceipt.snapshot_path) `
                -Right $artifacts.SnapshotPath) -and
            [string]$decisionReceipt.snapshot_sha256 -eq
                [string]$manifest.snapshot_sha256 -and
            [string]$decisionReceipt.decision_id -eq
                [string]$manifest.decision_id -and
            [string]$decisionReceipt.goal_id -eq
                [string]$manifest.goal_id -and
            [int]$decisionReceipt.deadline_total_day_exclusive -eq
                [int]$manifest.deadline_total_day_exclusive -and
            [long]$decisionReceipt.target_total_money_earned -eq
                [long]$manifest.target_total_money_earned -and
            [string]$decisionReceipt.source_state_hash -eq
                [string]$manifest.source_state_hash -and
            [int]$decisionReceipt.max_queue_item_count -eq
                [int]$manifest.max_queue_item_count -and
            @($decisionReceipt.mutations).Count -eq
                [int]$manifest.commitment_count -and
            @($decisionReceipt.layout_intents).Count -eq
                [int]$manifest.commitment_count -and
            -not [bool]$decisionReceipt.formal_product_training_authorized) `
        -Message "Money crop decision receipt drifted from its manifest."
    $receiptLedgerRaw = Get-RuntimeRawJsonProperty `
        -RawJson $decisionReceiptRaw -PropertyName "final_ledger"
    $ledgerRaw = [IO.File]::ReadAllText(
        $artifacts.StrategyLedgerPath,
        [Text.Encoding]::UTF8)
    Assert-MoneyCropInputCondition `
        -Condition ([string]::Equals(
            $receiptLedgerRaw.Trim(),
            $ledgerRaw.Trim(),
            [StringComparison]::Ordinal)) `
        -Message "Money crop strategy ledger is not the decision final ledger."

    Assert-MoneyCropInputCondition `
        -Condition (
            [string]$obligationRequest.schema_version -eq
                "money_revenue_crop_production_obligation_plan_request.v1" -and
            (Test-MoneyCropInputPathEqual `
                -Left ([string]$obligationRequest.snapshot_path) `
                -Right $artifacts.SnapshotPath) -and
            (Test-MoneyCropInputPathEqual `
                -Left ([string]$obligationRequest.strategy_ledger_path) `
                -Right $artifacts.StrategyLedgerPath) -and
            [int]$obligationRequest.deadline_total_day_exclusive -eq
                [int]$manifest.deadline_total_day_exclusive -and
            [long]$obligationRequest.target_total_money_earned -eq
                [long]$manifest.target_total_money_earned -and
            -not [bool]$obligationRequest.formal_product_training_authorized) `
        -Message "Money crop obligation request drifted from its manifest."

    Assert-MoneyCropInputCondition `
        -Condition (
            [string]$obligationPlan.schema_version -eq
                "money_revenue_crop_production_obligation_plan.v2" -and
            (Test-MoneyCropInputPathEqual `
                -Left ([string]$obligationPlan.request_path) `
                -Right $artifacts.ObligationRequestPath) -and
            [string]$obligationPlan.request_sha256 -eq
                [string]$manifest.crop_production_obligation_request_sha256 -and
            (Test-MoneyCropInputPathEqual `
                -Left ([string]$obligationPlan.snapshot_path) `
                -Right $artifacts.SnapshotPath) -and
            [string]$obligationPlan.snapshot_sha256 -eq
                [string]$manifest.snapshot_sha256 -and
            (Test-MoneyCropInputPathEqual `
                -Left ([string]$obligationPlan.strategy_ledger_path) `
                -Right $artifacts.StrategyLedgerPath) -and
            [string]$obligationPlan.strategy_ledger_sha256 -eq
                [string]$manifest.strategy_ledger_sha256 -and
            [string]$obligationPlan.source_state_hash -eq
                [string]$manifest.source_state_hash -and
            [int]$obligationPlan.deadline_total_day_exclusive -eq
                [int]$manifest.deadline_total_day_exclusive -and
            [long]$obligationPlan.target_total_money_earned -eq
                [long]$manifest.target_total_money_earned -and
            [int]$obligationPlan.active_commitment_count -eq
                [int]$manifest.commitment_count -and
            -not [bool]$obligationPlan.formal_product_training_authorized) `
        -Message "Money crop obligation plan drifted from its manifest."

    Assert-MoneyCropInputCondition `
        -Condition (
            [string]$runPlanRequest.schema_version -eq
                "money_revenue_crop_native_production_run_plan_request.v1" -and
            (Test-MoneyCropInputPathEqual `
                -Left ([string]$runPlanRequest.crop_production_plan_path) `
                -Right $artifacts.ObligationPlanPath) -and
            @($runPlanRequest.layout_intents).Count -eq
                [int]$manifest.commitment_count -and
            [int]$runPlanRequest.max_queue_item_count -eq
                [int]$manifest.max_queue_item_count -and
            -not [bool]$runPlanRequest.formal_product_training_authorized) `
        -Message "Money crop run-plan request drifted from its manifest."
    $receiptLayoutJson = @($decisionReceipt.layout_intents) |
        ConvertTo-Json -Depth 96 -Compress
    $requestLayoutJson = @($runPlanRequest.layout_intents) |
        ConvertTo-Json -Depth 96 -Compress
    Assert-MoneyCropInputCondition `
        -Condition ([string]::Equals(
            $receiptLayoutJson,
            $requestLayoutJson,
            [StringComparison]::Ordinal)) `
        -Message "Money crop layout intents drifted after the decision receipt."

    Assert-MoneyCropInputCondition `
        -Condition (
            [string]$runPlan.schema_version -eq
                "money_revenue_crop_native_production_run_plan.v1" -and
            [string]$runPlan.status -eq
                "native_crop_production_run_ready" -and
            [bool]$runPlan.native_production_run_ready -and
            (Test-MoneyCropInputPathEqual `
                -Left ([string]$runPlan.request_path) `
                -Right $artifacts.RunPlanRequestPath) -and
            [string]$runPlan.request_sha256 -eq
                [string]$manifest.crop_native_run_plan_request_sha256 -and
            (Test-MoneyCropInputPathEqual `
                -Left ([string]$runPlan.crop_production_plan_path) `
                -Right $artifacts.ObligationPlanPath) -and
            [string]$runPlan.crop_production_plan_sha256 -eq
                [string]$manifest.crop_production_obligation_plan_sha256 -and
            [string]$runPlan.source_state_hash -eq
                [string]$manifest.source_state_hash -and
            [int]$runPlan.source_total_day -eq
                [int]$manifest.source_total_day -and
            [int]$runPlan.max_queue_item_count -eq
                [int]$manifest.max_queue_item_count -and
            -not [bool]$runPlan.formal_product_training_authorized) `
        -Message "Money crop native run plan drifted from its manifest."

    $expectedIds = @($decisionRequest.commitments | ForEach-Object {
        [string]$_.commitment_id
    })
    Assert-MoneyCropInputIdSet `
        -Values @($decisionReceipt.layout_intents.commitment_id) `
        -ExpectedValues $expectedIds -Label "Decision receipt"
    Assert-MoneyCropInputIdSet `
        -Values @($obligationPlan.commitments.commitment_id) `
        -ExpectedValues $expectedIds -Label "Obligation plan"
    Assert-MoneyCropInputIdSet `
        -Values @($runPlanRequest.layout_intents.commitment_id) `
        -ExpectedValues $expectedIds -Label "Run-plan request"
    Assert-MoneyCropInputIdSet `
        -Values @($runPlan.covered_commitment_ids) `
        -ExpectedValues $expectedIds -Label "Native run plan"

    return [pscustomobject]@{
        ManifestPath = $manifestPath
        ManifestSha256 = Get-RuntimeFileSha256 -Path $manifestPath
        Manifest = $manifest
        SourceSavePath = $sourceSave
        DecisionRequestPath = $artifacts.DecisionRequestPath
        DecisionReceiptPath = $artifacts.DecisionReceiptPath
        SnapshotPath = $artifacts.SnapshotPath
        StrategyLedgerPath = $artifacts.StrategyLedgerPath
        ObligationRequestPath = $artifacts.ObligationRequestPath
        ObligationPlanPath = $artifacts.ObligationPlanPath
        RunPlanRequestPath = $artifacts.RunPlanRequestPath
        RunPlanPath = $artifacts.RunPlanPath
        DecisionReceipt = $decisionReceipt
        ObligationPlan = $obligationPlan
        RunPlanRequest = $runPlanRequest
        RunPlan = $runPlan
    }
}
