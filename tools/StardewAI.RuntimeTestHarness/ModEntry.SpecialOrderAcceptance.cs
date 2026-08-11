using System;
using System.Linq;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteAcceptSpecialOrder(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        var menu = Game1.activeClickableMenu as SpecialOrdersBoard;
        var index = request.SpecialOrderSelectionIndex;
        var order = menu is null || !index.HasValue
            ? null
            : index.Value == 0 ? menu.leftOrder : index.Value == 1 ? menu.rightOrder : null;
        var fingerprintBefore = order is null ? string.Empty : SpecialOrderFingerprint(order);
        var activeBefore = order is not null && Game1.player.team.specialOrders.Any(value =>
            value.questKey.Value == order.questKey.Value && value.generationSeed.Value == order.generationSeed.Value);
        var acceptedTypeBefore = Game1.player.team.acceptedSpecialOrderTypes.Contains(request.SpecialOrderBoardType);
        if (request.QuestInteractionKind != "accept_special_order") reasons.Add("special_order_interaction_kind_mismatch");
        if (menu is null) reasons.Add("special_order_board_menu_not_open");
        if (menu is not null && !string.Equals(menu.boardType ?? string.Empty, request.SpecialOrderBoardType, StringComparison.Ordinal))
            reasons.Add("special_order_board_type_drifted");
        if (!index.HasValue || index.Value is < 0 or > 1) reasons.Add("special_order_selection_index_invalid");
        if (order is null) reasons.Add("special_order_selected_offer_missing");
        if (order is not null && !string.Equals(order.questKey.Value, request.QuestKey, StringComparison.Ordinal))
            reasons.Add("special_order_quest_key_drifted");
        if (order is not null && order.generationSeed.Value != request.SpecialOrderGenerationSeed)
            reasons.Add("special_order_generation_seed_drifted");
        if (!string.Equals(fingerprintBefore, request.QuestOfferFingerprint, StringComparison.Ordinal))
            reasons.Add("special_order_offer_fingerprint_drifted");
        if (menu is not null)
        {
            var button = index == 0 ? menu.acceptLeftQuestButton : menu.acceptRightQuestButton;
            if (button is null || !button.visible) reasons.Add("special_order_accept_button_not_visible");
        }
        if (activeBefore) reasons.Add("special_order_offer_already_active");
        if (acceptedTypeBefore) reasons.Add("special_order_type_already_accepted_this_cycle");

        if (reasons.Count > 0 || menu is null || order is null || !index.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "accept_special_order",
                "matching_native_special_order_added_to_team=true",
                SpecialOrderAcceptanceObservedEffect(request),
                reasons.Distinct(StringComparer.Ordinal).ToArray());
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var buttonBounds = (index.Value == 0 ? menu.acceptLeftQuestButton : menu.acceptRightQuestButton).bounds;
        menu.receiveLeftClick(buttonBounds.Center.X, buttonBounds.Center.Y);
        var accepted = Game1.player.team.specialOrders.FirstOrDefault(value =>
            value.questKey.Value == request.QuestKey &&
            value.generationSeed.Value == request.SpecialOrderGenerationSeed);
        var verified = accepted is not null &&
            Game1.player.team.acceptedSpecialOrderTypes.Contains(request.SpecialOrderBoardType) &&
            string.Equals(fingerprintBefore, request.QuestOfferFingerprint, StringComparison.Ordinal);
        var verificationReasons = verified
            ? new[]
            {
                "native_SpecialOrdersBoard_receiveLeftClick_applied",
                "matching_quest_key_and_generation_seed_added_to_team",
                "matching_special_order_type_accepted"
            }
            : new[] { "special_order_native_acceptance_receipt_mismatch" };

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "accept_special_order",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = "matching_native_special_order_added_to_team=true",
            ObservedEffect = SpecialOrderAcceptanceObservedEffect(request),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            QuestCandidateId = request.QuestCandidateId,
            QuestFamily = request.QuestFamily,
            QuestKey = request.QuestKey,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "quests.special_orders[" + request.QuestKey + "].present",
                    Before = activeBefore.ToString().ToLowerInvariant(),
                    After = (accepted is not null).ToString().ToLowerInvariant()
                },
                new SimulatedFactChange
                {
                    Path = "quests.accepted_special_order_types[" + request.SpecialOrderBoardType + "]",
                    Before = acceptedTypeBefore.ToString().ToLowerInvariant(),
                    After = Game1.player.team.acceptedSpecialOrderTypes.Contains(request.SpecialOrderBoardType).ToString().ToLowerInvariant()
                }
            }
        };
    }

    private static string SpecialOrderFingerprint(StardewValley.SpecialOrders.SpecialOrder order) =>
        SpecialOrderOfferIdentity.Compute(
            order.orderType.Value,
            order.questKey.Value,
            order.generationSeed.Value,
            order.dueDate.Value,
            order.questDuration.Value.ToString());

    private static string SpecialOrderAcceptanceObservedEffect(TrainingExecutionRequest request) =>
        "quest_key=" + request.QuestKey +
        ";generation_seed=" + (request.SpecialOrderGenerationSeed?.ToString() ?? "missing") +
        ";active=" + Game1.player.team.specialOrders.Any(value =>
            value.questKey.Value == request.QuestKey &&
            value.generationSeed.Value == request.SpecialOrderGenerationSeed).ToString().ToLowerInvariant() +
        ";type_accepted=" + Game1.player.team.acceptedSpecialOrderTypes.Contains(request.SpecialOrderBoardType).ToString().ToLowerInvariant();
}
