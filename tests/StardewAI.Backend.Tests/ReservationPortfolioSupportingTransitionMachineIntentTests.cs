using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    private const string AcquisitionMachineSource =
        "{\"goal_id\":\"goal.grandpa_21\",\"route_occurrence_id\":\"route:machine-output\",\"route_kind\":\"machine_output\",\"source_id\":\"machine:12\",\"output_qualified_item_id\":\"(O)340\",\"machine_qualified_item_id\":\"(BC)12\"}";

    [Fact]
    public void MachineCapacitySettlementRebindsOnlyTheExactActiveIntent()
    {
        var snapshot = Snapshot();
        var service = new ReservationPortfolioLedgerService();
        var committed = service.Commit(
            null,
            snapshot,
            MachineCapacityCommitRequest(snapshot),
            "2026-09-27T00:00:00Z");
        Assert.True(committed.Accepted,
            string.Join(";", committed.Errors));

        var result = service.SettleSupportingTransition(
            committed.Ledger,
            snapshot,
            MachineCapacitySettlementRequest(snapshot),
            "2026-09-27T00:01:00Z");

        Assert.True(result.Accepted, string.Join(";", result.Errors));
        Assert.Equal(2, result.CommittedLedgerRevision);
        Assert.Equal("machine-support:route:machine-output",
            result.ReboundMachineSupportIntentId);
        Assert.Empty(result.MaterialSettlements);
        Assert.Empty(result.CurrencySettlements);
        var intent = Assert.Single(result.Ledger!.MachineSupportIntents);
        Assert.Equal(2, intent.Revision);
        Assert.Equal(snapshot.StateHash, intent.SourceStateHash);
        Assert.Equal(MachineSupportIntentStages.CraftSelected, intent.Stage);
        Assert.Equal(AcquisitionMachineSource, intent.SupportSourcesJson);
        Assert.Equal(2, result.Ledger.History.Count(row =>
            row.LedgerRevision == 2));
        Assert.Contains(result.Ledger.History, row =>
            row.CommitmentId == intent.IntentId &&
            row.SourceDecisionId == "candidate:craft-machine" &&
            row.Operation ==
                "machine_support_intent_rebound_after_supporting_transition");
    }

    [Fact]
    public void MachineCapacitySettlementRejectsSourceDriftAtomically()
    {
        var snapshot = Snapshot();
        var service = new ReservationPortfolioLedgerService();
        var committed = service.Commit(
            null,
            snapshot,
            MachineCapacityCommitRequest(snapshot),
            "2026-09-27T00:00:00Z");
        var request = MachineCapacitySettlementRequest(snapshot);
        request.MachineSupportSourcesJson =
            AcquisitionMachineSource.Replace(
                "route:machine-output",
                "route:wrong",
                StringComparison.Ordinal);

        var result = service.SettleSupportingTransition(
            committed.Ledger,
            snapshot,
            request,
            "2026-09-27T00:01:00Z");

        Assert.False(result.Accepted);
        Assert.Contains(
            "supporting_transition_machine_intent_marker_mismatch",
            result.Errors);
        Assert.Same(committed.Ledger, result.Ledger);
        Assert.Equal(1, Assert.Single(
            result.Ledger!.MachineSupportIntents).Revision);
    }

    private static ReservationPortfolioCommitRequest
        MachineCapacityCommitRequest(
            StardewAI.Contracts.State.SnapshotEnvelope snapshot) => new()
        {
            StateHash = snapshot.StateHash,
            PortfolioId = "support:route-machine",
            GoalId = "goal.grandpa_21",
            SourceDecisionId = "support:route-machine",
            MachineSupportIntent = new MachineSupportIntentUpsertRequest
            {
                StateHash = snapshot.StateHash,
                IntentId = "machine-support:route:machine-output",
                Stage = MachineSupportIntentStages.CraftSelected,
                SourceDecisionId = "candidate:craft-machine",
                GoalId = "goal.grandpa_21",
                QualifiedItemId = "(BC)12",
                ItemId = "12",
                DemandClass = "acquisition_route_requirement",
                SupportKind = "machine_capacity_acquisition_route",
                EvidenceStatus = AcquisitionMachineSource,
                SupportSourcesJson = AcquisitionMachineSource,
                SupportScore = 0.12,
                RequiredAdditionalMachineCount = 1
            }
        };

    private static ReservationPortfolioSupportingTransitionSettlementRequest
        MachineCapacitySettlementRequest(
            StardewAI.Contracts.State.SnapshotEnvelope snapshot) => new()
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = 1,
            PortfolioId = "support:route-machine",
            GoalId = "goal.grandpa_21",
            RouteSourceDecisionId = "candidate:craft-machine",
            SupportingTransitionReceiptSha256 = new string('c', 64),
            MachineSupportIntentId =
                "machine-support:route:machine-output",
            MachineSupportIntentStage =
                MachineSupportIntentStages.CraftSelected,
            MachineSupportSourcesJson = AcquisitionMachineSource,
            Reason = "verified_machine_capacity_craft"
        };
}
