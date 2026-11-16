using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CropTeacherNativeVariantTerminalProof
{
    private static CropTeacherObservedShippingPrefixVerification VerifyObservedPrefix(
        CropTeacherNativeVariantTerminalRequest wrapper, string retentionPath)
    {
        var prefix = wrapper.ObservedShippingPrefix!;
        Require(Same(prefix.RetentionSnapshotPath, retentionPath), "observed prefix changed its retained start.");
        var request = wrapper.Settlement;
        var identity = prefix.Transitions is { Length: > 0 } ? prefix.Transitions[0] :
            throw new InvalidDataException("Native variant terminal: missing observed prefix.");
        var semantic = request.RouteTransitions.Concat(request.ShippingDepositTransitions).Concat(request.SettlementRouteTransitions).ToArray();
        Require(semantic.All(row => row.RunId == identity.RunId && row.ExecutorVersion == identity.ExecutorVersion),
            "observed prefix and remaining actions changed native identity.");
        var recurrence = Read<MoneyRevenueRecurrenceProofManifest>(request.RevenueRecurrenceManifestPath);
        if (recurrence.Transitions is not [var sleep])
            throw new InvalidDataException("Native variant terminal: missing single native settlement.");
        Require(sleep.RunId == identity.RunId && sleep.ExecutorVersion == identity.ExecutorVersion,
            "observed prefix and final settlement changed native identity.");
        var receipts = prefix.Transitions.Select(row => row.ExecutionReceiptPath)
            .Concat(semantic.Select(row => row.ExecutionReceiptPath))
            .Concat((wrapper.NativeControlCheckpoints ?? []).SelectMany(row => row.Transitions).Select(row => row.ExecutionReceiptPath))
            .Append(sleep.ExecutionReceiptPath).Select(Path.GetFullPath).ToArray();
        Require(receipts.Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal).Count() == receipts.Length,
            "observed prefix/control/remaining settlement receipt is replayed.");
        return CropTeacherObservedShippingPrefixVerifier.Verify(prefix, wrapper.Target, prefix.Transitions[^1].AfterSnapshotPath);
    }

    private static MoneyRevenueTerminalQueueTransitionProof ObservedIdentity(MoneyRevenueCropNativeLifecycleTransitionRequest native) => new()
    {
        QueuePath = native.QueuePath, BeforeSnapshotPath = native.BeforeSnapshotPath,
        ExecutionReceiptPath = native.ExecutionReceiptPath, AfterSnapshotPath = native.AfterSnapshotPath,
        RunId = native.RunId, ExecutorVersion = native.ExecutorVersion, SelectedCandidateId = native.SelectedCandidateId
    };

    private static string FirstObservedShippingState(CropTeacherObservedShippingPrefix prefix, MoneyRevenueCropShippingDepositEvidence deposit)
    {
        // Aggregate-before may precede movement. Use the exact bound original shipping step,
        // not a fabricated one-item receipt or the queue's compilation source hash.
        foreach (var native in prefix.Transitions)
        {
            var receipt = Read<QueueExecutionReceiptEnvelope>(native.ExecutionReceiptPath);
            var steps = receipt.StepResults.Where(row => row.QueueItemId == deposit.QueueItemId).ToArray();
            if (steps.Length == 0) continue;
            Require(steps is [var step] && step.PrimitiveKind == "ship_inventory_item_to_bin" &&
                !string.IsNullOrWhiteSpace(step.SourceStateHash), "observed first shipping frame is ambiguous.");
            return steps[0].SourceStateHash;
        }
        throw new InvalidDataException("Native variant terminal: missing original first shipping step.");
    }
}
