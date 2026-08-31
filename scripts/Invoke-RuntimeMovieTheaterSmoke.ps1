param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-movie-theater-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [switch] $KeepGameRunning
)

$ErrorActionPreference = "Stop"

function Invoke-JsonPost([string] $Url, $Body, [int] $TimeoutSeconds = 900) {
    Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" `
        -Body ($Body | ConvertTo-Json -Depth 96) -TimeoutSec $TimeoutSeconds
}

function Wait-Json([string] $Url, [int] $TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $result = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 10
            if ($null -ne $result) { return $result }
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Url. Last error: $lastError"
}

function Wait-MovieSnapshot([int] $TimeoutSeconds, [scriptblock] $Predicate) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    $lastError = "not_requested"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Method Get -Uri $snapshotUrl -TimeoutSec 10
            $field = $snapshot.state.player.movie_theater
            if ($snapshot.save_id.status -in @("available", "derived") -and
                $field.status -in @("available", "derived") -and
                [string]$field.value.projection_status -eq "complete_locked_base_1.6.15" -and
                (& $Predicate $snapshot $field.value)) {
                return $snapshot
            }
            $lastError = "location=" + [string]$snapshot.state.player.location_id.value +
                ";service=" + [string]$field.value.service_status +
                ";event=" + [string]$field.value.active_event_id
        }
        catch { $lastError = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for movie theater snapshot. Last error: $lastError"
}

function New-BaseRequest($Snapshot, [string] $OptionId, [string] $QueueItemId) {
    [ordered]@{
        schema_version = "training_execution_request.v1"
        run_id = $RunId
        queue_id = "runtime-movie-theater-smoke"
        queue_item_id = $QueueItemId
        before_state_hash = $Snapshot.state_hash
        option_id = $OptionId
        execution_mode = "training_singleplayer"
        actor = "training_farmer.main"
        save_isolation_path = $savesPath
        request_nonce = [guid]::NewGuid().ToString("N")
        created_at = [DateTimeOffset]::UtcNow.ToString("O")
    }
}

function Get-AvailableStand($Endpoint, $Snapshot) {
    $px = [int]$Snapshot.state.player.tile_x.value
    $py = [int]$Snapshot.state.player.tile_y.value
    $available = @($Endpoint.stand_tiles | Where-Object { $_.available -and $_.path_reachable } |
        Sort-Object @{ Expression = { [int]$_.path_length }; Ascending = $true })
    $current = @($available | Where-Object { [int]$_.tile_x -eq $px -and [int]$_.tile_y -eq $py }) | Select-Object -First 1
    if ($null -ne $current) { return $current }
    $stand = $available | Select-Object -First 1
    if ($null -eq $stand) { throw "No available stand tile for $($Endpoint.action_token)." }
    return $stand
}

function Write-MovieSnapshotArtifact($Snapshot, [string] $Path) {
    [ordered]@{
        state_hash = [string]$Snapshot.state_hash
        save_id = $Snapshot.save_id
        location_id = $Snapshot.state.player.location_id
        tile_x = $Snapshot.state.player.tile_x
        tile_y = $Snapshot.state.player.tile_y
        movie_theater = $Snapshot.state.player.movie_theater
    } | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8
}

function New-MovieRequest(
    $Snapshot,
    [string] $Stage,
    $Endpoint,
    $Stand,
    [string] $QueueItemId,
    [int] $TicketSlot = -1,
    [int] $TicketStack = -1
) {
    $context = $Snapshot.state.player.movie_theater.value
    $request = New-BaseRequest $Snapshot "executor.watch_movie" $QueueItemId
    $request.location_id = [string]$Snapshot.state.player.location_id.value
    $request.target_location = [string]$Snapshot.state.player.location_id.value
    $request.target_tile_x = [int]$Endpoint.tile_x
    $request.target_tile_y = [int]$Endpoint.tile_y
    $request.stand_tile_x = [int]$Stand.tile_x
    $request.stand_tile_y = [int]$Stand.tile_y
    $request.max_movement_tiles = 512
    $request.movie_stage = $Stage
    $request.movie_projection_fingerprint = [string]$context.projection_fingerprint
    $request.movie_id = $movieId
    $request.movie_guest_name = $guestName
    $request.movie_concession_id = $concessionId
    $request.movie_objective_key = $objectiveKey
    $request.movie_friendship_effective = $movieFriendship
    $request.movie_concession_friendship_effective = $concessionFriendship
    $request.native_contract = [string]$context.native_contract
    if ($null -ne $Endpoint.action_raw) {
        $request.movie_action_raw = [string]$Endpoint.action_raw
        $request.movie_action_token = [string]$Endpoint.action_token
    }
    if ($TicketSlot -ge 0) {
        $request.movie_ticket_slot_index = $TicketSlot
        $request.movie_ticket_stack_before = $TicketStack
    }
    return $request
}

$gameDir = Join-Path $RuntimeRoot "Stardew Valley"
$smapiExe = Join-Path $gameDir "StardewModdingAPI.exe"
$savesPath = Join-Path $RuntimeRoot "saves"
$snapshotUrl = "http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"
$executorUrl = "http://127.0.0.1:8767/api/v1/training/execute"
if (-not (Test-Path -LiteralPath $smapiExe -PathType Leaf)) { throw "SMAPI executable not found: $smapiExe" }
if ([string]::IsNullOrWhiteSpace($SaveSlot)) {
    $SaveSlot = (Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name
}
foreach ($port in @(8765, 8767)) {
    if ($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)) {
        throw "Port $port is already listening. Refusing to attach."
    }
}
if ($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)) {
    throw "StardewModdingAPI is already running. Refusing to attach or start."
}

$artifactDirectory = Join-Path $ProjectRoot ("artifacts\runtime-movie-theater-smoke\" + $RunId)
$trainingOutputDirectory = Join-Path $artifactDirectory "training-output"
New-Item -ItemType Directory -Force -Path $trainingOutputDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot | Out-Null

$names = @(
    "STARDEWAI_TEST_SAVES", "STARDEWAI_TEST_SLOT", "STARDEWAI_SAVE_ISOLATION_PATH",
    "STARDEWAI_TRAINING_RUN_ID", "STARDEWAI_TRAINING_MODE", "STARDEWAI_TRAINING_OUTPUT_DIR",
    "STARDEWAI_SUPPRESS_LOCAL_RENDER", "SDL_AUDIODRIVER", "ALSOFT_DRIVERS"
)
$savedEnvironment = @{}
foreach ($name in $names) { $savedEnvironment[$name] = [Environment]::GetEnvironmentVariable($name) }
$process = $null
try {
    $env:STARDEWAI_TEST_SAVES = $savesPath
    $env:STARDEWAI_TEST_SLOT = $SaveSlot
    $env:STARDEWAI_SAVE_ISOLATION_PATH = $savesPath
    $env:STARDEWAI_TRAINING_RUN_ID = $RunId
    $env:STARDEWAI_TRAINING_MODE = "1"
    $env:STARDEWAI_TRAINING_OUTPUT_DIR = $trainingOutputDirectory
    $env:STARDEWAI_SUPPRESS_LOCAL_RENDER = "1"
    $env:SDL_AUDIODRIVER = "dummy"
    $env:ALSOFT_DRIVERS = "null"
    $process = Start-Process -FilePath $smapiExe -WorkingDirectory $gameDir -WindowStyle Hidden -PassThru
    Wait-Json "http://127.0.0.1:8767/health" 60 | Out-Null
    $loaded = Wait-MovieSnapshot 180 { param($snapshot, $context) $true }

    $setup = New-BaseRequest $loaded "debug.setup_movie_theater" "setup"
    $setupResult = Invoke-JsonPost $executorUrl $setup
    if ($setupResult.status -ne "applied" -or $setupResult.primitive_verification_status -ne "verified") {
        throw "Movie theater fixture setup failed: $($setupResult | ConvertTo-Json -Depth 32 -Compress)"
    }

    $before = Wait-MovieSnapshot 45 {
        param($snapshot, $context)
        [string]$snapshot.state.player.location_id.value -eq "Town" -and
            [int]$context.movie_ticket_count -eq 2 -and
            $null -ne (@($context.guest_options | Where-Object { $_.guest_name -eq "Abigail" -and $_.can_invite_now }) | Select-Object -First 1)
    }
    Write-MovieSnapshotArtifact $before (Join-Path $artifactDirectory "before-snapshot.json")
    $context = $before.state.player.movie_theater.value
    $guest = @($context.guest_options | Where-Object { $_.guest_name -eq "Abigail" }) | Select-Object -First 1
    $concession = @($guest.concessions | Sort-Object @{ Expression = { [int]$_.friendship_effective }; Descending = $true }, @{ Expression = { [int]$_.price }; Descending = $false }) | Select-Object -First 1
    if ($null -eq $guest -or $null -eq $concession) { throw "Movie guest or concession projection missing." }
    $movieId = [string]$context.movie_id
    $guestName = [string]$guest.guest_name
    $concessionId = [string]$concession.concession_id
    $objectiveKey = "$movieId`:$guestName`:$concessionId"
    $movieFriendship = [int]$guest.movie_friendship_effective
    $concessionFriendship = [int]$concession.friendship_effective
    $friendshipBefore = [int]$guest.friendship_points_before
    $week = [int]$context.total_week

    $ticket = @($context.movie_ticket_slots) | Select-Object -First 1
    $inviteEndpoint = [pscustomobject]@{ tile_x = [int]$guest.tile_x; tile_y = [int]$guest.tile_y }
    $inviteStand = [pscustomobject]@{ tile_x = [int]$before.state.player.tile_x.value; tile_y = [int]$before.state.player.tile_y.value }
    $inviteRequest = New-MovieRequest $before "watch_movie_invite_guest" $inviteEndpoint $inviteStand "invite" ([int]$ticket.slot_index) ([int]$ticket.stack)
    $inviteResult = Invoke-JsonPost $executorUrl $inviteRequest
    if ($inviteResult.status -ne "applied") { throw "Movie invitation failed: $($inviteResult | ConvertTo-Json -Depth 32 -Compress)" }

    $invited = Wait-MovieSnapshot 45 {
        param($snapshot, $movie)
        $movie.current_invitation.guest_name -eq "Abigail" -and -not $movie.current_invitation.fulfilled
    }
    $entranceContext = $invited.state.player.movie_theater.value
    $entrance = @($entranceContext.entrance_action_tiles) | Select-Object -First 1
    $entranceStand = Get-AvailableStand $entrance $invited
    $enterRequest = New-MovieRequest $invited "watch_movie_enter" $entrance $entranceStand "enter"
    $enterResult = Invoke-JsonPost $executorUrl $enterRequest
    if ($enterResult.status -ne "applied") { throw "Movie theater entry failed: $($enterResult | ConvertTo-Json -Depth 32 -Compress)" }

    $inside = Wait-MovieSnapshot 90 {
        param($snapshot, $movie)
        [string]$snapshot.state.player.location_id.value -eq "MovieTheater" -and
            $movie.current_invitation.guest_name -eq "Abigail" -and $movie.current_invitation.fulfilled
    }
    $insideContext = $inside.state.player.movie_theater.value
    $concessionEndpoint = @($insideContext.concession_action_tiles) | Select-Object -First 1
    $concessionStand = Get-AvailableStand $concessionEndpoint $inside
    $concessionRequest = New-MovieRequest $inside "watch_movie_concession" $concessionEndpoint $concessionStand "concession"
    $concessionResult = Invoke-JsonPost $executorUrl $concessionRequest
    if ($concessionResult.status -ne "applied") { throw "Movie concession failed: $($concessionResult | ConvertTo-Json -Depth 32 -Compress)" }

    $ready = Wait-MovieSnapshot 45 {
        param($snapshot, $movie)
        [string]$movie.current_invitation.purchased_concession_id -eq $concessionId
    }
    $readyContext = $ready.state.player.movie_theater.value
    $doors = @($readyContext.screening_door_action_tiles) | Select-Object -First 1
    $doorStand = Get-AvailableStand $doors $ready
    $screenRequest = New-MovieRequest $ready "watch_movie_screening" $doors $doorStand "screening"
    $screenResult = Invoke-JsonPost $executorUrl $screenRequest 900
    $screenResult | ConvertTo-Json -Depth 64 | Set-Content -LiteralPath (Join-Path $artifactDirectory "screening-result.json") -Encoding utf8

    $after = Wait-MovieSnapshot 60 {
        param($snapshot, $movie)
        [int]$movie.player_last_seen_movie_week -ge $week -and [string]$movie.active_event_id -eq ""
    }
    Write-MovieSnapshotArtifact $after (Join-Path $artifactDirectory "after-snapshot.json")
    $afterGuest = @($after.state.player.movie_theater.value.guest_options | Where-Object { $_.guest_name -eq "Abigail" }) | Select-Object -First 1
    $friendshipAfter = [int]$afterGuest.friendship_points_before
    $stageResults = @($inviteResult, $enterResult, $concessionResult, $screenResult)
    $passed = @($stageResults | Where-Object {
        $_.status -eq "applied" -and $_.primitive_verification_status -eq "verified"
    }).Count -eq 4 -and
        [int]$after.state.player.movie_theater.value.player_last_seen_movie_week -ge $week -and
        [int]$afterGuest.last_seen_movie_week -ge $week -and
        $friendshipAfter - $friendshipBefore -eq $movieFriendship + $concessionFriendship
    $summary = [ordered]@{
        status = if ($passed) { "passed" } else { "failed" }
        evidence_id = "EVD-321"
        run_id = $RunId
        save_slot = $SaveSlot
        expected_case_count = 4
        passed_case_count = if ($passed) { 4 } else { 0 }
        movie_id = $movieId
        guest = $guestName
        concession = $concessionId
        friendship_before = $friendshipBefore
        friendship_after = $friendshipAfter
        expected_friendship_delta = $movieFriendship + $concessionFriendship
        cases = @(
            [ordered]@{ stage = "invite"; status = $inviteResult.status; verification = $inviteResult.primitive_verification_status; block_reasons = @($inviteResult.block_reasons) },
            [ordered]@{ stage = "enter"; status = $enterResult.status; verification = $enterResult.primitive_verification_status; block_reasons = @($enterResult.block_reasons) },
            [ordered]@{ stage = "concession"; status = $concessionResult.status; verification = $concessionResult.primitive_verification_status; block_reasons = @($concessionResult.block_reasons) },
            [ordered]@{ stage = "screening"; status = $screenResult.status; verification = $screenResult.primitive_verification_status; block_reasons = @($screenResult.block_reasons) }
        )
    }
    $summary | ConvertTo-Json -Depth 48 | Set-Content -LiteralPath (Join-Path $artifactDirectory "summary.json") -Encoding utf8
    $summary | ConvertTo-Json -Depth 48
    if (-not $passed) { throw "Runtime movie theater smoke failed: $artifactDirectory" }
}
finally {
    if (-not $KeepGameRunning -and $null -ne $process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
        $process.WaitForExit(10000) | Out-Null
    }
    foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name]) }
}
