using System.Text.Json;
using System.Text.Json.Nodes;
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
        return await PrepareCropTeacherOwnedSelectedInterruptAsync(http, options, iteration, owner, artifacts,
            snapshot, sourcePath, before, original, "story.advance_event", "story-source").ConfigureAwait(false);
    }
}
