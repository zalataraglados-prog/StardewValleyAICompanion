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
