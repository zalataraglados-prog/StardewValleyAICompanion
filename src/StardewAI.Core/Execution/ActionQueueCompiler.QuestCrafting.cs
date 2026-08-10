using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static CompiledActionStep[] CompileCraftQuestItemStep(
        SmallModelAction action)
    {
        var recipeName = ReadParameter(action, "recipe_name");
        var outputQualifiedId = ReadParameter(
            action,
            "output_qualified_item_id");
        var outputCount = ReadIntParameter(action, "output_count");
        var questId = ReadParameter(action, "quest_id");
        if (string.IsNullOrWhiteSpace(recipeName) ||
            string.IsNullOrWhiteSpace(outputQualifiedId) ||
            string.IsNullOrWhiteSpace(questId) ||
            !outputCount.HasValue)
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "craft_quest_item",
                "quest:" + questId + ":recipe:" + recipeName + ":" +
                    outputQualifiedId,
                "player.inventory.materials_consumed_by_native_recipe=true;" +
                "player.inventory.output_increases=" + outputQualifiedId +
                ":" + outputCount.Value +
                ";quests[" + questId + "].completed_by_native_OnRecipeCrafted=true",
                30)
        };
    }

    private static string[] ValidateCraftQuestItemPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger? commitmentLedger)
    {
        if (action.OptionId != "executor.craft_quest_item")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("craft_quest_item_menu_must_be_clear");
        }

        ValidateQuestIdentityAgainstSnapshot(
            snapshot,
            ReadParameter(action, "quest_family") ?? string.Empty,
            ReadParameter(action, "quest_candidate_id") ?? string.Empty,
            ReadParameter(action, "quest_id") ?? string.Empty,
            ReadParameter(action, "quest_key") ?? string.Empty,
            ReadParameter(action, "quest_runtime_type") ?? string.Empty,
            ReadIntParameter(action, "quest_objective_index"),
            ReadIntParameter(action, "quest_expected_current_count"),
            ReadIntParameter(action, "quest_expected_target_count"),
            reasons);
        if (!string.Equals(
                ReadParameter(action, "quest_family"),
                "ordinary_quest",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadParameter(action, "quest_runtime_type"),
                "CraftingQuest",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadParameter(action, "quest_next_action"),
                "craft_item",
                StringComparison.Ordinal))
        {
            reasons.Add("craft_quest_item_typed_quest_identity_required");
        }

        var row = QuestCraftingRow(
            snapshot,
            ReadParameter(action, "quest_id"),
            ReadParameter(action, "recipe_name"));
        if (!row.HasValue)
        {
            reasons.Add("craft_quest_item_recipe_not_verified_by_transparent_state");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        var usesWorkbench = string.Equals(
            ReadParameter(action, "crafting_source"),
            "native_workbench_crafting_menu",
            StringComparison.Ordinal);
        var source = usesWorkbench
            ? QuestCraftingWorkbenchSource(
                row.Value,
                ReadParameter(action, "workbench_access_point_id"))
            : row;
        if (!source.HasValue)
        {
            reasons.Add("craft_quest_item_source_not_verified_by_transparent_state");
            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        var expectedReady = usesWorkbench
            ? "ready_for_native_workbench_crafting_menu"
            : "ready_for_native_personal_crafting_menu";
        var expectedIngredients = source.Value.TryGetProperty(
            "ingredient_rows",
            out var ingredients)
                ? ingredients.GetRawText()
                : "[]";
        if (!string.Equals(
                ReadString(source.Value, "craft_candidate_status"),
                expectedReady,
                StringComparison.Ordinal) ||
            ReadBool(
                source.Value,
                "output_inventory_acceptance_after_material_consumption") !=
            true)
        {
            reasons.Add("craft_quest_item_recipe_not_ready");
        }

        if (!string.Equals(
                ReadParameter(action, "output_qualified_item_id"),
                ReadString(row.Value, "output_qualified_item_id"),
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadParameter(action, "output_item_id"),
                ReadString(row.Value, "output_item_id"),
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadParameter(action, "quest_crafting_target_qualified_item_id"),
                ReadString(row.Value, "target_qualified_item_id"),
                StringComparison.Ordinal) ||
            ReadIntParameter(action, "output_count") !=
                ReadInt(row.Value, "output_count_per_craft") ||
            ReadIntParameter(action, "times_crafted_before") !=
                ReadInt(row.Value, "times_crafted") ||
            !string.Equals(
                ReadParameter(action, "ingredient_rows_json"),
                expectedIngredients,
                StringComparison.Ordinal))
        {
            reasons.Add("craft_quest_item_projection_drifted");
        }

        if (usesWorkbench)
        {
            ValidateQuestCraftingWorkbenchBinding(
                action,
                source.Value,
                reasons);
        }

        var reservation = new MachineCraftingMaterialReservationGuard()
            .Evaluate(
                snapshot,
                ingredients,
                usesWorkbench,
                commitmentLedger);
        if (!reservation.Ready)
        {
            reasons.AddRange(reservation.BlockingReasons);
        }
        if (!string.Equals(
                ReadParameter(action, "commitment_ledger_id"),
                reservation.LedgerId,
                StringComparison.Ordinal) ||
            ReadIntParameter(action, "commitment_ledger_revision") !=
                reservation.LedgerRevision ||
            !string.Equals(
                ReadParameter(action, "material_reservation_guard_status"),
                reservation.Status,
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadParameter(action, "material_reservation_ledger_id"),
                reservation.LedgerId,
                StringComparison.Ordinal) ||
            ReadIntParameter(action, "material_reservation_ledger_revision") !=
                reservation.LedgerRevision ||
            !string.Equals(
                ReadParameter(action, "material_reservation_ids_json"),
                JsonSerializer.Serialize(reservation.ReservationIds),
                StringComparison.Ordinal))
        {
            reasons.Add("craft_quest_item_material_reservation_projection_drifted");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static void ValidateQuestCraftingWorkbenchBinding(
        SmallModelAction action,
        JsonElement source,
        ICollection<string> reasons)
    {
        var expectedNodeIds = source.TryGetProperty(
            "native_container_node_ids",
            out var nodeIds)
                ? nodeIds.GetRawText()
                : "[]";
        var targetX = NullableReadInt(source, "tile_x");
        var targetY = NullableReadInt(source, "tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        if (!string.Equals(
                ReadParameter(action, "workbench_container_node_ids_json"),
                expectedNodeIds,
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadParameter(action, "location_id"),
                ReadString(source, "location_id"),
                StringComparison.Ordinal) ||
            ReadIntParameter(action, "target_tile_x") != targetX ||
            ReadIntParameter(action, "target_tile_y") != targetY ||
            !standX.HasValue || !standY.HasValue ||
            !targetX.HasValue || !targetY.HasValue ||
            Math.Abs(standX.Value - targetX.Value) +
            Math.Abs(standY.Value - targetY.Value) != 1)
        {
            reasons.Add("craft_quest_item_workbench_projection_drifted");
        }
    }

    private static JsonElement? QuestCraftingRow(
        SnapshotEnvelope snapshot,
        string? questId,
        string? recipeName)
    {
        var context = ReadStateFieldValue(snapshot, "player", "quest_crafting");
        if (!context.HasValue ||
            context.Value.ValueKind != JsonValueKind.Object ||
            !context.Value.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(row, "quest_id"),
                    questId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadString(row, "recipe_name"),
                    recipeName,
                    StringComparison.Ordinal))
            {
                return row;
            }
        }
        return null;
    }

    private static JsonElement? QuestCraftingWorkbenchSource(
        JsonElement row,
        string? accessPointId)
    {
        if (string.IsNullOrWhiteSpace(accessPointId) ||
            !row.TryGetProperty(
                "workbench_crafting_sources",
                out var sources) ||
            sources.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var source in sources.EnumerateArray())
        {
            if (source.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(source, "workbench_access_point_id"),
                    accessPointId,
                    StringComparison.Ordinal))
            {
                return source;
            }
        }
        return null;
    }
}
