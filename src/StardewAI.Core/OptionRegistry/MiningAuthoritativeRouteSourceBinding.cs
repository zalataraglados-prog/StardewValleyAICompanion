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
        public static string ReadSelectedStepSources(
            SnapshotEnvelope snapshot,
            MiningFloorStepPlan floorStep)
        {
            return floorStep.StepKind switch
            {
                MiningFloorStepKinds.CombatMonster or
                    MiningFloorStepKinds.ShootMonster =>
                    ReadSelectedMonsterSources(snapshot, floorStep),
                MiningFloorStepKinds.MineStone =>
                    ReadSelectedStoneSources(snapshot, floorStep),
                _ => "[]"
            };
        }

        private static string ReadSelectedMonsterSources(
            SnapshotEnvelope snapshot,
            MiningFloorStepPlan floorStep)
        {
            if (floorStep.StepKind is not
                    (MiningFloorStepKinds.CombatMonster or
                     MiningFloorStepKinds.ShootMonster) ||
                !string.Equals(
                    floorStep.CombatTerminalState,
                    "defeat",
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(floorStep.TargetRuntimeIdentity) ||
                string.IsNullOrWhiteSpace(floorStep.TargetName))
            {
                return "[]";
            }

            var monsters = ReadStateFieldValue(snapshot, "mining", "monsters");
            if (!monsters.HasValue ||
                monsters.Value.ValueKind != JsonValueKind.Array)
            {
                return "[]";
            }

            var matches = monsters.Value.EnumerateArray()
                .Where(monster => string.Equals(
                    ReadString(monster, "runtime_identity"),
                    floorStep.TargetRuntimeIdentity,
                    StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1 ||
                !string.Equals(
                    ReadString(matches[0], "name"),
                    floorStep.TargetName,
                    StringComparison.Ordinal) ||
                !matches[0].TryGetProperty(
                    "authoritative_route_sources",
                    out var sourceRows) ||
                sourceRows.ValueKind != JsonValueKind.Array)
            {
                return "[]";
            }

            var expectedSourceId = "monster:" + floorStep.TargetName;
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
                        "native_monster_drop_table",
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        sourceId,
                        expectedSourceId,
                        StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(qualifiedItemId))
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

        private static string SerializeSources(
            IEnumerable<MiningRouteSource> sources)
        {
            return JsonSerializer.Serialize(sources
                .Distinct()
                .OrderBy(source => source.QualifiedItemId, StringComparer.Ordinal)
                .ThenBy(source => source.SourceId, StringComparer.Ordinal)
                .Select(source => new
                {
                    route_kind = source.RouteKind,
                    source_id = source.SourceId,
                    qualified_item_id = source.QualifiedItemId
                })
                .ToArray());
        }

        private sealed record MiningRouteSource(
            string RouteKind,
            string SourceId,
            string QualifiedItemId);
    }
}
