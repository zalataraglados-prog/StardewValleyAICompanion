using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherSeedLifecycleRuntimeOptions(string BackendUrl, string EvidenceDirectory,
    string RunId, string Mode, string CancellationReason = "");

public static partial class CropTeacherSeedLifecycleRuntime
{
    public const string CalendarCompletion = "calendar_completion";
    public const string ExplicitCancellation = "explicit_cancellation";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<StrategyLedgerEvidence> RunAsync(HttpClient http, CropTeacherSeedLifecycleRuntimeOptions options,
        CropTeacherSeedLifecycleSupport scope,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<string, string, Task<JsonObject>> post,
        Func<string, string, Task> write)
    {
        if (string.IsNullOrWhiteSpace(options.RunId) ||
            options.Mode is not (CalendarCompletion or ExplicitCancellation) ||
            (options.Mode == ExplicitCancellation ? string.IsNullOrWhiteSpace(options.CancellationReason) : options.CancellationReason != "") ||
            !Uri.TryCreate(options.BackendUrl, UriKind.Absolute, out var origin) || origin.Scheme is not ("http" or "https") ||
            origin.UserInfo.Length != 0 || origin.Query.Length != 0 || origin.Fragment.Length != 0 || origin.AbsolutePath != "/")
            throw new ArgumentException("Crop lifecycle requires an explicit mode, run, backend origin and cancellation-only reason.");
        scope.ValidateInputsUnchanged();
        var source = scope.BaseLedger;
        var cancel = options.Mode == ExplicitCancellation ? scope.BeginCancellation(options.CancellationReason) : null;
        var output = Path.GetFullPath(options.EvidenceDirectory);
        options = options with { BackendUrl = origin.AbsoluteUri.TrimEnd('/'), EvidenceDirectory = output };
        if (Directory.Exists(output) || File.Exists(output))
            throw new IOException("Crop lifecycle evidence already exists; inspect original journals instead of repeating mutation.");
        Directory.CreateDirectory(output);
        var diagnostic = new JsonObject
        {
            ["schema_version"] = "crop_teacher_seed_lifecycle_runtime.v1", ["run_id"] = options.RunId,
            ["mode"] = options.Mode, ["commitment_id"] = scope.CommitmentId, ["release_step_id"] = scope.ReleaseStepId,
            ["backend_url"] = options.BackendUrl, ["source_state_hash"] = source.SourceStateHash,
            ["source_snapshot_path"] = scope.SnapshotPath, ["base_ledger_revision"] = source.Revision,
            ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(scope.InputHashes, JsonOptions),
            ["cancellation_request"] = cancel is null ? null : JsonSerializer.SerializeToNode(cancel.Request, JsonOptions),
            ["crop_closure_verified"] = false, ["resource_release_verified"] = false,
            ["release_mutation_performed"] = false, ["execution_authorized"] = false,
            ["native_outcome_verified"] = false, ["formal_product_training_authorized"] = false
        };
        // Winning the original create-new intent owns this output and its summary, including concurrent starts.
        await write(Path.Combine(output, "lifecycle-intent.json"), diagnostic.ToJsonString(JsonOptions));
        Exception? failure = null;
        try
        {
            CropTeacherSeedRelease release;
            if (cancel is not null)
                release = await CancelOnceAsync(http, options, scope, cancel, diagnostic, post, write);
            else
            {
                scope.ValidateInputsUnchanged();
                var observed = await ReadAsync(http, options, source.SourceStateHash, "Crop calendar closure");
                diagnostic["completion_readback_path"] = observed.Path;
                release = scope.VerifyCompletionReadback(observed.Ledger);
            }
            await write(Path.Combine(output, "verified-closure-ledger.json"), JsonSerializer.Serialize(release.ClosedLedger, JsonOptions));
            diagnostic["crop_closure_verified"] = true;
            diagnostic["release_required"] = release.RequiresRelease;
            if (release.RequiresRelease)
            {
                var mutationDiagnostic = new JsonObject();
                diagnostic["release"] = mutationDiagnostic;
                var reply = await commit(mutationDiagnostic, release.Request);
                await write(Path.Combine(output, "release-reply.json"), reply.ToJsonString(JsonOptions));
                release.VerifyReply(reply.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)
                    ?? throw new InvalidDataException("Crop lifecycle release reply is empty."));
                diagnostic["release_mutation_performed"] = true;
            }
            var final = await ReadAsync(http, options, source.SourceStateHash, "Crop lifecycle final readback");
            release.VerifyReadback(final.Ledger);
            diagnostic["final_ledger_path"] = final.Path;
            diagnostic["final_ledger_revision"] = final.Ledger.Revision;
            diagnostic["resource_release_verified"] = true;
            diagnostic["status"] = "crop_seed_lifecycle_roundtrip_verified";
            return final;
        }
        catch (Exception error)
        {
            failure = error;
            diagnostic["status"] = "crop_seed_lifecycle_incomplete";
            diagnostic["error"] = error.Message;
            throw;
        }
        finally
        {
            await CropTeacherSeedRuntimeTransaction.WriteSummaryAsync(write,
                Path.Combine(output, "lifecycle-summary.json"), diagnostic, failure);
        }
    }

    private static Task<StrategyLedgerEvidence> ReadAsync(HttpClient http, CropTeacherSeedLifecycleRuntimeOptions options,
        string hash, string label) => StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl,
            options.EvidenceDirectory, hash, JsonOptions, label);
}
