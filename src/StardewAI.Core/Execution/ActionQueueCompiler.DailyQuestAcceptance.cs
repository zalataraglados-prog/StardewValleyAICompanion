using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static CompiledActionStep[] CompileAcceptDailyQuestStep(SmallModelAction action)
    {
        var fingerprint = ReadParameter(action, "quest_offer_fingerprint");
        var runtimeType = ReadParameter(action, "quest_runtime_type");
        if (string.IsNullOrWhiteSpace(fingerprint) || string.IsNullOrWhiteSpace(runtimeType))
        {
            return Array.Empty<CompiledActionStep>();
        }

        return new[]
        {
            Step(
                "accept_daily_quest",
                "Billboard:offer=" + fingerprint,
                "native_daily_quest_in_actor_quest_log_and_acceptedDailyQuest=true_and_daysLeft=2",
                60)
        };
    }

    private static string[] ValidateAcceptDailyQuestPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.accept_daily_quest")
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        var fingerprint = ReadParameter(action, "quest_offer_fingerprint");
        var runtimeType = ReadParameter(action, "quest_runtime_type");
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            reasons.Add("daily_quest_offer_fingerprint_required");
        }
        if (string.IsNullOrWhiteSpace(runtimeType))
        {
            reasons.Add("daily_quest_runtime_type_required");
        }
        if (!ActionSeesActiveMenuOpen(action, snapshot) ||
            !string.Equals(
                ReadParameter(action, "compiler_context.active_menu_type_before_step"),
                "Billboard",
                StringComparison.Ordinal))
        {
            reasons.Add("daily_quest_billboard_menu_not_open");
        }

        var state = ReadStateFieldValue(snapshot, "quests", "daily_quest_offer");
        if (!state.HasValue || state.Value.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("daily_quest_offer_transparent_state_missing");
        }
        else
        {
            var offer = state.Value;
            if (ReadBool(offer, "can_accept") != true)
            {
                reasons.Add("daily_quest_native_can_accept_false");
            }
            if (!string.Equals(ReadString(offer, "offer_fingerprint"), fingerprint, StringComparison.Ordinal))
            {
                reasons.Add("daily_quest_offer_fingerprint_drifted");
            }
            if (!offer.TryGetProperty("quest", out var quest) || quest.ValueKind != JsonValueKind.Object ||
                !string.Equals(ReadString(quest, "runtime_type"), runtimeType, StringComparison.Ordinal))
            {
                reasons.Add("daily_quest_runtime_type_drifted");
            }
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
