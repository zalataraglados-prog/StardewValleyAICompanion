[CmdletBinding()]
param(
    [string] $OutputRoot = (Join-Path $PSScriptRoot ("..\artifacts\fishing-artifact-mapping\" + [guid]::NewGuid().ToString("N")))
)
$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")
. (Join-Path $PSScriptRoot "lib\RuntimeFishingCollectionEvidence.ps1")
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
if (Test-Path -LiteralPath $OutputRoot) { throw "Fixture output must be new: $OutputRoot" }
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot "Invoke-RuntimeFishingDailyPlanSmoke.ps1"), [ref]$tokens, [ref]$errors)
if ($errors.Count -gt 0) { throw "Fishing smoke parse failed." }
$skillSource = $ast.Extent.Text
foreach ($required in @('"--precompiled-queue", $skillTeacherQueue.QueuePath',
    '"--max-attempts", "1"', '"--required-verified-actions", "1"',
    '"--max-queue-item-attempts", "1"', '"--emit-queue-execution-receipt"')) {
    if (-not $skillSource.Contains($required)) { throw "Skills dispatch omitted original bounded queue arguments: $required" }
}
$function = $ast.Find({ param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq "Resolve-CompactTrainingAttempts"
}, $true)
if ($null -eq $function) { throw "Original compact mapping function is missing." }
. ([scriptblock]::Create($function.Extent.Text))
foreach ($queue in @(
    @{ file = "compiled-queue-0001.json"; id = "initial" },
    @{ file = "continuation-compiled-queue-0001-item-0002.json"; id = "continuation" },
    @{ file = "compiled-queue-0005.json"; id = "sparse" },
    @{ file = "compiled-queue-0006.json"; id = "trap" }
)) {
    Write-JsonFile (Join-Path $OutputRoot $queue.file) @{ queue_id = $queue.id }
}
foreach ($ordinal in @("0001", "0005", "0006")) {
    Write-JsonFile (Join-Path $OutputRoot "execution-$ordinal.json") @{ status = "fixture_only" }
}
$dataset = Join-Path $OutputRoot "rows.jsonl"
$rows = @("continuation", "sparse") | ForEach-Object {
    @{ row_id = $_; queue_id = $_; action_features = @{ option_ids = @("executor.catch_fish"); changed_facts = @(); features = @{ boolean = @() } } } |
        ConvertTo-Json -Depth 6 -Compress
}
$rows += '{"row_id":"trap","queue_id":"trap","action_features":{"option_ids":["executor.collect_crab_pot"],"changed_facts":[{"path":"player.fish_caught[(O)372]","before":"0,0","after":"1,8"}],"features":{"boolean":[{"name":"execution.primitive_verified","value":true}]}}}'
Write-Utf8Text -Path $dataset -Value ($rows -join "`n")
$attempts = @(Resolve-CompactTrainingAttempts -SnapshotDir $OutputRoot -DatasetPath $dataset)
if ($attempts.Count -ne 3 -or $attempts[0].iteration -ne 1 -or $attempts[1].iteration -ne 5 -or
    (Split-Path -Leaf $attempts[0].queue_path) -ne "continuation-compiled-queue-0001-item-0002.json" -or
    $attempts[2].option_id -ne "executor.collect_crab_pot" -or $attempts[2].observed_caught_qualified_item_id -ne "(O)372") {
    throw "Compact rows did not bind original continuation/sparse queue identities."
}
$unknown = Join-Path $OutputRoot "unknown.jsonl"
Write-Utf8Text -Path $unknown -Value '{"row_id":"unknown","queue_id":"absent"}'
try {
    Resolve-CompactTrainingAttempts -SnapshotDir $OutputRoot -DatasetPath $unknown | Out-Null
    throw "Expected missing queue identity rejection."
}
catch { if ($_.Exception.Message -notlike 'Compact JSONL training row did not map*') { throw } }
Write-JsonFile (Join-Path $OutputRoot "continuation-compiled-queue-0001-item-0003.json") @{ queue_id = "continuation" }
try {
    Resolve-CompactTrainingAttempts -SnapshotDir $OutputRoot -DatasetPath $dataset | Out-Null
    throw "Expected duplicate queue identity rejection."
}
catch { if ($_.Exception.Message -notlike 'Duplicate compiled queue_id*') { throw } }
foreach ($case in @(
    @{ before = "0,0"; after = "1,8"; expected = "(O)372" },
    @{ before = "0,0"; after = "2,8"; expected = "(O)372" },
    @{ before = "2,8"; after = "3,8"; expected = "(O)372" },
    @{ before = "0,0"; after = "0,0"; expected = "" },
    @{ before = "2,8"; after = "1,8"; expected = "" },
    @{ before = "unknown"; after = "1,8"; expected = "" },
    @{ before = "0,0"; after = "1,unknown"; expected = "" }
)) {
    $fact = @{ path = "player.fish_caught[(O)372]"; before = $case.before; after = $case.after }
    if ((Read-RuntimeFishingObservedCatch -OptionId "executor.collect_crab_pot" -Facts @($fact)) -cne $case.expected) {
        throw "Trap collection event projection mismatch: $($case.before) -> $($case.after)"
    }
}
$fact = @{ path = "player.fish_caught[(O)372]"; before = "0,0"; after = "1,8" }
if ((Read-RuntimeFishingObservedCatch "executor.collect_crab_pot" @($fact, $fact)) -ne "" -or
    (Read-RuntimeFishingObservedCatch "executor.claim_fishing_item_grab_reward" @($fact)) -ne "" -or
    (Read-RuntimeFishingObservedCatch "executor.catch_fish" @($fact)) -ne "" -or
    (Read-RuntimeFishingObservedCatch "executor.catch_fish" @(@{ path = "fishing.caught_qualified_item_id"; after = "(O)708" })) -ne "(O)708") {
    throw "Trap duplicate/reward isolation or original rod projection failed."
}
foreach ($case in @(
    @{ args = @{}; prefix = "Crab-pot collection capture requires" },
    @{ args = @{ StandaloneCollectionTeacher = $true; IsolateSave = $true; IsolateMods = $true; FishingLocation = "Beach" }; prefix = "Crab-pot collection capture requires" },
    @{ args = @{ StandaloneCollectionTeacher = $true; IsolateSave = $true; IsolateMods = $true; FishingLocation = "Farm"; SetupLocationFixture = $true }; prefix = "Configure only one isolated" },
    @{ args = @{ StandaloneCollectionTeacher = $true; IsolateSave = $true; IsolateMods = $true; FishingLocation = "Farm"; TaskFamily = "ordinary_quest" }; prefix = "Standalone collection Teacher capture requires" }
)) {
    $arguments = $case.args
    try {
        & (Join-Path $PSScriptRoot "Invoke-RuntimeFishingDailyPlanSmoke.ps1") -CrabPotQualifiedItemId "(O)372" @arguments
        throw "Expected preflight rejection before game/build/network operations."
    }
    catch { if (-not $_.Exception.Message.StartsWith($case.prefix)) { throw } }
}
try {
    & (Join-Path $PSScriptRoot "Invoke-RuntimeFishingDailyPlanSmoke.ps1") -IncludeCrabPotCounterfactual
    throw "Expected undeclared-boundary rejection before game/build/network operations."
}
catch { if (-not $_.Exception.Message.StartsWith("Crab-pot counterfactual requires")) { throw } }
try {
    & (Join-Path $PSScriptRoot "Invoke-RuntimeFishingDailyPlanSmoke.ps1") -MasterAnglerTerminalBoundaryDay 7
    throw "Expected terminal-boundary rejection before game/build/network operations."
}
catch { if (-not $_.Exception.Message.StartsWith("Terminal save boundary capture requires")) { throw } }
foreach ($case in @(
    @{ name = "IsolateMods"; value = $false },
    @{ name = "IsolateSave"; value = $false },
    @{ name = "IncludeCrabPotCounterfactual"; value = $false },
    @{ name = "SkillTerminalBoundaryLevel"; value = 0 },
    @{ name = "StandaloneCollectionTeacher"; value = $true },
    @{ name = "FishingMaxAttempts"; value = 2 },
    @{ name = "TaskFamily"; value = "ordinary_quest" }
)) {
    $arguments = @{
        StandaloneSkillTeacher = $true; SkillTerminalBoundaryLevel = 15
        IsolateSave = $true; IsolateMods = $true; FishingLocation = "Farm"
        IncludeCrabPotCounterfactual = $true; CrabPotQualifiedItemId = "(O)716"
    }
    $arguments[$case.name] = $case.value
    try {
        & (Join-Path $PSScriptRoot "Invoke-RuntimeFishingDailyPlanSmoke.ps1") @arguments
        throw "Expected invalid Skills preflight rejection before game/build/network operations."
    }
    catch { if (-not $_.Exception.Message.StartsWith("Skills threshold capture requires")) { throw } }
}
try {
    & (Join-Path $PSScriptRoot "Invoke-RuntimeFishingDailyPlanSmoke.ps1") -SkillTerminalBoundaryLevel 15
    throw "Expected Skills mode rejection before game/build/network operations."
}
catch { if (-not $_.Exception.Message.StartsWith("Skills terminal boundary requires")) { throw } }
Write-Output "PASS: original continuation/sparse and trap mapping, 11 catch-event checks, 6 original and 8 Skills preflight rejections; unknown/duplicate queues rejected. Fixtures retained at $OutputRoot"
