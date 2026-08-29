using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] WildTreeProductHarvestCandidates(SnapshotEnvelope snapshot)
    {
        var features = ReadStateFieldValue(snapshot, "current_location", "terrain_features");
        if (!features.HasValue || features.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var locationId = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return features.Value.EnumerateArray()
            .Where(feature => feature.ValueKind == JsonValueKind.Object && ReadString(feature, "runtime_type") == "StardewValley.TerrainFeatures.Tree")
            .Select(feature =>
            {
                var x = ReadInt(feature, "tile_x");
                var y = ReadInt(feature, "tile_y");
                var interaction = FindBestTerrainInteraction(snapshot, x, y, 1);
                var status = ReadString(feature, "tree_product_harvest_status");
                var guaranteed = ReadArray(feature, "tree_product_guaranteed_outputs");
                var optional = ReadArray(feature, "tree_product_optional_output_domain");
                var safeSlot = ReadInt(feature, "tree_product_safe_slot_index");
                var restoreSlot = ReadInt(feature, "tree_product_restore_slot_index");
                var reasons = new List<string>();
                if (status != "ready") reasons.Add(string.IsNullOrWhiteSpace(status) ? "tree_product_projection_unavailable" : status);
                if (ReadString(feature, "tree_product_data_contract_status") != "exact_locked_base_1.6.15" ||
                    ReadString(feature, "tree_product_projection_status") != "exact_from_native_tree_performUseAction_shake_and_locked_wild_tree_data" ||
                    ReadString(feature, "tree_product_output_distribution_status") != "complete_stochastic_native_branch_domain_no_rng_consumed")
                    reasons.Add("tree_product_projection_incomplete");
                if (guaranteed.Length != 1 || string.IsNullOrWhiteSpace(ReadString(guaranteed[0], "qualified_item_id")) || ReadInt(guaranteed[0], "quantity") != 1)
                    reasons.Add("tree_product_guaranteed_output_incomplete");
                if (safeSlot is < 0 or > 11 || restoreSlot is < 0 or > 11) reasons.Add("tree_product_empty_toolbar_slot_unavailable");
                if (interaction is null) reasons.Add("tree_product_no_reachable_adjacent_interaction");

                var parameters = interaction is null ? Array.Empty<SmallModelActionParameter>() : WildTreeProductParameters(feature, locationId, interaction);
                if (parameters.Length > 0)
                {
                    reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate { OptionId = "executor.harvest_tree_product", Parameters = parameters }));
                }
                var distance = interaction is null ? 0 : Math.Abs(playerX - interaction.Stand.X) + Math.Abs(playerY - interaction.Stand.Y);
                var outputId = guaranteed.Length == 0 ? string.Empty : ReadString(guaranteed[0], "qualified_item_id");
                return new EventCandidate
                {
                    CandidateId = "harvest-tree-product:" + locationId + ":" + x + "," + y + ":" + ReadString(feature, "tree_type"),
                    Kind = "harvest_tree_product",
                    Available = reasons.Count == 0,
                    LocationId = locationId,
                    TileX = x,
                    TileY = y,
                    ItemId = UnqualifiedObjectId(outputId),
                    QualifiedItemId = outputId,
                    Quantity = 1,
                    ExpectedEffect = WildTreeProductExpectedEffect(feature, interaction),
                    EstimatedTicks = Math.Max(45, distance * 60 + 45),
                    EnergyCost = 0,
                    AvailabilityClass = "transparent_native_wild_tree_seed_shake",
                    BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = parameters
                };
            })
            .OrderBy(candidate => candidate.EstimatedTicks).ThenBy(candidate => candidate.TileY).ThenBy(candidate => candidate.TileX)
            .ToArray();
    }

    private static SmallModelActionParameter[] WildTreeProductParameters(JsonElement feature, string locationId, TerrainInteraction interaction)
    {
        return new[]
        {
            Parameter("target_location", locationId), Parameter("target_tile_x", ReadInt(feature, "tile_x").ToString()), Parameter("target_tile_y", ReadInt(feature, "tile_y").ToString()),
            Parameter("interaction_tile_x", interaction.Action.X.ToString()), Parameter("interaction_tile_y", interaction.Action.Y.ToString()),
            Parameter("stand_tile_x", interaction.Stand.X.ToString()), Parameter("stand_tile_y", interaction.Stand.Y.ToString()),
            Parameter("target_runtime_type", ReadString(feature, "runtime_type")), Parameter("tree_product_tree_type", ReadString(feature, "tree_type")),
            Parameter("expected_tree_has_seed_before", ReadBool(feature, "has_seed").ToString().ToLowerInvariant()), Parameter("expected_tree_has_seed_after", ReadBool(feature, "tree_product_expected_has_seed_after").ToString().ToLowerInvariant()),
            Parameter("expected_tree_was_shaken_today_before", ReadBool(feature, "was_shaken_today").ToString().ToLowerInvariant()), Parameter("expected_tree_was_shaken_today_after", ReadBool(feature, "tree_product_expected_was_shaken_today_after").ToString().ToLowerInvariant()),
            Parameter("expected_output_items_json", JsonSerializer.Serialize(ReadArray(feature, "tree_product_guaranteed_outputs"))),
            Parameter("tree_product_output_context_tags_json", JsonSerializer.Serialize(ReadStringArray(feature, "tree_product_primary_context_tags"))),
            Parameter("tree_product_output_domain_json", JsonSerializer.Serialize(ReadArray(feature, "tree_product_optional_output_domain"))),
            Parameter("tree_product_output_domain_contract", ReadString(feature, "tree_product_output_distribution_status")),
            Parameter("expected_foraging_experience_delta", ReadInt(feature, "tree_product_expected_foraging_experience_delta").ToString()),
            Parameter("safe_slot_index", ReadInt(feature, "tree_product_safe_slot_index").ToString()), Parameter("safe_slot_kind", "empty"),
            Parameter("restore_slot_index", ReadInt(feature, "tree_product_restore_slot_index").ToString()),
            Parameter("tree_product_projection_status", ReadString(feature, "tree_product_projection_status")),
            Parameter("tree_product_native_contract", ReadString(feature, "tree_product_native_contract")), Parameter("max_movement_tiles", "512")
        };
    }

    private static string WildTreeProductExpectedEffect(JsonElement feature, TerrainInteraction? interaction)
    {
        return (interaction is null ? string.Empty : "tree_product_stand_tile=" + interaction.Stand.X + "," + interaction.Stand.Y + ";tree_product_interaction_tile=" + interaction.Action.X + "," + interaction.Action.Y + ";") +
            "tree_product_tree_type=" + ReadString(feature, "tree_type") +
            ";expected_tree_has_seed_before=" + ReadBool(feature, "has_seed").ToString().ToLowerInvariant() +
            ";expected_tree_has_seed_after=false;expected_tree_was_shaken_today_before=" + ReadBool(feature, "was_shaken_today").ToString().ToLowerInvariant() +
            ";expected_tree_was_shaken_today_after=true;expected_output_items_json=" + JsonSerializer.Serialize(ReadArray(feature, "tree_product_guaranteed_outputs")) +
            ";tree_product_output_domain_json=" + JsonSerializer.Serialize(ReadArray(feature, "tree_product_optional_output_domain")) +
            ";tree_product_output_domain_contract=" + ReadString(feature, "tree_product_output_distribution_status") +
            ";expected_foraging_experience_delta=0;safe_slot_index=" + ReadInt(feature, "tree_product_safe_slot_index") +
            ";restore_slot_index=" + ReadInt(feature, "tree_product_restore_slot_index") +
            ";tree_product_projection_status=" + ReadString(feature, "tree_product_projection_status") + ";max_movement_tiles=512";
    }
}
