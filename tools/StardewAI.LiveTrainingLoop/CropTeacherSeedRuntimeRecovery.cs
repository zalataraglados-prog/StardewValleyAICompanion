using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherSeedRuntimeRecoveryOptions(string BackendUrl, string RuntimeDirectory,
    string PortfolioJournalDirectory, string EvidenceDirectory, string RunId, string ExecutorVersion,
    string ExpectedIntentSha256);

public sealed class CropTeacherSeedRecoveredBoundary
{
    private readonly StrategyCommitmentLedger ledger;
    private readonly Action validate;
    internal CropTeacherSeedRecoveredBoundary(StrategyLedgerEvidence evidence, string afterSnapshotPath,
        string supportStepId, CropTeacherSeedRuntimeRecoveryOptions options, Action validate)
    {
        ledger = evidence.Ledger;
        LedgerPath = evidence.Path;
        AfterSnapshotPath = afterSnapshotPath;
        SupportStepId = supportStepId;
        RunId = options.RunId;
        ExecutorVersion = options.ExecutorVersion;
        OriginalIntentSha256 = options.ExpectedIntentSha256;
        OriginalRuntimeDirectory = Path.GetFullPath(options.RuntimeDirectory);
        this.validate = validate;
    }
    public string LedgerPath { get; }
    public string AfterSnapshotPath { get; }
    public string SupportStepId { get; }
    public string RunId { get; }
    public string ExecutorVersion { get; }
    public string OriginalIntentSha256 { get; }
    public string OriginalRuntimeDirectory { get; }
    public StrategyCommitmentLedger ReadLedger()
    {
        validate();
        return JsonSerializer.Deserialize<StrategyCommitmentLedger>(JsonSerializer.Serialize(ledger, JsonOptions), JsonOptions)!;
    }
    public void ValidateArtifactsUnchanged() => validate();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public static partial class CropTeacherSeedRuntimeRecovery
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<CropTeacherSeedRecoveredBoundary> RecoverAsync(HttpClient http,
        CropTeacherSeedRuntimeRecoveryOptions options, CropTeacherManagedSeedScopes scopes, CropTeacherRunStep step,
        Func<string, string, Task<JsonObject>> post, Func<string, string, Task> write)
    {
        if (string.IsNullOrWhiteSpace(options.RunId) || string.IsNullOrWhiteSpace(options.ExecutorVersion) ||
            string.IsNullOrEmpty(options.ExpectedIntentSha256) || options.ExpectedIntentSha256.Length != 64 ||
            options.ExpectedIntentSha256.Any(value => !char.IsAsciiHexDigitLower(value)))
            throw new ArgumentException("Crop seed recovery requires original run, executor and pinned intent SHA256.");
        var origin = CropTeacherSeedLifecycleRuntime.NormalizeBackendOrigin(options.BackendUrl);
        scopes.ValidateStep(step);
        var binding = Bind(scopes, step);
        var markerRequest = binding.ReadPreparationRequest();
        var root = Path.GetFullPath(options.RuntimeDirectory);
        var output = Path.GetFullPath(options.EvidenceDirectory);
        var artifacts = new Artifacts(binding.InputHashes);
        var intentPath = Path.Combine(root, "support-intent.json");
        var intent = artifacts.Read<JsonObject>(intentPath);
        var expected = CropTeacherSeedRuntimeTransaction.CreateIntent(
            new(options.BackendUrl, root, options.RunId, options.ExecutorVersion), binding, markerRequest);
        if (binding.PreparationDetails is not null) expected["preparation_details"] = binding.PreparationDetails.DeepClone();
        if (artifacts.Hashes[intentPath] != options.ExpectedIntentSha256 || !JsonNode.DeepEquals(expected, intent))
            throw new InvalidDataException("Crop seed recovery differs from its pinned original intent.");
        if (Directory.Exists(output) || File.Exists(output) || Contains(root, output) || Contains(output, root) ||
            binding.InputHashes.Keys.Any(path => Contains(output, path)))
            throw new IOException("Crop seed recovery requires a new, separate evidence directory.");
        Directory.CreateDirectory(output);
        var diagnostic = new JsonObject
        {
            ["schema_version"] = "crop_teacher_seed_runtime_recovery.v1", ["run_id"] = options.RunId,
            ["runtime_directory"] = root, ["original_intent_sha256"] = options.ExpectedIntentSha256,
            ["support_step_id"] = markerRequest.PortfolioId, ["native_dispatch_performed"] = false,
            ["portfolio_mutation_requested"] = false, ["formal_product_training_authorized"] = false
        };
        await write(Path.Combine(output, "recovery-intent.json"), diagnostic.ToJsonString(JsonOptions));
        Exception? failure = null;
        try
        {
            var preparation = artifacts.Read<JsonObject>(Path.Combine(root, "preparation-summary.json"));
            if (preparation["status"]?.GetValue<string>() != "crop_seed_support_prepared" ||
                expected.Any(row => !JsonNode.DeepEquals(row.Value, preparation[row.Key])))
                throw new InvalidDataException("Crop seed recovery has no completed original preparation.");
            var markerReply = await ReadReplyAsync(options, origin, root, output, artifacts, "marker", "commit",
                markerRequest, post, write);
            var marker = binding.VerifyPreparationReply(markerReply.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)
                ?? throw new InvalidDataException("Recovered crop marker reply is empty."));
            ValidateRetainedLedger(artifacts, preparation, "base_ledger_path", root, binding.ReadBaseLedger());
            ValidateRetainedLedger(artifacts, preparation, "marker_ledger_path", root, marker.Ledger);
            var preparedQueuePath = RequiredPath(preparation, "compiled_queue_path");
            var preparedQueue = artifacts.Read<JsonNode>(preparedQueuePath);
            if (artifacts.Hashes[preparedQueuePath] != preparation["compiled_queue_sha256"]?.GetValue<string>())
                throw new InvalidDataException("Original prepared crop queue hash drifted.");
            marker.ValidateQueue(preparedQueuePath);
            var transition = artifacts.Read<MoneyRevenueCropNativeLifecycleTransitionRequest>(Path.Combine(root, "native-transition-request.json"));
            if (transition.RunId != options.RunId || transition.ExecutorVersion != options.ExecutorVersion ||
                transition.TotalDay != binding.TotalDay || transition.TransitionKind != binding.TransitionKind ||
                !JsonNode.DeepEquals(preparedQueue, artifacts.Read<JsonNode>(transition.QueuePath)))
                throw new InvalidDataException("Recovered crop native transition differs from original preparation.");
            var nativeQueueHash = marker.ValidateQueue(transition.QueuePath);
            var proofPath = Path.Combine(root, "support-proof.json");
            var proofHash = artifacts.Pin(proofPath);
            var verified = marker.ResumeTransition(transition, proofPath, nativeQueueHash, proofHash);
            var settlementReply = await ReadReplyAsync(options, origin, root, output, artifacts, "settlement",
                "settle-supporting-transition", verified.ReadSettlementRequest(), post, write);
            var custody = verified.VerifySettlementReply(settlementReply.Deserialize<ReservationPortfolioSupportingTransitionSettlementResult>(JsonOptions)
                ?? throw new InvalidDataException("Recovered crop settlement reply is empty."));
            var finalLedger = custody.Ledger;
            if (verified.RequiresAcquisition)
            {
                var acquired = await ReadReplyAsync(options, origin, root, output, artifacts, "acquisition", "commit",
                    custody.ReadAcquiredSeedRequest!(), post, write);
                finalLedger = custody.VerifyAcquiredSeedReply!(acquired.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)
                    ?? throw new InvalidDataException("Recovered crop acquisition reply is empty."));
            }
            scopes.ValidateStep(step);
            artifacts.Validate();
            var final = await StrategyLedgerEvidenceStore.ReadAsync(http, origin, output, finalLedger.SourceStateHash,
                JsonOptions, "Recovered original crop boundary");
            CropTeacherSeedRuntimeTransaction.RequireLedger(finalLedger, final.Ledger, "recovery final readback");
            artifacts.Pin(final.Path);
            void Validate()
            {
                scopes.ValidateStep(step);
                _ = verified.ReadSettlementRequest();
                artifacts.Validate();
            }
            Validate();
            diagnostic["status"] = "original_crop_seed_runtime_recovered";
            diagnostic["original_native_effect_replayed"] = true;
            diagnostic["after_snapshot_path"] = transition.AfterSnapshotPath;
            diagnostic["final_ledger_path"] = final.Path;
            diagnostic["artifact_hashes"] = JsonSerializer.SerializeToNode(artifacts.Hashes, JsonOptions);
            return new(final, transition.AfterSnapshotPath, markerRequest.PortfolioId, options, Validate);
        }
        catch (Exception error)
        {
            failure = error;
            diagnostic["status"] = "original_crop_seed_runtime_recovery_incomplete";
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
