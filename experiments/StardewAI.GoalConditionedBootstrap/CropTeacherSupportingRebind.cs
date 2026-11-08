using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed class CropTeacherSupportingRebind
{
    private readonly SnapshotEnvelope after;
    private readonly StrategyCommitmentLedger ledger;
    private readonly ReservationPortfolioCommitRequest request;
    private readonly Action validate;
    private readonly string label;

    internal CropTeacherSupportingRebind(SnapshotEnvelope after, StrategyCommitmentLedger ledger, string stepId, Action validate, string label)
    {
        this.after = after; this.ledger = ledger; this.validate = validate; this.label = label;
        request = new()
        {
            StateHash = after.StateHash, ExpectedLedgerRevision = ledger.Revision,
            PortfolioId = stepId, SourceDecisionId = stepId, GoalId = "earn_money"
        };
    }

    internal StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(ledger);
    internal ReservationPortfolioCommitRequest Request
    {
        get { validate(); return CropTeacherSeedSupportSource.Copy(request); }
    }

    internal StrategyCommitmentLedger VerifyReply(ReservationPortfolioCommitResult reply)
    {
        validate();
        var reasons = new List<string>();
        Require(reply.Ledger is not null && ReservationPortfolioCommitEvidenceVerifier.ValidateMutation(
                request, ledger, reply.Ledger, after, reply, reasons),
            label + " rebind does not replay the original marker: " + string.Join(",", reasons));
        return CropTeacherSeedSupportSource.Copy(reply.Ledger!);
    }
}
