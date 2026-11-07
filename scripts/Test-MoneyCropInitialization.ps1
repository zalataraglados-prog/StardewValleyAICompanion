[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [string] $OutputRoot = (Join-Path ([IO.Path]::GetTempPath()) ('money-crop-initialization-' + [guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeMoneyCropProductionInput.ps1')

$output = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $output) { throw "Test output already exists: $output" }
$bootstrapDll = Join-Path $ProjectRoot 'experiments/StardewAI.GoalConditionedBootstrap/bin/Release/net8.0/StardewAI.GoalConditionedBootstrap.dll'
if (-not (Test-Path -LiteralPath $bootstrapDll -PathType Leaf)) {
    throw "Build the Release bootstrap before this game-free test: $bootstrapDll"
}
$passed = [Collections.Generic.List[string]]::new()
function Assert-Check([bool] $Condition, [string] $Name) {
    if (-not $Condition) { throw "Money initialization check failed: $Name" }
    $passed.Add($Name)
}
function Assert-Rejected([string] $Name, [scriptblock] $Action, [string] $Reason) {
    $rejected = $false
    $detail = 'no rejection'
    try { & $Action | Out-Null }
    catch {
        $detail = $_.Exception.ToString()
        $rejected = $detail.Contains($Reason)
    }
    if (-not $rejected) { throw "Expected $Name ($Reason); actual: $detail" }
    $passed.Add($Name)
}

$teacherScript = Join-Path $PSScriptRoot 'Initialize-MoneyRevenueCropProductionTeacherDecision.ps1'
$inputScript = Join-Path $PSScriptRoot 'Initialize-MoneyRevenueCropNativeProduction.ps1'
foreach ($path in @($PSCommandPath, $teacherScript, $inputScript)) {
    $tokens = $null
    $errors = $null
    [Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors) | Out-Null
    Assert-Check ($errors.Count -eq 0) ('parse:' + [IO.Path]::GetFileName($path))
}

$fixture = Join-Path $output 'fixture'
Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
    'self-test-money-revenue-crop-teacher-dispatch', '--output-root', $fixture)
$capture = Get-Content -LiteralPath (Join-Path $fixture 'capture-manifest.json') -Raw | ConvertFrom-Json
$original = Get-Content -LiteralPath (Join-Path $fixture 'dispatch/teacher-dispatch-manifest.json') -Raw | ConvertFrom-Json
$sourceSave = Join-Path $output 'fixture-source/Fixture_1'
Write-Utf8Text -Path (Join-Path $sourceSave 'Fixture_1') -Value '<SaveGame />'
$protected = @($capture.source_snapshot_path, $capture.layout_snapshots.path)
$hashes = @{}
foreach ($path in $protected) { $hashes[$path] = Get-RuntimeFileSha256 -Path $path }
$saveHash = Get-DirectoryContentHash -Path $sourceSave

$teacherArguments = @{
    ProjectRoot = $ProjectRoot
    SnapshotPath = [string]$capture.source_snapshot_path
    FarmLayoutSnapshotPaths = @($capture.layout_snapshots.path)
    DecisionId = [string]$original.decision_id
    DecisionRecordedAt = '2026-10-24T00:00:00Z'
    OutputRoot = (Join-Path $output 'teacher')
    OutputId = 'decision'
    SkipBuild = $true
}
$teacher = & $teacherScript @teacherArguments | Out-String | ConvertFrom-Json
$decisionPath = [string]$teacher.decision_request_path
$decision = Get-Content -LiteralPath $decisionPath -Raw | ConvertFrom-Json
$originalDecision = Get-Content -LiteralPath ([string]$original.teacher_decision_path) -Raw | ConvertFrom-Json
Assert-Check ($teacher.status -eq 'money_crop_production_teacher_decision_ready' -and
    $decision.teacher_selection_policy -ceq $originalDecision.teacher_selection_policy -and
    -not [bool]$teacher.formal_product_training_authorized) 'wrapper_uses_original_current_teacher'

$inputArguments = @{
    ProjectRoot = $ProjectRoot
    SourceSaveSlotPath = $sourceSave
    DecisionRequestPath = $decisionPath
    OutputRoot = (Join-Path $output 'production')
    InputId = 'input'
    SkipBuild = $true
}
$input = & $inputScript @inputArguments | Out-String | ConvertFrom-Json
$loaded = Read-MoneyCropNativeProductionInputManifest -ProjectRoot $ProjectRoot -Path ([string]$input.manifest_path)
Assert-Check ($input.status -eq 'native_crop_production_input_ready' -and
    $loaded.Manifest.teacher_selection_policy -ceq $decision.teacher_selection_policy -and
    -not [bool]$loaded.Manifest.formal_product_training_authorized) 'original_input_reader_accepts_full_bindings'

function Get-RunShape($Plan) {
    return [StardewAI.RuntimeSupport.CanonicalJson]::Serialize((@{
        source_state_hash = $Plan.source_state_hash
        covered_commitment_ids = $Plan.covered_commitment_ids
        first_total_day = $Plan.first_total_day
        last_total_day = $Plan.last_total_day
        days = $Plan.days
        seed_acquisitions = $Plan.seed_acquisitions
    } | ConvertTo-Json -Depth 96 -Compress))
}
$automaticPlan = Get-Content -LiteralPath ([string]$original.native_run_plan_path) -Raw | ConvertFrom-Json
Assert-Check ((Get-RunShape $loaded.RunPlan) -ceq (Get-RunShape $automaticPlan)) 'wrapper_and_automatic_dispatch_have_identical_execution_shape'
$manifestHash = Get-RuntimeFileSha256 -Path ([string]$input.manifest_path)
Assert-Rejected 'teacher_output_cannot_be_overwritten' { & $teacherScript @teacherArguments } 'already exists'
Assert-Rejected 'production_input_cannot_be_overwritten' { & $inputScript @inputArguments } 'already exists'
Assert-Rejected 'fixture_export_cannot_be_overwritten' {
    Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
        'self-test-money-revenue-crop-teacher-dispatch', '--output-root', $fixture)
} 'already exists'

$tampered = [System.Text.Json.Nodes.JsonNode]::Parse([IO.File]::ReadAllText($decisionPath))
$tampered['teacher_selection_policy'] = [System.Text.Json.Nodes.JsonValue]::Create('invented_policy')
$tamperedPath = Join-Path $output 'tampered-decision.json'
Write-Utf8Text -Path $tamperedPath -Value $tampered.ToJsonString()
$badArguments = $inputArguments.Clone()
$badArguments.DecisionRequestPath = $tamperedPath
$badArguments.InputId = 'tampered'
Assert-Rejected 'invented_policy_rejected_by_original_typed_binding' {
    & $inputScript @badArguments
} 'Teacher binding is invalid'
Assert-Check (-not (Test-Path -LiteralPath (Join-Path $output 'production/tampered/money-crop-native-production-input-manifest.json'))) 'rejected_decision_has_no_ready_manifest'

$tampered['teacher_selection_policy'] = [System.Text.Json.Nodes.JsonValue]::Create([string]$decision.teacher_selection_policy)
$tampered['teacher_candidate_evaluations'][0]['minimum_net_revenue'] = [System.Text.Json.Nodes.JsonValue]::Create([long]987654)
$driftedPath = Join-Path $output 'drifted-decision.json'
Write-Utf8Text -Path $driftedPath -Value $tampered.ToJsonString()
$badArguments.DecisionRequestPath = $driftedPath
$badArguments.InputId = 'drifted'
Assert-Rejected 'current_policy_cannot_bypass_original_exact_rebuild' {
    & $inputScript @badArguments
} 'does not rebuild exactly'
Assert-Check (-not (Test-Path -LiteralPath (Join-Path $output 'production/drifted/money-crop-native-production-input-manifest.json'))) 'drifted_decision_has_no_ready_manifest'

foreach ($path in $protected) {
    Assert-Check ((Get-RuntimeFileSha256 -Path $path) -ceq $hashes[$path]) ('unchanged:' + [IO.Path]::GetFileName($path))
}
Assert-Check ((Get-DirectoryContentHash -Path $sourceSave) -ceq $saveHash -and
    (Get-RuntimeFileSha256 -Path ([string]$input.manifest_path)) -ceq $manifestHash) 'source_save_and_accepted_manifest_preserved'
Write-JsonFile -Path (Join-Path $output 'summary.json') -Value ([ordered]@{
    status = 'money_crop_initialization_checks_passed'
    passed_count = $passed.Count
    passed = @($passed)
    fixture_only = $true
    game_started = $false
    formal_product_training_authorized = $false
})
Write-Output ("PASS: Money crop initialization ({0} checks); no game or training." -f $passed.Count)
