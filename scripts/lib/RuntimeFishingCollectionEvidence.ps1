function Read-RuntimeFishingObservedCatch {
    param([string] $OptionId, $Facts)
    if ($OptionId -eq "executor.catch_fish") {
        $value = @($Facts) | Where-Object path -eq "fishing.caught_qualified_item_id" |
            Select-Object -ExpandProperty after -First 1
        if ($null -eq $value) { return "" }
        return [string]$value
    }
    if ($OptionId -ne "executor.collect_crab_pot") { return "" }
    $collectionFacts = @($Facts | Where-Object { $_.path -match '^player\.fish_caught\[\(O\)[^\]]+\]$' })
    if ($collectionFacts.Count -ne 1) { return "" }
    $fact = $collectionFacts[0]
    if ([string]$fact.before -notmatch '^(\d+),\d+$') { return "" }
    $before = [long]$Matches[1]
    if ([string]$fact.after -notmatch '^(\d+),\d+$' -or [long]$Matches[1] -le $before) { return "" }
    return ([string]$fact.path).Substring('player.fish_caught['.Length).TrimEnd(']')
}

function Build-RuntimeMasterAnglerCatchSource {
    param(
        [string] $BootstrapDll, [string] $OutputRoot, [string] $RunId,
        [string] $RequirementInventory, [string] $AcquisitionLowering, $Step
    )
    $requestPath = Join-Path $OutputRoot "native-catch-request.json"
    $receiptPath = Join-Path $OutputRoot "native-catch-receipt.json"
    $replayPath = Join-Path $OutputRoot "native-catch-replay.json"
    Write-JsonFile $requestPath ([ordered]@{
        schema_version = "master_angler_native_catch_request.v1"
        run_id = $RunId
        requirement_inventory_path = $RequirementInventory
        acquisition_lowering_path = $AcquisitionLowering
        action_queue_path = $Step.mechanical_compiled_queue_path
        execution_receipt_path = $Step.execution_path
        after_snapshot_path = $Step.after_snapshot_path
    })
    & dotnet $BootstrapDll build-master-angler-native-catch-receipt --request $requestPath --output $receiptPath | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Original native catch source builder rejected the recorded action." }
    & dotnet $BootstrapDll verify-master-angler-native-catch-receipt --receipt $receiptPath --output $replayPath | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Original native catch source Verify rejected the recorded action." }
    if ((Get-FileHash -LiteralPath $receiptPath).Hash -cne (Get-FileHash -LiteralPath $replayPath).Hash) {
        throw "Original native catch source build and Verify differ."
    }
    return $receiptPath
}

function Initialize-RuntimeCrabPotCaptureBoundary {
    param(
        [string] $RunId, [string] $SavePath, [string] $SnapshotUrl, $Snapshot,
        [string] $QualifiedItemId, [int] $TileX, [int] $TileY, [string] $OutputRoot,
        [ValidateSet("primary", "counterfactual")] [string] $Identity
    )
    foreach ($stage in @("legal-tile", "ready")) {
        $request = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-fishing-fixture"
            queue_item_id = "runtime-fishing-fixture.crab-pot.$Identity.$stage"
            before_state_hash = $Snapshot.state_hash
            option_id = if ($stage -eq "legal-tile") { "debug.setup_crab_pot_placement_target" } else { "debug.setup_crab_pot_target" }
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $SavePath
            target_tile_x = $TileX
            target_tile_y = $TileY
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
        }
        if ($stage -eq "ready") {
            $request.qualified_item_id = $QualifiedItemId
            $request.quantity = 1
        }
        $result = Invoke-JsonPost -Url "http://127.0.0.1:8767/api/v1/training/execute" -Body $request
        Write-JsonFile (Join-Path $OutputRoot "crab-pot-$Identity-$stage-result.json") $result
        if ($result.status -ne "applied" -or $result.primitive_verification_status -ne "verified") {
            throw "Existing crab-pot $stage fixture failed: $(@($result.block_reasons) -join ',')"
        }
        if ($stage -eq "legal-tile") {
            if ($null -eq $result.target_tile_x -or $null -eq $result.target_tile_y) {
                throw "Existing crab-pot placement fixture omitted the native-legal reachable water tile."
            }
            $TileX = [int]$result.target_tile_x
            $TileY = [int]$result.target_tile_y
        }
        $Snapshot = Wait-WorldSnapshot -Url ($SnapshotUrl + "&fresh=1") -TimeoutSeconds 60
    }
    return [pscustomobject]@{ TileX = $TileX; TileY = $TileY; Snapshot = $Snapshot }
}
