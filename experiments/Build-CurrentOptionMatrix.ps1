[CmdletBinding()]
param(
    [string]$KnowledgeRoot = 'I:\StardewAI-KnowledgeArtifacts\game-1.6.15',
    [string]$ContentRoot = 'E:\StardewValleyAICompanion-runtime\Stardew Valley\Content',
    [string]$DecompileRoot = 'I:\StardewValleyAICompanion-decompile-linux-server-1.6.15',
    [string]$GamePath = 'E:\StardewValleyAICompanion-runtime\Stardew Valley'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$lockPath = Join-Path $PSScriptRoot 'current-option-input-lock.v1.json'
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
if ($lock.schema_version -ne 'goal_conditioned_current_option_input_lock.v1') {
    throw "Unsupported current option lock schema: $($lock.schema_version)"
}

function Assert-LockedFile {
    param([string]$Root, [object]$Descriptor)

    $rootFull = [System.IO.Path]::GetFullPath($Root).TrimEnd('\', '/')
    $path = [System.IO.Path]::GetFullPath((Join-Path $rootFull $Descriptor.relative_path))
    if (-not $path.StartsWith($rootFull + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Locked path escapes root: $($Descriptor.relative_path)"
    }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Locked source is missing: $path"
    }
    $item = Get-Item -LiteralPath $path
    $hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
    if ($item.Length -ne [long]$Descriptor.bytes -or $hash -ne [string]$Descriptor.sha256) {
        throw "Locked source drifted: $path"
    }
}

foreach ($source in $lock.repository_sources) {
    Assert-LockedFile -Root $projectRoot -Descriptor $source
}
foreach ($source in $lock.knowledge_sources) {
    Assert-LockedFile -Root $KnowledgeRoot -Descriptor $source
}

$head = (& git -C $projectRoot rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) {
    throw 'Current repository HEAD is unavailable.'
}
$baseCommit = [string]$lock.base_commit
& git -C $projectRoot merge-base --is-ancestor $baseCommit $head
if ($LASTEXITCODE -ne 0) {
    throw "Locked base commit is not an ancestor of HEAD: base=$baseCommit;head=$head"
}

$project = Join-Path $projectRoot 'tools\StardewAI.KnowledgeCompiler\StardewAI.KnowledgeCompiler.csproj'
$output = Join-Path $PSScriptRoot 'local-data\current-knowledge'
$exportRoot = Join-Path $KnowledgeRoot 'raw\game-1.6.15-20260723T093543Z'
$snapshot = Join-Path $KnowledgeRoot 'snapshots\current-live-full-snapshot.json'
$runtime = Join-Path $KnowledgeRoot 'runtime-binaries\linux-server-1.6.15-20260719'
$denominator = Join-Path $projectRoot 'catalogs\vanilla-1.6.15\native-action-denominator-freeze.json'

dotnet build $project "-p:GamePath=$GamePath" --verbosity minimal
if ($LASTEXITCODE -ne 0) { throw 'KnowledgeCompiler build failed.' }

$env:STARDEWAI_GENERATED_AT_UTC = [string]$lock.generated_at_utc
dotnet run --project $project --no-build -- `
    --export-root $exportRoot `
    --output $output `
    --content-root $ContentRoot `
    --snapshot-schema $snapshot `
    --game-assembly (Join-Path $runtime 'Stardew Valley.dll') `
    --game-data-assembly (Join-Path $runtime 'StardewValley.GameData.dll') `
    --decompile-root $DecompileRoot `
    --action-denominator-freeze $denominator
if ($LASTEXITCODE -ne 0) { throw 'KnowledgeCompiler generation failed.' }

$matrixPath = Join-Path $output 'option-governance-matrix.json'
$matrix = Get-Content -LiteralPath $matrixPath -Raw | ConvertFrom-Json
$contract = $lock.output_contract
$actualHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $matrixPath).Hash.ToLowerInvariant()
$actualBytes = (Get-Item -LiteralPath $matrixPath).Length
if ($matrix.schema_version -ne $contract.schema_version -or
    [int]$matrix.option_count -ne [int]$contract.option_count -or
    [int]$matrix.goal_template_count -ne [int]$contract.goal_template_count -or
    [int]$matrix.composite_option_count -ne [int]$contract.composite_option_count -or
    [int]$matrix.primitive_option_count -ne [int]$contract.primitive_option_count -or
    [int]$matrix.training_eligible_count -ne [int]$contract.training_eligible_count -or
    [int]$matrix.runtime_verified_count -ne [int]$contract.runtime_verified_count -or
    $actualBytes -ne [long]$contract.bytes -or
    $actualHash -ne [string]$contract.sha256) {
    throw "Current option matrix does not match its output contract: $matrixPath"
}

Write-Output "PASS: current option matrix options=$($matrix.option_count) runtime=$($matrix.runtime_verified_count) eligible=$($matrix.training_eligible_count) sha256=$actualHash"
