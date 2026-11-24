param(
    [Parameter(Mandatory)][string]$ProofPath,
    [Parameter(Mandatory)][string]$BinaryRoot,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
foreach($name in 'StardewAI.Contracts.dll','StardewAI.Core.dll','StardewAI.GoalConditionedBootstrap.dll'){
    [Reflection.Assembly]::LoadFrom((Join-Path $BinaryRoot $name))|Out-Null
}
$json=[Text.Json.JsonSerializerOptions]::new([Text.Json.JsonSerializerDefaults]::Web);$json.WriteIndented=$true
$sourceProof=Get-Content -LiteralPath $ProofPath -Raw|ConvertFrom-Json -AsHashtable
$baseText=Get-Content -LiteralPath $sourceProof.selected_route_path -Raw
$output=New-Item -ItemType Directory -Path $OutputDirectory
function Write-Json([string]$Path,$Value){$Value|ConvertTo-Json -Depth 64|Set-Content -LiteralPath $Path -Encoding utf8}
function Ref([string]$Path){@{path=[IO.Path]::GetFullPath($Path);sha256=(Get-FileHash -LiteralPath $Path).Hash.ToLowerInvariant()}}
function Typed-Proof($Value){[Text.Json.JsonSerializer]::Deserialize[StardewAI.Contracts.Training.FormalNativeRouteProofReference](($Value|ConvertTo-Json -Depth 64),$json)}
function Evaluate($Value){[StardewAI.Core.Training.FormalNativeRouteAdmissionConsumer]::Evaluate((Typed-Proof $Value))}
$rows=[Collections.Generic.List[object]]::new()
foreach($mode in 'valid_year5','wrong_source_hash','wrong_other_before_hash','wrong_journal_hash','wrong_gzip_hash','forged_budget_refreshed_hash','forged_cost_refreshed_hash','joint_disabled','primary_truncated','duplicate_receipt','after_substitution','wrong_target','self_attested_no_trace','mixed_acquisition_binding'){
    $dir=New-Item -ItemType Directory -Path (Join-Path $output.FullName $mode)
    $evidence=$baseText|ConvertFrom-Json -AsHashtable
    $proof=Get-Content -LiteralPath $ProofPath -Raw|ConvertFrom-Json -AsHashtable
    if($mode -in @('forged_budget_refreshed_hash','forged_cost_refreshed_hash','joint_disabled','primary_truncated')){
        $field=if($mode -eq 'forged_budget_refreshed_hash'){'compile_response'}else{'compile_request'}
        $data=Get-Content -LiteralPath $evidence[$field].path -Raw|ConvertFrom-Json -AsHashtable
        switch($mode){
            'forged_budget_refreshed_hash' {$data.plan.candidate_audit[0].remaining_energy_after++}
            'forged_cost_refreshed_hash' {($data.ranked_event_candidates|Where-Object {$_.candidate_id -ceq $data.primary_candidate_ids[0]}).energy_cost=0}
            'joint_disabled' {$data.use_joint_calendar=$false}
            'primary_truncated' {$data.primary_candidate_ids=@()}
        }
        $path=Join-Path $dir.FullName ($field+'.json');Write-Json $path $data;$evidence[$field]=Ref $path
    }
    switch($mode){
        'wrong_source_hash' {$evidence.snapshot.sha256='0'*64}
        'wrong_other_before_hash' {$evidence.native_actions[2].before_snapshot.sha256='0'*64}
        'wrong_journal_hash' {$evidence.native_actions[2].request_journal.sha256='0'*64}
        'wrong_gzip_hash' {$evidence.native_actions[2].request_gzip.sha256='0'*64}
        'duplicate_receipt' {$evidence.native_actions+=@($evidence.native_actions[0])}
        'after_substitution' {$copy=Join-Path $dir.FullName 'substituted-after.json';Copy-Item -LiteralPath $evidence.native_actions[0].after_snapshot.path -Destination $copy;$evidence.native_actions[0].after_snapshot=Ref $copy}
        'wrong_target' {$evidence.target_qualified_item_id='(O)130'}
        'self_attested_no_trace' {$evidence.native_actions=@();$evidence.ready=$true;$evidence.formal_training_authorized=$true}
        'mixed_acquisition_binding' {$proof.native_evidence=@{schema_version='caller-supplied-acquisition'}}
    }
    $file=Join-Path $dir.FullName 'evidence.json';Write-Json $file $evidence
    $proof.selected_route_path=$file;$proof.selected_route_sha256=(Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant()
    $assessment=Evaluate $proof
    $passed=$assessment.SourceReplayVerified
    $expected=$mode -eq 'valid_year5'
    $rows.Add([ordered]@{case=$mode;expected_function_verified=$expected;actual_function_verified=$passed;formal_gate_open=@($assessment.Gates|Where-Object Satisfied).Count -gt 0;test_passed=($passed -eq $expected);reason=$assessment.Detail})
    [IO.File]::WriteAllText((Join-Path $dir.FullName 'assessment.json'),[Text.Json.JsonSerializer]::Serialize($assessment,$assessment.GetType(),$json))
}

# Exercise the three existing consumers using real native input. The report's
# other nine gates use the established hermetic policy fixture: wiring only,
# not new Teacher data, evaluation, promotion or authority to launch.
$protocol=New-Item -ItemType Directory -Path (Join-Path $output.FullName 'three-consumers')
$evidence=$baseText|ConvertFrom-Json -AsHashtable
$response=Join-Path $protocol.FullName 'compile-response.json'
Copy-Item -LiteralPath $evidence.compile_response.path -Destination $response
$evidence.compile_response=Ref $response
$file=Join-Path $protocol.FullName 'evidence.json';Write-Json $file $evidence
$proof=Get-Content -LiteralPath $ProofPath -Raw|ConvertFrom-Json -AsHashtable
$proof.selected_route_path=$file;$proof.selected_route_sha256=(Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant()
$typed=Typed-Proof $proof
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $BinaryRoot 'StardewAI.GoalConditionedBootstrap.dll'))
$selfTest=$assembly.GetType('StardewAI.GoalConditionedBootstrap.BootstrapSelfTest',$true)
$report=$selfTest.GetMethod('VerifyFormalTrainingAdmissionPolicy',([Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::NonPublic)).Invoke($null,[object[]]@(0,$typed))
$request=[StardewAI.Contracts.Training.TrainingLaunchRequest]::new()
$request.RootPath=$protocol.FullName;$request.RunId='functional-proof-protocol';$request.NativeRouteProof=$typed
$request.Mode=[StardewAI.Contracts.Training.TrainingSessionMode]::FormalProductTraining
$request.WindowStyle='normal';$request.AllowGameLaunch=$false
$launcher=[StardewAI.Core.Training.StardewTrainingSessionLauncher]::new()
$prepare=$launcher.Prepare($request)
# This private test manifest exercises the prepared branch while deliberately
# retaining unrelated blocking conditions. It is never offered for promotion.
$prepare.Manifest.Status='prepared';$request.ManifestPath=$prepare.Manifest.ManifestPath
[IO.File]::WriteAllText($request.ManifestPath,[Text.Json.JsonSerializer]::Serialize($prepare.Manifest,$prepare.Manifest.GetType(),$json))
$launch=$launcher.Launch($request)
$gateJson=[Text.Json.JsonSerializer]::Serialize($report.NativeRouteAssessment.Gates,$json)
$parity=$gateJson -ceq [Text.Json.JsonSerializer]::Serialize($prepare.Manifest.NativeRouteAssessment.Gates,$json) -and
    $gateJson -ceq [Text.Json.JsonSerializer]::Serialize($launch.Manifest.NativeRouteAssessment.Gates,$json)
Add-Content -LiteralPath $response -Value ' '
$changed=$launcher.Launch($request)
$changedRejected=!$changed.Manifest.NativeRouteAssessment.SourceReplayVerified -and @($changed.Manifest.NativeRouteAssessment.Gates|Where-Object Satisfied).Count -eq 0
$record=[ordered]@{other_report_gates_are_hermetic_fixture=$true;real_native_gates_match_report_prepare_launch=$parity;native_gates=$report.NativeRouteAssessment.Gates;prepare_blocked=$prepare.Blocked;launch_blocked=$launch.Blocked;launch_attempted=$launch.LaunchAttempted;formal_authorized=$report.FormalProductTrainingAuthorized;changed_nested_artifact_rejected=$changedRejected;changed_launch_attempted=$changed.LaunchAttempted}
Write-Json (Join-Path $protocol.FullName 'summary.json') $record
if(!$parity -or $launch.LaunchAttempted -or $changed.LaunchAttempted -or !$changedRejected -or $report.FormalProductTrainingAuthorized -or @($report.NativeRouteAssessment.Gates|Where-Object Satisfied).Count){throw 'Existing consumer parity, target-plan gate or immutable-source guard failed.'}

# Private protocol copies relocate only receipt/journal artifact references.
# They are never native evidence or production handoff inputs. Original source
# bytes are read-only; mutations below touch only newly created fixture files.
$fixture=New-Item -ItemType Directory -Path (Join-Path $output.FullName 'prepared-dependency-fixture')
$frozen=$baseText|ConvertFrom-Json -AsHashtable
$sourceSnapshot=$frozen.snapshot.path
function Copy-SnapshotChunks([string]$Descriptor,[string]$Destination){
    $manifest=Get-Content -LiteralPath $Descriptor -Raw|ConvertFrom-Json -AsHashtable
    if($manifest.schema_version -cne 'stardewai.content_addressed_json.v1'){return}
    function Visit($node){
        if($node -is [Collections.IDictionary]){
            if($node.Contains('sha256') -and $node.Contains('uncompressed_bytes')){
                $hash=[string]$node.sha256
                $relative=Join-Path $hash.Substring(0,2) ($hash+'.json.gz')
                $from=Join-Path (Join-Path (Split-Path -Parent $Descriptor) '_snapshot-blobs') $relative
                $to=Join-Path (Join-Path $Destination '_snapshot-blobs') $relative
                [IO.Directory]::CreateDirectory((Split-Path -Parent $to))|Out-Null
                if(!(Test-Path -LiteralPath $to)){Copy-Item -LiteralPath $from -Destination $to}
            }
            foreach($child in $node.Values){Visit $child}
        }elseif($node -is [Collections.IEnumerable] -and $node -isnot [string]){foreach($child in $node){Visit $child}}
    }
    Visit $manifest
}
for($index=0;$index -lt $frozen.native_actions.Count;$index++){
    $action=$frozen.native_actions[$index]
    $dir=New-Item -ItemType Directory -Path (Join-Path $fixture.FullName ('frame-'+$index))
    $prefix=Split-Path -Parent $action.receipt.path
    foreach($role in 'queue','receipt','before_snapshot','after_snapshot','request_journal','request_gzip'){
        $original=$action[$role].path
        $copy=Join-Path $dir.FullName (Split-Path -Leaf $original)
        Copy-Item -LiteralPath $original -Destination $copy
        if($role -in @('receipt','request_journal')){
            $metadata=Get-Content -LiteralPath $copy -Raw|ConvertFrom-Json -AsHashtable
            foreach($key in @($metadata.Keys)){
                if($metadata[$key] -is [string] -and $metadata[$key].StartsWith($prefix+'\',[StringComparison]::OrdinalIgnoreCase)){
                    $metadata[$key]=$dir.FullName+$metadata[$key].Substring($prefix.Length)
                }
            }
            Write-Json $copy $metadata
        }
        if($role -in @('before_snapshot','after_snapshot')){Copy-SnapshotChunks $original $dir.FullName}
        $action[$role]=Ref $copy
        if($role -eq 'before_snapshot' -and $original -ceq $sourceSnapshot){$frozen.snapshot=Ref $copy}
    }
}
$fixtureEvidence=Join-Path $fixture.FullName 'evidence.json';Write-Json $fixtureEvidence $frozen
$fixtureProof=Get-Content -LiteralPath $ProofPath -Raw|ConvertFrom-Json -AsHashtable
$fixtureProof.selected_route_path=$fixtureEvidence;$fixtureProof.selected_route_sha256=(Get-FileHash -LiteralPath $fixtureEvidence).Hash.ToLowerInvariant()
$fixtureTyped=Typed-Proof $fixtureProof
if(!(Evaluate $fixtureProof).SourceReplayVerified){throw 'Relocated protocol fixture did not verify before its controlled mutation.'}
$dependencyRows=[Collections.Generic.List[object]]::new()
foreach($role in 'before_snapshot','request_journal','request_gzip'){
    $request.RootPath=$fixture.FullName;$request.RunId='dependency-'+$role;$request.ManifestPath='';$request.NativeRouteProof=$fixtureTyped
    $prepared=$launcher.Prepare($request)
    if(!$prepared.Manifest.NativeRouteAssessment.SourceReplayVerified){throw 'Unmodified protocol fixture was rejected at prepare.'}
    $prepared.Manifest.Status='prepared';$request.ManifestPath=$prepared.Manifest.ManifestPath
    [IO.File]::WriteAllText($request.ManifestPath,[Text.Json.JsonSerializer]::Serialize($prepared.Manifest,$prepared.Manifest.GetType(),$json))
    $file=$frozen.native_actions[2][$role].path
    $originalBytes=[IO.File]::ReadAllBytes($file)
    try{
        if($role -eq 'request_gzip'){
            $bytes=[byte[]]$originalBytes.Clone();$bytes[4]=$bytes[4] -bxor 1;[IO.File]::WriteAllBytes($file,$bytes)
        }else{Add-Content -LiteralPath $file -Value ' '}
        $result=$launcher.Launch($request)
        $rejected=!$result.Manifest.NativeRouteAssessment.SourceReplayVerified -and !$result.LaunchAttempted
        $dependencyRows.Add(@{changed_role=$role;changed_other_frame=$true;source_document_unchanged=$true;rejected=$rejected;launch_attempted=$result.LaunchAttempted;scope='relocated_protocol_fixture_only'})
        if(!$rejected){throw ('Prepared dependency replacement was accepted: '+$role)}
    }finally{[IO.File]::WriteAllBytes($file,$originalBytes)}
}
Write-Json (Join-Path $fixture.FullName 'mutation-results.json') $dependencyRows.ToArray()
Write-Json (Join-Path $output.FullName 'matrix.json') $rows.ToArray()
$failed=@($rows|Where-Object {!$_.test_passed})
[pscustomobject]@{cases=$rows.Count;passed=($rows.Count-$failed.Count);three_consumer_parity=$parity;prepared_source_change_rejected=$changedRejected;prepared_other_frame_dependency_rejections=$dependencyRows.Count;formal_gates_remain_closed=$true;game_started=$false}|ConvertTo-Json
if($failed.Count){throw 'A functional evidence counterexample was accepted or the retained positive case failed.'}
