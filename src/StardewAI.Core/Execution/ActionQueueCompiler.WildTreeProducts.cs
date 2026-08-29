using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string WildTreeProductNativeContract =
        "GameLocation.checkAction -> Tree.performUseAction -> Tree.shake; exact base Data/WildTrees seed branch; no direct tree, RNG, debris, inventory, or skill mutation";

    private static CompiledActionStep[] CompileHarvestWildTreeProductStep(SmallModelAction action)
    {
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        if (!x.HasValue || !y.HasValue) return Array.Empty<CompiledActionStep>();
        var estimatedTicks = Math.Max(1, ReadIntParameter(action, "estimated_minutes") ?? 1) * 60;
        return new[] { Step("harvest_tree_product", "current_location(" + x + "," + y + "):native_wild_tree_seed_shake", "current_location.terrain_features[" + x + "," + y + "].has_seed=false", estimatedTicks) };
    }

    private static string[] ValidateHarvestWildTreeProductPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.harvest_tree_product") return Array.Empty<string>();
        var reasons = new List<string>();
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var interactionX = ReadIntParameter(action, "interaction_tile_x");
        var interactionY = ReadIntParameter(action, "interaction_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var safeSlot = ReadIntParameter(action, "safe_slot_index");
        var restoreSlot = ReadIntParameter(action, "restore_slot_index");
        var guaranteedJson = ReadParameter(action, "expected_output_items_json");
        var optionalJson = ReadParameter(action, "tree_product_output_domain_json");
        if (!targetX.HasValue || !targetY.HasValue || !interactionX.HasValue || !interactionY.HasValue || !standX.HasValue || !standY.HasValue ||
            !safeSlot.HasValue || !restoreSlot.HasValue || string.IsNullOrWhiteSpace(guaranteedJson) || string.IsNullOrWhiteSpace(optionalJson) ||
            ReadBoolParameter(action, "expected_tree_has_seed_before") is null || ReadBoolParameter(action, "expected_tree_has_seed_after") is null ||
            ReadBoolParameter(action, "expected_tree_was_shaken_today_before") is null || ReadBoolParameter(action, "expected_tree_was_shaken_today_after") is null)
            return new[] { "harvest_tree_product_typed_target_fields_required" };
        if (interactionX != targetX || interactionY != targetY || Math.Abs(interactionX.Value - standX.Value) + Math.Abs(interactionY.Value - standY.Value) != 1)
            reasons.Add("harvest_tree_product_interaction_geometry_drifted");
        if (ActionSeesActiveMenuOpen(action, snapshot)) reasons.Add("harvest_tree_product_menu_must_be_clear");
        if (ReadParameter(action, "target_runtime_type") != "StardewValley.TerrainFeatures.Tree" ||
            ReadParameter(action, "tree_product_projection_status") != "exact_from_native_tree_performUseAction_shake_and_locked_wild_tree_data" ||
            ReadParameter(action, "tree_product_output_domain_contract") != "complete_stochastic_native_branch_domain_no_rng_consumed" ||
            ReadParameter(action, "tree_product_native_contract") != WildTreeProductNativeContract ||
            ReadBoolParameter(action, "expected_tree_has_seed_before") != true || ReadBoolParameter(action, "expected_tree_has_seed_after") != false ||
            ReadBoolParameter(action, "expected_tree_was_shaken_today_after") != true || ReadIntParameter(action, "expected_foraging_experience_delta") != 0)
            reasons.Add("harvest_tree_product_native_contract_incomplete");

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        if (!string.Equals(ReadParameter(action, "target_location"), currentLocation, StringComparison.OrdinalIgnoreCase)) reasons.Add("harvest_tree_product_target_location_mismatch");
        var features = ReadStateFieldValue(snapshot, "current_location", "terrain_features");
        var target = features.HasValue && features.Value.ValueKind == JsonValueKind.Array
            ? features.Value.EnumerateArray().FirstOrDefault(row => ReadString(row, "runtime_type") == "StardewValley.TerrainFeatures.Tree" && ReadInt(row, "tile_x") == targetX && ReadInt(row, "tile_y") == targetY)
            : default;
        if (target.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("harvest_tree_product_target_not_found_or_drifted");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }
        if (ReadString(target, "tree_product_harvest_status") != "ready") reasons.Add("harvest_tree_product_not_ready_by_transparent_state");
        var freshSafe = ReadNativeObjectCompilerSafeItemContext(snapshot);
        if (!freshSafe.AllowsEmpty || freshSafe.SafeSlotIndex != safeSlot || freshSafe.RestoreSlotIndex != restoreSlot || ReadParameter(action, "safe_slot_kind") != "empty")
            reasons.Add("harvest_tree_product_safe_slot_drifted");
        var freshGuaranteed = target.TryGetProperty("tree_product_guaranteed_outputs", out var guaranteed) ? JsonSerializer.Serialize(guaranteed) : string.Empty;
        var freshOptional = target.TryGetProperty("tree_product_optional_output_domain", out var optional) ? JsonSerializer.Serialize(optional) : string.Empty;
        if (ReadString(target, "tree_type") != ReadParameter(action, "tree_product_tree_type") || ReadBool(target, "has_seed") != true ||
            ReadBool(target, "was_shaken_today") != ReadBoolParameter(action, "expected_tree_was_shaken_today_before") ||
            ReadInt(target, "tree_product_safe_slot_index") != safeSlot || ReadInt(target, "tree_product_restore_slot_index") != restoreSlot ||
            !FruitTreeJsonEquivalent(freshGuaranteed, guaranteedJson) || !FruitTreeJsonEquivalent(freshOptional, optionalJson))
            reasons.Add("harvest_tree_product_projection_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
