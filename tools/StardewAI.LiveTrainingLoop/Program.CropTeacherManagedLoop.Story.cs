using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<ManagedCropControl> BuildCropTeacherOwnedStoryEventQueueAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherManagedSeedOwner owner)
    {
        var artifacts = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
        var sourcePath = owner.CurrentSnapshotPath;
        var snapshot = artifacts.Read<SnapshotEnvelope>(sourcePath);
        artifacts.Pin(owner.CurrentLedgerPath);
        var before = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(sourcePath))!.AsObject();
        if (!HasPriorityStoryEventInterrupt(before))
            throw new InvalidOperationException("Owned story selection requires its original active event boundary.");
        await IngestCropTeacherManagedSnapshotAsync(http, options, sourcePath, artifacts, snapshot).ConfigureAwait(false);
        var original = await BuildCropTeacherStoryEventQueueAsync(http, options, iteration,
            sourcePath, before, snapshot.StateHash).ConfigureAwait(false);
        var plan = artifacts.Read<SmallModelPlanEnvelope>(original.ModelPlanPath);
        var ranking = artifacts.Read<JsonObject>(original.RankingPath);
        var lease = SelectedQueueDecisionLease.CreateFromPlan(plan, ranking);
        var selected = lease.CandidateAt(0);
        if (selected.OptionId != "story.advance_event")
            throw new InvalidDataException("Owned story selection cannot replace another control owner.");
        // The interrupt has its own choice, not a replacement for the retained crop day plan.
        var selection = new CropTeacherDailySelection(ReadSnapshotEnvelopeInt(before, "time", "total_days"),
            plan.GoalId, lease, new HashSet<string>(StringComparer.Ordinal), original.ModelPlanPath, original.RankingPath)
        {
            SourceSnapshotPath = sourcePath
        };
        var prepared = await PrepareCropTeacherSelectedOtherGoalAsync(http, options, iteration,
            snapshot, selection, selected, owner).ConfigureAwait(false);
        if (prepared.OtherGoalPreparation is not { QueueStatus: "pending", SourceSelection: { } source } work)
            throw new InvalidDataException("Original story selection is not dispatchable: " + prepared.BlockReason);
        var queue = artifacts.Read<JsonObject>(work.QueuePath);
        artifacts.Validate();
        var operation = await owner.AdmitNativeSharedSourceYieldAsync(source,
            Path.Combine(options.SnapshotDir, $"execution-{iteration:D4}.json"),
            Path.Combine(options.SnapshotDir, $"after-snapshot-{iteration:D4}.json"),
            Path.Combine(owner.EvidenceDirectory, $"story-source-{iteration:D4}")).ConfigureAwait(false);
        var build = StandardResult(queue, original.ModelPlanPath, original.RankingPath,
            sourcePath, before, snapshot.StateHash, options, interruptKind: "priority_story_event") with
        {
            SnapshotProfile = "full",
            SnapshotUrl = SnapshotUrlForProfile(options.BridgeSnapshotUrl, "full", forceRefresh: true)
        };
        return new(build, operation) { SharedSelection = selection };
    }
}
