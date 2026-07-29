param(
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot =
        "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId =
        ("runtime-book-reading-smoke-" +
         (Get-Date -Format "yyyyMMdd-HHmmss")),
    [string] $OutputDirectory =
        "artifacts\runtime-book-reading-smoke",
    [int] $StartupTimeoutSeconds = 120,
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Write-JsonFile {
    param([string] $Path, $Value)
    $Value |
        ConvertTo-Json -Depth 96 |
        Set-Content -LiteralPath $Path -Encoding utf8
}

function Invoke-JsonPost {
    param([string] $Url, $Body, [int] $TimeoutSeconds = 120)
    Invoke-RestMethod `
        -Method Post `
        -Uri $Url `
        -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) `
        -TimeoutSec $TimeoutSeconds
}

function Invoke-JsonGet {
    param([string] $Url, [int] $TimeoutSeconds = 30)
    Invoke-RestMethod `
        -Method Get `
        -Uri $Url `
        -Headers @{ "Accept" = "application/json" } `
        -TimeoutSec $TimeoutSeconds
}

function Wait-JsonHealth {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-JsonGet -Url $Url -TimeoutSeconds 3
            if ($response.status -eq "ok") {
                return $response
            }
        }
        catch {
            $lastError = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-WorldSnapshot {
    param([string] $Url, [int] $TimeoutSeconds)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-JsonGet -Url $Url -TimeoutSeconds 5
            if ($snapshot.save_id.status -in @("available", "derived")) {
                return $snapshot
            }
            $lastStatus = "save=$($snapshot.save_id.status)"
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for world snapshot. Last status: $lastStatus"
}

function String-Value($Value) {
    if ($null -eq $Value) {
        return ""
    }
    return [string]$Value
}

$runtimeGameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $runtimeGameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl =
    "http://127.0.0.1:8765/api/v1/snapshot?profile=full"
$executorUrl =
    "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExe"
}
if (-not (Test-Path -LiteralPath $savesPath -PathType Container)) {
    throw "Isolated saves path not found: $savesPath"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $slot = Get-ChildItem -LiteralPath $savesPath -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $slot) {
        throw "No isolated save slots found under $savesPath"
    }
    $SaveSlot = $slot.Name
}

$runDirectory = Join-Path (
    Join-Path $ProjectRoot $OutputDirectory
) $RunId
New-Item -ItemType Directory -Force -Path $runDirectory |
    Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot |
    Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot |
    Out-Null

$smokeModsPath = Join-Path (
    Join-Path $RuntimeRoot "smoke-mods"
) $RunId
New-Item -ItemType Directory -Force -Path $smokeModsPath |
    Out-Null
foreach ($modName in @(
    "StardewAI.TransparentBridge",
    "StardewAI.RuntimeTestHarness"
)) {
    $sourceMod = Join-Path (
        Join-Path $runtimeGameDir "Mods"
    ) $modName
    $targetMod = Join-Path $smokeModsPath $modName
    New-Item -ItemType Directory -Force -Path $targetMod |
        Out-Null
    Copy-Item `
        -Path (Join-Path $sourceMod "*") `
        -Destination $targetMod `
        -Recurse `
        -Force
}

$previousEnv = @{}
foreach ($name in @(
    "STARDEWAI_TEST_SAVES",
    "STARDEWAI_TEST_SLOT",
    "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID",
    "STARDEWAI_TRAINING_MODE",
    "SDL_AUDIODRIVER",
    "ALSOFT_DRIVERS",
    "SMAPI_MODS_PATH"
)) {
    $previousEnv[$name] =
        [Environment]::GetEnvironmentVariable($name)
}

$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath

    $process = Start-Process `
        -FilePath $smapiExe `
        -WorkingDirectory $runtimeGameDir `
        -WindowStyle Hidden `
        -PassThru
    $executorHealth = Wait-JsonHealth `
        -Url "http://127.0.0.1:8767/health" `
        -TimeoutSeconds 30
    $snapshot = Wait-WorldSnapshot `
        -Url $snapshotUrl `
        -TimeoutSeconds $StartupTimeoutSeconds

    $cases = @(
        [ordered]@{
            fixture_branch = "skill_book"
            expected_branch = "skill_book"
            require_well_read = $false
        },
        [ordered]@{
            fixture_branch = "power_book_repeated_skill"
            expected_branch = "power_book_repeated_skill"
            require_well_read = $false
        },
        [ordered]@{
            fixture_branch = "power_book_repeated_all_skills"
            expected_branch = "power_book_repeated_all_skills"
            require_well_read = $false
        },
        [ordered]@{
            fixture_branch = "purple_book"
            expected_branch = "purple_book"
            require_well_read = $false
        },
        [ordered]@{
            fixture_branch = "power_book_first_read"
            expected_branch = "power_book_first_read"
            require_well_read = $false
        },
        [ordered]@{
            fixture_branch = "power_book_first_read_well_read"
            expected_branch = "power_book_first_read"
            require_well_read = $true
        },
        [ordered]@{
            fixture_branch = "queen_of_sauce_first_read"
            expected_branch = "queen_of_sauce_first_read"
            require_well_read = $false
        }
    )
    $caseResults = @()
    foreach ($case in $cases) {
        $caseName = [string]$case.fixture_branch
        $setupRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-book-reading-smoke"
            queue_item_id =
                "runtime-book-reading-smoke.$caseName.setup"
            before_state_hash = $snapshot.state_hash
            option_id = "debug.setup_book_fixture"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            book_native_branch = $caseName
        }
        $setupResult = Invoke-JsonPost `
            -Url $executorUrl `
            -Body $setupRequest
        Write-JsonFile `
            -Path (Join-Path $runDirectory "$caseName-setup.json") `
            -Value $setupResult
        if (
            $setupResult.status -ne "applied" -or
            $setupResult.primitive_verification_status -ne "verified"
        ) {
            throw "$caseName fixture failed: " +
                (@($setupResult.block_reasons) -join ",")
        }

        Start-Sleep -Milliseconds 300
        $before = Wait-WorldSnapshot `
            -Url $snapshotUrl `
            -TimeoutSeconds 30
        $book = @($before.state.player.book_candidates.value |
            Where-Object {
                [string]$_.native_branch -eq
                    [string]$case.expected_branch -and
                [bool]$_.available
            })
        if ($book.Count -ne 1) {
            throw "$caseName expected one available projected book, got " +
                $book.Count
        }
        $book = $book[0]
        if (
            [bool]$case.require_well_read -and
            -not [bool]$book.well_read_achievement_will_unlock
        ) {
            throw "$caseName did not project the Well Read unlock"
        }

        $readRequest = [ordered]@{
            schema_version = "training_execution_request.v1"
            run_id = $RunId
            queue_id = "runtime-book-reading-smoke"
            queue_item_id =
                "runtime-book-reading-smoke.$caseName.read"
            before_state_hash = $before.state_hash
            option_id = "executor.read_book"
            execution_mode = "training_singleplayer"
            actor = "training_farmer.main"
            save_isolation_path = $savesPath
            request_nonce = [guid]::NewGuid().ToString("N")
            created_at = [DateTimeOffset]::UtcNow.ToString("O")
            slot_index = [int]$book.slot_index
            item_id = [string]$book.item_id
            qualified_item_id = [string]$book.qualified_item_id
            book_runtime_type = [string]$book.runtime_type
            book_category = [int]$book.category
            book_stack_before = [int]$book.stack_before
            book_stack_after = [int]$book.stack_after
            book_native_branch = [string]$book.native_branch
            book_native_branch_status =
                [string]$book.native_branch_status
            book_context_tags_native_order_json =
                [string]$book.context_tags_native_order_json
            book_matched_experience_tag =
                [string]$book.matched_book_experience_tag
            expected_skill_experience_deltas_json =
                [string]$book.experience_deltas_json
            expected_mastery_experience_delta =
                [int]$book.mastery_experience_delta
            book_skill_level_deltas_json =
                [string]$book.skill_level_deltas_json
            book_new_levels_before_json =
                [string]$book.new_levels_before_json
            book_new_levels_after_json =
                [string]$book.new_levels_after_json
            book_native_feedback_callbacks =
                [string]$book.native_feedback_callbacks
            book_stat_key = [string]$book.book_stat_key
            book_stat_before =
                String-Value $book.book_stat_before
            book_stat_after =
                String-Value $book.book_stat_after
            read_a_book_mail_before =
                [bool]$book.read_a_book_mail_before
            read_a_book_mail_after =
                [bool]$book.read_a_book_mail_after
            well_read_achievement_before =
                [bool]$book.well_read_achievement_before
            well_read_achievement_after =
                [bool]$book.well_read_achievement_after
            well_read_achievement_will_unlock =
                [bool]$book.well_read_achievement_will_unlock
            well_read_hatter_mail_before =
                [bool]$book.well_read_hatter_mail_before
            well_read_hatter_mail_after =
                [bool]$book.well_read_hatter_mail_after
            well_read_dialogue_event_seen_before =
                [bool]$book.well_read_dialogue_event_seen_before
            well_read_dialogue_event_seen_after =
                [bool]$book.well_read_dialogue_event_seen_after
            well_read_ui_sound_platform_callbacks =
                [string]$book.well_read_ui_sound_platform_callbacks
            cooking_recipes_added_json =
                [string]$book.cooking_recipes_added_json
            cooking_recipes_added_count =
                [int]$book.cooking_recipes_added_count
        }
        $readResult = Invoke-JsonPost `
            -Url $executorUrl `
            -Body $readRequest
        Start-Sleep -Milliseconds 1100
        $after = Wait-WorldSnapshot `
            -Url $snapshotUrl `
            -TimeoutSeconds 30
        $remaining = @(
            $after.state.player.book_candidates.value |
            Where-Object {
                [int]$_.slot_index -eq [int]$book.slot_index -and
                [string]$_.qualified_item_id -eq
                    [string]$book.qualified_item_id
            }
        )
        $passed =
            $readResult.status -eq "applied" -and
            $readResult.primitive_verification_status -eq "verified" -and
            $remaining.Count -eq 0
        $caseResult = [ordered]@{
            fixture_branch = $caseName
            native_branch = [string]$book.native_branch
            qualified_item_id = [string]$book.qualified_item_id
            slot_index = [int]$book.slot_index
            well_read_unlock =
                [bool]$book.well_read_achievement_will_unlock
            expected_skill_experience_deltas_json =
                [string]$book.experience_deltas_json
            expected_mastery_experience_delta =
                [int]$book.mastery_experience_delta
            status = $readResult.status
            verification =
                $readResult.primitive_verification_status
            block_reasons = @($readResult.block_reasons)
            book_remaining_in_slot = $remaining.Count -gt 0
            result = if ($passed) { "passed" } else { "failed" }
        }
        Write-JsonFile `
            -Path (Join-Path $runDirectory "$caseName-before.json") `
            -Value $before
        Write-JsonFile `
            -Path (Join-Path $runDirectory "$caseName-read.json") `
            -Value $readResult
        Write-JsonFile `
            -Path (Join-Path $runDirectory "$caseName-after.json") `
            -Value $after
        $caseResults += [pscustomobject]$caseResult
        $snapshot = $after
    }

    $summary = [ordered]@{
        status = if (
            @($caseResults |
                Where-Object { $_.result -ne "passed" }).Count -eq 0
        ) {
            "passed"
        }
        else {
            "failed"
        }
        run_id = $RunId
        save_slot = $SaveSlot
        saves_path = $savesPath
        smoke_mods_path = $smokeModsPath
        loaded_mod_allowlist = @(
            "StardewAI.TransparentBridge",
            "StardewAI.RuntimeTestHarness"
        )
        cases = $caseResults
        executor_health = $executorHealth
        smapi_process_id = $process.Id
    }
    Write-JsonFile `
        -Path (Join-Path $runDirectory "summary.json") `
        -Value $summary
    $summary | ConvertTo-Json -Depth 16
    if ($summary.status -ne "passed") {
        throw "Runtime book-reading smoke failed. See $runDirectory"
    }
}
finally {
    foreach ($key in $previousEnv.Keys) {
        Set-Item -Path "env:$key" -Value $previousEnv[$key]
    }
    if (
        -not $KeepGameRunning -and
        $null -ne $process -and
        -not $process.HasExited
    ) {
        Stop-Process `
            -Id $process.Id `
            -Force `
            -ErrorAction SilentlyContinue
    }
}
