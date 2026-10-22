using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] AcquisitionMachinePlacementCandidates(
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger? commitmentLedger,
        AcquisitionMachineCapacityRequest request)
    {
        var generic = MachinePlacementCandidates(snapshot, commitmentLedger);
        if (!IsPineTapperAcquisition(request))
            return generic;

        var context = ReadStateFieldValue(snapshot, "player", "machine_placement");
        if (!context.HasValue ||
            context.Value.ValueKind != JsonValueKind.Object ||
            !context.Value.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var currentLocationId = ReadStateFieldString(
            snapshot,
            "player",
            "location_id");
        var projectionFingerprint = ReadString(
            context.Value,
            "static_projection_fingerprint");
        var result = new List<EventCandidate>();
        foreach (var row in rows.EnumerateArray().Where(row =>
                     row.ValueKind == JsonValueKind.Object &&
                     string.Equals(
                         ReadString(row, "qualified_item_id"),
                         request.MachineQualifiedItemId,
                         StringComparison.OrdinalIgnoreCase)))
        {
            var currentLocation = CurrentMachinePlacementLocation(
                row,
                currentLocationId);
            var forced = currentLocation.HasValue
                ? SelectAcquisitionTapperPlacement(
                    snapshot,
                    currentLocation.Value,
                    currentLocationId,
                    request.RouteKind,
                    request.SourceId,
                    request.OutputQualifiedItemId)
                : null;
            if (forced is not null)
            {
                var supportIntent = MachineSupportIntentProjection
                    .SelectForPlacement(
                        commitmentLedger,
                        request.MachineQualifiedItemId,
                        currentLocationId);
                result.Add(BuildMachinePlacementCandidate(
                    snapshot,
                    row,
                    currentLocationId,
                    projectionFingerprint,
                    commitmentLedger,
                    relocationIntent: null,
                    supportIntent,
                    forced));
            }
        }

        result.AddRange(generic.Where(candidate =>
        {
            if (!string.Equals(
                    candidate.Kind,
                    "route_connector_tile",
                    StringComparison.Ordinal))
            {
                return false;
            }
            var targetLocation = ReadParameter(
                candidate.Parameters,
                "continuation.machine_location_id");
            return rows.EnumerateArray().Any(row =>
                row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(row, "qualified_item_id"),
                    request.MachineQualifiedItemId,
                    StringComparison.OrdinalIgnoreCase) &&
                row.TryGetProperty("locations", out var locations) &&
                locations.ValueKind == JsonValueKind.Array &&
                locations.EnumerateArray().Any(location =>
                    string.Equals(
                        ReadString(location, "location_id"),
                        targetLocation,
                        StringComparison.OrdinalIgnoreCase) &&
                    HasAcquisitionTapperTarget(
                        location,
                        request.RouteKind,
                        request.SourceId,
                        request.OutputQualifiedItemId)));
        }));

        return result
            .Select(candidate => CloneCandidate(
                candidate,
                expectedEffect: candidate.ExpectedEffect +
                    ";machine_acquisition_exact_tapper_target=true",
                parameters: candidate.Parameters.Concat(new[]
                {
                    Parameter(
                        "machine_acquisition_target_route_kind",
                        request.RouteKind),
                    Parameter(
                        "machine_acquisition_target_source_id",
                        request.SourceId),
                    Parameter(
                        "machine_acquisition_target_output_qualified_item_id",
                        request.OutputQualifiedItemId)
                }).ToArray()))
            .OrderByDescending(candidate => candidate.Available)
            .ThenBy(candidate => candidate.EstimatedTicks <= 0
                ? int.MaxValue
                : candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
    }

    private MachinePlacementTileSelection? SelectAcquisitionTapperPlacement(
        SnapshotEnvelope snapshot,
        JsonElement location,
        string locationId,
        MachineSupportIntent? supportIntent)
    {
        if (supportIntent is null ||
            !MachineSupportIntentProjection.TryReadAcquisitionRouteSupport(
                supportIntent.SupportSourcesJson,
                supportIntent.GoalId,
                supportIntent.QualifiedItemId,
                out var source) ||
            !string.Equals(
                source.RouteKind,
                "native_wild_tree_tapper_output",
                StringComparison.Ordinal))
        {
            return null;
        }

        return SelectAcquisitionTapperPlacement(
            snapshot,
            location,
            locationId,
            source.RouteKind,
            source.SourceId,
            source.OutputQualifiedItemId);
    }

    private MachinePlacementTileSelection? SelectAcquisitionTapperPlacement(
        SnapshotEnvelope snapshot,
        JsonElement location,
        string locationId,
        string routeKind,
        string sourceId,
        string outputQualifiedItemId)
    {
        if (!string.Equals(
                routeKind,
                "native_wild_tree_tapper_output",
                StringComparison.Ordinal) ||
            !location.TryGetProperty("native_tapper_targets", out var targets) ||
            targets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        foreach (var target in targets.EnumerateArray()
                     .Where(target => TapperTargetMatches(
                         target,
                         sourceId,
                         outputQualifiedItemId))
                     .OrderBy(target =>
                         Math.Abs(playerX - ReadInt(target, "tile_x")) +
                         Math.Abs(playerY - ReadInt(target, "tile_y")))
                     .ThenBy(target => ReadInt(target, "tile_y"))
                     .ThenBy(target => ReadInt(target, "tile_x")))
        {
            var targetX = ReadInt(target, "tile_x", -1);
            var targetY = ReadInt(target, "tile_y", -1);
            var stand = FindBestMachineStandTile(
                snapshot,
                locationId,
                targetX,
                targetY);
            if (stand.Tile is not null)
            {
                return new MachinePlacementTileSelection(
                    new CandidateTile(targetX, targetY),
                    stand.Tile);
            }
        }
        return null;
    }

    private static bool HasAcquisitionTapperTarget(
        JsonElement location,
        string routeKind,
        string sourceId,
        string outputQualifiedItemId) =>
        string.Equals(
            routeKind,
            "native_wild_tree_tapper_output",
            StringComparison.Ordinal) &&
        location.TryGetProperty("native_tapper_targets", out var targets) &&
        targets.ValueKind == JsonValueKind.Array &&
        targets.EnumerateArray().Any(target =>
            TapperTargetMatches(
                target,
                sourceId,
                outputQualifiedItemId));

    private static bool TapperTargetMatches(
        JsonElement target,
        string sourceId,
        string outputQualifiedItemId) =>
        target.ValueKind == JsonValueKind.Object &&
        string.Equals(
            ReadString(target, "projection_status"),
            "native_object_placement_action_compatible_tapper_target",
            StringComparison.Ordinal) &&
        target.TryGetProperty("output_rows", out var rows) &&
        rows.ValueKind == JsonValueKind.Array &&
        rows.EnumerateArray().Any(row =>
            string.Equals(
                ReadString(row, "source_id"),
                sourceId,
                StringComparison.Ordinal) &&
            ReadInt(row, "days_until_ready") == 5 &&
            string.IsNullOrWhiteSpace(ReadString(row, "condition")) &&
            ReadInt(row, "days_until_ready_modifier_count") == 0 &&
            row.TryGetProperty("item_qualified_ids", out var items) &&
            items.ValueKind == JsonValueKind.Array &&
            items.EnumerateArray().Any(item =>
                item.ValueKind == JsonValueKind.String &&
                string.Equals(
                    item.GetString(),
                    outputQualifiedItemId,
                    StringComparison.Ordinal)));

    private static bool IsPineTapperAcquisition(
        AcquisitionMachineCapacityRequest request) =>
        string.Equals(
            request.RouteKind,
            "native_wild_tree_tapper_output",
            StringComparison.Ordinal) &&
        string.Equals(request.SourceId, "wild_tree:3:0", StringComparison.Ordinal) &&
        string.Equals(request.OutputQualifiedItemId, "(O)726", StringComparison.Ordinal) &&
        string.Equals(request.MachineQualifiedItemId, "(BC)105", StringComparison.Ordinal);
}
