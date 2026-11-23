param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$RuntimeAttempt,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$Attempt,
    [Parameter(Mandatory, ParameterSetName='Withdraw')][string]$ItemId,
    [Parameter(Mandatory, ParameterSetName='Deposit')][string[]]$DepositItemIds,
    [Parameter(ParameterSetName='Deposit')][hashtable]$DepositItemQualities = @{}
)

$ErrorActionPreference = 'Stop'
$runtime = Join-Path $RunDirectory $RuntimeAttempt
$settings = Get-Content -LiteralPath (Join-Path $runtime 'runtime-settings.json') -Raw | ConvertFrom-Json -AsHashtable
$session = Get-Content -LiteralPath (Join-Path $runtime 'live-session.json') -Raw | ConvertFrom-Json -DateKind String
$stand = Get-Content -LiteralPath (Join-Path $runtime 'verified-adjacent-chest-entry.json') -Raw | ConvertFrom-Json
$proof = New-Item -ItemType Directory -Path (Join-Path $RunDirectory ($Attempt + '-approach'))
$url = $settings.ASPNETCORE_URLS
$bridge = $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL
$goal = 'goal.world_progress.complete_community_center'
foreach ($service in $session.processes) {
    $process = Get-Process -Id $service.id
    if ($process.Path -cne $service.exe -or $process.StartTime.ToUniversalTime() -ne [DateTimeOffset]::Parse($service.started).UtcDateTime) {
        throw 'Original isolation process identity changed.'
    }
}
$body = (Invoke-WebRequest -Uri $bridge -TimeoutSec 15).Content
$fresh = $body | ConvertFrom-Json
$chest = @($fresh.state.farm.material_inventory_graph.value.access_points | Where-Object node_id -ceq $stand.chest_node_id)
if ($fresh.state.player.location_id.value -cne 'Farm' -or $chest.Count -ne 1 -or !$chest[0].actor_use_authorized -or
    ([math]::Abs($stand.stand_x - $chest[0].tile_x) + [math]::Abs($stand.stand_y - $chest[0].tile_y)) -ne 1) {
    throw 'The verified Farm chest or its retained access tile changed.'
}
if ($fresh.state.player.tile_x.value -ne $stand.stand_x -or $fresh.state.player.tile_y.value -ne $stand.stand_y) {
    [IO.File]::WriteAllText((Join-Path $proof.FullName 'before.json'), $body)
    $null = Invoke-RestMethod -Uri ($url + '/api/v1/snapshots') -Method Post -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body)) -TimeoutSec 45
    $action = @{
        schema_version = 'small_model_action.v1'; model_output_id = 'native-chest-withdrawal-approach'; source_model = 'retained_native_chest_access'
        state_hash = $fresh.state_hash; goal_id = $goal; execution_mode = 'training_singleplayer'
        actor = @{ actor_id = 'training_farmer.main'; actor_type = 'training_farmer'; control_surface = 'training_sandbox' }
        actions = @(@{ action_id = 'reach-verified-chest-tile'; option_id = 'executor.move_to_tile'; parameters = @(
            @{ name = 'target_location'; value = 'Farm' },
            @{ name = 'target_tile_x'; value = $stand.stand_x.ToString() },
            @{ name = 'target_tile_y'; value = $stand.stand_y.ToString() }
        )})
    }
    $queue = Invoke-RestMethod -Uri ($url + '/api/v1/small-model/action-queue/compile') -Method Post -ContentType 'application/json' -Body ($action | ConvertTo-Json -Depth 15 -Compress) -TimeoutSec 40
    if ($queue.status -ne 'pending' -or $queue.state_hash -cne $fresh.state_hash -or @($queue.items).Count -ne 1) {
        throw 'Original compiler could not bind the live path to the verified access tile.'
    }
    $path = Join-Path $proof.FullName 'approach-queue.json'
    $queue | ConvertTo-Json -Depth 40 | Set-Content -LiteralPath $path -Encoding utf8
    $nativeArgs = Get-Content -LiteralPath (Join-Path $RunDirectory 'spring27-chest-adapter-replay\execution-arguments.json') -Raw | ConvertFrom-Json
    $queueArg = [array]::IndexOf($nativeArgs, '--precompiled-queue') + 1
    if (!$queueArg) { throw 'The retained native controller argument is missing.' }
    $nativeArgs[$queueArg] = $path
    $retentionArg = [array]::IndexOf($nativeArgs, '--max-persisted-iterations') + 1
    if ($retentionArg) { $nativeArgs[$retentionArg] = '400' }
    foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process') }
    & 'D:\StardewAI-TestLab\dotnet\dotnet.exe' @nativeArgs *> (Join-Path $proof.FullName 'approach-native.log')
    if ($LASTEXITCODE -ne 0) { throw 'The exact chest approach did not verify.' }
}
$after = Invoke-RestMethod -Uri $bridge -TimeoutSec 15
if ($after.state.player.location_id.value -cne 'Farm' -or $after.state.player.tile_x.value -ne $stand.stand_x -or $after.state.player.tile_y.value -ne $stand.stand_y) {
    throw 'The actor did not reach the verified native chest stand tile.'
}
$entry = Join-Path $proof.FullName 'adjacent-source.json'
[ordered]@{ after=$after.state_hash; chest_node_id=$stand.chest_node_id; time=$after.in_game_time.value } | ConvertTo-Json | Set-Content -LiteralPath $entry
$withdraw = $PSCmdlet.ParameterSetName -eq 'Withdraw'
$items = if ($withdraw) { @($ItemId) } else { $DepositItemIds }
& (Join-Path $PSScriptRoot 'Invoke-IsolatedChestBootstrap.ps1') -RunDirectory $RunDirectory -RuntimeAttempt $RuntimeAttempt -Attempt $Attempt -VerifiedPlacementPath $entry -ItemIds $items -ItemQualities $DepositItemQualities -Withdraw:$withdraw
