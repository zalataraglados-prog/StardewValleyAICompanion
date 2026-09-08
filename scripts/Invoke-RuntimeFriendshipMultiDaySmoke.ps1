[CmdletBinding()]
param(
    [string]$ProjectRoot = "",
    [string]$RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string]$SaveSlot = "",
    [string]$RunId = ("runtime-friendship-multi-day-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [ValidateRange(2, 28)]
    [int]$TransitionCount = 8,
    [int]$StartupTimeoutSeconds = 180
)

$ErrorActionPreference = "Stop"
if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Write-Utf8NoBom {
    param([string]$Path, [string]$Content)

    [System.IO.File]::WriteAllText($Path, $Content, [System.Text.UTF8Encoding]::new($false))
}

function Get-SaveFingerprint {
    param([string]$Path)

    $root = [System.IO.Path]::GetFullPath($Path).TrimEnd('\') + '\'
    $lines = Get-ChildItem -LiteralPath $Path -File -Recurse |
        Sort-Object FullName |
        ForEach-Object {
            $fullPath = [System.IO.Path]::GetFullPath($_.FullName)
            if (-not $fullPath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Save fingerprint encountered a file outside the source root: $fullPath"
            }
            $relativePath = $fullPath.Substring($root.Length)
            $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
            $relativePath + "|" + $hash
        }
    $payload = [System.Text.Encoding]::UTF8.GetBytes(($lines -join "`n"))
    $sha256 = [System.Security.Cryptography.SHA256]::Create()
    try {
        [BitConverter]::ToString($sha256.ComputeHash($payload)).Replace("-", "")
    }
    finally {
        $sha256.Dispose()
    }
}

function Wait-FullSnapshot {
    param(
        [string]$Url,
        [int]$TimeoutSeconds,
        [Nullable[int]]$ExpectedTotalDays = $null
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastStatus = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $response = Invoke-WebRequest -UseBasicParsing -Uri $Url `
                -Headers @{ Accept = "application/json" } -TimeoutSec 10
            $snapshot = $response.Content | ConvertFrom-Json
            $progress = $snapshot.state.npcs.grandpa_friendship_progress
            $totalDays = [int]$snapshot.state.time.total_days.value
            $time = [int]$snapshot.state.time.time.value
            $lastStatus = "save=$($snapshot.save_id.status);progress=$($progress.status);days=$totalDays;time=$time"
            $expectedDayMatches = -not $ExpectedTotalDays.HasValue -or
                $totalDays -eq $ExpectedTotalDays.Value
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $progress.status -in @("available", "derived") -and
                $progress.value.projection_status -eq "complete_live_native_iteration" -and
                $progress.value.day_transition_inputs_status -eq "complete_live_native_fields" -and
                $expectedDayMatches) {
                return [pscustomobject]@{
                    Raw = $response.Content
                    Parsed = $snapshot
                }
            }
        }
        catch {
            $lastStatus = $_.Exception.Message
        }
        Start-Sleep -Milliseconds 500
    }
    throw "Timed out waiting for a full friendship snapshot. Last status: $lastStatus"
}

function New-ExecutionRequest {
    param(
        [string]$OptionId,
        [string]$QueueItemId,
        [string]$StateHash,
        [string]$IsolationPath
    )

    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-friendship-multi-day-smoke"
        queue_item_id = $QueueItemId
        before_state_hash = $StateHash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $IsolationPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Invoke-Execution {
    param([string]$Url, $Request, [int]$TimeoutSeconds)

    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Request | ConvertTo-Json -Depth 32) -TimeoutSec $TimeoutSeconds
}

function Find-FriendshipRow {
    param($Snapshot, [string]$NpcName)

    @($Snapshot.state.npcs.grandpa_friendship_progress.value.eligible_villager_rows) |
        Where-Object { [string]$_.npc_name -eq $NpcName } |
        Select-Object -First 1
}

$gameDirectory = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExecutable = Join-Path $gameDirectory "StardewModdingAPI.exe"
$sourceSavesRoot = Join-Path $RuntimeRoot "saves"
$bootstrapProject = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=true"
$executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExecutable -PathType Leaf)) {
    throw "SMAPI executable not found: $smapiExecutable"
}
if (-not (Test-Path -LiteralPath $sourceSavesRoot -PathType Container)) {
    throw "Isolated source saves directory not found: $sourceSavesRoot"
}
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $sourceSave = Get-ChildItem -LiteralPath $sourceSavesRoot -Directory |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -eq $sourceSave) {
        throw "No isolated source save exists under $sourceSavesRoot"
    }
    $SaveSlot = $sourceSave.Name
}
$sourceSavePath = Join-Path $sourceSavesRoot $SaveSlot
if (-not (Test-Path -LiteralPath $sourceSavePath -PathType Container)) {
    throw "Requested isolated source save slot not found: $sourceSavePath"
}
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Port $port is already listening. Refusing to attach to an existing runtime."
    }
}
$existingGame = Get-Process -Name @("StardewModdingAPI", "Stardew Valley") `
    -ErrorAction SilentlyContinue
if ($null -ne $existingGame) {
    throw "A Stardew process is already running. Refusing to attach or stop it."
}

$runDirectory = Join-Path $ProjectRoot ("artifacts\runtime-friendship-multi-day-smoke\" + $RunId)
$clonedSavesRoot = Join-Path $runDirectory "isolated-saves"
$clonedSavePath = Join-Path $clonedSavesRoot $SaveSlot
$fixturePath = Join-Path $runDirectory "fixture-result.json"
$summaryPath = Join-Path $runDirectory "summary.json"
$smokeModsPath = Join-Path (Join-Path $RuntimeRoot "smoke-mods") $RunId
$sourceFingerprintBefore = Get-SaveFingerprint -Path $sourceSavePath
New-Item -ItemType Directory -Force -Path $clonedSavesRoot | Out-Null
New-Item -ItemType Directory -Force -Path $smokeModsPath | Out-Null
Copy-Item -LiteralPath $sourceSavePath -Destination $clonedSavePath -Recurse

& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") `
    -ProjectRoot $ProjectRoot | Out-Null
foreach ($modName in @("StardewAI.TransparentBridge", "StardewAI.RuntimeTestHarness")) {
    Copy-Item -LiteralPath (Join-Path $gameDirectory ("Mods\" + $modName)) `
        -Destination (Join-Path $smokeModsPath $modName) -Recurse
}
& dotnet build $bootstrapProject --no-restore --nologo `
    "-p:GamePath=$gameDirectory"
if ($LASTEXITCODE -ne 0) {
    throw "Goal-conditioned bootstrap build failed with exit code $LASTEXITCODE."
}

$environmentNames = @(
    "STARDEWAI_TEST_SAVES",
    "STARDEWAI_TEST_SLOT",
    "STARDEWAI_TEST_AUTO_LOAD",
    "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID",
    "STARDEWAI_TRAINING_MODE",
    "STARDEWAI_TRAINING_OUTPUT_DIR",
    "STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE",
    "SDL_AUDIODRIVER",
    "ALSOFT_DRIVERS",
    "SMAPI_MODS_PATH"
)
$savedEnvironment = @{}
foreach ($name in $environmentNames) {
    $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, "Process")
}

$gameProcess = $null
try {
    $env:STARDEWAI_TEST_SAVES = $clonedSavesRoot
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_TEST_AUTO_LOAD = "true"
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $clonedSavesRoot
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $runDirectory
    $env:STARDEWAI_FREEZE_CLOCK_WHILE_EXECUTOR_IDLE = "true"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $env:SMAPI_MODS_PATH = $smokeModsPath

    $gameProcess = Start-Process -FilePath $smapiExecutable `
        -WorkingDirectory $gameDirectory -WindowStyle Hidden -PassThru
    $initial = Wait-FullSnapshot -Url $snapshotUrl -TimeoutSeconds $StartupTimeoutSeconds
    $fixture = Invoke-Execution -Url $executorUrl -TimeoutSeconds 60 -Request `
        (New-ExecutionRequest "debug.setup_friendship_transition_fixture" "$RunId.fixture" `
            ([string]$initial.Parsed.state_hash) $clonedSavesRoot)
    Write-Utf8NoBom -Path $fixturePath -Content ($fixture | ConvertTo-Json -Depth 32)
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") {
        throw "Friendship transition fixture failed: $(@($fixture.block_reasons) -join ',')"
    }

    $transitionSummaries = @()
    for ($index = 0; $index -lt $TransitionCount; $index++) {
        $dayNumber = $index + 1
        $dayDirectory = Join-Path $runDirectory ("day-{0:D2}" -f $dayNumber)
        New-Item -ItemType Directory -Force -Path $dayDirectory | Out-Null
        $current = Wait-FullSnapshot -Url $snapshotUrl -TimeoutSeconds 30
        $prepare = Invoke-Execution -Url $executorUrl -TimeoutSeconds 60 -Request `
            (New-ExecutionRequest "debug.prepare_partnership_sleep" `
                "$RunId.day-$dayNumber.prepare" ([string]$current.Parsed.state_hash) $clonedSavesRoot)
        Write-Utf8NoBom -Path (Join-Path $dayDirectory "prepare-sleep-result.json") `
            -Content ($prepare | ConvertTo-Json -Depth 32)
        if ($prepare.status -ne "applied" -or $prepare.primitive_verification_status -ne "verified") {
            throw "Native sleep preparation failed on transition ${dayNumber}: $(@($prepare.block_reasons) -join ',')"
        }

        $before = Wait-FullSnapshot -Url $snapshotUrl -TimeoutSeconds 30
        $beforePath = Join-Path $dayDirectory "before-full-snapshot.json"
        Write-Utf8NoBom -Path $beforePath -Content $before.Raw
        $beforeDays = [int]$before.Parsed.state.time.total_days.value
        $sleep = Invoke-Execution -Url $executorUrl -TimeoutSeconds 180 -Request `
            (New-ExecutionRequest "executor.sleep" "$RunId.day-$dayNumber.sleep" `
                ([string]$before.Parsed.state_hash) $clonedSavesRoot)
        Write-Utf8NoBom -Path (Join-Path $dayDirectory "sleep-result.json") `
            -Content ($sleep | ConvertTo-Json -Depth 32)
        $sleepBlockReasons = @($sleep.block_reasons)
        $acceptedDialogueBoundary = $sleep.status -eq "blocked" -and
            $sleepBlockReasons.Count -eq 1 -and
            [string]$sleepBlockReasons[0] -like "post_sleep_dialogue_unsafe:*" -and
            [string]$sleep.observed_effect -like "*total_days=$($beforeDays + 1)*"
        $sleepVerified = $sleep.status -eq "applied" -and
            $sleep.primitive_verification_status -eq "verified"
        if (-not $sleepVerified -and -not $acceptedDialogueBoundary) {
            throw "Native sleep failed on transition ${dayNumber}: $(@($sleep.block_reasons) -join ',')"
        }

        $after = Wait-FullSnapshot -Url $snapshotUrl -TimeoutSeconds 90 `
            -ExpectedTotalDays ($beforeDays + 1)
        $afterPath = Join-Path $dayDirectory "after-full-snapshot.json"
        $auditPath = Join-Path $dayDirectory "friendship-day-transition-audit.json"
        Write-Utf8NoBom -Path $afterPath -Content $after.Raw
        & dotnet run --project $bootstrapProject --no-build -- `
            audit-friendship-day-transition --before $beforePath --after $afterPath --output $auditPath
        if ($LASTEXITCODE -ne 0) {
            throw "Friendship transition audit failed on transition $dayNumber."
        }

        $audit = Get-Content -LiteralPath $auditPath -Raw | ConvertFrom-Json
        if ($audit.status -ne "pass" -or [int]$audit.verifiedNpcCount -le 0 -or
            [int]$audit.mismatchCount -ne 0) {
            throw "Friendship transition mismatch on transition $dayNumber."
        }
        $beforeLinus = Find-FriendshipRow -Snapshot $before.Parsed -NpcName "Linus"
        $afterLinus = Find-FriendshipRow -Snapshot $after.Parsed -NpcName "Linus"
        $auditLinus = @($audit.rows) |
            Where-Object { [string]$_.npcName -eq "Linus" } |
            Select-Object -First 1
        if ($null -eq $beforeLinus -or $null -eq $afterLinus -or $null -eq $auditLinus) {
            throw "Linus transition evidence missing on transition $dayNumber."
        }

        $transitionSummaries += [pscustomobject][ordered]@{
            transition_index = $dayNumber
            total_days_before = [int]$audit.beforeTotalDays
            total_days_after = [int]$audit.afterTotalDays
            verified_npc_count = [int]$audit.verifiedNpcCount
            verified_friendship_row_count = [int]$audit.verifiedFriendshipRowCount
            mismatch_count = [int]$audit.mismatchCount
            linus_points_before = [int]$beforeLinus.friendship_points
            linus_points_after = [int]$afterLinus.friendship_points
            linus_talked_before = [bool]$beforeLinus.talked_to_today
            linus_talked_after = [bool]$afterLinus.talked_to_today
            linus_gifts_today_before = [int]$beforeLinus.gifts_today
            linus_gifts_today_after = [int]$afterLinus.gifts_today
            linus_gifts_week_before = [int]$beforeLinus.gifts_this_week
            linus_gifts_week_after = [int]$afterLinus.gifts_this_week
            linus_transition_reasons = @($auditLinus.pointTransitionReasons)
            sleep_executor_status = [string]$sleep.status
            accepted_post_sleep_dialogue_boundary = $acceptedDialogueBoundary
            audit_path = $auditPath
        }
    }

    $weeklyBonusCovered = @($transitionSummaries | Where-Object {
        $_.linus_transition_reasons -contains "weekly_two_gift_bonus" -and
        $_.linus_gifts_week_before -eq 2 -and $_.linus_gifts_week_after -eq 0
    }).Count -gt 0
    $dailyResetCovered = @($transitionSummaries | Where-Object {
        $_.linus_talked_before -and -not $_.linus_talked_after -and
        $_.linus_gifts_today_before -gt 0 -and $_.linus_gifts_today_after -eq 0
    }).Count -gt 0
    $ordinaryDecayCovered = @($transitionSummaries | Where-Object {
        $_.linus_transition_reasons -contains "ordinary_not_talked_below_2500"
    }).Count -gt 0
    $sourceFingerprintAfter = Get-SaveFingerprint -Path $sourceSavePath
    $sourceSaveUntouched = $sourceFingerprintAfter -eq $sourceFingerprintBefore
    $passed = $transitionSummaries.Count -eq $TransitionCount -and
        @($transitionSummaries | Where-Object { $_.mismatch_count -ne 0 }).Count -eq 0 -and
        $weeklyBonusCovered -and $dailyResetCovered -and $ordinaryDecayCovered -and
        $sourceSaveUntouched
    $summary = [ordered]@{
        schema_version = "stardewai.runtime_friendship_multi_day_smoke.v1"
        status = if ($passed) { "passed" } else { "failed" }
        run_id = $RunId
        source_save_path = $sourceSavePath
        cloned_save_path = $clonedSavePath
        transition_count = $transitionSummaries.Count
        expected_transition_count = $TransitionCount
        weekly_two_gift_bonus_covered = $weeklyBonusCovered
        daily_talk_and_gift_reset_covered = $dailyResetCovered
        ordinary_not_talked_decay_covered = $ordinaryDecayCovered
        source_save_untouched = $sourceSaveUntouched
        source_save_fingerprint_before = $sourceFingerprintBefore
        source_save_fingerprint_after = $sourceFingerprintAfter
        mismatch_count = [int](($transitionSummaries | Measure-Object -Property mismatch_count -Sum).Sum)
        transitions = $transitionSummaries
        fixture_path = $fixturePath
        smoke_mods_path = $smokeModsPath
    }
    Write-Utf8NoBom -Path $summaryPath -Content ($summary | ConvertTo-Json -Depth 32)
    $summary | ConvertTo-Json -Depth 32
    if (-not $passed) {
        throw "Runtime friendship multi-day smoke failed: $runDirectory"
    }
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], "Process")
    }
    if ($null -ne $gameProcess -and -not $gameProcess.HasExited) {
        Stop-Process -Id $gameProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
