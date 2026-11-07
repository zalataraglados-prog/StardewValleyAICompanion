[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
$saved = Save-RuntimeProcessEnvironment -Names @('DOTNET_GCHeapHardLimit','COMPlus_GCHeapHardLimit','STARDEWAI_BOOTSTRAP_GC_HEAP_HARD_LIMIT')
$script:checks = 0
$script:mode = 'success'
function Check([bool]$Value,[string]$Name) {
    if (-not $Value) { throw "Bootstrap memory assertion failed: $Name" }
    $script:checks++
}
function dotnet {
    $script:seen = @{
        DotNet = $env:DOTNET_GCHeapHardLimit
        ComPlus = $env:COMPlus_GCHeapHardLimit
        Arguments = @($args)
    }
    if ($script:mode -eq 'throw') { throw 'fixture invocation failure' }
    $global:LASTEXITCODE = if ($script:mode -eq 'exit') { 7 } else { 0 }
    'fixture native output'
}
try {
    foreach ($case in @('legacy','bounded','dotnet','complus','both','exit','allowed-exit','throw')) {
        $env:STARDEWAI_BOOTSTRAP_GC_HEAP_HARD_LIMIT = if ($case -eq 'legacy') { $null } else { '0x60000000' }
        $env:DOTNET_GCHeapHardLimit = if ($case -in @('dotnet','both')) { '0x40000000' } else { $null }
        $env:COMPlus_GCHeapHardLimit = if ($case -in @('complus','both')) { '0x50000000' } else { $null }
        $before = Save-RuntimeProcessEnvironment -Names @('DOTNET_GCHeapHardLimit','COMPlus_GCHeapHardLimit','STARDEWAI_BOOTSTRAP_GC_HEAP_HARD_LIMIT')
        $script:mode = if ($case -eq 'allowed-exit') { 'exit' }
            elseif ($case -in @('exit','throw')) { $case } else { 'success' }
        $failed = $false
        $extra = if ($case -eq 'allowed-exit') { @{AllowedExitCodes=@(7)} } else { @{} }
        try { Invoke-RuntimeBootstrap -BootstrapDll 'fixture.dll' -Arguments @('fixture-command','--output','fixture.json') @extra }
        catch { $failed = $true }
        Check ($failed -eq ($case -in @('exit','throw'))) "${case}:original_failure_contract"
        Check (($script:seen.Arguments -join '|') -ceq 'fixture.dll|fixture-command|--output|fixture.json') "${case}:arguments_unchanged"
        $expected = if ($case -in @('dotnet','both')) { '0x40000000' }
            elseif ($case -in @('complus','legacy')) { $null } else { '0x60000000' }
        Check ([string]$script:seen.DotNet -ceq [string]$expected) "${case}:bounded_default_or_explicit_owner"
        Check ([string]$script:seen.ComPlus -ceq [string]$before['COMPlus_GCHeapHardLimit']) "${case}:complus_preserved"
        Check ($ErrorActionPreference -eq 'Stop') "${case}:error_preference_restored"
        foreach ($name in $before.Keys) {
            Check ([string][Environment]::GetEnvironmentVariable($name) -ceq [string]$before[$name]) "${case}:restored_$name"
        }
    }
    $parseErrors = $null
    $ast=[Management.Automation.Language.Parser]::ParseFile(
        (Join-Path $PSScriptRoot 'Invoke-RuntimeMoneyCropNativeProduction.ps1'),[ref]$null,[ref]$parseErrors)
    Check (@($parseErrors).Count -eq 0) 'crop_capture_release_parses'
    $gameHeapWrites=@($ast.FindAll({param($node)
        $node -is [Management.Automation.Language.AssignmentStatementAst] -and
        $node.Left.Extent.Text -in @('$env:DOTNET_GCHeapHardLimit','$env:COMPlus_GCHeapHardLimit')
    },$true))
    Check ($gameHeapWrites.Count -eq 0) 'crop_policy_does_not_set_game_heap_environment'
    $barriers=@($ast.FindAll({param($node)
        $node -is [Management.Automation.Language.InvokeMemberExpressionAst] -and
        $node.Extent.Text -ceq '[GC]::Collect()'
    },$true))
    Check ($barriers.Count -eq 1) 'one_coarse_offline_proof_collection_barrier'
    foreach($name in 'initial','expectedSourceSnapshot','current','result','route','routeArtifact',
        'preparation','homeRoute','control','observation','sleep') {
        $uses=@($ast.FindAll({param($node)
            $node -is [Management.Automation.Language.VariableExpressionAst] -and
            $node.VariablePath.UserPath -ceq $name
        },$true)|Sort-Object {$_.Extent.StartOffset})
        $last=$uses[-1]
        Check ($last.Extent.EndOffset -lt $barriers[0].Extent.StartOffset -and
            $last.Parent -is [Management.Automation.Language.AssignmentStatementAst] -and
            $last.Parent.Left -eq $last) "capture_$name`:released_and_not_used_after_barrier"
    }
}
finally { Restore-RuntimeProcessEnvironment -Values $saved }
"PASS: $script:checks Bootstrap heap budget, explicit overrides, failure cleanup and original argument checks."
