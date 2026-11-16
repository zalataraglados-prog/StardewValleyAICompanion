using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

// Indices count semantic route/deposit/home transitions; the final index is before sleep.
public sealed record CropTeacherTerminalControlCheckpoint(int BeforeTransitionIndex,
    MoneyRevenueCropNativeLifecycleTransitionRequest[] Transitions);

public sealed record CropTeacherTerminalControlCheckpointEvidence(int BeforeTransitionIndex,
    MoneyRevenueTerminalQueueTransitionEvidence[] Transitions);

internal sealed class CropTeacherTerminalControlSequence
{
    private readonly CropTeacherTerminalControlCheckpoint[] checkpoints;
    private readonly MoneyRevenueTerminalQueueTransitionProof identity;
    private readonly int totalDay;
    private int position;
    private int consumed;
    internal string CurrentPath { get; private set; }
    internal List<CropTeacherTerminalControlCheckpointEvidence> Evidence { get; } = [];

    internal CropTeacherTerminalControlSequence(CropTeacherNativeVariantTerminalRequest request,
        MoneyRevenueTerminalQueueTransitionProof identity, int totalDay, string sourcePath)
    {
        this.identity = identity; this.totalDay = totalDay; CurrentPath = sourcePath;
        checkpoints = request.NativeControlCheckpoints!;
        var last = request.Settlement.RouteTransitions.Length + request.Settlement.ShippingDepositTransitions.Length +
            request.Settlement.SettlementRouteTransitions.Length;
        Require(checkpoints.All(row => row is not null && row.BeforeTransitionIndex >= 0 && row.BeforeTransitionIndex <= last &&
                row.Transitions is { Length: > 0 } && row.Transitions.All(value => value is not null)) &&
            checkpoints.Select(row => row.BeforeTransitionIndex).SequenceEqual(checkpoints.Select(row => row.BeforeTransitionIndex).Distinct().Order()),
            "control checkpoint positions are duplicated, unordered or outside the terminal sequence.");
        var semantic = request.Settlement.RouteTransitions.Concat(request.Settlement.ShippingDepositTransitions)
            .Concat(request.Settlement.SettlementRouteTransitions);
        var controls = checkpoints.SelectMany(row => row.Transitions).Concat(request.NativeControlTail ?? []);
        var receipts = semantic.Select(row => row.ExecutionReceiptPath).Concat(controls.Select(row => row.ExecutionReceiptPath)).ToArray();
        Require(receipts.Select(Path.GetFullPath).Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Count() == receipts.Length,
            "control/semantic receipt is replayed.");
    }

    internal SnapshotEnvelope ObserveControls(SnapshotEnvelope expected)
    {
        if (consumed == checkpoints.Length || checkpoints[consumed].BeforeTransitionIndex != position) return expected;
        var checkpoint = checkpoints[consumed++];
        var controls = checkpoint.Transitions;
        Require(controls.All(row => row.TotalDay == totalDay && row.RunId == identity.RunId && row.ExecutorVersion == identity.ExecutorVersion),
            "control checkpoint differs from terminal native identity/day.");
        // The legacy independent recovery leaf labels close_menu differently. Normalize only
        // the comparison role; retain its original request and reuse the same old control kernel.
        var comparison = controls.Select(row =>
        {
            if (row.TransitionKind != "control_recovery") return row;
            var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(row.QueuePath, "terminal recovery queue");
            Require(queue.Items is [var item] && item.OptionId == "executor.close_menu", "independent recovery is not close_menu.");
            return row with { TransitionKind = "supporting_route_to_sleep" };
        }).ToArray();
        var evidence = MoneyRevenueCropNativeLifecycleProofBuilder.VerifyNativeObservedControlTail(
            CurrentPath, comparison, controls[^1].AfterSnapshotPath);
        Evidence.Add(new(position, evidence));
        CurrentPath = controls[^1].AfterSnapshotPath;
        return CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(CurrentPath, "terminal control checkpoint");
    }

    internal void Advance(string afterPath) { CurrentPath = afterPath; position++; }
    internal void RequireConsumed() => Require(consumed == checkpoints.Length, "unconsumed terminal control checkpoint.");
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidDataException("Native variant terminal: " + reason); }
}
