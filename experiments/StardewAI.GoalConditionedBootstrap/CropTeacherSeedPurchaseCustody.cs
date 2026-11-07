using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.GoalConditionedBootstrap.CropTeacherSeedPurchaseSupport;

namespace StardewAI.GoalConditionedBootstrap;

// Retains one verified settlement instead of revalidating a mutable HTTP reply for every next operation.
public sealed class CropTeacherSeedPurchaseCustody
{
    private readonly string commitmentId;
    private readonly StrategyCommitmentLedger settled;
    private readonly SnapshotEnvelope after;
    private readonly ReservationPortfolioCommitRequest? acquisition;
    private readonly Action validateArtifacts;

    internal CropTeacherSeedPurchaseCustody(string commitmentId, StrategyCommitmentLedger settled, SnapshotEnvelope after,
        ReservationPortfolioCommitRequest? acquisition, Action validateArtifacts)
    {
        this.commitmentId = commitmentId;
        this.settled = Copy(settled);
        this.after = after;
        this.acquisition = acquisition;
        this.validateArtifacts = validateArtifacts;
    }

    public StrategyCommitmentLedger Ledger => Copy(settled);

    public ReservationPortfolioCommitRequest AcquiredSeedRequest
    {
        get
        {
            validateArtifacts();
            Require(acquisition is not null, "Crop seed support has no verified acquired-seed delta.");
            return Copy(acquisition!);
        }
    }

    public StrategyCommitmentLedger VerifyAcquiredSeedReply(ReservationPortfolioCommitResult result)
    {
        validateArtifacts();
        Require(acquisition is not null, "Crop seed support has no verified acquired-seed delta.");
        var reasons = new List<string>();
        Require(result.Ledger is not null && ReservationPortfolioCommitEvidenceVerifier.ValidateMutation(
                acquisition!, settled, result.Ledger, after, result, reasons),
            "Crop acquired-seed claim does not replay exactly: " + string.Join(",", reasons));
        CropTeacherOwnedSeedSupply.Read(after, result.Ledger!, new[] { commitmentId });
        return Copy(result.Ledger!);
    }
}
