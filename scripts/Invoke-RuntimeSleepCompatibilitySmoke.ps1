param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "ProofFarm_450250338",
    [string] $RunId = ("runtime-sleep-compatibility-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [switch] $NoBuild
)
$ErrorActionPreference = "Stop"
$game = $null
$backend = $null
try {
    # Reuse the isolated native profession/save path and its owned processes.
    . (Join-Path $PSScriptRoot "Invoke-RuntimeFullShipmentTerminalSmoke.ps1") `
        -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot -SaveSlot $SaveSlot `
        -RunId $RunId -NoShipment -PendingProfessionChoiceId 1 `
        -NoBuild:$NoBuild -IsolateMods -KeepGameRunning | Out-Null

    $snapshot = Wait-WorldSnapshot -TimeoutSeconds 60
    $fixtureRequest = New-BaseRequest -Snapshot $snapshot `
        -OptionId "debug.prepare_full_shipment_terminal_sleep" `
        -QueueItemId "$RunId.legacy.fixture"
    $fixture = Invoke-JsonPost -Url $nativeExecutorUrl -Body $fixtureRequest
    if ($fixture.status -ne "applied" -or
        $fixture.primitive_verification_status -ne "verified") {
        throw "Legacy sleep precondition fixture failed."
    }
    $settleSnapshot = Wait-WorldSnapshot -TimeoutSeconds 60
    $settleRequest = New-BaseRequest -Snapshot $settleSnapshot `
        -OptionId "executor.wait_ticks" -QueueItemId "$RunId.legacy.fixture.settle"
    $settleRequest.wait_ticks = 30
    $settled = Invoke-JsonPost -Url $nativeExecutorUrl -Body $settleRequest
    Write-JsonFile -Path (Join-Path $artifactDirectory "legacy-fixture-settle.json") -Value $settled
    if ($settled.status -ne "applied" -or
        $settled.primitive_verification_status -ne "verified") {
        throw "Native control did not settle after the sleep fixture warp."
    }

    $nights = @()
    for ($index = 1; $index -le 2; $index++) {
        $before = Wait-WorldSnapshot -TimeoutSeconds 60
        Write-JsonFile -Path (Join-Path $artifactDirectory "legacy-before-$index.json") -Value ([ordered]@{
            total_days = Read-FieldValue $before "time" "total_days"
            movement_context = $before.state.player.movement_timing_context.value
            player_tile_x = Read-FieldValue $before "player" "tile_x"
            player_tile_y = Read-FieldValue $before "player" "tile_y"
        })
        $request = New-BaseRequest -Snapshot $before `
            -OptionId "executor.sleep" -QueueItemId "$RunId.legacy.sleep.$index"
        if ($request.Contains("source_planning_option_id")) {
            throw "Legacy sleep must not acquire a new planning-lineage requirement."
        }
        $result = Invoke-JsonPost -Url $nativeExecutorUrl -Body $request
        Write-JsonFile -Path (Join-Path $artifactDirectory "legacy-sleep-$index.json") -Value $result
        if ($result.status -ne "applied" -or
            $result.primitive_verification_status -ne "verified") {
            throw "Legacy sleep $index failed: $(@($result.block_reasons) -join ',')"
        }

        Start-Sleep -Milliseconds 500
        $after = Wait-WorldSnapshot -TimeoutSeconds 60
        $tracker = Invoke-JsonGet -Url $nightTransactionUrl
        $pause = $after.state.player.movement_timing_context.value.executor_owned_idle_pause
        Write-JsonFile -Path (Join-Path $artifactDirectory "legacy-observation-$index.json") -Value ([ordered]@{
            before_day = Read-FieldValue $before "time" "total_days"
            after_day = Read-FieldValue $after "time" "total_days"
            tracker = $tracker
            movement_context = $after.state.player.movement_timing_context.value
            installed_mods = $after.installed_mods
        })
        if ([int](Read-FieldValue $after "time" "total_days") -ne
                ([int](Read-FieldValue $before "time" "total_days") + 1) -or
            $tracker.status -ne "ready_for_safe_next_day_action" -or
            -not $tracker.saw_native_save_commit -or
            -not $tracker.saw_safe_new_day_ready_state -or
            $pause.applied -ne $true) {
            throw "Legacy sleep $index did not restore saved next-day idle ownership."
        }
        $null = Get-VerifiedEndOfNightEvidence -Tracker $tracker `
            -ExpectedStatus "ready_for_safe_next_day_action" `
            -ExpectedRunId $RunId -TrainingOutputDirectory $trainingOutputDirectory
        $nights += [ordered]@{
            transaction_id = [string]$tracker.transaction_id
            evidence_path = [string]$tracker.evidence_path
            status = [string]$tracker.status
            total_days = [int](Read-FieldValue $after "time" "total_days")
            idle_pause_applied = [bool]$pause.applied
        }
    }

    $prior = Get-Content -LiteralPath $nights[0].evidence_path -Raw | ConvertFrom-Json
    if ($nights[0].transaction_id -eq $nights[1].transaction_id -or
        $prior.status -ne "blocked" -or
        $prior.failure_reason -ne "end_of_night_followup_not_observed_before_next_sleep") {
        throw "Starting another native sleep lost or falsely completed the prior composite proof."
    }
    $sourceSaveHashAfter = Get-DirectoryContentHash -Path $sourceSavePath
    if ($sourceSaveHashAfter -ne $sourceSaveHashBefore) {
        throw "Compatibility smoke changed the source save."
    }
    $compatibility = [ordered]@{
        status = "passed"
        run_id = $RunId
        profession_save_composite = "passed"
        legacy_native_nights = $nights
        unfulfilled_prior_proof = [string]$prior.status
        source_save_preserved = $true
        training_label_eligible = $false
    }
    Write-JsonFile -Path (Join-Path $artifactDirectory "compatibility-summary.json") -Value $compatibility
    $compatibility | ConvertTo-Json -Depth 8
}
finally {
    foreach ($process in @($game, $backend)) {
        if ($null -ne $process -and -not $process.HasExited) {
            Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            $process.WaitForExit(10000) | Out-Null
        }
    }
}
