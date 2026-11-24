param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$PriorRuntimeAttempt,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9-]+$')][string]$RuntimeAttempt,
    [Parameter(Mandatory)][string]$ServicesDirectory
)
$ErrorActionPreference = 'Stop'
$prior = Join-Path $RunDirectory $PriorRuntimeAttempt
$session = Get-Content -LiteralPath (Join-Path $prior 'live-session.json') -Raw | ConvertFrom-Json -DateKind String
$startup = Get-Content -LiteralPath (Join-Path $prior 'startup.json') -Raw | ConvertFrom-Json -AsHashtable
$settings = Get-Content -LiteralPath (Join-Path $prior 'runtime-settings.json') -Raw | ConvertFrom-Json -AsHashtable
$build = Get-Content -LiteralPath (Join-Path $ServicesDirectory 'build-identity.json') -Raw | ConvertFrom-Json
if ($build.focused_tests.failed -ne 0 -or $build.focused_tests.passed -lt 1) { throw 'Aligned service tests have not passed.' }
foreach ($file in $build.files) {
    if ((Get-FileHash -LiteralPath $file.path).Hash -cne $file.sha256) { throw 'A published service changed.' }
}
foreach ($row in $session.processes) {
    $process = Get-Process -Id $row.id
    if ($process.Path -cne $row.exe -or $process.StartTime.ToUniversalTime() -ne [DateTimeOffset]::Parse($row.started).UtcDateTime) { throw 'Original process identity changed.' }
}
$controllers = @(Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" | Where-Object {
    $_.CommandLine -like '*StardewAI.LiveTrainingLoop.dll*' -and $_.CommandLine -like ('*' + $session.run_id + '*')
})
if ($controllers.Count) { throw 'A native controller is still live; preserve its exact handle.' }
$gameRow = $session.processes | Where-Object role -ceq 'game'
$mod = Join-Path (Split-Path -Parent $gameRow.exe) 'Mods\StardewAI.RuntimeTestHarness'
$contract = (Get-FileHash -LiteralPath (Join-Path $mod 'StardewAI.Contracts.dll')).Hash
foreach ($kind in 'backend','product-executor') {
    if ((Get-FileHash -LiteralPath (Join-Path $ServicesDirectory ($kind + '\StardewAI.Contracts.dll'))).Hash -cne $contract) { throw 'New service contracts differ from the native runtime.' }
}
$beforeBody = (Invoke-WebRequest -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 20).Content
$before = $beforeBody | ConvertFrom-Json
if ($before.save_id.value -cne 'ProofFarm' -or $before.player_id.value -cne '319842636613009663' -or $before.state.environment.training_run_id.value -cne $session.run_id) { throw 'The live game has a different identity.' }
$out = New-Item -ItemType Directory -Path (Join-Path $RunDirectory $RuntimeAttempt)
[IO.File]::WriteAllText((Join-Path $out.FullName 'before-service-transition.json'), $beforeBody)
$settings | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $out.FullName 'runtime-settings.json') -Encoding utf8
foreach ($name in 'verified-adjacent-chest-entry.json','verified-farm-chest-entry.json') { Copy-Item -LiteralPath (Join-Path $prior $name) -Destination (Join-Path $out.FullName $name) }
$intent = [ordered]@{prior_session=(Join-Path $prior 'live-session.json');published_identity=(Join-Path $ServicesDirectory 'build-identity.json');published_identity_sha256=(Get-FileHash -LiteralPath (Join-Path $ServicesDirectory 'build-identity.json')).Hash;game_process_unchanged=$true;before_state_hash=$before.state_hash}
$intent | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $out.FullName 'service-transition-intent.json') -Encoding utf8
foreach ($role in 'product','backend') {
    $row = $session.processes | Where-Object role -ceq $role
    Stop-Process -Id $row.id
    Wait-Process -Id $row.id -Timeout 10 -ErrorAction SilentlyContinue
    if (Get-Process -Id $row.id -ErrorAction SilentlyContinue) { throw 'Prior service did not stop.' }
}
if (Get-NetTCPConnection -LocalPort 18413,18414 -State Listen -ErrorAction SilentlyContinue) { throw 'A service endpoint is still owned.' }
$dotnet = ($session.processes | Where-Object role -ceq 'backend').exe
foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key, $settings[$key], 'Process') }
$backendPath = Join-Path $ServicesDirectory 'backend\StardewAI.Backend.dll'
$productPath = Join-Path $ServicesDirectory 'product-executor\StardewAI.ProductExecutor.dll'
$backend = Start-Process -FilePath $dotnet -ArgumentList @($backendPath) -WorkingDirectory (Split-Path -Parent $backendPath) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $out.FullName 'backend.stdout.log') -RedirectStandardError (Join-Path $out.FullName 'backend.stderr.log')
[Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', $settings.STARDEWAI_PRODUCT_EXECUTOR_URL, 'Process')
$product = Start-Process -FilePath $dotnet -ArgumentList @($productPath) -WorkingDirectory (Split-Path -Parent $productPath) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $out.FullName 'product.stdout.log') -RedirectStandardError (Join-Path $out.FullName 'product.stderr.log')
[Environment]::SetEnvironmentVariable('ASPNETCORE_URLS', $settings.ASPNETCORE_URLS, 'Process')
$processes = @($gameRow) + @(foreach ($entry in @(@{role='backend';p=$backend},@{role='product';p=$product})) { [ordered]@{role=$entry.role;id=$entry.p.Id;exe=$entry.p.Path;started=$entry.p.StartTime.ToUniversalTime().ToString('o')} })
[ordered]@{run_id=$session.run_id;save_root=$session.save_root;slot=$session.slot;processes=$processes} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $out.FullName 'live-session.json') -Encoding utf8
$startup.backend=@{path=$backendPath;sha256=(Get-FileHash -LiteralPath $backendPath).Hash}
$startup.product=@{path=$productPath;sha256=(Get-FileHash -LiteralPath $productPath).Hash}
$startup.processes=$processes
$startup['service_only_alignment']=$true
$startup['prior_runtime_attempt']=$PriorRuntimeAttempt
$startup | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $out.FullName 'startup.json') -Encoding utf8
$ready = $false
foreach ($attempt in 1..20) {
    if ($backend.HasExited -or $product.HasExited) { throw 'A recorded new service exited; inspect this attempt without restarting.' }
    try {
        $null = Invoke-RestMethod -Uri ($settings.ASPNETCORE_URLS + '/health') -TimeoutSec 2
        $null = Invoke-RestMethod -Uri ($settings.STARDEWAI_PRODUCT_EXECUTOR_URL + '/health') -TimeoutSec 2
        $ready = $true; break
    } catch { Start-Sleep -Milliseconds 500 }
}
if (!$ready) { throw 'Recorded service readiness is not verified; inspect its existing handles.' }
$afterBody = (Invoke-WebRequest -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 20).Content
$after = $afterBody | ConvertFrom-Json
[IO.File]::WriteAllText((Join-Path $out.FullName 'after-service-transition.json'), $afterBody)
$sameInventory = ($before.state.player.inventory.value | ConvertTo-Json -Depth 20 -Compress) -ceq ($after.state.player.inventory.value | ConvertTo-Json -Depth 20 -Compress)
if (!$sameInventory -or $before.state_hash -cne $after.state_hash -or !(Get-Process -Id $gameRow.id)) { throw 'Game state changed during service alignment; retain both snapshots for inspection.' }
[ordered]@{verified_at=(Get-Date).ToString('o');same_game_process_verified=$true;same_native_state_hash_verified=$true;same_native_inventory_verified=$sameInventory;state_hash=$after.state_hash;total_day=$after.state.time.total_days.value;time=$after.state.time.time.value;cash=$after.state.player.money.value;location=$after.state.player.location_id.value;contract_sha256=$contract;game_not_reloaded=$true;processes=$processes;formal_training_authorized=$false} |
    ConvertTo-Json -Depth 7 | Tee-Object -FilePath (Join-Path $out.FullName 'service-transition-verification.json')
