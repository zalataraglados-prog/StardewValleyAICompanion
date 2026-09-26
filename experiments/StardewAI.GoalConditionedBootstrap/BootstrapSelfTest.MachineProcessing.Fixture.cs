using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

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
                    Convert.ToBoolean(machine["ready_for_harvest"])
                        ? "ready_output"
                        : Convert.ToInt32(machine["minutes_until_ready"]) > 0
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
        bool includeActiveSource = false,
        bool readyForHarvest = false)
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
            ["ready_for_harvest"] = readyForHarvest,
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

    private static MachineRetryExpansionContext
        MachineRetryExpansionContextFixture(
            IReadOnlyList<Dictionary<string, object?>> machines)
    {
        using var currencyDocument = JsonDocument.Parse("{\"state\":{}}");
        return new MachineRetryExpansionContext(
            "full_shipment",
            new string('a', 64),
            0,
            new AcquisitionStrategyLedgerState(
                new StrategyCommitmentLedger
                {
                    LedgerId = "machine-retry-ledger",
                    SaveId = "machine-retry-save",
                    PlayerId = "42",
                    Revision = 0,
                    UpdatedAt = "2026-09-27T00:00:00Z",
                    SourceStateHash = new string('a', 64)
                },
                42),
            MachineResourceState(
                MachineResourceSlot(0, "(O)262", 100)),
            new AcquisitionShopQuoteSnapshotState(
                currencyDocument.RootElement.GetProperty("state").Clone()),
            MachineProcessingState(900, machines));
    }

    private static AcquisitionDailyTimeEnergySnapshotState
        MachineDailyTimeEnergyState(
            IReadOnlyList<Dictionary<string, object?>> machines,
            int timeOfDay = 900,
            int emptyInventorySlots = 12)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-machine-daily-fixture");
        Directory.CreateDirectory(root);
        var snapshotPath = Path.Combine(root, "snapshot.json");
        var timingPath = Path.Combine(root, "route-timing.json");
        foreach (var machine in machines)
            machine["machine_row_count_total"] = machines.Count;
        WriteStageOneCollectionSnapshot(
            snapshotPath,
            new string('a', 64),
            Array.Empty<StageOneFishFixture>(),
            playerLocation: "Farm",
            playerTileX: 1,
            playerTileY: 5,
            totalDays: 0,
            timeOfDay: timeOfDay);
        var snapshot = JsonNode.Parse(File.ReadAllText(snapshotPath))!;
        snapshot["state"]!["player"]!["inventory_capacity"] =
            JsonSerializer.SerializeToNode(new
            {
                status = "available",
                value = new
                {
                    max_items = 12,
                    occupied_item_stacks = 12 - emptyInventorySlots,
                    empty_slots = emptyInventorySlots,
                    has_empty_slot = emptyInventorySlots > 0
                }
            }, JsonDefaults.Options);
        snapshot["state"]!["farm"]!["machines"] =
            JsonSerializer.SerializeToNode(new
            {
                status = "available",
                value = machines
            }, JsonDefaults.Options);
        File.WriteAllText(
            snapshotPath,
            snapshot.ToJsonString(JsonDefaults.Options));
        Write(timingPath, StageOneRouteTimingCalibration(totalDays: 0));
        using var document = JsonDocument.Parse(
            File.ReadAllText(snapshotPath));
        return AcquisitionDailyTimeEnergySnapshotState.Read(
            document.RootElement,
            "1.6.15",
            0,
            timingPath);
    }

    private static AuthoritativeCalendarSourceWindow
        MachineDailyWindow() => new()
        {
            SourceKind = "machine",
            SourceKey = "machine:(BC)12:rule:keg_wheat",
            FirstTotalDay = 0,
            LastTotalDay = 0,
            TimeWindows = new[]
            {
                new MasterAnglerTimeWindow
                {
                    StartTime = 600,
                    EndTime = 2600
                }
            },
            WeatherModes = new[] { "all" }
        };
}
