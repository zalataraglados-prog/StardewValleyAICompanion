using System;
using System.Linq;
using System.Text.Json;

namespace StardewAI.Core.Execution
{
    public sealed partial class MiningFloorStepPlanner
    {
        private static MiningFloorStepPlan? SelectStaircasePlacement(
            JsonElement tiles,
            JsonElement resources,
            SearchResult search,
            bool[,] grid,
            int? restoreSlot)
        {
            if (!tiles.TryGetProperty(
                    "staircase_placement",
                    out var placement) ||
                placement.ValueKind != JsonValueKind.Object ||
                !string.Equals(
                    ReadString(placement, "status"),
                    "available",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ReadString(placement, "qualified_item_id"),
                    "(BC)71",
                    StringComparison.Ordinal) ||
                !placement.TryGetProperty(
                    "candidates",
                    out var candidates) ||
                candidates.ValueKind != JsonValueKind.Array ||
                !resources.TryGetProperty(
                    "staircase_slots",
                    out var staircaseSlots) ||
                staircaseSlots.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var slot = staircaseSlots
                .EnumerateArray()
                .Where(row =>
                    string.Equals(
                        ReadString(row, "qualified_item_id"),
                        "(BC)71",
                        StringComparison.Ordinal) &&
                    (ReadInt(row, "stack") ?? 0) > 0)
                .OrderBy(row => ReadInt(row, "slot_index") ?? int.MaxValue)
                .FirstOrDefault();
            var slotIndex = ReadInt(slot, "slot_index");
            var countBefore = ReadInt(resources, "staircase_count");
            if (!slotIndex.HasValue ||
                !countBefore.HasValue ||
                countBefore.Value <= 0)
            {
                return null;
            }

            return candidates
                .EnumerateArray()
                .Select(candidate => new
                {
                    Source = candidate,
                    TargetX = ReadInt(candidate, "target_tile_x"),
                    TargetY = ReadInt(candidate, "target_tile_y"),
                    ExpectedX = ReadInt(
                        candidate,
                        "expected_ladder_tile_x"),
                    ExpectedY = ReadInt(
                        candidate,
                        "expected_ladder_tile_y")
                })
                .Where(row =>
                    row.TargetX.HasValue &&
                    row.TargetY.HasValue &&
                    row.ExpectedX == row.TargetX &&
                    row.ExpectedY == row.TargetY)
                .Select(row => new
                {
                    row.Source,
                    Candidate = TargetCandidate(
                        row.TargetX!.Value,
                        row.TargetY!.Value,
                        search,
                        grid,
                        estimatedSwings: 0,
                        deterministicLadder: false)
                })
                .Where(row => row.Candidate is not null)
                .OrderBy(row => row.Candidate!.Distance)
                .ThenBy(row => row.Candidate!.TargetY)
                .ThenBy(row => row.Candidate!.TargetX)
                .Select(row =>
                {
                    var plan = Build(
                        MiningFloorStepKinds.PlaceStaircase,
                        "explicit_staircase_consumption_no_natural_descent",
                        row.Candidate!);
                    plan.StaircaseSlotIndex = slotIndex;
                    plan.StaircaseQualifiedItemId = "(BC)71";
                    plan.StaircaseCountBefore = countBefore;
                    plan.StaircaseCountAfter =
                        Math.Max(0, countBefore.Value - 1);
                    plan.RestoreSlotIndex = restoreSlot;
                    plan.SafetyWindowStatus =
                        "clear_at_snapshot_native_direct_tile";
                    return plan;
                })
                .FirstOrDefault();
        }
    }
}
