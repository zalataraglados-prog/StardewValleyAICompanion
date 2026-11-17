using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class MoneyRevenueCropManagedProvenance
{
    private static StringComparer Comparer => CropTeacherSeedTransferNativeOutcome.PathComparer;
    private readonly Dictionary<string, string> direct = new(Comparer);
    private readonly Dictionary<string, string> snapshotLogicalHashes = new(Comparer);
    private readonly Dictionary<string, string> inherited;
    private readonly CropTeacherRetainedInputs retained;
    private readonly MoneyRevenueCropManagedProvenanceRequest request;
    private readonly string requestPath, nativePath, loadedPath;
    private readonly SnapshotEnvelope loaded;
    private readonly JsonObject summary;

    public string RunId => request.RunId;

    public static MoneyRevenueCropManagedProvenance Prepare(string requestPath, string? expectedRunId = null) => new(requestPath, expectedRunId);

    private MoneyRevenueCropManagedProvenance(string path, string? expectedRunId)
    {
        using var fresh = InvocationReadSession.Start(Array.Empty<string>(), enabled: false);
        requestPath = Path.GetFullPath(path);
        request = Read<MoneyRevenueCropManagedProvenanceRequest>(Pin(requestPath));
        Require(request.SchemaVersion == "money_revenue_crop_managed_provenance_request.v1" &&
            !string.IsNullOrWhiteSpace(request.RunId) && request.AcceptedSavedReadbacks is { Length: > 0 } &&
            request.PriorEpisodeDirectories is not null && request.PriorEpisodeDirectories.All(path =>
                Path.IsPathFullyQualified(path) && Directory.Exists(path)), "Managed provenance request is incomplete.");
        Require(expectedRunId is null || expectedRunId == RunId, "Managed provenance has another authorized run ID.");
        var closeoutPath = Accepted(request.AcceptedManagedCloseout, "managed_saved_closeout");
        var closeout = Read<JsonObject>(closeoutPath);
        Require(Text(closeout, "schema_version") == "crop_teacher_managed_owner_resume.v1" && Text(closeout, "run_id") == RunId &&
            Flag(closeout, "native_saved_content_readback_verified") && Flag(closeout, "fresh_accepted_head_verified") &&
            Flag(closeout, "complete_managed_runtime_verified") && Flag(closeout, "terminal_settlement_verified") &&
            !Flag(closeout, "native_dispatch_performed") && !Flag(closeout, "teacher_preference_label_eligible") &&
            !Flag(closeout, "formal_product_training_authorized"), "Managed provenance requires an already accepted saved closeout, not manual/partial evidence.");
        var inheritedInputs = closeout["input_artifact_hashes"]!.Deserialize<Dictionary<string, string>>(JsonDefaults.Options)!;
        Require(inheritedInputs.Count > 0 && inheritedInputs.All(pair => Path.IsPathFullyQualified(pair.Key) && IsLowerSha256(pair.Value)),
            "Accepted managed closeout input bindings are incomplete.");
        inherited = inheritedInputs.ToDictionary(pair => Path.GetFullPath(pair.Key), pair => pair.Value, Comparer);
        var saved = closeout["saved_terminal_closeout"]!.AsObject();
        nativePath = Bound(Text(saved, "nativeSnapshotPath"));
        loadedPath = Bound(Text(saved, "currentSnapshotPath"));
        Require(Same(Text(closeout, "snapshot_path"), loadedPath), "Managed closeout current head differs.");
        loaded = Read<SnapshotEnvelope>(loadedPath);
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(loaded, "managed provenance loaded head");
        Require(loaded.StateHash == Text(closeout, "state_hash"), "Accepted loaded state differs.");
        var headerPath = Bound(Path.Combine(Text(closeout, "owner_directory"), "owner-intent.json"));
        var header = Read<JsonObject>(headerPath);
        Require(Text(header, "schema_version") == "crop_teacher_managed_seed_owner.v1" && Text(header, "run_id") == RunId,
            "Accepted owner header has another identity.");
        var cycleSourcePath = Bound(Text(header, "initial_snapshot_path"));
        var cycleSource = Read<SnapshotEnvelope>(cycleSourcePath);
        ValidateActor(cycleSource);
        var boundaryPath = Accepted(request.InitialBoundary, "fresh_save_initial_boundary");
        var boundary = Read<GoldenWalnutFreshSaveInitialBoundary>(boundaryPath);
        var dayZero = GoldenWalnutFreshSaveInitialBoundaryValidator.Validate(boundary, boundaryPath, RunId);
        ValidateActor(dayZero);
        Pin(Resolve(boundaryPath, boundary.SnapshotPath));
        var openingPath = Pin(request.OpeningEpisodePath);
        var opening = ReadEpisode(openingPath);
        Require(opening.AfterSnapshotFresh && opening.SourceStateHash == dayZero.StateHash && opening.BeforeGameTick == dayZero.GameTick,
            "Opening witness does not start at the original Day0 boundary.");
        var openingBefore = Snapshot(openingPath, opening.BeforeSnapshotPath, opening.SourceStateHash, opening.BeforeGameTick);
        var openingAfter = Snapshot(openingPath, opening.AfterSnapshotPath, opening.AfterStateHash, opening.AfterGameTick);
        Require(opening.AfterGameTick >= opening.BeforeGameTick && openingBefore.StateHash == dayZero.StateHash,
            "Opening witness reverses its original clock.");
        Observe(openingPath, opening, openingAfter, cycleSourcePath,
            new PlanExecutionEpisodeEnvelope { SourceStateHash = cycleSource.StateHash, BeforeGameTick = cycleSource.GameTick }, cycleSource);
        var acceptancePath = Bound(Text(saved, "readbackAcceptancePath"));
        Require(Same(request.AcceptedSavedReadbacks[^1].AcceptancePath, acceptancePath), "Readback is not the accepted closeout's original load.");
        foreach (var reference in request.AcceptedSavedReadbacks)
            foreach (var (input, hash) in reference.InputArtifactHashes)
                Require(inherited.TryGetValue(Path.GetFullPath(input), out var expected) && expected == hash,
                    "Readback input is outside the accepted closeout.");
        // The accepted report already seals its historical closure. Do not rematerialize every historical frame.
        retained = new CropTeacherRetainedInputs(direct.Keys.Where(IsJson).ToArray());
        var observed = retained.ReadHashes();
        foreach (var (input, expected) in inherited)
            Require((observed.TryGetValue(input, out var actual) ? actual : RegularHash(input)) == expected, "Accepted managed input changed: " + input);
        summary = new JsonObject
        {
            ["schema_version"] = "money_revenue_crop_managed_provenance.v1", ["source_run_id"] = RunId,
            ["request_path"] = requestPath, ["accepted_managed_closeout"] = JsonSerializer.SerializeToNode(request.AcceptedManagedCloseout, JsonDefaults.Options),
            ["initial_boundary_path"] = boundaryPath, ["opening_episode_path"] = openingPath,
            ["prior_complete_managed_runtime_report_reused"] = true, ["initial_day_zero_boundary_verified"] = true,
            ["inherited_input_reference_count"] = inherited.Count, ["production_replayed"] = false,
            ["inherited_reference_verification"] = "physical_hashes_of_accepted_closure",
            ["complete_managed_runtime_verified"] = false, ["source_schedule_verified"] = false, ["global_route_verified"] = false,
            ["native_dispatch_performed"] = false, ["teacher_preference_label_eligible"] = false, ["formal_product_training_authorized"] = false
        };
    }

    private string Accepted(MoneyRevenueArtifactBinding binding, string role)
    {
        Require(binding is not null && binding.Role == role && binding.Sha256 is not null && IsLowerSha256(binding.Sha256), "Accepted provenance reference requires its role and original SHA256.");
        var path = Pin(binding!.Path); Require(direct[path] == binding.Sha256, "Accepted provenance reference hash changed."); return path;
    }
    private string Bound(string path)
    {
        path = Path.GetFullPath(path);
        Require(inherited.TryGetValue(path, out var expected), "Closeout omitted an original source binding.");
        Pin(path); Require(direct[path] == expected, "Original closeout source changed."); return path;
    }
    private string Pin(string path)
    {
        Require(Path.IsPathFullyQualified(path), "Provenance input path must be absolute.");
        path = Path.GetFullPath(path); var hash = RegularHash(path);
        Require(!direct.TryGetValue(path, out var prior) || prior == hash, "Provenance input changed during assembly.");
        direct[path] = hash; return path;
    }
    private static string RegularHash(string path)
    {
        Require(File.Exists(path), "Provenance input is missing.");
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
            Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Provenance input traverses a reparse point.");
        return CurrentTeacherFrontierSupport.HashFile(path);
    }
    private void ValidateActor(SnapshotEnvelope snapshot)
    {
        MoneyRevenueCropSeedAcquisitionProofBuilder.ValidateSnapshot(snapshot, "managed provenance");
        Require(snapshot.SaveId.Value == loaded.SaveId.Value && snapshot.PlayerId.Value == loaded.PlayerId.Value &&
            snapshot.GameVersion == loaded.GameVersion, "Managed provenance crossed save/player/version.");
    }
    private SnapshotEnvelope Snapshot(string episodePath, string path, string hash, long tick)
    {
        var value = ReadSnapshot(Resolve(episodePath, path));
        Require(value.StateHash == hash && value.GameTick == tick, "Original episode snapshot identity differs.");
        return value;
    }
    private SnapshotEnvelope ReadSnapshot(string path)
    {
        path = Pin(path);
        var text = ContentAddressedJsonArtifactReader.ReadAllText(path);
        var logicalHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        Require(!snapshotLogicalHashes.TryGetValue(path, out var prior) || prior == logicalHash, "Snapshot logical content changed.");
        snapshotLogicalHashes[path] = logicalHash;
        var value = JsonSerializer.Deserialize<SnapshotEnvelope>(text, JsonDefaults.Options) ?? throw new InvalidDataException("Empty provenance snapshot.");
        ValidateActor(value);
        return value;
    }
    private PlanExecutionEpisodeEnvelope ReadEpisode(string path)
    {
        var value = Read<PlanExecutionEpisodeEnvelope>(path);
        Require(value.SchemaVersion == "plan_execution_episode.v1" && value.RunId == RunId && !string.IsNullOrWhiteSpace(value.EpisodeId),
            "Provenance episode has another run/schema or no identity."); return value;
    }
    private static void Observe(string beforePath, PlanExecutionEpisodeEnvelope before, SnapshotEnvelope beforeSnapshot,
        string afterPath, PlanExecutionEpisodeEnvelope after, SnapshotEnvelope afterSnapshot)
    {
        Require(after.BeforeGameTick >= before.AfterGameTick, "Observation clock reversed without an accepted readback.");
        Require(beforeSnapshot.InGameTime.Value == afterSnapshot.InGameTime.Value, "Observation crossed unreceipted game time.");
        GoldenWalnutFreshSaveEpisodeContinuityVerifier.Validate(beforePath, before, beforeSnapshot, afterPath, after, afterSnapshot);
        if (beforeSnapshot.StateHash != afterSnapshot.StateHash) CropTeacherSavedResourceReadback.VerifySnapshotContent(beforeSnapshot, afterSnapshot);
    }
    private static string Resolve(string ownerPath, string artifact) => Path.GetFullPath(Path.IsPathRooted(artifact)
        ? artifact : Path.Combine(Path.GetDirectoryName(ownerPath)!, artifact));
    private static bool IsJson(string path) => path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    private static T Read<T>(string path) => CurrentTeacherFrontierSupport.Read<T>(path, "Managed provenance");
    private static string Text(JsonObject value, string name) => value[name]?.GetValue<string>() ?? throw new InvalidDataException("Managed provenance missing " + name);
    private static bool Flag(JsonObject value, string name) => value[name]?.GetValue<bool>() ?? throw new InvalidDataException("Managed provenance missing " + name);
    private static bool Same(string first, string second) => Comparer.Equals(Path.GetFullPath(first), Path.GetFullPath(second));
}
