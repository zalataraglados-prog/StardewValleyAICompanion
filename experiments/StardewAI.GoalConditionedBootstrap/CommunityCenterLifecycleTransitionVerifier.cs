using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static class CommunityCenterLifecycleTransitionVerifier
{
    public const string InitialUnlock = "initial_unlock_event";
    public const string FirstJunimoNote = "first_junimo_note_interaction";
    public const string JunimoTextUnlock = "junimo_text_unlock_event";
    public const string RoomMailSettlement = "room_mail_day_settlement";
    public const string FinalCeremony = "final_ceremony_event";

    public static CommunityCenterLifecycleTransitionEvidence Verify(
        string transitionKind,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var reasons = new List<string>();
        var beforeProgress = ReadProgress(before);
        var afterProgress = ReadProgress(after);
        var beforeLifecycle = ReadLifecycle(beforeProgress);
        var afterLifecycle = ReadLifecycle(afterProgress);
        if (!beforeProgress.HasValue || !afterProgress.HasValue ||
            !beforeLifecycle.HasValue || !afterLifecycle.HasValue)
        {
            reasons.Add("community_center_lifecycle_projection_unavailable");
        }
        if (ReadString(beforeLifecycle, "projection_status") !=
                "complete_locked_base_1.6.15" ||
            ReadString(afterLifecycle, "projection_status") !=
                "complete_locked_base_1.6.15")
        {
            reasons.Add("community_center_lifecycle_asset_lock_missing");
        }

        var expectedEventId = transitionKind switch
        {
            InitialUnlock => "611439",
            FirstJunimoNote => string.Empty,
            JunimoTextUnlock => "112",
            FinalCeremony => "191393",
            RoomMailSettlement => string.Empty,
            _ => string.Empty
        };
        var beforeSummary = string.Empty;
        var afterSummary = string.Empty;
        switch (transitionKind)
        {
            case InitialUnlock:
                VerifyEventAssetTransition(
                    beforeLifecycle,
                    afterLifecycle,
                    "initial_unlock_event",
                    expectedEventId,
                    reasons);
                var beforeDoor = ReadBool(
                    beforeLifecycle,
                    "door_unlock_received");
                var afterDoor = ReadBool(
                    afterLifecycle,
                    "door_unlock_received");
                if (beforeDoor != false || afterDoor != true)
                    reasons.Add("community_center_door_unlock_transition_missing");
                beforeSummary = $"event_seen=false;door_unlock={ValueText(beforeDoor)}";
                afterSummary = $"event_seen=true;door_unlock={ValueText(afterDoor)}";
                break;
            case FirstJunimoNote:
                var beforeSeen = ReadBool(
                    beforeLifecycle,
                    "first_junimo_note_seen");
                var afterSeen = ReadBool(
                    afterLifecycle,
                    "first_junimo_note_seen");
                var beforeWizardPending = ReadBool(
                    beforeLifecycle,
                    "wizard_letter_pending");
                var afterWizardPending = ReadBool(
                    afterLifecycle,
                    "wizard_letter_pending");
                var afterWizardReceived = ReadBool(
                    afterLifecycle,
                    "wizard_letter_received");
                if (beforeSeen != false || afterSeen != true)
                    reasons.Add("community_center_first_junimo_note_transition_missing");
                if (beforeWizardPending == true ||
                    (afterWizardPending != true && afterWizardReceived != true))
                {
                    reasons.Add("community_center_wizard_letter_schedule_transition_missing");
                }
                beforeSummary =
                    $"first_note_seen={ValueText(beforeSeen)};wizard_pending={ValueText(beforeWizardPending)}";
                afterSummary =
                    $"first_note_seen={ValueText(afterSeen)};wizard_pending={ValueText(afterWizardPending)};wizard_received={ValueText(afterWizardReceived)}";
                break;
            case JunimoTextUnlock:
                VerifyEventAssetTransition(
                    beforeLifecycle,
                    afterLifecycle,
                    "junimo_text_event",
                    expectedEventId,
                    reasons);
                var beforeReadable = ReadBool(
                    beforeLifecycle,
                    "can_read_junimo_text_received");
                var afterReadable = ReadBool(
                    afterLifecycle,
                    "can_read_junimo_text_received");
                if (beforeReadable != false || afterReadable != true)
                    reasons.Add("community_center_junimo_text_transition_missing");
                beforeSummary = $"event_seen=false;can_read={ValueText(beforeReadable)}";
                afterSummary = $"event_seen=true;can_read={ValueText(afterReadable)}";
                break;
            case RoomMailSettlement:
                var beforePending = ReadStringArray(
                    beforeProgress,
                    "pending_area_mail_flags");
                var afterPending = ReadStringArray(
                    afterProgress,
                    "pending_area_mail_flags");
                var afterReceived = ReadStringArray(
                    afterProgress,
                    "completed_area_mail_flags");
                if (beforePending.Length == 0 ||
                    beforePending.Any(flag =>
                        !afterReceived.Contains(flag, StringComparer.Ordinal) ||
                        afterPending.Contains(flag, StringComparer.Ordinal)))
                {
                    reasons.Add("community_center_room_mail_settlement_missing");
                }
                var beforeAllAreas = ReadBool(
                    beforeLifecycle,
                    "all_areas_complete");
                var beforeNative = ReadBool(
                    beforeProgress,
                    "community_center_complete_native");
                var afterNative = ReadBool(
                    afterProgress,
                    "community_center_complete_native");
                if (beforeAllAreas == true &&
                    (beforeNative != false ||
                        afterNative != true ||
                        ReadBool(
                            afterLifecycle,
                            "all_area_completion_mails_received") != true))
                {
                    reasons.Add("community_center_native_completion_settlement_missing");
                }
                beforeSummary =
                    $"pending={JsonSerializer.Serialize(beforePending)};native={ValueText(beforeNative)}";
                afterSummary =
                    $"pending={JsonSerializer.Serialize(afterPending)};received={JsonSerializer.Serialize(afterReceived)};native={ValueText(afterNative)}";
                break;
            case FinalCeremony:
                VerifyEventAssetTransition(
                    beforeLifecycle,
                    afterLifecycle,
                    "final_ceremony_event",
                    expectedEventId,
                    reasons);
                var beforeNativeComplete = ReadBool(
                    beforeProgress,
                    "community_center_complete_native");
                var afterAccessible = ReadBool(
                    afterProgress,
                    "location_accessible");
                var beforeAdmitted = ReadBool(
                    beforeLifecycle,
                    "completion_admitted");
                var afterAdmitted = ReadBool(
                    afterLifecycle,
                    "completion_admitted");
                if (beforeNativeComplete != true)
                    reasons.Add("community_center_final_ceremony_native_completion_missing");
                if (afterAccessible != true ||
                    beforeAdmitted != false ||
                    afterAdmitted != true)
                {
                    reasons.Add("community_center_final_completion_admission_missing");
                }
                beforeSummary =
                    $"native={ValueText(beforeNativeComplete)};admitted={ValueText(beforeAdmitted)}";
                afterSummary =
                    $"accessible={ValueText(afterAccessible)};admitted={ValueText(afterAdmitted)}";
                break;
            default:
                reasons.Add("community_center_lifecycle_transition_kind_unknown");
                break;
        }

        var distinct = reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return new CommunityCenterLifecycleTransitionEvidence
        {
            TransitionKind = transitionKind,
            ExpectedEventId = expectedEventId,
            BeforeStage = ReadString(beforeLifecycle, "stage"),
            AfterStage = ReadString(afterLifecycle, "stage"),
            BeforeSummary = beforeSummary,
            AfterSummary = afterSummary,
            Verified = distinct.Length == 0,
            BlockingReasons = distinct
        };
    }

    private static void VerifyEventAssetTransition(
        JsonElement? beforeLifecycle,
        JsonElement? afterLifecycle,
        string property,
        string eventId,
        ICollection<string> reasons)
    {
        var beforeEvent = ReadObject(beforeLifecycle, property);
        var afterEvent = ReadObject(afterLifecycle, property);
        if (ReadString(beforeEvent, "event_id") != eventId ||
            ReadString(afterEvent, "event_id") != eventId ||
            ReadBool(beforeEvent, "asset_locked") != true ||
            ReadBool(afterEvent, "asset_locked") != true)
        {
            reasons.Add("community_center_lifecycle_event_asset_not_locked:" +
                eventId);
        }
        if (ReadBool(beforeEvent, "event_seen") != false ||
            ReadBool(afterEvent, "event_seen") != true)
        {
            reasons.Add("community_center_lifecycle_event_seen_transition_missing:" +
                eventId);
        }
    }

    private static JsonElement? ReadProgress(SnapshotEnvelope snapshot) =>
        TryStateValue(snapshot, "world_progress", "community_center", out var value) &&
        value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    private static JsonElement? ReadLifecycle(JsonElement? progress) =>
        ReadObject(progress, "lifecycle");

    private static JsonElement? ReadObject(
        JsonElement? row,
        string property) =>
        row.HasValue &&
        row.Value.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Object
            ? value
            : null;

    private static string[] ReadStringArray(
        JsonElement? row,
        string property) =>
        row.HasValue &&
        row.Value.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => item.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();

    private static string ReadString(JsonElement? row, string property) =>
        row.HasValue &&
        row.Value.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static bool? ReadBool(JsonElement? row, string property) =>
        row.HasValue &&
        row.Value.TryGetProperty(property, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

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

    private static string ValueText(bool? value) =>
        value?.ToString() ?? "unavailable";
}
