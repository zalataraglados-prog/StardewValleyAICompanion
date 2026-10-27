using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static class MasterAnglerRodEquipmentAcquisitionProtocol
{
    public const string RequirementId =
        "dependency.complete_master_angler.rod_equipment_acquisition";
    public const string OptionId = "economy.buy_supplies";
    public const string StarterMailOptionId = "mail.process_letter";
    public const string StarterQuestOptionId = "quest.advance";
    public const string StarterEventOptionId = "story.advance_event";
    public const string StarterMailId = "spring_2_1";
    public const string StarterQuestId = "13";
    public const string StarterEventId = "739330";
    public const string QualifiedItemId = "(T)BambooPole";
    public const string ShopId = "FishShop";
    public const int RequiredQuantity = 1;
    public const int MaximumUnitPrice = 500;

    public static string ObserveRodInventory(SnapshotEnvelope snapshot)
    {
        if (!snapshot.State.TryGetValue("fishing", out var fishing) ||
            fishing.ValueKind != JsonValueKind.Object ||
            !fishing.TryGetProperty("rod_inventory", out var envelope) ||
            envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() is not ("available" or "derived") ||
            !envelope.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return "unavailable";
        }

        var rows = value.EnumerateArray().ToArray();
        if (rows.Any(row =>
                row.ValueKind != JsonValueKind.Object ||
                !row.TryGetProperty("qualified_item_id", out var itemId) ||
                itemId.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(itemId.GetString())))
        {
            return "unavailable";
        }

        return rows.Length == 0 ? "missing" : "satisfied";
    }

    public static bool TryMatchCandidate(
        PolicyEventCandidatePrediction candidate,
        out string bindingKind)
    {
        var match = MatchCandidate(candidate);
        bindingKind = match?.BindingKind ?? string.Empty;
        return match is not null;
    }

    public static RodPrerequisiteCandidateMatch? MatchCandidate(
        PolicyEventCandidatePrediction candidate)
    {
        var nativeStarter = MatchNativeStarterCandidate(candidate);
        if (nativeStarter is not null)
        {
            return nativeStarter;
        }

        var bindingKind = candidate.Kind switch
        {
            "route_connector_tile" =>
                "authoritative_supporting_prerequisite_rolling_route_step",
            "interact_endpoint" =>
                "authoritative_supporting_prerequisite_shop_endpoint_open",
            "buy_shop_item" =>
                "authoritative_supporting_prerequisite_terminal_purchase",
            _ => string.Empty
        };
        return bindingKind.Length > 0 &&
            candidate.OptionId == OptionId &&
            candidate.QualifiedItemId == QualifiedItemId &&
            candidate.ItemId == "BambooPole" &&
            candidate.ShopId == ShopId &&
            candidate.Quantity == RequiredQuantity &&
            candidate.UnitPrice is > 0 and <= MaximumUnitPrice &&
            AuditedShopPurchasePolicy.IsSideEffectFreeNonObjectPurchase(
                candidate.QualifiedItemId) &&
            HasParameter(candidate, "continuation.option_id", OptionId) &&
            HasParameter(candidate, "continuation.shop_id", ShopId) &&
            HasParameter(
                candidate,
                "continuation.qualified_item_id",
                QualifiedItemId) &&
            HasParameter(candidate, "continuation.stock_id", QualifiedItemId) &&
            HasIntParameter(
                candidate,
                "continuation.output_stack_per_purchase",
                RequiredQuantity) &&
            HasIntParameter(
                candidate,
                "continuation.quantity",
                RequiredQuantity) &&
            TryReadIntParameter(
                candidate,
                "continuation.max_unit_price",
                out var maximumUnitPrice) &&
            maximumUnitPrice is > 0 and <= MaximumUnitPrice &&
            candidate.UnitPrice <= maximumUnitPrice
                ? new RodPrerequisiteCandidateMatch(
                    bindingKind,
                    100,
                    true,
                    ShopId,
                    maximumUnitPrice,
                    "authoritative_empty_rod_inventory+exact_audited_fish_shop_purchase_identity")
                : null;
    }

    public static bool PurchaseFallbackAllowed(SnapshotEnvelope snapshot)
    {
        if (!TryStateValue(snapshot, "time", "total_days", out var totalDays) ||
            !totalDays.TryGetInt32(out var dayIndex) ||
            dayIndex < 1)
        {
            return false;
        }

        return !StarterMailOutstanding(snapshot) &&
            !StarterQuestOutstanding(snapshot) &&
            !StarterEventOutstanding(snapshot);
    }

    private static RodPrerequisiteCandidateMatch? MatchNativeStarterCandidate(
        PolicyEventCandidatePrediction candidate)
    {
        if (candidate.OptionId == StarterMailOptionId)
        {
            var exactMail = candidate.Kind is
                    "route_connector_tile" or
                    "mailbox_approach" or
                    "open_mailbox_letter" &&
                HasParameter(candidate, "continuation.mail_id", StarterMailId) &&
                HasParameter(candidate, "target_runtime_identity", StarterMailId);
            var exactOpenLetter = candidate.Kind is
                    "mail_input_wait" or
                    "process_open_letter" &&
                HasParameter(candidate, "target_runtime_type", "LetterViewerMenu") &&
                HasParameter(candidate, "target_runtime_identity", StarterMailId) &&
                HasParameter(
                    candidate,
                    "continuation.option_id",
                    StarterMailOptionId);
            if (exactMail || exactOpenLetter)
            {
                var bindingKind = candidate.Kind switch
                {
                    "route_connector_tile" =>
                        "authoritative_supporting_prerequisite_starter_mail_route_step",
                    "mailbox_approach" =>
                        "authoritative_supporting_prerequisite_starter_mail_approach",
                    "open_mailbox_letter" =>
                        "authoritative_supporting_prerequisite_starter_mail_open",
                    "mail_input_wait" =>
                        "authoritative_supporting_prerequisite_starter_mail_wait",
                    "process_open_letter" =>
                        "authoritative_supporting_prerequisite_starter_mail_process",
                    _ => string.Empty
                };
                if (bindingKind.Length > 0)
                {
                    return new RodPrerequisiteCandidateMatch(
                        bindingKind,
                        10,
                        false,
                        string.Empty,
                        0,
                        "authoritative_empty_rod_inventory+native_starter_mail_spring_2_1");
                }
            }
        }

        if (candidate.OptionId == StarterQuestOptionId &&
            candidate.Kind == "route_connector_tile" &&
            HasParameter(candidate, "quest_id", StarterQuestId) &&
            HasParameter(candidate, "quest_runtime_type", "Quest") &&
            HasParameter(
                candidate,
                "quest_next_action",
                "trigger_location_event") &&
            HasParameter(candidate, "quest_target_location", "Beach") &&
            HasParameter(candidate, "quest_event_id", StarterEventId) &&
            HasParameter(
                candidate,
                "quest_event_required_mail_id",
                StarterMailId) &&
            HasParameter(
                candidate,
                "quest_event_source_asset",
                "Data/Events/Beach") &&
            HasParameter(
                candidate,
                "quest_event_source_key",
                "739330/t 600 1710/*n spring_2_1"))
        {
            return new RodPrerequisiteCandidateMatch(
                "authoritative_supporting_prerequisite_starter_quest_route_step",
                20,
                false,
                string.Empty,
                0,
                "authoritative_empty_rod_inventory+native_starter_quest_13_beach_route");
        }

        if (candidate.OptionId == StarterEventOptionId &&
            candidate.Kind is
                "advance_story_event_automatic" or
                "advance_story_event_choice" &&
            HasParameter(candidate, "story_event_id", StarterEventId) &&
            HasParameter(
                candidate,
                "continuation.story_event_id",
                StarterEventId))
        {
            return new RodPrerequisiteCandidateMatch(
                "authoritative_supporting_prerequisite_starter_event_grant",
                30,
                false,
                string.Empty,
                0,
                "authoritative_empty_rod_inventory+native_story_event_739330_grant");
        }

        return null;
    }

    private static bool StarterMailOutstanding(SnapshotEnvelope snapshot)
    {
        if (!TryStateValue(
                snapshot,
                "quests",
                "mailbox_processing",
                out var mailbox) ||
            mailbox.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (mailbox.TryGetProperty("pending_mail_id", out var pending) &&
            pending.ValueKind == JsonValueKind.String &&
            pending.GetString() == StarterMailId)
        {
            return true;
        }

        return mailbox.TryGetProperty(
                "queue_mail_ids_native_order",
                out var queue) &&
            queue.ValueKind == JsonValueKind.Array &&
            queue.EnumerateArray().Any(value =>
                value.ValueKind == JsonValueKind.String &&
                value.GetString() == StarterMailId);
    }

    private static bool StarterQuestOutstanding(SnapshotEnvelope snapshot) =>
        TryStateValue(snapshot, "quests", "active_quests", out var quests) &&
        quests.ValueKind == JsonValueKind.Array &&
        quests.EnumerateArray().Any(quest =>
            quest.ValueKind == JsonValueKind.Object &&
            quest.TryGetProperty("id", out var id) &&
            id.ValueKind == JsonValueKind.String &&
            id.GetString() == StarterQuestId &&
            (!quest.TryGetProperty("completed", out var completed) ||
                completed.ValueKind != JsonValueKind.True));

    private static bool StarterEventOutstanding(SnapshotEnvelope snapshot)
    {
        if (!TryStateValue(snapshot, "player", "story_event", out var story) ||
            story.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return story.TryGetProperty("active", out var active) &&
            active.ValueKind == JsonValueKind.True &&
            story.TryGetProperty("event_id", out var eventId) &&
            eventId.ValueKind == JsonValueKind.String &&
            eventId.GetString() == StarterEventId;
    }

    private static bool TryStateValue(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out JsonElement value)
    {
        value = default;
        return snapshot.State.TryGetValue(section, out var sectionValue) &&
            sectionValue.ValueKind == JsonValueKind.Object &&
            sectionValue.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is "available" or "derived" &&
            envelope.TryGetProperty("value", out value);
    }

    private static bool HasParameter(
        PolicyEventCandidatePrediction candidate,
        string name,
        string expected) =>
        CurrentTeacherFrontierSupport.TryReadUniqueParameter(
            candidate,
            name,
            out var value) &&
        value == expected;

    private static bool HasIntParameter(
        PolicyEventCandidatePrediction candidate,
        string name,
        int expected) =>
        TryReadIntParameter(candidate, name, out var value) && value == expected;

    private static bool TryReadIntParameter(
        PolicyEventCandidatePrediction candidate,
        string name,
        out int value) =>
        CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
            candidate,
            name,
            out value);

    public sealed record RodPrerequisiteCandidateMatch(
        string BindingKind,
        int Priority,
        bool IsPurchaseFallback,
        string ShopId,
        int MaximumUnitPrice,
        string IdentityEvidence);
}
