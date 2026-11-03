function Initialize-RuntimeRustyKeyGeodeBoundary {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)] $Context,
        [Parameter(Mandatory)] [string] $Directory,
        [string] $GeodeQualifiedItemId = '(O)275',
        [ValidateRange(0, 2147483647)] [int] $GeodesCrackedBefore = 42
    )
    function Assert-BoundaryIdentity($Snapshot) {
        if ($Snapshot.game_version -cne '1.6.15' -or
            $Snapshot.save_id.status -cne 'available' -or $Snapshot.player_id.status -cne 'available' -or
            [string]::IsNullOrWhiteSpace($identity[0]) -or [string]::IsNullOrWhiteSpace($identity[1]) -or
            [string]$Snapshot.save_id.value -cne $identity[0] -or [string]$Snapshot.player_id.value -cne $identity[1]) {
            throw 'Geode boundary save/actor identity is missing, unreadable or changed.'
        }
    }

    function Invoke-BoundarySetup([string] $Name, [string] $Option, $Parameters) {
        $body = [ordered]@{
            schema_version = 'training_execution_request.v1'; run_id = $Context.RunId
            queue_id = "$($Context.RunId).fixture"; queue_item_id = "$($Context.RunId).fixture.$Name"
            before_state_hash = $capture.Value.state_hash; option_id = $Option
            execution_mode = 'training_singleplayer'; actor = 'training_farmer.main'
            save_isolation_path = $Context.IsolatedSavesPath; request_nonce = [guid]::NewGuid().ToString('N')
            created_at = [DateTimeOffset]::UtcNow.ToString('O')
        }
        foreach ($key in $Parameters.Keys) { $body[$key] = $Parameters[$key] }
        Write-JsonFile -Path (Join-Path $Directory "$Name-request.json") -Value $body
        $result = Invoke-JsonPost -Url "$($Context.ExecutorRoot)/api/v1/training/execute" -Body $body
        Write-Utf8Text -Path (Join-Path $Directory "$Name-result.json") -Value $result.Raw
        if ($result.Value.status -cne 'applied' -or $result.Value.primitive_verification_status -cne 'verified') {
            throw "Geode boundary setup rejected: $Name"
        }
        $next = Get-FreshRuntimeSnapshot -Url $Context.SnapshotUrl -TimeoutSeconds 60
        Write-Utf8Text -Path (Join-Path $Directory "$Name-snapshot.json") -Value $next.Raw
        Assert-BoundaryIdentity $next.Value
        return $next
    }

    function Get-ExactDonatableGeodeInput($Snapshot) {
        foreach ($field in @($Snapshot.state.player.geode_processing, $Snapshot.state.player.inventory,
                $Snapshot.state.world_progress.museum)) {
            if ($field.status -cne 'available') { throw 'Geode boundary source field is unreadable.' }
        }
        $projection = $Snapshot.state.player.geode_processing.value
        $inputs = @($projection.inventory_inputs | Where-Object { $_.qualified_item_id -ceq $GeodeQualifiedItemId })
        if ($projection.projection_status -cne 'complete_locked_base_1.6.15' -or $projection.base_service_status -cne 'ready' -or
            $inputs.Count -ne 1 -or $inputs[0].status -cne 'available' -or $inputs[0].kind -cne 'exact' -or
            $inputs[0].expected_output.inventory_persists -isnot [bool] -or -not $inputs[0].expected_output.inventory_persists -or
            $inputs[0].expected_output.stack -le 0) { throw 'Boundary needs one exact, persistent current native geode output.' }
        $outputId = [string]$inputs[0].expected_output.qualified_item_id
        if (@($Snapshot.state.world_progress.museum.value.donatable_items | Where-Object {
                $_.qualified_item_id -ceq $outputId
            }).Count -ne 1 -or @($inputs[0].authoritative_route_sources | Where-Object {
                $_.route_kind -ceq 'native_geode_drop' -and $_.qualified_item_id -ceq $outputId -and
                -not [string]::IsNullOrWhiteSpace([string]$_.source_id)
            }).Count -eq 0) { throw 'Native geode output has no exact museum/source binding.' }
        return $inputs[0]
    }

    if (Test-Path -LiteralPath $Directory) { throw 'Geode boundary already exists; refusing fixture replay.' }
    if ([string]::IsNullOrWhiteSpace($Context.IsolatedSavesPath)) { throw 'An explicit isolated save path is required.' }
    New-Item -ItemType Directory -Path $Directory | Out-Null
    $capture = Get-FreshRuntimeSnapshot -Url $Context.SnapshotUrl -TimeoutSeconds 60
    $identity = @([string]$capture.Value.save_id.value, [string]$capture.Value.player_id.value)
    Assert-BoundaryIdentity $capture.Value
    Write-Utf8Text -Path (Join-Path $Directory 'initial-snapshot.json') -Value $capture.Raw
    $geodeSetup = [ordered]@{
        geode_qualified_item_id = $GeodeQualifiedItemId; geode_stack_before = 2; geode_money_before = 1000
        geodes_cracked_before = $GeodesCrackedBefore; mystery_boxes_opened_before = 0
        golden_coconut_cracked_before = $false; geode_got_mystery_book_mail_before = $true
        geode_artifact_found_mail_before = $false
    }
    $capture = Invoke-BoundarySetup 'geode-probe' 'debug.setup_geode_processing' $geodeSetup
    $inputBefore = Get-ExactDonatableGeodeInput $capture.Value
    $targetQualifiedId = [string]$inputBefore.expected_output.qualified_item_id
    $capture = Invoke-BoundarySetup 'museum' 'debug.setup_museum_donation' ([ordered]@{
        expected_donated_count_before = 59; inventory_slot_index = 11
        qualified_item_id = $targetQualifiedId; field_guide_quest_present_before = $false
    })
    # This clears the museum fixture's donated-item inventory before measurement.
    $capture = Invoke-BoundarySetup 'geode-restore' 'debug.setup_geode_processing' $geodeSetup
    $inputAfter = Get-ExactDonatableGeodeInput $capture.Value
    if ($inputAfter.expected_output.qualified_item_id -cne $targetQualifiedId -or
        $inputAfter.stack_before -ne 2 -or $inputAfter.slot_index -ne 0 -or
        $capture.Value.state.player.geode_processing.value.geodes_cracked_before -ne $GeodesCrackedBefore) {
        throw 'Native geode output or restored input drifted during boundary setup.'
    }
    Assert-RustyKeySnapshot -Snapshot $capture.Value -DonatedCount 59 -RewardApplied $false -KeyReceived $false
    $target = @($capture.Value.state.world_progress.museum.value.donatable_items | Where-Object {
        $_.qualified_item_id -ceq $targetQualifiedId
    })
    if ($target.Count -ne 1 -or $target[0].donated -isnot [bool] -or $target[0].donated -ne $false -or
        @($capture.Value.state.player.inventory.value | Where-Object {
            $_.qualified_item_id -ceq $targetQualifiedId -and $_.stack -gt 0
        }).Count -ne 0) {
        throw 'Geode boundary target must be undonated and absent from measured inventory.'
    }
    $startPath = Join-Path $Directory 'native-start-snapshot.json'
    Write-Utf8Text -Path $startPath -Value $capture.Raw
    $reportPath = Join-Path $Directory 'boundary.json'
    $report = [ordered]@{
        schema_version = 'stardewai.rusty_key_geode_boundary.v1'; run_id = $Context.RunId
        source_save_id = $identity[0]; source_player_id = $identity[1]
        target_qualified_item_id = $targetQualifiedId; geode_qualified_item_id = $GeodeQualifiedItemId
        native_start_snapshot_path = $startPath; native_start_snapshot_sha256 = Get-RuntimeFileSha256 -Path $startPath
        native_start_state_hash = $capture.Value.state_hash; donated_count = 59; target_inventory_quantity = 0
        boundary_only = $true; proves_native_acquisition = $false; formal_training_authorized = $false
    }
    Write-JsonFile -Path $reportPath -Value $report
    return [pscustomobject]$report
}
