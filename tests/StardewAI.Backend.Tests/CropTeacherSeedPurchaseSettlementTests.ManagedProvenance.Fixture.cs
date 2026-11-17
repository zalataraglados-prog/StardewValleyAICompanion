using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    // Accepted-record fixtures only. This consumer does not replay a whole native production cycle.
    private sealed record ManagedProvenanceCase(string Path, MoneyRevenueCropManagedProvenanceRequest Request,
        string SourcePath, PlanExecutionEpisodeEnvelope Episode, string HistoryOnlyPath);

    private static async Task<ManagedProvenanceCase> CreateManagedProvenanceCase(SavedReadbackOwnerFixture f, CropTeacherManagedSeedOwner owner)
    {
        var (_, binding, episode, sourcePath) = await CreateReadbackSourceBinding(f, owner, metadataRefresh: true);
        episode.AfterSnapshotFresh = true; episode.AfterStateHash = episode.SourceStateHash;
        episode.AfterSnapshotPath = sourcePath; episode.AfterGameTick = episode.BeforeGameTick;
        Fixture.Save(binding.ContinuationEpisodePath, episode);
        var headerPath = Path.Combine(f.Night.Options.EvidenceDirectory, "owner-intent.json");
        var header = JsonNode.Parse(File.ReadAllText(headerPath))!;
        var initialSourcePath = header["initial_snapshot_path"]!.GetValue<string>();
        var initialSource = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(initialSourcePath), JsonOptions)!;
        var dayZero = FreshSaveBoundarySnapshotFixture.Create();
        dayZero.SaveId = initialSource.SaveId; dayZero.PlayerId = initialSource.PlayerId;
        var dayZeroPath = Path.Combine(f.Witness.Root, "day-zero.json"); Fixture.Save(dayZeroPath, dayZero);
        var boundaryPath = Path.Combine(f.Witness.Root, "day-zero-boundary.json");
        Fixture.Save(boundaryPath, GoldenWalnutFreshSaveInitialBoundaryValidator.Build(dayZeroPath, binding.RunId, dayZero.StateHash));
        var openingPath = Path.Combine(f.Witness.Root, "opening-episode.json");
        Fixture.Save(openingPath, new PlanExecutionEpisodeEnvelope
        {
            EpisodeId = "declared-opening-observation", RunId = binding.RunId, SourceStateHash = dayZero.StateHash,
            BeforeGameTick = dayZero.GameTick, BeforeSnapshotPath = dayZeroPath,
            AfterStateHash = initialSource.StateHash, AfterGameTick = initialSource.GameTick,
            AfterSnapshotPath = initialSourcePath, AfterSnapshotFresh = true,
            ChangedFacts = JsonSerializer.SerializeToElement(Array.Empty<object>())
        });
        var hashes = binding.AcceptedSavedReadbacks.SelectMany(reference => reference.InputArtifactHashes)
            .GroupBy(pair => pair.Key).ToDictionary(group => group.Key, group => group.First().Value);
        foreach (var path in new[] { headerPath, initialSourcePath }) hashes[path] = SiteTillNativeFixture.FileHash(path);
        var historyOnly = Path.Combine(f.Witness.Root, "frozen-history-only.json"); Fixture.Save(historyOnly, new { retained = true });
        hashes[historyOnly] = SiteTillNativeFixture.FileHash(historyOnly);
        var closeoutPath = Path.Combine(f.Witness.Root, "accepted-managed-closeout.json");
        var loaded = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(f.LoadedPath), JsonOptions)!;
        Fixture.Save(closeoutPath, new
        {
            schema_version = "crop_teacher_managed_owner_resume.v1", run_id = binding.RunId, owner_directory = f.Night.Options.EvidenceDirectory,
            snapshot_path = f.LoadedPath, state_hash = loaded.StateHash, native_saved_content_readback_verified = true,
            fresh_accepted_head_verified = true, complete_managed_runtime_verified = true, terminal_settlement_verified = true,
            native_dispatch_performed = false, teacher_preference_label_eligible = false, formal_product_training_authorized = false,
            saved_terminal_closeout = new { nativeSnapshotPath = binding.NativeAfterSnapshotPath, currentSnapshotPath = f.LoadedPath,
                readbackAcceptancePath = binding.AcceptedSavedReadbacks[^1].AcceptancePath }, input_artifact_hashes = hashes
        });
        var request = new MoneyRevenueCropManagedProvenanceRequest("money_revenue_crop_managed_provenance_request.v1", binding.RunId,
            new("managed_saved_closeout", closeoutPath, SiteTillNativeFixture.FileHash(closeoutPath)),
            new("fresh_save_initial_boundary", boundaryPath, SiteTillNativeFixture.FileHash(boundaryPath)), openingPath,
            binding.AcceptedSavedReadbacks, [Path.GetDirectoryName(binding.ContinuationEpisodePath)!]);
        var requestPath = Path.Combine(f.Witness.Root, "managed-provenance-request.json"); Fixture.Save(requestPath, request);
        return new(requestPath, request, sourcePath, episode, historyOnly);
    }

    private static LiveTrainingOptions ManagedProvenanceOptions(ManagedProvenanceCase c, string root)
    {
        var options = new LiveTrainingOptions { Root = root, ManifestPath = Path.Combine(root, "run.json"), RunId = c.Request.RunId,
            ManagedCropSourceProvenanceRequestPath = c.Path, ArtifactRetentionMode = "stop", SkipTraining = true };
        Directory.CreateDirectory(options.SnapshotDir);
        return options;
    }
}
