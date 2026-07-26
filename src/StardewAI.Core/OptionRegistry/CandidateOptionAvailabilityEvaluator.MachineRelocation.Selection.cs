using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
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

        private static MachineRelocationTarget?
            SelectCrossLocationRelocationTarget(
                SnapshotEnvelope snapshot,
                JsonElement location,
                IReadOnlyCollection<JsonElement> peers,
                int arrivalX,
                int arrivalY)
        {
            var width = ReadInt(location, "map_width");
            var height = ReadInt(location, "map_height");
            if (width <= 0 || height <= 0)
            {
                return null;
            }

            var reachability =
                MachineRelocationReachabilityProjectionReader.Read(
                    snapshot,
                    ReadString(location, "location_id"),
                    arrivalX,
                    arrivalY);
            if (reachability is null)
            {
                return null;
            }

            var occupied = peers
                .Select(machine => TileKey(
                    ReadInt(machine, "tile_x"),
                    ReadInt(machine, "tile_y")))
                .ToHashSet(StringComparer.Ordinal);
            if (!TileInBounds(
                    arrivalX,
                    arrivalY,
                    width,
                    height) ||
                occupied.Contains(TileKey(arrivalX, arrivalY)) ||
                !reachability.Contains(arrivalX, arrivalY))
            {
                return null;
            }

            return EnumerateMachinePlacementTiles(location)
                .Where(tile =>
                    !occupied.Contains(TileKey(tile.X, tile.Y)) &&
                    (tile.X != arrivalX || tile.Y != arrivalY))
                .Select(tile => new
                {
                    Tile = tile,
                    ClusterDistance = NearestMachineDistance(
                        tile.X,
                        tile.Y,
                        peers),
                    Stand = SelectReachableMachineStand(
                        reachability,
                        tile)
                })
                .Where(row => row.Stand is not null)
                .OrderBy(row => row.ClusterDistance)
                .ThenBy(row => row.Stand!.RouteDistanceTiles)
                .ThenBy(row => row.Tile.Y)
                .ThenBy(row => row.Tile.X)
                .Select(row => new MachineRelocationTarget(
                    row.Tile,
                    row.Stand!.Tile,
                    row.ClusterDistance,
                    row.Stand.RouteDistanceTiles))
                .FirstOrDefault();
        }

        private static MachineReachableStand?
            SelectReachableMachineStand(
                MachineRelocationReachabilityProjection reachability,
                CandidateTile target)
        {
            return CardinalNeighbors(target)
                .Select(tile =>
                {
                    return reachability.TryGetProvenDistanceAvoiding(
                            tile.X,
                            tile.Y,
                            target.X,
                            target.Y,
                            out var distance)
                        ? new MachineReachableStand(tile, distance)
                        : null;
                })
                .Where(row => row is not null)
                .OrderBy(row => row!.RouteDistanceTiles)
                .ThenBy(row => row!.Tile.Y)
                .ThenBy(row => row!.Tile.X)
                .FirstOrDefault();
        }

        private static IEnumerable<CandidateTile> CardinalNeighbors(
            CandidateTile tile)
        {
            yield return new CandidateTile(tile.X, tile.Y - 1);
            yield return new CandidateTile(tile.X - 1, tile.Y);
            yield return new CandidateTile(tile.X + 1, tile.Y);
            yield return new CandidateTile(tile.X, tile.Y + 1);
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
            int ClusterDistance,
            int RouteDistanceTiles = 0);

        private sealed record MachineReachableStand(
            CandidateTile Tile,
            int RouteDistanceTiles);
    }
}
