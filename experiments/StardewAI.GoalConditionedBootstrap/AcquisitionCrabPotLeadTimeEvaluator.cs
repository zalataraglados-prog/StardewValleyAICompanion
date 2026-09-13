using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionCrabPotLeadTimeEvaluator
{
    public static AcquisitionCrabPotLeadTimeResult Evaluate(
        JsonElement network,
        IEnumerable<string> targetLocations,
        string targetItem,
        int targetTotalDay)
    {
        if (network.ValueKind != JsonValueKind.Object ||
            AcquisitionProcessingLeadTimeSnapshotState.ReadString(
                network,
                "schema_version") != "crab_pot_network.v1" ||
            AcquisitionProcessingLeadTimeSnapshotState.ReadString(
                network,
                "projection_status") !=
                "complete_crab_pots_across_loaded_persistent_locations" ||
            !network.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return Blocked("crab_pot_network_evidence_missing_or_incomplete");
        }
        var locations = targetLocations.ToHashSet(
            StringComparer.OrdinalIgnoreCase);
        var matching = rows.EnumerateArray().Where(row =>
                locations.Contains(
                    AcquisitionProcessingLeadTimeSnapshotState.ReadString(
                        row,
                        "location_id")) &&
                Contains(row, targetItem))
            .ToArray();
        if (matching.Length == 0)
            return Blocked("matched_crab_pot_row_missing");

        var evaluations = matching.Select(row =>
                EvaluateRow(row, targetItem, targetTotalDay))
            .ToArray();
        var blocking = evaluations.SelectMany(value => value.BlockingReasons)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return blocking.Length == 0
            ? new(true, evaluations, Array.Empty<string>())
            : new(false, evaluations, blocking);
    }

    private static AcquisitionProcessingLeadTimeEvaluation EvaluateRow(
        JsonElement row,
        string targetItem,
        int targetTotalDay)
    {
        var locationId = AcquisitionProcessingLeadTimeSnapshotState.ReadString(
            row,
            "location_id");
        if (string.IsNullOrWhiteSpace(locationId) ||
            !AcquisitionProcessingLeadTimeSnapshotState.TryReadBool(
                row,
                "exact_base_crab_pot",
                out var exactBase) ||
            !exactBase ||
            !AcquisitionProcessingLeadTimeSnapshotState.TryReadBool(
                row,
                "production_domain_complete",
                out var productionComplete) ||
            !productionComplete ||
            !AcquisitionProcessingLeadTimeSnapshotState.TryReadBool(
                row,
                "ready_state_consistent",
                out var readyConsistent) ||
            !readyConsistent ||
            !AcquisitionProcessingLeadTimeSnapshotState.TryReadBool(
                row,
                "ready_for_harvest",
                out var ready) ||
            !AcquisitionProcessingLeadTimeSnapshotState.TryReadBool(
                row,
                "owner_has_luremaster",
                out var luremaster))
        {
            return BlockedEvaluation(
                locationId,
                "crab_pot_runtime_row_invalid_or_incomplete");
        }
        var currentOutput =
            AcquisitionProcessingLeadTimeSnapshotState.ReadString(
                row,
                "current_output_qualified_item_id");
        if (ready && currentOutput == targetItem)
        {
            return Evaluation(
                locationId,
                "resolved_matching_crab_pot_output_ready",
                0,
                targetTotalDay,
                true);
        }
        var serviceStatus =
            AcquisitionProcessingLeadTimeSnapshotState.ReadString(
                row,
                "service_status");
        var bait = AcquisitionProcessingLeadTimeSnapshotState.ReadString(
            row,
            "bait_qualified_item_id");
        var serviced = ready
            ? currentOutput.Length > 0 && luremaster
            : currentOutput.Length == 0 &&
                serviceStatus == "producing_or_waiting" &&
                (bait.Length > 0 || luremaster);
        return serviced
            ? Evaluation(
                locationId,
                "resolved_serviced_crab_pot_next_morning",
                1,
                checked(targetTotalDay + 1),
                false)
            : BlockedEvaluation(
                locationId,
                "crab_pot_service_state_does_not_prove_next_production");
    }

    private static bool Contains(JsonElement row, string qualifiedItemId)
    {
        if (AcquisitionProcessingLeadTimeSnapshotState.ReadString(
                row,
                "current_output_qualified_item_id") == qualifiedItemId)
        {
            return true;
        }
        return row.TryGetProperty(
                "possible_qualified_item_ids",
                out var possible) &&
            possible.ValueKind == JsonValueKind.Array &&
            possible.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.String &&
                item.GetString() == qualifiedItemId);
    }

    private static AcquisitionProcessingLeadTimeEvaluation Evaluation(
        string locationId,
        string status,
        int leadDays,
        int earliestDay,
        bool ready) => new(
            locationId,
            "existing_crab_pot",
            status,
            "exact_native_day_update",
            null,
            leadDays,
            earliestDay,
            ready,
            new[]
            {
                "state.player.crab_pot_network.value.rows[]",
                "locked decompile StardewValley.Objects/CrabPot.cs DayUpdate"
            },
            Array.Empty<string>());

    private static AcquisitionProcessingLeadTimeEvaluation BlockedEvaluation(
        string locationId,
        string reason) => new(
            locationId,
            "existing_crab_pot",
            "blocked_processing_lead_time_evidence",
            string.Empty,
            null,
            null,
            null,
            null,
            Array.Empty<string>(),
            new[] { reason });

    private static AcquisitionCrabPotLeadTimeResult Blocked(string reason) =>
        new(
            false,
            Array.Empty<AcquisitionProcessingLeadTimeEvaluation>(),
            new[] { reason });
}

internal sealed record AcquisitionCrabPotLeadTimeResult(
    bool EvidenceAvailable,
    AcquisitionProcessingLeadTimeEvaluation[] Evaluations,
    string[] BlockingReasons);
