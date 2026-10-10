using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static void ValidateDeferredMonsterDropPickup(
            SnapshotEnvelope snapshot,
            int targetX,
            int targetY,
            string routeKind,
            string sourceId,
            string qualifiedItemId,
            int quality,
            int guaranteedMinimum,
            ICollection<string> reasons)
        {
            const string sourcePrefix = "monster:";
            var monsters = ReadStateFieldValue(snapshot, "mining", "monsters");
            var sourceName = sourceId.StartsWith(
                    sourcePrefix,
                    StringComparison.Ordinal)
                ? sourceId.Substring(sourcePrefix.Length)
                : string.Empty;
            var matches = !monsters.HasValue ||
                monsters.Value.ValueKind != JsonValueKind.Array
                    ? Array.Empty<JsonElement>()
                    : monsters.Value.EnumerateArray()
                        .Where(value => value.ValueKind ==
                                JsonValueKind.Object &&
                            ReadInt(value, "tile_x") == targetX &&
                            ReadInt(value, "tile_y") == targetY &&
                            ReadString(value, "name") == sourceName)
                        .ToArray();
            if (string.IsNullOrWhiteSpace(sourceName) || matches.Length != 1)
            {
                reasons.Add("deferred_pickup_source_monster_not_ready");
                return;
            }

            var monster = matches[0];
            if (!MonsterDeclaresRouteSource(
                    monster,
                    routeKind,
                    sourceId,
                    qualifiedItemId))
            {
                reasons.Add("deferred_pickup_authoritative_source_drifted");
            }
            if (quality != 0 ||
                guaranteedMinimum != 1 ||
                !MonsterGuaranteesItem(monster, qualifiedItemId))
            {
                reasons.Add("deferred_pickup_guaranteed_output_drifted");
            }
        }

        private static bool MonsterDeclaresRouteSource(
            JsonElement monster,
            string routeKind,
            string sourceId,
            string qualifiedItemId) =>
            monster.TryGetProperty(
                "authoritative_route_sources",
                out var sources) &&
            sources.ValueKind == JsonValueKind.Array &&
            sources.EnumerateArray().Count(value =>
                value.ValueKind == JsonValueKind.Object &&
                ReadString(value, "route_kind") == routeKind &&
                ReadString(value, "source_id") == sourceId &&
                ReadString(value, "qualified_item_id") ==
                    qualifiedItemId) == 1;

        private static bool MonsterGuaranteesItem(
            JsonElement monster,
            string qualifiedItemId) =>
            monster.TryGetProperty(
                "guaranteed_drop_qualified_item_ids",
                out var drops) &&
            drops.ValueKind == JsonValueKind.Array &&
            drops.EnumerateArray().Count(value =>
                value.ValueKind == JsonValueKind.String &&
                value.GetString() == qualifiedItemId) == 1;
    }
}
