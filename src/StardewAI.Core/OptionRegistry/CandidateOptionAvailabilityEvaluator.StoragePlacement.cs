using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[] StoragePlacementCandidates(
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger? commitmentLedger)
        {
            var context = ReadStateFieldValue(
                snapshot,
                "player",
                "storage_placement");
            if (!context.HasValue ||
                context.Value.ValueKind != JsonValueKind.Object ||
                !context.Value.TryGetProperty(
                    "rows",
                    out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var currentLocationId =
                ReadStateFieldString(
                    snapshot,
                    "player",
                    "location_id");
            var projectionFingerprint = ReadString(
                context.Value,
                "static_projection_fingerprint");
            var routeCandidates = RouteConnectorCandidates(
                snapshot,
                int.MaxValue);
            return rows.EnumerateArray()
                .Where(row =>
                    row.ValueKind ==
                    JsonValueKind.Object)
                .SelectMany(row => new[]
                    {
                        BuildStoragePlacementCandidate(
                            snapshot,
                            row,
                            currentLocationId,
                            projectionFingerprint,
                            commitmentLedger)
                    }
                    .Concat(
                        BuildRemoteStoragePlacementCandidates(
                            snapshot,
                            row,
                            currentLocationId,
                            routeCandidates,
                            commitmentLedger)))
                .OrderBy(
                    candidate => candidate.CandidateId,
                    StringComparer.Ordinal)
                .ToArray();
        }

        private static JsonElement?
            CurrentStoragePlacementLocation(
                JsonElement row,
                string currentLocationId)
        {
            if (!row.TryGetProperty(
                    "locations",
                    out var locations) ||
                locations.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var location in
                     locations.EnumerateArray())
            {
                if (location.ValueKind ==
                        JsonValueKind.Object &&
                    string.Equals(
                        ReadString(
                            location,
                            "location_id"),
                        currentLocationId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return location;
                }
            }
            return null;
        }

        private static string StorageRole(
            JsonElement row)
        {
            if (ReadBool(row, "shipping_storage") == true)
            {
                return "shipping";
            }
            if (ReadBool(row, "fridge_storage") == true)
            {
                return "fridge";
            }
            if (ReadBool(
                    row,
                    "shared_global_storage") == true)
            {
                return "shared_global";
            }
            if (ReadBool(
                    row,
                    "ordinary_material_storage") == true)
            {
                return "ordinary_material";
            }
            return "special_storage";
        }
    }
}
