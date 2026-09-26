using System;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Execution
{
    public sealed partial class MiningFloorStepPlanner
    {
        private const string BuriedItemTargetQualifiedItemId = "(O)585";
        private const double BuriedItemTargetProbabilityPerHoeCycle =
            0.001575d;

        private static MiningFloorStepPlan? SelectBuriedItemDig(
            JsonElement tiles,
            JsonElement resources,
            SearchResult search,
            bool[,] collisionGrid,
            int? restoreSlot)
        {
            if (!TryBuriedItemDigGrid(
                    tiles,
                    collisionGrid,
                    out var diggableGrid,
                    out var probability) ||
                !TryFirstToolSlot(resources, "hoe_slots", out var hoeSlot))
            {
                return null;
            }

            Candidate? best = null;
            for (var y = 0; y < diggableGrid.GetLength(1); y++)
            {
                for (var x = 0; x < diggableGrid.GetLength(0); x++)
                {
                    if (!diggableGrid[x, y])
                        continue;

                    var candidate = TargetCandidate(
                        x,
                        y,
                        search,
                        collisionGrid,
                        estimatedSwings: 1,
                        deterministicLadder: false);
                    if (candidate is null ||
                        best is not null &&
                        (candidate.Distance > best.Distance ||
                         candidate.Distance == best.Distance &&
                         (candidate.TargetY > best.TargetY ||
                          candidate.TargetY == best.TargetY &&
                          candidate.TargetX >= best.TargetX)))
                    {
                        continue;
                    }

                    best = candidate;
                }
            }

            if (best is null)
                return null;

            var plan = Build(
                MiningFloorStepKinds.DigBuriedItem,
                "native_mine_buried_item_dig_tile_reachable",
                best);
            plan.TargetQualifiedItemId = BuriedItemTargetQualifiedItemId;
            plan.ExpectedDropQualifiedItemIds =
                new[] { BuriedItemTargetQualifiedItemId };
            plan.SourceMatchStatus =
                "exact_native_mineshaft_buried_item_opportunity";
            plan.TargetDropChancePreview = probability;
            plan.TargetDropProbabilityStatus =
                "exact_native_probability_unrealized_global_rng";
            plan.ToolSlotIndex = hoeSlot;
            plan.RequiredToolKind = "hoe";
            plan.RestoreSlotIndex = restoreSlot;
            plan.SafetyWindowStatus = "clear_at_snapshot";
            return plan;
        }

        private static bool TryBuriedItemDigGrid(
            JsonElement tiles,
            bool[,] collisionGrid,
            out bool[,] diggableGrid,
            out double probability)
        {
            diggableGrid = new bool[0, 0];
            probability = 0d;
            if (!tiles.TryGetProperty("collision_context", out var context) ||
                ReadString(context, "status") != "available" ||
                ReadString(context, "buried_item_diggable_encoding") !=
                    "row_major_strings_1_native_hoe_hook_eligible_0_ineligible" ||
                ReadString(context, "buried_item_target_qualified_item_id") !=
                    BuriedItemTargetQualifiedItemId ||
                ReadString(context, "buried_item_probability_status") !=
                    "exact_native_branch_probability_unrealized_global_rng" ||
                ReadString(context, "buried_item_rng_contract") !=
                    "Game1.random_not_read_or_replayed_by_transparent_bridge" ||
                !context.TryGetProperty(
                    "buried_item_target_probability_per_hoe_cycle",
                    out var probabilityValue) ||
                !probabilityValue.TryGetDouble(out probability) ||
                Math.Abs(
                    probability - BuriedItemTargetProbabilityPerHoeCycle) >
                    0.000000000001d ||
                !context.TryGetProperty(
                    "buried_item_diggable_rows",
                    out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var width = collisionGrid.GetLength(0);
            var height = collisionGrid.GetLength(1);
            var rowValues = rows.EnumerateArray()
                .Select(row => row.GetString() ?? string.Empty)
                .ToArray();
            if (rowValues.Length != height ||
                rowValues.Any(row =>
                    row.Length != width ||
                    row.Any(value => value != '0' && value != '1')))
            {
                return false;
            }

            diggableGrid = new bool[width, height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    diggableGrid[x, y] = rowValues[y][x] == '1';
                }
            }
            return true;
        }

        private static bool TryFirstToolSlot(
            JsonElement resources,
            string property,
            out int slotIndex)
        {
            slotIndex = -1;
            if (resources.ValueKind != JsonValueKind.Object ||
                !resources.TryGetProperty(property, out var slots) ||
                slots.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var values = slots.EnumerateArray()
                .Select(slot => ReadInt(slot, "slot_index"))
                .Where(value => value.HasValue && value.Value >= 0)
                .Select(value => value!.Value)
                .OrderBy(value => value)
                .ToArray();
            if (values.Length == 0)
                return false;

            slotIndex = values[0];
            return true;
        }
    }
}
