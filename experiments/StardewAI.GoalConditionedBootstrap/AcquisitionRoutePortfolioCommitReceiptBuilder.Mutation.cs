using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioCommitReceiptBuilder
{
    private static bool ValidateMutation(
        AcquisitionRoutePortfolioAdmission admission,
        StrategyCommitmentLedger baseLedger,
        StrategyCommitmentLedger committed,
        SnapshotEnvelope snapshot,
        string? commitResultPath,
        ICollection<string> reasons)
    {
        if (string.IsNullOrWhiteSpace(commitResultPath))
        {
            reasons.Add("atomic_commit_result_required");
            return false;
        }
        var result = CurrentTeacherFrontierSupport.Read<
            ReservationPortfolioCommitResult>(
            Path.GetFullPath(commitResultPath),
            "Reservation portfolio commit result");
        return ReservationPortfolioCommitEvidenceVerifier.ValidateMutation(
            admission.AtomicCommitRequest!,
            baseLedger,
            committed,
            snapshot,
            result,
            reasons);
    }
}
