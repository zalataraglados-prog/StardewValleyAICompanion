using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeShopSaleEpisodeVerifier
{
    internal static VerifiedNativeShopSaleEpisode Verify(string episodePath)
    {
        var fullEpisodePath = RequiredFile(
            episodePath,
            "Native shop-sale execution episode");
        var episode = CurrentTeacherFrontierSupport.Read<
            PlanExecutionEpisodeEnvelope>(
            fullEpisodePath,
            "Native shop-sale execution episode");
        ValidateEpisodeHeader(episode);
        var beforePath = RequiredFile(
            episode.BeforeSnapshotPath,
            "Native shop-sale before snapshot");
        var afterPath = RequiredFile(
            episode.AfterSnapshotPath,
            "Native shop-sale after snapshot");
        var resultPath = RequiredFile(
            episode.ExecutionResultPath,
            "Native shop-sale execution result");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            beforePath,
            "Native shop-sale before snapshot");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterPath,
            "Native shop-sale after snapshot");
        var result = CurrentTeacherFrontierSupport.Read<TrainingExecutionResult>(
            resultPath,
            "Native shop-sale execution result");
        ValidateEpisodeBinding(episode, before, after, result);
        var progression =
            MoneyRevenueProgressionVerifier.VerifyImmediateShopSale(
                before,
                after);
        var command = ValidateQueueItem(
            episode,
            result,
            progression.TotalMoneyEarnedDelta);
        ValidateExecutionResult(
            result,
            episode,
            progression.Before.Money,
            progression.After.Money);
        return new VerifiedNativeShopSaleEpisode(
            fullEpisodePath,
            CurrentTeacherFrontierSupport.HashFile(fullEpisodePath),
            beforePath,
            CurrentTeacherFrontierSupport.HashFile(beforePath),
            afterPath,
            CurrentTeacherFrontierSupport.HashFile(afterPath),
            resultPath,
            CurrentTeacherFrontierSupport.HashFile(resultPath),
            episode.RunId,
            episode.EpisodeId,
            result.QueueItemId,
            command.ShopId,
            command.QualifiedItemId,
            command.SlotIndex,
            command.Quantity,
            command.UnitPrice,
            progression);
    }

}

internal sealed record VerifiedNativeShopSaleEpisode(
    string ExecutionEpisodePath,
    string ExecutionEpisodeSha256,
    string BeforeSnapshotPath,
    string BeforeSnapshotSha256,
    string AfterSnapshotPath,
    string AfterSnapshotSha256,
    string ExecutionResultPath,
    string ExecutionResultSha256,
    string RunId,
    string EpisodeId,
    string QueueItemId,
    string ShopId,
    string QualifiedItemId,
    int SlotIndex,
    int Quantity,
    int UnitPrice,
    MoneyRevenueTransitionEvidence Progression);
