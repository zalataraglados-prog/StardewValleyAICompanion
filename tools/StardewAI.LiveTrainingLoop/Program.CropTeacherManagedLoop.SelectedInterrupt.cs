using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<ManagedCropControl> PrepareCropTeacherOwnedSelectedInterruptAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherManagedSeedOwner owner, CropSeedRuntimeArtifacts artifacts,
        SnapshotEnvelope snapshot, string sourcePath, JsonObject before, CropTeacherControlQueueBuildResult original,
        string expectedOptionId, string runtimePrefix)
    {
        var plan = artifacts.Read<SmallModelPlanEnvelope>(original.ModelPlanPath);
        var ranking = artifacts.Read<JsonObject>(original.RankingPath);
        if (plan.Steps.Length == 0)
            throw new InvalidDataException("Managed crop control interrupt has no original dispatchable choice.");
        var lease = SelectedQueueDecisionLease.CreateFromPlan(plan, ranking);
        var selected = lease.CandidateAt(0);
        if (selected.OptionId != expectedOptionId)
            throw new InvalidDataException("Owned interrupt selection cannot replace another control owner.");
        // The interrupt owns its choice; it does not replace the retained crop day plan.
        var selection = new CropTeacherDailySelection(ReadSnapshotEnvelopeInt(before, "time", "total_days"),
            plan.GoalId, lease, new HashSet<string>(StringComparer.Ordinal), original.ModelPlanPath, original.RankingPath)
        {
            SourceSnapshotPath = sourcePath
        };
        var prepared = await PrepareCropTeacherSelectedOtherGoalAsync(http, options, iteration,
            snapshot, selection, selected, owner).ConfigureAwait(false);
        if (prepared.OtherGoalPreparation is not { QueueStatus: "pending", SourceSelection: { } source } work)
            throw new InvalidDataException("Original control interrupt selection is not dispatchable: " + prepared.BlockReason);
        var queue = artifacts.Read<JsonObject>(work.QueuePath);
        artifacts.Validate();
        var operation = await owner.AdmitNativeSharedSourceYieldAsync(source,
            Path.Combine(options.SnapshotDir, $"execution-{iteration:D4}.json"),
            Path.Combine(options.SnapshotDir, $"after-snapshot-{iteration:D4}.json"),
            Path.Combine(owner.EvidenceDirectory, $"{runtimePrefix}-{iteration:D4}")).ConfigureAwait(false);
        var build = StandardResult(queue, original.ModelPlanPath, original.RankingPath,
            sourcePath, before, snapshot.StateHash, options, interruptKind: original.InterruptKind) with
        {
            SnapshotProfile = "full",
            SnapshotUrl = SnapshotUrlForProfile(options.BridgeSnapshotUrl, "full", forceRefresh: true)
        };
        return new(build, operation) { SharedSelection = selection };
    }
}
