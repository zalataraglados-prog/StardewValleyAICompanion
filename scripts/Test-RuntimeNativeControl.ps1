param([Parameter(Mandatory)] [string] $OutputRoot)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeNativeControl.ps1')
if (Test-Path -LiteralPath $OutputRoot) { throw 'Control test output exists.' }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$script:checks = 0
$context = [pscustomobject]@{ BackendUrl = 'mock'; ArtifactDirectory = $OutputRoot; LoopDll = 'mock/Loop.dll' }
function Assert-Control($Condition, [string] $Message) {
    if (-not $Condition) { throw $Message }
    $script:checks++
}
function New-ControlSnapshot([int] $Index, [int] $Day = 6) {
    return [pscustomobject]@{ state_hash = "state-$Index"; state = @{
        time = @{ total_days = @{ value = $Day; status = 'available' } }
    } }
}
function Import-RuntimeSnapshotFile {
    param($Path, $BackendUrl, $SnapshotProfile, $CoreAssemblyPath)
    $index = [int]($Path -replace '^source-', '')
    return [pscustomobject]@{ Path = $Path; Value = (New-ControlSnapshot $index $(
        if ($script:scenario -eq 'day-drift' -and $index -gt 0) { 7 } else { 6 })) }
}
function Invoke-RuntimeRanking {
    param($BackendUrl, $GoalId, $SnapshotStateHash, $OutputPath, $OptionId, $Parameters)
    Assert-Control ($GoalId -ceq 'daily.closed_loop' -and $OptionId -ceq 'recovery.stabilize_day' -and
        $Parameters.Count -eq 1 -and $Parameters[0].name -ceq 'control_plane.native_save_boundary' -and
        $Parameters[0].value -ceq 'true') 'Control ranking changed its outer owner or save intent.'
    $index = [int]($SnapshotStateHash -replace '^state-', '')
    $ready = $index -ge $script:waitsRequired
    $candidate = [pscustomobject]@{
        option_id = 'recovery.stabilize_day'; available = $true
        candidate_id = $(if ($ready) { 'recovery:native_save_boundary' } else { 'recovery:wait_for_native_control_settle' })
        kind = $(if ($ready) { 'recovery_sleep_immediately' } else { 'recovery_native_control_settle' })
    }
    if ($script:scenario -eq 'blocked') { $candidate.available = $false }
    if ($script:scenario -eq 'wrong-kind') { $candidate.kind = 'other' }
    $candidates = if ($script:scenario -eq 'ambiguous') { @($candidate, $candidate) } else { @($candidate) }
    return [pscustomobject]@{ ranked_event_candidates = $candidates }
}
function Invoke-RuntimeDailyPlanStep {
    param($Context, $Name, $OptionId, $CandidateId, $CandidateKind, $CandidateParameters, $SourceSnapshotPath, $GoalId)
    Assert-Control ($OptionId -ceq 'recovery.stabilize_day' -and
        $CandidateId -ceq 'recovery:wait_for_native_control_settle' -and
        $CandidateKind -ceq 'recovery_native_control_settle' -and
        $GoalId -ceq 'daily.closed_loop') 'Preflight attempted sleep or another task instead of observing control.'
    $index = [int]($SourceSnapshotPath -replace '^source-', '')
    $script:executions++
    return [pscustomobject]@{
        Before = (New-ControlSnapshot $(if ($script:scenario -eq 'wrong-source') { 99 } else { $index }))
        After = (New-ControlSnapshot $(if ($script:scenario -eq 'no-progress') { $index } else { $index + 1 }))
        AfterPath = "source-$($index + 1)"
    }
}
foreach ($waits in @(0, 2, 4)) {
    $script:scenario = 'valid'; $script:waitsRequired = $waits; $script:executions = 0
    $result = Wait-RuntimeNativeSaveBoundaryControl -Context $context -SourceSnapshotPath source-0 `
        -SnapshotProfile full -TotalDay 6 -Name "valid-$waits"
    Assert-Control ($result.SourcePath -ceq "source-$waits" -and
        @($result.Observations).Count -eq $waits -and $script:executions -eq $waits) `
        'Control preflight lost observations, reordered sources, or executed sleep.'
}
foreach ($case in @('blocked', 'wrong-kind', 'ambiguous', 'wrong-source', 'no-progress', 'day-drift', 'bounded')) {
    $script:scenario = $case; $script:waitsRequired = $(if ($case -eq 'wrong-kind') { 0 } else { 5 })
    $script:executions = 0; $rejected = $false
    try {
        Wait-RuntimeNativeSaveBoundaryControl -Context $context -SourceSnapshotPath source-0 `
            -SnapshotProfile full -TotalDay 6 -Name $case -MaximumObservations 2 | Out-Null
    } catch { $rejected = $true }
    Assert-Control $rejected "Unsafe control preflight was accepted: $case"
    Assert-Control ($script:executions -le 2) 'Bounded control observation limit was exceeded.'
}
foreach ($relative in @('Invoke-RuntimeMoneyCropNativeProduction.ps1', 'lib/RuntimeMoneyCropTerminalSettlement.ps1')) {
    $source = Get-Content -LiteralPath (Join-Path $PSScriptRoot $relative) -Raw
    $tokens = $null; $errors = $null
    $null = [Management.Automation.Language.Parser]::ParseInput($source, [ref]$tokens, [ref]$errors)
    Assert-Control ($errors.Count -eq 0) "Control caller does not parse: $relative"
    Assert-Control ($source.IndexOf('Wait-RuntimeNativeSaveBoundaryControl') -lt
        $source.IndexOf('$sleep = Invoke-RuntimeDailyPlanStep')) 'Control must settle before the original sleep command.'
    Assert-Control ($source.Contains('$control.Observations') -and $source.Contains('$currentPath = $control.SourcePath')) `
        'Caller dropped control receipts or failed to rebind the native sleep source.'
}
Write-JsonFile (Join-Path $OutputRoot 'summary.json') @{
    status = 'passed'; checks = $script:checks; native_game_started = $false
    protocol_fixtures_only = $true; formal_product_training_authorized = $false
}
"PASS: $script:checks native-control preflight and source-chain checks."
