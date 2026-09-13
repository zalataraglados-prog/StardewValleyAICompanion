using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteTargetDateResourceBuilder
{
    private static AcquisitionRouteTargetDateResource EvaluateFishing(
        AcquisitionRouteTargetDateFacility route,
        AcquisitionResourceInputSnapshotState state)
    {
        var requiredBaitQuantity = RequiredAmount(route);
        var windows = route.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .MatchingWindows;
        Require(windows.Length > 0,
            "A matched fishing route lacks target-date windows.");
        if (windows.Any(window => !window.RequireMagicBait))
            return NotRequired(route, FishingBait);
        if (!state.RodEvidenceAvailable)
            return Blocked(route, FishingBait, state.RodBlockingReasons);
        if (!state.HasMagicBaitCapableRod)
        {
            return ResolvedMiss(
                route,
                FishingBait,
                new AcquisitionResourceInputEvaluation(
                    "magic_bait",
                    MagicBaitQualifiedItemId,
                    requiredBaitQuantity,
                    0,
                    "resolved_resource_input_miss",
                    new[] { "state.fishing.rod_inventory.value[]" },
                    Array.Empty<string>()),
                "no_magic_bait_capable_rod");
        }
        if (state.AttachedMagicBaitQuantity >= requiredBaitQuantity)
        {
            return ResolvedMatch(
                route,
                FishingBait,
                new AcquisitionResourceInputEvaluation(
                    "attached_magic_bait",
                    MagicBaitQualifiedItemId,
                    requiredBaitQuantity,
                    state.AttachedMagicBaitQuantity,
                    "resolved_resource_input_match",
                    new[] { "state.fishing.rod_inventory.value[].bait" },
                    Array.Empty<string>()));
        }
        return EvaluateMaterial(
            route,
            FishingBait,
            "loose_magic_bait",
            MagicBaitQualifiedItemId,
            checked(requiredBaitQuantity -
                state.AttachedMagicBaitQuantity),
            state);
    }

    private static AcquisitionRouteTargetDateResource EvaluateCrabPot(
        AcquisitionRouteTargetDateFacility route,
        AcquisitionResourceInputSnapshotState state)
    {
        var network = state.CrabPotNetwork;
        if (network.ValueKind != JsonValueKind.Object ||
            AcquisitionLocationRouteSnapshotState.ReadString(
                network,
                "schema_version") != "crab_pot_network.v1" ||
            AcquisitionLocationRouteSnapshotState.ReadString(
                network,
                "projection_status") !=
                "complete_crab_pots_across_loaded_persistent_locations" ||
            !network.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return Blocked(
                route,
                CrabPotService,
                "crab_pot_network_evidence_missing_or_incomplete");
        }
        var targetLocations = route.TargetEvaluations
            .Select(target => target.TargetLocationId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var targetItem = QualifiedItemId(route);
        var matching = rows.EnumerateArray().Where(row =>
                targetLocations.Contains(
                    AcquisitionLocationRouteSnapshotState.ReadString(
                        row,
                        "location_id")) &&
                CrabPotRowContains(row, targetItem))
            .ToArray();
        if (matching.Length == 0)
            return Blocked(route, CrabPotService, "matched_crab_pot_row_missing");

        foreach (var row in matching)
        {
            if (AcquisitionLocationRouteSnapshotState.ReadString(
                    row,
                    "current_output_qualified_item_id") == targetItem ||
                AcquisitionLocationRouteSnapshotState.ReadBool(
                    row,
                    "owner_has_luremaster") == true ||
                !string.IsNullOrWhiteSpace(
                    AcquisitionLocationRouteSnapshotState.ReadString(
                        row,
                        "bait_qualified_item_id")))
            {
                return NotRequired(route, CrabPotService);
            }
        }
        if (matching.All(row =>
                AcquisitionLocationRouteSnapshotState.ReadString(
                    row,
                    "service_status") == "bait_required"))
        {
            return Blocked(
                route,
                CrabPotService,
                "crab_pot_native_bait_candidate_domain_not_bound");
        }
        return Blocked(route, CrabPotService, "crab_pot_service_state_invalid");
    }

    private static bool CrabPotRowContains(
        JsonElement row,
        string qualifiedItemId)
    {
        if (AcquisitionLocationRouteSnapshotState.ReadString(
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
}
