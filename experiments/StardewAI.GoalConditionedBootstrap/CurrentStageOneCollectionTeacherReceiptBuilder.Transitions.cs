using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentStageOneCollectionTeacherReceiptBuilder
{
    private static PolicyTeacherRequirementTransition[] VerifyTransitions(
        CurrentStageOneCollectionTeacherPreferenceLabel preference,
        QueueExecutionReceiptEnvelope? receipt,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var selected = preference.SelectedCandidate!;
        var queueItems = preference.CompiledQueue!.Items;
        return selected.RequirementCredits
            .Select(credit => VerifyTransition(
                credit,
                queueItems,
                receipt,
                before,
                after))
            .ToArray();
    }

    private static PolicyTeacherRequirementTransition VerifyTransition(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem[] queueItems,
        QueueExecutionReceiptEnvelope? receipt,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        return credit.BindingKind switch
        {
            "authoritative_acquisition_endpoint" => VerifyAcquisition(
                credit,
                before,
                after),
            "native_full_shipment_completion" => VerifyFullShipment(
                credit,
                queueItems,
                receipt,
                before,
                after),
            "native_museum_donation_completion" => VerifyCollectionBoolean(
                credit,
                before,
                after,
                "museum",
                "donatable_items",
                "qualified_item_id",
                "donated",
                "native_completion_false_to_true"),
            "native_community_center_payment_completion" =>
                VerifyCommunityCenterPayment(credit, before, after),
            "native_community_center_donation_completion" =>
                VerifyCommunityCenterDonation(
                    credit,
                    CommunityCenterDonationQueueItem(credit, queueItems),
                    before,
                    after),
            "authoritative_window_terminal_attempt" => VerifyCollectionBoolean(
                credit,
                before,
                after,
                "fish_collection_progress",
                "items",
                "qualified_item_id",
                "caught",
                "native_collection_false_to_true"),
            "authoritative_window_rolling_route_step" => VerifyRouteStep(
                credit,
                queueItems.LastOrDefault(item => !string.IsNullOrWhiteSpace(
                    ReadParameter(item, "expected_target_location"))),
                before,
                after),
            "authoritative_collection_rolling_route_step" => VerifyRouteStep(
                credit,
                queueItems.LastOrDefault(item => !string.IsNullOrWhiteSpace(
                    ReadParameter(item, "expected_target_location"))),
                before,
                after),
            "authoritative_supporting_prerequisite_rolling_route_step" =>
                VerifyRouteStep(
                    credit,
                    queueItems.LastOrDefault(item => !string.IsNullOrWhiteSpace(
                        ReadParameter(item, "expected_target_location"))),
                    before,
                    after),
            "authoritative_supporting_prerequisite_starter_mail_route_step" =>
                VerifyStarterMailRoute(
                    credit,
                    queueItems.LastOrDefault(),
                    before,
                    after),
            "authoritative_supporting_prerequisite_starter_mail_approach" =>
                VerifyStarterMailApproach(
                    credit,
                    queueItems.LastOrDefault(),
                    before,
                    after),
            "authoritative_supporting_prerequisite_starter_mail_open" =>
                VerifyStarterMailOpen(
                    credit,
                    queueItems.LastOrDefault(),
                    before,
                    after),
            "authoritative_supporting_prerequisite_starter_mail_wait" =>
                VerifyStarterMailWait(
                    credit,
                    queueItems.LastOrDefault(),
                    before,
                    after),
            "authoritative_supporting_prerequisite_starter_mail_process" =>
                VerifyStarterMailProcess(
                    credit,
                    queueItems.LastOrDefault(),
                    before,
                    after),
            "authoritative_supporting_prerequisite_starter_quest_route_step" =>
                VerifyStarterQuestRoute(
                    credit,
                    queueItems.LastOrDefault(),
                    before,
                    after),
            "authoritative_supporting_prerequisite_starter_event_grant" =>
                VerifyRodAcquisition(credit, before, after),
            "authoritative_supporting_prerequisite_shop_endpoint_open" =>
                VerifyFishShopOpened(credit, before, after),
            "authoritative_supporting_prerequisite_terminal_purchase" =>
                VerifyRodAcquisition(credit, before, after),
            _ => Transition(
                credit,
                "unsupported_binding_kind",
                "unsupported",
                "unsupported",
                false)
        };
    }

    private static PolicyTeacherRequirementTransition VerifyAcquisition(
        CurrentCollectionRequirementCredit credit,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var evidence = ExactInventoryReceiptVerifier.Verify(
            before,
            after,
            credit.QualifiedItemId,
            credit.RequiredQuantity,
            credit.MinimumQuality);
        return Transition(
            credit,
            "exact_inventory_quantity_increased",
            CountText(evidence.BeforeQuantity),
            CountText(evidence.AfterQuantity),
            evidence.Verified);
    }

    private static PolicyTeacherRequirementTransition VerifyFullShipment(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem[] queueItems,
        QueueExecutionReceiptEnvelope? receipt,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var beforeShipped = ReadCollectionBoolean(
            before,
            "full_shipment_progress",
            "items",
            "qualified_item_id",
            credit.QualifiedItemId,
            "shipped");
        var afterShipped = ReadCollectionBoolean(
            after,
            "full_shipment_progress",
            "items",
            "qualified_item_id",
            credit.QualifiedItemId,
            "shipped");
        var beforePending = ShippingBinCount(before, credit.QualifiedItemId);
        var afterPending = ShippingBinCount(after, credit.QualifiedItemId);
        var beforeInventory = InventoryCount(before, credit.QualifiedItemId, 0);
        var afterInventory = InventoryCount(after, credit.QualifiedItemId, 0);
        var settled = beforeShipped == false && afterShipped == true;
        var terminalBefore = "not_required";
        var terminalAfter = "not_required";
        var deposit = !settled && receipt is not null
            ? NativeShippingDepositEvidenceVerifier.Verify(
                queueItems,
                receipt,
                before,
                after,
                credit.QualifiedItemId,
                credit.RequiredQuantity)
            : null;
        if (deposit is not null)
        {
            terminalBefore =
                $"pending={CountText(deposit.BeforeBinItemQuantity)};" +
                $"inventory={CountText(deposit.BeforeInventoryItemQuantity)};" +
                $"slot={CountText(deposit.BeforeInventorySlotQuantity)};" +
                $"value={deposit.BeforePendingSettlementValue?.ToString() ?? "unavailable"}";
            terminalAfter =
                $"pending={CountText(deposit.AfterBinItemQuantity)};" +
                $"inventory={CountText(deposit.AfterInventoryItemQuantity)};" +
                $"slot={CountText(deposit.AfterInventorySlotQuantity)};" +
                $"value={deposit.AfterPendingSettlementValue?.ToString() ?? "unavailable"}";
        }
        var terminalStepPending = deposit?.Verified == true;
        return Transition(
            credit,
            settled
                ? "native_full_shipment_false_to_true"
                : terminalStepPending
                    ? "exact_pending_native_shipment_terminal_step_increased"
                    : "exact_pending_native_shipment_increased",
            $"shipped={BooleanText(beforeShipped)};pending={CountText(beforePending)};inventory={CountText(beforeInventory)};terminal={terminalBefore}",
            $"shipped={BooleanText(afterShipped)};pending={CountText(afterPending)};inventory={CountText(afterInventory)};terminal={terminalAfter}",
            settled || terminalStepPending);
    }

    private static PolicyTeacherRequirementTransition VerifyCollectionBoolean(
        CurrentCollectionRequirementCredit credit,
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        string field,
        string rows,
        string identityProperty,
        string completionProperty,
        string transitionKind)
    {
        var beforeValue = ReadCollectionBoolean(
            before,
            field,
            rows,
            identityProperty,
            credit.QualifiedItemId,
            completionProperty);
        var afterValue = ReadCollectionBoolean(
            after,
            field,
            rows,
            identityProperty,
            credit.QualifiedItemId,
            completionProperty);
        return Transition(
            credit,
            transitionKind,
            beforeValue?.ToString() ?? "unavailable",
            afterValue?.ToString() ?? "unavailable",
            beforeValue == false && afterValue == true);
    }

    private static PolicyTeacherRequirementTransition VerifyCommunityCenterPayment(
        CurrentCollectionRequirementCredit credit,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var evidence = ExactCommunityCenterPaymentReceiptVerifier.Verify(
            before,
            after,
            credit.RequirementId,
            credit.AlternativeIndex,
            credit.RequiredQuantity);
        return Transition(
            credit,
            evidence.TransitionKind,
            $"money={CountText(evidence.BeforeMoney)};completed={BooleanText(evidence.BeforeNativeCompletion)}",
            $"money={CountText(evidence.AfterMoney)};completed={BooleanText(evidence.AfterNativeCompletion)}",
            evidence.Verified);
    }

    private static PolicyTeacherRequirementTransition VerifyCommunityCenterDonation(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem? queueItem,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var evidence = ExactCommunityCenterDonationReceiptVerifier.Verify(
            credit,
            queueItem,
            before,
            after);
        return Transition(
            credit,
            "native_community_center_donation_full_projection_transition",
            evidence.BeforeSummary,
            evidence.AfterSummary,
            evidence.Verified);
    }

    private static ActionQueueItem? CommunityCenterDonationQueueItem(
        CurrentCollectionRequirementCredit credit,
        IEnumerable<ActionQueueItem> queueItems)
    {
        const string prefix = "community_center:bundle:";
        var bundleKey = credit.RequirementId.StartsWith(
                prefix,
                StringComparison.Ordinal)
            ? credit.RequirementId[prefix.Length..]
            : string.Empty;
        var matches = queueItems.Where(item =>
                string.Equals(
                    item.OptionId,
                    "executor.donate_community_center_item",
                    StringComparison.Ordinal) &&
                CommunityCenterDonationParameterProtocol.TryParseExecution(
                    item.NormalizedCommand?.Parameters,
                    out var projection) &&
                projection.Binding.BundleDataKey == bundleKey &&
                projection.Binding.BundleIngredientIndex ==
                    credit.AlternativeIndex)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private static PolicyTeacherRequirementTransition VerifyRouteStep(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem? queueItem,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        if (queueItem is null)
        {
            return Transition(
                credit,
                "route_endpoint_evidence_missing",
                "unavailable",
                "unavailable",
                false);
        }
        var expectedLocation = ReadParameter(
            queueItem,
            "expected_target_location");
        var expectedX = ReadIntParameter(
            queueItem,
            "expected_arrival_tile_x");
        var expectedY = ReadIntParameter(
            queueItem,
            "expected_arrival_tile_y");
        var beforeLocation = RequiredStateString(
            before,
            "player",
            "location_id");
        var afterLocation = RequiredStateString(
            after,
            "player",
            "location_id");
        var beforeX = RequiredStateInt(before, "player", "tile_x");
        var beforeY = RequiredStateInt(before, "player", "tile_y");
        var afterX = RequiredStateInt(after, "player", "tile_x");
        var afterY = RequiredStateInt(after, "player", "tile_y");
        var changed = !string.Equals(beforeLocation, afterLocation,
                StringComparison.Ordinal) || beforeX != afterX || beforeY != afterY;
        var arrived = !string.IsNullOrWhiteSpace(expectedLocation) &&
            string.Equals(afterLocation, expectedLocation, StringComparison.Ordinal) &&
            (!expectedX.HasValue || afterX == expectedX.Value) &&
            (!expectedY.HasValue || afterY == expectedY.Value);
        return Transition(
            credit,
            "exact_route_endpoint_reached",
            $"{beforeLocation}:{beforeX},{beforeY}",
            $"{afterLocation}:{afterX},{afterY}",
            changed && arrived);
    }

    private static PolicyTeacherRequirementTransition VerifyFishShopOpened(
        CurrentCollectionRequirementCredit credit,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var beforeShop = ReadOpenShopId(before);
        var afterShop = ReadOpenShopId(after);
        return Transition(
            credit,
            "exact_fish_shop_menu_opened",
            beforeShop ?? "unavailable",
            afterShop ?? "unavailable",
            beforeShop != MasterAnglerRodEquipmentAcquisitionProtocol.ShopId &&
            afterShop == MasterAnglerRodEquipmentAcquisitionProtocol.ShopId);
    }

    private static PolicyTeacherRequirementTransition VerifyStarterMailRoute(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem? queueItem,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var transition = VerifyRouteStep(credit, queueItem, before, after);
        var exactMail = queueItem is not null &&
            ReadParameter(queueItem, "continuation.mail_id") ==
                MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailId;
        return Transition(
            credit,
            "native_starter_mail_route_endpoint_reached",
            transition.BeforeValue,
            transition.AfterValue,
            transition.Verified && exactMail);
    }

    private static PolicyTeacherRequirementTransition VerifyStarterMailApproach(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem? queueItem,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var expectedX = queueItem is null
            ? null
            : ReadIntParameter(queueItem, "target_tile_x");
        var expectedY = queueItem is null
            ? null
            : ReadIntParameter(queueItem, "target_tile_y");
        var beforeLocation = RequiredStateString(before, "player", "location_id");
        var afterLocation = RequiredStateString(after, "player", "location_id");
        var beforeX = RequiredStateInt(before, "player", "tile_x");
        var beforeY = RequiredStateInt(before, "player", "tile_y");
        var afterX = RequiredStateInt(after, "player", "tile_x");
        var afterY = RequiredStateInt(after, "player", "tile_y");
        var exactMail = queueItem is not null &&
            ReadParameter(queueItem, "continuation.mail_id") ==
                MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailId;
        var changed = beforeX != afterX || beforeY != afterY ||
            !string.Equals(beforeLocation, afterLocation, StringComparison.Ordinal);
        return Transition(
            credit,
            "native_starter_mailbox_stand_tile_reached",
            $"{beforeLocation}:{beforeX},{beforeY}",
            $"{afterLocation}:{afterX},{afterY}",
            exactMail && changed &&
            string.Equals(beforeLocation, afterLocation, StringComparison.Ordinal) &&
            expectedX.HasValue && afterX == expectedX.Value &&
            expectedY.HasValue && afterY == expectedY.Value);
    }

    private static PolicyTeacherRequirementTransition VerifyStarterMailOpen(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem? queueItem,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var candidateMail = queueItem is null
            ? string.Empty
            : ReadParameter(queueItem, "continuation.mail_id");
        var beforePending = ReadPendingMailId(before);
        var afterLetter = ReadLetterState(after);
        var afterQuest = HasActiveQuest(after,
            MasterAnglerRodEquipmentAcquisitionProtocol.StarterQuestId);
        return Transition(
            credit,
            "native_starter_mail_opened_and_quest_constructed",
            beforePending ?? "unavailable",
            $"letter_quest={afterLetter.QuestId};active_quest={afterQuest}",
            candidateMail == MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailId &&
            beforePending == MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailId &&
            (afterLetter.IsStarterLetter || afterQuest));
    }

    private static PolicyTeacherRequirementTransition VerifyStarterMailWait(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem? queueItem,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var beforeLetter = ReadLetterState(before);
        var afterLetter = ReadLetterState(after);
        var exactCandidate = queueItem is not null &&
            ReadParameter(queueItem, "target_runtime_identity") ==
                MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailId;
        return Transition(
            credit,
            "native_starter_letter_input_settled",
            $"starter={beforeLetter.IsStarterLetter};input={beforeLetter.CanReceiveInput};close={beforeLetter.ReadyToClose}",
            $"starter={afterLetter.IsStarterLetter};input={afterLetter.CanReceiveInput};close={afterLetter.ReadyToClose}",
            exactCandidate && beforeLetter.IsStarterLetter &&
            (!beforeLetter.CanReceiveInput || !beforeLetter.ReadyToClose) &&
            afterLetter.IsStarterLetter && afterLetter.CanReceiveInput &&
            afterLetter.ReadyToClose);
    }

    private static PolicyTeacherRequirementTransition VerifyStarterMailProcess(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem? queueItem,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var beforeLetter = ReadLetterState(before);
        var afterLetter = ReadLetterState(after);
        var afterQuest = HasActiveQuest(after,
            MasterAnglerRodEquipmentAcquisitionProtocol.StarterQuestId);
        var afterEvent = IsStarterEventActive(after);
        var afterRod = ReadRodCount(after, credit.QualifiedItemId);
        var exactCandidate = queueItem is not null &&
            ReadParameter(queueItem, "target_runtime_identity") ==
                MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailId;
        return Transition(
            credit,
            "native_starter_mail_processed_to_quest_13",
            $"starter_letter={beforeLetter.IsStarterLetter}",
            $"starter_letter={afterLetter.IsStarterLetter};active_quest={afterQuest};active_event={afterEvent};rod={CountText(afterRod)}",
            exactCandidate && beforeLetter.IsStarterLetter &&
            !afterLetter.IsStarterLetter &&
            (afterQuest || afterEvent || afterRod >= credit.RequiredQuantity));
    }

    private static PolicyTeacherRequirementTransition VerifyStarterQuestRoute(
        CurrentCollectionRequirementCredit credit,
        ActionQueueItem? queueItem,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var route = VerifyRouteStep(credit, queueItem, before, after);
        var exactQuest = queueItem is not null &&
            ReadParameter(queueItem, "quest_id") ==
                MasterAnglerRodEquipmentAcquisitionProtocol.StarterQuestId &&
            ReadParameter(queueItem, "quest_target_location") == "Beach";
        var nativeEventTookControl = IsStarterEventActive(after);
        var afterRod = ReadRodCount(after, credit.QualifiedItemId);
        return Transition(
            credit,
            "native_quest_13_route_progressed_toward_beach",
            route.BeforeValue,
            route.AfterValue +
                $";starter_event={nativeEventTookControl};rod={CountText(afterRod)}",
            exactQuest &&
                (route.Verified || nativeEventTookControl ||
                    afterRod >= credit.RequiredQuantity));
    }

    private static PolicyTeacherRequirementTransition VerifyRodAcquisition(
        CurrentCollectionRequirementCredit credit,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var beforeCount = ReadRodCount(before, credit.QualifiedItemId);
        var afterCount = ReadRodCount(after, credit.QualifiedItemId);
        return Transition(
            credit,
            "authoritative_rod_inventory_zero_to_owned",
            CountText(beforeCount),
            CountText(afterCount),
            beforeCount == 0 && afterCount >= credit.RequiredQuantity);
    }

    private static string? ReadOpenShopId(SnapshotEnvelope snapshot)
    {
        if (!TryStateValue(snapshot, "menus", "shop_stock", out var stock) ||
            stock.ValueKind != System.Text.Json.JsonValueKind.Object ||
            !stock.TryGetProperty("shop_id", out var shopId) ||
            shopId.ValueKind != System.Text.Json.JsonValueKind.String ||
            !stock.TryGetProperty("read_only", out var readOnly) ||
            readOnly.ValueKind != System.Text.Json.JsonValueKind.False)
        {
            return null;
        }
        return shopId.GetString();
    }

    private static int? ReadRodCount(
        SnapshotEnvelope snapshot,
        string qualifiedItemId)
    {
        if (!TryStateValue(snapshot, "fishing", "rod_inventory", out var rods) ||
            rods.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return null;
        }

        var count = 0;
        foreach (var row in rods.EnumerateArray())
        {
            if (row.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !row.TryGetProperty("qualified_item_id", out var itemId) ||
                itemId.ValueKind != System.Text.Json.JsonValueKind.String ||
                string.IsNullOrWhiteSpace(itemId.GetString()))
            {
                return null;
            }
            if (itemId.GetString() == qualifiedItemId)
                count++;
        }
        return count;
    }

    private static string? ReadPendingMailId(SnapshotEnvelope snapshot)
    {
        if (!TryStateValue(snapshot, "quests", "mailbox_processing", out var mailbox) ||
            mailbox.ValueKind != JsonValueKind.Object ||
            !mailbox.TryGetProperty("pending_mail_id", out var mailId) ||
            mailId.ValueKind != JsonValueKind.String)
        {
            return null;
        }
        return mailId.GetString();
    }

    private static (bool IsStarterLetter, string QuestId, bool CanReceiveInput,
        bool ReadyToClose) ReadLetterState(SnapshotEnvelope snapshot)
    {
        if (!TryStateValue(snapshot, "menus", "menu_specific_state", out var menu) ||
            menu.ValueKind != JsonValueKind.Object ||
            !menu.TryGetProperty("kind", out var kind) ||
            kind.ValueKind != JsonValueKind.String ||
            kind.GetString() != "letter_viewer")
        {
            return (false, string.Empty, false, false);
        }

        var questId = menu.TryGetProperty("quest_id", out var quest) &&
            quest.ValueKind == JsonValueKind.String
                ? quest.GetString() ?? string.Empty
                : string.Empty;
        var mailTitle = menu.TryGetProperty("mail_title", out var title) &&
            title.ValueKind == JsonValueKind.String
                ? title.GetString() ?? string.Empty
                : string.Empty;
        var canReceiveInput = menu.TryGetProperty("can_receive_input", out var input) &&
            input.ValueKind == JsonValueKind.True;
        var readyToClose = menu.TryGetProperty("ready_to_close", out var ready) &&
            ready.ValueKind == JsonValueKind.True;
        return (
            mailTitle == MasterAnglerRodEquipmentAcquisitionProtocol.StarterMailId,
            questId,
            canReceiveInput,
            readyToClose);
    }

    private static bool HasActiveQuest(SnapshotEnvelope snapshot, string questId) =>
        TryStateValue(snapshot, "quests", "active_quests", out var quests) &&
        quests.ValueKind == JsonValueKind.Array &&
        quests.EnumerateArray().Any(quest =>
            quest.ValueKind == JsonValueKind.Object &&
            quest.TryGetProperty("id", out var id) &&
            id.ValueKind == JsonValueKind.String &&
            id.GetString() == questId &&
            (!quest.TryGetProperty("completed", out var completed) ||
                completed.ValueKind != JsonValueKind.True));

    private static bool IsStarterEventActive(SnapshotEnvelope snapshot) =>
        TryStateValue(snapshot, "player", "story_event", out var story) &&
        story.ValueKind == JsonValueKind.Object &&
        story.TryGetProperty("active", out var active) &&
        active.ValueKind == JsonValueKind.True &&
        story.TryGetProperty("event_id", out var eventId) &&
        eventId.ValueKind == JsonValueKind.String &&
        eventId.GetString() ==
            MasterAnglerRodEquipmentAcquisitionProtocol.StarterEventId;

    private static PolicyTeacherRequirementTransition Transition(
        CurrentCollectionRequirementCredit credit,
        string transitionKind,
        string before,
        string after,
        bool verified)
        => new()
        {
            RequirementSetId = credit.RequirementSetId,
            RequirementId = credit.RequirementId,
            AlternativeIndex = credit.AlternativeIndex,
            QualifiedItemId = credit.QualifiedItemId,
            BindingKind = credit.BindingKind,
            TransitionKind = transitionKind,
            BeforeValue = before,
            AfterValue = after,
            Verified = verified
        };

    private static string CountText(int? value) =>
        value?.ToString() ?? "unavailable";

    private static string BooleanText(bool? value) =>
        value?.ToString() ?? "unavailable";
}
