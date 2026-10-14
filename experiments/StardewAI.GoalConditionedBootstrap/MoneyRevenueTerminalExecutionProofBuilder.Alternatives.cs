using StardewAI.Contracts.State;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueTerminalExecutionProofBuilder
{
    internal static MoneyRevenueTerminalExecutionEvidence VerifyAlternative(
        MoneyRevenueCounterfactualProofRequest request,
        MoneyRevenueTerminalAlternativeProof proof)
    {
        Require(proof.RequestId == request.RequestId &&
                request.TerminalOpportunityCostProofRequired &&
                request.TerminalReservationClaimProofRequired &&
                request.RevenueRecurrenceProofRequired,
            "Money Revenue terminal proof does not bind its request requirements.");
        var terminalStartPath = RequiredFile(
            request.TerminalSnapshotPath,
            "Money Revenue terminal start snapshot");
        var terminalStart = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            terminalStartPath,
            "Money Revenue terminal start snapshot");
        Require(Hash(terminalStartPath) == request.TerminalSnapshotSha256 &&
                terminalStart.StateHash == request.TerminalStateHash,
            "Money Revenue terminal start snapshot drifted from its request.");

        var routeProofs = proof.RouteTransitions ??
            Array.Empty<MoneyRevenueTerminalQueueTransitionProof>();
        var route = VerifyRouteTransitions(
            request,
            terminalStart,
            routeProofs);
        var native = VerifyNativeTerminal(request, proof, route);
        var reservation = VerifyStrategyLedgerAndReservation(
            request,
            proof,
            terminalStart,
            route.TerminalActionBefore,
            native.TerminalActionAfter);

        var elapsedTicks = checked(
            native.TerminalActionAfter.GameTick - terminalStart.GameTick);
        Require(elapsedTicks > 0 && elapsedTicks <= int.MaxValue,
            "Money Revenue terminal execution has an invalid elapsed tick count.");
        var evidencePaths = route.Evidence
            .SelectMany(row => new[]
            {
                routeProofs[row.TransitionIndex].QueuePath,
                routeProofs[row.TransitionIndex].BeforeSnapshotPath,
                routeProofs[row.TransitionIndex].ExecutionReceiptPath,
                routeProofs[row.TransitionIndex].AfterSnapshotPath
            })
            .Concat(native.Artifacts.Select(row => row.Path))
            .Append(proof.BeforeStrategyLedgerPath)
            .Append(proof.AfterStrategyLedgerPath)
            .Append(native.RecurrenceManifestPath)
            .Append(native.RecurrenceReceiptPath)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var opportunityCost = new AcquisitionOpportunityCostVector(
            GameClockBudgetPolicy.TicksToGameMinutes((int)elapsedTicks),
            0d,
            0,
            Array.Empty<AcquisitionOpportunityMaterialCost>(),
            Array.Empty<AcquisitionOpportunityCurrencyCost>(),
            evidencePaths);
        MoneyRevenueAlternativeComparer.ValidateOpportunityCost(
            opportunityCost);

        var evidence = new MoneyRevenueTerminalExecutionEvidence(
            request.RequestId,
            request.ConcreteAlternativeId,
            request.RequiredRevenueTransitionProofKind,
            terminalStart.StateHash,
            native.TerminalActionBeforeStateHash,
            native.TerminalActionAfter.StateHash,
            elapsedTicks,
            opportunityCost.GuaranteedElapsedGameMinutes,
            opportunityCost,
            reservation.TargetNodeId,
            reservation.TargetSlotIndex,
            reservation.TargetStack,
            reservation.TargetReservedQuantity,
            reservation.TargetAvailableQuantity,
            reservation.BeforeLedgerSha256,
            reservation.AfterLedgerSha256,
            reservation.LedgerId,
            reservation.LedgerRevision,
            route.Evidence,
            native.Artifacts,
            native.RecurrenceManifestSha256,
            native.RecurrenceReceiptSha256,
            true,
            true,
            true,
            true,
            string.Empty);
        return evidence with
        {
            EvidenceSha256 = AcquisitionRoutePortfolioTeacherPreferenceBuilder
                .ArtifactSha256(evidence)
        };
    }
}
