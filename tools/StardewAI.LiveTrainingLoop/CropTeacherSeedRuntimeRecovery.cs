using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherSeedRuntimeRecoveryOptions(string BackendUrl, string RuntimeDirectory,
    string PortfolioJournalDirectory, string EvidenceDirectory, string RunId, string ExecutorVersion,
    string ExpectedIntentSha256) : ICropSeedPortfolioRecoveryOptions
{
    internal CropSeedPreparationReference? PreparationReference { get; init; }
}

public sealed class CropTeacherSeedRecoveredBoundary
{
    private readonly StrategyCommitmentLedger ledger;
    private readonly Action validate;
    internal CropTeacherSeedRecoveredBoundary(StrategyLedgerEvidence evidence, string afterSnapshotPath,
        string supportStepId, CropTeacherSeedRuntimeRecoveryOptions options, Action validate,
        string preparedQueuePath, string preparedQueueSha256)
    {
        ledger = evidence.Ledger;
        LedgerPath = evidence.Path;
        AfterSnapshotPath = afterSnapshotPath;
        SupportStepId = supportStepId;
        RunId = options.RunId;
        ExecutorVersion = options.ExecutorVersion;
        OriginalIntentSha256 = options.ExpectedIntentSha256;
        OriginalRuntimeDirectory = Path.GetFullPath(options.RuntimeDirectory);
        PreparedQueuePath = preparedQueuePath;
        PreparedQueueSha256 = preparedQueueSha256;
        this.validate = validate;
    }
    public string LedgerPath { get; }
    public string AfterSnapshotPath { get; }
    public string SupportStepId { get; }
    public string RunId { get; }
    public string ExecutorVersion { get; }
    public string OriginalIntentSha256 { get; }
    public string OriginalRuntimeDirectory { get; }
    public string PreparedQueuePath { get; }
    public string PreparedQueueSha256 { get; }
    internal CropSeedPreparationReference? PreparationReference { get; init; }
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
        var context = OpenOriginalPreparation(options, scopes, step);
        var (origin, root, output, binding, markerRequest, artifacts, _) = context;
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
            var prepared = await ReadOriginalPreparationAsync(options, context, post, write);
            var marker = prepared.Marker;
            var preparedQueuePath = prepared.QueuePath;
            var preparedQueue = prepared.Queue;
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
            return new(final, transition.AfterSnapshotPath, markerRequest.PortfolioId, options, Validate,
                preparedQueuePath, artifacts.Hashes[preparedQueuePath]) { PreparationReference = options.PreparationReference };
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
