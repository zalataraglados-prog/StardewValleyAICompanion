using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private IEnumerable<EventCandidate> BindCraftingQuestCandidates(
        SnapshotEnvelope snapshot,
        QuestCandidateRef quest,
        StrategyCommitmentLedger? commitmentLedger)
    {
        var context = ReadStateFieldValue(snapshot, "player", "quest_crafting");
        if (!context.HasValue ||
            context.Value.ValueKind != JsonValueKind.Object ||
            !context.Value.TryGetProperty("rows", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return new[]
            {
                BlockedQuestCandidate(
                    snapshot,
                    quest,
                    "quest_crafting_projection_unavailable")
            };
        }

        var matching = rows.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .Where(row => string.Equals(
                ReadString(row, "quest_id"),
                quest.QuestId,
                StringComparison.Ordinal))
            .Where(row => ItemIdentityMatches(
                ReadString(row, "output_item_id"),
                ReadString(row, "target_qualified_item_id"),
                quest.RequiredItemId))
            .ToArray();
        if (matching.Length == 0)
        {
            var target = rows.EnumerateArray().FirstOrDefault(row =>
                row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(row, "quest_id"),
                    quest.QuestId,
                    StringComparison.Ordinal));
            return new[]
            {
                BlockedQuestCandidate(
                    snapshot,
                    quest,
                    target.ValueKind == JsonValueKind.Object
                        ? ReadString(target, "craft_candidate_status")
                        : "quest_crafting_target_row_not_found")
            };
        }

        return matching
            .SelectMany(row => BuildQuestCraftingCandidates(
                snapshot,
                quest,
                row,
                commitmentLedger))
            .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
    }

    private IEnumerable<EventCandidate> BuildQuestCraftingCandidates(
        SnapshotEnvelope snapshot,
        QuestCandidateRef quest,
        JsonElement row,
        StrategyCommitmentLedger? commitmentLedger)
    {
        yield return BuildQuestCraftingCandidate(
            snapshot,
            quest,
            row,
            null,
            commitmentLedger);
        if (!row.TryGetProperty(
                "workbench_crafting_sources",
                out var sources) ||
            sources.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var source in sources.EnumerateArray())
        {
            if (source.ValueKind == JsonValueKind.Object)
            {
                yield return BuildQuestCraftingCandidate(
                    snapshot,
                    quest,
                    row,
                    source,
                    commitmentLedger);
            }
        }
    }

    private EventCandidate BuildQuestCraftingCandidate(
        SnapshotEnvelope snapshot,
        QuestCandidateRef quest,
        JsonElement row,
        JsonElement? workbenchSource,
        StrategyCommitmentLedger? commitmentLedger)
    {
        var usesWorkbench = workbenchSource.HasValue;
        var source = workbenchSource ?? row;
        var recipeName = ReadString(row, "recipe_name");
        var outputQualifiedId = ReadString(
            row,
            "output_qualified_item_id");
        var outputItemId = ReadString(row, "output_item_id");
        var outputCount = Math.Max(
            1,
            ReadInt(row, "output_count_per_craft", 1));
        var timesCrafted = Math.Max(0, ReadInt(row, "times_crafted"));
        var ingredients = source.TryGetProperty(
            "ingredient_rows",
            out var ingredientRows)
                ? ingredientRows
                : default;
        var ingredientsJson = ingredients.ValueKind == JsonValueKind.Array
            ? ingredients.GetRawText()
            : "[]";
        var craftingSource = usesWorkbench
            ? "native_workbench_crafting_menu"
            : "native_personal_crafting_menu";
        var expectedReady = usesWorkbench
            ? "ready_for_native_workbench_crafting_menu"
            : "ready_for_native_personal_crafting_menu";
        var accessPointId = usesWorkbench
            ? ReadString(source, "workbench_access_point_id")
            : string.Empty;
        var locationId = usesWorkbench
            ? ReadString(source, "location_id")
            : ReadStateFieldString(snapshot, "player", "location_id");
        var targetX = usesWorkbench ? NullableReadInt(source, "tile_x") : null;
        var targetY = usesWorkbench ? NullableReadInt(source, "tile_y") : null;
        var currentLocation = ReadStateFieldString(
            snapshot,
            "player",
            "location_id");
        var sameLocation = string.Equals(
            locationId,
            currentLocation,
            StringComparison.Ordinal);
        var stand = usesWorkbench && sameLocation &&
            targetX.HasValue && targetY.HasValue
                ? FindBestStandTile(snapshot, targetX.Value, targetY.Value)
                : null;
        var nodeIdsJson = usesWorkbench && source.TryGetProperty(
            "native_container_node_ids",
            out var nodeIds)
                ? nodeIds.GetRawText()
                : "[]";
        var reservation = new MachineCraftingMaterialReservationGuard()
            .Evaluate(
                snapshot,
                ingredients,
                usesWorkbench,
                commitmentLedger);
        var reasons = new List<string>();
        if (!string.Equals(
                ReadString(source, "craft_candidate_status"),
                expectedReady,
                StringComparison.Ordinal))
        {
            reasons.Add("quest_crafting_recipe_not_ready");
        }
        if (ReadBool(
                source,
                "output_inventory_acceptance_after_material_consumption") !=
            true)
        {
            reasons.Add("quest_crafting_output_cannot_fit");
        }
        if (ActiveMenuOpenForCandidate(snapshot))
        {
            reasons.Add("quest_crafting_menu_must_be_clear");
        }
        if (string.IsNullOrWhiteSpace(recipeName) ||
            string.IsNullOrWhiteSpace(outputQualifiedId))
        {
            reasons.Add("quest_crafting_recipe_identity_unavailable");
        }
        if (usesWorkbench &&
            (string.IsNullOrWhiteSpace(accessPointId) || stand is null))
        {
            reasons.Add(sameLocation
                ? "quest_crafting_workbench_access_unavailable"
                : "quest_crafting_workbench_requires_current_location_rebind");
        }
        if (!reservation.Ready)
        {
            reasons.AddRange(reservation.BlockingReasons);
        }

        var sourceCandidate = new EventCandidate
        {
            CandidateId = "quest-craft:" + quest.QuestId + ":" +
                recipeName + (usesWorkbench
                    ? ":workbench:" + accessPointId
                    : ":personal"),
            Kind = "craft_quest_item",
            Available = reasons.Count == 0,
            LocationId = locationId,
            ItemId = outputItemId,
            QualifiedItemId = outputQualifiedId,
            Quantity = outputCount,
            EstimatedTicks = 30,
            EnergyCost = 0,
            AvailabilityClass = usesWorkbench
                ? "transparent_quest_recipe_native_workbench_crafting"
                : "transparent_quest_recipe_native_personal_crafting",
            ExpectedEffect =
                "player.inventory.materials_consumed_by_native_recipe=true" +
                ";player.inventory.output_increases=" +
                outputQualifiedId + ":" + outputCount +
                ";quest.completed_by_native_OnRecipeCrafted=true" +
                ";quest_id=" + quest.QuestId,
            BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Parameters = new[]
            {
                Parameter("recipe_name", recipeName),
                Parameter("output_qualified_item_id", outputQualifiedId),
                Parameter("output_item_id", outputItemId),
                Parameter("output_count", outputCount.ToString(CultureInfo.InvariantCulture)),
                Parameter("times_crafted_before", timesCrafted.ToString(CultureInfo.InvariantCulture)),
                Parameter("ingredient_rows_json", ingredientsJson),
                Parameter("crafting_source", craftingSource),
                Parameter("workbench_access_point_id", accessPointId),
                Parameter("workbench_container_node_ids_json", nodeIdsJson),
                Parameter("location_id", locationId),
                Parameter("target_tile_x", targetX?.ToString(CultureInfo.InvariantCulture)),
                Parameter("target_tile_y", targetY?.ToString(CultureInfo.InvariantCulture)),
                Parameter("stand_tile_x", stand?.X.ToString(CultureInfo.InvariantCulture)),
                Parameter("stand_tile_y", stand?.Y.ToString(CultureInfo.InvariantCulture)),
                Parameter("quest_crafting_target_qualified_item_id", ReadString(row, "target_qualified_item_id")),
                Parameter("commitment_ledger_id", reservation.LedgerId),
                Parameter("commitment_ledger_revision", reservation.LedgerRevision.ToString(CultureInfo.InvariantCulture)),
                Parameter("material_reservation_guard_status", reservation.Status),
                Parameter("material_reservation_ledger_id", reservation.LedgerId),
                Parameter("material_reservation_ledger_revision", reservation.LedgerRevision.ToString(CultureInfo.InvariantCulture)),
                Parameter("material_reservation_ids_json", JsonSerializer.Serialize(reservation.ReservationIds)),
                Parameter("native_contract", "CraftingPage.receiveLeftClick->CraftingRecipe.consumeIngredients->Quest.OnRecipeCrafted")
            }
        };
        return AttachQuest(sourceCandidate, quest);
    }
}
