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
    private static CompiledActionStep[] CompileAcceptSpecialOrderStep(SmallModelAction action)
    {
        var fingerprint = ReadParameter(action, "quest_offer_fingerprint");
        var questKey = ReadParameter(action, "quest_key");
        var selection = ReadIntParameter(action, "special_order_selection_index");
        if (string.IsNullOrWhiteSpace(fingerprint) || string.IsNullOrWhiteSpace(questKey) ||
            !selection.HasValue || selection.Value is < 0 or > 1)
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "accept_special_order",
                "SpecialOrdersBoard:index=" + selection.Value + ";offer=" + fingerprint,
                "matching_native_special_order_added_to_team_and_type_accepted=true",
                60)
        };
    }

    private static string[] ValidateAcceptSpecialOrderPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.accept_special_order") return Array.Empty<string>();

        var reasons = new List<string>();
        var fingerprint = ReadParameter(action, "quest_offer_fingerprint");
        var questKey = ReadParameter(action, "quest_key");
        var boardType = ReadParameter(action, "special_order_board_type");
        var generationSeed = ReadIntParameter(action, "special_order_generation_seed");
        var selectionIndex = ReadIntParameter(action, "special_order_selection_index");
        if (string.IsNullOrWhiteSpace(fingerprint)) reasons.Add("special_order_offer_fingerprint_required");
        if (string.IsNullOrWhiteSpace(questKey)) reasons.Add("special_order_quest_key_required");
        if (!generationSeed.HasValue) reasons.Add("special_order_generation_seed_required");
        if (!selectionIndex.HasValue || selectionIndex.Value is < 0 or > 1) reasons.Add("special_order_selection_index_invalid");
        if (!ActionSeesActiveMenuOpen(action, snapshot) ||
            !string.Equals(ReadParameter(action, "compiler_context.active_menu_type_before_step"), "SpecialOrdersBoard", StringComparison.Ordinal))
        {
            reasons.Add("special_order_board_menu_not_open");
        }

        var boards = ReadStateFieldValue(snapshot, "quests", "special_order_boards");
        JsonElement? matchingBoard = null;
        if (boards.HasValue && boards.Value.ValueKind == JsonValueKind.Array)
        {
            matchingBoard = boards.Value.EnumerateArray().FirstOrDefault(board =>
                board.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(board, "board_type"), boardType, StringComparison.Ordinal) &&
                ReadBool(board, "menu_open") == true);
        }
        if (!matchingBoard.HasValue || matchingBoard.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("special_order_active_board_transparent_state_missing");
        }
        else
        {
            var offers = matchingBoard.Value.TryGetProperty("offers", out var values) && values.ValueKind == JsonValueKind.Array
                ? values.EnumerateArray().ToArray()
                : Array.Empty<JsonElement>();
            var offer = offers.FirstOrDefault(value =>
                value.ValueKind == JsonValueKind.Object && ReadInt(value, "selection_index") == selectionIndex);
            if (offer.ValueKind != JsonValueKind.Object)
            {
                reasons.Add("special_order_selected_offer_missing");
            }
            else
            {
                if (!string.Equals(ReadString(offer, "offer_fingerprint"), fingerprint, StringComparison.Ordinal))
                    reasons.Add("special_order_offer_fingerprint_drifted");
                if (!offer.TryGetProperty("order", out var order) || order.ValueKind != JsonValueKind.Object)
                {
                    reasons.Add("special_order_offer_payload_missing");
                }
                else
                {
                    if (!string.Equals(ReadString(order, "quest_key"), questKey, StringComparison.Ordinal))
                        reasons.Add("special_order_quest_key_drifted");
                    if (ReadInt(order, "generation_seed") != generationSeed)
                        reasons.Add("special_order_generation_seed_drifted");
                }
            }
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
