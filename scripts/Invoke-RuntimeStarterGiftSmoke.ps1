[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $RunId = ("runtime-starter-gift-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputRoot = "artifacts\runtime-starter-gift",
    [int] $StartupTimeoutSeconds = 300,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonGet([string] $Url, [int] $TimeoutSeconds = 30) {
    $response = Invoke-WebRequest -UseBasicParsing -Uri $Url `
        -Headers @{ Accept = "application/json" } -TimeoutSec $TimeoutSeconds
    [pscustomobject]@{
        Raw = [string]$response.Content
        Value = $response.Content | ConvertFrom-Json
    }
}

function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 180) {
    Invoke-RestMethod -Method Post -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 64) -TimeoutSec $TimeoutSeconds
}

function Wait-Snapshot([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet $Url 30
            if ($response.Value.schema_version -eq "snapshot.v1" -and
                $response.Value.state.player.location_id.status -in @("available", "derived")) {
                return $response
            }
            $lastStatus = "schema=$($response.Value.schema_version)"
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for full snapshot. Last status: $lastStatus"
}

function Wait-StarterGiftProjection([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Wait-Snapshot $Url 30
            $gift = $response.Value.state.current_location.starter_gift_claim.value
            if ($gift.status -eq "ready" -and
                [bool]$gift.exact_starter_gift_present) {
                return $response
            }
            $lastStatus = "gift=$($gift.status);present=$($gift.exact_starter_gift_present)"
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for starter gift projection. Last status: $lastStatus"
}

function Write-RawJson([string] $Path, [string] $Raw) {
    [IO.File]::WriteAllText($Path, $Raw, [Text.UTF8Encoding]::new($false))
}

function Write-Json([string] $Path, $Value) {
    [IO.File]::WriteAllText(
        $Path,
        ($Value | ConvertTo-Json -Depth 96),
        [Text.UTF8Encoding]::new($false))
}

function New-Request($Snapshot, [string] $OptionId, [string] $ItemId, [string] $SavesPath) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-starter-gift"
        queue_item_id = $ItemId
        before_state_hash = [string]$Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $SavesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

$resolvedOutputRoot = if ([IO.Path]::IsPathRooted($OutputRoot)) {
    [IO.Path]::GetFullPath($OutputRoot)
}
else {
    [IO.Path]::GetFullPath((Join-Path $ProjectRoot $OutputRoot))
}
$runDirectory = Join-Path $resolvedOutputRoot $RunId
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executeUrl = "http://127.0.0.1:8767/api/v1/training/execute"
$existingGameIds = @(Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue |
    Select-Object -ExpandProperty Id)
$game = $null

try {
    & (Join-Path $ProjectRoot "scripts\Invoke-RuntimeFreshSaveSmoke.ps1") `
        -ProjectRoot $ProjectRoot `
        -RuntimeRoot $RuntimeRoot `
        -RunId $RunId `
        -OutputRoot $resolvedOutputRoot `
        -StartupTimeoutSeconds $StartupTimeoutSeconds `
        -KeepGameRunning | Out-Null

    $newGames = @(Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue |
        Where-Object { $existingGameIds -notcontains $_.Id })
    if ($newGames.Count -ne 1) {
        throw "Expected exactly one fresh-save game process; found $($newGames.Count)."
    }
    $game = $newGames[0]

    $freshSummaryPath = Join-Path $runDirectory "summary.json"
    $freshSummary = Get-Content -LiteralPath $freshSummaryPath -Raw | ConvertFrom-Json
    Copy-Item -LiteralPath $freshSummaryPath `
        -Destination (Join-Path $runDirectory "fresh-save-summary.json")
    $savesPath = [string]$freshSummary.isolated_save_root

    $readyResponse = Wait-StarterGiftProjection $snapshotUrl 60
    $ready = $readyResponse.Value
    for ($menuPass = 0; $menuPass -lt 8 -and
        [bool]$ready.state.menus.active_menu.value.is_open; $menuPass++) {
        $closeRequest = New-Request $ready "executor.close_menu" `
            "$RunId.initial-close.$menuPass" $savesPath
        $close = Invoke-JsonPost $executeUrl $closeRequest
        if ($close.status -ne "applied") {
            throw "Initial menu close failed: $(@($close.block_reasons) -join ',')"
        }
        Start-Sleep -Milliseconds 500
        $readyResponse = Wait-StarterGiftProjection $snapshotUrl 60
        $ready = $readyResponse.Value
    }
    if ([bool]$ready.state.menus.active_menu.value.is_open) {
        throw "Initial menu did not close."
    }

    Write-RawJson (Join-Path $runDirectory "before-snapshot.json") $readyResponse.Raw
    $gift = $ready.state.current_location.starter_gift_claim.value
    if ($gift.status -ne "ready" -or -not [bool]$gift.exact_starter_gift_present) {
        throw "Transparent starter gift projection is not ready: $($gift.status)"
    }
    $stands = @($gift.stand_tiles | Where-Object { [bool]$_.available })
    if ($stands.Count -lt 1) {
        throw "No transparent starter gift stand tile is available."
    }
    $stand = $stands | Sort-Object tile_y, tile_x | Select-Object -First 1

    $claim = New-Request $ready "rewards.claim_starter_gift" "$RunId.claim" $savesPath
    $claim.location_id = [string]$gift.location_id
    $claim.target_tile_x = [int]$gift.target_tile_x
    $claim.target_tile_y = [int]$gift.target_tile_y
    $claim.stand_tile_x = [int]$stand.tile_x
    $claim.stand_tile_y = [int]$stand.tile_y
    $claim.target_runtime_type = [string]$gift.target_runtime_type
    $claim.qualified_item_id = [string]$gift.qualified_item_id
    $claim.quantity = [int]$gift.content_slot_count
    $claim.expected_output_items_json = [string]$gift.expected_output_items_json
    $claim.quest_id = [string]$gift.expected_quest_id
    $claim.reward_branch = "starter_gift_farm_type_$([int]$gift.farm_type)"
    $claim.interaction_kind = [string]$gift.interaction_kind
    $claim.expected_action_type = [string]$gift.expected_action_type
    $claim.native_contract = [string]$gift.native_contract
    $claim.max_movement_tiles = 64
    $result = Invoke-JsonPost $executeUrl $claim
    Write-Json (Join-Path $runDirectory "claim-result.json") $result

    Start-Sleep -Milliseconds 500
    $afterResponse = Wait-Snapshot $snapshotUrl 30
    $after = $afterResponse.Value
    Write-RawJson (Join-Path $runDirectory "after-snapshot.json") $afterResponse.Raw
    $afterGift = $after.state.current_location.starter_gift_claim.value
    $passed = $result.status -eq "applied" -and
        $result.primitive_verification_status -eq "verified" -and
        -not [bool]$afterGift.exact_starter_gift_present -and
        [string]$after.state_hash -ne [string]$ready.state_hash

    $summary = [ordered]@{
        schema_version = "stardewai.runtime_starter_gift_smoke.v1"
        status = if ($passed) { "passed" } else { "failed" }
        run_id = $RunId
        save_id = [string]$ready.save_id.value
        native_save_slot = [string]$freshSummary.native_save_slot
        isolated_save_root = $savesPath
        before_state_hash = [string]$ready.state_hash
        after_state_hash = [string]$after.state_hash
        projection_status_before = [string]$gift.status
        projection_status_after = [string]$afterGift.status
        target_tile = "$([int]$gift.target_tile_x),$([int]$gift.target_tile_y)"
        expected_quest_id = [string]$gift.expected_quest_id
        expected_output_items_json = [string]$gift.expected_output_items_json
        claim_status = [string]$result.status
        claim_verification = [string]$result.primitive_verification_status
        loaded_mod_allowlist = @(
            "StardewAI.TransparentBridge",
            "StardewAI.RuntimeTestHarness")
        output_directory = $runDirectory
    }
    Write-Json (Join-Path $runDirectory "starter-gift-summary.json") $summary
    $summary | ConvertTo-Json -Depth 16
    if (-not $passed) {
        exit 2
    }
}
finally {
    if (-not $KeepGameRunning -and $null -ne $game -and -not $game.HasExited) {
        Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue
        $game.WaitForExit(10000) | Out-Null
    }
}
