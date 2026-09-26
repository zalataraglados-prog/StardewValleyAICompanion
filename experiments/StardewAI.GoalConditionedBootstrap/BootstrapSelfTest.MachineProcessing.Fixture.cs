using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static AcquisitionRouteTargetDateReservation
        MachineProcessingReservation(
            AcquisitionRouteCalendarResolution route,
            IReadOnlyList<Dictionary<string, object?>> machines)
    {
        var facility = MachineResourceFacilityRoute(route) with
        {
            TargetEvaluations = machines.Select(machine =>
                MachineProcessingFacilityTarget(
                    route,
                    Convert.ToInt32(machine["tile_x"]),
                    Convert.ToInt32(machine["tile_y"]),
                    Convert.ToInt32(machine["minutes_until_ready"]) > 0
                        ? "processing"
                        : "idle"))
                .ToArray()
        };
        var resources = AcquisitionRouteTargetDateResourceBuilder.Evaluate(
            facility,
            route,
            MachineResourceState(
                MachineResourceSlot(
                    0,
                    "(O)262",
                    Math.Max(1, route.RequiredAmount))));
        Require(resources.ResourceInputsMatchTargetDate == true,
            "Machine processing fixture resource axis did not match.");
        var currency = new AcquisitionRouteTargetDateCurrency(
            route.RouteOccurrenceId,
            resources,
            "resolved_currency_budget_not_required",
            true,
            true,
            "none",
            null,
            Array.Empty<string>(),
            Array.Empty<string>());
        return new AcquisitionRouteTargetDateReservation(
            route.RouteOccurrenceId,
            currency,
            "resolved_inventory_reservation_match",
            true,
            true,
            "fixture_claim_set",
            null,
            Array.Empty<string>(),
            Array.Empty<string>());
    }

    private static AcquisitionFacilityTargetEvaluation
        MachineProcessingFacilityTarget(
            AcquisitionRouteCalendarResolution route,
            int tileX,
            int tileY,
            string capacityState) => new(
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
                tileX,
                tileY,
                true,
                capacityState);

    private static Dictionary<string, object?> MachineProcessingRow(
        AcquisitionRouteCalendarResolution route,
        int tileX,
        int tileY,
        int minutesUntilReady,
        string? activeOutputId = null,
        bool includeActiveSource = false)
    {
        var activeSources = includeActiveSource
            ? new object[]
            {
                new
                {
                    route_kind = route.RouteKind,
                    source_id = route.SourceId,
                    qualified_item_id = activeOutputId
                }
            }
            : Array.Empty<object>();
        return new Dictionary<string, object?>
        {
            ["location_id"] = "Farm",
            ["tile_x"] = tileX,
            ["tile_y"] = tileY,
            ["qualified_item_id"] =
                route.MachineSource!.MachineQualifiedItemId,
            ["location_is_player_controlled"] = true,
            ["owner_player_id"] = 42L,
            ["ready_for_harvest"] = false,
            ["minutes_until_ready"] = minutesUntilReady,
            ["machine_has_input"] = true,
            ["machine_has_output"] = true,
            ["machine_row_count_total"] = 0,
            ["machine_row_snapshot_status"] =
                "complete_no_row_truncation",
            ["machine_input_probe_eligible_count"] = 0,
            ["held_item"] = activeOutputId is null
                ? null
                : new
                {
                    qualified_item_id = activeOutputId,
                    stack = 1,
                    quality = 0
                },
            ["active_output_authoritative_route_sources"] = activeSources
        };
    }

    private static AcquisitionProcessingLeadTimeSnapshotState
        MachineProcessingState(
            int timeOfDay,
            IReadOnlyList<Dictionary<string, object?>> machines)
    {
        foreach (var machine in machines)
            machine["machine_row_count_total"] = machines.Count;
        var json = JsonSerializer.Serialize(new
        {
            state = new
            {
                player = new
                {
                    location_id = new
                    {
                        status = "available",
                        value = "Farm"
                    }
                },
                farm = new
                {
                    crops = new
                    {
                        status = "available",
                        value = Array.Empty<object>()
                    },
                    machines = new
                    {
                        status = "available",
                        value = machines
                    }
                },
                current_location = new
                {
                    crops = new
                    {
                        status = "available",
                        value = Array.Empty<object>()
                    }
                },
                time = new
                {
                    time = new
                    {
                        status = "available",
                        value = timeOfDay
                    }
                },
                world_progress = new
                {
                    game_state_query_calendar_state = new
                    {
                        status = "available",
                        adapter = "vanilla_1_6_15_gsq_calendar",
                        value = new
                        {
                            current_total_day = 0,
                            time_of_day = timeOfDay,
                            days_played = 1u,
                            festival_location_context_resolution_status =
                                "not_projected",
                            festival_date_keys = Array.Empty<string>(),
                            active_passive_festival_ids =
                                Array.Empty<string>(),
                            passive_festivals = Array.Empty<object>()
                        }
                    }
                }
            }
        }, JsonDefaults.Options);
        using var document = JsonDocument.Parse(json);
        return AcquisitionProcessingLeadTimeSnapshotState.Read(
            document.RootElement);
    }
}
