using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task IngestCropTeacherManagedSnapshotAsync(HttpClient http, LiveTrainingOptions options,
        string path, CropSeedRuntimeArtifacts artifacts)
    {
        var snapshot = artifacts.Read<SnapshotEnvelope>(path);
        var reply = await PostJsonStringAsync(http, SnapshotIngestUrl(options, "full"),
            ContentAddressedJsonArtifactReader.ReadAllText(path)).ConfigureAwait(false);
        if (reply["accepted"]?.GetValue<bool>() != true || reply["state_hash"]?.GetValue<string>() != snapshot.StateHash)
            throw new InvalidDataException("Managed snapshot ingest did not accept its exact retained state hash.");
        artifacts.Validate();
    }
}
