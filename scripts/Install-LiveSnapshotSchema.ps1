[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceSnapshot,
    [string]$KnowledgeRoot = "I:\StardewAI-KnowledgeArtifacts\game-1.6.15",
    [string]$ExpectedGameVersion = "1.6.15",
    [string]$GamePath = "E:\StardewValleyAICompanion-runtime\Stardew Valley",
    [string]$LockFile = "",
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $PSScriptRoot
$source = (Resolve-Path -LiteralPath $SourceSnapshot).Path
$snapshotRoot = Join-Path ([System.IO.Path]::GetFullPath($KnowledgeRoot)) "snapshots"
$target = Join-Path $snapshotRoot "current-live-full-snapshot.json"
$metadataTarget = Join-Path $snapshotRoot "current-live-full-snapshot.metadata.json"
$validationCandidateRoot = Join-Path $projectRoot "artifacts\snapshot-schema-validation-candidate"
$validationRoot = Join-Path $projectRoot "artifacts\snapshot-schema-validation-current"
$validationTarget = Join-Path $validationRoot "snapshot-schema-validation.json"
$compilerProject = Join-Path $projectRoot "tools\StardewAI.KnowledgeCompiler\StardewAI.KnowledgeCompiler.csproj"
$lockPath = if ([string]::IsNullOrWhiteSpace($LockFile)) {
    Join-Path $projectRoot "knowledge-artifacts.lock.json"
} else {
    [System.IO.Path]::GetFullPath($LockFile)
}
if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) {
    throw "Knowledge artifact lock file is missing: $lockPath"
}
$lock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
if ([string]$lock.schema_version -ne "stardewai.knowledge_artifact_lock.v1") {
    throw "Unexpected knowledge artifact lock schema: $($lock.schema_version)"
}

$snapshot = Get-Content -LiteralPath $source -Raw | ConvertFrom-Json
if ([string]$snapshot.schema_version -ne "snapshot.v1") {
    throw "Source is not snapshot.v1: $source"
}
if ([string]$snapshot.game_version -ne $ExpectedGameVersion) {
    throw "Snapshot game version mismatch: expected=$ExpectedGameVersion actual=$($snapshot.game_version)"
}
if ($null -eq $snapshot.state -or $null -eq $snapshot.state.player) {
    throw "Snapshot has no state.player object: $source"
}

if (-not $NoBuild) {
    & dotnet build $compilerProject --no-restore "-p:GamePath=$GamePath" --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        throw "KnowledgeCompiler build failed with exit code $LASTEXITCODE."
    }
}
& dotnet run --project $compilerProject --no-build -- `
    --validate-snapshot-schema-only $source `
    --output $validationCandidateRoot
$validationExitCode = $LASTEXITCODE
$validationCandidate = Join-Path $validationCandidateRoot "snapshot-schema-validation.json"
if (-not (Test-Path -LiteralPath $validationCandidate -PathType Leaf)) {
    throw "Snapshot validator did not produce: $validationCandidate"
}
$validation = Get-Content -LiteralPath $validationCandidate -Raw | ConvertFrom-Json
if ($validationExitCode -ne 0 -or [int]$validation.blocking_count -ne 0) {
    throw "Snapshot schema validation blocked installation: exit=$validationExitCode blocking=$($validation.blocking_count)"
}

New-Item -ItemType Directory -Path $snapshotRoot -Force | Out-Null
New-Item -ItemType Directory -Path $validationRoot -Force | Out-Null
$sourceHash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
$incoming = Join-Path $snapshotRoot ("current-live-full-snapshot." + [guid]::NewGuid().ToString("N") + ".incoming")
$metadataIncoming = $metadataTarget + "." + [guid]::NewGuid().ToString("N") + ".incoming"
$lockIncoming = $lockPath + "." + [guid]::NewGuid().ToString("N") + ".incoming"
$validationIncoming = $validationTarget + "." + [guid]::NewGuid().ToString("N") + ".incoming"
Copy-Item -LiteralPath $source -Destination $incoming
Copy-Item -LiteralPath $validationCandidate -Destination $validationIncoming
$incomingHash = (Get-FileHash -LiteralPath $incoming -Algorithm SHA256).Hash.ToLowerInvariant()
if ($incomingHash -ne $sourceHash) {
    throw "Copied snapshot hash mismatch: source=$sourceHash incoming=$incomingHash"
}

$metadata = [ordered]@{
    schema_version = "stardewai.live_snapshot_schema_pointer.v1"
    installed_at_utc = [DateTimeOffset]::UtcNow.ToString("O")
    source_snapshot = $source
    source_sha256 = $sourceHash
    target_snapshot = $target
    game_version = [string]$snapshot.game_version
    bridge_version = [string]$snapshot.bridge_version
    state_hash = [string]$snapshot.state_hash
    required_state_factor_count = [int]$validation.required_state_factor_count
    readable_with_provenance_count = [int]$validation.readable_with_provenance_count
    contextual_or_stale_count = [int]$validation.contextual_or_stale_count
    blocking_count = [int]$validation.blocking_count
    validation_report = $validationTarget
}
$metadata | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $metadataIncoming -Encoding utf8
$metadataHash = (Get-FileHash -LiteralPath $metadataIncoming -Algorithm SHA256).Hash.ToLowerInvariant()
$metadataBytes = (Get-Item -LiteralPath $metadataIncoming).Length
& dotnet run --project $compilerProject --no-build -- `
    --update-current-snapshot-lock $lockPath `
    --output $lockIncoming `
    --snapshot-relative-path "snapshots\current-live-full-snapshot.json" `
    --snapshot-bytes ([string](Get-Item -LiteralPath $incoming).Length) `
    --snapshot-sha256 $sourceHash `
    --metadata-relative-path "snapshots\current-live-full-snapshot.metadata.json" `
    --metadata-bytes ([string]$metadataBytes) `
    --metadata-sha256 $metadataHash `
    --required-count ([string][int]$validation.required_state_factor_count) `
    --readable-count ([string][int]$validation.readable_with_provenance_count) `
    --contextual-count ([string][int]$validation.contextual_or_stale_count) `
    --blocking-count ([string][int]$validation.blocking_count)
if ($LASTEXITCODE -ne 0) {
    throw "Knowledge artifact lock update failed with exit code $LASTEXITCODE."
}
$null = Get-Content -LiteralPath $lockIncoming -Raw | ConvertFrom-Json

# Every replacement candidate is validated before the externally visible pointer advances.
Move-Item -LiteralPath $validationIncoming -Destination $validationTarget -Force
Move-Item -LiteralPath $incoming -Destination $target -Force
Move-Item -LiteralPath $metadataIncoming -Destination $metadataTarget -Force
Move-Item -LiteralPath $lockIncoming -Destination $lockPath -Force

$installedSnapshotHash = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant()
$installedMetadataHash = (Get-FileHash -LiteralPath $metadataTarget -Algorithm SHA256).Hash.ToLowerInvariant()
$installedLock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
if ($installedSnapshotHash -ne $sourceHash -or
    $installedMetadataHash -ne $metadataHash -or
    [string]$installedLock.current_snapshot.sha256 -ne $sourceHash -or
    [string]$installedLock.current_snapshot.metadata_sha256 -ne $metadataHash -or
    [int]$installedLock.current_snapshot.blocking_count -ne 0) {
    throw "Installed current snapshot, metadata, and lock failed consistency verification."
}

$metadata | ConvertTo-Json -Depth 8
