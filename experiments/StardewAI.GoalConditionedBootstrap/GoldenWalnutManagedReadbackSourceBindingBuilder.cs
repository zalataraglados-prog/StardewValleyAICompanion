using System.Collections.ObjectModel;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static class GoldenWalnutManagedReadbackSourceBindingBuilder
{
    public static GoldenWalnutManagedReadbackSourceBinding Build(string requestPath)
    {
        using var fresh = InvocationReadSession.Start(Array.Empty<string>(), enabled: false);
        requestPath = Path.GetFullPath(requestPath);
        var request = Read<GoldenWalnutManagedReadbackSourceBindingRequest>(requestPath);
        Require(request.SchemaVersion == "golden_walnut_managed_readback_source_binding_request.v1" &&
            !string.IsNullOrWhiteSpace(request.RunId) && request.AcceptedSavedReadbacks is { Length: > 0 },
            "Request requires the original run and accepted saved readback references.");
        var root = Path.GetDirectoryName(requestPath)!;
        var nativePath = Resolve(root, request.NativeAfterSnapshotPath);
        var episodePath = Resolve(root, request.ContinuationEpisodePath);
        var episode = Read<PlanExecutionEpisodeEnvelope>(episodePath);
        Require(episode.SchemaVersion == "plan_execution_episode.v1" && episode.RunId == request.RunId &&
            !string.IsNullOrWhiteSpace(episode.EpisodeId), "Continuation episode has another schema/run or no identity.");
        var sourcePath = Resolve(Path.GetDirectoryName(episodePath)!, episode.BeforeSnapshotPath);
        var accepted = Read<JsonElement>(request.AcceptedSavedReadbacks[^1].AcceptancePath);
        var loadedPath = accepted.GetProperty("after_snapshot_path").GetString() ??
            throw new InvalidDataException("Accepted readback has no original loaded snapshot.");
        Require(Path.IsPathFullyQualified(loadedPath), "Accepted loaded snapshot path is not absolute.");
        var retained = new CropTeacherRetainedInputs(new[] { requestPath, nativePath, episodePath, sourcePath, loadedPath }
            .Distinct(CropTeacherSeedTransferNativeOutcome.PathComparer).ToArray());

        // Verify the exact original load first; a later observation is not that native load.
        var boundary = CropTeacherAcceptedSavedReadbackVerifier.Verify(nativePath, loadedPath,
            request.RunId, request.AcceptedSavedReadbacks);
        var loaded = Read<SnapshotEnvelope>(loadedPath);
        var source = Read<SnapshotEnvelope>(sourcePath);
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(source, "managed readback continuation");
        Require(source.StateHash == episode.SourceStateHash && source.GameTick == episode.BeforeGameTick &&
            source.GameTick >= loaded.GameTick, "Continuation source differs from its original episode or reverses the loaded clock.");
        CropTeacherSavedResourceReadback.VerifySnapshotContent(loaded, source);
        // These in-memory views describe an observation boundary, never a fabricated native episode.
        GoldenWalnutFreshSaveEpisodeContinuityVerifier.Validate(loadedPath,
            new PlanExecutionEpisodeEnvelope { AfterStateHash = loaded.StateHash, AfterGameTick = loaded.GameTick }, loaded,
            episodePath, episode, source);

        var hashes = retained.ReadHashes().ToDictionary(pair => pair.Key, pair => pair.Value,
            CropTeacherSeedTransferNativeOutcome.PathComparer);
        foreach (var reference in request.AcceptedSavedReadbacks)
            foreach (var (path, hash) in reference.InputArtifactHashes)
            {
                var full = Path.GetFullPath(path);
                Require(!hashes.TryGetValue(full, out var prior) || prior == hash, "Original readback input binding changed.");
                hashes[full] = hash;
            }
        retained.Validate();
        return new(requestPath, request.RunId, episode.EpisodeId, source.SaveId.Value!, source.PlayerId.Value!, source.GameVersion,
            loadedPath, sourcePath, loaded.StateHash, source.StateHash, loaded.GameTick, source.GameTick,
            boundary.PersistentStateSha256, boundary.AcceptanceSha256s, new ReadOnlyDictionary<string, string>(hashes));
    }

    private static T Read<T>(string path) => CurrentTeacherFrontierSupport.Read<T>(path, "Managed readback source binding");
    private static string Resolve(string root, string path) => string.IsNullOrWhiteSpace(path)
        ? throw new InvalidDataException("Managed readback source artifact path is missing.")
        : Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException("Managed readback source binding: " + message);
    }
}
