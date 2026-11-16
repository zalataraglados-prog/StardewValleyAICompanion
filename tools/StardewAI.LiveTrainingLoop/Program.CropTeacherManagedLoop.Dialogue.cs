using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<ManagedCropControl> BuildCropTeacherOwnedDialogueRecoveryQueueAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherManagedSeedOwner owner)
    {
        owner.ValidateOriginalSupportingExecutor(options, watering: false);
        CropTeacherControlQueueBuildResult? build = null;
        var operation = await owner.CompileAndAdmitOriginalControlAsync(async () =>
        {
            var sourcePath = owner.CurrentSnapshotPath;
            var source = JsonNode.Parse(ContentAddressedJsonArtifactReader.ReadAllText(sourcePath))!.AsObject();
            if (HasPriorityStoryEventInterrupt(source) || !CropTeacherControlInterruptClassifier.HasSafeSpeakerlessDialogue(source))
                throw new InvalidDataException("Owned dialogue recovery cannot replace an unsafe or story-owned control.");
            var prefix = CropTeacherArtifactPrefix(options, iteration, "dialogue-recovery");
            var receipt = Path.Combine(options.SnapshotDir, "execution-" + iteration.ToString("D4") + ".json");
            var after = Path.Combine(options.SnapshotDir, "after-snapshot-" + iteration.ToString("D4") + ".json");
            if (new[] { prefix + "-model-plan.json", prefix + "-compiled-queue.json", receipt, after }
                .Any(path => File.Exists(path) || Directory.Exists(path)))
                throw new IOException("Owned dialogue recovery requires new artifact slots; recover retained control instead of overwriting it.");
            build = await BuildCropTeacherDialogueRecoveryQueueAsync(http, options, iteration, sourcePath, source,
                ReadString(source, "state_hash")).ConfigureAwait(false);
            return new("safe_dialogue_recovery", build.ModelPlanPath, prefix + "-compiled-queue.json", receipt, after,
                Path.Combine(owner.EvidenceDirectory, "native-" + iteration.ToString("D4")));
        }).ConfigureAwait(false);
        return new(build!, operation);
    }
}
