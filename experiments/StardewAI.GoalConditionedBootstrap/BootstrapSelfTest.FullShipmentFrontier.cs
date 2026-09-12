using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyCurrentFullShipmentTeacherFrontier(
        string outputRoot)
    {
        var fixtureRoot = Path.Combine(
            outputRoot,
            "current-full-shipment-teacher-frontier-fixture");
        Directory.CreateDirectory(fixtureRoot);
        var inventoryPath = Path.Combine(fixtureRoot, "requirements.json");
        var loweringPath = Path.Combine(fixtureRoot, "lowering.json");
        var rankingPath = Path.Combine(fixtureRoot, "ranking.json");
        var snapshotPath = Path.Combine(fixtureRoot, "snapshot.json");
        const string stateHash = "full-shipment-frontier-state";

        Write(inventoryPath, new
        {
            schema_version = "authoritative_goal_requirement_inventory.v1",
            status = "complete",
            goal_id = "goal.grandpa_21",
            denominator_complete = true,
            acquisition_routes_complete = true,
            requirement_sets = new[]
            {
                new
                {
                    requirement_set_id = "full_shipment",
                    required_group_count = 2,
                    route_covered_group_count = 2,
                    acquisition_routes_complete = true,
                    groups = new object[]
                    {
                        RequirementGroup(
                            "full_shipment:item:24",
                            "24",
                            "(O)24",
                            "Parsnip",
                            "harvests_as",
                            "crop:472",
                            "farm.maintain_crops",
                            "economy.buy_supplies"),
                        RequirementGroup(
                            "full_shipment:item:388",
                            "388",
                            "(O)388",
                            "Wood",
                            "drops_item",
                            "tree:wood",
                            "foraging.clear_obstacles",
                            string.Empty)
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
            requirement_sets = new[]
            {
                new
                {
                    requirement_set_id = "full_shipment",
                    required_group_count = 2,
                    runtime_admitted_group_count = 2,
                    teacher_admitted_group_count = 2,
                    groups = new object[]
                    {
                        LoweredGroup(
                            "full_shipment:item:24",
                            "24",
                            "(O)24",
                            "Parsnip",
                            "harvests_as",
                            "crop:472",
                            "farm.maintain_crops",
                            "economy.buy_supplies"),
                        LoweredGroup(
                            "full_shipment:item:388",
                            "388",
                            "(O)388",
                            "Wood",
                            "drops_item",
                            "tree:wood",
                            "foraging.clear_obstacles",
                            string.Empty)
                    }
                }
            }
        });
        WriteFullShipmentSnapshotFixture(snapshotPath, stateHash, 0.5);

        var ranking = Ranking(
            stateHash,
            Candidate(
                "harvest-parsnip",
                "farm.maintain_crops",
                "harvest_crop",
                "24",
                "(O)24",
                rank: 1),
            Candidate(
                "ship-parsnip",
                "economy.ship_items",
                "ship_item",
                "24",
                "(O)24",
                rank: 2,
                fullShipmentContribution: true),
            Candidate(
                "wrong-output-same-option",
                "farm.maintain_crops",
                "harvest_crop",
                "388",
                "(O)388",
                rank: 3),
            Candidate(
                "blocked-exact-output",
                "farm.maintain_crops",
                "harvest_crop",
                "24",
                "(O)24",
                rank: 4,
                available: false),
            Candidate(
                "supporting-seed-purchase",
                "economy.buy_supplies",
                "shop_purchase",
                "472",
                "(O)472",
                rank: 5),
            Candidate(
                "completed-wood",
                "foraging.clear_obstacles",
                "clear_obstacle_tile",
                "388",
                "(O)388",
                rank: 6));
        Write(rankingPath, ranking);

        var result = CurrentFullShipmentTeacherFrontierBuilder.Build(
            inventoryPath,
            loweringPath,
            rankingPath,
            snapshotPath);
        Require(result.Status == "ready",
            "Current Full Shipment Teacher frontier was not ready.");
        Require(result.RequiredGroupCount == 2 &&
                result.CompletedGroupCount == 1 &&
                result.MissingGroupCount == 1 &&
                result.MatchedMissingGroupCount == 1,
            "Current Full Shipment progress denominator drifted.");
        Require(result.TrainingLabelEligible &&
                !result.EmitsNegativeLabelsForUnavailableRoutes,
            "Current Full Shipment frontier label policy drifted.");
        Require(result.CandidateBindings.Length == 2 &&
                result.CandidateBindings.Any(value =>
                    value.CandidateId == "harvest-parsnip" &&
                    value.BindingKind == "authoritative_acquisition_endpoint" &&
                    value.MatchedRoutes.Length == 1) &&
                result.CandidateBindings.Any(value =>
                    value.CandidateId == "ship-parsnip" &&
                    value.BindingKind == "native_full_shipment_completion"),
            "Current Full Shipment exact candidate bindings drifted.");
        Require(!result.CandidateBindings.Any(value =>
                value.CandidateId is
                    "wrong-output-same-option" or
                    "blocked-exact-output" or
                    "supporting-seed-purchase" or
                    "completed-wood"),
            "An ineligible, mismatched, supporting, or completed candidate leaked into the Full Shipment Teacher frontier.");

        Write(rankingPath, Ranking(
            stateHash,
            Candidate(
                "blocked-only",
                "farm.maintain_crops",
                "harvest_crop",
                "24",
                "(O)24",
                rank: 1,
                available: false)));
        var deferred = CurrentFullShipmentTeacherFrontierBuilder.Build(
            inventoryPath,
            loweringPath,
            rankingPath,
            snapshotPath);
        Require(deferred.Status == "no_current_matching_candidate" &&
                !deferred.TrainingLabelEligible &&
                deferred.CandidateBindings.Length == 0 &&
                deferred.Requirements.Single(value => !value.Completed)
                    .DeferReasons.Contains(
                        "future_or_currently_unavailable_routes_are_not_negative_labels",
                        StringComparer.Ordinal),
            "Unavailable Full Shipment routes were not deferred fail-closed.");

        Write(rankingPath, Ranking("different-state"));
        var staleRejected = false;
        try
        {
            _ = CurrentFullShipmentTeacherFrontierBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath);
        }
        catch (InvalidDataException)
        {
            staleRejected = true;
        }
        Require(staleRejected,
            "A stale ranking was admitted against a different snapshot state.");

        Write(rankingPath, Ranking(stateHash));
        WriteFullShipmentSnapshotFixture(snapshotPath, stateHash, 0.75);
        var inconsistentProgressRejected = false;
        try
        {
            _ = CurrentFullShipmentTeacherFrontierBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath);
        }
        catch (InvalidDataException)
        {
            inconsistentProgressRejected = true;
        }
        Require(inconsistentProgressRejected,
            "Inconsistent Full Shipment aggregate progress was admitted.");
    }

    private static object RequirementGroup(
        string requirementId,
        string itemId,
        string qualifiedItemId,
        string displayName,
        string routeKind,
        string sourceId,
        string endpointOptionId,
        string supportingOptionId) => new
    {
        requirement_id = requirementId,
        required_alternative_count = 1,
        route_covered = true,
        alternatives = new[]
        {
            new
            {
                item_id = itemId,
                qualified_item_id = qualifiedItemId,
                display_name = displayName,
                match_kind = "item_id",
                amount = 1,
                minimum_quality = 0,
                acquisition_routes = new[]
                {
                    new
                    {
                        kind = routeKind,
                        source_id = sourceId,
                        source_asset = "fixture",
                        source_path = "fixture.path"
                    }
                }
            }
        }
    };

    private static object LoweredGroup(
        string requirementId,
        string itemId,
        string qualifiedItemId,
        string displayName,
        string routeKind,
        string sourceId,
        string endpointOptionId,
        string supportingOptionId) => new
    {
        requirement_id = requirementId,
        required_alternative_count = 1,
        runtime_admitted_alternative_count = 1,
        teacher_admitted_alternative_count = 1,
        runtime_admission_ready = true,
        teacher_admission_ready = true,
        blocked_route_kinds = Array.Empty<string>(),
        alternatives = new[]
        {
            new
            {
                item_id = itemId,
                qualified_item_id = qualifiedItemId,
                display_name = displayName,
                match_kind = "item_id",
                amount = 1,
                minimum_quality = 0,
                runtime_admission_ready = true,
                teacher_admission_ready = true,
                routes = new[]
                {
                    new
                    {
                        route_kind = routeKind,
                        source_id = sourceId,
                        source_asset = "fixture",
                        source_path = "fixture.path",
                        supervision_mode = "deterministic_dependency",
                        uncertainty_mode = "deterministic_fresh_receipt",
                        required_downstream_dependency_axes =
                            StageOneCollectionRouteDependencyAxes.Required,
                        endpoint_option_ids = new[] { endpointOptionId },
                        supporting_option_ids = string.IsNullOrEmpty(supportingOptionId)
                            ? Array.Empty<string>()
                            : new[] { supportingOptionId },
                        runtime_admission_ready = true,
                        teacher_admission_ready = true
                    }
                }
            }
        }
    };

    private static object ShipmentItem(
        string itemId,
        string qualifiedItemId,
        int shippedCount,
        bool shipped) => new
    {
        item_id = itemId,
        qualified_item_id = qualifiedItemId,
        display_name = itemId,
        category = 0,
        object_type = "Basic",
        current_shipped_count = shippedCount,
        shipped
    };

    private static void WriteFullShipmentSnapshotFixture(
        string path,
        string stateHash,
        double completionRatio) => Write(path, new
    {
        state_hash = stateHash,
        state = new
        {
            world_progress = new
            {
                full_shipment_progress = new
                {
                    status = "available",
                    value = new
                    {
                        eligible_item_count = 2,
                        shipped_eligible_item_count = 1,
                        missing_item_count = 1,
                        completion_ratio = completionRatio,
                        complete = false,
                        items = new object[]
                        {
                            ShipmentItem("24", "(O)24", 0, false),
                            ShipmentItem("388", "(O)388", 7, true)
                        },
                        missing_item_ids = new[] { "24" }
                    }
                }
            }
        }
    });

    private static AvailabilityAwarePolicyPredictionEnvelope Ranking(
        string stateHash,
        params PolicyEventCandidatePrediction[] candidates) => new()
    {
        Availability = new OptionAvailabilityEnvelope
        {
            StateHash = stateHash
        },
        RankedEventCandidates = candidates
    };

    private static PolicyEventCandidatePrediction Candidate(
        string candidateId,
        string optionId,
        string kind,
        string itemId,
        string qualifiedItemId,
        int rank,
        bool available = true,
        bool fullShipmentContribution = false) => new()
    {
        CandidateId = candidateId,
        OptionId = optionId,
        Kind = kind,
        ItemId = itemId,
        QualifiedItemId = qualifiedItemId,
        Rank = rank,
        Available = available,
        AllowedToday = available,
        TimelineStatus = available ? "ready_now" : "blocked",
        BlockReasons = available
            ? Array.Empty<string>()
            : new[] { "fixture_blocked" },
        FullShipmentKnown = fullShipmentContribution ? true : null,
        FullShipmentEligible = fullShipmentContribution ? true : null,
        FullShipmentCurrentShippedCount = fullShipmentContribution ? 0 : null,
        FullShipmentAlreadyShipped = fullShipmentContribution ? false : null,
        FullShipmentContributes = fullShipmentContribution ? true : null
    };
}
