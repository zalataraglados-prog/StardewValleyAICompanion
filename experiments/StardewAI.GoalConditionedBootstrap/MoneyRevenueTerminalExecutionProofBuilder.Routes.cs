using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueTerminalExecutionProofBuilder
{
    private static TerminalRouteVerification VerifyRouteTransitions(
        MoneyRevenueCounterfactualProofRequest request,
        SnapshotEnvelope terminalStart,
        MoneyRevenueTerminalQueueTransitionProof[] transitions)
    {
        transitions ??= Array.Empty<MoneyRevenueTerminalQueueTransitionProof>();
        var candidate = request.TerminalCandidate;
        var verified = MoneyRevenueResourceNeutralTerminalRouteVerifier.Verify(
            terminalStart,
            transitions,
            new MoneyRevenueTerminalContinuationIdentity(
                request.TerminalOptionId,
                candidate.QualifiedItemId,
                candidate.SlotIndex,
                candidate.Quantity,
                candidate.UnitPrice,
                candidate.ShopId));
        return new TerminalRouteVerification(
            verified.TerminalActionBefore,
            transitions.Length == 0
                ? request.TerminalSnapshotPath
                : Path.GetFullPath(transitions[^1].AfterSnapshotPath),
            verified.Evidence);
    }

    private sealed record TerminalRouteVerification(
        SnapshotEnvelope TerminalActionBefore,
        string TerminalActionBeforePath,
        MoneyRevenueTerminalQueueTransitionEvidence[] Evidence);
}
