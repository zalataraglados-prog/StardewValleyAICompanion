[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $WorkDirectory,
    [Parameter(Mandatory)]
    [string] $TerminalProofIndex,
    [string] $ComparisonWaveHandoffPath = "",
    [string] $ExistingComparisonAdmissionIndex = "",
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

function Resolve-ProofPath {
    param([string] $Path, [string] $Label, [switch] $Optional)
    if ([string]::IsNullOrWhiteSpace($Path)) {
        if ($Optional) { return "" }
        throw "$Label path is required."
    }
    return Resolve-RuntimeEvidenceInputPath `
        -ProjectRoot $ProjectRoot -Path $Path
}

function Get-StableIdentityHash {
    param([Parameter(Mandatory)][string] $Value)
    $algorithm = [Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [Text.Encoding]::UTF8.GetBytes($Value)
        return ([BitConverter]::ToString(
            $algorithm.ComputeHash($bytes))).Replace("-", "").ToLowerInvariant()
    }
    finally {
        $algorithm.Dispose()
    }
}

function Test-SamePath {
    param([string] $Left, [string] $Right)
    return [string]::Equals(
        [IO.Path]::GetFullPath($Left),
        [IO.Path]::GetFullPath($Right),
        [StringComparison]::OrdinalIgnoreCase)
}

function Assert-FileIdentity {
    param([string] $Path, [string] $ExpectedSha256, [string] $Label)
    $fullPath = Resolve-ProofPath -Path $Path -Label $Label
    $actual = (Get-FileHash -LiteralPath $fullPath `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $ExpectedSha256) {
        throw "$Label hash drifted from the comparison-wave handoff."
    }
    return $fullPath
}

function Convert-RouteTransition {
    param($Transition)
    [ordered]@{
        transition_id = [string]$Transition.transition_id
        queue_path = Resolve-ProofPath `
            -Path ([string]$Transition.queue_path) -Label "Route queue"
        before_snapshot_path = Resolve-ProofPath `
            -Path ([string]$Transition.before_snapshot_path) `
            -Label "Route before snapshot"
        execution_receipt_path = Resolve-ProofPath `
            -Path ([string]$Transition.execution_receipt_path) `
            -Label "Route execution receipt"
        after_snapshot_path = Resolve-ProofPath `
            -Path ([string]$Transition.after_snapshot_path) `
            -Label "Route after snapshot"
        run_id = [string]$Transition.run_id
        executor_version = [string]$Transition.executor_version
        selected_candidate_id = [string]$Transition.selected_candidate_id
    }
}

function Convert-TerminalAlternative {
    param($Alternative)
    $shipping = $null
    if ($null -ne $Alternative.shipping_deposit_transition) {
        $shipping = Convert-RouteTransition `
            -Transition $Alternative.shipping_deposit_transition
    }
    [ordered]@{
        request_id = [string]$Alternative.request_id
        before_strategy_ledger_capture_path = Resolve-ProofPath `
            -Path ([string]$Alternative.before_strategy_ledger_capture_path) `
            -Label "Before-terminal strategy-ledger capture"
        after_strategy_ledger_capture_path = Resolve-ProofPath `
            -Path ([string]$Alternative.after_strategy_ledger_capture_path) `
            -Label "After-terminal strategy-ledger capture"
        route_transitions = @($Alternative.route_transitions | ForEach-Object {
            Convert-RouteTransition -Transition $_
        })
        shop_sale_episode_path = Resolve-ProofPath `
            -Path ([string]$Alternative.shop_sale_episode_path) `
            -Label "Native shop-sale episode" -Optional
        shipping_deposit_transition = $shipping
        revenue_recurrence_manifest_path = Resolve-ProofPath `
            -Path ([string]$Alternative.revenue_recurrence_manifest_path) `
            -Label "Revenue recurrence manifest"
        revenue_recurrence_receipt_path = Resolve-ProofPath `
            -Path ([string]$Alternative.revenue_recurrence_receipt_path) `
            -Label "Revenue recurrence receipt"
    }
}

$workPath = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $WorkDirectory
if (-not (Test-Path -LiteralPath $workPath -PathType Container)) {
    throw "Money Revenue comparison work directory is missing: $workPath"
}
$planPath = Join-Path $workPath `
    "money-revenue-comparison-collection-plan.json"
$counterfactualManifestPath = Join-Path $workPath `
    "money-revenue-counterfactual-proof-request-manifest.json"
$counterfactualCatalogPath = Join-Path $workPath `
    "money-revenue-counterfactual-proof-request-catalog.json"
foreach ($required in @(
    $planPath,
    $counterfactualManifestPath,
    $counterfactualCatalogPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Money Revenue comparison prerequisite is missing: $required"
    }
}
$plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
$criterionIdsToComplete = @($plan.crossed_criterion_ids |
    ForEach-Object { [string]$_ })
if (-not [string]::IsNullOrWhiteSpace($ComparisonWaveHandoffPath)) {
    $handoffPath = Resolve-ProofPath `
        -Path $ComparisonWaveHandoffPath `
        -Label "Money Revenue comparison-wave handoff"
    $handoff = Get-Content -LiteralPath $handoffPath -Raw |
        ConvertFrom-Json
    $handoffCriterionIds = @($handoff.scheduled_criterion_ids |
        ForEach-Object { [string]$_ })
    if ([string]$handoff.schema_version -ne
            "money_revenue_comparison_wave_handoff.v1" -or
        [string]$handoff.status -ne "comparison_wave_initialized" -or
        [bool]$handoff.formal_product_training_authorized -or
        [string]$handoff.source_id -ne [string]$plan.run_id -or
        [string]$handoff.dataset_partition -ne
            [string]$plan.dataset_partition -or
        -not (Test-SamePath `
            -Left ([string]$handoff.work_directory) -Right $workPath) -or
        -not (Test-SamePath `
            -Left ([string]$handoff.comparison_collection_plan_path) `
            -Right $planPath) -or
        $handoffCriterionIds.Count -eq 0 -or
        @($handoffCriterionIds | Sort-Object -Unique).Count -ne
            $handoffCriterionIds.Count -or
        @($handoffCriterionIds | Where-Object {
            $criterionIdsToComplete -notcontains $_
        }).Count -gt 0) {
        throw "Money Revenue comparison-wave handoff is invalid."
    }
    Assert-FileIdentity `
        -Path ([string]$handoff.source_work_plan_path) `
        -ExpectedSha256 ([string]$handoff.source_work_plan_sha256) `
        -Label "Source Money work plan" | Out-Null
    Assert-FileIdentity `
        -Path ([string]$handoff.source_corpus_path) `
        -ExpectedSha256 ([string]$handoff.source_corpus_sha256) `
        -Label "Source Money corpus" | Out-Null
    $handoffRunSummaryPath = Assert-FileIdentity `
        -Path ([string]$handoff.native_run_summary_path) `
        -ExpectedSha256 ([string]$handoff.native_run_summary_sha256) `
        -Label "Source Money native run summary"
    Assert-FileIdentity `
        -Path ([string]$handoff.comparison_collection_plan_path) `
        -ExpectedSha256 `
            ([string]$handoff.comparison_collection_plan_sha256) `
        -Label "Money Revenue comparison collection plan" | Out-Null
    if (-not (Test-SamePath `
        -Left $handoffRunSummaryPath `
        -Right ([string]$plan.native_run_summary_path))) {
        throw "Comparison-wave handoff native run does not match the plan."
    }
    $criterionIdsToComplete = $handoffCriterionIds
}

$terminalIndexPath = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $TerminalProofIndex
if (-not (Test-Path -LiteralPath $terminalIndexPath -PathType Leaf)) {
    throw "Money Revenue terminal-proof index is missing: $terminalIndexPath"
}
$terminalIndex = Get-Content -LiteralPath $terminalIndexPath -Raw |
    ConvertFrom-Json
if ([string]$terminalIndex.schema_version -ne
        "money_revenue_terminal_proof_index.v1" -or
    [bool]$terminalIndex.formal_product_training_authorized) {
    throw "Money Revenue terminal-proof index schema is invalid."
}
$terminalAlternatives = @($terminalIndex.alternatives | ForEach-Object {
    Convert-TerminalAlternative -Alternative $_
})

$bootstrapProject = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj"
$bootstrapDll = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"
if (-not $SkipBuild) {
    & dotnet build $bootstrapProject -c Release --no-restore --nologo |
        Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Goal-conditioned bootstrap Release build failed."
    }
}
if (-not (Test-Path -LiteralPath $bootstrapDll -PathType Leaf)) {
    throw "Goal-conditioned bootstrap Release DLL is missing: $bootstrapDll"
}

$assemblyRequestPath = Join-Path $workPath `
    "money-revenue-terminal-execution-manifest-assembly-request.json"
$terminalManifestPath = Join-Path $workPath `
    "money-revenue-terminal-execution-proof-manifest.json"
$terminalReceiptPath = Join-Path $workPath `
    "money-revenue-terminal-execution-proof-receipt.json"
Write-JsonFile -Path $assemblyRequestPath -Value ([ordered]@{
    schema_version =
        "money_revenue_terminal_execution_manifest_assembly_request.v1"
    counterfactual_request_manifest_path = $counterfactualManifestPath
    counterfactual_request_catalog_path = $counterfactualCatalogPath
    alternatives = $terminalAlternatives
    formal_product_training_authorized = $false
})
Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
    "build-money-revenue-terminal-execution-manifest",
    "--request", $assemblyRequestPath,
    "--output", $terminalManifestPath)
Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
    "build-money-revenue-terminal-execution-proof",
    "--manifest", $terminalManifestPath,
    "--output", $terminalReceiptPath)

$admissionRoot = Join-Path $workPath "comparison-admissions"
$existingAttachments = @()
if (-not [string]::IsNullOrWhiteSpace(
        $ExistingComparisonAdmissionIndex)) {
    $existingIndexPath = Resolve-ProofPath `
        -Path $ExistingComparisonAdmissionIndex `
        -Label "Existing Money comparison-admission index"
    $existingIndex = Get-Content -LiteralPath $existingIndexPath -Raw |
        ConvertFrom-Json
    if ([string]$existingIndex.schema_version -notin @(
            "money_threshold_comparison_admission_index.v1",
            "money_threshold_comparison_admission_index.v2",
            "money_threshold_comparison_admission_index.v3") -or
        [bool]$existingIndex.formal_product_training_authorized) {
        throw "Existing Money comparison-admission index is invalid."
    }
    $existingAttachments = @($existingIndex.admissions | ForEach-Object {
        $runId = [string]$_.run_id
        $criterionId = [string]$_.criterion_id
        $episodePath = [string]$_.execution_episode_path
        $trajectoryPath = [string]$_.native_trajectory_receipt_path
        if ([string]::IsNullOrWhiteSpace($runId) -or
            [string]::IsNullOrWhiteSpace($criterionId) -or
            ($existingIndex.schema_version -eq
                "money_threshold_comparison_admission_index.v1" -and
                (-not [string]::IsNullOrWhiteSpace($episodePath) -or
                 -not [string]::IsNullOrWhiteSpace($trajectoryPath))) -or
            ($existingIndex.schema_version -eq
                "money_threshold_comparison_admission_index.v2" -and
                -not [string]::IsNullOrWhiteSpace($trajectoryPath)) -or
            (-not [string]::IsNullOrWhiteSpace($episodePath) -and
             -not [string]::IsNullOrWhiteSpace($trajectoryPath))) {
            throw "Existing comparison-admission attachment is invalid."
        }
        [ordered]@{
            run_id = $runId
            criterion_id = $criterionId
            admission_manifest_path = Resolve-ProofPath `
                -Path ([string]$_.admission_manifest_path) `
                -Label "Existing comparison-admission manifest"
            admission_receipt_path = Resolve-ProofPath `
                -Path ([string]$_.admission_receipt_path) `
                -Label "Existing comparison-admission receipt"
            execution_episode_path = Resolve-ProofPath `
                -Path $episodePath `
                -Label "Existing selected execution episode" -Optional
            native_trajectory_receipt_path = Resolve-ProofPath `
                -Path $trajectoryPath `
                -Label "Existing native trajectory receipt" -Optional
        }
    })
    $existingKeys = @($existingAttachments | ForEach-Object {
        ([string]$_.run_id) + "|" + ([string]$_.criterion_id)
    })
    if ($existingKeys.Count -ne
        @($existingKeys | Sort-Object -Unique).Count) {
        throw "Existing Money comparison-admission index has duplicate keys."
    }
}
$attachments = @()
$unmatched = @()
$trajectoryReceiptsByAlternative =
    [Collections.Generic.Dictionary[string, string]]::new(
        [StringComparer]::Ordinal)
foreach ($criterionId in $criterionIdsToComplete) {
    $safeCriterion = ([string]$criterionId) -replace '[^a-zA-Z0-9._-]', '_'
    $criterionRoot = Join-Path $admissionRoot $safeCriterion
    $admissionManifestPath = Join-Path $criterionRoot `
        "money-revenue-comparison-admission-manifest.json"
    $admissionReceiptPath = Join-Path $criterionRoot `
        "money-revenue-comparison-admission-receipt.json"
    Write-JsonFile -Path $admissionManifestPath -Value ([ordered]@{
        schema_version = "money_revenue_comparison_admission_manifest.v1"
        criterion_id = [string]$criterionId
        terminal_execution_proof_manifest_path = $terminalManifestPath
        terminal_execution_proof_receipt_path = $terminalReceiptPath
        formal_product_training_authorized = $false
    })
    Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
        "build-money-revenue-comparison-admission",
        "--manifest", $admissionManifestPath,
        "--output", $admissionReceiptPath)
    $receipt = Get-Content -LiteralPath $admissionReceiptPath -Raw |
        ConvertFrom-Json
    $selectedId = [string]$receipt.comparison.selected_alternative_id
    $selected = @($receipt.alternatives | Where-Object {
        [string]$_.concrete_alternative_id -eq $selectedId
    })
    $hasSelectedOutcome =
        [bool]$receipt.production_teacher_preference_label_eligible -and
        $selected.Count -eq 1
    if (-not $hasSelectedOutcome) {
        $unmatched += [ordered]@{
            criterion_id = [string]$criterionId
            selected_alternative_id = $selectedId
            reason = if (-not [bool]$receipt.production_teacher_preference_label_eligible) {
                "authoritative_frontier_not_unique"
            } else {
                "selected_alternative_evidence_missing"
            }
        }
        continue
    }

    if ($trajectoryReceiptsByAlternative.ContainsKey($selectedId)) {
        $trajectoryReceiptPath =
            $trajectoryReceiptsByAlternative[$selectedId]
    }
    else {
        $selectedIdentityHash = Get-StableIdentityHash -Value $selectedId
        $trajectoryRoot = Join-Path (
            Join-Path $workPath "native-trajectories") $selectedIdentityHash
        $trajectoryManifestPath = Join-Path $trajectoryRoot `
            "money-threshold-native-trajectory-manifest.json"
        $trajectoryReceiptPath = Join-Path $trajectoryRoot `
            "money-threshold-native-trajectory-receipt.json"
        Write-JsonFile -Path $trajectoryManifestPath -Value ([ordered]@{
            schema_version = "money_threshold_native_trajectory_manifest.v1"
            request_id = [string]$selected[0].request_id
            terminal_execution_proof_manifest_path = $terminalManifestPath
            terminal_execution_proof_receipt_path = $terminalReceiptPath
            formal_product_training_authorized = $false
        })
        Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
            "build-money-threshold-native-trajectory",
            "--manifest", $trajectoryManifestPath,
            "--output", $trajectoryReceiptPath)
        $trajectoryReceiptsByAlternative.Add(
            $selectedId,
            $trajectoryReceiptPath)
    }

    $verificationRequestPath = Join-Path $criterionRoot `
        "money-threshold-comparison-verification-corpus-request.json"
    $verificationCorpusPath = Join-Path $criterionRoot `
        "money-threshold-comparison-verification-corpus.json"
    Write-JsonFile -Path $verificationRequestPath -Value ([ordered]@{
        schema_version = "money_threshold_teacher_corpus_request.v4"
        corpus_id = ([string]$plan.run_id) + "-" + [string]$criterionId
        sources = @([ordered]@{
            source_id = [string]$plan.run_id
            execution_episode_path = ""
            native_trajectory_receipt_path = $trajectoryReceiptPath
            comparison_admissions = @([ordered]@{
                criterion_id = [string]$criterionId
                admission_manifest_path = $admissionManifestPath
                admission_receipt_path = $admissionReceiptPath
            })
        })
        formal_product_training_authorized = $false
    })
    Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
        "build-money-threshold-teacher-corpus",
        "--request", $verificationRequestPath,
        "--output", $verificationCorpusPath)
    $attachments += [ordered]@{
        run_id = [string]$plan.run_id
        criterion_id = [string]$criterionId
        admission_manifest_path = $admissionManifestPath
        admission_receipt_path = $admissionReceiptPath
        selected_alternative_id = $selectedId
        native_trajectory_receipt_path = $trajectoryReceiptPath
    }
}

$existingKeySet = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::Ordinal)
foreach ($existing in $existingAttachments) {
    [void]$existingKeySet.Add(
        ([string]$existing.run_id) + "|" +
        ([string]$existing.criterion_id))
}
foreach ($attachment in $attachments) {
    $key = ([string]$attachment.run_id) + "|" +
        ([string]$attachment.criterion_id)
    if (-not $existingKeySet.Add($key)) {
        throw "New comparison admission duplicates the existing index: $key"
    }
}
$cumulativeAttachments = @($existingAttachments) + @($attachments)
$outcomesByRun = $cumulativeAttachments | Group-Object run_id
foreach ($runOutcomes in $outcomesByRun) {
    $outcomeKeys = @($runOutcomes.Group | ForEach-Object {
        if (-not [string]::IsNullOrWhiteSpace(
                [string]$_.native_trajectory_receipt_path)) {
            "trajectory:" + [IO.Path]::GetFullPath(
                [string]$_.native_trajectory_receipt_path)
        }
        elseif (-not [string]::IsNullOrWhiteSpace(
                [string]$_.execution_episode_path)) {
            "episode:" + [IO.Path]::GetFullPath(
                [string]$_.execution_episode_path)
        }
    } | Sort-Object -Unique)
    if ($outcomeKeys.Count -gt 1) {
        throw "Cumulative comparison admissions select multiple outcomes for " +
            "run $($runOutcomes.Name)."
    }
}

$attachmentIndexPath = Join-Path $workPath `
    "money-threshold-comparison-admission-index.json"
Write-JsonFile -Path $attachmentIndexPath -Value ([ordered]@{
    schema_version = "money_threshold_comparison_admission_index.v3"
    admissions = $cumulativeAttachments
    formal_product_training_authorized = $false
})
$allMatched = $attachments.Count -eq $criterionIdsToComplete.Count
$distinctSelectedOutcomeCount = @($attachments |
    Select-Object -ExpandProperty selected_alternative_id -Unique).Count
$hasIncomparableFrontier = @($unmatched | Where-Object {
    [string]$_.reason -eq "authoritative_frontier_not_unique"
}).Count -gt 0
[ordered]@{
    status = if ($allMatched) {
        if ($distinctSelectedOutcomeCount -le 1) {
            "comparison_admissions_ready_for_batch_join"
        } else {
            "criterion_specific_teacher_outcomes_require_distinct_seed_runs"
        }
    } elseif ($hasIncomparableFrontier) {
        "comparison_admissions_blocked_by_incomparable_frontier"
    } else {
        "comparison_admissions_missing_selected_evidence"
    }
    run_id = [string]$plan.run_id
    terminal_request_count = $terminalAlternatives.Count
    comparison_admission_count = $attachments.Count
    cumulative_comparison_admission_count = $cumulativeAttachments.Count
    unmatched_criteria = $unmatched
    terminal_execution_proof_receipt_path = $terminalReceiptPath
    comparison_admission_index_path = $attachmentIndexPath
    formal_product_training_authorized = $false
} | ConvertTo-Json -Depth 32
