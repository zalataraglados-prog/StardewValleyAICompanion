using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public interface ICropTeacherSharedPortfolioPreparation
{
    IReadOnlyDictionary<string, string> InputHashes { get; }
    StrategyCommitmentLedger BaseLedger { get; }
    ReservationPortfolioCommitRequest PreparationRequest { get; }
    void ValidateInputsUnchanged();
    StrategyCommitmentLedger VerifyPreparationReply(ReservationPortfolioCommitResult reply);
    StrategyCommitmentLedger VerifyPreparationReference(string requestPath, string replyPath, string journalPath, string backendUrl);
}
