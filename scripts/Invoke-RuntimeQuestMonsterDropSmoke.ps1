param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [int] $MineLevel = 45,
    [string] $QualifiedItemId = "(O)768",
    [string] $QuestId = "stardewai.runtime.monster-drop",
    [ValidateSet("ordinary_quest", "special_order")]
    [string] $QuestFamily = "ordinary_quest",
    [string] $RunId = ("runtime-quest-monster-drop-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory = "artifacts\runtime-quest-monster-drop",
    [switch] $VisibleGame,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value | ConvertTo-Json -Depth 96 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 150)
    Invoke-RestMethod `
        -Method Post `
        -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) `
        -TimeoutSec $TimeoutSeconds
}

function Wait-FullSnapshot {
    param([int] $TimeoutSeconds = 60)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_attempted"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod `
                -Method Get `
                -Uri "http://127.0.0.1:8765/api/v1/snapshot?profile=full" `
                -Headers @{ Accept = "application/json" } `
                -TimeoutSec 15
            if ($snapshot.schema_version -eq "snapshot.v1" -and
                $snapshot.state.mining.completeness.value.status -eq "complete" -and
                [int]$snapshot.state.mining.current_mine.value.mine_level -eq $MineLevel) {
                return $snapshot
            }
            $lastError = "snapshot_not_ready"
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 300
    }
    throw "Full mining snapshot did not become ready: $lastError"
}

function Read-Quest {
    param($Snapshot)
    if ($QuestFamily -eq "special_order") {
        $order = $Snapshot.state.quests.special_orders.value |
            Where-Object { [string]$_.quest_key -eq $QuestId } |
            Select-Object -First 1
        if ($null -eq $order -or @($order.objectives).Count -eq 0) {
            return $null
        }
        $objective = @($order.objectives)[0]
        return [pscustomobject]@{
            current_count = [int]$objective.current_count
            target_count = [int]$objective.max_count
        }
    }

    $quest = $Snapshot.state.quests.active_quests.value |
        Where-Object {
            [string]$_.id -eq $QuestId -and
            [string]$_.runtime_type -eq "ResourceCollectionQuest"
        } |
        Select-Object -First 1
    if ($null -eq $quest) {
        return $null
    }
    return [pscustomobject]@{
        current_count = [int]$quest.per_type_fields.number_collected
        target_count = [int]$quest.per_type_fields.number
    }
}

function Find-MatchingDebris {
    param($Snapshot)
    return $Snapshot.state.current_location.debris.value |
        Where-Object {
            [string]$_.qualified_item_id -eq $QualifiedItemId -and
            @($_.chunks).Count -gt 0
        } |
        Select-Object -First 1
}

if ($MineLevel -lt 1 -or $MineLevel -gt 120) {
    throw "MineLevel must be between 1 and 120."
}

$runDirectory = Join-Path $ProjectRoot (Join-Path $OutputDirectory $RunId)
$bootstrapOutput = Join-Path $OutputDirectory $RunId
$savesPath = Join-Path $RuntimeRoot "saves"
$gameProcess = $null
New-Item -ItemType Directory -Force -Path $runDirectory | Out-Null

try {
    $existingGameIds = @(Get-Process -Name StardewModdingAPI -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty Id)
    & (Join-Path $ProjectRoot "scripts\Invoke-RuntimeMiningSnapshotSmoke.ps1") `
        -ProjectRoot $ProjectRoot `
        -RuntimeRoot $RuntimeRoot `
        -MineLevel $MineLevel `
        -MinimumBreakableStoneCount 0 `
        -SampleCount 1 `
        -MaximumSnapshotMilliseconds 5000 `
        -RunId $RunId `
        -OutputDirectory $bootstrapOutput `
        -MiningCalibrationLoadout `
        -VisibleGame:$VisibleGame `
        -KeepGameRunning | Out-Null

    $gameProcess = Get-Process -Name StardewModdingAPI -ErrorAction Stop |
        Where-Object {
            $_.Id -notin $existingGameIds -and
            $_.Path -like "$RuntimeRoot\*"
        } |
        Sort-Object StartTime -Descending |
        Select-Object -First 1
    if ($null -eq $gameProcess) {
        throw "Isolated SMAPI process was not found after bootstrap."
    }

    $initial = Wait-FullSnapshot
    $fixtureRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-quest-monster-drop"
        queue_item_id = "runtime-quest-monster-drop.setup"
        before_state_hash = $initial.state_hash
        option_id = "debug.setup_quest_monster_drop_fixture"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        quest_id = $QuestId
        quest_family = $QuestFamily
        quest_expected_target_count = 1
        qualified_item_id = $QualifiedItemId
    }
    $fixture = Invoke-JsonPost `
        -Url "http://127.0.0.1:8767/api/v1/training/execute" `
        -Body $fixtureRequest
    Write-JsonFile (Join-Path $runDirectory "fixture-result.json") $fixture
    if ($fixture.status -ne "applied" -or
        $fixture.primitive_verification_status -ne "verified") {
        throw "Quest monster-drop fixture failed: $(@($fixture.block_reasons) -join ',')"
    }

    Start-Sleep -Milliseconds 500
    $beforeCombat = Wait-FullSnapshot
    $questBefore = Read-Quest $beforeCombat
    $target = $beforeCombat.state.mining.monsters.value |
        Where-Object {
            [string]$_.runtime_identity -eq
                [string]$fixture.combat_target_runtime_identity
        } |
        Select-Object -First 1
    if ($null -eq $questBefore -or $null -eq $target) {
        throw "Fixture quest or monster was absent from the transparent snapshot."
    }
    $projection = $target.melee_attack_projections |
        Where-Object {
            $_.duration_status -eq
                "exact_active_melee_phase_excluding_movement" -and
            $_.terminal_effect -eq "defeat"
        } |
        Sort-Object expected_active_damage_duration_ms, slot_index |
        Select-Object -First 1
    if ($null -eq $projection) {
        throw "Fixture monster had no complete defeat-capable melee projection."
    }
    if ($QualifiedItemId -notin @($target.possible_drop_qualified_item_ids)) {
        throw "Transparent monster projection omitted the fixture drop."
    }

    $combatRequest = [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-quest-monster-drop"
        queue_item_id = "runtime-quest-monster-drop.combat"
        before_state_hash = $beforeCombat.state_hash
        option_id = "executor.combat_monster"
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
        target_tile_x = [int]$target.tile_x
        target_tile_y = [int]$target.tile_y
        target_runtime_identity = [string]$target.runtime_identity
        target_runtime_type = [string]$target.runtime_type
        target_name = [string]$target.name
        max_attacks = 64
        max_movement_tiles = 512
        combat_weapon_slot_index = [int]$projection.slot_index
        combat_terminal_state = "defeat"
        required_weapon_enchantment_runtime_type =
            [string]$target.melee_damage_semantics.required_weapon_enchantment_runtime_type
        qualified_item_id = $QualifiedItemId
        quest_candidate_id = "runtime_fixture:$QuestId"
        quest_family = $QuestFamily
        quest_id = $QuestId
        quest_key = if ($QuestFamily -eq "special_order") { $QuestId } else { "" }
        quest_objective_index = if ($QuestFamily -eq "special_order") { 0 } else { $null }
        quest_runtime_type = if ($QuestFamily -eq "special_order") {
            "SpecialOrder"
        } else {
            "ResourceCollectionQuest"
        }
        quest_expected_current_count = 0
        quest_expected_target_count = 1
        quest_acquisition_source_step = $true
        quest_acquisition_target_step = $false
    }
    $combat = Invoke-JsonPost `
        -Url "http://127.0.0.1:8767/api/v1/training/execute" `
        -Body $combatRequest
    Write-JsonFile (Join-Path $runDirectory "combat-result.json") $combat
    if ($combat.status -ne "applied" -or
        $combat.primitive_verification_status -ne "verified" -or
        -not $combat.combat_target_defeated) {
        throw "Task-attached combat failed: $(@($combat.block_reasons) -join ',')"
    }

    Start-Sleep -Milliseconds 600
    $afterCombat = Wait-FullSnapshot
    $questAfterCombat = Read-Quest $afterCombat
    $progressAfterCombat = if ($null -eq $questAfterCombat) {
        1
    } else {
        [int]$questAfterCombat.current_count
    }
    $pickup = $null
    $afterReceipt = $afterCombat
    if ($progressAfterCombat -eq 0) {
        $debris = Find-MatchingDebris $afterCombat
        if ($null -eq $debris) {
            throw "Combat made no quest progress and exposed no matching debris."
        }
        $chunk = $debris.chunks | Select-Object -First 1
        $pickupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-quest-monster-drop"
            queue_item_id = "runtime-quest-monster-drop.pickup"
            before_state_hash = $afterCombat.state_hash
            option_id = "executor.pickup_debris"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            target_tile_x = [int]$chunk.tile_x
            target_tile_y = [int]$chunk.tile_y
            debris_index = [int]$debris.debris_index
            qualified_item_id = $QualifiedItemId
            quest_candidate_id = "runtime_fixture:$QuestId"
            quest_family = $QuestFamily
            quest_id = $QuestId
            quest_key = if ($QuestFamily -eq "special_order") { $QuestId } else { "" }
            quest_objective_index = if ($QuestFamily -eq "special_order") { 0 } else { $null }
            quest_runtime_type = if ($QuestFamily -eq "special_order") {
                "SpecialOrder"
            } else {
                "ResourceCollectionQuest"
            }
            quest_expected_current_count = 0
            quest_expected_target_count = 1
            quest_acquisition_source_step = $false
            quest_acquisition_target_step = $true
        }
        $pickup = Invoke-JsonPost `
            -Url "http://127.0.0.1:8767/api/v1/training/execute" `
            -Body $pickupRequest
        Write-JsonFile (Join-Path $runDirectory "pickup-result.json") $pickup
        if ($pickup.status -ne "applied" -or
            $pickup.primitive_verification_status -ne "verified") {
            throw "Task-attached debris pickup failed: $(@($pickup.block_reasons) -join ',')"
        }
        Start-Sleep -Milliseconds 500
        $afterReceipt = Wait-FullSnapshot
    }

    $questAfterReceipt = Read-Quest $afterReceipt
    $progressAfterReceipt = if ($null -eq $questAfterReceipt) {
        1
    } else {
        [int]$questAfterReceipt.current_count
    }
    $summary = [ordered]@{
        status = if ($progressAfterReceipt -ge 1) { "passed" } else { "failed" }
        run_id = $RunId
        quest_family = $QuestFamily
        mine_level = $MineLevel
        qualified_item_id = $QualifiedItemId
        fixture_status = [string]$fixture.status
        combat_status = [string]$combat.status
        combat_verification = [string]$combat.primitive_verification_status
        combat_target_defeated = [bool]$combat.combat_target_defeated
        combat_attack_count = [int]$combat.combat_attack_count
        combat_hit_count = [int]$combat.combat_hit_count
        quest_progress_before = 0
        quest_progress_after_combat = $progressAfterCombat
        receipt_mode = if ($progressAfterCombat -gt 0) {
            "native_incidental_pickup_during_combat"
        } else {
            "fresh_snapshot_debris_pickup"
        }
        pickup_status = if ($null -eq $pickup) {
            "not_required"
        } else {
            [string]$pickup.status
        }
        quest_progress_after_receipt = $progressAfterReceipt
        source_feedback_reasons = @($combat.primitive_verification_reasons)
        bridge_state_hash_before = [string]$beforeCombat.state_hash
        bridge_state_hash_after = [string]$afterReceipt.state_hash
        smapi_process_id = $gameProcess.Id
    }
    Write-JsonFile (Join-Path $runDirectory "before-combat-snapshot.json") $beforeCombat
    Write-JsonFile (Join-Path $runDirectory "after-combat-snapshot.json") $afterCombat
    Write-JsonFile (Join-Path $runDirectory "after-receipt-snapshot.json") $afterReceipt
    Write-JsonFile (Join-Path $runDirectory "summary.json") $summary
    $summary | ConvertTo-Json -Depth 16
    if ($summary.status -ne "passed") {
        throw "Runtime quest monster-drop smoke failed. See $runDirectory"
    }
}
finally {
    if (-not $KeepGameRunning -and
        $null -ne $gameProcess -and
        -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
