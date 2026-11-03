using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CollectionTeacherSupervisionReplayVerifier : IPolicyTeacherSupervisionReplayVerifier
{
    public const string ProviderId = "deterministic_teacher.current_stage_one_collection";
    public const string ProviderVersion = "v1";
    public string TeacherId => ProviderId;
    public string TeacherVersion => ProviderVersion;
    private readonly CurrentCommunityCenterDenominatorReport? suppliedDenominator;

    public CollectionTeacherSupervisionReplayVerifier() { }

    internal CollectionTeacherSupervisionReplayVerifier(CurrentCommunityCenterDenominatorReport denominator)
    {
        suppliedDenominator = denominator;
    }

    public PolicyDecisionTrajectoryEnvelope Rebuild(PolicyDecisionTrajectoryEnvelope claimed)
    {
        var sources = claimed.Audit?.TeacherSupervision?.Sources
            ?? throw new InvalidDataException("Independent Teacher source artifacts are missing.");
        var denominator = suppliedDenominator ?? CurrentCommunityCenterDenominatorBuilder.Build(
            sources.RequirementInventoryPath, sources.BeforeSnapshotPath);
        var replay = CurrentStageOneCollectionTeacherReceiptBuilder.Build(
            sources.RequirementInventoryPath, sources.AcquisitionLoweringPath,
            sources.RankingPath, sources.BeforeSnapshotPath, sources.TargetDateIntentsPath,
            sources.PreferencePath, sources.ExecutionReceiptPath, sources.AfterSnapshotPath,
            claimed.TrajectoryId, claimed.RunId, claimed.Versions.KnowledgeDictionary,
            claimed.Versions.Executor, denominator, sources.StrategyLedgerPath);
        if (replay.Status != "ready" || !replay.TeacherTrainingRowEligible || replay.TrainingRow is null)
            throw new InvalidDataException("Independent Teacher replay rejected its source evidence: " +
                string.Join(",", replay.BlockingReasons));
        return replay.TrainingRow;
    }
}
