using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private static EventCandidate[] MachineCraftingCandidates(SnapshotEnvelope snapshot)
        {
            var context = ReadStateFieldValue(snapshot, "player", "machine_crafting");
            if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object ||
                !context.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            return rows.EnumerateArray()
                .Where(row => row.ValueKind == JsonValueKind.Object)
                .Select(row => BuildMachineCraftingCandidate(snapshot, row))
                .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .ToArray();
        }

        private static EventCandidate BuildMachineCraftingCandidate(SnapshotEnvelope snapshot, JsonElement row)
        {
            var recipeName = ReadString(row, "recipe_name");
            var outputQualifiedId = ReadString(row, "output_qualified_item_id");
            var outputItemId = ReadString(row, "output_item_id");
            var outputCount = Math.Max(1, ReadInt(row, "output_count_per_craft", 1));
            var timesCrafted = Math.Max(0, ReadInt(row, "times_crafted"));
            var ingredientRowsJson = row.TryGetProperty("ingredient_rows", out var ingredientRows)
                ? ingredientRows.GetRawText()
                : "[]";
            var blockReasons = new List<string>();
            if (!string.Equals(ReadString(row, "craft_candidate_status"), "ready_for_native_personal_crafting_menu", StringComparison.Ordinal))
            {
                blockReasons.Add("machine_recipe_not_ready_for_native_personal_crafting");
            }
            if (ReadBool(row, "output_inventory_acceptance_after_material_consumption") != true)
            {
                blockReasons.Add("machine_recipe_output_cannot_fit_after_material_consumption");
            }
            if (ActiveMenuOpenForCandidate(snapshot))
            {
                blockReasons.Add("machine_crafting_menu_must_be_clear");
            }
            if (string.IsNullOrWhiteSpace(recipeName) || string.IsNullOrWhiteSpace(outputQualifiedId))
            {
                blockReasons.Add("machine_recipe_identity_unavailable");
            }

            return new EventCandidate
            {
                CandidateId = "machine-craft:" + recipeName + ":" + outputQualifiedId,
                Kind = "craft_machine_item",
                Available = blockReasons.Count == 0,
                LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
                ExpectedEffect = "player.inventory.materials_consumed_by_native_recipe=true" +
                    ";recipe_name=" + recipeName +
                    ";output_qualified_item_id=" + outputQualifiedId +
                    ";output_item_id=" + outputItemId +
                    ";output_count=" + outputCount +
                    ";times_crafted_before=" + timesCrafted +
                    ";times_crafted_after=" + (timesCrafted + outputCount) +
                    ";native_contract=CraftingPage.receiveLeftClick",
                ItemId = outputItemId,
                QualifiedItemId = outputQualifiedId,
                Quantity = outputCount,
                EstimatedTicks = 30,
                EnergyCost = 0,
                AvailabilityClass = "transparent_machine_recipe_native_personal_crafting",
                BlockReasons = blockReasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = new[]
                {
                    Parameter("recipe_name", recipeName),
                    Parameter("output_qualified_item_id", outputQualifiedId),
                    Parameter("output_item_id", outputItemId),
                    Parameter("output_count", outputCount.ToString()),
                    Parameter("times_crafted_before", timesCrafted.ToString()),
                    Parameter("ingredient_rows_json", ingredientRowsJson),
                    Parameter("crafting_source", "native_personal_crafting_menu")
                }
            };
        }
    }
}
