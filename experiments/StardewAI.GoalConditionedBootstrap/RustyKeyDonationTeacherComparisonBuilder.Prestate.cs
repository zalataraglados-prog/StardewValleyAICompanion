using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using static StardewAI.GoalConditionedBootstrap.RustyKeyNativeKeyReceiptBuilder;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyDonationTeacherComparisonBuilder
{
    private static void ValidatePrestate(SnapshotEnvelope snapshot)
    {
        Require(snapshot.GameVersion == "1.6.15" && !string.IsNullOrWhiteSpace(snapshot.BridgeVersion) &&
            snapshot.GameTick >= 0 && snapshot.State is not null && SnapshotHash.MatchesStateHash(snapshot.State, snapshot.StateHash),
            "Rusty Key Teacher snapshot version, tick or hash is invalid.");
        Require(TotalDay(snapshot) is >= 0 and < 224, "Rusty Key Teacher decision is outside Stage 1.");
        var museum = Value(snapshot, "world_progress", "museum");
        Require(museum.ValueKind == JsonValueKind.Object && Number(museum, "donated_count") == 59 &&
            Number(museum, "rusty_key_donation_threshold") == 60 &&
            IsFalse(museum, "has_rusty_key") && IsFalse(museum, "rusty_key_reward_claimed") &&
            IsFalse(museum, "rusty_key_prerequisite_event_seen") && IsFalse(museum, "rusty_key_event_seen") &&
            museum.TryGetProperty("museum_is_current_location", out var here) && here.ValueKind == JsonValueKind.True &&
            Value(snapshot, "player", "location_id").ValueEquals("ArchaeologyHouse"),
            "Rusty Key Teacher needs an untriggered held-item threshold decision inside the museum.");
        var story = Value(snapshot, "player", "story_event");
        var menu = Value(snapshot, "menus", "active_menu");
        Require(story.ValueKind == JsonValueKind.Object && IsFalse(story, "active") && IsFalse(story, "event_up") &&
            menu.ValueKind == JsonValueKind.Object && IsFalse(menu, "is_open"),
            "Rusty Key Teacher cannot compare a no-input alternative during native event/menu input.");
        Require(SnapshotGameClockBudgetReader.AvailableMinutes(snapshot,
                GameClockBudgetPolicy.AutonomousRecoveryStartTime) is int minutes &&
            minutes >= GameClockBudgetPolicy.TicksToGameMinutes(HorizonTicks),
            "Rusty Key Teacher comparison horizon exceeds the current safe clock budget.");
    }

    private static RustyKeyDonationTeacherAlternative EvaluateDefer(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger, string goalId)
    {
        // This is a compiler-checked counterfactual, never a new automatic candidate or runtime command.
        var plan = new SmallModelPlanEnvelope
        {
            PlanId = DeferId, SourceModel = PolicyId, StateHash = snapshot.StateHash, GoalId = goalId,
            Actor = ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer),
            Steps = new[] { new SmallModelPlanStep
            {
                StepId = DeferId, Kind = "wait_ticks", WaitTicks = HorizonTicks,
                TargetLocation = "ArchaeologyHouse", EstimatedMinutes = GameClockBudgetPolicy.TicksToGameMinutes(HorizonTicks)
            } }
        };
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, ledger);
        var reasons = CurrentStageOneCollectionTeacherPreferenceBuilder.CompilationReasons(plan, queue).ToList();
        if (queue.Status != "pending" || queue.Items.Length != 1 ||
            queue.Items.Any(item => item.Status != "pending" || item.OptionId != "executor.wait_ticks"))
            reasons.Add("original_bounded_wait_compiler_rejected");
        Stabilize(queue, snapshot, ledger, DeferId);
        return new(DeferId, "bounded_no_donation_counterfactual", string.Empty, reasons.Count == 0,
            reasons.Distinct(StringComparer.Ordinal).ToArray(), 0, HorizonTicks, queue);
    }

    private static bool IsFalse(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.False;

    private static int Number(JsonElement row, string name)
    {
        Require(row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out _),
            "Rusty Key Teacher explicit integer is missing: " + name);
        return value.GetInt32();
    }
}
