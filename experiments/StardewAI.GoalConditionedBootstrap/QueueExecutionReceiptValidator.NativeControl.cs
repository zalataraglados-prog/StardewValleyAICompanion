using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using System.Text.Json;
using static StardewAI.GoalConditionedBootstrap.MoneyRevenueResourceNeutralTerminalRouteVerifier;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class QueueExecutionReceiptValidator
{
    // Shape exception only: callers still validate the entire receipt, continuity,
    // calendar, resource boundary and final destination through the original gate.
    internal static bool IsExactNativeControlObservation(
        ActionQueueEnvelope queue,
        SnapshotEnvelope before,
        QueueExecutionReceiptEnvelope receipt,
        SnapshotEnvelope after)
    {
        var candidate = NativeControlSettleRecovery.TryCreate(before);
        var items = queue.Items ?? Array.Empty<ActionQueueItem>();
        if (candidate is null || receipt.SelectedCandidateId != candidate.CandidateId ||
            items.Length != 1 || items[0].OptionId != "executor.wait_ticks" ||
            items[0].NormalizedCommand is not { } command ||
            command.OptionId != "executor.wait_ticks" ||
            command.Steps is not { Length: 1 } steps ||
            steps[0].StepType != "wait_ticks" ||
            steps[0].Target != NativeControlSettleRecovery.WaitTicks.ToString() ||
            receipt.AfterGameTick - receipt.BeforeGameTick < NativeControlSettleRecovery.WaitTicks)
            return false;
        var parameters = command.Parameters ?? Array.Empty<SmallModelActionParameter>();
        bool Exact(string name, string value) => parameters.Count(row => row.Name == name) == 1 &&
            parameters.Single(row => row.Name == name).Value == value;
        bool SameField(string field, JsonValueKind kind) =>
            TryStateFieldValue(before, "player", field, out var left) &&
            TryStateFieldValue(after, "player", field, out var right) &&
            left.ValueKind == kind && right.ValueKind == kind &&
            (kind == JsonValueKind.String
                ? !string.IsNullOrWhiteSpace(left.GetString()) && left.GetString() == right.GetString()
                : left.TryGetInt32(out var x) && right.TryGetInt32(out var y) && x == y);
        return Exact("wait_ticks", NativeControlSettleRecovery.WaitTicks.ToString()) &&
            Exact("precondition", "candidate_id:" + candidate.CandidateId) &&
            SameField("location_id", JsonValueKind.String) &&
            SameField("tile_x", JsonValueKind.Number) &&
            SameField("tile_y", JsonValueKind.Number);
    }
}
