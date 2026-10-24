using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static GoldenWalnutFreshSaveMilestoneAssessment
        EvaluateGoldenWalnutSourceMilestone(string snapshotJson)
    {
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(
            snapshotJson,
            JsonOptions) ?? throw new InvalidDataException(
            "golden_walnut_source_milestone_snapshot_invalid");
        return GoldenWalnutFreshSaveMilestoneEvaluator.Evaluate(snapshot);
    }

    private static async Task<GoldenWalnutFreshSaveMilestoneAssessment>
        EvaluateGoldenWalnutSourceMilestoneArtifactAsync(string path)
    {
        var snapshotJson =
            await ContentAddressedJsonArtifactStore.ReadAllTextAsync(path);
        return EvaluateGoldenWalnutSourceMilestone(snapshotJson);
    }

    private static string GoldenWalnutSourceMilestoneSignature(
        GoldenWalnutFreshSaveMilestoneAssessment assessment) =>
        string.Join("|", assessment.BlockingReasons);

    private static void AppendGoldenWalnutSourceMilestoneProgress(
        LiveTrainingOptions options,
        GoldenWalnutFreshSaveMilestoneAssessment assessment,
        int iteration,
        string stateHash,
        string queueId)
    {
        AppendProgress(
            options,
            assessment.Ready
                ? "golden_walnut_source_milestone_ready"
                : "golden_walnut_source_milestone_waiting",
            iteration,
            stateHash,
            queueId,
            "tracked_collected_quantity=" +
            assessment.TrackedCollectedQuantity +
            " blockers=" + string.Join(",", assessment.BlockingReasons));
    }
}
