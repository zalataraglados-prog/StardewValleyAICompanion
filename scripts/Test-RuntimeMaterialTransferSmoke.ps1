[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string] $OutputRoot = (Join-Path ([IO.Path]::GetTempPath()) ('material-transfer-check-' + [guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeMaterialTransferSmoke.ps1')
$loop = Join-Path $ProjectRoot 'tools/StardewAI.LiveTrainingLoop/bin/Release/net8.0/StardewAI.LiveTrainingLoop.dll'
$core = Join-Path (Split-Path -Parent $loop) 'StardewAI.Core.dll'
$loopAssembly = [Reflection.Assembly]::LoadFrom($loop)
$coreAssembly = [Reflection.Assembly]::LoadFrom($core)
$passed = [Collections.Generic.List[string]]::new()
New-Item -ItemType Directory -Path $OutputRoot -Force | Out-Null
function Assert-Check([bool]$Value, [string]$Message) { if (-not $Value) { throw $Message } }
function Assert-Rejected([string]$Name, [scriptblock]$Action, [string]$Message = '') {
    $rejected = $false
    try { & $Action | Out-Null }
    catch { $rejected = [string]::IsNullOrEmpty($Message) -or $_.Exception.ToString().Contains($Message) }
    Assert-Check $rejected ("Expected rejection: $Name")
    $passed.Add($Name)
}

$scriptPath = Join-Path $PSScriptRoot 'Invoke-RuntimeMaterialTransferDailyPlanSmoke.ps1'
foreach ($file in @($scriptPath,(Join-Path $PSScriptRoot 'lib/RuntimeMaterialTransferSmoke.ps1'),(Join-Path $PSScriptRoot 'lib/RuntimeLogicalJson.ps1'))) {
    $tokens = $null
    $errors = $null
    [Management.Automation.Language.Parser]::ParseFile($file,[ref]$tokens,[ref]$errors) | Out-Null
    Assert-Check ($errors.Count -eq 0) ('PowerShell parse failed: ' + $file)
}
$passed.Add('actual_scripts_parse')
$base = @{RuntimeRoot=(Join-Path $OutputRoot 'no-game-runtime');OutputDirectory=$OutputRoot;RunId='preflight';NoBuild=$true}
Assert-Rejected 'explicit_source_required' { & $scriptPath @base } 'An explicit source SaveSlot is required'
Assert-Rejected 'source_slot_name_only' { & $scriptPath @base -SaveSlot '../outside' } 'single source directory name'
Assert-Rejected 'owned_cleanup_required' { & $scriptPath @base -SaveSlot missing -KeepGameRunning } 'KeepGameRunning is not supported'
Assert-Rejected 'partial_target_rejected' { & $scriptPath @base -SaveSlot missing -TargetTileX 2 } 'must both be supplied or omitted'
Assert-Rejected 'sparse_single_click_rejected' { & $scriptPath @base -SaveSlot missing -FixtureProfile sparse_source -TransferQuantity 1 } 'partial multi-unit transfer'
Assert-Rejected 'unsupported_profile_rejected' { & $scriptPath @base -SaveSlot missing -FixtureProfile invented }
Assert-Rejected 'missing_source_no_launch' { & $scriptPath @base -SaveSlot missing } 'Source save slot is missing'
$fixtureRoot = Join-Path $OutputRoot 'copy-preflight-runtime'
$sourceSave = Join-Path $fixtureRoot 'saves/Fixture_1'
Write-Utf8Text -Path (Join-Path $sourceSave 'Fixture_1') -Value '<SaveGame />'
Assert-Rejected 'nested_copy_target_rejected_before_launch' {
    & $scriptPath -RuntimeRoot $fixtureRoot -SaveSlot Fixture_1 -OutputDirectory $sourceSave -RunId nested -NoBuild
} 'Artifacts must not be nested under the source save'
New-Item -ItemType Directory -Path (Join-Path $OutputRoot 'preflight') | Out-Null
Assert-Rejected 'existing_run_no_replay' { & $scriptPath @base -SaveSlot missing } 'Existing run cannot be replayed'

$plain = Join-Path $OutputRoot 'raw-source.json'
$raw = '{"schema_version":"snapshot.v1","state":{"player":{"value":1.0},"time":{"value":"2026-11-04T09:00:00+08:00"}}}'
Write-Utf8Text -Path $plain -Value $raw
Assert-Check ((Read-RuntimeLogicalJsonArtifact -Path $plain -Raw) -ceq $raw) 'Plain raw mode changed JSON text.'
$passed.Add('plain_raw_lexemes_preserved')
$manifest = Join-Path $OutputRoot 'stored/source.json'
$task = [StardewAI.LiveTrainingLoop.ContentAddressedJsonArtifactStore]::WriteAsync(
    [string]$manifest,[string]$raw,'content_addressed_gzip',[Threading.CancellationToken]::None)
$task.GetAwaiter().GetResult() | Out-Null
$expected = [StardewAI.Core.Infrastructure.ContentAddressedJsonArtifactReader]::ReadAllText($manifest)
Assert-Check ((Read-RuntimeLogicalJsonArtifact -Path $manifest -CoreAssemblyPath $core -Raw) -ceq $expected) 'Manifest raw mode changed verified logical text.'
$passed.Add('manifest_raw_uses_original_verified_reader')
Assert-Check ((Read-RuntimeLogicalJsonArtifact -Path $manifest -CoreAssemblyPath $core).state.player.value -eq 1) 'Default object mode regressed.'
$passed.Add('default_object_mode_preserved')
$blob = Get-ChildItem -LiteralPath (Join-Path $OutputRoot 'stored/_snapshot-blobs') -File -Recurse -Filter '*.json.gz' | Select-Object -First 1
[IO.File]::WriteAllBytes($blob.FullName,[byte[]]@(1,2,3))
Assert-Rejected 'raw_mode_still_rejects_corrupt_blob' { Read-RuntimeLogicalJsonArtifact -Path $manifest -CoreAssemblyPath $core -Raw }

function New-CheckSlot([int]$Index,[string]$Qid,[int]$Stack,[int]$Quality) {
    return @{slot_index=$Index;qualified_item_id=$Qid;stack=$Stack;quality=$Quality;maximum_stack_size=999;runtime_type='StardewValley.Object'}
}
$before = @{schema_version='material_inventory_graph.v1';status='available';player_id=42
    inventory_nodes=@(
        @{node_id='player';inventory_kind='player_inventory';supply_state='available';actor_use_authorized=$true;owner_player_id=42;location_id='Farm';capacity=12;slots=@()},
        @{node_id='chest';inventory_kind='chest';supply_state='available';actor_use_authorized=$true;owner_player_id=42;location_id='Farm';capacity=36
            slots=@((New-CheckSlot 5 '(O)388' 11 4),(New-CheckSlot 9 '(O)390' 3 0))}
    );access_points=@(@{node_id='chest';access_kind='placed_chest';special_chest_type='None';locked_by_other_player=$false})
}
$after = $before | ConvertTo-Json -Depth 30 | ConvertFrom-Json
$after.inventory_nodes[0].slots = @((New-CheckSlot 0 '(O)388' 2 4))
$after.inventory_nodes[1].slots = @((New-CheckSlot 0 '(O)388' 9 4),(New-CheckSlot 1 '(O)390' 3 0))
$intent = @{schema_version='material_transfer_intent.v1';source_node_id='chest';destination_node_id='player';source_slot_index=5
    qualified_item_id='(O)388';quality=4;quantity=2;expected_source_stack=11}
$projector = $coreAssembly.GetType('StardewAI.Core.Execution.MaterialTransferProjector')
$method = $projector.GetMethod('Project')
$types = @($method.GetParameters().ParameterType)
$options = [System.Text.Json.JsonSerializerOptions]::new()
$options.PropertyNamingPolicy = [System.Text.Json.JsonNamingPolicy]::SnakeCaseLower
$arguments = [object[]]::new(2)
$arguments[0] = [System.Text.Json.JsonSerializer]::Deserialize(($before | ConvertTo-Json -Depth 30),$types[0],$options)
$arguments[1] = [System.Text.Json.JsonSerializer]::Deserialize(($intent | ConvertTo-Json -Depth 30),$types[1],$options)
$projected = $method.Invoke([Activator]::CreateInstance($projector),$arguments)
Assert-Check ($projected.Status -ceq 'projected') ('Actual source projection failed: ' + ($projected.BlockingReasons -join ','))
$recorded = [System.Text.Json.JsonSerializer]::Serialize($projected,$projected.GetType(),$options) | ConvertFrom-Json
Assert-RuntimeMaterialNativeLineage -BeforeGraph $before -AfterGraph $after -Intent $intent -RecordedProjection $recorded -CoreAssemblyPath $core
$passed.Add('original_projector_and_verifier_accept_sparse_lineage')
$altered = $after | ConvertTo-Json -Depth 30 | ConvertFrom-Json
$altered.inventory_nodes[1].slots[1].stack = 4
Assert-Rejected 'unrelated_stock_drift_rejected' { Assert-RuntimeMaterialNativeLineage $before $altered $intent $recorded $core }
$altered = $after | ConvertTo-Json -Depth 30 | ConvertFrom-Json
$altered.inventory_nodes[1].slots[1].slot_index = 9
Assert-Rejected 'unrelated_slot_drift_rejected' { Assert-RuntimeMaterialNativeLineage $before $altered $intent $recorded $core }
$altered = $after | ConvertTo-Json -Depth 30 | ConvertFrom-Json
$altered.inventory_nodes[1].owner_player_id = 43
Assert-Rejected 'owner_drift_rejected' { Assert-RuntimeMaterialNativeLineage $before $altered $intent $recorded $core }
$tampered = $recorded | ConvertTo-Json -Depth 30 | ConvertFrom-Json
$tampered.source_stack_after = 10
Assert-Rejected 'recorded_projection_tampering_rejected' { Assert-RuntimeMaterialNativeLineage $before $after $intent $tampered $core }
$wrong = $intent.Clone()
$wrong.expected_source_stack = 10
Assert-Rejected 'wrong_source_intent_rejected' { Assert-RuntimeMaterialNativeLineage $before $after $wrong $recorded $core }
Write-JsonFile -Path (Join-Path $OutputRoot 'summary.json') -Value @{status='passed';passed_count=$passed.Count;cases=$passed.ToArray();native_game_run=$false;formal_training_authorized=$false}
Write-Host "Material transfer checks passed: $($passed.Count); no game launched."
