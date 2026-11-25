using System;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

public static class NativeNpcQuestionRecovery
{
    public const string CandidateKind = "recovery_answer_npc_question";

    public static NativeNpcQuestionEvidence? Read(SnapshotEnvelope snapshot)
    {
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "menus", "active_menu")) ||
            !ReadableStatus(ReadStateFieldStatus(snapshot, "menus", "menu_specific_state"))) return null;
        var menu = ReadStateFieldValue(snapshot, "menus", "active_menu");
        var detail = ReadStateFieldValue(snapshot, "menus", "menu_specific_state");
        if (!menu.HasValue || menu.Value.ValueKind != JsonValueKind.Object ||
            ReadString(menu.Value, "type") != "DialogueBox" || ReadBool(menu.Value, "is_open") != true ||
            ReadBool(menu.Value, "event_up") != false || ReadBool(menu.Value, "dialogue_is_question") != true ||
            ReadBool(menu.Value, "dialogue_transitioning") != false ||
            !menu.Value.TryGetProperty("dialogue_safety_timer", out var timer) || !timer.TryGetInt32(out var t) || t > 0 ||
            !string.IsNullOrWhiteSpace(ReadString(menu.Value, "last_question_key")) ||
            !detail.HasValue || !detail.Value.TryGetProperty("npc_question", out var question)) return null;
        try
        {
            var evidence = JsonSerializer.Deserialize<NativeNpcQuestionEvidence>(question.GetRawText());
            return evidence?.BestAnswer() is not null &&
                evidence.Speaker == ReadString(menu.Value, "dialogue_speaker_name") ? evidence : null;
        }
        catch (JsonException) { return null; }
    }

    public static EventCandidate? Candidate(SnapshotEnvelope snapshot)
    {
        var evidence = Read(snapshot);
        var answer = evidence?.BestAnswer();
        if (evidence is null || answer is null) return null;
        return new EventCandidate
        {
            CandidateId = "recovery:answer_npc_question:" + evidence.Speaker,
            Kind = CandidateKind, Available = true,
            LocationId = ReadStateFieldString(snapshot, "player", "location_id"),
            EstimatedTicks = 20, ExpectedEffect = "native_npc_question_answered;native_friendship_effect",
            Parameters = new[]
            {
                new SmallModelActionParameter { Name = "expected_dialogue_key", Value = evidence.Identity() },
                new SmallModelActionParameter { Name = "dialogue_response_key", Value = answer.Key },
                new SmallModelActionParameter { Name = "npc_name", Value = evidence.Speaker }
            }
        };
    }

    public static bool Matches(SnapshotEnvelope snapshot, string? identity, string? response)
    {
        var evidence = Read(snapshot);
        return evidence is not null && evidence.Identity() == identity && evidence.BestAnswer()?.Key == response;
    }
}
