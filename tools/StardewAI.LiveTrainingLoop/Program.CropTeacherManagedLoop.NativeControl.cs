using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private sealed record ManagedNativeControlSettle(StageOneCollectionTeacherQueueBuildResult Build,
        CropTeacherManagedSeedOperation Operation);

    private static async Task<ManagedNativeControlSettle?> PrepareCropTeacherOwnedNativeControlSettleAsync(
        LiveTrainingOptions options, int iteration, CropTeacherManagedSeedOwner owner)
    {
        owner.ValidateOriginalRouteExecutor(options);
        if (owner.Pending is not null || owner.HeadRecoveryRequired)
            throw new InvalidOperationException("Recover the retained crop phase before selecting another control observation.");
        var source = JsonSerializer.Deserialize<SnapshotEnvelope>(
            ContentAddressedJsonArtifactReader.ReadAllText(owner.CurrentSnapshotPath), JsonOptions)!;
        if (NativeControlSettleRecovery.TryCreate(source) is null) return null;
        StageOneCollectionTeacherQueueBuildResult? build = null;
        var operation = await owner.CompileAndAdmitOriginalRouteAsync(async () =>
        {
            var sourcePath = owner.CurrentSnapshotPath;
            var json = ContentAddressedJsonArtifactReader.ReadAllText(sourcePath);
            var accepted = JsonSerializer.Deserialize<SnapshotEnvelope>(json, JsonOptions)!;
            var prefix = Path.Combine(options.SnapshotDir, "native-control-settle-");
            string Artifact(string kind) => prefix + kind + "-" + iteration.ToString("D4") + ".json";
            var receipt = Path.Combine(options.SnapshotDir, "execution-" + iteration.ToString("D4") + ".json");
            var after = Path.Combine(options.SnapshotDir, "after-snapshot-" + iteration.ToString("D4") + ".json");
            if (new[] { Artifact("preference"), Artifact("compiled-plan"), Artifact("ranking"), Artifact("compiled-queue"), receipt, after }
                .Any(path => File.Exists(path) || Directory.Exists(path)))
                throw new IOException("Owned control observation requires new artifact slots; recover retained work instead of overwriting it.");
            build = await TryBuildNativeControlSettleQueueAsync(options, json, accepted.StateHash, sourcePath,
                Artifact("preference"), Artifact("compiled-plan"), Artifact("ranking"), Artifact("compiled-queue")).ConfigureAwait(false);
            if (build?.Queue is null)
                throw new InvalidDataException("Original native-control observation is not dispatchable: " + build?.Detail);
            // The original route proof already verifies this exact observation without claiming movement.
            return new("supporting_route_to_sleep", build.PlanPath, build.QueuePath, receipt, after,
                Path.Combine(owner.EvidenceDirectory, "native-" + iteration.ToString("D4")), NativeControlObservation: true);
        }).ConfigureAwait(false);
        return new(build!, operation);
    }
}
