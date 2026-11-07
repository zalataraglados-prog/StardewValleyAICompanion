[CmdletBinding()]
param([string]$ProjectRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string]$OutputRoot=(Join-Path ([IO.Path]::GetTempPath()) ('snapshot-import-'+[guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeMoneyCropNative.ps1')
if (Test-Path -LiteralPath $OutputRoot) { throw 'Snapshot import test output exists.' }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$loop=Join-Path $ProjectRoot 'tools/StardewAI.LiveTrainingLoop/bin/Release/net8.0/StardewAI.LiveTrainingLoop.dll'
$core=Join-Path (Split-Path -Parent $loop) 'StardewAI.Core.dll'
$null=[Reflection.Assembly]::LoadFrom($loop)
$null=[Reflection.Assembly]::LoadFrom($core)
$passed=[Collections.Generic.List[string]]::new()
function Assert-Check([bool]$Value,[string]$Name) {
    if (-not $Value) { throw "Snapshot import check failed: $Name" }
    $passed.Add($Name)
}
function Assert-Rejected([string]$Name,[scriptblock]$Action) {
    $failed=$false
    try { & $Action | Out-Null } catch { $failed=$true }
    Assert-Check $failed $Name
}
foreach ($file in @('lib/RuntimeEvidenceCommon.ps1','lib/RuntimeMoneyCropNative.ps1',
    'lib/RuntimeMoneyCropSeedAcquisition.ps1','lib/RuntimeMoneyCropTerminalSettlement.ps1',
    'lib/RuntimeMoneyCropEvidenceFinalization.ps1','lib/RuntimeMoneyCropProductionInput.ps1','Invoke-RuntimeMoneyCropNativeProduction.ps1')) {
    $parseErrors=$null
    $null=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $file),[ref]$null,[ref]$parseErrors)
    Assert-Check (@($parseErrors).Count -eq 0) ("parse:$file")
}
$raw='{"schema_version":"snapshot.v1","state_hash":"fixture-state","state":{"player":{"value":1.0},"time":{"value":"2026-11-07T09:00:00+08:00"}}}'
$plain=Join-Path $OutputRoot 'plain.json'
$manifest=Join-Path $OutputRoot 'stored/snapshot.json'
Write-Utf8Text $plain $raw
$task=[StardewAI.LiveTrainingLoop.ContentAddressedJsonArtifactStore]::WriteAsync(
    $manifest,$raw,'content_addressed_gzip',[Threading.CancellationToken]::None)
$null=$task.GetAwaiter().GetResult()
$manifestHash=Get-RuntimeFileSha256 $manifest
$logical=[StardewAI.Core.Infrastructure.ContentAddressedJsonArtifactReader]::ReadAllText($manifest)
$originalPost=(Get-Command Invoke-JsonPost).ScriptBlock
$script:postCount=0
$script:backendAccepted=$true
$script:backendHash='fixture-state'
function Invoke-JsonPost {
    param($Url,$Body)
    $script:postCount++
    $script:lastPostedBody=$Body
    $script:lastPostedUrl=$Url
    if (($Body | ConvertFrom-Json).schema_version -cne 'snapshot.v1') { throw 'Backend never accepts an artifact manifest.' }
    return [pscustomobject]@{Value=[pscustomobject]@{accepted=$script:backendAccepted;state_hash=$script:backendHash}}
}
$inline=Import-RuntimeSnapshotFile -Path $plain -BackendUrl 'http://fixture' -SnapshotProfile full
Assert-Check ($inline.Raw -ceq $raw -and $inline.Path -ceq [IO.Path]::GetFullPath($plain)) 'inline_lexemes_and_path_preserved'
$stored=Import-RuntimeSnapshotFile -Path $manifest -BackendUrl 'http://fixture' -SnapshotProfile farm_layout -CoreAssemblyPath $core
Assert-Check ($stored.Raw -ceq $logical -and $script:lastPostedBody -ceq $logical -and
    $stored.Value.state_hash -ceq 'fixture-state' -and $stored.Path -ceq [IO.Path]::GetFullPath($manifest) -and
    $script:lastPostedUrl -ceq 'http://fixture/api/v1/snapshots?profile=farm_layout') 'manifest_decoded_before_original_backend_identity_gate'
$copy=Join-Path $OutputRoot 'relocated/daily.json'
$null=Copy-RuntimeSnapshotArtifact -SourcePath $manifest -DestinationPath $copy -CoreAssemblyPath $core
Assert-Check (([IO.File]::ReadAllText($copy)) -ceq $logical -and
    (Read-RuntimeLogicalJsonArtifact $copy).schema_version -ceq 'snapshot.v1') 'relocated_copy_has_no_dangling_blob_reference'
$plainCopy=Join-Path $OutputRoot 'relocated/plain.json'
$null=Copy-RuntimeSnapshotArtifact -SourcePath $plain -DestinationPath $plainCopy
Assert-Check (([IO.File]::ReadAllText($plainCopy)) -ceq $raw) 'inline_copy_lexemes_preserved'
$null=Copy-RuntimeSnapshotArtifact -SourcePath $manifest -DestinationPath $manifest -CoreAssemblyPath $core
Assert-Check ((Get-RuntimeFileSha256 $manifest) -ceq $manifestHash) 'self_copy_keeps_manifest_immutable'
$script:backendHash='foreign-state'
Assert-Rejected 'backend_state_hash_drift_still_rejected' { Import-RuntimeSnapshotFile $manifest 'http://fixture' full -CoreAssemblyPath $core }
$script:backendHash='fixture-state'
$script:backendAccepted=$false
Assert-Rejected 'backend_rejection_still_rejected' { Import-RuntimeSnapshotFile $plain 'http://fixture' full }
$bad=Join-Path $OutputRoot 'stored/corrupt-manifest.json'
$forged=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
$forged.logical_sha256='f'*64
Write-JsonFile $bad $forged
$postBefore=$script:postCount
Assert-Rejected 'corrupt_manifest_rejected_before_http' { Import-RuntimeSnapshotFile $bad 'http://fixture' full -CoreAssemblyPath $core }
Assert-Check ($script:postCount -eq $postBefore) 'corrupt_input_never_posted'
$badCopy=Join-Path $OutputRoot 'relocated/must-not-exist.json'
Assert-Rejected 'corrupt_copy_rejected' { Copy-RuntimeSnapshotArtifact $bad $badCopy -CoreAssemblyPath $core }
Assert-Check (-not (Test-Path -LiteralPath $badCopy)) 'corrupt_copy_leaves_no_output'
Set-Item -Path Function:Invoke-JsonPost -Value $originalPost
function Invoke-WebRequest {
    param($Uri,$Method,$ContentType,$Body,$TimeoutSec,[switch]$UseBasicParsing)
    $response=[Net.Http.HttpResponseMessage]::new([Net.HttpStatusCode]::UnprocessableEntity)
    $response.Content=[Net.Http.StringContent]::new('{"error":"backend_body_marker"}')
    $exception=[InvalidOperationException]::new('fixture HTTP422')
    Add-Member -InputObject $exception -MemberType NoteProperty -Name Response -Value $response
    $record=[Management.Automation.ErrorRecord]::new($exception,'fixture422',[Management.Automation.ErrorCategory]::InvalidData,$Uri)
    $record.ErrorDetails=[Management.Automation.ErrorDetails]::new('{"error":"backend_body_marker"}')
    $response.Dispose()
    throw $record
}
$rejected=$false
try { Invoke-JsonPost -Url 'http://fixture' -Body '{}' | Out-Null }
catch {
    $rejected=$_.Exception.Message.Contains('backend_body_marker') -and
        $_.Exception.Data['RuntimeHttpStatusCode'] -eq 422 -and
        -not $_.Exception.Message.Contains('Cannot access a disposed object')
}
Assert-Check $rejected 'disposed_http_response_retains_body_and_status'
Assert-Check ((Get-RuntimeFileSha256 $manifest) -ceq $manifestHash) 'source_manifest_bytes_preserved'
Write-JsonFile (Join-Path $OutputRoot 'summary.json') @{status='passed';checks=$passed.ToArray();native_game_started=$false}
"PASS: $($passed.Count) original-reader snapshot import/copy, identity and HTTP error checks."
