using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyRollingSupplyProposalBuilder
{
    public static RustyKeyRollingSupplyProposal Build(AcquisitionRoutePortfolioInputs inputs)
    {
        var inventory = CurrentTeacherFrontierSupport.Read<AuthoritativeRequirementInventoryReport>(
            inputs.RequirementInventoryPath, "Rusty Key requirement inventory");
        var snapshot = AcquisitionReadOnlySnapshotProjection.Read(inputs.SnapshotPath);
        var ledger = AcquisitionStrategyLedgerReader.Read(inputs.StrategyLedgerPath, snapshot).Ledger;
        return Build(inventory, snapshot, ledger,
            CurrentTeacherFrontierSupport.HashFile(inputs.RequirementInventoryPath),
            CurrentTeacherFrontierSupport.HashFile(inputs.SnapshotPath),
            CurrentTeacherFrontierSupport.HashFile(inputs.StrategyLedgerPath),
            () => string.IsNullOrWhiteSpace(inputs.TargetDateOpportunityCostPath)
                ? null : AcquisitionRoutePortfolioBuilder.Prepare(inputs), inputs.RouteTimingCalibrationPath);
    }

    internal static RustyKeyRollingSupplyProposal Build(
        AuthoritativeRequirementInventoryReport inventory, SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger,
        string inventorySha256, string snapshotSha256, string ledgerSha256,
        Func<AcquisitionRoutePortfolioBuilder.AcquisitionRoutePortfolioBuildContext?> acquisitionContext,
        string routeTimingCalibrationPath = "")
    {
        var result = new RustyKeyRollingSupplyProposal
        {
            GoalId = inventory.GoalId, SnapshotStateHash = snapshot.StateHash,
            SourceSaveId = snapshot.SaveId.Value ?? string.Empty, SourcePlayerId = snapshot.PlayerId.Value ?? string.Empty,
            RequirementInventorySha256 = inventorySha256, SnapshotSha256 = snapshotSha256,
            StrategyLedgerSha256 = ledgerSha256, StrategyLedgerRevision = ledger.Revision
        };
        var reasons = new List<string>();
        var source = RustyKeyAcquisitionPortfolioBuilder.ReadCurrentScope(inventory, snapshot, reasons);
        if (ledger.SchemaVersion != "strategy_commitment_ledger.v1" || string.IsNullOrWhiteSpace(ledger.LedgerId) ||
            ledger.Revision < 0 || ledger.SaveId != result.SourceSaveId || ledger.PlayerId != result.SourcePlayerId)
            reasons.Add("rusty_key_rolling_ledger_identity_invalid");
        if (source is null || reasons.Count > 0)
            return Block(result, reasons);
        result.SourceTotalDay = source.SourceTotalDay;
        result.DonatedCount = source.Progress.AggregateCompletedCount;
        result.DonationThreshold = source.Threshold;
        result.RemainingDonationCount = Math.Max(0, source.Threshold - result.DonatedCount);
        result.CompleteMuseumItemDenominator = source.Museum.Groups.OrderBy(group => group.RequirementId, StringComparer.Ordinal)
            .Select(group => new RustyKeyAcquisitionItem(group.RequirementId, group.Alternatives[0].QualifiedItemId,
                source.Progress.Requirements[group.RequirementId].Completed, false)).ToArray();
        if (result.RemainingDonationCount == 0)
        {
            result.ThresholdSatisfied = true;
            result.Status = "donation_threshold_satisfied_native_key_handoff_still_required";
            return result;
        }
        if (result.SourceTotalDay >= RustyKeyAcquisitionPortfolioBuilder.StageOneDeadlineExclusive - 1)
            return Block(result, new[] { "rusty_key_supply_date_leaves_no_pre_year3_key_handoff" });
        var evaluations = new List<RustyKeyRollingCandidateEvaluation>();
        string[]? pending = null;
        var temporalRequirements = new HashSet<string>(StringComparer.Ordinal);
        if (CandidateOptionAvailabilityEvaluator.RequiresExclusiveRecovery(snapshot) || NativeControlSettleRecovery.TryCreate(snapshot) is not null)
        {
            pending = ReadPendingDonations(source, snapshot, ledger, result.GoalId, reasons);
            if (reasons.Count == 0 && TryOwnedSupplyDeferral(result, snapshot, ledger, pending, temporalRequirements, resumeOnly: true))
                return result;
        }
        if (TryHeldDonation(result, source, snapshot, ledger, evaluations, routeTimingCalibrationPath, temporalRequirements))
        {
            result.CandidateEvaluations = evaluations.ToArray();
            return result;
        }

        pending ??= ReadPendingDonations(source, snapshot, ledger, result.GoalId, reasons);
        if (pending.Length > 0) result.PendingDonationRequirementIds = pending;
        if (reasons.Count == 0 && TryOwnedSupplyDeferral(result, snapshot, ledger, pending, temporalRequirements))
        {
            result.CandidateEvaluations = evaluations.ToArray();
            return result;
        }
        if (reasons.Count == 0 && TryStoredCustody(result, source, snapshot, ledger, evaluations, routeTimingCalibrationPath))
        {
            result.CandidateEvaluations = evaluations.ToArray();
            return result;
        }
        if (reasons.Count > 0 || pending.Length >= result.RemainingDonationCount)
        {
            result.CandidateEvaluations = evaluations.ToArray();
            return Block(result, reasons.Count > 0 ? reasons : new[] { "rusty_key_owned_supply_pending_donation_or_transfer" });
        }

        var context = acquisitionContext();
        if (context is null)
        {
            result.CandidateEvaluations = evaluations.ToArray();
            return Block(result, new[] { "rusty_key_rolling_acquisition_source_chain_required" });
        }
        if (context.RequirementInventorySha256 != inventorySha256 || context.SnapshotSha256 != snapshotSha256 ||
            context.StrategyLedgerSha256 != ledgerSha256 || context.Snapshot.StateHash != snapshot.StateHash ||
            context.LedgerState.Ledger.Revision != ledger.Revision)
        {
            result.CandidateEvaluations = evaluations.ToArray();
            return Block(result, new[] { "rusty_key_rolling_sources_changed_during_acquisition_rebuild" });
        }
        BuildAcquisitionStep(result, source, context, evaluations, routeTimingCalibrationPath);
        result.CandidateEvaluations = evaluations.ToArray();
        return result;
    }

    private static RustyKeyRollingSupplyProposal Block(RustyKeyRollingSupplyProposal result, IEnumerable<string> reasons)
    {
        result.BlockingReasons = reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return result;
    }

    private static Lazy<MuseumDonationRouteScheduleBuilder> DonationRouteScheduler(SnapshotEnvelope snapshot, string calibrationPath) =>
        new(() => new(snapshot, !string.IsNullOrWhiteSpace(calibrationPath) && File.Exists(calibrationPath)
            ? File.ReadAllText(calibrationPath) : string.Empty));
}
