using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] QuestCancellationCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent)
    {
        var fingerprint = CancellationIntent(intent, "quest_cancellation_fingerprint");
        var reason = CancellationIntent(intent, "quest_cancel_reason");
        if (string.IsNullOrWhiteSpace(fingerprint) || string.IsNullOrWhiteSpace(reason) ||
            CancellationIntent(intent, "confirm_quest_cancel") != "true")
            return Array.Empty<EventCandidate>();

        var projection = ReadStateFieldValue(snapshot, "quests", "cancellation_candidates");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(projection.Value, "invocation_policy") != "player_command_only" ||
            !projection.Value.TryGetProperty("candidates", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return Array.Empty<EventCandidate>();

        var matching = rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object &&
            string.Equals(ReadString(row, "cancellation_fingerprint"), fingerprint, StringComparison.Ordinal)).ToArray();
        if (matching.Length != 1 || !matching[0].TryGetProperty("quest", out var quest) ||
            quest.ValueKind != JsonValueKind.Object)
            return Array.Empty<EventCandidate>();

        var row = matching[0];
        var parameters = QuestCancellationCandidateParameters(projection.Value, row, quest, reason);
        var reasons = QuestCancellationStringArray(row, "blocked_diagnostics").ToList();
        if (ReadBool(row, "eligible") != true || ReadBool(row, "native_button_visible") != true)
            reasons.Add("quest_cancellation_native_button_unavailable");
        var activeMenu = ReadStateFieldValue(snapshot, "menus", "active_menu");
        if (activeMenu.HasValue && ReadBool(activeMenu.Value, "is_open") == true)
            reasons.Add("quest_cancellation_requires_clear_menu");
        reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "quest.cancel",
            Parameters = parameters,
            InvocationSource = OptionInvocationSource.PlayerCommand,
            ExplicitConfirmationGranted = true
        }));
        var blocking = reasons.Distinct(StringComparer.Ordinal).ToArray();
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "quest.cancel:" + fingerprint,
                Kind = "cancel_quest",
                Available = blocking.Length == 0,
                AllowedNow = blocking.Length == 0,
                AllowedToday = blocking.Length == 0,
                DisplayName = ReadString(quest, "title"),
                EstimatedTicks = 180,
                EnergyCost = 0,
                AvailabilityClass = "explicit_player_command_native_ordinary_quest_cancellation",
                ExpectedEffect = "quest_removed=true;accepted=false;accepted_daily_quest=" +
                    (ReadBool(row, "expected_accepted_daily_quest_after") == true ? "true" : "false"),
                BlockReasons = blocking,
                Parameters = parameters
            }
        };
    }

    private static SmallModelActionParameter[] QuestCancellationCandidateParameters(
        JsonElement projection,
        JsonElement row,
        JsonElement quest,
        string reason) => new[]
    {
        Parameter("quest_candidate_id", "quest_cancel:" + ReadString(row, "cancellation_fingerprint")),
        Parameter("quest_family", "ordinary"),
        Parameter("quest_id", ReadString(quest, "id")),
        Parameter("quest_runtime_type", ReadString(quest, "runtime_type")),
        Parameter("quest_cancellation_fingerprint", ReadString(row, "cancellation_fingerprint")),
        Parameter("quest_cancel_reason", reason),
        Parameter("confirm_quest_cancel", "true"),
        Parameter("quest_expected_accepted_before", (ReadBool(quest, "accepted") == true).ToString().ToLowerInvariant()),
        Parameter("quest_expected_completed_before", (ReadBool(quest, "completed") == true).ToString().ToLowerInvariant()),
        Parameter("quest_expected_daily_quest", (ReadBool(quest, "daily_quest") == true).ToString().ToLowerInvariant()),
        Parameter("quest_expected_day_accepted", ReadInt(quest, "day_quest_accepted").ToString(CultureInfo.InvariantCulture)),
        Parameter("quest_expected_days_left", ReadInt(quest, "days_left").ToString(CultureInfo.InvariantCulture)),
        Parameter("quest_log_count_before", ReadInt(projection, "quest_log_count_before").ToString(CultureInfo.InvariantCulture)),
        Parameter("quest_log_count_after", ReadInt(row, "expected_quest_log_count_after").ToString(CultureInfo.InvariantCulture)),
        Parameter("quest_accepted_daily_before", (ReadBool(projection, "accepted_daily_quest_before") == true).ToString().ToLowerInvariant()),
        Parameter("quest_accepted_daily_after", (ReadBool(row, "expected_accepted_daily_quest_after") == true).ToString().ToLowerInvariant()),
        Parameter("quest_resets_accepted_daily_quest", (ReadBool(row, "resets_accepted_daily_quest") == true).ToString().ToLowerInvariant()),
        Parameter("native_contract", ReadString(projection, "native_contract"))
    };

    private static string CancellationIntent(SmallModelActionParameter[] intent, string name) =>
        intent.FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))?.Value ?? string.Empty;

    private static string[] QuestCancellationStringArray(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty).Where(item => item.Length > 0).ToArray()
            : Array.Empty<string>();
}
