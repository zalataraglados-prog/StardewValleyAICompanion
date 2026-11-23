param(
    [Parameter(Mandatory)][string]$ExecutionReceiptPath,
    [Parameter(Mandatory)][string]$BinaryRoot,
    [Parameter(Mandatory)][string]$OutputPath,
    [string]$ProductAfterSnapshotPath
)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $OutputPath) { throw 'Keep the prior replay; choose a new output path.' }
$receipt = Get-Content -LiteralPath $ExecutionReceiptPath -Raw | ConvertFrom-Json
foreach ($name in 'StardewAI.Contracts.dll','StardewAI.Core.dll') {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $BinaryRoot $name))
}
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $BinaryRoot 'StardewAI.GoalConditionedBootstrap.dll'))
if (!('ExistingNativeActionReaderInvocation' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Reflection;
public static class ExistingNativeActionReaderInvocation {
 public static object Read(Assembly assembly, string queue, string receipt, string after, string productAfter) {
  var type = assembly.GetType("StardewAI.GoalConditionedBootstrap.NativeExecutionActionSourceReader", true);
  var method = type.GetMethod("ReadVerifiedProduct", BindingFlags.Static | BindingFlags.NonPublic);
  if (method == null) throw new InvalidOperationException("Pinned original verified-Product reader is missing.");
  return method.Invoke(null, new object[] { queue, receipt, after, productAfter });
 }
}
'@
}
$queue = [string]$receipt.mechanical_compiled_queue_path
if (!$queue) { throw 'Original receipt does not name its actual mechanical queue.' }
$action = [ExistingNativeActionReaderInvocation]::Read(
    $assembly, $queue, [string]$ExecutionReceiptPath, [string]$receipt.after_snapshot_path, $ProductAfterSnapshotPath)
$record = [ordered]@{
    reader='NativeExecutionActionSourceReader.ReadVerifiedProduct'
    reader_assembly=$assembly.Location
    reader_assembly_sha256=(Get-FileHash -LiteralPath $assembly.Location).Hash
    original_native_source_replay_verified=$true
    run_id=$action.Result.RunId;queue_id=$action.Queue.QueueId;queue_item_id=$action.Item.QueueItemId
    option_id=$action.Result.OptionId;native_status=$action.Result.Status
    before_state_hash=$action.Before.StateHash;after_state_hash=$action.After.StateHash
    before_game_tick=$action.Before.GameTick;after_game_tick=$action.After.GameTick
    combat_target_runtime_identity=$action.Result.CombatTargetRuntimeIdentity
    combat_target_defeated=$action.Result.CombatTargetDefeated
    combat_attack_count=$action.Result.CombatAttackCount;combat_hit_count=$action.Result.CombatHitCount
    combat_damage_taken=$action.Result.CombatDamageTaken
    native_changed_facts=$action.Result.ChangedFacts
    source_sha256=$action.SourceSha256
    formal_training_authorized=$false
}
$record | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $OutputPath -Encoding utf8
[pscustomobject]@{original_native_source_replay_verified=$true;option=$action.Result.OptionId;target_defeated=$action.Result.CombatTargetDefeated;source_count=$action.SourceSha256.Count} | ConvertTo-Json
