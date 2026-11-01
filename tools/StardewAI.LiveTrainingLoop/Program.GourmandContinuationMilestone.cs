using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static GoldenWalnutGourmandContinuationMilestoneAssessment
        EvaluateGourmandContinuationMilestone(string snapshotJson)
    {
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(
            snapshotJson,
            JsonOptions) ?? throw new InvalidDataException(
            "golden_walnut_gourmand_continuation_snapshot_invalid");
        return GoldenWalnutGourmandContinuationMilestoneEvaluator.Evaluate(
            snapshot);
    }

    private static async Task<
        GoldenWalnutGourmandContinuationMilestoneAssessment>
        EvaluateGourmandContinuationMilestoneArtifactAsync(string path)
    {
        var snapshotJson =
            await ContentAddressedJsonArtifactStore.ReadAllTextAsync(path);
        return EvaluateGourmandContinuationMilestone(snapshotJson);
    }

    private static string GourmandContinuationMilestoneSignature(
        GoldenWalnutGourmandContinuationMilestoneAssessment assessment) =>
        assessment.RequestsFulfilled + "|" +
        assessment.CollectedGourmandWalnutQuantity + "|" +
        string.Join("|", assessment.BlockingReasons);

    private static void AppendGourmandContinuationMilestoneProgress(
        LiveTrainingOptions options,
        GoldenWalnutGourmandContinuationMilestoneAssessment assessment,
        int iteration,
        string stateHash,
        string queueId)
    {
        AppendProgress(
            options,
            assessment.Ready
                ? "golden_walnut_gourmand_continuation_ready"
                : "golden_walnut_gourmand_continuation_waiting",
            iteration,
            stateHash,
            queueId,
            "requests_fulfilled=" + assessment.RequestsFulfilled +
            " collected_quantity=" +
            assessment.CollectedGourmandWalnutQuantity +
            " blockers=" + string.Join(",", assessment.BlockingReasons));
    }
}
