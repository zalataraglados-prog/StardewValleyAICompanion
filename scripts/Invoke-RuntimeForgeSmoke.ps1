param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [string] $RuntimeRoot = "E:\StardewValleyAICompanion-runtime",
    [string] $SaveSlot = "",
    [string] $RunId = ("runtime-forge-" + (Get-Date -Format "yyyyMMdd-HHmmss")),
    [int] $BackendPort = 8794,
    [int] $StartupTimeoutSeconds = 180,
    [switch] $KeepGameRunning
)
$ErrorActionPreference = "Stop"

function Write-Json([string] $Path, $Value) { $Value | ConvertTo-Json -Depth 96 | Set-Content -LiteralPath $Path -Encoding utf8 }
function Invoke-Post([string] $Url, $Body) { Invoke-RestMethod -Method Post -Uri $Url -ContentType "application/json; charset=utf-8" -Body ($Body | ConvertTo-Json -Depth 96) -TimeoutSec 120 }
function Wait-Json([string] $Url, [int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds); $last = "not_requested"
    while ((Get-Date) -lt $deadline) { try { $value = Invoke-RestMethod -Method Get -Uri $Url -TimeoutSec 15; if ($null -ne $value) { return $value } } catch { $last = $_.Exception.Message }; Start-Sleep -Seconds 2 }
    throw "Timed out waiting for $Url. Last error: $last"
}
function Wait-World([int] $Seconds) {
    $deadline = (Get-Date).AddSeconds($Seconds)
    while ((Get-Date) -lt $deadline) { try { $snapshot = Invoke-RestMethod -Uri $snapshotUrl -TimeoutSec 15; if ($snapshot.state.player.location_id.status -in @("available","derived")) { return $snapshot } } catch {}; Start-Sleep -Seconds 2 }
    throw "Timed out waiting for loaded world."
}
function Wait-ForgeRow([string] $Operation, [int] $Seconds = 90) {
    $deadline = (Get-Date).AddSeconds($Seconds); $last = "none"
    while ((Get-Date) -lt $deadline) {
        try {
            $snapshot = Invoke-RestMethod -Uri $snapshotUrl -TimeoutSec 15
            $rows = @($snapshot.state.player.forge.value.rows | Where-Object {
                $_.forge_operation -eq $Operation -and $_.forge_source_kind -eq "mini_forge" -and
                $_.left_source_id -eq "inventory:0" -and
                (($Operation -like "unforge_*") -or $_.right_source_id -eq "inventory:1") -and
                $_.forge_candidate_status -eq "ready_for_native_forge_menu"
            })
            $last = "forge=$($snapshot.state.player.forge.status);rows=$($rows.Count)"
            if ($rows.Count -gt 0) { return [pscustomobject]@{ snapshot=$snapshot; row=$rows[0] } }
        } catch { $last = $_.Exception.Message }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for forge row $Operation. Last status: $last"
}
function Invoke-ForgeCase([int] $Ordinal, [string] $Operation) {
    $caseDirectory = Join-Path $artifactDirectory ("{0:D2}-{1}" -f $Ordinal,$Operation); $loopRoot = Join-Path $caseDirectory "loop"
    New-Item -ItemType Directory -Force -Path $caseDirectory | Out-Null
    $initial = Wait-Json $snapshotUrl 60
    $fixture = Invoke-Post $executorUrl ([ordered]@{
        schema_version="training_execution_request.v1"; run_id=$RunId; queue_id="runtime-forge-fixture"; queue_item_id="$RunId.fixture.$Operation"
        before_state_hash=[string]$initial.state_hash; option_id="debug.setup_forge_fixture"; execution_mode="training_singleplayer"; actor="training_farmer.main"
        save_isolation_path=$savesPath; request_nonce=[guid]::NewGuid().ToString("N"); created_at=[DateTimeOffset]::UtcNow.ToString("O"); forge_operation=$Operation
    })
    Write-Json (Join-Path $caseDirectory "fixture-result.json") $fixture
    if ($fixture.status -ne "applied" -or $fixture.primitive_verification_status -ne "verified") { throw "Forge fixture failed for ${Operation}: $(@($fixture.block_reasons) -join ',')" }
    Start-Sleep -Milliseconds 750
    $ready = Wait-ForgeRow $Operation; $snapshotPath = Join-Path $caseDirectory "before-snapshot.json"; Write-Json $snapshotPath $ready.snapshot
    $arguments = [System.Collections.Generic.List[string]]::new()
    foreach ($value in @($loopDll,"--root",$loopRoot,"--backend-url",$backendUrl,"--bridge-snapshot-url",$snapshotUrl,"--executor-url",$executorBaseUrl,
        "--snapshot-file",$snapshotPath,"--no-manifest","--run-id",$RunId,"--save-isolation-path",$savesPath,"--iterations","1","--skip-training","--sleep-ms","0",
        "--use-daily-plan","--daily-plan-max-candidates","1","--daily-plan-candidate-options","crafting.forge_item","--daily-plan-candidate-kind","forge_item",
        "--daily-plan-explicit-confirmation","--daily-plan-candidate-parameter","forge_operation=$Operation","--daily-plan-candidate-parameter","forge_reason=isolated_native_runtime_verification",
        "--daily-plan-candidate-parameter","left_source_id=inventory:0","--daily-plan-candidate-parameter","forge_source_id=$($ready.row.forge_source_id)","--after-snapshot-wait-ms","500")) { $arguments.Add([string]$value) }
    if ($Operation -notlike "unforge_*") { $arguments.Add("--daily-plan-candidate-parameter"); $arguments.Add("right_source_id=inventory:1") }
    $output = & dotnet $arguments; $output | Set-Content -LiteralPath (Join-Path $caseDirectory "live-training-loop.stdout.log") -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw "Forge case $Operation failed with exit code $LASTEXITCODE." }
    $snapshotDirectory = Join-Path $loopRoot ("runs\" + $RunId + "\live-snapshots")
    $execution = Get-Content -LiteralPath (Join-Path $snapshotDirectory "execution-0001.json") -Raw | ConvertFrom-Json
    $result = @($execution.step_results | Where-Object { $_.option_id -eq "executor.forge_item" }) | Select-Object -Last 1
    $passed = $null -ne $result -and $result.status -eq "applied" -and $result.primitive_verification_status -eq "verified"
    $summary = [ordered]@{ ordinal=$Ordinal; operation=$Operation; source_id=[string]$ready.row.forge_source_id; output_contract=[string]$ready.row.output_contract_kind; status=if($passed){"passed"}else{"failed"}; execution_status=[string]$result.status; verification_status=[string]$result.primitive_verification_status; verification_reasons=@($result.primitive_verification_reasons); observed_effect=[string]$result.observed_effect; block_reasons=@($result.block_reasons) }
    Write-Json (Join-Path $caseDirectory "summary.json") $summary
    if (-not $passed) { throw "Runtime forge case failed: $caseDirectory" }
    [pscustomobject]$summary
}

$gameDirectory=Join-Path $RuntimeRoot "Stardew Valley"; $smapi=Join-Path $gameDirectory "StardewModdingAPI.exe"; $savesPath=Join-Path $RuntimeRoot "saves"
$backendUrl="http://127.0.0.1:$BackendPort"; $snapshotUrl="http://127.0.0.1:8765/api/v1/snapshot?profile=full&fresh=1"; $executorBaseUrl="http://127.0.0.1:8767"; $executorUrl="$executorBaseUrl/api/v1/training/execute"
$loopDll=Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\bin\Release\net8.0\StardewAI.LiveTrainingLoop.dll"
if ([string]::IsNullOrWhiteSpace($SaveSlot)) { $SaveSlot=(Get-ChildItem -LiteralPath $savesPath -Directory | Sort-Object LastWriteTime -Descending | Select-Object -First 1).Name }
foreach($port in @(8765,8767,$BackendPort)){if($null -ne (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue)){throw "Runtime forge smoke requires unused port $port."}}
if($null -ne (Get-Process -Name "StardewModdingAPI" -ErrorAction SilentlyContinue)){throw "StardewModdingAPI is already running. Refusing to attach."}
$artifactDirectory=Join-Path $ProjectRoot ("artifacts\runtime-forge\"+$RunId); New-Item -ItemType Directory -Force -Path $artifactDirectory | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-TransparentBridgeToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
& (Join-Path $ProjectRoot "scripts\Deploy-RuntimeTestHarnessToRuntime.ps1") -ProjectRoot $ProjectRoot -RuntimeRoot $RuntimeRoot | Out-Null
dotnet build (Join-Path $ProjectRoot "tools\StardewAI.LiveTrainingLoop\StardewAI.LiveTrainingLoop.csproj") -c Release --nologo "-p:GamePath=$gameDirectory" | Out-Null
if($LASTEXITCODE -ne 0){throw "LiveTrainingLoop build failed."}
$names=@("STARDEWAI_TEST_SAVES","STARDEWAI_TEST_SLOT","STARDEWAI_TEST_AUTO_LOAD","STARDEWAI_SAVE_ISOLATION_PATH","STARDEWAI_TRAINING_RUN_ID","STARDEWAI_TRAINING_MODE","STARDEWAI_TRAINING_OUTPUT_DIR","SDL_AUDIODRIVER","ALSOFT_DRIVERS","ASPNETCORE_URLS");$saved=@{};foreach($name in $names){$saved[$name]=[Environment]::GetEnvironmentVariable($name)}
$game=$null;$backend=$null
try {
    $env:STARDEWAI_TEST_SAVES=$savesPath;$env:STARDEWAI_TEST_SLOT=$SaveSlot;$env:STARDEWAI_TEST_AUTO_LOAD="true";$env:STARDEWAI_SAVE_ISOLATION_PATH=$savesPath;$env:STARDEWAI_TRAINING_RUN_ID=$RunId;$env:STARDEWAI_TRAINING_MODE="1";$env:STARDEWAI_TRAINING_OUTPUT_DIR=$artifactDirectory;$env:SDL_AUDIODRIVER="dummy";$env:ALSOFT_DRIVERS="null";$env:ASPNETCORE_URLS=$backendUrl
    $backend=Start-Process dotnet -ArgumentList @("run","--no-restore","--project",(Join-Path $ProjectRoot "src\StardewAI.Backend\StardewAI.Backend.csproj"),"--no-launch-profile") -WorkingDirectory $ProjectRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $artifactDirectory "backend.stdout.log") -RedirectStandardError (Join-Path $artifactDirectory "backend.stderr.log") -PassThru
    Wait-Json "$backendUrl/health" 60 | Out-Null
    $game=Start-Process $smapi -WorkingDirectory $gameDirectory -WindowStyle Hidden -RedirectStandardOutput (Join-Path $artifactDirectory "game.stdout.log") -RedirectStandardError (Join-Path $artifactDirectory "game.stderr.log") -PassThru
    Wait-Json "$executorBaseUrl/health" 45 | Out-Null; Wait-World $StartupTimeoutSeconds | Out-Null
    $operations=@("gem_forge","prismatic_enchant","galaxy_soul","diamond_forge","dragon_tooth_innate_reroll","weapon_appearance","combine_rings","unforge_weapon","unforge_combined_ring")
    $cases=@();for($i=0;$i -lt $operations.Count;$i++){$cases+=Invoke-ForgeCase ($i+1) $operations[$i]}
    $summary=[ordered]@{schema_version="stardewai.runtime_forge_smoke.v1";status=if(@($cases | Where-Object { $_.status -ne "passed" }).Count -eq 0){"passed"}else{"failed"};run_id=$RunId;save_slot=$SaveSlot;scope="all_locked_1_6_15_ForgeMenu_operation_families";cases=$cases}
    Write-Json (Join-Path $artifactDirectory "summary.json") $summary;$summary|ConvertTo-Json -Depth 64
    if($summary.status -ne "passed"){throw "Runtime forge smoke failed: $artifactDirectory"}
}
finally { foreach($name in $saved.Keys){[Environment]::SetEnvironmentVariable($name,$saved[$name],"Process")};if(-not $KeepGameRunning -and $null -ne $game -and -not $game.HasExited){Stop-Process -Id $game.Id -Force -ErrorAction SilentlyContinue};if($null -ne $backend -and -not $backend.HasExited){Stop-Process -Id $backend.Id -Force -ErrorAction SilentlyContinue} }
