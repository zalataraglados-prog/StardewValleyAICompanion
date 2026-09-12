[CmdletBinding()]
param(
    [string]$Knowledge = 'I:\StardewAI-KnowledgeArtifacts\game-1.6.15\derived\game-1.6.15-20260723T093543Z-linux-v24\goal-dependency-index.json',
    [string]$KnowledgeRoot = 'I:\StardewAI-KnowledgeArtifacts\game-1.6.15',
    [string]$ContentRoot = 'E:\StardewValleyAICompanion-runtime\Stardew Valley\Content',
    [string]$DecompileRoot = 'I:\StardewValleyAICompanion-decompile-linux-server-1.6.15',
    [string]$Ranking = 'I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r36-round03-20260905-154816\run\live-snapshots\ranking-response-0002.json',
    [string]$FullShipmentSnapshot = 'I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r36-round03-20260905-154816\run\live-snapshots\before-snapshot-0002.json',
    [string]$Legacy = 'I:\StardewAITrainingArchive\119.91.139.160\training-plan-result-r36-round03-20260905-154816\canonical-state\datasets\policy-decision-trajectories.jsonl',
    [string]$SocialSnapshot = 'I:\StardewAITrainingLab\goal-conditioned-bootstrap-v1\artifacts\runtime-social-future-evidence-smoke\runtime-social-future-evidence-smoke-20260906-053142\social-future-snapshot.json',
    [string]$RouteTimingCalibration = 'I:\StardewAITrainingLab\goal-conditioned-bootstrap-v1\artifacts\runtime-movement-timing-calibration\runtime-movement-timing-calibration-20260906-043908\summary.json'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$bootstrap = Join-Path $PSScriptRoot 'StardewAI.GoalConditionedBootstrap\StardewAI.GoalConditionedBootstrap.csproj'
$recorder = Join-Path $PSScriptRoot 'StardewAI.DemonstrationRecorder\StardewAI.DemonstrationRecorder.csproj'
$output = Join-Path $PSScriptRoot 'local-data\output'
$evidenceLock = Join-Path $PSScriptRoot 'evidence-lock.v1.json'
$claimLedger = Join-Path $PSScriptRoot 'goal-claim-conflict-ledger.v1.json'
$scheduleGrammarContract = Join-Path $PSScriptRoot 'npc-schedule-grammar-contract.v1.json'
$sourceManifest = Join-Path $KnowledgeRoot 'raw\game-1.6.15-20260723T093543Z\manifest.json'
$frontierExpansion = Join-Path $PSScriptRoot 'goal-method-expansion-overlay.v1.json'
$frontierDependencies = Join-Path $PSScriptRoot 'goal-method-dependency-expansions.v1.json'
$isolatedTrainingAuthorization = Join-Path $PSScriptRoot 'isolated-training-authorization.v1.json'
$directionCatalogSource = Join-Path $root 'src\StardewAI.Core\Training\GrandpaDirectionCatalog.cs'
$optionMatrix = Join-Path $PSScriptRoot 'local-data\current-knowledge\option-governance-matrix.json'
$authoritativeGraph = Join-Path $PSScriptRoot 'local-data\current-knowledge\authoritative-dependency-graph.json'
$acquisitionLoweringCatalog = Join-Path $PSScriptRoot 'acquisition-route-option-lowering.v1.json'

dotnet build $bootstrap --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Bootstrap build failed.' }
dotnet build $recorder --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Recorder build failed.' }
dotnet run --project $bootstrap --no-build -- audit-evidence `
    --lock $evidenceLock `
    --knowledge-root $KnowledgeRoot `
    --content-root $ContentRoot `
    --output (Join-Path $output 'evidence-audit-v24.json')
if ($LASTEXITCODE -ne 0) { throw 'Evidence audit failed.' }
dotnet run --project $bootstrap --no-build -- audit-claims `
    --ledger $claimLedger `
    --knowledge $Knowledge `
    --decompile-root $DecompileRoot `
    --knowledge-root $KnowledgeRoot `
    --output (Join-Path $output 'claim-conflict-audit-v1.json')
if ($LASTEXITCODE -ne 0) { throw 'Claim conflict audit failed.' }
dotnet run --project $bootstrap --no-build -- audit-schedules `
    --contract $scheduleGrammarContract `
    --manifest $sourceManifest `
    --decompile-root $DecompileRoot `
    --output (Join-Path $output 'npc-schedule-grammar-audit-v1.json')
if ($LASTEXITCODE -ne 0) { throw 'NPC schedule grammar audit failed.' }
& (Join-Path $PSScriptRoot 'Build-CurrentOptionMatrix.ps1') `
    -KnowledgeRoot $KnowledgeRoot `
    -ContentRoot $ContentRoot `
    -DecompileRoot $DecompileRoot
if ($LASTEXITCODE -ne 0) { throw 'Current option matrix generation failed.' }
$requirementInventoryPath = Join-Path $output 'authoritative-requirement-inventory-v1.json'
dotnet run --project $bootstrap --no-build -- build-requirement-inventory `
    --manifest $sourceManifest `
    --knowledge $Knowledge `
    --authoritative-graph $authoritativeGraph `
    --decompile-root $DecompileRoot `
    --output $requirementInventoryPath
if ($LASTEXITCODE -ne 0) { throw 'Authoritative requirement inventory generation failed.' }
$requirementInventory = Get-Content -LiteralPath $requirementInventoryPath -Raw | ConvertFrom-Json
$requirementCounts = @{}
$requirementSets = @{}
foreach ($set in $requirementInventory.requirement_sets) {
    $requirementCounts[$set.requirement_set_id] = [int]$set.required_group_count
    $requirementSets[$set.requirement_set_id] = $set
}
if (-not [bool]$requirementInventory.denominator_complete -or
    $requirementCounts['full_shipment'] -ne 154 -or
    $requirementCounts['master_angler'] -ne 72 -or
    $requirementCounts['museum_collection'] -ne 95 -or
    $requirementCounts['community_center_standard'] -ne 30) {
    throw 'Authoritative requirement denominators do not match the exact 1.6.15 native rules.'
}
if ($requirementSets['full_shipment'].route_covered_group_count -ne 154 -or
    $requirementSets['master_angler'].route_covered_group_count -ne 72 -or
    $requirementSets['museum_collection'].route_covered_group_count -ne 95 -or
    $requirementSets['community_center_standard'].route_covered_group_count -ne 30 -or
    -not [bool]$requirementInventory.acquisition_routes_complete) {
    throw 'Authoritative acquisition route coverage regression failed.'
}
$masterAngler = @($requirementInventory.requirement_sets |
    Where-Object requirement_set_id -eq 'master_angler')
if ($masterAngler.Count -ne 1 -or -not [bool]$masterAngler[0].acquisition_routes_complete) {
    throw 'Master Angler runtime data does not provide an acquisition route for every required fish.'
}
$masterAnglerOpportunityPath = Join-Path $output 'master-angler-opportunity-catalog-v1.json'
dotnet run --project $bootstrap --no-build -- build-master-angler-opportunity-catalog `
    --requirement-inventory $requirementInventoryPath `
    --output $masterAnglerOpportunityPath
if ($LASTEXITCODE -ne 0) { throw 'Master Angler opportunity catalog generation failed.' }
$masterAnglerOpportunity = Get-Content -LiteralPath $masterAnglerOpportunityPath -Raw | ConvertFrom-Json
if ($masterAnglerOpportunity.status -ne 'complete' -or
    -not [bool]$masterAnglerOpportunity.source_inventory_complete -or
    -not [bool]$masterAnglerOpportunity.static_calendar_constraint_complete -or
    [int]$masterAnglerOpportunity.native_denominator_count -ne 72 -or
    [int]$masterAnglerOpportunity.rod_location_species_count -ne 60 -or
    [int]$masterAnglerOpportunity.mine_override_only_species_count -ne 2 -or
    [int]$masterAnglerOpportunity.mine_override_source_species_count -ne 3 -or
    [int]$masterAnglerOpportunity.mine_override_area_count -ne 4 -or
    [int]$masterAnglerOpportunity.trap_species_count -ne 10 -or
    [int]$masterAnglerOpportunity.native_get_fish_override_file_count -ne 5 -or
    @($masterAnglerOpportunity.unresolved_species_ids).Count -ne 0 -or
    @($masterAnglerOpportunity.unresolved_calendar_rule_ids).Count -ne 0) {
    throw 'Master Angler opportunity source inventory is incomplete.'
}
$yearBoundedRules = @($masterAnglerOpportunity.species |
    ForEach-Object { @($_.location_rules) } |
    Where-Object { $_.condition -match '(^|, )YEAR 2($|, )' })
if ($yearBoundedRules.Count -ne 1 -or
    [int]$yearBoundedRules[0].calendar.minimum_year -ne 2 -or
    $null -ne $yearBoundedRules[0].calendar.maximum_year) {
    throw 'Master Angler YEAR query normalization no longer matches native minimum-year semantics.'
}
$gameStateQueryEvidence = @($masterAnglerOpportunity.source_evidence |
    Where-Object source_id -eq 'native_game_state_query_rule')
if ($gameStateQueryEvidence.Count -ne 1) {
    throw 'Master Angler calendar normalization is missing native GameStateQuery evidence.'
}
$masterAnglerWindowsPath = Join-Path $output 'master-angler-stage-one-window-index-v1.json'
dotnet run --project $bootstrap --no-build -- build-master-angler-stage-one-windows `
    --catalog $masterAnglerOpportunityPath `
    --deadline-year 3 `
    --output $masterAnglerWindowsPath
if ($LASTEXITCODE -ne 0) { throw 'Master Angler Stage 1 static window generation failed.' }
$masterAnglerWindows = Get-Content -LiteralPath $masterAnglerWindowsPath -Raw | ConvertFrom-Json
$lavaEelMineWindows = @($masterAnglerWindows.species |
    Where-Object qualified_item_id -eq '(O)162' |
    ForEach-Object { @($_.windows) } |
    Where-Object { $_.source_kind -eq 'mine_override' -and [int]$_.mine_area -eq 80 })
if ($masterAnglerWindows.status -ne 'complete_static_windows_dynamic_execution_pending' -or
    -not [bool]$masterAnglerWindows.static_window_coverage_complete -or
    [bool]$masterAnglerWindows.training_label_eligible -or
    [int]$masterAnglerWindows.deadline_total_day_exclusive -ne 224 -or
    [int]$masterAnglerWindows.native_denominator_count -ne 72 -or
    @($masterAnglerWindows.unresolved_species_ids).Count -ne 0 -or
    $lavaEelMineWindows.Count -ne 1) {
    throw 'Master Angler Stage 1 static deadline windows are incomplete.'
}
$acquisitionLoweringPath = Join-Path $output 'acquisition-route-option-lowering-v1.json'
dotnet run --project $bootstrap --no-build -- build-acquisition-route-lowering `
    --requirement-inventory $requirementInventoryPath `
    --catalog $acquisitionLoweringCatalog `
    --option-matrix $optionMatrix `
    --isolated-training-authorization $isolatedTrainingAuthorization `
    --output $acquisitionLoweringPath
if ($LASTEXITCODE -ne 0) { throw 'Acquisition route option lowering failed.' }
$acquisitionLowering = Get-Content -LiteralPath $acquisitionLoweringPath -Raw | ConvertFrom-Json
$acquisitionSetIds = @($acquisitionLowering.requirement_sets | ForEach-Object requirement_set_id)
if ([int]$acquisitionLowering.requirement_set_count -ne 4 -or
    [int]$acquisitionLowering.requirement_group_count -ne 351 -or
    [int]$acquisitionLowering.route_occurrence_count -ne 1599 -or
    [int]$acquisitionLowering.observed_route_kind_count -ne 33 -or
    [int]$acquisitionLowering.classified_route_kind_count -ne 33 -or
    [int]$acquisitionLowering.admitted_route_kind_count -ne 33 -or
    [int]$acquisitionLowering.blocked_route_kind_count -ne 0 -or
    @($acquisitionLowering.unknown_route_kinds).Count -ne 0 -or
    @($acquisitionLowering.unobserved_catalog_route_kinds).Count -ne 0 -or
    @('full_shipment', 'master_angler', 'museum_collection', 'community_center_standard' |
        Where-Object { $_ -notin $acquisitionSetIds }).Count -ne 0) {
    throw 'Acquisition route lowering denominator or exact catalog coverage drifted.'
}
$expectedAcquisitionGaps = @()
$actualAcquisitionGaps = @($acquisitionLowering.route_kinds |
    Where-Object { -not [bool]$_.teacher_admission_ready } |
    ForEach-Object route_kind |
    Sort-Object)
if (@(Compare-Object $expectedAcquisitionGaps $actualAcquisitionGaps).Count -ne 0) {
    throw 'Acquisition route lowering gap identity drifted.'
}
$loweredAlternatives = @($acquisitionLowering.requirement_sets |
    ForEach-Object { @($_.groups) } |
    ForEach-Object { @($_.alternatives) })
$loweredRoutes = @($loweredAlternatives | ForEach-Object { @($_.routes) })
$requiredDependencyAxes = @(
    'calendar_window',
    'unlock_state',
    'location_route',
    'facility_capacity',
    'resource_inputs',
    'currency_budget',
    'inventory_reservation',
    'processing_lead_time',
    'stochastic_retry_budget',
    'daily_time_energy_budget',
    'opportunity_cost',
    'fresh_terminal_receipt'
)
$incompleteDependencyRoutes = @($loweredRoutes | Where-Object {
    @($_.required_downstream_dependency_axes).Count -ne $requiredDependencyAxes.Count -or
    @(Compare-Object $requiredDependencyAxes `
        @($_.required_downstream_dependency_axes)).Count -ne 0
})
$incompleteDependencyRouteKinds = @($acquisitionLowering.route_kinds | Where-Object {
    @($_.required_downstream_dependency_axes).Count -ne $requiredDependencyAxes.Count -or
    @(Compare-Object $requiredDependencyAxes `
        @($_.required_downstream_dependency_axes)).Count -ne 0
})
$unusableAlternatives = @($loweredAlternatives | Where-Object {
    -not [bool]$_.teacher_admission_ready -or
    @($_.routes | Where-Object { [bool]$_.teacher_admission_ready }).Count -eq 0
})
$unboundRoutes = @($loweredRoutes | Where-Object {
    [string]::IsNullOrWhiteSpace([string]$_.route_kind) -or
    [string]::IsNullOrWhiteSpace([string]$_.source_id) -or
    [string]::IsNullOrWhiteSpace([string]$_.source_asset) -or
    [string]::IsNullOrWhiteSpace([string]$_.source_path) -or
    @($_.endpoint_option_ids).Count -eq 0
})
if ($loweredAlternatives.Count -ne 450 -or
    $loweredRoutes.Count -ne [int]$acquisitionLowering.route_occurrence_count -or
    -not [bool]$acquisitionLowering.dependency_axis_inventory_complete -or
    @($acquisitionLowering.required_downstream_dependency_axes).Count -ne `
        $requiredDependencyAxes.Count -or
    @(Compare-Object $requiredDependencyAxes `
        @($acquisitionLowering.required_downstream_dependency_axes)).Count -ne 0 -or
    $incompleteDependencyRoutes.Count -ne 0 -or
    $incompleteDependencyRouteKinds.Count -ne 0 -or
    $unusableAlternatives.Count -ne 0 -or
    $unboundRoutes.Count -ne 0) {
    throw 'Per-requirement acquisition route lowering is incomplete.'
}
$routeCalendarResolutionPath = Join-Path $output 'acquisition-route-calendar-resolution-v1.json'
dotnet run --project $bootstrap --no-build -- build-acquisition-route-calendar-resolution `
    --requirement-inventory $requirementInventoryPath `
    --acquisition-lowering $acquisitionLoweringPath `
    --master-angler-windows $masterAnglerWindowsPath `
    --output $routeCalendarResolutionPath
if ($LASTEXITCODE -ne 0) { throw 'Acquisition route calendar resolution failed.' }
$routeCalendarResolution = Get-Content -LiteralPath $routeCalendarResolutionPath -Raw |
    ConvertFrom-Json
$calendarSupportedRouteKinds = @(
    'harvests_as',
    'sells',
    'native_crab_pot_output',
    'native_location_artifact_spot',
    'native_location_fish_spawn',
    'native_location_forage_spawn',
    'native_mine_fishing_override'
)
$calendarSupportedRoutes = @($loweredRoutes | Where-Object {
    $_.route_kind -in $calendarSupportedRouteKinds
})
$resolvedCalendarRoutes = @($routeCalendarResolution.routes | Where-Object {
    $_.status -eq 'resolved_static_source_window_target_date_pending'
})
$blockedSupportedCalendarRoutes = @($routeCalendarResolution.routes | Where-Object {
    $_.route_kind -in $calendarSupportedRouteKinds -and
    $_.status -ne 'resolved_static_source_window_target_date_pending'
})
$locationDataEvidence = @($requirementInventory.source_evidence |
    Where-Object source_id -eq 'runtime_data_locations')
$cropDataEvidence = @($requirementInventory.source_evidence |
    Where-Object source_id -eq 'runtime_data_crops')
$cropGrowthEvidence = @($requirementInventory.source_evidence |
    Where-Object source_id -eq 'native_crop_growth_rule')
$cropPlantingEvidence = @($requirementInventory.source_evidence |
    Where-Object source_id -eq 'native_crop_planting_rule')
$shopDataEvidence = @($requirementInventory.source_evidence |
    Where-Object source_id -eq 'runtime_data_shops')
$shopAccessEvidence = @($requirementInventory.source_evidence |
    Where-Object source_id -eq 'access_constraint_index')
$shopStockEvidence = @($requirementInventory.source_evidence |
    Where-Object source_id -eq 'native_shop_stock_rule')
$shopOpenEvidence = @($requirementInventory.source_evidence |
    Where-Object source_id -eq 'native_shop_open_rule')
$shopPurchaseEvidence = @($requirementInventory.source_evidence |
    Where-Object source_id -eq 'native_shop_purchase_rule')
$nativeGameStateQueryEvidence = @($requirementInventory.source_evidence |
    Where-Object source_id -eq 'native_game_state_query_rule')
$resolvedCropRoutes = @($resolvedCalendarRoutes | Where-Object {
    $_.route_kind -eq 'harvests_as'
})
$resolvedShopRoutes = @($resolvedCalendarRoutes | Where-Object {
    $_.route_kind -eq 'sells'
})
$wildSeedCropRoutes = @($resolvedCropRoutes | Where-Object {
    [bool]$_.crop_source.stochastic_outcome
})
$invalidCropRoutes = @($resolvedCropRoutes | Where-Object {
    $null -eq $_.crop_source -or
    [int]$_.crop_source.base_growth_days -le 0 -or
    @($_.crop_source.native_seasons).Count -eq 0 -or
    @($_.crop_source.possible_harvest_qualified_item_ids).Count -eq 0 -or
    $_.qualified_item_id -notin @($_.crop_source.possible_harvest_qualified_item_ids) -or
    @($_.calendar_windows | Where-Object source_kind -eq 'crop_native_season').Count -eq 0 -or
    @($_.calendar_windows | Where-Object {
        $_.source_kind -eq 'crop_season_ignored_location' -and
        $_.required_location_capability -ne 'seeds_ignore_seasons'
    }).Count -ne 0
})
$expectedWildSeedOutputDomains = @{
    'crop:495' = @('(O)16', '(O)18', '(O)20', '(O)22')
    'crop:496' = @('(O)396', '(O)398', '(O)402')
    'crop:497' = @('(O)404', '(O)406', '(O)408', '(O)410')
    'crop:498' = @('(O)412', '(O)414', '(O)416', '(O)418')
}
$invalidWildSeedCropRoutes = @($wildSeedCropRoutes | Where-Object {
    -not $expectedWildSeedOutputDomains.ContainsKey([string]$_.source_id) -or
    @(Compare-Object `
        $expectedWildSeedOutputDomains[[string]$_.source_id] `
        @($_.crop_source.possible_harvest_qualified_item_ids)).Count -ne 0 -or
    @($_.calendar_windows | Where-Object { -not [bool]$_.stochastic_outcome }).Count -ne 0
})
$invalidShopRoutes = @($resolvedShopRoutes | Where-Object {
    $null -eq $_.shop_source -or
    $_.source_id -ne ('shop:' + [string]$_.shop_source.shop_id) -or
    $_.qualified_item_id -ne $_.shop_source.data_item_qualified_id -or
    [int]$_.shop_source.stock_row_index -lt 0 -or
    [string]$_.shop_source.source_row_sha256 -notmatch '^[0-9a-f]{64}$' -or
    -not [bool]$_.shop_source.native_condition_handlers_complete -or
    -not [bool]$_.shop_source.requires_location_access_resolution -or
    -not [bool]$_.shop_source.requires_live_stock_receipt -or
    @($_.calendar_windows).Count -eq 0 -or
    @($_.calendar_windows | Where-Object {
        $_.source_kind -ne 'shop_stock_rule' -or
        $_.required_location_capability -ne 'shop_access' -or
        -not [bool]$_.requires_location_access_evidence -or
        -not [bool]$_.requires_existing_live_candidate_match
    }).Count -ne 0
})
if ($routeCalendarResolution.status -ne 'partial_static_sources_explicitly_blocked' -or
    -not [bool]$routeCalendarResolution.route_occurrence_inventory_complete -or
    [bool]$routeCalendarResolution.static_calendar_source_resolution_complete -or
    [bool]$routeCalendarResolution.training_label_eligible -or
    $locationDataEvidence.Count -ne 1 -or
    $cropDataEvidence.Count -ne 1 -or
    $cropGrowthEvidence.Count -ne 1 -or
    $cropPlantingEvidence.Count -ne 1 -or
    $shopDataEvidence.Count -ne 1 -or
    $shopAccessEvidence.Count -ne 1 -or
    $shopStockEvidence.Count -ne 1 -or
    $shopOpenEvidence.Count -ne 1 -or
    $shopPurchaseEvidence.Count -ne 1 -or
    $nativeGameStateQueryEvidence.Count -ne 1 -or
    $routeCalendarResolution.location_data_sha256 -ne $locationDataEvidence[0].sha256 -or
    $routeCalendarResolution.crop_data_sha256 -ne $cropDataEvidence[0].sha256 -or
    $routeCalendarResolution.native_crop_growth_source_sha256 -ne `
        $cropGrowthEvidence[0].sha256 -or
    $routeCalendarResolution.native_crop_planting_source_sha256 -ne `
        $cropPlantingEvidence[0].sha256 -or
    $routeCalendarResolution.shop_data_sha256 -ne $shopDataEvidence[0].sha256 -or
    $routeCalendarResolution.access_constraint_index_sha256 -ne `
        $shopAccessEvidence[0].sha256 -or
    $routeCalendarResolution.native_shop_stock_source_sha256 -ne `
        $shopStockEvidence[0].sha256 -or
    $routeCalendarResolution.native_shop_open_source_sha256 -ne `
        $shopOpenEvidence[0].sha256 -or
    $routeCalendarResolution.native_shop_purchase_source_sha256 -ne `
        $shopPurchaseEvidence[0].sha256 -or
    $routeCalendarResolution.native_game_state_query_source_sha256 -ne `
        $nativeGameStateQueryEvidence[0].sha256 -or
    [int]$routeCalendarResolution.route_occurrence_count -ne $loweredRoutes.Count -or
    [int]$routeCalendarResolution.resolved_static_source_count -ne 689 -or
    [int]$routeCalendarResolution.blocked_static_source_count -ne 910 -or
    $calendarSupportedRoutes.Count -ne 689 -or
    $resolvedCalendarRoutes.Count -ne 689 -or
    $resolvedCropRoutes.Count -ne 75 -or
    $wildSeedCropRoutes.Count -ne 8 -or
    $invalidCropRoutes.Count -ne 0 -or
    $invalidWildSeedCropRoutes.Count -ne 0 -or
    $resolvedShopRoutes.Count -ne 109 -or
    $invalidShopRoutes.Count -ne 0 -or
    @($resolvedShopRoutes | Where-Object {
        [int]$_.shop_source.available_stock -ge 0
    }).Count -ne 69 -or
    @($resolvedShopRoutes | Where-Object {
        $null -ne $_.shop_source.trade_item_id
    }).Count -ne 68 -or
    @($resolvedShopRoutes | Where-Object {
        [bool]$_.shop_source.is_recipe
    }).Count -ne 1 -or
    @($resolvedShopRoutes | Where-Object {
        @($_.calendar_windows | Where-Object stochastic_outcome).Count -gt 0
    }).Count -ne 2 -or
    @($resolvedShopRoutes | Where-Object {
        @($_.shop_source.interaction_endpoints).Count -gt 0
    }).Count -ne 27 -or
    @($resolvedShopRoutes | Where-Object {
        @($_.shop_source.door_windows).Count -gt 0
    }).Count -ne 22 -or
    @($resolvedCalendarRoutes | Where-Object {
        $_.evidence_class -eq 'runtime_location_artifact_spot_window'
    }).Count -ne 68 -or
    @($resolvedCalendarRoutes | Where-Object {
        $_.evidence_class -eq 'runtime_location_forage_window'
    }).Count -ne 165 -or
    @($resolvedCalendarRoutes | Where-Object {
        $_.evidence_class -eq 'runtime_location_nonfish_fishing_window'
    }).Count -ne 8 -or
    @($resolvedCalendarRoutes | Where-Object {
        @($_.calendar_windows).Count -eq 0
    }).Count -ne 0 -or
    $blockedSupportedCalendarRoutes.Count -ne 0) {
    throw 'Acquisition route calendar source resolution regression failed.'
}
$targetDateCalendarPath = Join-Path $output `
    'acquisition-route-target-date-calendar-day-0-v1.json'
dotnet run --project $bootstrap --no-build -- `
    build-acquisition-route-target-date-calendar `
    --requirement-inventory $requirementInventoryPath `
    --acquisition-lowering $acquisitionLoweringPath `
    --master-angler-windows $masterAnglerWindowsPath `
    --calendar-resolution $routeCalendarResolutionPath `
    --target-total-day 0 `
    --output $targetDateCalendarPath
if ($LASTEXITCODE -ne 0) {
    throw 'Acquisition route target-date calendar resolution failed.'
}
$targetDateCalendar = Get-Content -LiteralPath $targetDateCalendarPath -Raw |
    ConvertFrom-Json
$targetDateRoutes = @($targetDateCalendar.routes)
$targetDateEligibleRoutes = @($targetDateRoutes | Where-Object {
    [bool]$_.static_window_matches_target_date
})
$targetDateBlockedRoutes = @($targetDateRoutes | Where-Object {
    -not [bool]$_.calendar_axis_resolved
})
$invalidTargetDateRoutes = @($targetDateRoutes | Where-Object {
    ([bool]$_.calendar_axis_resolved -and
        $_.source_resolution_status -ne
            'resolved_static_source_window_target_date_pending') -or
    ([bool]$_.static_window_matches_target_date -and
        @($_.matching_windows).Count -eq 0) -or
    (-not [bool]$_.static_window_matches_target_date -and
        @($_.matching_windows).Count -ne 0)
})
$targetDateStaticHash = (Get-FileHash -LiteralPath $routeCalendarResolutionPath `
    -Algorithm SHA256).Hash.ToLowerInvariant()
if ($targetDateCalendar.status -ne `
        'partial_target_date_calendar_axis_source_blocks' -or
    -not [bool]$targetDateCalendar.route_occurrence_inventory_complete -or
    [bool]$targetDateCalendar.calendar_axis_resolution_complete -or
    [bool]$targetDateCalendar.training_label_eligible -or
    [int]$targetDateCalendar.target_total_day -ne 0 -or
    [int]$targetDateCalendar.route_occurrence_count -ne 1599 -or
    [int]$targetDateCalendar.calendar_axis_resolved_count -ne 689 -or
    [int]$targetDateCalendar.static_window_match_count -ne 451 -or
    [int]$targetDateCalendar.static_window_miss_count -ne 238 -or
    [int]$targetDateCalendar.blocked_static_source_count -ne 910 -or
    $targetDateRoutes.Count -ne 1599 -or
    @($targetDateRoutes.route_occurrence_id | Select-Object -Unique).Count -ne 1599 -or
    $targetDateCalendar.static_calendar_resolution_sha256 -ne `
        $targetDateStaticHash -or
    $invalidTargetDateRoutes.Count -ne 0 -or
    @($targetDateBlockedRoutes | Where-Object {
        @($_.blocking_reasons).Count -eq 0
    }).Count -ne 0 -or
    @($targetDateEligibleRoutes | Where-Object {
        @($_.pending_dynamic_conditions).Count -gt 0
    }).Count -ne 15 -or
    @($targetDateEligibleRoutes | Where-Object route_kind -eq 'harvests_as').Count -ne 75 -or
    @($targetDateEligibleRoutes | Where-Object route_kind -eq `
        'native_location_artifact_spot').Count -ne 64 -or
    @($targetDateEligibleRoutes | Where-Object route_kind -eq `
        'native_location_fish_spawn').Count -ne 163 -or
    @($targetDateEligibleRoutes | Where-Object route_kind -eq `
        'native_location_forage_spawn').Count -ne 53 -or
    @($targetDateEligibleRoutes | Where-Object route_kind -eq `
        'native_mine_fishing_override').Count -ne 3 -or
    @($targetDateEligibleRoutes | Where-Object route_kind -eq 'sells').Count -ne 93) {
    throw 'Acquisition route target-date calendar-axis regression failed.'
}
$snapshotForUnlock = Get-Content -LiteralPath $FullShipmentSnapshot -Raw |
    ConvertFrom-Json
$unlockTargetTotalDay =
    [int]$snapshotForUnlock.state.time.total_days.value
$unlockTargetDateCalendarPath = Join-Path $output `
    'acquisition-route-target-date-calendar-snapshot-day-v1.json'
dotnet run --project $bootstrap --no-build -- `
    build-acquisition-route-target-date-calendar `
    --requirement-inventory $requirementInventoryPath `
    --acquisition-lowering $acquisitionLoweringPath `
    --master-angler-windows $masterAnglerWindowsPath `
    --calendar-resolution $routeCalendarResolutionPath `
    --target-total-day $unlockTargetTotalDay `
    --output $unlockTargetDateCalendarPath
if ($LASTEXITCODE -ne 0) {
    throw 'Snapshot-date calendar resolution failed.'
}
$targetDateUnlockPath = Join-Path $output `
    'acquisition-route-target-date-unlock-state-v1.json'
dotnet run --project $bootstrap --no-build -- `
    build-acquisition-route-target-date-unlock-state `
    --requirement-inventory $requirementInventoryPath `
    --acquisition-lowering $acquisitionLoweringPath `
    --master-angler-windows $masterAnglerWindowsPath `
    --calendar-resolution $routeCalendarResolutionPath `
    --target-date-calendar $unlockTargetDateCalendarPath `
    --snapshot $FullShipmentSnapshot `
    --output $targetDateUnlockPath
if ($LASTEXITCODE -ne 0) {
    throw 'Acquisition route target-date unlock-state resolution failed.'
}
$targetDateUnlock = Get-Content -LiteralPath $targetDateUnlockPath -Raw |
    ConvertFrom-Json
$unlockRoutes = @($targetDateUnlock.routes)
$unlockBlockedRoutes = @($unlockRoutes | Where-Object {
    $_.unlock_axis_status -eq 'blocked_unlock_evidence'
})
$unlockTargetDateCalendar = Get-Content `
    -LiteralPath $unlockTargetDateCalendarPath -Raw | ConvertFrom-Json
$calendarRouteById = @{}
foreach ($calendarRoute in @($unlockTargetDateCalendar.routes)) {
    $calendarRouteById[[string]$calendarRoute.route_occurrence_id] = $calendarRoute
}
$unlockProjectionDrift = @($unlockRoutes | Where-Object {
    $sourceRoute = $calendarRouteById[[string]$_.route_occurrence_id]
    $null -eq $sourceRoute -or
    [string]$_.source_resolution_status -ne
        [string]$sourceRoute.source_resolution_status -or
    (ConvertTo-Json -InputObject @($_.matching_windows) -Depth 20 -Compress) -ne
        (ConvertTo-Json -InputObject @($sourceRoute.matching_windows) `
            -Depth 20 -Compress)
})
$unlockStaticWindowShapeDrift = @($unlockRoutes | Where-Object {
    if ([bool]$_.static_window_matches_target_date) {
        return @($_.matching_windows).Count -eq 0
    }
    return @($_.matching_windows).Count -ne 0
})
if ($targetDateUnlock.status -ne 'partial_target_date_unlock_axis_blocks' -or
    -not [bool]$targetDateUnlock.route_occurrence_inventory_complete -or
    [bool]$targetDateUnlock.unlock_axis_resolution_complete -or
    [bool]$targetDateUnlock.training_label_eligible -or
    [int]$targetDateUnlock.target_total_day -ne 37 -or
    [int]$targetDateUnlock.route_occurrence_count -ne 1599 -or
    [int]$targetDateUnlock.unlock_axis_resolved_count -ne 680 -or
    [int]$targetDateUnlock.unlock_state_match_count -ne 489 -or
    [int]$targetDateUnlock.unlock_state_miss_count -ne 0 -or
    [int]$targetDateUnlock.static_window_miss_count -ne 191 -or
    [int]$targetDateUnlock.blocked_upstream_calendar_count -ne 910 -or
    [int]$targetDateUnlock.blocked_unlock_evidence_count -ne 9 -or
    [int]$targetDateUnlock.pending_calendar_condition_count -ne 6 -or
    [int]$targetDateUnlock.pending_stochastic_condition_count -ne 1 -or
    [int]$targetDateUnlock.pending_resource_condition_count -ne 1 -or
    [int]$targetDateUnlock.pending_location_condition_count -ne 0 -or
    [int]$targetDateUnlock.unsupported_condition_count -ne 0 -or
    $unlockRoutes.Count -ne 1599 -or
    @($unlockRoutes.route_occurrence_id | Select-Object -Unique).Count -ne 1599 -or
    $unlockProjectionDrift.Count -ne 0 -or
    $unlockStaticWindowShapeDrift.Count -ne 0 -or
    $unlockBlockedRoutes.Count -ne 9 -or
    @($unlockBlockedRoutes | Where-Object {
        @($_.blocking_reasons) -notcontains `
            'game_state_query_unlock_state_missing'
    }).Count -ne 0) {
    throw 'Acquisition route target-date unlock-state regression failed.'
}
$currentFullShipmentFrontierPath = Join-Path $output 'current-full-shipment-teacher-frontier.json'
dotnet run --project $bootstrap --no-build -- build-current-full-shipment-teacher-frontier `
    --requirement-inventory $requirementInventoryPath `
    --acquisition-lowering $acquisitionLoweringPath `
    --ranking $Ranking `
    --snapshot $FullShipmentSnapshot `
    --output $currentFullShipmentFrontierPath
if ($LASTEXITCODE -ne 0) { throw 'Current Full Shipment Teacher frontier generation failed.' }
$currentFullShipmentFrontier = Get-Content -LiteralPath $currentFullShipmentFrontierPath -Raw |
    ConvertFrom-Json
if ($currentFullShipmentFrontier.status -ne 'no_current_matching_candidate' -or
    [int]$currentFullShipmentFrontier.required_group_count -ne 154 -or
    [int]$currentFullShipmentFrontier.completed_group_count -ne 6 -or
    [int]$currentFullShipmentFrontier.missing_group_count -ne 148 -or
    [int]$currentFullShipmentFrontier.current_candidate_binding_count -ne 0 -or
    [bool]$currentFullShipmentFrontier.training_label_eligible -or
    [bool]$currentFullShipmentFrontier.emits_negative_labels_for_unavailable_routes) {
    throw 'Current Full Shipment Teacher frontier fail-closed regression failed.'
}
dotnet run --project $bootstrap --no-build -- build-goal-method-graph `
    --expansion $frontierExpansion `
    --dependencies $frontierDependencies `
    --isolated-training-authorization $isolatedTrainingAuthorization `
    --requirement-inventory $requirementInventoryPath `
    --acquisition-lowering $acquisitionLoweringPath `
    --acquisition-lowering-catalog $acquisitionLoweringCatalog `
    --knowledge $Knowledge `
    --option-matrix $optionMatrix `
    --claim-ledger $claimLedger `
    --direction-catalog-source $directionCatalogSource `
    --output (Join-Path $output 'goal-method-frontier-v3.json')
if ($LASTEXITCODE -ne 0) { throw 'Goal-method frontier generation failed.' }
$frontier = Get-Content -LiteralPath `
    (Join-Path $output 'goal-method-frontier-v3.json') -Raw |
    ConvertFrom-Json
if ([int]$frontier.governance_blocked_criterion_count -ne 0 -or
    @($frontier.isolated_teacher_authorized_option_ids).Count -ne 4) {
    throw 'Isolated-training authorization did not close the expected governance-only frontier blockers.'
}
$collectionMethods = @($frontier.methods | Where-Object {
    @($_.requirement_set_readiness).Count -gt 0
})
$expectedCollectionDirections = @(
    'complete_community_center',
    'complete_full_shipment',
    'complete_master_angler',
    'complete_museum_collection'
)
$actualCollectionDirections = @($collectionMethods | ForEach-Object direction_id | Sort-Object)
$invalidCollectionReadiness = @($collectionMethods | Where-Object {
    @($_.requirement_set_readiness).Count -ne 1 -or
    -not [bool]$_.requirement_set_readiness[0].acquisition_admission_ready -or
    [int]$_.requirement_set_readiness[0].required_group_count -ne
        [int]$_.requirement_set_readiness[0].teacher_admitted_group_count
})
$alternativeGraphNodes = @($frontier.nodes | Where-Object kind -eq 'authoritative_requirement_alternative')
$routeGraphNodes = @($frontier.nodes | Where-Object kind -eq 'authoritative_acquisition_route')
if (@(Compare-Object $expectedCollectionDirections $actualCollectionDirections).Count -ne 0 -or
    $invalidCollectionReadiness.Count -ne 0 -or
    $alternativeGraphNodes.Count -ne $loweredAlternatives.Count -or
    $routeGraphNodes.Count -ne $loweredRoutes.Count -or
    @($frontier.edges | Where-Object kind -eq 'lowers_acquisition_route').Count -ne
        $loweredRoutes.Count) {
    throw 'Goal-method graph did not preserve the per-requirement acquisition bindings.'
}
$breadth = $frontier.breadth_coverage
$breadthClassTotal = [int]$breadth.executable_route_criterion_count +
    [int]$breadth.dependency_graph_pending_criterion_count +
    [int]$breadth.missing_dependency_graph_criterion_count +
    [int]$breadth.governance_blocked_criterion_count
$knownBreadthBlockerIds = @($breadth.blocker_clusters | ForEach-Object blocker_id)
$unresolvedBreadthRows = @($breadth.criteria | Where-Object route_class -ne 'executable_route')
$danglingBreadthBlockerIds = @($unresolvedBreadthRows | ForEach-Object blocker_ids |
    Where-Object { $_ -notin $knownBreadthBlockerIds } | Select-Object -Unique)
$invalidSharedDependencies = @($breadth.shared_dependency_clusters |
    Where-Object { @($_.direction_ids).Count -lt 2 })
$museumAcquisitionFamily = @($breadth.shared_dependency_clusters |
    Where-Object dependency_id -eq 'museum_item_acquisition_and_reservation')
if (-not [bool]$breadth.all_criteria_classified -or
    [int]$breadth.classified_criterion_count -ne [int]$frontier.criterion_count -or
    @($breadth.criteria).Count -ne [int]$frontier.criterion_count -or
    $breadthClassTotal -ne [int]$frontier.criterion_count -or
    @($unresolvedBreadthRows | Where-Object { @($_.blocker_ids).Count -eq 0 }).Count -ne 0 -or
    [int]$breadth.missing_dependency_graph_criterion_count -ne 0 -or
    $danglingBreadthBlockerIds.Count -ne 0 -or
    $invalidSharedDependencies.Count -ne 0 -or
    $museumAcquisitionFamily.Count -ne 1 -or
    @($museumAcquisitionFamily[0].direction_ids).Count -ne 2 -or
    'complete_museum_collection' -notin $museumAcquisitionFamily[0].direction_ids -or
    'obtain_rusty_key' -notin $museumAcquisitionFamily[0].direction_ids) {
    throw 'Goal-method breadth classification is incomplete or internally inconsistent.'
}
dotnet run --project $bootstrap --no-build -- self-test `
    --knowledge $Knowledge `
    --ranking $Ranking `
    --legacy $Legacy `
    --output-root $output
if ($LASTEXITCODE -ne 0) { throw 'Bootstrap self-test failed.' }

dotnet run --project $bootstrap --no-build -- audit-current-social-frontier `
    --snapshot $SocialSnapshot `
    --calibration $RouteTimingCalibration `
    --output (Join-Path $output 'current-social-contact-frontier.json')
if ($LASTEXITCODE -ne 0) { throw 'Current social frontier audit failed.' }
dotnet run --project $bootstrap --no-build -- plan-current-social-day `
    --snapshot $SocialSnapshot `
    --calibration $RouteTimingCalibration `
    --output (Join-Path $output 'current-social-day-itinerary.json')
if ($LASTEXITCODE -ne 0) { throw 'Current social day itinerary planning failed.' }
dotnet run --project $bootstrap --no-build -- build-current-social-teacher-label `
    --snapshot $SocialSnapshot `
    --calibration $RouteTimingCalibration `
    --output (Join-Path $output 'current-social-day-teacher-label.json')
if ($LASTEXITCODE -ne 0) { throw 'Current social teacher label binding failed.' }

Write-Output "PASS: isolated goal-conditioned bootstrap regression ($root)"
