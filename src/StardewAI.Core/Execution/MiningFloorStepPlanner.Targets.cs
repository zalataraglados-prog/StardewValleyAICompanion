using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Execution
{
    public sealed partial class MiningFloorStepPlanner
    {
        private static MiningFloorStepPlan? SelectTargetObject(
            JsonElement objects,
            SearchResult search,
            bool[,] grid,
            string[] targetDropIds,
            string[] sourceIds,
            int? restoreSlot)
        {
            var requestedDrops = new HashSet<string>(targetDropIds, StringComparer.OrdinalIgnoreCase);
            var explicitSources = new HashSet<string>(sourceIds, StringComparer.OrdinalIgnoreCase);
            if (requestedDrops.Count == 0 && explicitSources.Count == 0)
            {
                return null;
            }

            return objects.EnumerateArray()
                .Select(obj => BuildObjectSourceMatch(obj, requestedDrops, explicitSources, search, grid))
                .Where(row => row is not null && row.Candidate is not null)
                .OrderBy(row => row!.MatchRank)
                .ThenBy(row => row!.Candidate!.Distance + row.Candidate.Swings)
                .Select(row =>
                {
                    var stepKind = ReadBool(row!.Object, "is_container") ? MiningFloorStepKinds.BreakContainer : MiningFloorStepKinds.MineStone;
                    var plan = Build(stepKind, "target_resource_or_artifact_source_reachable", row.Candidate!);
                    plan.TargetQualifiedItemId = ReadString(row.Object, "qualified_item_id");
                    plan.ExpectedDropQualifiedItemIds = row.MatchedDropIds;
                    plan.SourceMatchStatus = row.MatchStatus;
                    plan.RestoreSlotIndex = restoreSlot;
                    plan.SafetyWindowStatus = "clear_at_snapshot";
                    if (stepKind == MiningFloorStepKinds.MineStone)
                    {
                        ApplyStoneExperienceProjection(plan, row.Object);
                    }
                    return plan;
                })
                .FirstOrDefault();
        }

        private static ObjectSourceMatch? BuildObjectSourceMatch(
            JsonElement obj,
            HashSet<string> requestedDrops,
            HashSet<string> explicitSources,
            SearchResult search,
            bool[,] grid)
        {
            var sourceId = ReadString(obj, "qualified_item_id");
            var guaranteed = new HashSet<string>(ReadStrings(obj, "guaranteed_drop_qualified_item_ids"), StringComparer.OrdinalIgnoreCase);
            var possible = new HashSet<string>(ReadStrings(obj, "possible_drop_qualified_item_ids"), StringComparer.OrdinalIgnoreCase);
            var matchedGuaranteed = requestedDrops.Where(guaranteed.Contains).OrderBy(id => id, StringComparer.Ordinal).ToArray();
            var matchedPossible = requestedDrops.Where(possible.Contains).OrderBy(id => id, StringComparer.Ordinal).ToArray();

            string matchStatus;
            int matchRank;
            string[] matchedDropIds;
            if (explicitSources.Contains(sourceId))
            {
                matchStatus = "explicit_source_id";
                matchRank = 0;
                matchedDropIds = matchedPossible;
            }
            else if (matchedGuaranteed.Length > 0)
            {
                matchStatus = "guaranteed_drop";
                matchRank = 0;
                matchedDropIds = matchedGuaranteed;
            }
            else if (matchedPossible.Length > 0)
            {
                matchStatus = "conditional_drop";
                matchRank = 1;
                matchedDropIds = matchedPossible;
            }
            else
            {
                return null;
            }

            return new ObjectSourceMatch
            {
                Object = obj,
                Candidate = TargetCandidate(obj, search, grid, Math.Max(1, ReadInt(obj, "best_pickaxe_hits_remaining") ?? 1), false),
                MatchRank = matchRank,
                MatchStatus = matchStatus,
                MatchedDropIds = matchedDropIds
            };
        }

        private static MiningFloorStepPlan? SelectDebris(
            JsonElement debris,
            SearchResult search,
            bool[,] grid,
            string[] targetIds,
            int? restoreSlot,
            JsonElement? playerInventory,
            int? maximumDistance = null)
        {
            var targets = new HashSet<string>(targetIds, StringComparer.OrdinalIgnoreCase);
            MiningFloorStepPlan? best = null;
            foreach (var row in debris.EnumerateArray())
            {
                var qualifiedItemId = ReadString(row, "qualified_item_id");
                if (string.IsNullOrWhiteSpace(qualifiedItemId) ||
                    row.TryGetProperty("is_collectible_item_debris", out var collectible) && collectible.ValueKind == JsonValueKind.False ||
                    targets.Count > 0 && !targets.Contains(qualifiedItemId) ||
                    !row.TryGetProperty("chunks", out var chunks) || chunks.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var chunk in chunks.EnumerateArray())
                {
                    var candidate = WalkCandidate(chunk, search, grid);
                    if (candidate is null || maximumDistance.HasValue && candidate.Distance > maximumDistance.Value || best is not null && candidate.Distance >= best.EstimatedMovementTiles)
                    {
                        continue;
                    }

                    var plan = Build(MiningFloorStepKinds.PickupDebris, "target_debris_reachable", candidate);
                    plan.TargetQualifiedItemId = qualifiedItemId;
                    plan.DebrisIndex = ReadInt(row, "debris_index");
                    plan.InventoryItemTotalBefore = InventoryItemTotal(
                        playerInventory,
                        qualifiedItemId);
                    plan.RestoreSlotIndex = restoreSlot;
                    best = plan;
                }
            }
            return best;
        }

        private static int? InventoryItemTotal(
            JsonElement? inventory,
            string qualifiedItemId)
        {
            if (!inventory.HasValue ||
                inventory.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return inventory.Value
                .EnumerateArray()
                .Where(item => string.Equals(
                    ReadString(item, "qualified_item_id"),
                    qualifiedItemId,
                    StringComparison.OrdinalIgnoreCase))
                .Sum(item => Math.Max(0, ReadInt(item, "stack") ?? 0));
        }

        private static MiningFloorStepPlan? SelectContainer(
            JsonElement objects,
            SearchResult search,
            bool[,] grid,
            int? maximumDistance = null,
            int? targetTileX = null,
            int? targetTileY = null)
        {
            return objects.EnumerateArray()
                .Where(obj => ReadBool(obj, "is_container"))
                .Where(obj =>
                    !targetTileX.HasValue ||
                    !targetTileY.HasValue ||
                    ReadInt(obj, "tile_x") == targetTileX &&
                    ReadInt(obj, "tile_y") == targetTileY)
                .Select(obj => new
                {
                    Object = obj,
                    Candidate = TargetCandidate(obj, search, grid, Math.Max(1, ReadInt(obj, "health_or_hits_remaining") ?? 3), false)
                })
                .Where(row => row.Candidate is not null && (!maximumDistance.HasValue || row.Candidate.Distance <= maximumDistance.Value))
                .OrderBy(row => row.Candidate!.Distance + row.Candidate.Swings)
                .ThenBy(row => ReadInt(row.Object, "tile_y") ?? int.MaxValue)
                .ThenBy(row => ReadInt(row.Object, "tile_x") ?? int.MaxValue)
                .Select(row =>
                {
                    var plan = Build(MiningFloorStepKinds.BreakContainer, "opportunistic_breakable_container_within_four_tiles", row.Candidate!);
                    plan.TargetQualifiedItemId = ReadString(row.Object, "qualified_item_id");
                    plan.SafetyWindowStatus = "clear_at_snapshot";
                    return plan;
                })
                .FirstOrDefault();
        }

        private static MiningFloorStepPlan? SelectResourceClump(
            JsonElement resourceClumps,
            SearchResult search,
            bool[,] grid,
            int? originTileX = null,
            int? originTileY = null)
        {
            if (resourceClumps.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return resourceClumps.EnumerateArray()
                .Where(clump =>
                    !originTileX.HasValue ||
                    !originTileY.HasValue ||
                    ReadInt(clump, "tile_x") == originTileX &&
                    ReadInt(clump, "tile_y") == originTileY)
                .Where(clump =>
                    ReadBool(clump, "native_executor_supported") &&
                    ReadBool(clump, "tool_gate_satisfied") &&
                    string.Equals(ReadString(clump, "executor_status"), "native_executor_available", StringComparison.Ordinal))
                .Select(clump =>
                {
                    var tileX = ReadInt(clump, "tile_x");
                    var tileY = ReadInt(clump, "tile_y");
                    var width = ReadInt(clump, "width");
                    var height = ReadInt(clump, "height");
                    var swings = ReadInt(clump, "expected_hits_remaining");
                    return new
                    {
                        Clump = clump,
                        TileX = tileX,
                        TileY = tileY,
                        Width = width,
                        Height = height,
                        Candidate = tileX.HasValue && tileY.HasValue && width > 0 && height > 0 && swings > 0
                            ? RectangleTargetCandidate(tileX.Value, tileY.Value, width.Value, height.Value, search, grid, swings.Value)
                            : null
                    };
                })
                .Where(row => row.Candidate is not null)
                .OrderBy(row => row.Candidate!.Distance + row.Candidate.Swings)
                .ThenBy(row => row.Candidate!.Swings)
                .ThenBy(row => row.Candidate!.Distance)
                .ThenBy(row => row.TileY)
                .ThenBy(row => row.TileX)
                .Select(row =>
                {
                    var plan = Build(MiningFloorStepKinds.BreakResourceClump, "reachable_supported_resource_clump", row.Candidate!);
                    plan.ResourceClumpTileX = row.TileX;
                    plan.ResourceClumpTileY = row.TileY;
                    plan.ResourceClumpWidth = row.Width;
                    plan.ResourceClumpHeight = row.Height;
                    plan.ResourceClumpParentSheetIndex = ReadInt(row.Clump, "parent_sheet_index");
                    plan.ResourceClumpHealth = ReadDouble(
                        row.Clump,
                        "health");
                    plan.ToolSlotIndex = ReadInt(row.Clump, "selected_tool_slot_index");
                    plan.RequiredToolKind = ReadString(row.Clump, "required_tool");
                    plan.ResourceClumpMinimumUpgradeLevel = ReadInt(
                        row.Clump,
                        "minimum_upgrade_level");
                    plan.ResourceClumpToolQualifiedItemId = ReadString(
                        row.Clump,
                        "selected_tool_qualified_item_id");
                    plan.ResourceClumpToolUpgradeLevel = ReadInt(
                        row.Clump,
                        "selected_tool_upgrade_level");
                    plan.ResourceClumpToolAdditionalPower = ReadInt(
                        row.Clump,
                        "selected_tool_additional_power");
                    plan.ResourceClumpToolEffectiveUpgradeLevel = ReadInt(
                        row.Clump,
                        "selected_tool_effective_upgrade_level");
                    plan.ResourceClumpDamagePerHit = ReadDouble(
                        row.Clump,
                        "damage_per_hit");
                    plan.ExpectedOutputItemsJson = ReadString(
                        row.Clump,
                        "expected_core_output_items_json");
                    plan.ResourceClumpPossibleSecretNoteQualifiedItemId =
                        ReadString(
                            row.Clump,
                            "possible_secret_note_qualified_item_id");
                    plan.TargetRuntimeType = ReadString(
                        row.Clump,
                        "runtime_type");
                    plan.SafetyWindowStatus = "clear_at_snapshot";
                    return plan;
                })
                .FirstOrDefault();
        }

    }
}
