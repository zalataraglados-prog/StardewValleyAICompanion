using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    internal static partial class MiningAuthoritativeRouteSourceBinding
    {
        private static string ReadSelectedStoneSources(
            SnapshotEnvelope snapshot,
            MiningFloorStepPlan floorStep)
        {
            if (!floorStep.TargetTileX.HasValue ||
                !floorStep.TargetTileY.HasValue ||
                string.IsNullOrWhiteSpace(floorStep.TargetQualifiedItemId))
            {
                return "[]";
            }

            var objects = ReadStateFieldValue(snapshot, "mining", "objects");
            if (!objects.HasValue ||
                objects.Value.ValueKind != JsonValueKind.Array)
            {
                return "[]";
            }

            var matches = objects.Value.EnumerateArray()
                .Where(obj =>
                    ReadInt(obj, "tile_x") == floorStep.TargetTileX &&
                    ReadInt(obj, "tile_y") == floorStep.TargetTileY)
                .ToArray();
            if (matches.Length != 1 ||
                ReadString(matches[0], "item_id") != "95" ||
                !string.Equals(
                    ReadString(matches[0], "qualified_item_id"),
                    floorStep.TargetQualifiedItemId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ReadString(matches[0], "drop_rule_branch"),
                    "game_location_break_stone_direct_node",
                    StringComparison.Ordinal) ||
                !matches[0].TryGetProperty(
                    "guaranteed_drop_qualified_item_ids",
                    out var guaranteedDrops) ||
                guaranteedDrops.ValueKind != JsonValueKind.Array ||
                !guaranteedDrops.EnumerateArray().Any(item =>
                    item.ValueKind == JsonValueKind.String &&
                    string.Equals(
                        item.GetString(),
                        "(O)909",
                        StringComparison.Ordinal)) ||
                !matches[0].TryGetProperty(
                    "authoritative_route_sources",
                    out var sourceRows) ||
                sourceRows.ValueKind != JsonValueKind.Array)
            {
                return "[]";
            }

            var sources = new List<MiningRouteSource>();
            foreach (var row in sourceRows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object)
                    return "[]";

                var routeKind = ReadString(row, "route_kind");
                var sourceId = ReadString(row, "source_id");
                var qualifiedItemId = ReadString(row, "qualified_item_id");
                if (!string.Equals(
                        routeKind,
                        "native_radioactive_ore_node",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        sourceId,
                        "GameLocation.breakStone",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        qualifiedItemId,
                        "(O)909",
                        StringComparison.Ordinal))
                {
                    return "[]";
                }

                sources.Add(new MiningRouteSource(
                    routeKind,
                    sourceId,
                    qualifiedItemId));
            }

            return SerializeSources(sources);
        }
    }
}
