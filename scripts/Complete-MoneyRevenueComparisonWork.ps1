[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $WorkDirectory,
    [Parameter(Mandatory)]
    [string] $TerminalProofIndex,
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
$attachments = @()
$unmatched = @()
foreach ($criterionId in @($plan.crossed_criterion_ids)) {
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
    $saleArtifacts = if ($selected.Count -eq 1) {
        @($selected[0].native_terminal_artifacts | Where-Object {
            [string]$_.role -eq "shop_sale_episode"
        })
    } else { @() }
    $selectedRequiresRollout = $selected.Count -eq 1 -and
        [bool]$selected[0].acquisition_rollout_required
    $hasSupportedEpisode =
        [bool]$receipt.production_teacher_preference_label_eligible -and
        -not $selectedRequiresRollout -and
        $saleArtifacts.Count -eq 1
    if (-not $hasSupportedEpisode) {
        $unmatched += [ordered]@{
            criterion_id = [string]$criterionId
            selected_alternative_id = $selectedId
            reason = if (-not [bool]$receipt.production_teacher_preference_label_eligible) {
                "authoritative_frontier_not_unique"
            } elseif ($selectedRequiresRollout) {
                "teacher_selected_rollout_outcome_not_supported_by_money_corpus"
            } else {
                "teacher_selected_shipping_outcome_not_supported_by_money_corpus"
            }
        }
        continue
    }
    $selectedEpisodePath = [IO.Path]::GetFullPath(
        [string]$saleArtifacts[0].path)

    $verificationRequestPath = Join-Path $criterionRoot `
        "money-threshold-comparison-verification-corpus-request.json"
    $verificationCorpusPath = Join-Path $criterionRoot `
        "money-threshold-comparison-verification-corpus.json"
    Write-JsonFile -Path $verificationRequestPath -Value ([ordered]@{
        schema_version = "money_threshold_teacher_corpus_request.v3"
        corpus_id = ([string]$plan.run_id) + "-" + [string]$criterionId
        sources = @([ordered]@{
            source_id = [string]$plan.run_id
            execution_episode_path = $selectedEpisodePath
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
        execution_episode_path = $selectedEpisodePath
    }
}

$attachmentIndexPath = Join-Path $workPath `
    "money-threshold-comparison-admission-index.json"
Write-JsonFile -Path $attachmentIndexPath -Value ([ordered]@{
    schema_version = "money_threshold_comparison_admission_index.v2"
    admissions = $attachments
    formal_product_training_authorized = $false
})
$allMatched = $attachments.Count -eq @($plan.crossed_criterion_ids).Count
$distinctSelectedEpisodeCount = @($attachments |
    Select-Object -ExpandProperty execution_episode_path -Unique).Count
$hasIncomparableFrontier = @($unmatched | Where-Object {
    [string]$_.reason -eq "authoritative_frontier_not_unique"
}).Count -gt 0
[ordered]@{
    status = if ($allMatched) {
        if ($distinctSelectedEpisodeCount -le 1) {
            "comparison_admissions_ready_for_batch_join"
        } else {
            "criterion_specific_teacher_outcomes_require_distinct_seed_runs"
        }
    } elseif ($hasIncomparableFrontier) {
        "comparison_admissions_blocked_by_incomparable_frontier"
    } else {
        "teacher_selected_outcome_not_supported_by_money_corpus"
    }
    run_id = [string]$plan.run_id
    terminal_request_count = $terminalAlternatives.Count
    comparison_admission_count = $attachments.Count
    unmatched_criteria = $unmatched
    terminal_execution_proof_receipt_path = $terminalReceiptPath
    comparison_admission_index_path = $attachmentIndexPath
    formal_product_training_authorized = $false
} | ConvertTo-Json -Depth 32
