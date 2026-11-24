# 2026-11-24 correction: this manually scoped three-island-fish diagnostic is
# not the original global selector. Its refusal is not a global-flow blocker
# and never invalidates previously frozen Community Center capability evidence.
param(
    [Parameter(Mandatory)][string]$FrozenDirectory,
    [Parameter(Mandatory)][string]$TemplateDirectory,
    [Parameter(Mandatory)][string]$BinaryRoot
)

$ErrorActionPreference = 'Stop'
$snapshot = Join-Path $FrozenDirectory 'summer10-frozen-snapshot.json'
$ledger = Join-Path $FrozenDirectory 'ledger-after-native-read.json'
$binding = Get-Content -LiteralPath (Join-Path $FrozenDirectory 'frozen-source-binding.json') -Raw | ConvertFrom-Json
if ((Get-FileHash -LiteralPath $snapshot).Hash -cne $binding.source_snapshot_sha256 -or
    (Get-FileHash -LiteralPath $ledger).Hash -cne $binding.ledger_after_sha256) { throw 'Frozen snapshot or actual ledger changed.' }
$bootstrap = Join-Path $BinaryRoot 'StardewAI.GoalConditionedBootstrap.dll'
$dotnet = 'D:\StardewAI-TestLab\dotnet\dotnet.exe'
$executed = [Collections.Generic.List[object]]::new()

# Reuse the original report builders and existing command templates. This is
# orchestration only: no routes, forecasts, weights, or verifier rules are added.
function Invoke-OriginalSelectionCommand([string]$Name, [string[]]$Arguments) {
    $outputIndex = [Array]::IndexOf($Arguments, '--output')
    if ($outputIndex -lt 0) { throw 'Original command has no retained output.' }
    $outputPath = $Arguments[$outputIndex + 1]
    if (Test-Path -LiteralPath $outputPath) { throw ('Keep the prior report: ' + $outputPath) }
    $Arguments | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $FrozenDirectory ($Name + '-arguments.json')) -Encoding utf8
    & $dotnet @Arguments *> (Join-Path $FrozenDirectory ($Name + '.log'))
    $code = $LASTEXITCODE
    $executed.Add([ordered]@{name=$Name;exit_code=$code;command=$Arguments[1];output=$outputPath;output_exists=(Test-Path -LiteralPath $outputPath)})
    $executed.ToArray() | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $FrozenDirectory 'executed-commands.json') -Encoding utf8
    if ($code -notin @(0,2) -or !(Test-Path -LiteralPath $outputPath)) { throw ('Original selection builder stopped: ' + $Name) }
    Write-Host ($Name + ' exit=' + $code)
}

function Invoke-OriginalTemplate([string]$File, [string]$Name) {
    $arguments = [string[]](Get-Content -LiteralPath (Join-Path $TemplateDirectory $File) -Raw | ConvertFrom-Json)
    for ($i = 0; $i -lt $arguments.Length; $i++) {
        if ($arguments[$i].StartsWith($TemplateDirectory + '\', [StringComparison]::OrdinalIgnoreCase)) {
            $arguments[$i] = Join-Path $FrozenDirectory ((Split-Path -Leaf $arguments[$i]) -replace '^spring27-', 'summer10-')
        }
    }
    $arguments[0] = $bootstrap
    foreach ($pair in @{'--snapshot'=$snapshot;'--strategy-ledger'=$ledger}.GetEnumerator()) {
        $index = [Array]::IndexOf($arguments, $pair.Key)
        if ($index -ge 0) { $arguments[$index + 1] = $pair.Value }
    }
    Invoke-OriginalSelectionCommand $Name $arguments
}

$request = Get-Content -LiteralPath (Join-Path $TemplateDirectory 'spring27-island-native-catch-preference-request.json') -Raw | ConvertFrom-Json
$request.request_id = 'r2-summer10-island-native-catch-20261124'
$request.snapshot_state_hash = $binding.source_state_hash
$request.expected_ledger_revision = $binding.ledger_revision
$request | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $FrozenDirectory 'summer10-island-native-catch-preference-request.json') -Encoding utf8
Copy-Item -LiteralPath (Join-Path $TemplateDirectory 'spring27-no-supplied-forecast-manifest.json') -Destination (Join-Path $FrozenDirectory 'summer10-no-supplied-forecast-manifest.json')
Invoke-OriginalTemplate 'calendar-arguments.json' 'calendar'
$templateArguments = [string[]](Get-Content -LiteralPath (Join-Path $TemplateDirectory 'calendar-arguments.json') -Raw | ConvertFrom-Json)
$authority = [Collections.Generic.List[string]]::new()
foreach ($flag in @('--requirement-inventory','--acquisition-lowering','--master-angler-windows')) {
    $authority.Add($flag)
    $authority.Add($templateArguments[[Array]::IndexOf($templateArguments,$flag)+1])
}
$calendar = Join-Path $FrozenDirectory 'summer10-current-native-calendar-resolution.json'
$targetCalendar = Join-Path $FrozenDirectory 'summer10-target-date-calendar-current-day.json'
$unlock = Join-Path $FrozenDirectory 'summer10-target-date-unlock-current-day.json'
Invoke-OriginalSelectionCommand 'target-calendar' ([string[]](@($bootstrap,'build-current-native-acquisition-route-target-date-calendar') + $authority.ToArray() + @('--calendar-resolution',$calendar,'--snapshot',$snapshot,'--target-total-day',$binding.source_total_day.ToString(),'--output',$targetCalendar)))
Invoke-OriginalSelectionCommand 'target-unlock' ([string[]](@($bootstrap,'build-acquisition-route-target-date-unlock-state') + $authority.ToArray() + @('--calendar-resolution',$calendar,'--target-date-calendar',$targetCalendar,'--snapshot',$snapshot,'--output',$unlock)))
foreach ($entry in @(
    @('festival-arguments.json','festival'),
    @('location-arguments.json','location'),
    @('facility-arguments.json','facility'),
    @('resource-arguments-v2.json','resource'),
    @('currency-arguments-v2.json','currency'),
    @('reservation-arguments.json','reservation'),
    @('processing-arguments.json','processing'),
    @('fishing-probability-arguments.json','fishing-probability'),
    @('stochastic-retry-arguments.json','stochastic-retry'),
    @('daily-time-energy-arguments.json','daily-time-energy'),
    @('opportunity-cost-arguments.json','opportunity-cost'),
    @('island-preference-arguments.json','island-preference')
)) { Invoke-OriginalTemplate $entry[0] $entry[1] }
$preferencePath = Join-Path $FrozenDirectory 'summer10-island-native-catch-preference.json'
$preference = Get-Content -LiteralPath $preferencePath -Raw | ConvertFrom-Json
[ordered]@{
    created_at=(Get-Date -Format o);source_state_hash=$binding.source_state_hash;source_total_day=$binding.source_total_day
    cash=$binding.cash;ledger_revision=$binding.ledger_revision;actual_ledger_sha256=$binding.ledger_after_sha256
    bootstrap_sha256=(Get-FileHash -LiteralPath $bootstrap).Hash
    core_sha256=(Get-FileHash -LiteralPath (Join-Path $BinaryRoot 'StardewAI.Core.dll')).Hash
    command_count=$executed.Count;preference=$preferencePath;preference_sha256=(Get-FileHash -LiteralPath $preferencePath).Hash
    status=$preference.status;selection_disposition=$preference.selection_disposition
    candidate_denominator_complete=$preference.candidate_denominator_complete
    selected_proposal_present=($null -ne $preference.selected_proposal)
    blocking_reasons=$preference.blocking_reasons
    diagnostic_only=$true;actual_global_selector_used=$false
    scope='Manual three-island-fish scope diagnostic through original builders; not the original selected global plan and not evidence that frozen Community Center capability must be replayed.'
    formal_training_authorized=$false
} | ConvertTo-Json -Depth 6 | Tee-Object -FilePath (Join-Path $FrozenDirectory 'selection-summary.json')
