using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherSeedRelease
{
    private readonly CropTeacherSeedLifecycleSupport scope;
    private readonly StrategyCommitmentLedger closed;
    private readonly ReservationPortfolioCommitRequest request;
    private StrategyCommitmentLedger? final;

    internal CropTeacherSeedRelease(CropTeacherSeedLifecycleSupport scope, StrategyCommitmentLedger closed)
    {
        this.scope = scope;
        this.closed = CropTeacherSeedSupportSource.Copy(closed);
        var crop = this.closed.CropPlantingCommitments.Single(row => row.CommitmentId == scope.Crop.CommitmentId);
        Require(this.closed.PreparedMaterialTransfer is null && this.closed.SourceStateHash == scope.Snapshot.StateHash &&
                crop.Status is StrategyCommitmentStatuses.Cancelled or StrategyCommitmentStatuses.Completed,
            "Crop seed release requires verified closure without pending transfer.");
        var ids = ValidateOwnedClaims(this.closed, crop);
        request = new ReservationPortfolioCommitRequest
        {
            StateHash = scope.Snapshot.StateHash, ExpectedLedgerRevision = this.closed.Revision,
            PortfolioId = scope.ReleaseStepId, SourceDecisionId = crop.SourceDecisionId, GoalId = "earn_money",
            ReleaseReservationIds = ids
        };
        if (!RequiresRelease) final = this.closed;
    }

    public bool RequiresRelease => request.ReleaseReservationIds.Length > 0;
    public StrategyCommitmentLedger ClosedLedger => CropTeacherSeedSupportSource.Copy(closed);
    public ReservationPortfolioCommitRequest Request
    {
        get
        {
            scope.ValidateInputsUnchanged();
            Require(RequiresRelease, "Crop seed lifecycle has no active resources to release; do not send an empty mutation.");
            return CropTeacherSeedSupportSource.Copy(request);
        }
    }

    public StrategyCommitmentLedger VerifyReply(ReservationPortfolioCommitResult result)
    {
        scope.ValidateInputsUnchanged();
        Require(RequiresRelease && result.Accepted && result.Errors is { Length: 0 } && result.Ledger is not null,
            "Crop seed release reply is rejected/incomplete: " + string.Join(",", result.Errors ?? Array.Empty<string>()));
        var reasons = new List<string>();
        Require(ReservationPortfolioCommitEvidenceVerifier.ValidateMutation(request, closed, result.Ledger!, scope.Snapshot, result, reasons),
            "Crop seed release complete reply does not replay exactly: " + string.Join(",", reasons));
        final = CropTeacherSeedSupportSource.Copy(result.Ledger!);
        return CropTeacherSeedSupportSource.Copy(final);
    }

    public StrategyCommitmentLedger VerifyReadback(StrategyCommitmentLedger observed)
    {
        scope.ValidateInputsUnchanged();
        Require(final is not null, "Crop seed lifecycle release is not verified; recover its original portfolio reply first.");
        AcquisitionStrategyLedgerReader.Read(observed, scope.Snapshot);
        Require(EqualJson(StrategyCommitmentReadReconciler.Reconcile(final!, scope.Snapshot, observed.UpdatedAt), observed),
            "Crop seed lifecycle final readback drifted.");
        return CropTeacherSeedSupportSource.Copy(observed);
    }

    internal static string[] ValidateOwnedClaims(StrategyCommitmentLedger ledger, CropPlantingCommitment crop)
    {
        var seeds = ledger.MaterialReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active &&
            row.Purpose == "crop_seed_supply:" + crop.CommitmentId).ToArray();
        var cashId = crop.CommitmentId + ":seed-purchase:money";
        var cash = ledger.CurrencyReservations.Where(row => row.Status == StrategyCommitmentStatuses.Active &&
            (row.ReservationId == cashId || row.Purpose == "crop_seed_purchase:" + crop.CommitmentId)).ToArray();
        Require(seeds.All(row => row.SourceDecisionId == crop.SourceDecisionId && row.GoalId == "earn_money" &&
                    row.QualifiedItemId == crop.SeedQualifiedItemId && row.OutputCustody is null) &&
                cash.Length <= 1 && cash.All(row => row.ReservationId == cashId && row.SourceDecisionId == crop.SourceDecisionId &&
                    row.GoalId == "earn_money" && row.CurrencyId == 0 && row.Purpose == "crop_seed_purchase:" + crop.CommitmentId),
            "Crop seed lifecycle resources have a foreign owner/goal/item/purpose.");
        return seeds.Select(row => row.ReservationId).Concat(cash.Select(row => row.ReservationId))
            .OrderBy(id => id, StringComparer.Ordinal).ToArray();
    }
}
