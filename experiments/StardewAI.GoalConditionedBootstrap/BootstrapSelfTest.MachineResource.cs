using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMachineResourceResolution()
    {
        var baseRoute = MachineFacilityStaticRoute();
        var itemSource = baseRoute.MachineSource! with
        {
            MinimumStack = 1,
            Triggers = new[]
            {
                new AcquisitionMachineTriggerEvidence(
                    "wheat",
                    1,
                    "262",
                    Array.Empty<string>(),
                    1,
                    string.Empty)
            },
            AdditionalConsumedItems = new[]
            {
                new AcquisitionMachineConsumedItemEvidence("382", 1)
            }
        };
        var itemRoute = baseRoute with
        {
            RequiredAmount = 2,
            MachineSource = itemSource
        };
        var matched = AcquisitionRouteTargetDateResourceBuilder.Evaluate(
            MachineResourceFacilityRoute(itemRoute),
            itemRoute,
            MachineResourceState(
                MachineResourceSlot(0, "(O)262", 2),
                MachineResourceSlot(1, "(O)382", 2)));
        Require(matched.ResourceInputsMatchTargetDate == true &&
                matched.ResourceRequirementKind ==
                    "native_machine_consumed_items" &&
                matched.InputEvaluations.Length == 2 &&
                matched.InputEvaluations[0].InputKind ==
                    "machine_primary_input" &&
                matched.InputEvaluations[0].RequiredQuantity == 2 &&
                matched.InputEvaluations[1].InputKind ==
                    "machine_additional_input" &&
                matched.InputEvaluations[1].RequiredQuantity == 2 &&
                matched.InputEvaluations.All(row =>
                    row.MachineBinding?.RequiredAttemptCount == 2),
            "Machine primary and additional input demand drifted.");

        var partialCredit = AcquisitionRouteTargetDateResourceBuilder.Evaluate(
            MachineResourceFacilityRoute(
                itemRoute,
                existingOutputQuantity: 1),
            itemRoute,
            MachineResourceState(
                MachineResourceSlot(0, "(O)262", 1),
                MachineResourceSlot(1, "(O)382", 1)));
        Require(partialCredit.ResourceInputsMatchTargetDate == true &&
                partialCredit.InputEvaluations.All(value =>
                    value.RequiredQuantity == 1 &&
                    value.MachineBinding is
                    {
                        RequiredAttemptCount: 1,
                        CreditedExistingOutputQuantity: 1
                    }),
            "Existing machine output did not reduce residual input demand.");

        var fullCredit = AcquisitionRouteTargetDateResourceBuilder.Evaluate(
            MachineResourceFacilityRoute(
                itemRoute,
                existingOutputQuantity: 2),
            itemRoute,
            MachineResourceState());
        Require(fullCredit.ResourceInputsMatchTargetDate == true &&
                fullCredit.InputEvaluations.Single() is
                {
                    InputKind: "machine_existing_output_credit",
                    RequiredQuantity: 0,
                    AvailableQuantity: 2,
                    MachineBinding:
                    {
                        RequiredAttemptCount: 0,
                        CreditedExistingOutputQuantity: 2
                    }
                },
            "Existing machine output did not satisfy zero-input demand.");

        var insufficient = AcquisitionRouteTargetDateResourceBuilder
            .Evaluate(
                MachineResourceFacilityRoute(itemRoute),
                itemRoute,
                MachineResourceState(
                    MachineResourceSlot(0, "(O)262", 2),
                    MachineResourceSlot(1, "(O)382", 1)));
        Require(insufficient.ResourceInputsMatchTargetDate == false &&
                insufficient.NonMatchingReasons.Contains(
                    "required_machine_resource_quantity_unavailable:(O)382",
                    StringComparer.Ordinal),
            "Insufficient machine additional input was not rejected.");

        var chestOnly = AcquisitionRouteTargetDateResourceBuilder.Evaluate(
            MachineResourceFacilityRoute(itemRoute),
            itemRoute,
            MachineResourceStateWithChestOnly(
                MachineResourceSlot(0, "(O)262", 2),
                MachineResourceSlot(1, "(O)382", 2)));
        Require(chestOnly.ResourceInputsMatchTargetDate == true &&
                chestOnly.InputEvaluations.Length == 2 &&
                chestOnly.InputEvaluations.All(input =>
                    input.MachineBinding?.EligibleSlots is
                    [{ RequiresPlayerStaging: true }]),
            "Stageable current-location chest stock was not preserved as an explicit machine-input prerequisite.");

        var nonPlayerChest = AcquisitionRouteTargetDateResourceBuilder.Evaluate(
            MachineResourceFacilityRoute(itemRoute),
            itemRoute,
            MachineResourceStateWithChestOnly(
                isPlayerChest: false,
                MachineResourceSlot(0, "(O)262", 2),
                MachineResourceSlot(1, "(O)382", 2)));
        Require(nonPlayerChest.ResourceInputsMatchTargetDate == false,
            "A non-player chest was admitted as executable staging stock.");

        var tagSource = itemSource with
        {
            Triggers = new[]
            {
                new AcquisitionMachineTriggerEvidence(
                    "greens",
                    1,
                    string.Empty,
                    new[] { "category_greens" },
                    1,
                    "!ITEM_CONTEXT_TAG Input edible_mushroom, " +
                    "ITEM_EDIBILITY Input 1")
            },
            AdditionalConsumedItems =
                Array.Empty<AcquisitionMachineConsumedItemEvidence>()
        };
        var tagRoute = baseRoute with { MachineSource = tagSource };
        var tagMatched = AcquisitionRouteTargetDateResourceBuilder
            .Evaluate(
                MachineResourceFacilityRoute(tagRoute),
                tagRoute,
                MachineResourceState(
                    MachineResourceSlot(
                        0,
                        "(O)404",
                        1,
                        new[] { "category_greens", "edible_mushroom" },
                        10),
                    MachineResourceSlot(
                        1,
                        "(O)20",
                        1,
                        new[] { "category_greens" },
                        40)));
        Require(tagMatched.ResourceInputsMatchTargetDate == true &&
                tagMatched.InputEvaluations.Single().QualifiedItemId ==
                    "(O)20" &&
                tagMatched.InputEvaluations.Single().MachineBinding is
                    { MinimumEdibility: 1 } binding &&
                binding.RequiredContextTags.SequenceEqual(
                    new[] { "category_greens" }),
            "Machine tag and edibility input selection drifted.");

        var metadataBlocked = AcquisitionRouteTargetDateResourceBuilder
            .Evaluate(
                MachineResourceFacilityRoute(tagRoute),
                tagRoute,
                MachineResourceState(
                    MachineResourceSlot(
                        0,
                        "(O)20",
                        1,
                        Array.Empty<string>(),
                        40,
                        exactTags: false)));
        Require(!metadataBlocked.ResourceInputAxisResolved &&
                metadataBlocked.BlockingReasons.Contains(
                    "machine_input_item_metadata_incomplete:greens",
                    StringComparer.Ordinal),
            "Missing machine input context tags did not fail closed.");

        var automaticSource = itemSource with
        {
            Triggers = new[]
            {
                new AcquisitionMachineTriggerEvidence(
                    "day_update",
                    8,
                    string.Empty,
                    Array.Empty<string>(),
                    1,
                    string.Empty)
            }
        };
        var automaticRoute = baseRoute with
        {
            MachineSource = automaticSource
        };
        var automatic = AcquisitionRouteTargetDateResourceBuilder
            .Evaluate(
                MachineResourceFacilityRoute(automaticRoute),
                automaticRoute,
                MachineResourceState());
        Require(automatic.ResourceInputAxisResolved &&
                automatic.ResourceInputsMatchTargetDate == true &&
                automatic.ResourceInputAxisStatus ==
                    "resolved_resource_inputs_not_required",
            "Automatic machine trigger incorrectly consumed load inputs.");

        const string absentCondition =
            "!PLAYER_HAS_ITEM Current (O)308";
        var conditionMatch = AcquisitionRouteTargetDateResourceBuilder
            .Evaluate(
                MachineResourceFacilityRoute(
                    automaticRoute,
                    new[] { absentCondition }),
                automaticRoute,
                MachineResourceState());
        Require(conditionMatch.ResourceInputsMatchTargetDate == true &&
                conditionMatch.InputEvaluations.Single()
                    .ResourceConditionBinding is
                    { Negated: true, NativeResult: false },
            "Non-consuming resource condition match drifted.");
        var conditionMiss = AcquisitionRouteTargetDateResourceBuilder
            .Evaluate(
                MachineResourceFacilityRoute(
                    automaticRoute,
                    new[] { absentCondition }),
                automaticRoute,
                MachineResourceState(
                    MachineResourceSlot(0, "(O)308", 1)));
        Require(conditionMiss.ResourceInputsMatchTargetDate == false &&
                conditionMiss.InputEvaluations.Single()
                    .ResourceConditionBinding is
                    { Negated: true, NativeResult: true },
            "Non-consuming resource condition miss drifted.");
    }

    private static AcquisitionResourceInputSnapshotState MachineResourceState(
        params MaterialInventorySlot[] slots)
    {
        var graph = new MaterialInventoryGraph
        {
            PlayerId = 42,
            InventoryNodes = new[]
            {
                new MaterialInventoryNode
                {
                    NodeId = "player:42",
                    InventoryKind = "player_inventory",
                    SupplyState = "available",
                    OwnershipClass = "actor_owned",
                    ActorUseAuthorized = true,
                    OwnerPlayerId = 42,
                    LocationId = "Farm",
                    Capacity = 36,
                    Slots = slots
                }
            },
            PhysicalInventoryCount = 1,
            AccessPointCount = 0,
            DeduplicatedAccessPointCount = 0
        };
        return MachineResourceState(graph);
    }

    private static AcquisitionResourceInputSnapshotState
        MachineResourceStateWithChestOnly(
            params MaterialInventorySlot[] chestSlots)
        => MachineResourceStateWithChestOnly(true, chestSlots);

    private static AcquisitionResourceInputSnapshotState
        MachineResourceStateWithChestOnly(
            bool isPlayerChest,
            params MaterialInventorySlot[] chestSlots)
    {
        var graph = new MaterialInventoryGraph
        {
            PlayerId = 42,
            InventoryNodes = new[]
            {
                new MaterialInventoryNode
                {
                    NodeId = "player:42",
                    InventoryKind = "player_inventory",
                    SupplyState = "available",
                    OwnershipClass = "actor_owned",
                    ActorUseAuthorized = true,
                    OwnerPlayerId = 42,
                    LocationId = "Farm",
                    Capacity = 36,
                    Slots = Array.Empty<MaterialInventorySlot>()
                },
                new MaterialInventoryNode
                {
                    NodeId = "chest:Farm:4,5",
                    InventoryKind = "chest",
                    SupplyState = "available",
                    OwnershipClass = "actor_owned",
                    ActorUseAuthorized = true,
                    LocationId = "Farm",
                    TileX = 4,
                    TileY = 5,
                    Capacity = 36,
                    Slots = chestSlots
                }
            },
            PhysicalInventoryCount = 2,
            AccessPoints = new[]
            {
                new MaterialInventoryAccessPoint
                {
                    AccessPointId = "access:chest:Farm:4,5",
                    NodeId = "chest:Farm:4,5",
                    AccessKind = "placed_chest",
                    LocationId = "Farm",
                    LocationIsCurrent = true,
                    TileX = 4,
                    TileY = 5,
                    SpecialChestType = "None",
                    OwnerPlayerId = 42,
                    IsPlayerChest = isPlayerChest
                }
            },
            AccessPointCount = 1,
            DeduplicatedAccessPointCount = 1
        };
        return MachineResourceState(graph);
    }

    private static AcquisitionResourceInputSnapshotState MachineResourceState(
        MaterialInventoryGraph graph)
    {
        var json = JsonSerializer.Serialize(new
        {
            state = new
            {
                farm = new
                {
                    material_inventory_graph = new
                    {
                        status = "available",
                        confidence = 1d,
                        value = graph
                    }
                }
            }
        }, JsonDefaults.Options);
        using var document = JsonDocument.Parse(json);
        var state = AcquisitionResourceInputSnapshotState.Read(
            document.RootElement);
        _ = state.MaterialEvidenceAvailable;
        return state;
    }

    private static MaterialInventorySlot MachineResourceSlot(
        int index,
        string qualifiedItemId,
        int stack,
        string[]? tags = null,
        int edibility = -300,
        bool exactTags = true) => new()
        {
            SlotIndex = index,
            ItemId = qualifiedItemId,
            QualifiedItemId = qualifiedItemId,
            RuntimeType = "StardewValley.Object",
            Stack = stack,
            MaximumStackSize = 999,
            ContextTags = tags ?? Array.Empty<string>(),
            ContextTagsProjectionStatus = exactTags
                ? "exact_item_get_context_tags"
                : "unavailable",
            Edibility = edibility,
            EdibilityProjectionStatus = "exact_object_edibility"
        };

    private static AcquisitionRouteTargetDateFacility
        MachineResourceFacilityRoute(
            AcquisitionRouteCalendarResolution route,
            string[]? pendingResourceConditions = null,
            int existingOutputQuantity = 0)
    {
        var unlock = new AcquisitionRouteTargetDateUnlock(
            route.RouteOccurrenceId,
            route.RequirementSetId,
            route.RequirementId,
            route.AlternativeIndex,
            route.RouteIndex,
            route.QualifiedItemId,
            route.MatchKind,
            route.RequiredAmount,
            route.MinimumQuality,
            route.RouteKind,
            route.UncertaintyMode,
            route.SourceId,
            route.Status,
            "resolved_target_date_inside_static_window_downstream_pending",
            true,
            route.CalendarWindows,
            "resolved_unlock_state_match_downstream_pending",
            true,
            true,
            Array.Empty<AcquisitionUnlockConditionEvaluation>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            pendingResourceConditions ?? Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
        var festival = new AcquisitionRouteTargetDateFestival(
            route.RouteOccurrenceId,
            unlock,
            "resolved_calendar_conditions_match_downstream_pending",
            true,
            true,
            Array.Empty<AcquisitionCalendarConditionEvaluation>(),
            Array.Empty<string>());
        var location = new AcquisitionRouteTargetDateLocation(
            route.RouteOccurrenceId,
            festival,
            "resolved_location_route_match_downstream_pending",
            true,
            true,
            Array.Empty<AcquisitionLocationRouteTargetEvaluation>(),
            Array.Empty<string>(),
            Array.Empty<string>());
        var targets = existingOutputQuantity > 0
            ? new[]
            {
                new AcquisitionFacilityTargetEvaluation(
                    "Farm",
                    null,
                    "resolved_existing_machine_capacity_match",
                    null,
                    null,
                    null,
                    null,
                    new[] { "state.farm.machines.value[]" },
                    Array.Empty<string>(),
                    route.MachineSource!.MachineQualifiedItemId,
                    12,
                    34,
                    true,
                    "processing",
                    true,
                    route.QualifiedItemId,
                    existingOutputQuantity,
                    route.MinimumQuality,
                    true)
            }
            : Array.Empty<AcquisitionFacilityTargetEvaluation>();
        return new AcquisitionRouteTargetDateFacility(
            route.RouteOccurrenceId,
            location,
            "resolved_facility_capacity_match_downstream_pending",
            true,
            true,
            "existing_machine",
            targets,
            Array.Empty<string>(),
            Array.Empty<string>());
    }
}
