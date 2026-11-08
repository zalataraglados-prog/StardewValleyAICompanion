using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public interface ICropTeacherSupportingTransition
{
    string AfterSnapshotPath { get; }
    StrategyCommitmentLedger BaseLedger { get; }
    ReservationPortfolioCommitRequest RebindRequest { get; }
    IReadOnlyDictionary<string, string> InputHashes { get; }
    StrategyCommitmentLedger VerifyRebindReply(ReservationPortfolioCommitResult reply);
    void ValidateInputsUnchanged();
}
