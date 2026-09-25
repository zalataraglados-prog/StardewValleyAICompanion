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
        private static string ReadSelectedBuriedItemSources(
            SnapshotEnvelope snapshot,
            MiningFloorStepPlan floorStep)
        {
            if (floorStep.StepKind != MiningFloorStepKinds.DigBuriedItem ||
                !floorStep.TargetTileX.HasValue ||
                !floorStep.TargetTileY.HasValue ||
                floorStep.TargetQualifiedItemId != "(O)585")
            {
                return "[]";
            }

            var currentMine = ReadStateFieldValue(
                snapshot,
                "mining",
                "current_mine");
            var tiles = ReadStateFieldValue(snapshot, "mining", "tiles");
            if (!currentMine.HasValue ||
                ReadBool(currentMine.Value, "is_quarry_mine") ||
                !tiles.HasValue ||
                !tiles.Value.TryGetProperty(
                    "collision_context",
                    out var context) ||
                ReadString(context, "status") != "available" ||
                ReadString(context, "buried_item_diggable_encoding") !=
                    "row_major_strings_1_native_hoe_hook_eligible_0_ineligible" ||
                ReadString(context, "buried_item_target_qualified_item_id") !=
                    "(O)585" ||
                ReadString(context, "buried_item_probability_status") !=
                    "exact_native_branch_probability_unrealized_global_rng" ||
                ReadString(context, "buried_item_rng_contract") !=
                    "Game1.random_not_read_or_replayed_by_transparent_bridge" ||
                !SelectedTileIsEligible(context, floorStep) ||
                !context.TryGetProperty(
                    "buried_item_authoritative_route_sources",
                    out var sourceRows) ||
                sourceRows.ValueKind != JsonValueKind.Array)
            {
                return "[]";
            }

            var sources = new List<MiningRouteSource>();
            foreach (var row in sourceRows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object ||
                    ReadString(row, "route_kind") !=
                        "native_mine_buried_item" ||
                    ReadString(row, "source_id") !=
                        "MineShaft.checkForBuriedItem" ||
                    ReadString(row, "qualified_item_id") != "(O)585")
                {
                    return "[]";
                }

                sources.Add(new MiningRouteSource(
                    "native_mine_buried_item",
                    "MineShaft.checkForBuriedItem",
                    "(O)585"));
            }

            return SerializeSources(sources);
        }

        private static bool SelectedTileIsEligible(
            JsonElement context,
            MiningFloorStepPlan floorStep)
        {
            if (!context.TryGetProperty(
                    "buried_item_diggable_rows",
                    out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var y = floorStep.TargetTileY!.Value;
            var x = floorStep.TargetTileX!.Value;
            var rowValues = rows.EnumerateArray()
                .Select(row => row.GetString() ?? string.Empty)
                .ToArray();
            return y >= 0 && y < rowValues.Length &&
                x >= 0 && x < rowValues[y].Length &&
                rowValues[y][x] == '1';
        }
    }
}
