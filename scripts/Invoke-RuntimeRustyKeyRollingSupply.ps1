[CmdletBinding()]
param(
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,
    [Parameter(Mandatory)] [string] $OutputDirectory,
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9._-]{0,127}$')] [string] $RunId,
    [Parameter(Mandatory)] [string] $BackendUrl,
    [Parameter(Mandatory)] [string] $SnapshotUrl,
    [Parameter(Mandatory)] [string] $ExecutorUrl,
    [Parameter(Mandatory)] [string] $IsolatedSavesPath,
    [Parameter(Mandatory)] [string] $RequirementInventoryPath,
    [Parameter(Mandatory)] [string] $AcquisitionLoweringPath,
    [Parameter(Mandatory)] [string] $MasterAnglerWindowsPath,
    [Parameter(Mandatory)] [string] $RouteTimingPath,
    [Parameter(Mandatory)] [string] $FishingForecastManifestPath,
    [ValidateRange(1, 256)] [int] $MaxSteps = 32,
    [ValidateRange(1024, 1048576)] [int] $MinFreeSpaceMb = 8192,
    [ValidateSet('product','runtime_harness')] [string] $ExecutionTarget = 'product'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'lib/RuntimeEvidenceCommon.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeAcquisitionPlanningPipeline.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyRollingPlanning.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyRollingExecution.ps1')
. (Join-Path $PSScriptRoot 'lib/RuntimeRustyKeyRollingLoop.ps1')
$runRoot = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) $RunId
if (Test-Path -LiteralPath $runRoot) { throw "Run already exists; blind native replay is forbidden: $runRoot" }
$loopDll = Join-Path $ProjectRoot 'tools/StardewAI.LiveTrainingLoop/bin/Release/net8.0/StardewAI.LiveTrainingLoop.dll'
$bootstrapDll = Join-Path $ProjectRoot 'experiments/StardewAI.GoalConditionedBootstrap/bin/Release/net8.0/StardewAI.GoalConditionedBootstrap.dll'
$inputFiles = @($loopDll, $bootstrapDll, $RequirementInventoryPath, $AcquisitionLoweringPath,
    $MasterAnglerWindowsPath, $RouteTimingPath, $FishingForecastManifestPath)
foreach ($directory in @((Split-Path -Parent $loopDll), (Split-Path -Parent $bootstrapDll))) {
    $inputFiles += @(Join-Path $directory 'StardewAI.Core.dll'; Join-Path $directory 'StardewAI.Contracts.dll')
}
foreach ($path in $inputFiles) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing runtime input: $path" }
}
if (-not (Test-Path -LiteralPath $IsolatedSavesPath -PathType Container)) { throw 'Isolated save directory does not exist.' }
$drive = [IO.DriveInfo]::new([IO.Path]::GetPathRoot($runRoot))
if ($drive.AvailableFreeSpace -lt ([long]$MinFreeSpaceMb * 1MB)) { throw 'Insufficient space for Rusty Key runtime artifacts.' }
New-Item -ItemType Directory -Path $runRoot | Out-Null
$inputs = [ordered]@{}
foreach ($path in $inputFiles) {
    $inputs[[IO.Path]::GetFullPath($path)] = Get-RuntimeFileSha256 -Path $path
}
Write-JsonFile -Path (Join-Path $runRoot 'run-inputs.json') -Value ([ordered]@{
    schema_version = 'stardewai.rusty_key_rolling_runtime_inputs.v1'; run_id = $RunId
    input_sha256 = $inputs; execution_target = $ExecutionTarget; isolated_saves_path = $IsolatedSavesPath
    max_steps = $MaxSteps; min_free_space_mb = $MinFreeSpaceMb; formal_training_authorized = $false
})
$context = New-RuntimeQueueContext -LoopDll $loopDll -ArtifactDirectory $runRoot -BackendUrl $BackendUrl `
    -SnapshotUrl $SnapshotUrl -SnapshotProfile full -ExecutorRoot $ExecutorUrl -ProductUrl $ExecutorUrl `
    -RunId $RunId -IsolatedSavesPath $IsolatedSavesPath
$result = Invoke-RuntimeRustyKeyRollingLoop -Context $context -BootstrapDll $bootstrapDll `
    -RequirementInventoryPath $RequirementInventoryPath -AcquisitionLoweringPath $AcquisitionLoweringPath `
    -MasterAnglerWindowsPath $MasterAnglerWindowsPath -RouteTimingPath $RouteTimingPath `
    -FishingForecastManifestPath $FishingForecastManifestPath -MaxSteps $MaxSteps `
    -MinFreeSpaceMb $MinFreeSpaceMb -ExecutionTarget $ExecutionTarget
foreach ($path in $inputs.Keys) {
    if ((Get-RuntimeFileSha256 -Path $path) -ne $inputs[$path]) {
        Write-JsonFile -Path (Join-Path $runRoot 'failure.json') -Value ([ordered]@{
            run_id = $RunId; status = 'failed'; error = "Runtime input changed during execution: $path"
            input_identity_verified = $false; formal_training_authorized = $false
        })
        throw "Runtime input changed during execution: $path"
    }
}
$result.input_identity_verified = $true
Write-JsonFile -Path (Join-Path $runRoot 'summary.json') -Value $result
$result
if ($result.status -ne 'native_key_observed') { exit 2 }
