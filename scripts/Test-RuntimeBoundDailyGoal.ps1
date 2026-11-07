[CmdletBinding()]
param([string]$OutputRoot=(Join-Path ([IO.Path]::GetTempPath()) ('bound-daily-goal-'+[guid]::NewGuid().ToString('N'))))
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
if (Test-Path -LiteralPath $OutputRoot) { throw 'Daily goal test output exists.' }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$script:capturedArguments=@()
function dotnet {
    $script:capturedArguments=@($args | ForEach-Object { $_ })
    $global:LASTEXITCODE=0
    'argument-capture-only'
}
function Read-RuntimeLoopArtifacts {
    param($LoopRoot,$RunId,$CoreAssemblyPath,[switch]$AllowVerifiedContinuation)
    return [pscustomobject]@{argument_capture_only=$true}
}
$source=Join-Path $OutputRoot 'source.json'
Write-Utf8Text $source '{"fixture_only":true}'
$context=New-RuntimeQueueContext -LoopDll (Join-Path $OutputRoot 'StardewAI.LiveTrainingLoop.dll') `
    -ArtifactDirectory $OutputRoot -BackendUrl 'http://fixture' -SnapshotUrl 'http://fixture/snapshot' `
    -SnapshotProfile full -ExecutorRoot 'http://fixture/executor' -ProductUrl 'http://fixture/product' `
    -RunId 'argument-test' -IsolatedSavesPath (Join-Path $OutputRoot 'isolated-saves')
$checks=[Collections.Generic.List[string]]::new()
function Check([bool]$Value,[string]$Name) {
    if (-not $Value) { throw "Bound goal assertion failed: $Name" }
    $checks.Add($Name)
}
$commonAst=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1'),[ref]$null,[ref]$null)
$functionAst=$commonAst.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
    $node.Name -ceq 'Invoke-RuntimeDailyPlanStep'},$true)
$names=@($functionAst.Body.ParamBlock.Parameters | ForEach-Object { $_.Name.VariablePath.UserPath })
Check (($names -join '|') -ceq 'Context|Name|OptionId|CandidateKind|CandidateId|CandidateParameters|SourceSnapshotPath|AllowVerifiedContinuation|ExecutionTarget|GoalId') 'original_parameter_positions_preserved'
foreach ($case in 'legacy','explicit') {
    New-Item -ItemType Directory -Path (Join-Path $OutputRoot $case) | Out-Null
    $extra=if ($case -ceq 'explicit') { @{GoalId='daily.closed_loop'} } else { @{} }
    $result=Invoke-RuntimeDailyPlanStep -Context $context -Name $case -OptionId economy.buy_supplies `
        -CandidateKind interact_endpoint -CandidateId 'purchase:exact' -CandidateParameters @('continuation.quantity=1') `
        -SourceSnapshotPath $source @extra
    $arguments=[string[]]$script:capturedArguments
    $index=[array]::IndexOf($arguments,'--goal')
    Check ($result.argument_capture_only -eq $true) "${case}:original_return_contract"
    Check ($arguments -contains '--skip-training' -and $arguments -contains '--use-daily-plan' -and
        $arguments -contains 'purchase:exact' -and $arguments -contains 'continuation.quantity=1') "${case}:original_binding_preserved"
    if ($case -ceq 'legacy') { Check ($index -eq -1) 'legacy_default_not_overridden' }
    else { Check ($index -ge 0 -and $arguments[$index+1] -ceq 'daily.closed_loop' -and
        @($arguments | Where-Object { $_ -ceq '--goal' }).Count -eq 1) 'explicit_goal_forwarded_once' }
}
$parseErrors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'lib/RuntimeMoneyCropSeedAcquisition.ps1'),[ref]$null,[ref]$parseErrors)
Check (@($parseErrors).Count -eq 0) 'crop_caller_parses'
$calls=@($ast.FindAll({param($node) $node -is [Management.Automation.Language.CommandAst] -and
    $node.GetCommandName() -ceq 'Invoke-RuntimeDailyPlanStep'},$true))
Check ($calls.Count -eq 1) 'single_original_crop_dispatch_path'
$elements=$calls[0].CommandElements
$goals=@(for ($i=0;$i -lt $elements.Count-1;$i++) {
    if ($elements[$i] -is [Management.Automation.Language.CommandParameterAst] -and $elements[$i].ParameterName -ceq 'GoalId') {
        $elements[$i+1].Value
    }
})
Check ($goals.Count -eq 1 -and $goals[0] -ceq 'daily.closed_loop') 'crop_purchase_declares_outer_daily_owner'
Write-JsonFile (Join-Path $OutputRoot 'summary.json') @{status='passed';checks=$checks.ToArray();argument_capture_only=$true
    native_game_started=$false;formal_product_training_authorized=$false}
"PASS: $($checks.Count) bound-goal forwarding, original default/binding and crop caller checks."
