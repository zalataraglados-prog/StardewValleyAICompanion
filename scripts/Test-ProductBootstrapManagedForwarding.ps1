[CmdletBinding()]
param(
    [string] $LiveLoopDll = (Join-Path $PSScriptRoot '../tools/StardewAI.LiveTrainingLoop/bin/Release/net8.0/StardewAI.LiveTrainingLoop.dll'),
    [string] $OutputRoot = (Join-Path ([IO.Path]::GetTempPath()) ('bootstrap-managed-' + [guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
$ContinueGlobalDailyPlanAfterCropTeacherRun = $false
$checks = [Collections.Generic.List[string]]::new()
function Check([bool] $Value, [string] $Name) {
    if (!$Value) { throw "Managed bootstrap forwarding assertion failed: $Name" }
    $checks.Add($Name)
}
function ParseScript([string] $Path) {
    $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile($Path, [ref]$null, [ref]$errors)
    Check (@($errors).Count -eq 0) ('parses:' + [IO.Path]::GetFileName($Path))
    return $ast
}
$primary = ParseScript (Join-Path $PSScriptRoot 'Invoke-ProductBootstrapCalibration.ps1')
$wrapper = ParseScript (Join-Path $PSScriptRoot 'Invoke-GoldenWalnutFreshSaveCalibration.ps1')
foreach ($entry in @($primary, $wrapper)) {
    $names = @($entry.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
    $tail = $names[-3..-1] -join '|'
    $expected = 'UseManagedCropTeacher|JointDailyCalendar' + $(if ($entry -eq $wrapper) { '|ContinueAfterCropTeacherRun' } else { '|ContinueGlobalDailyPlanAfterCropTeacherRun' })
    Check ($tail -ceq $expected) 'old_positional_parameters_preserved'
    foreach ($name in @('UseManagedCropTeacher', 'JointDailyCalendar')) {
        Check ($names -contains $name) ('original_launcher_exposes:' + $name)
    }
}
. (Join-Path $PSScriptRoot 'lib/ProductBootstrapManagedArguments.ps1')
$calls = @($primary.FindAll({ param($node) $node -is [Management.Automation.Language.CommandAst] -and
    $node.GetCommandName() -ceq 'Get-ProductBootstrapManagedLoopArguments' }, $true))
Check ($calls.Count -eq 1) 'one_argument_adapter_no_second_runtime'
$originalCall = [scriptblock]::Create($calls[0].Extent.Text)
$sourceBindings = @($primary.FindAll({ param($node) $node -is [Management.Automation.Language.IfStatementAst] -and
    $node.Clauses[0].Item1.Extent.Text -ceq '$CaptureGoldenWalnutFreshSaveEvidence -or $resumeCropTeacher -or $UseManagedCropTeacher' }, $true))
Check ($sourceBindings.Count -eq 2) 'managed_preserves_original_snapshot_and_native_save_binding'
$default = @(Get-ProductBootstrapManagedLoopArguments)
Check ($default.Count -eq 0) 'old_default_adds_no_arguments'
$joint = @(Get-ProductBootstrapManagedLoopArguments -JointDailyCalendar)
Check (($joint -join '|') -ceq '--joint-daily-calendar') 'joint_only_does_not_enable_managed_owner'
$loopDir = Split-Path -Parent ([IO.Path]::GetFullPath($LiveLoopDll))
foreach ($name in @('StardewAI.Contracts.dll', 'StardewAI.Core.dll', 'StardewAI.GoalConditionedBootstrap.dll', 'StardewAI.LiveTrainingLoop.dll')) {
    [Reflection.Assembly]::LoadFrom((Join-Path $loopDir $name)) | Out-Null
}
$base = @('--root', (Join-Path $OutputRoot 'artifacts'), '--run-id', 'forwarding-fixture', '--no-manifest',
    '--skip-training', '--use-daily-plan', '--use-product-executor', '--max-attempts', '8', '--required-verified-actions', '1')
$old = [LiveTrainingOptions]::Parse([string[]]($base + $default))
Check (!$old.UseManagedCropTeacher -and !$old.UseJointDailyCalendar -and !$old.RequireNativeSaveBoundary) 'original_parser_keeps_old_mode'
try {
    Get-ProductBootstrapManagedLoopArguments -ContinueGlobalDailyPlanAfterCropTeacherRun | Out-Null
    throw 'Global daily continuation without a managed owner was accepted.'
} catch { Check ($_.Exception.Message.Contains('requires UseManagedCropTeacher')) 'global_daily_requires_original_managed_owner' }
$UseManagedCropTeacher = $true
$JointDailyCalendar = $true
$ResumeCropTeacherTransitionLedger = ''
$ContinueGlobalDailyPlanAfterCropTeacherRun = $true
$globalExtra = @(& $originalCall)
$globalOptions = [LiveTrainingOptions]::Parse([string[]]($base + $globalExtra + @(
    '--artifact-retention-mode', 'stop', '--require-native-save-boundary', '--save-slot', 'declared-global-slot',
    '--save-isolation-path', (Join-Path $OutputRoot 'saves'))))
Check ($globalOptions.ContinueGlobalDailyPlanAfterCropTeacherRun -and $globalOptions.ShouldContinueAfterCropTeacherRun) 'actual_launcher_continues_original_daily_planning'
Check (!$globalOptions.UseStageOneCollectionTeacher -and !$globalOptions.ContinueAfterCropTeacherRun) 'global_daily_does_not_alias_collection_control'
Check (@($globalExtra | Where-Object { $_ -ceq '--continue-global-daily-plan-after-crop-teacher-run' }).Count -eq 1) 'global_daily_flag_forwarded_once'
$ContinueGlobalDailyPlanAfterCropTeacherRun = $false
$authority = [IO.Path]::GetFullPath($LiveLoopDll)
$legacyArgs = @('--continue-after-crop-teacher-run', '--resume-crop-teacher-transition-ledger', $authority,
    '--stage-one-collection-requirement-inventory', $authority, '--stage-one-collection-acquisition-lowering', $authority,
    '--master-angler-stage-one-window-index', $authority, '--route-timing-calibration', $authority)
$legacy = [LiveTrainingOptions]::Parse([string[]]($base + $legacyArgs))
Check ($legacy.ContinueAfterCropTeacherRun -and $legacy.UseStageOneCollectionTeacher -and !$legacy.UseManagedCropTeacher -and
    $legacy.ResumeCropTeacherTransitionLedgerPath -ceq $authority) 'actual_parser_keeps_original_legacy_resume'
try {
    [LiveTrainingOptions]::Parse([string[]]($base + @('--continue-after-crop-teacher-run'))) | Out-Null
    throw 'Continuation without an original source was accepted.'
} catch { Check ($_.Exception.ToString().Contains('--resume-crop-teacher-transition-ledger')) 'actual_parser_rejects_continuation_without_source' }
try {
    [LiveTrainingOptions]::Parse([string[]](@($base | Where-Object { $_ -cne '--use-product-executor' }) + $legacyArgs)) | Out-Null
    throw 'Legacy resume without its original product boundary was accepted.'
} catch { Check ($_.Exception.ToString().Contains('requires --use-daily-plan, --use-product-executor')) 'actual_parser_retains_legacy_product_guard' }
foreach ($withCapture in @($false, $true)) {
foreach ($withJoint in @($false, $true)) {
    $UseManagedCropTeacher = $true
    $JointDailyCalendar = $withJoint
    $CaptureGoldenWalnutFreshSaveEvidence = $withCapture
    $ResumeCropTeacherTransitionLedger = ''
    $extra = @(& $originalCall)
    $savesFullPath = Join-Path $OutputRoot 'saves'
    $SaveSlot = 'declared-save'
    $SaveBoundaryMaxAttempts = 16
    $resumeCropTeacher = $false
    $arguments = @($base)
    if ($withCapture) { $arguments += '--capture-golden-walnut-fresh-save-evidence' }
    foreach ($binding in $sourceBindings) { . ([scriptblock]::Create($binding.Extent.Text)) }
    $arguments += $extra
    $parsed = [LiveTrainingOptions]::Parse([string[]]$arguments)
    Check ($parsed.UseManagedCropTeacher -and $parsed.EmitQueueExecutionReceipt -and $parsed.RequireNativeSaveBoundary) "managed_guards:$withJoint"
    Check ($parsed.UseJointDailyCalendar -eq $withJoint -and $parsed.DailyPlanMaxCandidates -eq 4) "original_calendar_and_pool:$withJoint"
    Check ($parsed.MaxQueueItemAttempts -eq 12) "original_queue_limit:$withJoint"
    Check (@($extra | Where-Object { $_ -ceq '--use-managed-crop-teacher' }).Count -eq 1) "single_owner_flag:$withJoint"
    try {
        [LiveTrainingOptions]::Parse([string[]]@($arguments | Where-Object { $_ -cne '--emit-queue-execution-receipt' })) | Out-Null
        throw 'Missing original receipt guard was accepted.'
    } catch {
        Check ($_.Exception.ToString().Contains('Managed crop Teacher calibration requires')) "original_missing_receipt_rejected:$withJoint"
    }
}
}
try {
    Get-ProductBootstrapManagedLoopArguments -UseManagedCropTeacher -LegacyCropResume | Out-Null
    throw 'Invalid managed source mode was accepted.'
} catch { Check ($_.Exception.Message.Contains('cannot fork a legacy unmanaged resume')) 'legacy_resume' }
$trajectoryGuards = @($primary.FindAll({ param($node) $node -is [Management.Automation.Language.IfStatementAst] -and
    ($node.Clauses[0].Item1.Extent.Text.StartsWith('-not $trajectoryExists -and') -or
     $node.Clauses[0].Item1.Extent.Text.StartsWith('$accepted.Count -lt 1 -and')) }, $true))
Check ($trajectoryGuards.Count -eq 2) 'original_policy_trajectory_presence_guards_retained'
$trajectoryExists = $false
$accepted = @()
$CaptureGoldenWalnutFreshSaveEvidence = $false
$resumeCropTeacher = $false
$UseManagedCropTeacher = $true
foreach ($guard in $trajectoryGuards) { . ([scriptblock]::Create($guard.Extent.Text)) }
Check $true 'explicit_managed_mechanical_calibration_does_not_require_policy_labels'
$UseManagedCropTeacher = $false
foreach ($guard in $trajectoryGuards) {
    try {
        . ([scriptblock]::Create($guard.Extent.Text))
        throw 'Ordinary bootstrap lost its original policy-trajectory requirement.'
    } catch { Check ($_.Exception.Message.StartsWith('Bootstrap calibration produced no ')) 'ordinary_mode_still_requires_original_policy_trajectory' }
}
Check ($primary.Extent.Text.Contains('$arguments += $managedLoopArguments')) 'original_dotnet_arguments_consume_adapter'
$continuationGuards = @($primary.FindAll({ param($node) $node -is [Management.Automation.Language.IfStatementAst] -and
    ($node.Clauses[0].Item1.Extent.Text -ceq '$ContinueAfterCropTeacherRun -and -not $resumeCropTeacher -and -not $UseManagedCropTeacher' -or
     $node.Clauses[0].Item1.Extent.Text -ceq '$UseManagedCropTeacher -and $ContinueAfterCropTeacherRun -and $ArtifactRetentionMode -ne ''stop''') }, $true))
Check ($continuationGuards.Count -eq 2) 'original_preflight_checks_explicit_source_and_retention'
$globalGuards = @($primary.FindAll({ param($node) $node -is [Management.Automation.Language.IfStatementAst] -and
    $node.Clauses[0].Item1.Extent.Text.StartsWith('$ContinueGlobalDailyPlanAfterCropTeacherRun -and') }, $true))
Check ($globalGuards.Count -eq 2) 'global_daily_preflight_runs_before_launcher_adapter'
foreach ($mode in @('valid', 'collection', 'schedule', 'gourmand', 'rolling')) {
    $ContinueGlobalDailyPlanAfterCropTeacherRun = $true
    $ContinueAfterCropTeacherRun = $mode -ceq 'collection'
    $RequireGoldenWalnutSourceScheduleReady = $mode -ceq 'schedule'
    $RequireGourmandContinuationReady = $mode -ceq 'gourmand'
    $ArtifactRetentionMode = if ($mode -ceq 'rolling') { 'rolling' } else { 'stop' }
    $errorText = ''
    try { foreach ($guard in $globalGuards) { . ([scriptblock]::Create($guard.Extent.Text)) } }
    catch { $errorText = $_.Exception.Message }
    Check (($mode -ceq 'valid' -and $errorText -eq '') -or
        ($mode -cne 'valid' -and $errorText.StartsWith('ContinueGlobalDailyPlanAfterCropTeacherRun'))) ('actual_global_daily_preflight:' + $mode)
}
$ContinueGlobalDailyPlanAfterCropTeacherRun = $false
$RequireGoldenWalnutSourceScheduleReady = $false
$RequireGourmandContinuationReady = $false
$ContinueAfterCropTeacherRun = $true
foreach ($mode in @('managed', 'legacy', 'missing_source', 'unsafe_rolling')) {
    $UseManagedCropTeacher = $mode -in @('managed', 'unsafe_rolling')
    $resumeCropTeacher = $mode -ceq 'legacy'
    $ArtifactRetentionMode = if ($mode -ceq 'managed') { 'stop' } else { 'rolling' }
    $errorText = ''
    try { foreach ($guard in $continuationGuards) { . ([scriptblock]::Create($guard.Extent.Text)) } }
    catch { $errorText = $_.Exception.Message }
    $expected = switch ($mode) {
        'missing_source' { 'requires ResumeCropTeacherTransitionLedger or UseManagedCropTeacher' }
        'unsafe_rolling' { 'requires ArtifactRetentionMode stop' }
        default { '' }
    }
    Check ($errorText.Contains($expected) -and (($expected -ne '') -or $errorText -eq '')) ('actual_product_continuation_preflight:' + $mode)
}
$retentionArgument = @($wrapper.FindAll({ param($node) $node -is [Management.Automation.Language.CommandParameterAst] -and
    $node.ParameterName -ceq 'ArtifactRetentionMode' }, $true))
Check ($retentionArgument.Count -eq 1) 'wrapper_keeps_single_retention_owner'
$retentionValue = $retentionArgument[0].Argument
if ($null -eq $retentionValue) {
    $elements = @($retentionArgument[0].Parent.CommandElements)
    $retentionValue = $elements[[Array]::IndexOf($elements, $retentionArgument[0]) + 1]
}
foreach ($mode in @('default', 'managed_calibration', 'managed_continue')) {
    $UseManagedCropTeacher = $mode -ne 'default'
    $ContinueAfterCropTeacherRun = $mode -ceq 'managed_continue'
    $value = & ([scriptblock]::Create($retentionValue.Extent.Text))
    $expected = if ($mode -ceq 'managed_continue') { 'stop' } else { 'rolling' }
    Check ($value -ceq $expected) ('actual_wrapper_retention:' + $mode)
}
foreach ($name in @('UseManagedCropTeacher', 'JointDailyCalendar', 'ContinueAfterCropTeacherRun')) {
    $parameters = @($wrapper.FindAll({ param($node) $node -is [Management.Automation.Language.CommandParameterAst] -and
        $node.ParameterName -ceq $name }, $true))
    Check ($parameters.Count -eq 1 -and $parameters[0].Argument.Extent.Text -ceq ('$' + $name)) ('wrapper_forwards_original_switch:' + $name)
}
if (Test-Path -LiteralPath $OutputRoot) { throw 'Preserve the existing test output.' }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
@{status='passed';checks=$checks.ToArray();native_game_started=$false;formal_product_training_authorized=$false} |
    ConvertTo-Json -Depth 5 | Out-File -LiteralPath (Join-Path $OutputRoot 'summary.json') -Encoding utf8 -NoClobber
"PASS: $($checks.Count) original launcher/managed parser and default-mode checks; no game."
