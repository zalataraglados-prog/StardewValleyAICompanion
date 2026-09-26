using System.Security.Cryptography;
using System.Text;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionReplanBuilder
{
    public static AcquisitionRouteSupportingTransitionReplanAdmission Build(
        AcquisitionRouteSupportingTransitionRequestInputs priorInputs,
        AcquisitionRouteSupportingTransitionSettlementProof proof,
        AcquisitionRoutePortfolioInputs freshInputs)
    {
        var settlementPath = Path.GetFullPath(proof.SettlementReceiptPath);
        var expectedSettlement =
            AcquisitionRouteSupportingTransitionSettlementBuilder.BuildReceipt(
                priorInputs,
                proof.SupportRequestPath,
                proof.SupportCommitReceiptPath,
                proof.CommittedLedgerPath,
                proof.CommitResultPath,
                proof.CompilationPath,
                proof.ExecutionReceiptPath,
                proof.AfterSnapshotPath,
                proof.SupportingTransitionReceiptPath,
                proof.RunId,
                proof.ExecutorVersion,
                proof.SettlementRequestPath,
                proof.SettlementResultPath,
                proof.SettledLedgerPath);
        var settlement = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteSupportingTransitionSettlementReceipt>(
            settlementPath,
            "Acquisition support recurrence settlement receipt");
        Require(EqualJson(settlement, expectedSettlement),
            "Support recurrence settlement receipt drifted.");
        var supportRequest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteSupportingTransitionRequest>(
            Path.GetFullPath(proof.SupportRequestPath),
            "Acquisition support recurrence request source");
        var compilation = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteDispatchCompilation>(
            Path.GetFullPath(proof.CompilationPath),
            "Acquisition support recurrence prior compilation");
        var context = AcquisitionRoutePortfolioBuilder.Prepare(freshInputs);
        var matchingRoutes = context.Opportunity.Routes.Where(route =>
                route.RouteOccurrenceId == settlement.RouteOccurrenceId)
            .ToArray();
        Require(matchingRoutes.Length == 1,
            "Support recurrence route is missing from the fresh denominator.");
        var requirement = AcquisitionRoutePortfolioBuilder.RequirementRoute(
            matchingRoutes[0]);
        var fresh = new AcquisitionRouteSupportingTransitionFreshContext(
            context.Inventory.GoalId,
            context.Snapshot.StateHash,
            context.SnapshotSha256,
            context.LedgerState.Ledger.Revision,
            context.StrategyLedgerSha256,
            context.RequirementInventorySha256,
            context.OpportunityCostSha256,
            matchingRoutes[0].RouteOccurrenceId,
            requirement.RequirementSetId,
            requirement.RequirementId,
            context.Opportunity.SnapshotStateHash ==
                context.Snapshot.StateHash);
        return BuildCore(
            settlement,
            supportRequest,
            compilation,
            fresh,
            CurrentTeacherFrontierSupport.HashFile(settlementPath));
    }

    internal static AcquisitionRouteSupportingTransitionReplanAdmission
        BuildCore(
            AcquisitionRouteSupportingTransitionSettlementReceipt settlement,
            AcquisitionRouteSupportingTransitionRequest supportRequest,
            AcquisitionRouteDispatchCompilation compilation,
            AcquisitionRouteSupportingTransitionFreshContext fresh,
            string settlementSha256)
    {
        var reasons = new List<string>();
        if (!settlement.ReservationLifecycleVerified ||
            !settlement.FreshReplanRequired ||
            settlement.RouteTerminalCompletionRecorded ||
            settlement.TerminalReceiptEligible ||
            settlement.FormalTrainingAuthorized)
        {
            reasons.Add("support_replan_settlement_not_verified");
        }
        if (!IsSha256(settlementSha256) ||
            settlement.GoalId != supportRequest.GoalId ||
            settlement.SupportRequestId != supportRequest.SupportRequestId ||
            settlement.RouteOccurrenceId != supportRequest.RouteOccurrenceId ||
            fresh.GoalId != settlement.GoalId ||
            fresh.RouteOccurrenceId != settlement.RouteOccurrenceId ||
            fresh.StateHash != settlement.AfterStateHash ||
            fresh.LedgerRevision != settlement.SettledLedgerRevision ||
            (!string.IsNullOrWhiteSpace(settlement.SettledLedgerSha256) &&
             fresh.LedgerSha256 != settlement.SettledLedgerSha256))
        {
            reasons.Add("support_replan_source_identity_mismatch");
        }
        var queueInvalidated = PriorQueueInvalidated(
            supportRequest,
            compilation,
            fresh);
        if (!queueInvalidated)
            reasons.Add("support_replan_prior_queue_not_invalidated");
        if (!fresh.AllTargetDateAxesRebuilt ||
            string.IsNullOrWhiteSpace(fresh.RequirementSetId) ||
            string.IsNullOrWhiteSpace(fresh.RequirementId) ||
            !IsSha256(fresh.SnapshotSha256) ||
            !IsSha256(fresh.LedgerSha256) ||
            !IsSha256(fresh.RequirementInventorySha256) ||
            !IsSha256(fresh.OpportunityCostSha256))
        {
            reasons.Add("support_replan_fresh_axis_evidence_incomplete");
        }
        var blocking = reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var ready = blocking.Length == 0;
        var next = ready
            ? new AcquisitionRoutePortfolioTeacherPreferenceRequest
            {
                RequestId = RequestId(
                    supportRequest.SupportRequestId,
                    settlementSha256,
                    fresh.StateHash,
                    fresh.LedgerSha256),
                GoalId = fresh.GoalId,
                SnapshotStateHash = fresh.StateHash,
                ExpectedLedgerRevision = fresh.LedgerRevision,
                ScopedRequirements = new[]
                {
                    new AcquisitionRoutePortfolioRequirementScope(
                        fresh.RequirementSetId,
                        fresh.RequirementId)
                }
            }
            : null;
        return new AcquisitionRouteSupportingTransitionReplanAdmission
        {
            Status = ready
                ? "verified_supporting_transition_fresh_replan_admission"
                : "blocked_supporting_transition_fresh_replan",
            GoalId = settlement.GoalId,
            SupportRequestId = settlement.SupportRequestId,
            RouteOccurrenceId = settlement.RouteOccurrenceId,
            PriorQueueId = compilation.ActionQueue?.QueueId ?? string.Empty,
            PriorStateHash = supportRequest.SourceStateHash,
            PriorLedgerRevision =
                compilation.ActionQueue is null
                    ? 0
                    : PriorLedgerRevision(compilation),
            FreshStateHash = fresh.StateHash,
            FreshLedgerRevision = fresh.LedgerRevision,
            SupportSettlementReceiptSha256 = settlementSha256,
            FreshSnapshotSha256 = fresh.SnapshotSha256,
            FreshStrategyLedgerSha256 = fresh.LedgerSha256,
            FreshRequirementInventorySha256 =
                fresh.RequirementInventorySha256,
            FreshOpportunityCostSha256 = fresh.OpportunityCostSha256,
            RequirementSetId = fresh.RequirementSetId,
            RequirementId = fresh.RequirementId,
            AllTargetDateAxesRebuilt = fresh.AllTargetDateAxesRebuilt,
            PriorQueueInvalidated = queueInvalidated,
            FreshTeacherRequestReady = ready,
            NextTeacherPreferenceRequest = next,
            FormalTrainingAuthorized = false,
            BlockingReasons = blocking
        };
    }

    private static string RequestId(
        string supportRequestId,
        string settlementSha256,
        string stateHash,
        string ledgerSha256)
    {
        var digest = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(string.Join(
                    "|",
                    supportRequestId,
                    settlementSha256,
                    stateHash,
                    ledgerSha256))))
            .ToLowerInvariant();
        return supportRequestId + ":replan:" + digest;
    }

}
