using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyCurrentStageOneCollectionTeacherFrontier(
        string outputRoot)
    {
        var root = Path.Combine(
            outputRoot,
            "current-stage-one-collection-teacher-frontier-fixture");
        Directory.CreateDirectory(root);
        var inventoryPath = Path.Combine(root, "requirements.json");
        var loweringPath = Path.Combine(root, "lowering.json");
        var catalogPath = Path.Combine(root, "master-angler-catalog.json");
        var windowsPath = Path.Combine(root, "master-angler-windows.json");
        var calibrationPath = Path.Combine(root, "route-timing.json");
        var snapshotPath = Path.Combine(root, "snapshot.json");
        var intentsPath = Path.Combine(root, "master-angler-intents.json");
        var rankingPath = Path.Combine(root, "ranking.json");
        const string stateHash = "stage-one-collection-frontier-state";

        var fish = Enumerable.Range(0, 72)
            .Select(index => new StageOneFishFixture(
                index == 0 ? "145" : "test_" + index,
                index == 0 ? "(O)145" : "(O)test_" + index,
                index == 0 ? "Sunfish" : "Test Fish " + index))
            .ToArray();
        var masterAlternatives = fish
            .Select(value => CollectionAlternative(
                value.ItemId,
                value.QualifiedItemId,
                value.DisplayName,
                "item_id",
                1,
                0,
                "native_location_fish_spawn",
                value.ItemId == "145" ? "Beach:0" : "fixture:" + value.ItemId))
            .ToArray();
        var masterGroups = fish.Zip(masterAlternatives)
            .Select(value => CollectionRequirementGroup(
                "master_angler:item:" + value.First.ItemId,
                "all_required",
                1,
                value.Second))
            .ToArray();
        Write(inventoryPath, new
        {
            schema_version = "authoritative_goal_requirement_inventory.v1",
            status = "complete",
            goal_id = "goal.grandpa_21",
            game_version = "1.6.15",
            denominator_complete = true,
            acquisition_routes_complete = true,
            requirement_sets = new object[]
            {
                RequirementSet(
                    "full_shipment",
                    CollectionRequirementGroup(
                        "full_shipment:item:24", "all_required", 1,
                        CollectionAlternative(
                            "24", "(O)24", "Parsnip", "item_id", 1, 0,
                            "harvests_as", "crop:472"))),
                RequirementSet("master_angler", masterGroups),
                RequirementSet(
                    "museum_collection",
                    CollectionRequirementGroup(
                        "museum:item:96", "all_required", 1,
                        CollectionAlternative(
                            "96", "(O)96", "Dwarf Scroll I", "item_id", 1, 0,
                            "native_geode_drop", "geode:535"))),
                RequirementSet(
                    "community_center_standard",
                    CollectionRequirementGroup(
                        "community_center:bundle:Pantry/5",
                        "choose_at_least_required_slots",
                        1,
                        CollectionAlternative(
                            "24", "(O)24", "Parsnip", "item_id", 1, 0,
                            "harvests_as", "crop:472")))
            }
        });

        var masterLoweredGroups = fish
            .Select(value => CollectionLoweredGroup(
                    "master_angler:item:" + value.ItemId,
                    1,
                    CollectionLoweredAlternative(
                        value.ItemId,
                        value.QualifiedItemId,
                        value.DisplayName,
                        "item_id",
                        1,
                        0,
                        "native_location_fish_spawn",
                        value.ItemId == "145" ? "Beach:0" : "fixture:" + value.ItemId,
                        "fishing.catch_fish")))
            .ToArray();
        Write(loweringPath, new
        {
            schema_version = "acquisition_route_option_lowering.v1",
            status = "complete",
            goal_id = "goal.grandpa_21",
            requirement_inventory_sha256 = HashFile(inventoryPath),
            requirement_sets = new object[]
            {
                LoweringSet(
                    "full_shipment",
                    CollectionLoweredGroup(
                        "full_shipment:item:24", 1,
                        CollectionLoweredAlternative(
                            "24", "(O)24", "Parsnip", "item_id", 1, 0,
                            "harvests_as", "crop:472", "farm.maintain_crops"))),
                LoweringSet("master_angler", masterLoweredGroups),
                LoweringSet(
                    "museum_collection",
                    CollectionLoweredGroup(
                        "museum:item:96", 1,
                        CollectionLoweredAlternative(
                            "96", "(O)96", "Dwarf Scroll I", "item_id", 1, 0,
                            "native_geode_drop", "geode:535", "processing.crack_geode"))),
                LoweringSet(
                    "community_center_standard",
                    CollectionLoweredGroup(
                        "community_center:bundle:Pantry/5", 1,
                        CollectionLoweredAlternative(
                            "24", "(O)24", "Parsnip", "item_id", 1, 0,
                            "harvests_as", "crop:472", "farm.maintain_crops")))
            }
        });

        Write(catalogPath, new
        {
            schema_version = "master_angler_opportunity_catalog.v1",
            status = "complete",
            goal_id = "goal.grandpa_21",
            game_version = "1.6.15",
            native_denominator_count = 72,
            source_inventory_complete = true,
            static_calendar_constraint_complete = true,
            requirement_inventory_path = Path.GetFullPath(inventoryPath),
            requirement_inventory_sha256 = HashFile(inventoryPath),
            species = fish.Select(value => new
            {
                item_id = value.ItemId,
                qualified_item_id = value.QualifiedItemId,
                display_name = value.DisplayName
            }).ToArray(),
            unresolved_species_ids = Array.Empty<string>(),
            unresolved_calendar_rule_ids = Array.Empty<string>()
        });
        var windowSpecies = fish.Select((value, index) => new
        {
            qualified_item_id = value.QualifiedItemId,
            display_name = value.DisplayName,
            acquisition_class = "rod_location_rule",
            windows = index == 0
                ? new object[]
                {
                    new
                    {
                        source_kind = "location_rule",
                        source_key = "Beach:0",
                        location_id = "Beach",
                        first_total_day = 0,
                        last_total_day = 27,
                        time_windows = new[] { new { start_time = 600, end_time = 2600 } },
                        weather_modes = new[] { "sun" },
                        dynamic_conditions = Array.Empty<string>(),
                        minimum_fishing_level = 0,
                        require_magic_bait = false,
                        training_rod_allowed = true
                    }
                }
                : Array.Empty<object>()
        }).ToArray();
        Write(windowsPath, new
        {
            schema_version = "master_angler_stage_one_window_index.v1",
            status = "complete_static_windows_dynamic_execution_pending",
            goal_id = "goal.grandpa_21",
            game_version = "1.6.15",
            catalog_path = Path.GetFullPath(catalogPath),
            catalog_sha256 = HashFile(catalogPath),
            deadline_year = 3,
            deadline_season = "spring",
            deadline_day_of_month = 1,
            deadline_total_day_exclusive = 224,
            native_denominator_count = 72,
            static_window_coverage_complete = true,
            training_label_eligible = false,
            species = windowSpecies,
            unresolved_species_ids = Array.Empty<string>()
        });
        Write(calibrationPath, StageOneRouteTimingCalibration());
        WriteStageOneCollectionSnapshot(snapshotPath, stateHash, fish);

        var intents = MasterAnglerTargetDateIntentBuilder.Build(
            windowsPath,
            snapshotPath,
            calibrationPath);
        Write(intentsPath, intents);
        var parameters = intents.Candidates.Single().Parameters;
        var sharedCandidate = CollectionCandidate(
                "shared-parsnip-harvest",
                "farm.maintain_crops",
                "harvest_crop_tile",
                "24",
                "(O)24",
                99,
                1);
        sharedCandidate.LocationId = "Farm";
        sharedCandidate.TileX = 4;
        sharedCandidate.TileY = 5;
        sharedCandidate.EstimatedTicks = 60;
        sharedCandidate.Score = 9000;
        sharedCandidate.ModelScore = 8000;
        sharedCandidate.ExpectedReward = 7000;
        var routeCandidate = CollectionCandidate(
                "master-angler-route",
                "fishing.catch_fish",
                "route_connector_tile",
                string.Empty,
                string.Empty,
                100,
                0,
                parameters.Concat(new[]
                {
                    Parameter("continuation.option_id", "fishing.catch_fish"),
                    Parameter(
                        "master_angler_time_budget_status",
                        "conservative_full_remaining_connector_path_and_terminal_reserve"),
                    Parameter("connector_kind", "building_door"),
                    Parameter("expected_target_location", "Town"),
                    Parameter("expected_arrival_tile_x", "1"),
                    Parameter("expected_arrival_tile_y", "5"),
                    Parameter("estimated_minutes", "2")
                }).ToArray());
        routeCandidate.AvailabilityClass = "master_angler_rolling_route";
        routeCandidate.LocationId = "Farm";
        routeCandidate.TileX = 2;
        routeCandidate.TileY = 4;
        routeCandidate.EstimatedTicks = 120;
        routeCandidate.Score = -9000;
        routeCandidate.ModelScore = -8000;
        routeCandidate.ExpectedReward = -7000;
        var wrongCrabCandidate = CollectionCandidate(
                "wrong-crab-output",
                "fishing.collect_crab_pots",
                "collect_crab_pot",
                "test_1",
                "(O)test_1",
                1,
                1,
                parameters.Concat(new[]
                {
                    Parameter(
                        "continuation.option_id",
                        "fishing.collect_crab_pots"),
                    Parameter(
                        "master_angler_time_budget_status",
                        "conservative_full_remaining_connector_path_and_terminal_reserve"),
                    Parameter("expected_fish_collection_eligible", "1")
                }).ToArray());
        wrongCrabCandidate.AvailabilityClass =
            "master_angler_exact_ready_crab_pot";
        Write(rankingPath, CollectionRanking(
            stateHash,
            sharedCandidate,
            routeCandidate,
            wrongCrabCandidate));

        var result = CurrentStageOneCollectionTeacherFrontierBuilder.Build(
            inventoryPath,
            loweringPath,
            rankingPath,
            snapshotPath,
            intentsPath);
        Require(result.Status == "candidate_contract_ready" &&
                result.RequirementSetCount == 4 &&
                result.CurrentCandidateMembershipEligible &&
                !result.TeacherPreferenceLabelEligible &&
                !result.UsesLearnerRankOrScore &&
                !result.EmitsNegativeLabelsForUnavailableRoutes,
            "Unified Stage 1 collection candidate contract policy drifted.");
        Require(result.MasterAngler.RequiredGroupCount == 72 &&
                result.MasterAngler.ObservedGroupCount == 72 &&
                result.MasterAngler.MissingGroupCount == 1 &&
                result.MasterAngler.CurrentIntentCount == 1 &&
                result.MasterAngler.CandidateBindings.Length == 1 &&
                result.MasterAngler.RejectedIntentCandidates.Single().CandidateId ==
                    "wrong-crab-output",
            "Master Angler current requirement binding or strict crab-pot identity drifted.");
        var shared = result.SelectionContract.CandidateChoices.Single(value =>
            value.CandidateId == "shared-parsnip-harvest");
        Require(shared.RequirementCredits.Length == 2 &&
                shared.RequirementCredits.Select(value => value.RequirementSetId)
                    .ToHashSet(StringComparer.Ordinal)
                    .SetEquals(new[]
                    {
                        "full_shipment",
                        "community_center_standard"
                    }),
            "One current candidate was not deduplicated across exact requirement credits.");
        Require(result.SelectionContract.SelectionGroups.Length == 4 &&
                result.SelectionContract.CandidateChoices.Length == 2 &&
                result.SelectionContract.SelectionGroups.All(value =>
                    value.MaximumSelectedCandidateCountThisDecision is 0 or 1),
            "Unified collection selection cardinality drifted.");

        var preference =
            CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath);
        Require(preference.Status == "ready" &&
                preference.TeacherPreferenceLabelEligible &&
                !preference.FormalTrainingAuthorized &&
                !preference.UsesLearnerRankOrScore &&
                !preference.EmitsNegativeLabelsForUnavailableRoutes &&
                preference.SelectedCandidate?.CandidateId ==
                    "master-angler-route" &&
                preference.SelectedCandidate.SelectionReason ==
                    "authoritative_current_day_deadline" &&
                preference.PairwisePreferences.Length == 1 &&
                preference.PairwisePreferences[0]
                    .FirstDifferingAuthoritativeCriterion ==
                    "authoritative_current_day_deadline" &&
                preference.CompiledPlan?.Steps.Length == 1 &&
                preference.CompiledQueue?.Status == "pending" &&
                preference.CompiledQueue.Items.Length == 1,
            "Independent current collection Teacher preference did not select and compile the authoritative deadline candidate.");

        sharedCandidate.Rank = 1;
        sharedCandidate.Score = 1_000_000;
        sharedCandidate.ModelScore = 1_000_000;
        sharedCandidate.ExpectedReward = 1_000_000;
        routeCandidate.Rank = 1000;
        routeCandidate.Score = -1_000_000;
        routeCandidate.ModelScore = -1_000_000;
        routeCandidate.ExpectedReward = -1_000_000;
        Write(rankingPath, CollectionRanking(
            stateHash,
            sharedCandidate,
            routeCandidate,
            wrongCrabCandidate));
        var learnerSignalInvariant =
            CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath);
        Require(learnerSignalInvariant.Status == "ready" &&
                learnerSignalInvariant.SelectedCandidate?.CandidateId ==
                    "master-angler-route" &&
                learnerSignalInvariant.CompiledPlan?.Steps.Length == 1 &&
                learnerSignalInvariant.CompiledQueue?.Items.Length == 1,
            "Learner rank or score changed the deterministic Teacher preference.");

        var tieA = CollectionCandidate(
            "tied-parsnip-a",
            "farm.maintain_crops",
            "harvest_crop_tile",
            "24",
            "(O)24",
            1,
            1);
        var tieB = CollectionCandidate(
            "tied-parsnip-b",
            "farm.maintain_crops",
            "harvest_crop_tile",
            "24",
            "(O)24",
            2,
            1);
        foreach (var candidate in new[] { tieA, tieB })
        {
            candidate.LocationId = "Farm";
            candidate.TileX = 4;
            candidate.TileY = 5;
            candidate.EstimatedTicks = 60;
        }
        Write(rankingPath, CollectionRanking(stateHash, tieA, tieB));
        var tiedPreference =
            CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath);
        Require(tiedPreference.Status ==
                    "blocked_authoritatively_tied_top_candidates" &&
                !tiedPreference.TeacherPreferenceLabelEligible &&
                tiedPreference.SelectedCandidate is null &&
                tiedPreference.BlockingReasons.Length == 1,
            "An arbitrary candidate ID was used to manufacture a Teacher preference tie-break.");

        intents.SourceStateHash = "stale-master-angler-intent-state";
        Write(intentsPath, intents);
        var staleIntentRejected = false;
        try
        {
            _ = CurrentStageOneCollectionTeacherFrontierBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath);
        }
        catch (InvalidDataException)
        {
            staleIntentRejected = true;
        }
        Require(staleIntentRejected,
            "A Master Angler intent from a different decision state was admitted.");
    }

}
