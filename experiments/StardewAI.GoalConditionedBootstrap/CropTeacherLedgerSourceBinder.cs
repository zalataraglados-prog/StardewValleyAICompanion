using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static class CropTeacherLedgerSourceBinder
{
    public static void ValidateCurrent(string snapshotPath, string ledgerPath)
    {
        var (snapshot, state) = ReadContext(snapshotPath, ledgerPath);
        Require(state.Ledger.SourceStateHash == snapshot.StateHash,
            "Crop Teacher ledger is not bound to the current source.");
    }

    public static ReservationPortfolioCommitRequest BuildRefreshRequest(
        string snapshotPath, string ledgerPath, string decisionId)
    {
        var (snapshot, state) = ReadContext(snapshotPath, ledgerPath);
        return RefreshRequest(snapshot, state.Ledger, decisionId);
    }

    public static StrategyCommitmentLedger VerifyRefreshResult(
        string snapshotPath, string ledgerPath, ReservationPortfolioCommitRequest request,
        ReservationPortfolioCommitResult result)
    {
        var (snapshot, state) = ReadContext(snapshotPath, ledgerPath);
        Require(EqualJson(request, RefreshRequest(snapshot, state.Ledger, request.SourceDecisionId)),
            "Crop Teacher ledger refresh request changed its exact marker-only contract.");
        var reasons = new List<string>();
        Require(result.Ledger is not null && result.Ledger.SourceStateHash == snapshot.StateHash,
            "Crop Teacher refreshed ledger is not bound to the source.");
        Require(ReservationPortfolioCommitEvidenceVerifier.ValidateMutation(
                request, state.Ledger, result.Ledger!, snapshot, result, reasons),
            "Crop Teacher ledger refresh did not replay exactly: " + string.Join(",", reasons));
        return result.Ledger!;
    }

    private static ReservationPortfolioCommitRequest RefreshRequest(
        SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger, string decisionId)
    {
        Require(!string.IsNullOrWhiteSpace(decisionId) && ledger.SourceStateHash != snapshot.StateHash,
            "Crop Teacher ledger refresh requires a new source and explicit decision identity.");
        return new ReservationPortfolioCommitRequest
        {
            StateHash = snapshot.StateHash, ExpectedLedgerRevision = ledger.Revision,
            PortfolioId = "crop-teacher-ledger-refresh:" + decisionId,
            GoalId = "earn_money", SourceDecisionId = decisionId
        };
    }

    private static (SnapshotEnvelope, AcquisitionStrategyLedgerState) ReadContext(
        string snapshotPath, string ledgerPath)
    {
        using var document = JsonDocument.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(snapshotPath));
        var snapshot = document.RootElement.Deserialize<SnapshotEnvelope>(JsonDefaults.Options)
            ?? throw new InvalidDataException("Crop Teacher ledger source is empty.");
        Require(snapshot.SchemaVersion == "snapshot.v1" && snapshot.GameVersion == "1.6.15" &&
                snapshot.StateHash == SnapshotHash.ComputeStateHash(snapshot.State),
            "Crop Teacher ledger source integrity is invalid.");
        var state = AcquisitionStrategyLedgerReader.Read(ledgerPath, snapshot);
        Require(!state.Ledger.CropPlantingCommitments.Any(row => row.Status == StrategyCommitmentStatuses.Active),
            "Crop Teacher must continue an active crop's complete existing commitment and layout.");
        var resources = AcquisitionResourceInputSnapshotState.Read(document.RootElement);
        AcquisitionReservationSupplyValidator.Validate(state, resources, resources.ShopQuotes);
        return (snapshot, state);
    }
}
