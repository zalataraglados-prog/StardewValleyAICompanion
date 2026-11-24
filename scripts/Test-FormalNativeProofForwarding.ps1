param([string]$OutputRoot=(Join-Path ([IO.Path]::GetTempPath()) ('formal-proof-forwarding-'+[guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
$repository=Split-Path -Parent $PSScriptRoot
[IO.Directory]::CreateDirectory($OutputRoot)|Out-Null
$proof=[ordered]@{schema_version='functional_native_route_proof_reference.v1';selected_route_path='D:\declared-only\functional-evidence.json';selected_route_sha256=('a'*64);native_evidence=$null}
$proofJson=$proof|ConvertTo-Json -Depth 8
$rows=[Collections.Generic.List[object]]::new()
function File([string]$Path,[string]$Value='{}'){
    [IO.Directory]::CreateDirectory((Split-Path -Parent $Path))|Out-Null
    [IO.File]::WriteAllText($Path,$Value,[Text.UTF8Encoding]::new($false))
}
function Assert-Body([string]$Path,[bool]$Expected){
    $body=Get-Content -LiteralPath $Path -Raw|ConvertFrom-Json -AsHashtable
    if($body.ContainsKey('native_route_proof') -ne $Expected){throw 'Optional proof presence changed.'}
    if($Expected -and (($body.native_route_proof|ConvertTo-Json -Depth 12 -Compress) -cne ($proof|ConvertTo-Json -Depth 12 -Compress))){throw 'Forwarded proof changed.'}
}
foreach($enabled in $false,$true){
    $case=Join-Path $OutputRoot ('powershell-'+$enabled)
    foreach($relative in 'saves/slot/slot','runtime/Stardew Valley/StardewModdingAPI.exe','project/src/StardewAI.Backend/bin/Release/net8.0/StardewAI.Backend.dll','project/tools/StardewAI.ProductExecutor/bin/Release/net8.0/StardewAI.ProductExecutor.dll','project/tools/StardewAI.LiveTrainingLoop/bin/Release/net8.0/StardewAI.LiveTrainingLoop.dll','training/datasets/policy-decision-trajectories.jsonl','training/datasets/formal-policy/policy-dataset-manifest.json','training/checkpoints/structured-policy-latest.json'){File (Join-Path $case $relative)}
    $proofPath=Join-Path $case 'proof with spaces.json';File $proofPath $proofJson
    $requests=[Collections.Generic.List[string]]::new()
    & {
        function Get-NetTCPConnection { [CmdletBinding()]param($State,$LocalPort) }
        function Get-Process { [CmdletBinding()]param($Name,$Id) }
        function Start-Process { [CmdletBinding()]param($FilePath,$ArgumentList,$WorkingDirectory,$WindowStyle,$RedirectStandardOutput,$RedirectStandardError,[switch]$PassThru) [pscustomobject]@{Id=73001;HasExited=$false} }
        function Stop-Process { [CmdletBinding()]param($Id,[switch]$Force) }
        function Invoke-RestMethod {
            [CmdletBinding()]param($Method,$Uri,$TimeoutSec,$ContentType,$Body)
            if($Method -eq 'Get'){return @{status='ok'}}
            $requests.Add($Body)
            return @{blocked=$false;started=$true;manifest=@{manifest_path='declared-manifest';run_id='declared';product_executor_process_id=73002;process_id=73003;live_training_loop_process_id=73004}}
        }
        $arguments=@{ProjectRoot=(Join-Path $case 'project');TrainingRoot=(Join-Path $case 'training');RuntimeRoot=(Join-Path $case 'runtime');SavesPath=(Join-Path $case 'saves');SaveSlot='slot';Launch=$true}
        if($enabled){$arguments.NativeRouteProofPath=$proofPath}
        & (Join-Path $repository 'scripts/Start-FormalProductTraining.ps1') @arguments |Out-Null
    }
    if($requests.Count -ne 2 -or $requests[0] -cne $requests[1]){throw 'PowerShell prepare/launch did not reuse one frozen request.'}
    $capture=Join-Path $case 'captured-request.json';File $capture $requests[0];Assert-Body $capture $enabled
    $rows.Add(@{platform='powershell';proof_supplied=$enabled;prepare_launch_identical=$true;processes_mocked=$true;network_mocked=$true})
}

$bash=if($IsWindows){Join-Path (Split-Path -Parent (Split-Path -Parent (Get-Command git).Source)) 'bin/bash.exe'}else{(Get-Command bash).Source}
if(!(Test-Path -LiteralPath $bash)){throw 'Bash is required for the shell entry-point transport test.'}
$old=@{}
foreach($name in 'FAKE_CAPTURE','FAKE_BIN','FORWARDING_SCRIPT','STARDEWAI_APP_ROOT','STARDEWAI_FORMAL_TRAINING_ROOT','STARDEWAI_FORMAL_RUN_ID','STARDEWAI_FORMAL_SAVE_ROOT','STARDEWAI_FORMAL_SAVE_SLOT','STARDEWAI_FORMAL_NATIVE_ROUTE_PROOF'){$old[$name]=[Environment]::GetEnvironmentVariable($name,'Process')}
try{
    foreach($enabled in $false,$true){
        $case=Join-Path $OutputRoot ('shell-'+$enabled)
        foreach($name in 'StardewAI.Backend','StardewAI.ProductExecutor','StardewAI.LiveTrainingLoop','StardewAI.PolicyDataset','StardewAI.PolicyModel'){
            File (Join-Path $case ('app/'+$name)) ("#!/bin/sh`n"+$(if($name -eq 'StardewAI.Backend'){'touch "$FAKE_CAPTURE/ready"'}else{'true'})+"`n")
        }
        foreach($relative in 'saves/slot/slot','training/datasets/policy-decision-trajectories.jsonl','training/datasets/formal-policy/policy-dataset-manifest.json','training/checkpoints/structured-policy-latest.json'){File (Join-Path $case $relative)}
        $capture=Join-Path $case 'captured';[IO.Directory]::CreateDirectory($capture)|Out-Null
        File (Join-Path $case 'bin/pgrep') "#!/bin/sh`nprintf '424242\n'`n"
        $curl=@'
#!/bin/sh
set -eu
url=''
request=''
for arg in "$@"; do
  case "$arg" in http:*) url="$arg";; @*) request="${arg#@}";; esac
done
case "$url" in
  */health) [ -f "$FAKE_CAPTURE/ready" ] || exit 22; printf '{"status":"ok"}';;
  */prepare) cp "$request" "$FAKE_CAPTURE/prepare.json"; printf '{"blocked":false}';;
  */launch) cp "$request" "$FAKE_CAPTURE/launch.json"; printf '{"blocked":false,"started":true}';;
  *) exit 90;;
esac
'@
        File (Join-Path $case 'bin/curl') ($curl.Replace("`r`n","`n")+"`n")
        $entry=Join-Path $case 'entry.sh'
        File $entry ((Get-Content -LiteralPath (Join-Path $repository 'scripts/start-formal-product-training-attached.sh') -Raw).Replace("`r`n","`n"))
        $proofPath=Join-Path $case 'proof with spaces.json';File $proofPath $proofJson
        $settings=@{FAKE_CAPTURE=$capture;FAKE_BIN=(Join-Path $case 'bin');FORWARDING_SCRIPT=$entry;STARDEWAI_APP_ROOT=(Join-Path $case 'app');STARDEWAI_FORMAL_TRAINING_ROOT=(Join-Path $case 'training');STARDEWAI_FORMAL_RUN_ID='proof-transport';STARDEWAI_FORMAL_SAVE_ROOT=(Join-Path $case 'saves');STARDEWAI_FORMAL_SAVE_SLOT='slot';STARDEWAI_FORMAL_NATIVE_ROUTE_PROOF=$(if($enabled){$proofPath}else{''})}
        foreach($name in $settings.Keys){[Environment]::SetEnvironmentVariable($name,$settings[$name].Replace('\','/'),'Process')}
        $command='if command -v cygpath >/dev/null; then FAKE_BIN=$(cygpath -u "$FAKE_BIN"); fi; export PATH="$FAKE_BIN:$PATH"; chmod +x "$FAKE_BIN/curl" "$FAKE_BIN/pgrep" "$STARDEWAI_APP_ROOT/"*; [ "$(command -v curl)" = "$FAKE_BIN/curl" ] || exit 91; exec sh "$FORWARDING_SCRIPT"'
        & $bash --noprofile --norc -c $command *> (Join-Path $case 'shell.log')
        if($LASTEXITCODE -ne 0){throw ('Shell transport fixture failed; see '+(Join-Path $case 'shell.log'))}
        $prepare=Join-Path $capture 'prepare.json';$launch=Join-Path $capture 'launch.json'
        Assert-Body $prepare $enabled;Assert-Body $launch $enabled
        if((Get-Content -LiteralPath $prepare -Raw) -cne (Get-Content -LiteralPath $launch -Raw)){throw 'Shell prepare/launch request changed.'}
        $rows.Add(@{platform='shell';proof_supplied=$enabled;prepare_launch_identical=$true;backend_is_fixture_executable=$true;network_mocked=$true})
    }
}finally{foreach($name in $old.Keys){[Environment]::SetEnvironmentVariable($name,$old[$name],'Process')}}
$rows.ToArray()|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $OutputRoot 'summary.json') -Encoding utf8
[pscustomobject]@{cases=$rows.Count;passed=$rows.Count;real_game_started=$false;real_backend_started=$false;output=$OutputRoot}|ConvertTo-Json
