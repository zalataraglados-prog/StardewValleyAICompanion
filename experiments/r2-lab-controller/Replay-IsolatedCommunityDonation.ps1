param(
    [Parameter(Mandatory)][string]$ExecutionReceiptPath,
    [Parameter(Mandatory)][string]$BinaryRoot,
    [Parameter(Mandatory)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputPath) { throw 'Preserve the prior replay; choose a new output path.' }
foreach ($name in 'StardewAI.Contracts.dll','StardewAI.Core.dll') {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $BinaryRoot $name))
}
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $BinaryRoot 'StardewAI.GoalConditionedBootstrap.dll'))
$result = Get-Content -LiteralPath $ExecutionReceiptPath -Raw | ConvertFrom-Json
$reader = $assembly.GetType('StardewAI.GoalConditionedBootstrap.NativeExecutionActionSourceReader', $true).
    GetMethod('ReadVerifiedProduct', [Reflection.BindingFlags]'NonPublic,Static')
$source = $reader.Invoke($null, [object[]]@(
    [string]$result.mechanical_compiled_queue_path, [string]$ExecutionReceiptPath,
    [string]$result.after_snapshot_path, [string]$result.after_snapshot_path))
$protocol = [StardewAI.Contracts.Execution.CommunityCenterDonationParameterProtocol]
$parseArgs = [object[]]@($source.Item.NormalizedCommand.Parameters, $null)
if (!$protocol.GetMethod('TryParseExecution').Invoke($null, $parseArgs)) { throw 'Original donation protocol rejected its queue.' }
$projection = $parseArgs[1]
$creditType = $assembly.GetType('StardewAI.GoalConditionedBootstrap.CurrentCollectionRequirementCredit', $true)
$credit = [Activator]::CreateInstance($creditType, [object[]]@(
    'community_center', ('community_center:bundle:' + $projection.Binding.BundleDataKey),
    [int]$projection.Binding.BundleIngredientIndex, [string]$projection.QualifiedItemId,
    'native_donation', [int]$projection.Binding.RequiredStack, [int]0, 'consumed_by_native_donation'))
$verifier = $assembly.GetType('StardewAI.GoalConditionedBootstrap.ExactCommunityCenterDonationReceiptVerifier', $true).
    GetMethod('Verify', [Reflection.BindingFlags]'Public,Static')
$evidence = $verifier.Invoke($null, [object[]]@($credit, $source.Item, $source.Before, $source.After))
if (!$evidence.Verified -or $evidence.BlockingReasons.Count) { throw ('Original exact donation verifier rejected source: ' + ($evidence.BlockingReasons -join ',')) }
$report = [ordered]@{
    schema_version='r2_lab_original_exact_donation_replay.v1'
    reader='NativeExecutionActionSourceReader.ReadVerifiedProduct'
    verifier='ExactCommunityCenterDonationReceiptVerifier.Verify'
    reader_and_verifier_assembly=$assembly.Location
    assembly_sha256=(Get-FileHash -LiteralPath $assembly.Location).Hash
    original_native_source_replay_verified=$true
    exact_donation_transition_verified=$evidence.Verified
    blocking_reasons=@($evidence.BlockingReasons)
    requirement_id=$credit.RequirementId
    bundle_id=$projection.BundleId
    ingredient_index=$projection.Binding.BundleIngredientIndex
    qualified_item_id=$projection.QualifiedItemId
    required_quantity=$projection.Binding.RequiredStack
    before_summary=$evidence.BeforeSummary
    after_summary=$evidence.AfterSummary
    run_id=$source.Result.RunId
    before_state_hash=$source.Before.StateHash
    after_state_hash=$source.After.StateHash
    before_game_tick=$source.Before.GameTick
    after_game_tick=$source.After.GameTick
    source_sha256=$source.SourceSha256
    formal_training_authorized=$false
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding utf8
[pscustomobject]@{item=$projection.QualifiedItemId;bundle=$projection.BundleId;native_source_verified=$true;exact_donation_verified=$true}
