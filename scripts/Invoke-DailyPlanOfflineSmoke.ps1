param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $Root = (Join-Path $ProjectRoot "artifacts\daily-plan-offline-smoke"),
    [int] $Port = 5118
)

$ErrorActionPreference = "Stop"

function FieldJson($value, [string] $status = "available") {
    $sourceKind = if ($status -eq "available") { "game_object" } else { "unavailable" }
    $field = [ordered]@{
        value = $value
        status = $status
        source = [ordered]@{ kind = $sourceKind; path = "offline-smoke" }
        adapter = "offline-smoke"
        read_at_tick = 100
        confidence = if ($status -eq "available") { 1.0 } else { 0.0 }
    }
    if ($status -ne "available") {
        $field.reason = "offline_smoke_unavailable"
    }
    return $field
}

function JsonString([string] $value) {
    return ($value | ConvertTo-Json -Compress)
}

function CanonicalJson($value) {
    if ($null -eq $value) {
        return "null"
    }
    if ($value -is [string]) {
        return JsonString $value
    }
    if ($value -is [bool]) {
        return ($value | ConvertTo-Json -Compress).ToLowerInvariant()
    }
    if ($value -is [int] -or $value -is [long] -or $value -is [double] -or $value -is [decimal]) {
        return [System.Convert]::ToString($value, [System.Globalization.CultureInfo]::InvariantCulture)
    }
    if ($value -is [System.Collections.IDictionary]) {
        $parts = @()
        foreach ($key in ($value.Keys | Sort-Object)) {
            $parts += (JsonString ([string] $key)) + ":" + (CanonicalJson $value[$key])
        }
        return "{" + ($parts -join ",") + "}"
    }
    if ($value -is [System.Collections.IEnumerable] -and -not ($value -is [string])) {
        $parts = @()
        foreach ($item in $value) {
            $parts += CanonicalJson $item
        }
        return "[" + ($parts -join ",") + "]"
    }
    return ($value | ConvertTo-Json -Depth 64 -Compress)
}

function ComputeHash($state) {
    $canonical = CanonicalJson $state
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($canonical)
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        return -join ($sha.ComputeHash($bytes) | ForEach-Object { $_.ToString("x2") })
    }
    finally {
        $sha.Dispose()
    }
}

function WaitBackend([string] $url) {
    for ($i = 0; $i -lt 60; $i++) {
        try {
            Invoke-RestMethod -Uri "$url/health" -Method Get -TimeoutSec 2 | Out-Null
            return
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    }
    throw "Backend did not become healthy at $url"
}

$backendUrl = "http://127.0.0.1:$Port"
$runId = "offline-smoke-" + (Get-Date -Format "yyyyMMddHHmmss")
$snapshotPath = Join-Path $Root "offline-snapshot.json"
$datasetPath = Join-Path $Root "datasets\live-training-feature-rows.jsonl"
$backendStdout = Join-Path $Root "backend.stdout.log"
$backendStderr = Join-Path $Root "backend.stderr.log"
New-Item -ItemType Directory -Force -Path $Root | Out-Null

$state = [ordered]@{
    environment = [ordered]@{
        game_version = FieldJson "1.6.15"
        smapi_version = FieldJson "4.5.2"
        bridge_version = FieldJson "offline-smoke"
        training_mode = FieldJson "1"
        training_run_id = FieldJson $runId
        save_isolation_path = FieldJson (Join-Path $Root "saves")
        installed_mods = FieldJson @()
    }
    identity = [ordered]@{
        save_id = FieldJson "OfflineSmokeFarm"
        player_id = FieldJson "offline"
    }
    time = [ordered]@{
        year = FieldJson 3
        season = FieldJson "spring"
        day = FieldJson 1
        time = FieldJson 610
        weather = FieldJson "sun"
    }
    player = [ordered]@{
        location_id = FieldJson "Farm"
        tile_x = FieldJson 64
        tile_y = FieldJson 15
        facing_direction = FieldJson 2
        money = FieldJson 500
        total_money_earned = FieldJson 1000000
        health = FieldJson 100
        max_health = FieldJson 100
        energy = FieldJson 270
        stamina = FieldJson 270
        max_energy = FieldJson 270
        level = FieldJson 25
        current_tool = FieldJson "(T)WateringCan"
        inventory = FieldJson @()
    }
    mods = [ordered]@{
        installed_count = FieldJson 0
        installed_mods = FieldJson @()
    }
    game = [ordered]@{
        current_location = FieldJson "Farm"
        time_of_day = FieldJson 610
    }
    farm = [ordered]@{
        grandpa_score = FieldJson 3
        crops = FieldJson @(
            [ordered]@{ tile_x = 1; tile_y = 2; needs_watering = $true; watered = $false },
            [ordered]@{ tile_x = 3; tile_y = 4; needs_watering = $true; watered = $false }
        )
    }
    current_location = [ordered]@{
        identity = FieldJson ([ordered]@{ name = "Farm"; name_or_unique_name = "Farm"; type = "StardewValley.Farm" })
    }
    npcs = [ordered]@{
        positions = FieldJson @()
        friendships = FieldJson @()
        schedules = FieldJson $null "unavailable"
    }
    quests = [ordered]@{
        active_quests = FieldJson @()
        mail_received = FieldJson @()
        completed_quests = FieldJson $null "unavailable"
    }
    world_progress = [ordered]@{
        community_center = FieldJson ([ordered]@{ location_accessible = $true; completed = $true })
        joja_membership = FieldJson $false
        achievements = FieldJson @(5, 26, 34)
    }
    menus = [ordered]@{
        active_menu = FieldJson ([ordered]@{ is_open = $false; type = "none"; full_type = $null })
    }
    modded_state = [ordered]@{
        installed_count = FieldJson 0
        installed = FieldJson @()
        content_pack_count = FieldJson 0
        content_packs = FieldJson @()
    }
    transport = [ordered]@{
        event_stream_websocket = FieldJson ([ordered]@{ endpoint = "offline-smoke" })
    }
}

$stateHash = ComputeHash $state
$snapshot = [ordered]@{
    schema_version = "snapshot.v1"
    bridge_version = "offline-smoke"
    game_version = "1.6.15"
    smapi_version = "4.5.2"
    installed_mods = @()
    save_id = FieldJson "OfflineSmokeFarm"
    player_id = FieldJson "offline"
    game_tick = 100
    in_game_time = FieldJson 610
    real_timestamp = "2026-07-11T00:00:00Z"
    state_hash = $stateHash
    completeness = "partial"
    unavailable_fields = @("npcs.schedules", "quests.completed_quests")
    state = $state
}
$snapshot | ConvertTo-Json -Depth 80 | Set-Content -Path $snapshotPath -Encoding UTF8

$backend = $null
try {
    $env:ASPNETCORE_URLS = $backendUrl
    $backend = Start-Process -FilePath "dotnet" `
        -ArgumentList @("run", "--no-restore", "--project", (Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"), "--no-launch-profile") `
        -WorkingDirectory $ProjectRoot `
        -WindowStyle Hidden `
        -RedirectStandardOutput $backendStdout `
        -RedirectStandardError $backendStderr `
        -PassThru
    WaitBackend $backendUrl

    dotnet run --no-restore --project (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -- `
        --root $Root `
        --backend-url $backendUrl `
        --snapshot-file $snapshotPath `
        --no-manifest `
        --run-id $runId `
        --save-isolation-path (Join-Path $Root "saves") `
        --iterations 1 `
        --train-every 1 `
        --sleep-ms 0 `
        --use-daily-plan `
        --daily-plan-max-candidates 1 `
        --daily-plan-candidate-options "farm.maintain_crops" `
        --no-executor-feedback-required

    if (!(Test-Path $datasetPath)) {
        throw "Dataset was not written: $datasetPath"
    }
    $lastRow = Get-Content -Path $datasetPath | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Last 1 | ConvertFrom-Json
    $numeric = @($lastRow.action_features.features.numeric)
    $categorical = @($lastRow.action_features.features.categorical)
    $accepted = ($numeric | Where-Object { $_.name -eq "candidate_audit.accepted_count" }).value
    $skippedMax = ($numeric | Where-Object { $_.name -eq "candidate_audit.skipped_max_candidates_count" }).value
    $primarySkip = ($categorical | Where-Object { $_.name -eq "candidate_audit.primary_skip_reason" }).value
    if ($accepted -lt 1 -or $skippedMax -lt 1 -or $primarySkip -ne "max_candidates_reached") {
        throw "Candidate audit feature check failed: accepted=$accepted skippedMax=$skippedMax primarySkip=$primarySkip"
    }

    [ordered]@{
        status = "ok"
        root = (Resolve-Path $Root).Path
        dataset_path = (Resolve-Path $datasetPath).Path
        snapshot_path = (Resolve-Path $snapshotPath).Path
        accepted_count = $accepted
        skipped_max_candidates_count = $skippedMax
        primary_skip_reason = $primarySkip
    } | ConvertTo-Json -Depth 8
}
finally {
    if ($LASTEXITCODE -ne 0 -or !(Test-Path $datasetPath)) {
        if (Test-Path $backendStdout) {
            Write-Host "backend stdout tail:"
            Get-Content -Path $backendStdout -Tail 80
        }
        if (Test-Path $backendStderr) {
            Write-Host "backend stderr tail:"
            Get-Content -Path $backendStderr -Tail 80
        }
    }
    if ($backend -and !$backend.HasExited) {
        Stop-Process -Id $backend.Id -Force
    }
    Remove-Item Env:\ASPNETCORE_URLS -ErrorAction SilentlyContinue
}
