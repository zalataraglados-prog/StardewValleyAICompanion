param([Parameter(Mandatory)][string]$RunDirectory)
$ErrorActionPreference = 'Stop'
$common = 'D:\StardewValleyAICompanion-step5-native-c42508c1\scripts\lib\RuntimeEvidenceCommon.ps1'
. $common
$run = Get-Item -LiteralPath $RunDirectory
$fixture = Get-Content -LiteralPath (Join-Path $run.FullName 'fixture.json') -Raw | ConvertFrom-Json
$settings = Get-Content -LiteralPath (Join-Path $run.FullName 'runtime-settings.json') -Raw | ConvertFrom-Json -AsHashtable
$lab = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $lab 'published\native-services-c42508c1-20261124\backend\StardewAI.Backend.dll'
$product = Join-Path $lab 'published\native-services-c42508c1-20261124\product-executor\StardewAI.ProductExecutor.dll'
$null = Assert-RuntimeFileIdentity -Path $backend -ExpectedSha256 '706B24A269BA505FA44B932305F0F469DFBB37D2A44D2F5536E04984125AD368' -Label 'Backend'
$null = Assert-RuntimeFileIdentity -Path $product -ExpectedSha256 'AB8ACBD3362513B27CEF0C0C51652F0490EACDE7EAC9962132A4B15C73A61E9F' -Label 'Product'
Assert-RuntimePortsUnused -Ports @(8765,8766,8767,18413,18414) -OperationName 'Isolated unlocked-island validation'
foreach ($file in $fixture.original_hashes.PSObject.Properties) {
    $null = Assert-RuntimeFileIdentity -Path (Join-Path (Join-Path $fixture.save_root $fixture.slot) $file.Name) -ExpectedSha256 $file.Value -Label $file.Name
}
$prior = Save-RuntimeProcessEnvironment -Names @($settings.Keys)
$processes = [Collections.Generic.List[object]]::new()
try {
    foreach ($key in $settings.Keys) { [Environment]::SetEnvironmentVariable($key,$settings[$key],'Process') }
    $game = Join-Path $lab 'Spring15本机续跑\runtime\Stardew Valley\StardewModdingAPI.exe'
    $dotnet = 'D:\StardewAI-TestLab\dotnet\dotnet.exe'
    foreach ($entry in @(@{role='backend';exe=$dotnet;arguments=@($backend);cwd=(Split-Path $backend)},@{role='product';exe=$dotnet;arguments=@($product);cwd=(Split-Path $product)},@{role='game';exe=$game;arguments=@();cwd=(Split-Path $game)})) {
        $env:ASPNETCORE_URLS = if ($entry.role -ceq 'product') { $settings.STARDEWAI_PRODUCT_EXECUTOR_URL } else { $settings.ASPNETCORE_URLS }
        $p = Start-RuntimeEvidenceProcess -FilePath $entry.exe -ArgumentList $entry.arguments -WorkingDirectory $entry.cwd -StandardOutputPath (Join-Path $run.FullName ($entry.role+'.stdout.log')) -StandardErrorPath (Join-Path $run.FullName ($entry.role+'.stderr.log'))
        $processes.Add($p)
        $entry['process'] = $p
        $row = [ordered]@{role=$entry.role;id=$p.Id;exe=$entry.exe;started=$p.StartTime.ToUniversalTime().ToString('o')}
        $row | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run.FullName ($entry.role+'-process.json')) -Encoding utf8
    }
    $rows = @(foreach ($role in 'game','backend','product') { Get-Content -LiteralPath (Join-Path $run.FullName ($role+'-process.json')) -Raw | ConvertFrom-Json })
    [ordered]@{run_id=$fixture.run_id;save_root=$fixture.save_root;slot=$fixture.slot;processes=$rows} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $run.FullName 'live-session.json') -Encoding utf8
    $ready = $false
    foreach ($attempt in 1..45) {
        if (@($processes | Where-Object HasExited).Count) { throw 'Recorded runtime service exited; retained logs identify it.' }
        try {
            $body = (Invoke-WebRequest -Uri $settings.STARDEWAI_BRIDGE_SNAPSHOT_URL -TimeoutSec 2).Content
            $frame = $body | ConvertFrom-Json
            if ($frame.save_id.value -ceq $fixture.expected_save_id -and $frame.player_id.value -ceq $fixture.expected_player_id -and $frame.state.time.total_days.value -eq $fixture.expected_total_day -and $frame.state.environment.training_run_id.value -ceq $fixture.run_id) { $ready=$true; break }
        } catch { }
        Start-Sleep -Seconds 1
    }
    if (!$ready) { throw 'Exact unlocked fixture did not finish loading.' }
    [IO.File]::WriteAllText((Join-Path $run.FullName 'initial-readback.json'),$body)
    [ordered]@{run_directory=$run.FullName;save_id=$frame.save_id.value;player_id=$frame.player_id.value;total_day=$frame.state.time.total_days.value;location=$frame.state.player.location_id.value;state_hash=$frame.state_hash;ready=$true} | ConvertTo-Json
} catch {
    Stop-RuntimeEvidenceProcesses -Processes $processes.ToArray()
    throw
} finally { Restore-RuntimeProcessEnvironment -Values $prior }
