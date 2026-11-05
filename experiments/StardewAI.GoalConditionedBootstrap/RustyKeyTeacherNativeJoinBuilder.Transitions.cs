using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Strategy;
using static StardewAI.GoalConditionedBootstrap.RustyKeyNativeKeyReceiptBuilder;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyTeacherNativeJoinBuilder
{
    private static string ValidateDonation(NativeAction action, RustyKeyDonationTeacherComparison comparison)
    {
        Require(action.Result.OptionId == "executor.donate_museum_item" && action.Before.StateHash == comparison.SourceStateHash &&
            action.Before.GameTick == comparison.GameTick && TotalDay(action.Before) == comparison.TotalDay &&
            TotalDay(action.After) == comparison.TotalDay && action.Queue.GoalId == comparison.GoalId,
            "Rusty Key native donation does not bind the independent decision frame/goal.");
        var preferredIds = comparison.PairwisePreferences.Select(row => row.PreferredCandidateId).ToHashSet(StringComparer.Ordinal);
        Require(action.Queue.CandidateAudit is not null && action.Queue.CandidateAudit.All(row => row is not null),
            "Rusty Key donation candidate attribution is missing.");
        var accepted = action.Queue.CandidateAudit!.Where(row => row.Decision == "accepted" && preferredIds.Contains(row.CandidateId)).ToArray();
        Require(accepted.Length == 1, "Rusty Key native donation is not an independently preferred candidate.");
        var selected = comparison.CandidateEvaluations.Single(row => row.CandidateId == accepted[0].CandidateId);
        Require(selected.Admitted && selected.CompiledQueue is { Items.Length: 1 } &&
            SameCommand(action.Item.NormalizedCommand, selected.CompiledQueue.Items[0].NormalizedCommand) &&
            action.After.GameTick - action.Before.GameTick <= comparison.ComparisonHorizonTicks,
            "Rusty Key native donation differs from the original Teacher compilation or exceeded its comparison horizon.");
        Require(MaterialOutputCustodyContract.TryMuseumDonated(action.Before, selected.QualifiedItemId, out var donatedBefore) && !donatedBefore &&
            MaterialOutputCustodyContract.TryMuseumDonated(action.After, selected.QualifiedItemId, out var donatedAfter) && donatedAfter,
            "Rusty Key native donation has no exact item false-to-true transition.");
        var beforeCount = ExactInventoryReceiptVerifier.InventoryCount(action.Before, selected.QualifiedItemId, 0);
        var afterCount = ExactInventoryReceiptVerifier.InventoryCount(action.After, selected.QualifiedItemId, 0);
        Require(beforeCount.HasValue && afterCount.HasValue && beforeCount.Value - afterCount.Value == 1 &&
            Facts(action.Result).SetEquals(new[] { "LibraryMuseum.OpenDonationMenu_completed", "MuseumMenu.receiveLeftClick_inventory_completed",
                "MuseumMenu.receiveLeftClick_display_completed", "Game1.exitActiveMenu_reward_settlement_completed" }),
            "Rusty Key native donation did not consume exactly one item through the original native menu.");
        return selected.CandidateId;
    }

    private static bool SameCommand(NormalizedCommand left, NormalizedCommand right)
    {
        // A later planning-budget proof marker is not a native action or permission change.
        object Projection(NormalizedCommand command) => new
        {
            command.CommandType, command.OptionId, command.BehaviorCategory, command.CompilerResponsibility,
            command.TrainingRole, command.StateHash, command.ExecutionMode, command.Actor, command.GoalId,
            parameters = (command.Parameters ?? Array.Empty<SmallModelActionParameter>())
                .Where(row => row is not null && !row.Name.StartsWith("budget.", StringComparison.Ordinal) &&
                    !(row.Name == "safety_constraint" && row.Value == "daily_plan_time_budget_checked"))
                .OrderBy(row => row.Name, StringComparer.Ordinal).ThenBy(row => row.Value, StringComparer.Ordinal).ToArray(),
            steps = (command.Steps ?? Array.Empty<CompiledActionStep>())
                .Select(step => new { step.StepType, step.Target, step.ExpectedEffect, step.EstimatedTicks }).ToArray()
        };
        return left.Parameters is not null && left.Parameters.All(row => row is not null && !string.IsNullOrWhiteSpace(row.Name)) &&
            left.Steps is not null && left.Steps.All(row => row is not null) && EqualJson(Projection(left), Projection(right));
    }

    private static void ValidateSleep(NativeAction action, int decisionDay)
    {
        Require(NativeDaySettlementSupport.IsSingleNativeSleepQueue(action.Queue) && TotalDay(action.Before) == decisionDay &&
            TotalDay(action.After) == decisionDay + 1 && SnapshotGameClockBudgetReader.CurrentTime(action.After) == 600 &&
            !string.IsNullOrWhiteSpace(action.Result.EndOfNightTransactionId) &&
            !string.IsNullOrWhiteSpace(action.Result.EndOfNightTransactionEvidencePath) &&
            action.Result.EndOfNightTransactionStatus is "ready_for_safe_next_day_action" or "complete" &&
            Facts(action.Result).SetEquals(new[] { "sleep_yes_confirmed", "new_day_observed", "native_save_committed",
                "post_sleep_menu_closed", "native_new_day_world_stable" }),
            "Rusty Key continuation requires the original immutable native-save-ready sleep action.");
        var changes = action.Result.ChangedFacts;
        Require(changes is not null && changes.All(row => row is not null), "Rusty Key sleep changes are malformed.");
        var days = changes!.Where(row => row.Path == "time.total_days").ToArray();
        var story = Value(action.After, "player", "story_event");
        var menu = Value(action.After, "menus", "active_menu");
        Require(days.Length == 1 && days[0].Before == decisionDay.ToString(CultureInfo.InvariantCulture) &&
            days[0].After == (decisionDay + 1).ToString(CultureInfo.InvariantCulture) &&
            False(story, "active") && False(story, "event_up") && False(menu, "is_open"),
            "Rusty Key native sleep did not restore exact next-day control.");
    }

    private static void ValidateProgress(SnapshotEnvelope snapshot, bool terminal)
    {
        var museum = Value(snapshot, "world_progress", "museum");
        Require(museum.ValueKind == JsonValueKind.Object && museum.TryGetProperty("donated_count", out var count) &&
            count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var donated) && donated == 60 &&
            True(museum, "rusty_key_reward_claimed") && True(museum, "rusty_key_prerequisite_event_seen") &&
            (terminal ? True(museum, "has_rusty_key") && True(museum, "rusty_key_event_seen") :
                False(museum, "has_rusty_key") && False(museum, "rusty_key_event_seen")) &&
            Value(snapshot, "player", "has_rusty_key").ValueKind == (terminal ? JsonValueKind.True : JsonValueKind.False),
            "Rusty Key native donation/reward/key progress changed outside its exact transition.");
    }

    private static HashSet<string> Facts(TrainingExecutionResult result) =>
        (result.PrimitiveVerificationReasons ?? Array.Empty<string>()).ToHashSet(StringComparer.Ordinal);
    private static bool True(JsonElement row, string name) => row.ValueKind == JsonValueKind.Object &&
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
    private static bool False(JsonElement row, string name) => row.ValueKind == JsonValueKind.Object &&
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.False;
}
