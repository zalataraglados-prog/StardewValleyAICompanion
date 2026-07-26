using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private MachinePlacementTileSelection?
            SelectExactMachinePlacementTile(
                SnapshotEnvelope snapshot,
                JsonElement location,
                string locationId,
                int targetX,
                int targetY)
        {
            if (!MachinePlacementRangeContains(
                    location,
                    targetX,
                    targetY))
            {
                return null;
            }
            var stand = FindBestMachineStandTile(
                snapshot,
                locationId,
                targetX,
                targetY);
            return stand.Tile is null
                ? null
                : new MachinePlacementTileSelection(
                    new CandidateTile(targetX, targetY),
                    stand.Tile);
        }

        private static bool MachinePlacementRangeContains(
            JsonElement location,
            int targetX,
            int targetY)
        {
            return location.TryGetProperty(
                    "static_legal_tile_ranges",
                    out var ranges) &&
                ranges.ValueKind == JsonValueKind.Array &&
                ranges.EnumerateArray().Any(range =>
                    range.ValueKind == JsonValueKind.Object &&
                    ReadInt(range, "y") == targetY &&
                    targetX >= ReadInt(range, "start_x") &&
                    targetX <= ReadInt(
                        range,
                        "end_x",
                        ReadInt(range, "start_x") - 1));
        }

        private static MachineRelocationIntent?
            ActiveMachineRelocationIntent(
                SnapshotEnvelope snapshot,
                StrategyCommitmentLedger? commitmentLedger,
                string qualifiedItemId,
                string currentLocationId)
        {
            if (commitmentLedger is null)
            {
                return null;
            }
            return commitmentLedger.MachineRelocationIntents
                .Where(intent =>
                    string.Equals(
                        intent.Status,
                        StrategyCommitmentStatuses.Active,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        intent.QualifiedItemId,
                        qualifiedItemId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        intent.TargetLocationId,
                        currentLocationId,
                        StringComparison.OrdinalIgnoreCase) &&
                    !MachineExistsAt(
                        snapshot,
                        intent.SourceLocationId,
                        intent.SourceTileX,
                        intent.SourceTileY,
                        intent.QualifiedItemId))
                .OrderBy(intent => intent.IntentId, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static bool MachineExistsAt(
            SnapshotEnvelope snapshot,
            string locationId,
            int x,
            int y,
            string qualifiedItemId)
        {
            var machines = ReadStateFieldValue(
                snapshot,
                "farm",
                "machines");
            return machines.HasValue &&
                machines.Value.ValueKind == JsonValueKind.Array &&
                machines.Value.EnumerateArray().Any(row =>
                    row.ValueKind == JsonValueKind.Object &&
                    ReadInt(row, "tile_x") == x &&
                    ReadInt(row, "tile_y") == y &&
                    string.Equals(
                        ReadString(row, "location_id"),
                        locationId,
                        StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(
                        ReadString(row, "qualified_item_id"),
                        qualifiedItemId,
                        StringComparison.OrdinalIgnoreCase));
        }
    }
}
