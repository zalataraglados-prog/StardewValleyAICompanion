[CmdletBinding()]
param(
    [string] $LiveLoopDll = (Join-Path $PSScriptRoot '../tools/StardewAI.LiveTrainingLoop/bin/Release/net8.0/StardewAI.LiveTrainingLoop.dll'),
    [string] $OutputRoot = (Join-Path ([IO.Path]::GetTempPath()) ('bootstrap-managed-' + [guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
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
    Check (($names[-2..-1] -join '|') -ceq 'UseManagedCropTeacher|JointDailyCalendar') 'old_positional_parameters_preserved'
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
foreach ($name in @('UseManagedCropTeacher', 'JointDailyCalendar')) {
    $parameters = @($wrapper.FindAll({ param($node) $node -is [Management.Automation.Language.CommandParameterAst] -and
        $node.ParameterName -ceq $name }, $true))
    Check ($parameters.Count -eq 1 -and $parameters[0].Argument.Extent.Text -ceq ('$' + $name)) ('wrapper_forwards_original_switch:' + $name)
}
if (Test-Path -LiteralPath $OutputRoot) { throw 'Preserve the existing test output.' }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
@{status='passed';checks=$checks.ToArray();native_game_started=$false;formal_product_training_authorized=$false} |
    ConvertTo-Json -Depth 5 | Out-File -LiteralPath (Join-Path $OutputRoot 'summary.json') -Encoding utf8 -NoClobber
"PASS: $($checks.Count) original launcher/managed parser and default-mode checks; no game."
