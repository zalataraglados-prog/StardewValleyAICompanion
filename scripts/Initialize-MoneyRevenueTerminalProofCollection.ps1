[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string] $WorkDirectory,
    [string] $ProjectRoot =
        (Resolve-Path (Join-Path $PSScriptRoot "..")).Path,
    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

function Resolve-RequiredFile {
    param([string] $Path, [string] $Label)
    $fullPath = Resolve-RuntimeEvidenceInputPath `
        -ProjectRoot $ProjectRoot -Path $Path
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
        throw "$Label is missing: $fullPath"
    }
    return $fullPath
}

function Test-SamePath {
    param([string] $Left, [string] $Right)
    return [string]::Equals(
        [IO.Path]::GetFullPath($Left),
        [IO.Path]::GetFullPath($Right),
        [StringComparison]::OrdinalIgnoreCase)
}

$workPath = Resolve-RuntimeEvidenceInputPath `
    -ProjectRoot $ProjectRoot -Path $WorkDirectory
if (-not (Test-Path -LiteralPath $workPath -PathType Container)) {
    throw "Money Revenue comparison work directory is missing: $workPath"
}
$planPath = Resolve-RequiredFile `
    -Path (Join-Path $workPath `
        "money-revenue-terminal-proof-collection-plan.json") `
    -Label "Money Revenue terminal-proof collection plan"
$catalogPath = Resolve-RequiredFile `
    -Path (Join-Path $workPath `
        "money-revenue-counterfactual-proof-request-catalog.json") `
    -Label "Money Revenue counterfactual request catalog"
$fragmentRoot = Join-Path $workPath "terminal-proof-fragments"
$fragmentIndexPath = Join-Path $workPath `
    "money-revenue-terminal-proof-fragment-index.json"
$checkpointRequestPath = Join-Path $workPath `
    "money-revenue-terminal-proof-collection-checkpoint-request.json"
$checkpointPath = Join-Path $workPath `
    "money-revenue-terminal-proof-collection-checkpoint.json"
$terminalProofIndexPath = Join-Path $workPath `
    "money-revenue-terminal-proof-index.json"
$bootstrapProject = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj"
$bootstrapDll = Join-Path $ProjectRoot `
    "experiments\StardewAI.GoalConditionedBootstrap\bin\Release\net8.0\StardewAI.GoalConditionedBootstrap.dll"

if (-not $SkipBuild) {
    & dotnet build $bootstrapProject -c Release --no-restore --nologo |
        Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Goal-conditioned bootstrap Release build failed."
    }
}
if (-not (Test-Path -LiteralPath $bootstrapDll -PathType Leaf)) {
    throw "Goal-conditioned bootstrap Release DLL is missing: $bootstrapDll"
}

$plan = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
$catalog = Get-Content -LiteralPath $catalogPath -Raw | ConvertFrom-Json
if ([string]$plan.schema_version -ne
        "money_revenue_terminal_proof_collection_plan.v2" -or
    [string]$plan.status -ne "terminal_proof_collection_required" -or
    [bool]$plan.terminal_proof_index_ready -or
    [bool]$plan.formal_product_training_authorized -or
    [string]$catalog.schema_version -ne
        "money_revenue_counterfactual_proof_request_catalog.v1" -or
    -not [bool]$catalog.counterfactual_proof_collection_ready -or
    [bool]$catalog.formal_product_training_authorized -or
    -not (Test-SamePath `
        -Left ([string]$plan.counterfactual_request_catalog_path) `
        -Right $catalogPath)) {
    throw "Money Revenue terminal-proof collection inputs are invalid."
}
$requestById = @{}
foreach ($request in @($catalog.requests)) {
    $requestId = [string]$request.request_id
    if ([string]::IsNullOrWhiteSpace($requestId) -or
        $requestById.ContainsKey($requestId)) {
        throw "Money Revenue counterfactual request identity is invalid."
    }
    $requestById.Add($requestId, $request)
}

New-Item -ItemType Directory -Force -Path $fragmentRoot | Out-Null
if (Test-Path -LiteralPath $fragmentIndexPath -PathType Leaf) {
    $fragmentIndex = Get-Content -LiteralPath $fragmentIndexPath -Raw |
        ConvertFrom-Json
    if ([string]$fragmentIndex.schema_version -ne
            "money_revenue_terminal_proof_fragment_index.v1" -or
        [bool]$fragmentIndex.formal_product_training_authorized -or
        -not (Test-SamePath `
            -Left ([string]$fragmentIndex.collection_plan_path) `
            -Right $planPath)) {
        throw "Existing Money Revenue terminal-proof fragment index is invalid."
    }
    $fragments = @($fragmentIndex.alternatives)
}
else {
    $fragments = @()
    Write-JsonFile -Path $fragmentIndexPath -Value ([ordered]@{
        schema_version = "money_revenue_terminal_proof_fragment_index.v1"
        collection_plan_path = $planPath
        alternatives = @()
        formal_product_training_authorized = $false
    })
}

Write-JsonFile -Path $checkpointRequestPath -Value ([ordered]@{
    schema_version =
        "money_revenue_terminal_proof_collection_checkpoint_request.v1"
    collection_plan_path = $planPath
    fragment_index_path = $fragmentIndexPath
    formal_product_training_authorized = $false
})
Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
    "build-money-revenue-terminal-proof-collection-checkpoint",
    "--request", $checkpointRequestPath,
    "--output", $checkpointPath)

$knownRequestIds = @($fragments | ForEach-Object {
    [string]$_.request_id
})
if ($knownRequestIds.Count -ne
    @($knownRequestIds | Sort-Object -Unique).Count) {
    throw "Existing Money Revenue terminal-proof fragments contain duplicate requests."
}
$materializedCount = 0
foreach ($item in @($plan.work_items | Where-Object {
    [string]$_.collection_mode -eq "reuse_verified_shop_sale_episode"
})) {
    $requestId = [string]$item.request_id
    if ($knownRequestIds -contains $requestId) { continue }
    if (-not $requestById.ContainsKey($requestId)) {
        throw "Reusable Money Revenue request is outside the catalog: $requestId"
    }
    $request = $requestById[$requestId]
    $episodePath = Resolve-RequiredFile `
        -Path ([string]$item.reusable_native_episode_path) `
        -Label "Reusable native shop-sale episode"
    $episodeSha256 = (Get-FileHash -LiteralPath $episodePath `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($episodeSha256 -ne
        ([string]$item.reusable_native_episode_sha256).ToLowerInvariant()) {
        throw "Reusable native shop-sale episode hash drifted: $requestId"
    }
    $episode = Get-Content -LiteralPath $episodePath -Raw | ConvertFrom-Json
    $beforeSnapshotPath = Resolve-RequiredFile `
        -Path ([string]$request.terminal_snapshot_path) `
        -Label "Reusable shop-sale terminal request snapshot"
    $beforeSnapshotSha256 = Get-RuntimeFileSha256 -Path $beforeSnapshotPath
    if (-not (Test-SamePath `
            -Left ([string]$item.terminal_snapshot_path) `
            -Right $beforeSnapshotPath) -or
        $beforeSnapshotSha256 -ne
            ([string]$request.terminal_snapshot_sha256).ToLowerInvariant() -or
        $beforeSnapshotSha256 -ne
            ([string]$item.terminal_snapshot_sha256).ToLowerInvariant()) {
        throw "Reusable shop-sale terminal request boundary drifted: $requestId"
    }
    $afterSnapshotPath = Resolve-RequiredFile `
        -Path ([string]$episode.after_snapshot_path) `
        -Label "Reusable shop-sale after snapshot"
    $beforeLedgerPath = Resolve-RequiredFile `
        -Path ([string]$request.terminal_strategy_ledger_path) `
        -Label "Terminal strategy ledger"
    $safeRequestId = $requestId -replace '[^a-zA-Z0-9._-]', '_'
    $fragmentDirectory = Join-Path $fragmentRoot `
        ("{0:D4}-{1}" -f [int]$item.ordinal, $safeRequestId)
    New-Item -ItemType Directory -Force -Path $fragmentDirectory | Out-Null
    $afterLedgerPath = Join-Path $fragmentDirectory `
        "after-terminal-strategy-ledger.json"
    if (Test-Path -LiteralPath $afterLedgerPath -PathType Leaf) {
        $beforeLedgerSha256 = (Get-FileHash -LiteralPath $beforeLedgerPath `
            -Algorithm SHA256).Hash
        $afterLedgerSha256 = (Get-FileHash -LiteralPath $afterLedgerPath `
            -Algorithm SHA256).Hash
        if ($beforeLedgerSha256 -ne $afterLedgerSha256) {
            throw "Persisted after-terminal strategy ledger drifted: $requestId"
        }
    }
    else {
        Copy-Item -LiteralPath $beforeLedgerPath `
            -Destination $afterLedgerPath
    }

    $beforeCapturePath = Join-Path $fragmentDirectory `
        "before-terminal-strategy-ledger-capture.json"
    $afterCapturePath = Join-Path $fragmentDirectory `
        "after-terminal-strategy-ledger-capture.json"
    Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
        "build-strategy-ledger-boundary-capture",
        "--boundary-kind", "before_terminal_action",
        "--request-id", $requestId,
        "--concrete-alternative-id", ([string]$request.concrete_alternative_id),
        "--run-id", ([string]$episode.run_id),
        "--snapshot", $beforeSnapshotPath,
        "--strategy-ledger", $beforeLedgerPath,
        "--output", $beforeCapturePath)
    Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
        "build-strategy-ledger-boundary-capture",
        "--boundary-kind", "after_terminal_action",
        "--request-id", $requestId,
        "--concrete-alternative-id", ([string]$request.concrete_alternative_id),
        "--run-id", ([string]$episode.run_id),
        "--snapshot", $afterSnapshotPath,
        "--strategy-ledger", $afterLedgerPath,
        "--output", $afterCapturePath)

    $recurrenceManifestPath = Join-Path $fragmentDirectory `
        "money-revenue-recurrence-proof-manifest.json"
    $recurrenceReceiptPath = Join-Path $fragmentDirectory `
        "money-revenue-recurrence-proof-receipt.json"
    Write-JsonFile -Path $recurrenceManifestPath -Value ([ordered]@{
        schema_version = "money_revenue_recurrence_proof_manifest.v1"
        transitions = @([ordered]@{
            transition_id = "terminal-shop-sale:$requestId"
            proof_kind = "native_shop_sale"
            shop_sale_episode_path = $episodePath
            queue_path = ""
            before_snapshot_path = ""
            execution_receipt_path = ""
            after_snapshot_path = ""
            run_id = ""
            executor_version = ""
            selected_candidate_id = ""
        })
        formal_product_training_authorized = $false
    })
    Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
        "build-money-revenue-recurrence-proof",
        "--manifest", $recurrenceManifestPath,
        "--output", $recurrenceReceiptPath)
    $fragments += [ordered]@{
        request_id = $requestId
        before_strategy_ledger_capture_path = $beforeCapturePath
        after_strategy_ledger_capture_path = $afterCapturePath
        route_transitions = @()
        shop_sale_episode_path = $episodePath
        shipping_deposit_transition = $null
        revenue_recurrence_manifest_path = $recurrenceManifestPath
        revenue_recurrence_receipt_path = $recurrenceReceiptPath
    }
    $knownRequestIds += $requestId
    $materializedCount++
}

Write-JsonFile -Path $fragmentIndexPath -Value ([ordered]@{
    schema_version = "money_revenue_terminal_proof_fragment_index.v1"
    collection_plan_path = $planPath
    alternatives = @($fragments)
    formal_product_training_authorized = $false
})
Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
    "build-money-revenue-terminal-proof-collection-checkpoint",
    "--request", $checkpointRequestPath,
    "--output", $checkpointPath)
$checkpoint = Get-Content -LiteralPath $checkpointPath -Raw |
    ConvertFrom-Json
if ([bool]$checkpoint.terminal_proof_index_ready) {
    Invoke-RuntimeBootstrap -BootstrapDll $bootstrapDll -Arguments @(
        "build-money-revenue-terminal-proof-index",
        "--request", $checkpointRequestPath,
        "--checkpoint", $checkpointPath,
        "--output", $terminalProofIndexPath)
}

[ordered]@{
    status = [string]$checkpoint.status
    request_count = [int]$checkpoint.request_count
    newly_materialized_offline_fragment_count = $materializedCount
    verified_fragment_count = [int]$checkpoint.verified_fragment_count
    pending_request_count = [int]$checkpoint.pending_request_count
    pending_request_ids = @($checkpoint.pending_request_ids)
    terminal_proof_index_ready =
        [bool]$checkpoint.terminal_proof_index_ready
    fragment_index_path = $fragmentIndexPath
    checkpoint_path = $checkpointPath
    terminal_proof_index_path = if (
        [bool]$checkpoint.terminal_proof_index_ready) {
        $terminalProofIndexPath
    } else { "" }
    formal_product_training_authorized = $false
} | ConvertTo-Json -Depth 12
