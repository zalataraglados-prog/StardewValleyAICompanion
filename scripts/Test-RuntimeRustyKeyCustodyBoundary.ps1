[CmdletBinding()]
param([Parameter(Mandatory)] [string] $OutputRoot)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyCustodyBoundary.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyCustodyRoundTrip.ps1')
$passed = [Collections.Generic.List[string]]::new()
$fixtureSource = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $PSScriptRoot) 'tools/StardewAI.RuntimeTestHarness/ModEntry.MaterialInventoryGraph.cs') -Raw
if ($fixtureSource -notmatch 'Enumerable\.Range\(2, Math\.Max\(0, width - 4\)\)' -or
    $fixtureSource -notmatch 'location\.warps\.FirstOrDefault\(\)' -or
    $fixtureSource -notmatch 'probe\.canBePlacedHere') { throw 'Small-map fixture search must use its own native coordinate space and retain placement checks.' }
$passed.Add('small_map_search_retains_native_placement_check')
$context = [pscustomobject]@{ RunId = 'synthetic-custody-boundary'; SnapshotUrl = 'http://unused.invalid'
    BackendUrl = 'http://unused.invalid'; ExecutorRoot = 'http://unused.invalid'; IsolatedSavesPath = $OutputRoot }
function Get-FreshRuntimeSnapshot {
    param($Url, $TimeoutSeconds)
    $script:captures++
    $nodes = @(@{ node_id = 'player:1'; inventory_kind = 'player_inventory' })
    if ($script:captures -gt 1) {
        $chest = @{ node_id = 'chest:synthetic'; inventory_kind = 'chest'; location_id = 'Blacksmith'; actor_use_authorized = $true
            slots = @(@{ slot_index = 9; qualified_item_id = '(O)390'; stack = 3; quality = 0 }) }
        if ($script:case -eq 'missing-stone') { $chest.slots = @() }
        $nodes += $chest
        if ($script:case -eq 'ambiguous-chest') { $nodes += $chest }
    }
    $value = @{ state_hash = "synthetic-$script:captures"; save_id = @{ value = 'synthetic-save' }; player_id = @{ value = '1' }
        state = @{ farm = @{ material_inventory_graph = @{ status = $(if ($script:case -eq 'unknown-graph') {'unknown'} else {'available'})
            value = @{ status = 'available'; inventory_nodes = $nodes } } } } }
    if ($script:case -eq 'different-save' -and $script:captures -gt 1) { $value.save_id.value = 'foreign-save' }
    return [pscustomobject]@{ Value = $value; Raw = ($value | ConvertTo-Json -Depth 30) }
}
function Invoke-JsonPost {
    param($Url, $Body)
    $script:fixtures++
    if ($Body.option_id -cne 'debug.setup_material_transfer_target' -or $Body.location_id -cne 'Blacksmith' -or
        $Body.fixture_material_transfer_profile -cne 'sparse_source' -or $Body.save_isolation_path -cne $context.IsolatedSavesPath) {
        throw 'Fixture scope or location changed.'
    }
    $value = @{ status = $(if ($script:case -eq 'setup-failed') {'blocked'} else {'applied'}); primitive_verification_status = 'verified' }
    return [pscustomobject]@{ Value = $value; Raw = ($value | ConvertTo-Json) }
}
function Save-RuntimeSnapshotAndIngest {
    param($Path, $Capture, $BackendUrl, $SnapshotProfile)
    Write-Utf8Text -Path $Path -Value $Capture.Raw
}
function Save-RuntimeRustyKeyLatestLedger {
    param($Context, $StateHash, $Path)
    Write-JsonFile -Path $Path -Value @{ revision = $(if ($script:case -eq 'used-ledger') {1} else {0}) }
    return $Path
}
function Invoke-RuntimeRustyKeyLedgerMutation {
    param($Context, $Operation, $Directory, $Name, $Request)
    $script:mutations++
    if ($Operation -cne 'commit' -or $Request.material_claims.Count -ne 1 -or
        $Request.material_claims[0].quantity -ne 3 -or $Request.material_claims[0].slot_index -ne 9 -or
        $Request.material_claims[0].goal_id -cne 'fixture.other_goal_stone') { throw 'Protection did not use one ordinary original commit.' }
    return [pscustomobject]@{ LedgerPath = 'synthetic-protected-ledger' }
}
foreach ($script:case in @('normal','unknown-graph','missing-stone','ambiguous-chest','different-save','setup-failed','used-ledger')) {
    $script:captures = 0; $script:fixtures = 0; $script:mutations = 0
    $rejected = $false
    try {
        $boundary = Initialize-RuntimeRustyKeyCustodyBoundary -Context $context -Directory (Join-Path $OutputRoot $script:case)
        if ($script:case -ne 'normal' -or $boundary.chest_node_id -cne 'chest:synthetic' -or
            $boundary.native_acquisition_performed -or $boundary.formal_training_authorized) { throw 'Invalid boundary result.' }
    } catch { if ($script:case -eq 'normal') { throw }; $rejected = $true }
    if ($script:case -eq 'normal') {
        if ($script:fixtures -ne 1 -or $script:mutations -ne 1) { throw 'Setup was repeated or protection omitted.' }
    } elseif (-not $rejected -or $script:mutations -ne 0) { throw "Invalid fixture reached ledger mutation: $script:case" }
    $passed.Add($script:case)
}
$script:captures = 0; $script:fixtures = 0
$rejected = $false
try { Initialize-RuntimeRustyKeyCustodyBoundary -Context $context -Directory (Join-Path $OutputRoot 'normal') | Out-Null }
catch { $rejected = $true }
if (-not $rejected -or $script:captures -ne 0 -or $script:fixtures -ne 0) { throw 'Existing boundary was replayed.' }
$passed.Add('no_fixture_replay')
$proof = @{ run_id = $context.RunId; custody_reservation_id = 'custody'; protected_reservation_id = 'stone'
    acquisition_receipt_sha256 = ('a' * 64); chest_node_id = 'chest:synthetic' }
foreach ($mutation in @('valid','run','reacquired','no-transfer','no-donation','custody-live','receipt','stone-slot','stone-goal','stone-status')) {
    $ledger = @{ material_reservations = @(
        @{ reservation_id = 'custody'; status = 'completed'; output_custody = @{ fresh_terminal_receipt_sha256 = $proof.acquisition_receipt_sha256 } },
        @{ reservation_id = 'stone'; status = 'active'; node_id = $proof.chest_node_id; slot_index = 1; quantity = 3; goal_id = 'fixture.other_goal_stone' }) }
    $rolling = @{ run_id = $context.RunId; steps = @(@{kind='custody_transfer'},@{kind='held_donation'})
        last_ledger_path = (Join-Path $OutputRoot "$mutation-final-ledger.json") }
    switch ($mutation) {
        'run' { $rolling.run_id = 'foreign-run' }
        'reacquired' { $rolling.steps += @{kind='terminal_acquisition'} }
        'no-transfer' { $rolling.steps = @(@{kind='held_donation'}) }
        'no-donation' { $rolling.steps = @(@{kind='custody_transfer'}) }
        'custody-live' { $ledger.material_reservations[0].status = 'active' }
        'receipt' { $ledger.material_reservations[0].output_custody.fresh_terminal_receipt_sha256 = 'foreign' }
        'stone-slot' { $ledger.material_reservations[1].slot_index = 9 }
        'stone-goal' { $ledger.material_reservations[1].goal_id = 'wrong-goal' }
        'stone-status' { $ledger.material_reservations[1].status = 'completed' }
    }
    Write-JsonFile -Path $rolling.last_ledger_path -Value $ledger
    $rejected = $false
    try { Assert-RuntimeRustyKeyCustodyRoundTrip -Rolling $rolling -Proof $proof }
    catch { $rejected = $true }
    if ($rejected -ne ($mutation -ne 'valid')) { throw "Round-trip scope guard failed: $mutation" }
    $passed.Add("proof_$mutation")
}
$sourceInput = Join-Path $OutputRoot 'source with spaces.json'
Write-Utf8Text -Path $sourceInput -Value '{}'
$parserContext = [pscustomobject]@{ RunId = $context.RunId; SnapshotUrl = $context.SnapshotUrl; BackendUrl = $context.BackendUrl
    ExecutorRoot = $context.ExecutorRoot; IsolatedSavesPath = (Join-Path $OutputRoot 'never-created-isolation'); ArtifactDirectory = $OutputRoot }
$inputs = @{}
foreach ($name in @('RequirementInventoryPath','AcquisitionLoweringPath','MasterAnglerWindowsPath','RouteTimingPath','FishingForecastManifestPath')) {
    $inputs[$name] = $sourceInput
}
$arguments = @(Get-RuntimeRustyKeyCustodyPrefixArguments -Context $parserContext -ProjectRoot (Split-Path -Parent $PSScriptRoot) -AcquisitionInputs $inputs)
$parseError = Join-Path $OutputRoot 'actual-prefix-argument-binding.stderr.log'
& pwsh @arguments 2> $parseError | Out-Null
if ($LASTEXITCODE -ne 1 -or (Get-Content -LiteralPath $parseError -Raw) -notlike '*Isolated save directory does not exist.*') {
    throw 'Actual child-process argument binding did not reach the intended preflight without HTTP/native execution.'
}
$global:LASTEXITCODE = 0
$passed.Add('actual_child_parameter_binding_with_spaced_source_paths')
Write-JsonFile -Path (Join-Path $OutputRoot 'summary.json') -Value ([ordered]@{
    status = 'passed'; passed_count = $passed.Count; passed = $passed.ToArray(); native_game_run = $false
    scope = 'synthetic_setup_transport_and_final_scope_guards_only'; formal_training_authorized = $false
})
Write-Output "Custody boundary checks passed: $($passed.Count); no game launched."
