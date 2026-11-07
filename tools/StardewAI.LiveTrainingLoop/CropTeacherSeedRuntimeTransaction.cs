using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

internal sealed partial class CropTeacherSeedRuntimeTransaction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly HttpClient http;
    private readonly CropSeedRuntimeOptions options;
    private readonly CropSeedRuntimeScopeBinding scope;
    private readonly CropSeedRuntimeMarkerBinding marker;
    private readonly string queueSha256;
    private readonly string queuePath;
    private readonly JsonNode preparedQueue;
    private readonly JsonObject diagnostic;
    private readonly Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit;
    private readonly Func<JsonObject, ReservationPortfolioSupportingTransitionSettlementRequest, Task<JsonObject>> settle;
    private readonly Func<string, string, Task> write;
    private int completionStarted;

    internal string PreparedQueuePath => queuePath;
    internal string PreparedQueueSha256 => queueSha256;
    internal Task<StrategyLedgerEvidence> ReadOriginalAuthorityAsync() =>
        ReadLedgerAsync(http, options, marker.Ledger.SourceStateHash, "Crop seed original batch authority");

    private CropTeacherSeedRuntimeTransaction(HttpClient http, CropSeedRuntimeOptions options,
        CropSeedRuntimeScopeBinding scope, CropSeedRuntimeMarkerBinding marker, string queuePath,
        string queueSha256, JsonNode preparedQueue, JsonObject diagnostic,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<JsonObject, ReservationPortfolioSupportingTransitionSettlementRequest, Task<JsonObject>> settle,
        Func<string, string, Task> write)
    {
        this.http = http;
        this.options = options;
        this.scope = scope;
        this.marker = marker;
        this.queueSha256 = queueSha256;
        this.queuePath = queuePath;
        this.preparedQueue = preparedQueue;
        this.diagnostic = diagnostic;
        this.commit = commit;
        this.settle = settle;
        this.write = write;
    }

    internal static async Task<CropTeacherSeedRuntimeTransaction> PrepareAsync(HttpClient http,
        CropSeedRuntimeOptions options, CropSeedRuntimeScopeBinding scope,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<JsonObject, ReservationPortfolioSupportingTransitionSettlementRequest, Task<JsonObject>> settle,
        Func<string, string, Task> write, Func<Task<string>> compileQueue)
    {
        if (string.IsNullOrWhiteSpace(options.RunId) || string.IsNullOrWhiteSpace(options.ExecutorVersion))
            throw new InvalidDataException("Crop seed runtime identity is incomplete.");
        var output = Path.GetFullPath(options.EvidenceDirectory);
        if (Directory.Exists(output) || File.Exists(output))
            throw new IOException("Crop seed runtime evidence directory already exists; refusing another mutation.");
        options = options with { EvidenceDirectory = output };
        Directory.CreateDirectory(output);
        var request = scope.ReadPreparationRequest();
        var diagnostic = CreateIntent(options, scope, request);
        Exception? failure = null;
        try
        {
            if (scope.PreparationDetails is not null) diagnostic["preparation_details"] = scope.PreparationDetails.DeepClone();
            await write(Path.Combine(output, "support-intent.json"), diagnostic.ToJsonString(JsonOptions));
            var before = await ReadLedgerAsync(http, options, request.StateHash, "Crop seed support base");
            RequireLedger(scope.ReadBaseLedger(), before.Ledger, "base");
            diagnostic["base_ledger_path"] = before.Path;
            var markerDiagnostic = new JsonObject();
            diagnostic["marker"] = markerDiagnostic;
            var reply = await commit(markerDiagnostic, request);
            await write(Path.Combine(output, "marker-reply.json"), reply.ToJsonString(JsonOptions));
            var result = reply.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)
                ?? throw new InvalidDataException("Crop seed runtime marker reply is empty.");
            var verified = scope.VerifyPreparationReply(result);
            var readback = await ReadLedgerAsync(http, options, request.StateHash, "Crop seed support marker readback");
            RequireLedger(verified.Ledger, readback.Ledger, "marker readback");
            diagnostic["marker_ledger_path"] = readback.Path;
            scope.ValidateInputsUnchanged();
            var queuePath = Path.GetFullPath(await compileQueue());
            var queueHash = verified.ValidateQueue(queuePath);
            var preparedQueue = JsonNode.Parse(await File.ReadAllTextAsync(queuePath))
                ?? throw new InvalidDataException("Crop seed runtime prepared queue is empty.");
            if (verified.ValidateQueue(queuePath) != queueHash)
                throw new InvalidDataException("Crop seed runtime prepared queue changed while reading.");
            diagnostic["compiled_queue_path"] = queuePath;
            diagnostic["compiled_queue_sha256"] = queueHash;
            diagnostic["status"] = "crop_seed_support_prepared";
            return new(http, options, scope, verified, queuePath, queueHash, preparedQueue, diagnostic, commit, settle, write);
        }
        catch (Exception error)
        {
            failure = error;
            diagnostic["status"] = "crop_seed_support_preparation_incomplete";
            diagnostic["error"] = error.Message;
            throw;
        }
        finally
        {
            await WriteSummaryAsync(write, Path.Combine(output, "preparation-summary.json"), diagnostic, failure);
        }
    }

    public void ValidateDispatchQueue(string path) => ValidateDispatchQueueCore(path);

    private string ValidateDispatchQueueCore(string path)
    {
        if (marker.ValidateQueue(queuePath) != queueSha256 || !JsonNode.DeepEquals(preparedQueue,
                JsonNode.Parse(File.ReadAllText(path))))
            throw new InvalidDataException("Crop seed runtime dispatch differs from the prepared compilation.");
        return marker.ValidateQueue(path);
    }

    private static Task<StrategyLedgerEvidence> ReadLedgerAsync(HttpClient http,
        CropSeedRuntimeOptions options, string hash, string label) =>
        StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, options.EvidenceDirectory, hash, JsonOptions, label);

    internal static void RequireLedger(StrategyCommitmentLedger expected, StrategyCommitmentLedger actual, string stage)
    {
        if (!JsonNode.DeepEquals(JsonSerializer.SerializeToNode(expected, JsonOptions),
                JsonSerializer.SerializeToNode(actual, JsonOptions)))
            throw new InvalidDataException("Crop seed runtime " + stage + " ledger changed.");
    }

    internal static JsonObject CreateIntent(CropSeedRuntimeOptions options, CropSeedRuntimeScopeBinding scope,
        ReservationPortfolioCommitRequest request)
    {
        var intent = new JsonObject
        {
            ["schema_version"] = scope.RuntimeSchema,
            ["commitment_id"] = scope.CommitmentId, ["support_step_id"] = request.PortfolioId,
            ["before_snapshot_path"] = scope.SnapshotPath, ["before_state_hash"] = request.StateHash,
            ["base_ledger_revision"] = request.ExpectedLedgerRevision, ["run_id"] = options.RunId,
            ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(scope.InputHashes, JsonOptions),
            ["executor_version"] = options.ExecutorVersion, ["backend_url"] = options.BackendUrl,
            ["native_dispatch_performed_by_adapter"] = false, ["support_slice_verified"] = false,
            ["resource_settlement_verified"] = false, ["formal_product_training_authorized"] = false
        };
        return intent;
    }

    internal static async Task WriteSummaryAsync(Func<string, string, Task> write, string path,
        JsonObject diagnostic, Exception? failure)
    {
        try { await write(path, diagnostic.ToJsonString(JsonOptions)); }
        catch (Exception error) when (failure is not null && error is IOException or UnauthorizedAccessException)
        {
            failure.Data["crop_seed_runtime_diagnostic_write_failure"] = error.Message;
        }
    }
}
