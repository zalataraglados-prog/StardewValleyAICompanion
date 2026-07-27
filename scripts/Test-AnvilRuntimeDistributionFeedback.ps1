param(
    [Parameter(Mandatory)]
    $LoadItem,
    [Parameter(Mandatory)]
    $LoadExecution,
    [Parameter(Mandatory)]
    [string] $DatasetPath,
    [Parameter(Mandatory)]
    $AdditionalItems,
    [Parameter(Mandatory)]
    $AdditionalCountsBefore,
    [Parameter(Mandatory)]
    $AdditionalCountsAfter,
    [Parameter(Mandatory)]
    [string] $RunDirectory
)

$ErrorActionPreference = "Stop"

function Read-QueueParameter {
    param($QueueItem, [string] $Name)
    foreach ($parameter in @(
        $QueueItem.normalized_command.parameters)) {
        if ([string]$parameter.name -eq $Name) {
            return [string]$parameter.value
        }
    }
    return ""
}

function Read-NamedFeature {
    param($Features, [string] $Name)
    foreach ($feature in @($Features)) {
        if ([string]$feature.name -eq $Name) {
            return $feature.value
        }
    }
    return $null
}

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value |
        ConvertTo-Json -Depth 96 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

$trainingKind = Read-QueueParameter `
    -QueueItem $LoadItem `
    -Name "machine_prediction_training_kind"
$fingerprint = Read-QueueParameter `
    -QueueItem $LoadItem `
    -Name "machine_prediction_contract_fingerprint"
$outcomeKind = Read-QueueParameter `
    -QueueItem $LoadItem `
    -Name "machine_output_distribution_outcome_kind"
$utilityMetric = Read-QueueParameter `
    -QueueItem $LoadItem `
    -Name "anvil_reforge_utility_metric"
$currentUtility = [double](Read-QueueParameter `
    -QueueItem $LoadItem `
    -Name "anvil_reforge_current_utility")
$expectedUtility = [double](Read-QueueParameter `
    -QueueItem $LoadItem `
    -Name "anvil_reforge_expected_utility")
$expectedDelta = [double](Read-QueueParameter `
    -QueueItem $LoadItem `
    -Name "anvil_reforge_expected_utility_delta")
$improvementProbability = [double](Read-QueueParameter `
    -QueueItem $LoadItem `
    -Name "anvil_reforge_improvement_probability")
$loadoutStatus = Read-QueueParameter `
    -QueueItem $LoadItem `
    -Name "anvil_reforge_loadout_status"
$capabilityClass = Read-QueueParameter `
    -QueueItem $LoadItem `
    -Name "anvil_reforge_capability_class"
$loadoutRelation = Read-QueueParameter `
    -QueueItem $LoadItem `
    -Name "anvil_reforge_loadout_relation"
$goalDemandStatus = Read-QueueParameter `
    -QueueItem $LoadItem `
    -Name "anvil_reforge_goal_demand_status"
$goalFamily = Read-QueueParameter `
    -QueueItem $LoadItem `
    -Name "anvil_reforge_goal_family"
$effectiveDemandScore = [double](Read-QueueParameter `
    -QueueItem $LoadItem `
    -Name "anvil_reforge_effective_demand_score")
$realizedUtility =
    [double]$LoadExecution.anvil_reforge_realized_utility
$realizedDelta =
    [double]$LoadExecution.anvil_reforge_realized_utility_delta
$realizedOutcomeJson =
    [string]$LoadExecution.anvil_reforge_realized_outcome_json
$realizedOutcome = if (
    [string]::IsNullOrWhiteSpace(
        $realizedOutcomeJson)) {
    $null
}
else {
    $realizedOutcomeJson | ConvertFrom-Json
}

$fingerprintValid =
    $fingerprint -match "^[0-9a-f]{64}$"
$utilityMathValid =
    [Math]::Abs(
        $expectedUtility -
        $currentUtility -
        $expectedDelta) -le 0.00000001 -and
    [Math]::Abs(
        $realizedUtility -
        $currentUtility -
        $realizedDelta) -le 0.00000001
if ($trainingKind -ne "complete_distribution" -or
    -not $fingerprintValid -or
    [string]::IsNullOrWhiteSpace($outcomeKind) -or
    [string]::IsNullOrWhiteSpace($utilityMetric) -or
    $loadoutStatus -ne "exact_live_trinket_loadout" -or
    [string]::IsNullOrWhiteSpace($capabilityClass) -or
    [string]::IsNullOrWhiteSpace($loadoutRelation) -or
    [string]::IsNullOrWhiteSpace($goalDemandStatus) -or
    [string]::IsNullOrWhiteSpace($goalFamily) -or
    $effectiveDemandScore -lt -1 -or
    $effectiveDemandScore -gt 1 -or
    $improvementProbability -lt 0 -or
    $improvementProbability -gt 1 -or
    -not $utilityMathValid -or
    $null -eq $realizedOutcome -or
    [string]$LoadExecution.machine_output_distribution_outcome_kind -ne
        $outcomeKind -or
    [string]$LoadExecution.anvil_reforge_utility_metric -ne
        $utilityMetric) {
    Write-JsonFile `
        (Join-Path $RunDirectory "anvil-feedback-rejected.json") `
        $LoadExecution
    throw (
        "Compiled Anvil distribution contract and " +
        "native realized feedback did not agree.")
}

$datasetRows = @(
    Get-Content -LiteralPath $DatasetPath |
    Where-Object {
        -not [string]::IsNullOrWhiteSpace($_)
    } |
    ForEach-Object {
        $_ | ConvertFrom-Json
    })
$matchingRows = @(
    $datasetRows |
    Where-Object {
        @($_.action_features.option_ids) -contains
            "executor.load_machine_input" -and
        [string]$_.action_features.training_role -eq
            "strategy_value"
    })
if ($matchingRows.Count -ne 1) {
    throw (
        "Expected exactly one Anvil strategy-value " +
        "dataset row; found " +
        $matchingRows.Count)
}

$datasetRow = $matchingRows[0]
$numericFeatures =
    $datasetRow.action_features.features.numeric
$categoricalFeatures =
    $datasetRow.action_features.features.categorical
$rowRealizedDelta = [double](
    Read-NamedFeature `
        -Features $numericFeatures `
        -Name "machine.anvil.reforge.realized_utility_delta")
$rowOutcomeKind = [string](
    Read-NamedFeature `
        -Features $categoricalFeatures `
        -Name "machine.anvil.reforge.outcome_kind")
$rowUtilityMetric = [string](
    Read-NamedFeature `
        -Features $categoricalFeatures `
        -Name "machine.anvil.reforge.utility_metric")
$rowCapabilityClass = [string](
    Read-NamedFeature `
        -Features $categoricalFeatures `
        -Name "machine.anvil.reforge.capability_class")
$rowLoadoutRelation = [string](
    Read-NamedFeature `
        -Features $categoricalFeatures `
        -Name "machine.anvil.reforge.loadout_relation")
$rowGoalDemandStatus = [string](
    Read-NamedFeature `
        -Features $categoricalFeatures `
        -Name "machine.anvil.reforge.goal_demand_status")
$rowGoalFamily = [string](
    Read-NamedFeature `
        -Features $categoricalFeatures `
        -Name "machine.anvil.reforge.goal_family")
$rowEffectiveDemandScore = [double](
    Read-NamedFeature `
        -Features $numericFeatures `
        -Name "machine.anvil.reforge.effective_demand_score")
if ([string]$datasetRow.action_features.learning_scope -ne
        "policy_ranker" -or
    [bool]$datasetRow.action_features.exclude_from_policy_training -or
    [Math]::Abs(
        [double]$datasetRow.labels.total_reward -
        $realizedDelta) -gt 0.00000001 -or
    [Math]::Abs(
        $rowRealizedDelta -
        $realizedDelta) -gt 0.00000001 -or
    $rowOutcomeKind -ne $outcomeKind -or
    $rowUtilityMetric -ne $utilityMetric -or
    $rowCapabilityClass -ne $capabilityClass -or
    $rowLoadoutRelation -ne $loadoutRelation -or
    $rowGoalDemandStatus -ne $goalDemandStatus -or
    $rowGoalFamily -ne $goalFamily -or
    [Math]::Abs(
        $rowEffectiveDemandScore -
        $effectiveDemandScore) -gt 0.00000001) {
    Write-JsonFile `
        (Join-Path $RunDirectory "anvil-dataset-row-rejected.json") `
        $datasetRow
    throw (
        "Anvil native feedback was not attributed " +
        "to the strategy-value dataset row.")
}

foreach ($additionalItem in @($AdditionalItems)) {
    $additionalId =
        [string]$additionalItem.qualified_item_id
    $requiredCount =
        [int]$additionalItem.quantity
    if ([int]$AdditionalCountsBefore[$additionalId] -
        [int]$AdditionalCountsAfter[$additionalId] -ne
        $requiredCount) {
        throw (
            "Additional Anvil input $additionalId " +
            "did not change by $requiredCount.")
    }
}

[pscustomobject]@{
    Verified = $true
    RealizedUtilityDelta = $realizedDelta
    RealizedOutcomeJson = $realizedOutcomeJson
    DatasetTrainingRole =
        [string]$datasetRow.action_features.training_role
    CapabilityClass = $capabilityClass
    LoadoutRelation = $loadoutRelation
    GoalDemandStatus = $goalDemandStatus
    GoalFamily = $goalFamily
    EffectiveDemandScore = $effectiveDemandScore
}
