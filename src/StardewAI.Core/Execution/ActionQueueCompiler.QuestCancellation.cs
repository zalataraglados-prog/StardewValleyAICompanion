using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string QuestCancellationCompilerNativeContract =
        "QuestLog_row_receiveLeftClick->cancelQuestButton_receiveLeftClick->accepted_false->questLog_remove->same_day_daily_acceptedDailyQuest_false";

    private static readonly string[] QuestCancellationBoundNames =
    {
        "quest_candidate_id", "quest_family", "quest_id", "quest_runtime_type",
        "quest_cancellation_fingerprint", "quest_expected_accepted_before",
        "quest_expected_completed_before", "quest_expected_daily_quest", "quest_expected_day_accepted",
        "quest_expected_days_left", "quest_log_count_before", "quest_log_count_after",
        "quest_accepted_daily_before", "quest_accepted_daily_after", "quest_resets_accepted_daily_quest",
        "native_contract"
    };

    private static SmallModelActionParameter[] BuildQuestCancellationParameters(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var requestedFingerprint = ReadParameter(action, "quest_cancellation_fingerprint");
        var parameters = action.Parameters
            .Where(parameter => !QuestCancellationBoundNames.Contains(parameter.Name, StringComparer.Ordinal)).ToList();
        var projection = ReadStateFieldValue(snapshot, "quests", "cancellation_candidates");
        if (string.IsNullOrWhiteSpace(requestedFingerprint) || !projection.HasValue ||
            projection.Value.ValueKind != JsonValueKind.Object ||
            !projection.Value.TryGetProperty("candidates", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return parameters.ToArray();
        var matching = rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(row, "cancellation_fingerprint"), requestedFingerprint, StringComparison.Ordinal)).ToArray();
        if (matching.Length != 1 || !matching[0].TryGetProperty("quest", out var quest) ||
            quest.ValueKind != JsonValueKind.Object)
            return parameters.ToArray();
        var row = matching[0];
        parameters.AddRange(new[]
        {
            Parameter("quest_candidate_id", "quest_cancel:" + requestedFingerprint),
            Parameter("quest_family", "ordinary"),
            Parameter("quest_id", ReadString(quest, "id")),
            Parameter("quest_runtime_type", ReadString(quest, "runtime_type")),
            Parameter("quest_cancellation_fingerprint", requestedFingerprint),
            Parameter("quest_expected_accepted_before", (ReadBool(quest, "accepted") == true).ToString().ToLowerInvariant()),
            Parameter("quest_expected_completed_before", (ReadBool(quest, "completed") == true).ToString().ToLowerInvariant()),
            Parameter("quest_expected_daily_quest", (ReadBool(quest, "daily_quest") == true).ToString().ToLowerInvariant()),
            Parameter("quest_expected_day_accepted", ReadInt(quest, "day_quest_accepted").ToString(CultureInfo.InvariantCulture)),
            Parameter("quest_expected_days_left", ReadInt(quest, "days_left").ToString(CultureInfo.InvariantCulture)),
            Parameter("quest_log_count_before", ReadInt(projection.Value, "quest_log_count_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("quest_log_count_after", ReadInt(row, "expected_quest_log_count_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("quest_accepted_daily_before", (ReadBool(projection.Value, "accepted_daily_quest_before") == true).ToString().ToLowerInvariant()),
            Parameter("quest_accepted_daily_after", (ReadBool(row, "expected_accepted_daily_quest_after") == true).ToString().ToLowerInvariant()),
            Parameter("quest_resets_accepted_daily_quest", (ReadBool(row, "resets_accepted_daily_quest") == true).ToString().ToLowerInvariant()),
            Parameter("native_contract", ReadString(projection.Value, "native_contract"))
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompileQuestCancellationStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundQuestCancellationAction(action, snapshot);
        var fingerprint = ReadParameter(bound, "quest_cancellation_fingerprint");
        if (string.IsNullOrWhiteSpace(fingerprint)) return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step(
                "cancel_quest",
                "QuestLog:cancellation=" + fingerprint,
                "quest_removed=true;accepted=false;accepted_daily_quest=" + ReadParameter(bound, "quest_accepted_daily_after"),
                300)
        };
    }

    private static string[] ValidateQuestCancellationPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId is not ("quest.cancel" or "executor.cancel_quest")) return Array.Empty<string>();
        var reasons = new List<string>();
        if (string.IsNullOrWhiteSpace(ReadParameter(action, "quest_cancel_reason")) ||
            ReadParameter(action, "confirm_quest_cancel") != "true")
            reasons.Add("quest_cancellation_explicit_reason_and_confirmation_required");
        if (ActionSeesActiveMenuOpen(action, snapshot)) reasons.Add("quest_cancellation_requires_clear_menu");
        var projection = ReadStateFieldValue(snapshot, "quests", "cancellation_candidates");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("quest_cancellation_projection_unavailable").ToArray();
        if (ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(projection.Value, "invocation_policy") != "player_command_only" ||
            ReadString(projection.Value, "native_contract") != QuestCancellationCompilerNativeContract)
            reasons.Add("quest_cancellation_complete_locked_player_command_projection_required");
        var bound = BoundQuestCancellationAction(action, snapshot);
        var fingerprint = ReadParameter(bound, "quest_cancellation_fingerprint");
        if (!projection.Value.TryGetProperty("candidates", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return reasons.Append("quest_cancellation_candidate_rows_missing").ToArray();
        var matching = rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(row, "cancellation_fingerprint"), fingerprint, StringComparison.Ordinal)).ToArray();
        if (matching.Length != 1 || !matching[0].TryGetProperty("quest", out var quest) || quest.ValueKind != JsonValueKind.Object)
            return reasons.Append("quest_cancellation_exact_identity_missing_or_ambiguous").Distinct(StringComparer.Ordinal).ToArray();
        var row = matching[0];
        if (ReadBool(row, "eligible") != true || ReadBool(row, "native_button_visible") != true ||
            ReadString(row, "status") != "ready")
            reasons.Add("quest_cancellation_native_quest_not_eligible");
        var exact = ReadParameter(bound, "quest_family") == "ordinary" &&
            ReadParameter(bound, "quest_id") == ReadString(quest, "id") &&
            ReadParameter(bound, "quest_runtime_type") == ReadString(quest, "runtime_type") &&
            ReadBoolParameter(bound, "quest_expected_accepted_before") == ReadBool(quest, "accepted") &&
            ReadBoolParameter(bound, "quest_expected_completed_before") == ReadBool(quest, "completed") &&
            ReadBoolParameter(bound, "quest_expected_daily_quest") == ReadBool(quest, "daily_quest") &&
            ReadIntParameter(bound, "quest_expected_day_accepted") == ReadInt(quest, "day_quest_accepted") &&
            ReadIntParameter(bound, "quest_expected_days_left") == ReadInt(quest, "days_left") &&
            ReadIntParameter(bound, "quest_log_count_before") == ReadInt(projection.Value, "quest_log_count_before") &&
            ReadIntParameter(bound, "quest_log_count_after") == ReadInt(row, "expected_quest_log_count_after") &&
            ReadBoolParameter(bound, "quest_accepted_daily_before") == ReadBool(projection.Value, "accepted_daily_quest_before") &&
            ReadBoolParameter(bound, "quest_accepted_daily_after") == ReadBool(row, "expected_accepted_daily_quest_after") &&
            ReadBoolParameter(bound, "quest_resets_accepted_daily_quest") == ReadBool(row, "resets_accepted_daily_quest") &&
            ReadParameter(bound, "native_contract") == QuestCancellationCompilerNativeContract;
        if (!exact) reasons.Add("quest_cancellation_complete_fresh_typed_binding_required");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundQuestCancellationAction(SmallModelAction action, SnapshotEnvelope snapshot) => new()
    {
        ActionId = action.ActionId,
        OptionId = action.OptionId,
        Rationale = action.Rationale,
        Parameters = BuildQuestCancellationParameters(action, snapshot)
    };
}
