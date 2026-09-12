using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    public static void RunCurrentCollection(string outputRoot)
    {
        var fullPath = Path.GetFullPath(outputRoot);
        VerifyCurrentFullShipmentTeacherFrontier(fullPath);
        VerifyCurrentCollectionTeacherFrontier(fullPath);
        VerifyCurrentStageOneCollectionTeacherFrontier(fullPath);
    }

    private static void VerifyCurrentCollectionTeacherFrontier(
        string outputRoot)
    {
        var fixtureRoot = Path.Combine(
            outputRoot,
            "current-collection-teacher-frontier-fixture");
        Directory.CreateDirectory(fixtureRoot);
        var inventoryPath = Path.Combine(fixtureRoot, "requirements.json");
        var loweringPath = Path.Combine(fixtureRoot, "lowering.json");
        var rankingPath = Path.Combine(fixtureRoot, "ranking.json");
        var snapshotPath = Path.Combine(fixtureRoot, "snapshot.json");
        const string stateHash = "collection-frontier-state";

        Write(inventoryPath, new
        {
            schema_version = "authoritative_goal_requirement_inventory.v1",
            status = "complete",
            goal_id = "goal.grandpa_21",
            denominator_complete = true,
            acquisition_routes_complete = true,
            requirement_sets = new object[]
            {
                new
                {
                    requirement_set_id = "museum_collection",
                    required_group_count = 2,
                    route_covered_group_count = 2,
                    acquisition_routes_complete = true,
                    groups = new object[]
                    {
                        CollectionRequirementGroup(
                            "museum:item:390",
                            "all_required",
                            1,
                            CollectionAlternative(
                                "390", "(O)390", "Stone", "item_id", 1, 0,
                                "native_geode_drop", "geode:535")),
                        CollectionRequirementGroup(
                            "museum:item:96",
                            "all_required",
                            1,
                            CollectionAlternative(
                                "96", "(O)96", "Dwarf Scroll I", "item_id", 1, 0,
                                "native_geode_drop", "geode:535"))
                    }
                },
                new
                {
                    requirement_set_id = "community_center_standard",
                    required_group_count = 2,
                    route_covered_group_count = 2,
                    acquisition_routes_complete = true,
                    groups = new object[]
                    {
                        CollectionRequirementGroup(
                            "community_center:bundle:Pantry/5",
                            "choose_at_least_required_slots",
                            1,
                            CollectionAlternative(
                                "24", "(O)24", "Parsnip", "item_id", 5, 2,
                                "harvests_as", "crop:472"),
                            CollectionAlternative(
                                "188", "(O)188", "Green Bean", "item_id", 5, 2,
                                "harvests_as", "crop:473")),
                        CollectionRequirementGroup(
                            "community_center:bundle:Vault/23",
                            "choose_at_least_required_slots",
                            1,
                            CollectionAlternative(
                                "-1", "", "", "money_payment", 2500, 2500,
                                "native_money_payment", "money"))
                    }
                }
            }
        });
        Write(loweringPath, new
        {
            schema_version = "acquisition_route_option_lowering.v1",
            status = "complete",
            goal_id = "goal.grandpa_21",
            requirement_inventory_sha256 = HashFile(inventoryPath),
            dependency_axis_inventory_complete = true,
            required_downstream_dependency_axes =
                StageOneCollectionRouteDependencyAxes.Required,
            requirement_sets = new object[]
            {
                new
                {
                    requirement_set_id = "museum_collection",
                    required_group_count = 2,
                    runtime_admitted_group_count = 2,
                    teacher_admitted_group_count = 2,
                    groups = new object[]
                    {
                        CollectionLoweredGroup(
                            "museum:item:390", 1,
                            CollectionLoweredAlternative(
                                "390", "(O)390", "Stone", "item_id", 1, 0,
                                "native_geode_drop", "geode:535",
                                "processing.crack_geode")),
                        CollectionLoweredGroup(
                            "museum:item:96", 1,
                            CollectionLoweredAlternative(
                                "96", "(O)96", "Dwarf Scroll I", "item_id", 1, 0,
                                "native_geode_drop", "geode:535",
                                "processing.crack_geode"))
                    }
                },
                new
                {
                    requirement_set_id = "community_center_standard",
                    required_group_count = 2,
                    runtime_admitted_group_count = 2,
                    teacher_admitted_group_count = 2,
                    groups = new object[]
                    {
                        CollectionLoweredGroup(
                            "community_center:bundle:Pantry/5", 1,
                            CollectionLoweredAlternative(
                                "24", "(O)24", "Parsnip", "item_id", 5, 2,
                                "harvests_as", "crop:472", "farm.maintain_crops"),
                            CollectionLoweredAlternative(
                                "188", "(O)188", "Green Bean", "item_id", 5, 2,
                                "harvests_as", "crop:473", "farm.maintain_crops")),
                        CollectionLoweredGroup(
                            "community_center:bundle:Vault/23", 1,
                            CollectionLoweredAlternative(
                                "-1", "", "", "money_payment", 2500, 2500,
                                "native_money_payment", "money",
                                "community_center.donate_bundle_items"))
                    }
                }
            }
        });
        WriteCollectionSnapshotFixture(snapshotPath, stateHash, includeMuseumRows: true);
        Write(rankingPath, CollectionRanking(
            stateHash,
            CollectionCandidate(
                "museum-direct", "museum.donate_items", "donate_museum_item",
                "96", "(O)96", 1, 1,
                Parameter("expected_donated_count_before", "1"),
                Parameter("expected_donated_count_after", "2"),
                Parameter("expected_stack_before", "1"),
                Parameter("expected_stack_after", "0"),
                Parameter("museum_total_donatable_items", "2"),
                Parameter("expected_collection_complete_after", "true")),
            CollectionCandidate(
                "museum-acquire", "processing.crack_geode", "process_geode",
                "96", "(O)96", 2, 1),
            CollectionCandidate(
                "museum-route", "museum.donate_items", "route_connector_tile",
                "96", "(O)96", 3, 1,
                Parameter("continuation.option_id", "museum.donate_items"),
                Parameter("continuation.target_location", "ArchaeologyHouse"),
                Parameter("continuation.inventory_slot_index", "4"),
                Parameter("continuation.item_id", "96"),
                Parameter("continuation.qualified_item_id", "(O)96")),
            CollectionCandidate(
                "museum-route-wrong-slot", "museum.donate_items",
                "route_connector_tile", "96", "(O)96", 4, 1,
                Parameter("continuation.option_id", "museum.donate_items"),
                Parameter("continuation.target_location", "ArchaeologyHouse"),
                Parameter("continuation.inventory_slot_index", "-1"),
                Parameter("continuation.item_id", "96"),
                Parameter("continuation.qualified_item_id", "(O)96")),
            CollectionCandidate(
                "bundle-direct", "community_center.donate_bundle_items",
                "donate_community_center_item", "24", "(O)24", 5, 5,
                Parameter("bundle_data_key", "Pantry/5"),
                Parameter("bundle_ingredient_index", "0"),
                Parameter("required_stack", "5"),
                Parameter("expected_item_quality", "2"),
                Parameter("expected_stack_before", "5"),
                Parameter("expected_stack_after", "0"),
                Parameter("inventory_item_total_before", "8"),
                Parameter("inventory_item_total_after", "3"),
                Parameter("expected_bundle_completed_count_before", "0"),
                Parameter("expected_bundle_completed_count_after", "2"),
                Parameter("expected_bundle_complete_after", "true")),
            CollectionCandidate(
                "bundle-route", "community_center.donate_bundle_items",
                "route_connector_tile", "24", "(O)24", 6, 5,
                Parameter("continuation.option_id", "community_center.donate_bundle_items"),
                Parameter("continuation.target_location", "CommunityCenter"),
                Parameter("continuation.bundle_data_key", "Pantry/5"),
                Parameter("continuation.bundle_ingredient_index", "0"),
                Parameter("continuation.inventory_slot_index", "6"),
                Parameter("continuation.item_id", "24"),
                Parameter("continuation.qualified_item_id", "(O)24"),
                Parameter("continuation.expected_item_quality", "2"),
                Parameter("continuation.required_stack", "5")),
            CollectionCandidate(
                "bundle-route-low-quality",
                "community_center.donate_bundle_items",
                "route_connector_tile", "24", "(O)24", 7, 5,
                Parameter("continuation.option_id", "community_center.donate_bundle_items"),
                Parameter("continuation.target_location", "CommunityCenter"),
                Parameter("continuation.bundle_data_key", "Pantry/5"),
                Parameter("continuation.bundle_ingredient_index", "0"),
                Parameter("continuation.inventory_slot_index", "6"),
                Parameter("continuation.item_id", "24"),
                Parameter("continuation.qualified_item_id", "(O)24"),
                Parameter("continuation.expected_item_quality", "1"),
                Parameter("continuation.required_stack", "5")),
            CollectionCandidate(
                "bundle-acquire-quality", "farm.maintain_crops",
                "harvest_crop_tile", "188", "(O)188", 8, 1,
                Parameter("expected_output_quality", "2")),
            CollectionCandidate(
                "bundle-acquire-unknown-quality", "farm.maintain_crops",
                "harvest_crop_tile", "188", "(O)188", 9, 1),
            CollectionCandidate(
                "bundle-acquire-low-quality", "farm.maintain_crops",
                "harvest_crop_tile", "188", "(O)188", 10, 1,
                Parameter("expected_output_quality", "1")),
            CollectionCandidate(
                "vault-unproven", "community_center.donate_bundle_items",
                "donate_community_center_item", "-1", "", 11, 2500)));

        var result = CurrentCollectionTeacherFrontierBuilder.Build(
            inventoryPath,
            loweringPath,
            rankingPath,
            snapshotPath);
        Require(result.Status == "ready" && result.TrainingLabelEligible,
            "Current collection Teacher frontier was not ready.");
        var museum = result.RequirementSets.Single(value =>
            value.RequirementSetId == "museum_collection");
        var communityCenter = result.RequirementSets.Single(value =>
            value.RequirementSetId == "community_center_standard");
        Require(museum.TransparentStateReady &&
                museum.CompletedGroupCount == 1 &&
                museum.MissingGroupCount == 1 &&
                museum.MatchedMissingGroupCount == 1,
            "Museum per-item completion frontier drifted.");
        Require(communityCenter.TransparentStateReady &&
                communityCenter.CompletedGroupCount == 0 &&
                communityCenter.MissingGroupCount == 2 &&
                communityCenter.MatchedMissingGroupCount == 1,
            "Community Center OR-bundle frontier drifted.");
        Require(result.CandidateBindings.Length == 6 &&
                result.CandidateBindings.Any(value =>
                    value.CandidateId == "museum-direct" &&
                    value.BindingKind == "native_museum_donation_completion") &&
                result.CandidateBindings.Any(value =>
                    value.CandidateId == "museum-route" &&
                    value.BindingKind ==
                        "authoritative_collection_rolling_route_step") &&
                result.CandidateBindings.Any(value =>
                    value.CandidateId == "museum-acquire" &&
                    value.BindingKind == "authoritative_acquisition_endpoint") &&
                result.CandidateBindings.Any(value =>
                    value.CandidateId == "bundle-direct" &&
                    value.RequiredQuantity == 5 &&
                    value.MinimumQuality == 2 &&
                    value.RemainingSlotCount == 1) &&
                result.CandidateBindings.Any(value =>
                    value.CandidateId == "bundle-route" &&
                    value.BindingKind ==
                        "authoritative_collection_rolling_route_step" &&
                    value.CandidateQuality == 2) &&
                result.CandidateBindings.Any(value =>
                    value.CandidateId == "bundle-acquire-quality" &&
                    value.CandidateQuality == 2),
            "Exact museum or quantity-quality Bundle bindings drifted.");
        Require(!result.CandidateBindings.Any(value => value.CandidateId is
                    "museum-route-wrong-slot" or
                    "bundle-route-low-quality" or
                    "bundle-acquire-unknown-quality" or
                    "bundle-acquire-low-quality" or
                    "vault-unproven") &&
                !result.EmitsNegativeLabelsForUnavailableRoutes,
            "An unproven quality/payment candidate leaked into collection labels.");

        var incompleteLoweringPath = Path.Combine(
            fixtureRoot,
            "lowering-missing-dependency-axis.json");
        var incompleteLowering = JsonNode.Parse(File.ReadAllText(loweringPath))!
            .AsObject();
        var routeAxes = incompleteLowering["requirement_sets"]![0]!["groups"]![0]!
            ["alternatives"]![0]!["routes"]![0]!
            ["required_downstream_dependency_axes"]!.AsArray();
        routeAxes.RemoveAt(routeAxes.Count - 1);
        File.WriteAllText(
            incompleteLoweringPath,
            incompleteLowering.ToJsonString(JsonDefaults.Options));
        var incompleteAxesRejected = false;
        try
        {
            _ = CurrentCollectionTeacherFrontierBuilder.Build(
                inventoryPath,
                incompleteLoweringPath,
                rankingPath,
                snapshotPath);
        }
        catch (InvalidDataException)
        {
            incompleteAxesRejected = true;
        }

        Require(incompleteAxesRejected,
            "A collection route with a missing dependency axis did not fail closed.");

        WriteCollectionSnapshotFixture(
            snapshotPath,
            stateHash,
            includeMuseumRows: false);
        var legacyMuseum = CurrentCollectionTeacherFrontierBuilder.Build(
            inventoryPath,
            loweringPath,
            rankingPath,
            snapshotPath);
        var blockedMuseum = legacyMuseum.RequirementSets.Single(value =>
            value.RequirementSetId == "museum_collection");
        Require(legacyMuseum.Status == "ready_with_blocked_requirement_sets" &&
                blockedMuseum.Status == "blocked" &&
                !blockedMuseum.TrainingLabelEligible &&
                blockedMuseum.Requirements.All(value => value.Completed is null) &&
                legacyMuseum.CandidateBindings.All(value =>
                    value.RequirementSetId != "museum_collection"),
            "A legacy aggregate-only museum snapshot did not fail closed per set.");
    }

    private static object CollectionRequirementGroup(
        string requirementId,
        string selectionRule,
        int requiredAlternativeCount,
        params object[] alternatives) => new
    {
        requirement_id = requirementId,
        selection_rule = selectionRule,
        required_alternative_count = requiredAlternativeCount,
        route_covered = true,
        alternatives
    };

    private static object CollectionAlternative(
        string itemId,
        string qualifiedItemId,
        string displayName,
        string matchKind,
        int amount,
        int minimumQuality,
        string routeKind,
        string sourceId,
        bool includeFixtureShopRoute = false) => new
    {
        item_id = itemId,
        qualified_item_id = qualifiedItemId,
        display_name = displayName,
        match_kind = matchKind,
        amount,
        minimum_quality = minimumQuality,
        acquisition_routes = new[] { CollectionAcquisitionRoute(routeKind, sourceId) }
            .Concat(includeFixtureShopRoute
                ? new[] { CollectionAcquisitionRoute("sells", "shop:FixtureShop") }
                : Array.Empty<object>())
            .ToArray()
    };

    private static object CollectionAcquisitionRoute(
        string routeKind,
        string sourceId) => new
    {
        kind = routeKind,
        source_id = sourceId,
        source_asset = RouteSourceAsset(routeKind),
        source_path = RouteSourcePath(routeKind, sourceId)
    };

    private static object CollectionLoweredGroup(
        string requirementId,
        int requiredAlternativeCount,
        params object[] alternatives) => new
    {
        requirement_id = requirementId,
        required_alternative_count = requiredAlternativeCount,
        runtime_admitted_alternative_count = alternatives.Length,
        teacher_admitted_alternative_count = alternatives.Length,
        runtime_admission_ready = true,
        teacher_admission_ready = true,
        blocked_route_kinds = Array.Empty<string>(),
        alternatives
    };

    private static object CollectionLoweredAlternative(
        string itemId,
        string qualifiedItemId,
        string displayName,
        string matchKind,
        int amount,
        int minimumQuality,
        string routeKind,
        string sourceId,
        string endpointOptionId,
        bool includeFixtureShopRoute = false) => new
    {
        item_id = itemId,
        qualified_item_id = qualifiedItemId,
        display_name = displayName,
        match_kind = matchKind,
        amount,
        minimum_quality = minimumQuality,
        runtime_admission_ready = true,
        teacher_admission_ready = true,
        routes = new[] { CollectionLoweredRoute(routeKind, sourceId, endpointOptionId) }
            .Concat(includeFixtureShopRoute
                ? new[]
                {
                    CollectionLoweredRoute(
                        "sells", "shop:FixtureShop", "economy.buy_supplies")
                }
                : Array.Empty<object>())
            .ToArray()
    };

    private static object CollectionLoweredRoute(
        string routeKind,
        string sourceId,
        string endpointOptionId) => new
    {
        route_kind = routeKind,
        source_id = sourceId,
        source_asset = RouteSourceAsset(routeKind),
        source_path = RouteSourcePath(routeKind, sourceId),
        supervision_mode = "deterministic_dependency",
        uncertainty_mode = routeKind == "harvests_as"
            ? "source_resolved_downstream"
            : "deterministic_fresh_receipt",
        required_downstream_dependency_axes =
            StageOneCollectionRouteDependencyAxes.Required,
        endpoint_option_ids = new[] { endpointOptionId },
        supporting_option_ids = Array.Empty<string>(),
        runtime_admission_ready = true,
        teacher_admission_ready = true
    };

    private static string RouteSourceAsset(string routeKind) => routeKind switch
    {
        "harvests_as" => "Data/Crops",
        "sells" => "Data/Shops",
        _ => "fixture"
    };

    private static string RouteSourcePath(string routeKind, string sourceId) =>
        routeKind switch
        {
            "harvests_as" when sourceId.StartsWith("crop:", StringComparison.Ordinal) =>
                "payload." + sourceId["crop:".Length..] + ".HarvestItemId",
            "sells" when sourceId == "shop:FixtureShop" =>
                "payload.FixtureShop.Items[0]",
            _ => "fixture.path"
        };

    private static object NativeConditionClause(string canonicalKey) => new
    {
        error = (string?)null,
        handler = new
        {
            canonicalKey
        }
    };

    private static AvailabilityAwarePolicyPredictionEnvelope CollectionRanking(
        string stateHash,
        params PolicyEventCandidatePrediction[] candidates) => new()
    {
        Availability = new OptionAvailabilityEnvelope
        {
            StateHash = stateHash
        },
        RankedEventCandidates = candidates
    };

    private static PolicyEventCandidatePrediction CollectionCandidate(
        string candidateId,
        string optionId,
        string kind,
        string itemId,
        string qualifiedItemId,
        int rank,
        int quantity,
        params SmallModelActionParameter[] parameters) => new()
    {
        CandidateId = candidateId,
        OptionId = optionId,
        Kind = kind,
        ItemId = itemId,
        QualifiedItemId = qualifiedItemId,
        Rank = rank,
        Quantity = quantity,
        Available = true,
        AllowedToday = true,
        TimelineStatus = "ready_now",
        Parameters = parameters
    };

    private static SmallModelActionParameter Parameter(string name, string value) =>
        new()
        {
            Name = name,
            Value = value
        };

    private static void WriteCollectionSnapshotFixture(
        string path,
        string stateHash,
        bool includeMuseumRows)
    {
        var museum = includeMuseumRows
            ? new
            {
                donated_count = 1,
                total_donatable_items = 2,
                missing_item_count = 1,
                donatable_items = new[]
                {
                    new
                    {
                        item_id = "390",
                        qualified_item_id = "(O)390",
                        display_name = "Stone",
                        object_type = "Minerals",
                        donated = true
                    },
                    new
                    {
                        item_id = "96",
                        qualified_item_id = "(O)96",
                        display_name = "Dwarf Scroll I",
                        object_type = "Arch",
                        donated = false
                    }
                },
                missing_item_ids = new[] { "96" },
                collection_complete = false
            }
            : null;
        var legacyMuseum = new
        {
            donated_count = 1,
            total_donatable_items = 2,
            collection_complete = false
        };
        Write(path, new
        {
            state_hash = stateHash,
            state = new
            {
                world_progress = new
                {
                    museum = new
                    {
                        status = "available",
                        value = includeMuseumRows ? (object)museum! : legacyMuseum
                    },
                    community_center = new
                    {
                        status = "available",
                        value = new
                        {
                            route_state = "undecided",
                            bundle_data_row_count = 2,
                            projected_bundle_row_count = 2,
                            unavailable_bundle_row_count = 0,
                            complete_bundle_count = 0,
                            bundle_rows = new object[]
                            {
                                CollectionBundleRow(
                                    "Pantry/5", 5, 1,
                                    CollectionIngredient(0, "24", 5, 2, false),
                                    CollectionIngredient(1, "188", 5, 2, false)),
                                CollectionBundleRow(
                                    "Vault/23", 23, 1,
                                    CollectionIngredient(0, "-1", 2500, 2500, false))
                            }
                        }
                    }
                }
            }
        });
    }

    private static object CollectionBundleRow(
        string bundleDataKey,
        int bundleId,
        int requiredSlots,
        params object[] ingredients) => new
    {
        projection_status = "exact",
        bundle_data_key = bundleDataKey,
        bundle_id = bundleId,
        required_slot_count = requiredSlots,
        completed_ingredient_count = 0,
        complete = false,
        ingredients
    };

    private static object CollectionIngredient(
        int index,
        string itemId,
        int stack,
        int quality,
        bool completed) => new
    {
        ingredient_index = index,
        item_id_or_category = itemId,
        required_stack = stack,
        minimum_quality = quality,
        completed
    };
}
