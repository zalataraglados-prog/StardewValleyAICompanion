using System;
using System.Collections.Generic;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string WildTreeChopNativeContract =
        "Axe native tool lifecycle -> Tree.performToolAction -> Tree.performTreeFall(trunk,stump) -> Tree.tickUpdate falling settlement; locked Data/WildTrees DropWoodOnChop, DropHardwoodOnLumberChop, ChopItems, SeedItemId, and SeedOnChopChance; complete stochastic output domain; no direct tree, RNG, debris, inventory, stats, or skill mutation";

    private static string[] ValidateWildTreeChopPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot,
        JsonElement target)
    {
        var reasons = new List<string>();
        var expectedMinimumOutputs = target.TryGetProperty("tree_chop_guaranteed_minimum_outputs", out var minimumOutputs)
            ? JsonSerializer.Serialize(minimumOutputs)
            : string.Empty;
        var expectedOutputDomain = target.TryGetProperty("tree_chop_optional_output_domain", out var outputDomain)
            ? JsonSerializer.Serialize(outputDomain)
            : string.Empty;

        if (!string.Equals(
                ReadStateFieldString(snapshot, "player", "location_id"),
                ReadParameter(action, "target_location"),
                StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("wild_tree_chop_target_location_mismatch");
        }
        if (ReadString(target, "tree_chop_acquisition_status") != "ready" ||
            ReadString(target, "tree_chop_completion_mode") != "wild_tree_removed")
        {
            reasons.Add("wild_tree_chop_not_ready_by_transparent_state");
        }
        if (ReadParameter(action, "target_runtime_type") != "StardewValley.TerrainFeatures.Tree" ||
            ReadString(target, "runtime_type") != "StardewValley.TerrainFeatures.Tree" ||
            ReadParameter(action, "tree_chop_native_contract") != WildTreeChopNativeContract ||
            ReadString(target, "tree_chop_native_contract") != WildTreeChopNativeContract)
        {
            reasons.Add("wild_tree_chop_native_contract_incomplete");
        }
        if (ReadParameter(action, "tree_chop_tree_type") != ReadString(target, "tree_type") ||
            ReadParameter(action, "tree_chop_data_contract_status") != ReadString(target, "tree_chop_data_contract_status") ||
            ReadParameter(action, "tree_chop_protection_status") != ReadString(target, "tree_chop_protection_status") ||
            ReadParameter(action, "tree_chop_projection_status") != ReadString(target, "tree_chop_projection_status") ||
            ReadParameter(action, "tree_chop_output_domain_contract") != ReadString(target, "tree_chop_output_distribution_status"))
        {
            reasons.Add("wild_tree_chop_projection_contract_drifted");
        }
        if (ReadIntParameter(action, "max_tool_swings") != NullableReadInt(target, "tree_chop_expected_tool_swings") ||
            ReadIntParameter(action, "tool_slot_index") != NullableReadInt(target, "tree_chop_tool_slot_index") ||
            ReadParameter(action, "required_tool_kind") != "axe" ||
            ReadString(target, "tree_chop_required_tool_kind") != "axe")
        {
            reasons.Add("wild_tree_chop_tool_projection_drifted");
        }
        if (string.IsNullOrWhiteSpace(expectedMinimumOutputs) ||
            string.IsNullOrWhiteSpace(expectedOutputDomain) ||
            !FruitTreeJsonEquivalent(
                ReadParameter(action, "tree_chop_guaranteed_minimum_outputs_json") ?? string.Empty,
                expectedMinimumOutputs) ||
            !FruitTreeJsonEquivalent(
                ReadParameter(action, "tree_chop_output_domain_json") ?? string.Empty,
                expectedOutputDomain))
        {
            reasons.Add("wild_tree_chop_output_domain_drifted");
        }
        if (ReadBoolParameter(action, "expected_tree_has_seed_before") != false ||
            ReadBool(target, "has_seed") != false ||
            ReadBoolParameter(action, "expected_tree_has_moss_before") != false ||
            ReadBool(target, "has_moss") != false ||
            ReadIntParameter(action, "expected_tree_growth_stage_before") != NullableReadInt(target, "growth_stage") ||
            ReadDoubleParameter(action, "expected_tree_health_before") != NullableReadDouble(target, "health") ||
            ReadBoolParameter(action, "expected_tree_present_after") != false)
        {
            reasons.Add("wild_tree_chop_tree_state_projection_drifted");
        }
        if (ReadIntParameter(action, "expected_foraging_experience_before") != NullableReadInt(target, "tree_chop_foraging_experience_before") ||
            ReadIntParameter(action, "expected_foraging_experience_delta") != NullableReadInt(target, "tree_chop_foraging_experience_delta") ||
            ReadIntParameter(action, "expected_foraging_experience_after") != NullableReadInt(target, "tree_chop_foraging_experience_after") ||
            ReadLongActionParameter(action, "expected_trees_chopped_before") != ReadWildTreeChopLong(target, "tree_chop_trees_chopped_before") ||
            ReadLongActionParameter(action, "expected_trees_chopped_delta") != ReadWildTreeChopLong(target, "tree_chop_trees_chopped_delta") ||
            ReadLongActionParameter(action, "expected_trees_chopped_after") != ReadWildTreeChopLong(target, "tree_chop_trees_chopped_after"))
        {
            reasons.Add("wild_tree_chop_stat_or_experience_projection_drifted");
        }

        return reasons.ToArray();
    }

    private static long? ReadWildTreeChopLong(JsonElement element, string property)
    {
        return element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(property, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt64(out var parsed)
                ? parsed
                : null;
    }
}
