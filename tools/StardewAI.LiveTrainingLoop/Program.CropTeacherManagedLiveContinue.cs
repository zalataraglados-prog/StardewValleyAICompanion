using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private sealed record ManagedLiveGame(int ProcessId, string ExecutablePath, DateTimeOffset StartedAt);

    private static ManagedLiveGame ReadManagedLiveGame(JsonObject session, LiveTrainingOptions options)
    {
        var games = (session["processes"] as JsonArray)?.OfType<JsonObject>()
            .Where(row => row["role"]?.GetValue<string>() == "game").ToArray() ?? [];
        if (session["run_id"]?.GetValue<string>() != options.RunId ||
            session["slot"]?.GetValue<string>() != options.SaveSlot ||
            !ManagedResumeSamePath(session["save_root"]?.GetValue<string>() ?? "", options.SaveIsolationPath) || games.Length != 1)
            throw new InvalidDataException("Live session is not this original run/save/native actor.");
        var game = games[0];
        var executable = game["exe"]?.GetValue<string>() ?? "";
        if (game["id"]?.GetValue<int>() is not > 0 || !Path.IsPathFullyQualified(executable) ||
            !Path.GetFileNameWithoutExtension(executable).Equals("StardewModdingAPI", StringComparison.OrdinalIgnoreCase) ||
            !DateTimeOffset.TryParse(game["started"]?.GetValue<string>(), out var started))
            throw new InvalidDataException("Live session lacks the exact original native process identity.");
        return new(game["id"]!.GetValue<int>(), executable, started);
    }

    private static void RequireManagedLiveProcess(Process process, ManagedLiveGame expected)
    {
        if (process.HasExited || process.Id != expected.ProcessId ||
            process.StartTime.ToUniversalTime() != expected.StartedAt.UtcDateTime ||
            process.MainModule?.FileName is not { } executable || !ManagedResumeSamePath(executable, expected.ExecutablePath))
            throw new InvalidDataException("Same-live continuation lost its original native process.");
    }

    private static async Task<CropTeacherManagedContinuation> ContinueLiveCropTeacherManagedOwnerAsync(HttpClient http, LiveTrainingOptions options)
    {
        options.ValidateManagedCropTeacherLiveContinueMode();
        options.BackendUrl = CropTeacherSeedLifecycleRuntime.NormalizeBackendOrigin(options.BackendUrl);
        var artifacts = new CropSeedRuntimeArtifacts(new Dictionary<string, string>());
        var session = artifacts.Read<JsonObject>(options.ManagedCropLiveSessionPath);
        var expected = ReadManagedLiveGame(session, options);
        var process = Process.GetProcessById(expected.ProcessId);
        CropTeacherManagedSeedOwner? owner = null;
        try
        {
            RequireManagedLiveProcess(process, expected);
            var binding = new CropTeacherNativeSaveBinding(options.SaveIsolationPath, options.SaveSlot);
            owner = await CropTeacherManagedOwnerResume.OpenAsync(options.ResumeManagedCropOwnerDirectory, options.RunId,
                options.PolicyTrajectoryExecutorVersion, options.BackendUrl, options.SnapshotDir,
                binding, WritePortfolioJournalFileAsync).ConfigureAwait(false);
            if (owner.Pending is not null)
                throw new InvalidOperationException("Finish the original pending operation explicitly before same-live Main continuation.");
            var output = Path.Combine(owner.EvidenceDirectory, "live-continue-entry-" + Guid.NewGuid().ToString("N"));
            owner.ValidateResumeOutputDirectory(output);
            Directory.CreateDirectory(output);
            var restored = await RestoreCropTeacherManagedOwnerWithReportAsync(http, options, owner, binding,
                Path.Combine(output, "restore")).ConfigureAwait(false);
            var endpoint = new UriBuilder(options.BridgeSnapshotUrl) { Query = "profile=full&fresh=1" };
            var text = await http.GetStringAsync(endpoint.Uri).ConfigureAwait(false);
            var snapshotPath = Path.Combine(output, "same-live-snapshot.json");
            await WritePortfolioJournalFileAsync(snapshotPath, text).ConfigureAwait(false);
            var live = artifacts.Read<SnapshotEnvelope>(snapshotPath);
            RequireManagedLiveProcess(process, expected);
            var source = owner.ReadLiveContinuationSource(live, expected.StartedAt);
            artifacts.Validate();
            var reportPath = Path.Combine(output, "same-live-continuation.json");
            await WritePortfolioJournalFileAsync(reportPath, new JsonObject
            {
                ["schema_version"] = "same_live_crop_owner_continuation.v1", ["run_id"] = options.RunId,
                ["owner_directory"] = owner.EvidenceDirectory, ["state_hash"] = live.StateHash,
                ["game_tick"] = live.GameTick, ["original_native_plan_path"] = source.NativeRunPlanPath,
                ["session_path"] = options.ManagedCropLiveSessionPath, ["snapshot_path"] = snapshotPath,
                ["process_id"] = expected.ProcessId, ["process_started_at_utc"] = expected.StartedAt.ToString("O"),
                ["executable_path"] = expected.ExecutablePath,
                ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(artifacts.Hashes, JsonOptions),
                ["live_process_bound"] = true, ["accepted_world_matched"] = true,
                ["entry_native_dispatch_performed"] = false, ["native_reload_performed"] = false,
                ["saved_content_readback_verified"] = false, ["formal_product_training_authorized"] = false
            }.ToJsonString(JsonOptions)).ConfigureAwait(false);
            RequireManagedLiveProcess(process, expected);
            artifacts.Validate();
            return new(owner, source, process, restored with { ReportPath = reportPath });
        }
        catch
        {
            process.Dispose();
            if (owner is not null) await owner.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
