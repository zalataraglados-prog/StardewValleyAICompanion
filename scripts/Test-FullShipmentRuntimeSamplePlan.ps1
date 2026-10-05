[CmdletBinding()]
param(
    [string] $ProjectRoot = "",
    [string] $StaticInventory =
        "local-data\full-shipment-evidence-milestone\static-inventory.json",
    [string] $Plan =
        "catalogs\vanilla-1.6.15\full-shipment-runtime-sample-plan.json",
    [string] $Output = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
}
. (Join-Path $PSScriptRoot "lib\RuntimeEvidenceCommon.ps1")

function Resolve-PlanPath {
    param([string] $Path)
    return Resolve-RuntimeEvidenceInputPath `
        -ProjectRoot $ProjectRoot -Path $Path
}

function Get-StringSetKey {
    param($Values)
    return [string]::Join(",", @(
        $Values | ForEach-Object { [string]$_ } | Sort-Object))
}

function Get-RouteSignature {
    param($Value)
    return @(
        [string]$Value.route_kind,
        [string]$Value.source_evidence_mode,
        (Get-StringSetKey $Value.endpoint_option_ids),
        (Get-StringSetKey $Value.supporting_option_ids),
        (Get-StringSetKey $Value.inline_support_transition_kinds)
    ) -join "|"
}

$inventoryPath = Resolve-PlanPath $StaticInventory
$planPath = Resolve-PlanPath $Plan
foreach ($path in @($inventoryPath, $planPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Full Shipment sample-plan input is missing: $path"
    }
}

$inventory = Get-Content -LiteralPath $inventoryPath -Raw | ConvertFrom-Json
$planValue = Get-Content -LiteralPath $planPath -Raw | ConvertFrom-Json
$entries = @($planValue.entries)
$strata = @($inventory.runtime_sample_strata)
$routes = @($inventory.routes)

if ([string]$inventory.schema_version -ne
        "full_shipment_static_compilability_inventory.v2" -or
    -not [bool]$inventory.static_compilability_complete -or
    [int]$inventory.required_group_count -ne 154 -or
    [int]$inventory.route_occurrence_count -ne 641 -or
    [int]$inventory.runtime_sample_stratum_count -ne 26 -or
    $strata.Count -ne 26) {
    throw "Full Shipment static inventory is not the complete 154/641/26 denominator."
}
if ([string]$planValue.schema_version -ne
        "full_shipment_runtime_sample_plan.v1" -or
    [bool]$planValue.formal_training_authorized -or
    [int]$planValue.expected_stratum_count -ne 26 -or
    $entries.Count -ne 26) {
    throw "Full Shipment runtime sample plan is invalid."
}
if (@($entries.scenario | Sort-Object -Unique).Count -ne $entries.Count -or
    @($entries.route_kind | Sort-Object -Unique).Count -ne $entries.Count) {
    throw "Full Shipment runtime sample plan duplicates a scenario or route kind."
}
if (@($entries | Where-Object run_batch -eq "high_risk").Count -ne 7 -or
    @($entries | Where-Object shared_shipping_anchor).Count -ne 1) {
    throw "Full Shipment runtime sample plan batch boundaries drifted."
}

$rows = foreach ($entry in $entries) {
    $matches = @($routes | Where-Object {
        [string]$_.requirement_id -eq [string]$entry.requirement_id -and
        [string]$_.qualified_item_id -eq [string]$entry.qualified_item_id -and
        [string]$_.route_kind -eq [string]$entry.route_kind
    })
    if ($matches.Count -eq 0) {
        throw "Scenario $($entry.scenario) does not bind any exact static route."
    }
    if (@($matches | Where-Object {
            -not [bool]$_.static_compilation_ready -or
            [string]$entry.endpoint_option_id -notin
                @($_.endpoint_option_ids | ForEach-Object { [string]$_ })
        }).Count -gt 0) {
        throw "Scenario $($entry.scenario) is not statically executable by its declared endpoint."
    }
    $signatures = @($matches | ForEach-Object {
        Get-RouteSignature $_
    } | Sort-Object -Unique)
    if ($signatures.Count -ne 1) {
        throw "Scenario $($entry.scenario) crosses runtime execution signatures."
    }
    $route = $matches[0]
    $signature = $signatures[0]
    $matchingStrata = @($strata | Where-Object {
        (Get-RouteSignature $_) -eq $signature
    })
    if ($matchingStrata.Count -ne 1) {
        throw "Scenario $($entry.scenario) does not bind one exact runtime stratum."
    }
    [ordered]@{
        scenario = [string]$entry.scenario
        run_batch = [string]$entry.run_batch
        stratum_id = [string]$matchingStrata[0].stratum_id
        route_kind = [string]$route.route_kind
        candidate_route_occurrence_count = $matches.Count
        candidate_route_occurrence_ids = @($matches.route_occurrence_id |
            ForEach-Object { [string]$_ } | Sort-Object)
        requirement_id = [string]$route.requirement_id
        qualified_item_id = [string]$route.qualified_item_id
        endpoint_option_id = [string]$entry.endpoint_option_id
        shared_shipping_anchor = [bool]$entry.shared_shipping_anchor
    }
}

if (@($rows.stratum_id | Sort-Object -Unique).Count -ne $strata.Count) {
    throw "Full Shipment runtime sample plan does not cover all 26 strata exactly once."
}

$report = [ordered]@{
    schema_version = "full_shipment_runtime_sample_plan_reconciliation.v1"
    status = "verified_exact_26_stratum_plan"
    static_inventory_sha256 = (Get-FileHash -LiteralPath $inventoryPath `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    sample_plan_sha256 = (Get-FileHash -LiteralPath $planPath `
        -Algorithm SHA256).Hash.ToLowerInvariant()
    required_group_count = 154
    route_occurrence_count = 641
    runtime_sample_stratum_count = 26
    planned_scenario_count = $rows.Count
    high_risk_scenario_count = @($rows | Where-Object {
        $_.run_batch -eq "high_risk"
    }).Count
    formal_training_authorized = $false
    rows = @($rows)
}

if (-not [string]::IsNullOrWhiteSpace($Output)) {
    Write-JsonFile -Path (Resolve-PlanPath $Output) -Value $report
}
$report | ConvertTo-Json -Depth 16
