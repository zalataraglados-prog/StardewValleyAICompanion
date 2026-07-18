using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal sealed record MachineInfrastructureProjection
{
    public string FleetStatus { get; init; } = "unavailable";
    public string InputProbeStatus { get; init; } = "unavailable";
    public string RouteCostStatus { get; init; } = "unavailable";
    public bool RowSnapshotComplete { get; init; }
    public int TotalCount { get; init; }
    public int LocationCount { get; init; }
    public string LocationCountsJson { get; init; } = "{}";
    public int CurrentLocationCount { get; init; }
    public int RemoteCount { get; init; }
    public int ProcessingCount { get; init; }
    public int ReadyOutputCount { get; init; }
    public int IdleManualInputCount { get; init; }
    public int IdleNonManualCount { get; init; }
    public int ProcessingDueTodayCount { get; init; }
    public int ProcessingMinutesRemainingTotal { get; init; }
    public int ActionableServiceCount { get; init; }
    public int RemoteActionableServiceCount { get; init; }
    public int ProbeEligibleCount { get; init; }
    public int ProbeObservedMachineCount { get; init; }
    public int LoadableMachineCount { get; init; }
    public int LoadableAlternativeCount { get; init; }
    public int DistinctInventorySlotCount { get; init; }
    public int DeterministicOutputAlternativeCount { get; init; }
    public int MinimumObservedProcessMinutes { get; init; }
    public int MaximumObservedProcessMinutes { get; init; }
    public int ReachableMachineLocationCount { get; init; }
    public int UnreachableMachineLocationCount { get; init; }
    public int RouteHopLowerBoundTotal { get; init; }
    public string MachineCraftingProjectionStatus { get; init; } = "unavailable";
    public string MachineCraftableCountStatus { get; init; } = "unavailable";
    public int KnownMachineRecipeCount { get; init; }
    public int ReadyMachineRecipeCount { get; init; }
    public int CraftableMachineOutputCount { get; init; }
    public int UnclassifiedKnownRecipeCount { get; init; }
    public bool CaskRecipeKnown { get; init; }
    public int CaskCraftableCount { get; init; }
    public string CaskCraftCandidateStatus { get; init; } = "unavailable";
    public string CaskOutputQualifiedItemId { get; init; } = string.Empty;
    public string CaskPlacementLocationRule { get; init; } = string.Empty;
}

internal static partial class MachineInfrastructureProjectionEvaluator
{
    public static MachineInfrastructureProjection NotApplicable() => new()
    {
        FleetStatus = "not_applicable_non_expansion_upgrade",
        InputProbeStatus = "not_applicable_non_expansion_upgrade",
        RouteCostStatus = "not_applicable_non_expansion_upgrade"
    };

    public static KeyValuePair<string, string>[] ParameterValues(MachineInfrastructureProjection projection) =>
        new[]
        {
            Pair("machine_infrastructure_projection_schema", "machine_infrastructure.v1"),
            Pair("machine_fleet_projection_status", projection.FleetStatus),
            Pair("machine_fleet_row_snapshot_complete", Lower(projection.RowSnapshotComplete)),
            Pair("machine_fleet_total_count", projection.TotalCount),
            Pair("machine_fleet_location_count", projection.LocationCount),
            Pair("machine_fleet_location_counts_json", projection.LocationCountsJson),
            Pair("machine_fleet_current_location_count", projection.CurrentLocationCount),
            Pair("machine_fleet_remote_count", projection.RemoteCount),
            Pair("machine_fleet_processing_count", projection.ProcessingCount),
            Pair("machine_fleet_ready_output_count", projection.ReadyOutputCount),
            Pair("machine_fleet_idle_manual_input_count", projection.IdleManualInputCount),
            Pair("machine_fleet_idle_nonmanual_count", projection.IdleNonManualCount),
            Pair("machine_fleet_processing_due_today_count", projection.ProcessingDueTodayCount),
            Pair("machine_fleet_processing_minutes_remaining_total", projection.ProcessingMinutesRemainingTotal),
            Pair("machine_throughput_evidence_status", "current_work_in_progress_and_immediate_deterministic_duration_only"),
            Pair("machine_fleet_actionable_service_count", projection.ActionableServiceCount),
            Pair("machine_fleet_remote_actionable_service_count", projection.RemoteActionableServiceCount),
            Pair("machine_input_probe_status", projection.InputProbeStatus),
            Pair("machine_input_probe_eligible_count", projection.ProbeEligibleCount),
            Pair("machine_input_probe_observed_machine_count", projection.ProbeObservedMachineCount),
            Pair("machine_input_probe_loadable_machine_count", projection.LoadableMachineCount),
            Pair("machine_input_probe_loadable_alternative_count", projection.LoadableAlternativeCount),
            Pair("machine_input_probe_distinct_inventory_slot_count", projection.DistinctInventorySlotCount),
            Pair("machine_input_probe_deterministic_output_alternative_count", projection.DeterministicOutputAlternativeCount),
            Pair("machine_input_probe_min_process_minutes", projection.MinimumObservedProcessMinutes),
            Pair("machine_input_probe_max_process_minutes", projection.MaximumObservedProcessMinutes),
            Pair("machine_service_route_cost_status", projection.RouteCostStatus),
            Pair("machine_service_reachable_location_count", projection.ReachableMachineLocationCount),
            Pair("machine_service_unreachable_location_count", projection.UnreachableMachineLocationCount),
            Pair("machine_service_route_hop_lower_bound_total", projection.RouteHopLowerBoundTotal),
            Pair("machine_service_route_cost_semantics", "resolved_graph_hops_not_walking_ticks_or_round_trip_time"),
            Pair("machine_crafting_projection_status", projection.MachineCraftingProjectionStatus),
            Pair("machine_crafting_count_status", projection.MachineCraftableCountStatus),
            Pair("machine_crafting_known_machine_recipe_count", projection.KnownMachineRecipeCount),
            Pair("machine_crafting_ready_machine_recipe_count", projection.ReadyMachineRecipeCount),
            Pair("machine_crafting_output_count_from_current_inventory", projection.CraftableMachineOutputCount),
            Pair("machine_crafting_unclassified_known_recipe_count", projection.UnclassifiedKnownRecipeCount),
            Pair("machine_crafting_cask_recipe_known", Lower(projection.CaskRecipeKnown)),
            Pair("machine_crafting_cask_count_from_current_inventory", projection.CaskCraftableCount),
            Pair("machine_crafting_cask_candidate_status", projection.CaskCraftCandidateStatus),
            Pair("machine_crafting_cask_output_qualified_item_id", projection.CaskOutputQualifiedItemId),
            Pair("machine_crafting_cask_placement_location_rule", projection.CaskPlacementLocationRule),
            Pair("machine_infrastructure_demand_semantics", "live_backlog_and_live_crop_wave_latest_build_window_committed_future_planting_queue_pending")
        };

    public static MachineInfrastructureProjection Evaluate(SnapshotEnvelope snapshot)
    {
        var machines = ReadStateFieldValue(snapshot, "farm", "machines");
        if (!machines.HasValue || machines.Value.ValueKind != JsonValueKind.Array ||
            !ReadableStatus(ReadStateFieldStatus(snapshot, "farm", "machines")))
        {
            return new MachineInfrastructureProjection();
        }

        var rows = machines.Value.EnumerateArray().ToArray();
        if (rows.Any(row => row.ValueKind != JsonValueKind.Object))
        {
            return new MachineInfrastructureProjection { FleetStatus = "invalid_machine_row" };
        }

        var advertisedCounts = rows
            .Select(row => ReadInt(row, "machine_row_count_total", -1))
            .Distinct()
            .ToArray();
        var advertisedProbeEligibleCounts = rows
            .Select(row => ReadInt(row, "machine_input_probe_eligible_count", -1))
            .Distinct()
            .ToArray();
        var complete = rows.Length == 0 ||
            (advertisedCounts.Length == 1 && advertisedCounts[0] == rows.Length &&
             advertisedProbeEligibleCounts.Length == 1 && advertisedProbeEligibleCounts[0] >= 0 && rows.All(row =>
                string.Equals(ReadString(row, "machine_row_snapshot_status"), "complete_no_row_truncation", StringComparison.Ordinal)));
        if (!complete)
        {
            return new MachineInfrastructureProjection
            {
                FleetStatus = "invalid_or_truncated_machine_rows",
                TotalCount = rows.Length
            };
        }

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        var currentTime = ReadStateFieldInt(snapshot, "time", "time", 600);
        var minutesRemainingToday = MinutesRemainingUntilTwoAm(currentTime);
        var locationCounts = rows
            .GroupBy(row => ReadString(row, "location_id", "Farm"), StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var processing = rows.Where(row => ReadInt(row, "minutes_until_ready") > 0).ToArray();
        var ready = rows.Where(row => ReadBool(row, "ready_for_harvest")).ToArray();
        var idle = rows.Where(row => ReadInt(row, "minutes_until_ready") <= 0 && !ReadBool(row, "ready_for_harvest")).ToArray();
        var idleManual = idle.Where(row => ReadBool(row, "machine_has_input")).ToArray();
        var currentIdleManual = idleManual.Where(row => SameLocation(row, currentLocation)).ToArray();
        var probedRows = idleManual.Where(row =>
            string.Equals(ReadString(row, "loadable_input_probe_status"), "available_main_thread_cache", StringComparison.Ordinal)).ToArray();
        var loadableRows = probedRows
            .Select(row => new { Row = row, Inputs = ReadArray(row, "loadable_inputs") })
            .ToArray();
        var alternatives = loadableRows.SelectMany(row => row.Inputs).Where(input => input.ValueKind == JsonValueKind.Object).ToArray();
        var deterministicOutputs = alternatives
            .Where(HasDeterministicOutput)
            .ToArray();
        var deterministicMinutes = deterministicOutputs
            .Select(ReadProcessMinutes)
            .Where(minutes => minutes > 0)
            .ToArray();
        var probeEligibleCount = advertisedProbeEligibleCounts.DefaultIfEmpty(-1).Single();
        var probeStatus = idleManual.Length == 0
            ? "not_applicable_no_idle_manual_input_machine"
            : currentIdleManual.Length == 0
                ? "blocked_remote_idle_manual_inputs_require_route_and_fresh_snapshot"
                : probedRows.Length == currentIdleManual.Length
                ? "complete_current_idle_manual_input_probe"
                : probedRows.Length > 0
                    ? "bounded_rotating_current_map_probe_partial"
                    : "bounded_probe_has_no_observed_idle_machine";
        var route = EvaluateRouteHops(snapshot, currentLocation, locationCounts.Keys);
        var crafting = ReadMachineCraftingProjection(snapshot);

        return new MachineInfrastructureProjection
        {
            FleetStatus = rows.Length == 0 ? "complete_empty_machine_fleet" : "complete_machine_rows",
            InputProbeStatus = probeStatus,
            RouteCostStatus = route.Status,
            RowSnapshotComplete = true,
            TotalCount = rows.Length,
            LocationCount = locationCounts.Count,
            LocationCountsJson = JsonSerializer.Serialize(locationCounts),
            CurrentLocationCount = rows.Count(row => SameLocation(row, currentLocation)),
            RemoteCount = rows.Count(row => !SameLocation(row, currentLocation)),
            ProcessingCount = processing.Length,
            ReadyOutputCount = ready.Length,
            IdleManualInputCount = idleManual.Length,
            IdleNonManualCount = idle.Length - idleManual.Length,
            ProcessingDueTodayCount = processing.Count(row => ReadInt(row, "minutes_until_ready") <= minutesRemainingToday),
            ProcessingMinutesRemainingTotal = processing.Sum(row => Math.Max(0, ReadInt(row, "minutes_until_ready"))),
            ActionableServiceCount = ready.Length + idleManual.Length,
            RemoteActionableServiceCount = ready.Count(row => !SameLocation(row, currentLocation)) +
                idleManual.Count(row => !SameLocation(row, currentLocation)),
            ProbeEligibleCount = Math.Max(0, probeEligibleCount),
            ProbeObservedMachineCount = probedRows.Length,
            LoadableMachineCount = loadableRows.Count(row => row.Inputs.Length > 0),
            LoadableAlternativeCount = alternatives.Length,
            DistinctInventorySlotCount = alternatives.Select(input => ReadInt(input, "slot_index", -1)).Where(index => index >= 0).Distinct().Count(),
            DeterministicOutputAlternativeCount = deterministicOutputs.Length,
            MinimumObservedProcessMinutes = deterministicMinutes.DefaultIfEmpty(0).Min(),
            MaximumObservedProcessMinutes = deterministicMinutes.DefaultIfEmpty(0).Max(),
            ReachableMachineLocationCount = route.ReachableCount,
            UnreachableMachineLocationCount = route.UnreachableCount,
            RouteHopLowerBoundTotal = route.HopTotal,
            MachineCraftingProjectionStatus = crafting.Status,
            MachineCraftableCountStatus = crafting.CountStatus,
            KnownMachineRecipeCount = crafting.KnownRecipeCount,
            ReadyMachineRecipeCount = crafting.ReadyRecipeCount,
            CraftableMachineOutputCount = crafting.CraftableOutputCount,
            UnclassifiedKnownRecipeCount = crafting.UnclassifiedKnownRecipeCount,
            CaskRecipeKnown = crafting.CaskRecipeKnown,
            CaskCraftableCount = crafting.CaskCraftableCount,
            CaskCraftCandidateStatus = crafting.CaskCandidateStatus,
            CaskOutputQualifiedItemId = crafting.CaskOutputQualifiedItemId,
            CaskPlacementLocationRule = crafting.CaskPlacementLocationRule
        };
    }

    private static JsonElement[] ReadArray(JsonElement row, string property) =>
        row.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().ToArray()
            : Array.Empty<JsonElement>();

    private static bool HasDeterministicOutput(JsonElement input) =>
        input.TryGetProperty("predicted_output", out var output) && output.ValueKind == JsonValueKind.Object &&
        string.Equals(ReadString(output, "status"), "available", StringComparison.Ordinal);

    private static int ReadProcessMinutes(JsonElement input)
    {
        if (!input.TryGetProperty("predicted_output", out var output) || output.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        return Math.Max(0, ReadInt(output, "effective_minutes_until_ready",
            ReadInt(output, "override_minutes_until_ready", ReadInt(output, "rule_minutes_until_ready"))));
    }

    private static bool SameLocation(JsonElement row, string currentLocation) =>
        string.Equals(ReadString(row, "location_id", "Farm"), currentLocation, StringComparison.OrdinalIgnoreCase);

    private static int MinutesRemainingUntilTwoAm(int time)
    {
        var hour = Math.Clamp(time / 100, 0, 26);
        var minute = Math.Clamp(time % 100, 0, 59);
        var absolute = hour * 60 + minute;
        return Math.Max(0, 26 * 60 - absolute);
    }

    private static RouteHopProjection EvaluateRouteHops(
        SnapshotEnvelope snapshot,
        string currentLocation,
        IEnumerable<string> machineLocations)
    {
        var destinations = machineLocations
            .Where(location => !string.Equals(location, currentLocation, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (destinations.Length == 0)
        {
            return new RouteHopProjection("not_applicable_single_current_location", 0, 0, 0);
        }

        var graph = ReadStateFieldValue(snapshot, "locations", "route_graph");
        if (!graph.HasValue || graph.Value.ValueKind != JsonValueKind.Object ||
            !graph.Value.TryGetProperty("edges", out var edges) || edges.ValueKind != JsonValueKind.Array)
        {
            return new RouteHopProjection("route_graph_unavailable", 0, destinations.Length, 0);
        }

        var adjacency = edges.EnumerateArray()
            .Where(edge => edge.ValueKind == JsonValueKind.Object && ReadBool(edge, "resolved"))
            .Select(edge => new { From = ReadString(edge, "from_location"), To = ReadString(edge, "target_location") })
            .Where(edge => !string.IsNullOrWhiteSpace(edge.From) && !string.IsNullOrWhiteSpace(edge.To))
            .GroupBy(edge => edge.From, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(edge => edge.To).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), StringComparer.OrdinalIgnoreCase);
        var hops = destinations.Select(destination => ShortestHopCount(adjacency, currentLocation, destination)).ToArray();
        var reachable = hops.Count(hop => hop >= 0);
        return new RouteHopProjection(
            reachable == destinations.Length ? "resolved_route_graph_hop_lower_bound" : "partially_unresolved_route_graph_hop_lower_bound",
            reachable,
            destinations.Length - reachable,
            hops.Where(hop => hop >= 0).Sum());
    }

    private static int ShortestHopCount(IReadOnlyDictionary<string, string[]> adjacency, string start, string target)
    {
        var queue = new Queue<(string Location, int Hops)>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { start };
        queue.Enqueue((start, 0));
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (string.Equals(current.Location, target, StringComparison.OrdinalIgnoreCase))
            {
                return current.Hops;
            }
            if (!adjacency.TryGetValue(current.Location, out var next))
            {
                continue;
            }
            foreach (var location in next.Where(visited.Add))
            {
                queue.Enqueue((location, current.Hops + 1));
            }
        }
        return -1;
    }

    private static KeyValuePair<string, string> Pair(string name, object value) =>
        new(name, value?.ToString() ?? string.Empty);

    private static string Lower(bool value) => value.ToString().ToLowerInvariant();

    private sealed record RouteHopProjection(string Status, int ReachableCount, int UnreachableCount, int HopTotal);

}
