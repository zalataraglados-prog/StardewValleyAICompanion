using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static class CropTeacherDeveloperSavedSourceRebind
{
    // Marker replay only: this grants no saved-world, native-load or training certificate.
    public static StrategyCommitmentLedger Verify(ReservationPortfolioCommitRequest request,
        StrategyCommitmentLedger before, SnapshotEnvelope snapshot, ReservationPortfolioCommitResult reply)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(reply);
        if (request.StateHash != snapshot.StateHash || request.MaterialClaims is not { Length: 0 } ||
            request.CurrencyClaims is not { Length: 0 } || request.ReleaseReservationIds is not { Length: 0 } ||
            request.MaterialConsumption is not null || request.MaterialTransferPreparation is not null ||
            request.InventoryConsumptionPreparation is not null || request.MachineSupportIntent is not null || reply.Ledger is null)
            throw new InvalidDataException("Developer saved-source marker must not change resource reservations or consumption.");
        var reasons = new List<string>();
        if (!ReservationPortfolioCommitEvidenceVerifier.ValidateMutation(request, before, reply.Ledger, snapshot, reply, reasons))
            throw new InvalidDataException("Developer saved-source marker does not replay: " + string.Join(",", reasons));
        return CropTeacherSeedSupportSource.Copy(reply.Ledger);
    }
}
