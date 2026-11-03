using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyAcquisitionPortfolioBuilder
{
    private const string MuseumSetId = "museum_collection";
    private const int StageOneDeadlineExclusive = 224;

    public static RustyKeyAcquisitionPortfolioAdmission Build(AcquisitionRoutePortfolioInputs inputs) =>
        InvocationDeterministicBuildSession.Observe(
            "hard_admission_certificate", "RustyKeyAcquisitionPortfolioBuilder.Build", "v1",
            InvocationDeterministicBuildSession.Describe(inputs), () =>
            {
                var context = AcquisitionRoutePortfolioBuilder.Prepare(inputs);
                var proposal = CurrentTeacherFrontierSupport.Read<AcquisitionRoutePortfolioProposal>(
                    inputs.ProposalPath, "Rusty Key acquisition portfolio proposal");
                return Build(context, proposal, CurrentTeacherFrontierSupport.HashFile(inputs.ProposalPath));
            });

    internal static RustyKeyAcquisitionPortfolioAdmission Build(
        AcquisitionRoutePortfolioBuilder.AcquisitionRoutePortfolioBuildContext context,
        AcquisitionRoutePortfolioProposal proposal,
        string proposalSha256)
    {
        var snapshot = context.Snapshot;
        var result = new RustyKeyAcquisitionPortfolioAdmission
        {
            GoalId = context.Inventory.GoalId,
            SnapshotStateHash = snapshot.StateHash, SnapshotSha256 = context.SnapshotSha256,
            RequirementInventorySha256 = context.RequirementInventorySha256,
            StrategyLedgerSha256 = context.StrategyLedgerSha256, ProposalSha256 = proposalSha256,
            SourceSaveId = snapshot.SaveId.Value ?? string.Empty,
            SourcePlayerId = snapshot.PlayerId.Value ?? string.Empty,
            TargetTotalDay = context.Opportunity.TargetTotalDay
        };
        var reasons = new List<string>();
        if (snapshot.SchemaVersion != "snapshot.v1" || snapshot.GameVersion != context.Inventory.GameVersion ||
            snapshot.SaveId.Status != FieldStatus.Available || string.IsNullOrWhiteSpace(snapshot.SaveId.Value) ||
            snapshot.PlayerId.Status != FieldStatus.Available || string.IsNullOrWhiteSpace(snapshot.PlayerId.Value) ||
            snapshot.StateHash != SnapshotHash.ComputeStateHash(snapshot.State))
            reasons.Add("rusty_key_snapshot_identity_or_hash_invalid");
        if (proposal.GoalId != context.Inventory.GoalId ||
            (proposal.GoalId != GoalMethodTeacherCoverageGoalIds.Authoritative &&
             proposal.GoalId != GoalMethodTeacherCoverageGoalIds.AcquisitionCorpus) ||
            proposal.SnapshotStateHash != snapshot.StateHash || string.IsNullOrWhiteSpace(proposal.ProposalId) ||
            proposal.ExpectedLedgerRevision != context.LedgerState.Ledger.Revision)
            reasons.Add("rusty_key_proposal_identity_invalid");
        if (proposal.CompletedAlternatives is not { Length: 0 } ||
            !string.IsNullOrEmpty(proposal.PriorRolloutCheckpointSha256) ||
            !string.IsNullOrEmpty(proposal.PriorSupportingTransitionReplanSha256))
            reasons.Add("rusty_key_fresh_scope_cannot_claim_unverified_continuation");
        if (!TryCurrentTotalDay(snapshot, out var sourceDay))
            reasons.Add("rusty_key_current_total_day_unavailable");
        result.SourceTotalDay = sourceDay;
        var museum = context.Inventory.RequirementSets.Single(value => value.RequirementSetId == MuseumSetId);
        ValidateMuseumRequirementShape(museum);
        var museumAvailable = TryMuseumValue(snapshot, out var value);
        var progress = museumAvailable
            ? CurrentCollectionTeacherFrontierBuilder.ReadMuseumProgressValue(value, museum)
            : CurrentCollectionProgress.Unavailable("museum_transparent_state_unavailable");
        if (!progress.Available)
            reasons.AddRange(progress.BlockingReasons);
        if (!museumAvailable ||
            !value.TryGetProperty("rusty_key_donation_threshold", out var threshold) ||
            threshold.ValueKind != JsonValueKind.Number || !threshold.TryGetInt32(out var required) || required != 60 ||
            ReadString(value, "rusty_key_reward_id") != "museum60" ||
            ReadString(value, "rusty_key_reward_action") != "MarkEventSeen Host 295672")
        {
            reasons.Add("rusty_key_live_reward_authority_invalid");
            return Block(result, reasons);
        }
        result.DonationThreshold = required;
        result.DonatedCount = progress.AggregateCompletedCount;
        result.RemainingDonationCount = Math.Max(0, required - result.DonatedCount);
        var selected = ValidateSelectedScope(context, proposal, museum, progress, reasons);
        result.CompleteMuseumItemDenominator = museum.Groups.OrderBy(group => group.RequirementId, StringComparer.Ordinal)
            .Select(group => new RustyKeyAcquisitionItem(group.RequirementId, group.Alternatives[0].QualifiedItemId,
                progress.Available && progress.Requirements[group.RequirementId].Completed,
                selected.Contains(group.RequirementId))).ToArray();
        if (selected.Count != result.RemainingDonationCount)
            reasons.Add("rusty_key_selected_distinct_items_do_not_equal_live_deficit");
        if (result.RemainingDonationCount == 0 &&
            (proposal.SelectedRouteOccurrenceIds is { Length: > 0 } || proposal.ReplacedRouteOccurrenceIds is { Length: > 0 }))
            reasons.Add("rusty_key_threshold_satisfied_cannot_schedule_more_acquisition");
        if (reasons.Count > 0)
            return Block(result, reasons);
        if (result.RemainingDonationCount == 0)
        {
            result.ThresholdSatisfied = true;
            result.Status = "donation_threshold_satisfied_native_key_handoff_still_required";
            return result;
        }
        // Acquisition must leave at least the following morning before Year 3; this
        // necessary bound does not certify the remaining donation/key event schedule.
        if (result.TargetTotalDay < result.SourceTotalDay || result.TargetTotalDay >= StageOneDeadlineExclusive - 1)
            return Block(result, new[] { "rusty_key_supply_date_leaves_no_pre_year3_key_handoff" });
        result.AcquisitionPortfolio = AcquisitionRoutePortfolioBuilder.Build(context, proposal, proposalSha256);
        if (!result.AcquisitionPortfolio.PortfolioAdmissionReady)
            return Block(result, result.AcquisitionPortfolio.BlockingReasons);
        result.AcquisitionPortfolioReady = true;
        result.Status = "admitted_rusty_key_threshold_acquisition_pending_commit_and_native_donation";
        return result;
    }

    private static RustyKeyAcquisitionPortfolioAdmission Block(
        RustyKeyAcquisitionPortfolioAdmission result, IEnumerable<string> reasons)
    {
        result.BlockingReasons = reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return result;
    }

    private static bool TryMuseumValue(SnapshotEnvelope snapshot, out JsonElement value)
    {
        value = default;
        return snapshot.State.TryGetValue("world_progress", out var world) && world.ValueKind == JsonValueKind.Object &&
            world.TryGetProperty("museum", out var field) && field.ValueKind == JsonValueKind.Object &&
            ReadString(field, "status") is FieldStatus.Available or FieldStatus.Derived &&
            field.TryGetProperty("value", out value) &&
            value.ValueKind == JsonValueKind.Object;
    }

    private static bool TryCurrentTotalDay(SnapshotEnvelope snapshot, out int totalDay)
    {
        totalDay = 0;
        return snapshot.State.TryGetValue("time", out var time) && time.ValueKind == JsonValueKind.Object &&
            time.TryGetProperty("total_days", out var field) && field.ValueKind == JsonValueKind.Object &&
            ReadString(field, "status") == FieldStatus.Available && field.TryGetProperty("value", out var value) &&
            value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out totalDay) && totalDay >= 0;
    }

    private static string ReadString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String
            ? field.GetString() ?? string.Empty : string.Empty;
}
