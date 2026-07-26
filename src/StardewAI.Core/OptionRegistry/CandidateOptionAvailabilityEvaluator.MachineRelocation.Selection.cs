using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private MachineRelocationTarget? SelectRelocationTarget(
            SnapshotEnvelope snapshot,
            JsonElement location,
            string locationId,
            int sourceX,
            int sourceY,
            IReadOnlyCollection<JsonElement> peers)
        {
            return EnumerateMachinePlacementTiles(location)
                .Where(tile => tile.X != sourceX || tile.Y != sourceY)
                .Select(tile => new
                {
                    Tile = tile,
                    ClusterDistance = NearestMachineDistance(
                        tile.X,
                        tile.Y,
                        peers)
                })
                .OrderBy(row => row.ClusterDistance)
                .ThenBy(row => row.Tile.Y)
                .ThenBy(row => row.Tile.X)
                .Select(row =>
                {
                    var stand = FindBestMachineStandTile(
                        snapshot,
                        locationId,
                        row.Tile.X,
                        row.Tile.Y);
                    return stand.Tile is null
                        ? null
                        : new MachineRelocationTarget(
                            row.Tile,
                            stand.Tile,
                            row.ClusterDistance);
                })
                .FirstOrDefault(row => row is not null);
        }

        private static IEnumerable<CandidateTile>
            EnumerateMachinePlacementTiles(JsonElement location)
        {
            if (!location.TryGetProperty(
                    "static_legal_tile_ranges",
                    out var ranges) ||
                ranges.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            foreach (var range in ranges.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object))
            {
                var y = ReadInt(range, "y");
                var startX = ReadInt(range, "start_x");
                var endX = ReadInt(range, "end_x", startX - 1);
                for (var x = startX; x <= endX; x++)
                {
                    yield return new CandidateTile(x, y);
                }
            }
        }

        private static int NearestMachineDistance(
            int x,
            int y,
            IEnumerable<JsonElement> machines)
        {
            return machines.Min(machine =>
                Math.Abs(x - ReadInt(machine, "tile_x")) +
                Math.Abs(y - ReadInt(machine, "tile_y")));
        }

        private static int ReadCandidateInt(
            EventCandidate candidate,
            string name)
        {
            var value = candidate.Parameters.FirstOrDefault(parameter =>
                string.Equals(
                    parameter.Name,
                    name,
                    StringComparison.Ordinal))?.Value;
            return int.TryParse(value, out var parsed) ? parsed : 0;
        }

        private static string MachineLocationId(JsonElement machine)
        {
            var locationId = ReadString(machine, "location_id");
            return string.IsNullOrWhiteSpace(locationId)
                ? "Farm"
                : locationId;
        }

        private sealed record MachineRelocationTarget(
            CandidateTile Target,
            CandidateTile Stand,
            int ClusterDistance);
    }
}
