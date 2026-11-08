using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;
using Artifacts = StardewAI.LiveTrainingLoop.CropSeedRuntimeArtifacts;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherSeedLifecycleRecoveryOptions(string BackendUrl, string RuntimeDirectory,
    string PortfolioJournalDirectory, string EvidenceDirectory, string RunId, string ExpectedIntentSha256,
    string Mode, string CancellationReason = "") : ICropSeedPortfolioRecoveryOptions;

public sealed class CropTeacherSeedLifecycleRecoveredBoundary
{
    private readonly StrategyCommitmentLedger ledger;
    private readonly Action validate;
    internal CropTeacherSeedLifecycleRecoveredBoundary(StrategyLedgerEvidence evidence, string snapshotPath,
        string releaseStepId, CropTeacherSeedLifecycleRecoveryOptions options, Action validate)
    {
        ledger = evidence.Ledger; LedgerPath = evidence.Path; SnapshotPath = snapshotPath;
        ReleaseStepId = releaseStepId; RunId = options.RunId; Mode = options.Mode;
        OriginalIntentSha256 = options.ExpectedIntentSha256; OriginalRuntimeDirectory = Path.GetFullPath(options.RuntimeDirectory);
        this.validate = validate;
    }
    public string LedgerPath { get; }
    public string SnapshotPath { get; }
    public string ReleaseStepId { get; }
    public string RunId { get; }
    public string Mode { get; }
    public string OriginalIntentSha256 { get; }
    public string OriginalRuntimeDirectory { get; }
    public void ValidateArtifactsUnchanged() => validate();
    public StrategyCommitmentLedger ReadLedger()
    {
        validate();
        return JsonSerializer.Deserialize<StrategyCommitmentLedger>(JsonSerializer.Serialize(ledger, JsonOptions), JsonOptions)!;
    }
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public static partial class CropTeacherSeedLifecycleRecovery
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<CropTeacherSeedLifecycleRecoveredBoundary> RecoverAsync(HttpClient http,
        CropTeacherSeedLifecycleRecoveryOptions value, CropTeacherSeedLifecycleSupport scope,
        Func<string, string, Task<JsonObject>> post, Func<string, string, Task> write)
    {
        var origin = CropTeacherSeedLifecycleRuntime.NormalizeBackendOrigin(value.BackendUrl);
        var options = value with { BackendUrl = origin, RuntimeDirectory = Path.GetFullPath(value.RuntimeDirectory),
            EvidenceDirectory = Path.GetFullPath(value.EvidenceDirectory) };
        var original = new CropTeacherSeedLifecycleRuntimeOptions(origin, options.RuntimeDirectory, options.RunId, options.Mode, options.CancellationReason);
        CropTeacherSeedLifecycleRuntime.ValidateOptions(original);
        if (options.ExpectedIntentSha256 is not { Length: 64 } || options.ExpectedIntentSha256.Any(value => !char.IsAsciiHexDigitLower(value)))
            throw new ArgumentException("Crop lifecycle recovery requires its pinned original intent SHA256.");
        scope.ValidateInputsUnchanged();
        var artifacts = new Artifacts(scope.InputHashes);
        var root = options.RuntimeDirectory;
        var output = options.EvidenceDirectory;
        var cancel = options.Mode == CropTeacherSeedLifecycleRuntime.ExplicitCancellation ? scope.BeginCancellation(options.CancellationReason) : null;
        var intentPath = Path.Combine(root, "lifecycle-intent.json");
        var intent = artifacts.Read<JsonObject>(intentPath);
        if (artifacts.Hashes[intentPath] != options.ExpectedIntentSha256 ||
            !JsonNode.DeepEquals(intent, CropTeacherSeedLifecycleRuntime.CreateIntent(original, scope, cancel, scope.BaseLedger)))
            throw new InvalidDataException("Crop lifecycle recovery differs from its pinned original identity.");
        if (Directory.Exists(output) || File.Exists(output) || CropTeacherSeedRuntimeRecovery.Contains(root, output) ||
            CropTeacherSeedRuntimeRecovery.Contains(output, root) || scope.InputHashes.Keys.Any(path => CropTeacherSeedRuntimeRecovery.Contains(output, path)))
            throw new IOException("Crop lifecycle recovery requires a new, separate evidence directory.");
        Directory.CreateDirectory(output);
        var diagnostic = new JsonObject
        {
            ["schema_version"] = "crop_teacher_seed_lifecycle_recovery.v1", ["run_id"] = options.RunId,
            ["runtime_directory"] = root, ["original_intent_sha256"] = options.ExpectedIntentSha256,
            ["release_step_id"] = scope.ReleaseStepId, ["mode"] = options.Mode,
            ["cancellation_resent"] = false, ["portfolio_mutation_requested"] = false,
            ["native_outcome_verified"] = false, ["execution_authorized"] = false, ["formal_product_training_authorized"] = false
        };
        await write(Path.Combine(output, "recovery-intent.json"), diagnostic.ToJsonString(JsonOptions));
        Exception? failure = null;
        try
        {
            var closed = artifacts.Read<StrategyCommitmentLedger>(Path.Combine(root, "verified-closure-ledger.json"));
            var release = cancel is null ? scope.VerifyCompletionReadback(closed) :
                await ReadCancellationAsync(original, scope, cancel, closed, artifacts);
            if (release.RequiresRelease)
            {
                var reply = await CropTeacherSeedRuntimeRecovery.ReadReplyAsync(options, origin, root, output, artifacts,
                    "release", "commit", release.Request, post, write);
                release.VerifyReply(reply.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)
                    ?? throw new InvalidDataException("Recovered crop release reply is empty."));
            }
            else if (File.Exists(Path.Combine(root, "release-reply.json")))
                throw new InvalidDataException("Crop lifecycle without owned resources has an unexpected release reply.");
            scope.ValidateInputsUnchanged();
            artifacts.Validate();
            // Original lookup/reply verification must precede a Get that may reconcile the ledger.
            var final = await StrategyLedgerEvidenceStore.ReadAsync(http, origin, output, closed.SourceStateHash,
                JsonOptions, "Recovered original crop lifecycle");
            release.VerifyReadback(final.Ledger);
            artifacts.Pin(final.Path);
            void Validate() { scope.ValidateInputsUnchanged(); artifacts.Validate(); }
            Validate();
            diagnostic["status"] = "original_crop_seed_lifecycle_recovered";
            diagnostic["final_ledger_path"] = final.Path;
            diagnostic["artifact_hashes"] = JsonSerializer.SerializeToNode(artifacts.Hashes, JsonOptions);
            return new(final, scope.SnapshotPath, scope.ReleaseStepId, options, Validate);
        }
        catch (Exception error)
        {
            failure = error;
            diagnostic["status"] = "original_crop_seed_lifecycle_recovery_incomplete";
            diagnostic["error"] = error.Message;
            throw;
        }
        finally
        {
            await CropTeacherSeedRuntimeTransaction.WriteSummaryAsync(write,
                Path.Combine(output, "recovery-summary.json"), diagnostic, failure);
        }
    }
}
