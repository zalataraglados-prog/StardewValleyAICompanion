param(
    [Parameter(Mandatory)][string]$RunDirectory,
    [Parameter(Mandatory)][string]$BinaryRoot,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference='Stop'
$buildIdentity=Get-Content -LiteralPath (Join-Path (Split-Path -Parent $BinaryRoot) 'build-identity.json') -Raw|ConvertFrom-Json
foreach($file in $buildIdentity.files){
    if((Get-FileHash -LiteralPath (Join-Path $BinaryRoot $file.name)).Hash -cne $file.sha256){throw 'Current development replay build differs from its recorded build.'}
}
foreach($name in 'StardewAI.Contracts.dll','StardewAI.Core.dll','StardewAI.GoalConditionedBootstrap.dll'){
    [Reflection.Assembly]::LoadFrom((Join-Path $BinaryRoot $name))|Out-Null
}
$assembly=[Reflection.Assembly]::LoadFrom((Join-Path $BinaryRoot 'StardewAI.GoalConditionedBootstrap.dll'))
$type=$assembly.GetType('StardewAI.GoalConditionedBootstrap.NativeExecutionActionSourceReader',$true)
$flags=[Reflection.BindingFlags]::Static -bor [Reflection.BindingFlags]::NonPublic
$method=$type.GetMethod('ReadFrameCore',$flags)
if(!$method -or $method.GetParameters().Count -ne 6){throw 'The pinned original development frame-reader signature differs.'}
$productBoundary=[Enum]::Parse($method.GetParameters()[4].ParameterType,'NativeAction')
$packet=Get-Content -LiteralPath (Join-Path $RunDirectory 'route-handoff\selected-local-route-and-ledger.json') -Raw|ConvertFrom-Json
$output=New-Item -ItemType Directory -Path $OutputDirectory
$results=@(foreach($case in $packet.cases){
    $queue=Get-Content -LiteralPath $case.original_compiled_queue.path -Raw|ConvertFrom-Json
    if(@($queue.items).Count -ne 1){throw 'This per-action replay does not replace the original sequential queue verifier.'}
    # Invoke the original strict Product-aware frame reader. No source files,
    # queue identities, recorded captures or reader conditions are changed.
    $frame=$method.Invoke($null,[object[]]@($case.original_compiled_queue.path,$case.native_receipt.path,$case.original_after.path,$null,$productBoundary,$false))
    if($frame.Result.RunId -cne $packet.run_id -or $frame.Before.SaveId.Value -cne $packet.save_id -or $frame.Before.PlayerId.Value -cne $packet.player_id){throw 'Development replay returned another source identity.'}
    [ordered]@{case_path=$case.case_path;option_id=$frame.Result.OptionId;native_status=$frame.Result.Status;primitive_verification_status=$frame.Result.PrimitiveVerificationStatus;before_state_hash=$frame.Before.StateHash;after_state_hash=$frame.After.StateHash;source_sha256=$frame.SourceSha256;source_paths=$frame.SourcePaths}
})
$certificate=[StardewAI.GoalConditionedBootstrap.MasterAnglerSharedCapabilityCertificateBuilder]::ReadAndReplay($packet.existing_access_certificate.path)
if(!$certificate.Ready -or $certificate.RunId -cne $packet.run_id){throw 'The development build rejected the actual access certificate.'}
$result=[ordered]@{
    schema_version='r2_local_development_source_replay.v1';recorded_at=[DateTimeOffset]::Now.ToString('o')
    source_commit=$buildIdentity.source_commit;build_identity_path=(Join-Path (Split-Path -Parent $BinaryRoot) 'build-identity.json')
    build_identity_sha256=(Get-FileHash -LiteralPath (Join-Path (Split-Path -Parent $BinaryRoot) 'build-identity.json')).Hash
    reader='NativeExecutionActionSourceReader.ReadFrameCore; original NativeAction Product boundary; strict verified action; one-item queues only'
    source_code_modified=$false;game_started=$false;current_development_reader_verified=$true
    verified_case_count=$results.Count;shared_access_certificate_ready=$certificate.Ready;cases=$results
    formal_training_authorized=$false;training_label_eligible=$false
}
$result|ConvertTo-Json -Depth 11|Set-Content -LiteralPath (Join-Path $output.FullName 'summary.json') -Encoding utf8
[pscustomobject]@{development_source=$buildIdentity.source_commit;verified_case_count=$results.Count;access_ready=$certificate.Ready;game_started=$false}|ConvertTo-Json
