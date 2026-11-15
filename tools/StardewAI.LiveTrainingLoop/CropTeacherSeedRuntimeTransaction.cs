using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
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
    private readonly string intentSha256;
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
        string queueSha256, JsonNode preparedQueue, string intentSha256, JsonObject diagnostic,
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
        this.intentSha256 = intentSha256;
        this.diagnostic = diagnostic;
        this.commit = commit;
        this.settle = settle;
        this.write = write;
    }

    internal static CropTeacherSeedRuntimeTransaction RestorePrepared(HttpClient http, CropSeedRuntimeOptions options,
        CropSeedRuntimeScopeBinding scope, CropSeedRuntimeMarkerBinding marker, string queuePath, string queueHash,
        JsonNode preparedQueue, string intentHash, JsonObject diagnostic,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<JsonObject, ReservationPortfolioSupportingTransitionSettlementRequest, Task<JsonObject>> settle,
        Func<string, string, Task> write) => new(http, options, scope, marker, queuePath, queueHash,
            preparedQueue.DeepClone(), intentHash, diagnostic.DeepClone().AsObject(), commit, settle, write);

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
        CropSeedRuntimeMarkerBinding verified = null!;
        var prepared = await PortfolioPreparationRuntime.RunAsync(http, options, request, diagnostic,
            () => AddPreparationDetails(diagnostic, scope), scope.ReadBaseLedger,
            result => { verified = scope.VerifyPreparationReply(result); return verified.Ledger; }, scope.ValidateInputsUnchanged,
            compileQueue, path => verified.ValidateQueue(path), commit, write);
        return new(http, options, scope, verified, prepared.QueuePath, prepared.QueueSha256, prepared.Queue,
            prepared.IntentSha256, diagnostic, commit, settle, write);
    }

    public void ValidateDispatchQueue(string path)
    {
        RequireExecutionLease();
        ValidateDispatchQueueCore(path);
    }

    private string ValidateDispatchQueueCore(string path) =>
        ValidatePreparedQueue(marker, queuePath, queueSha256, preparedQueue, path);

    internal static string ValidatePreparedQueue(CropSeedRuntimeMarkerBinding marker, string queuePath,
        string queueSha256, JsonNode preparedQueue, string path)
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

    internal static void AddPreparationDetails(JsonObject intent, CropSeedRuntimeScopeBinding scope)
    {
        if (scope.PreparationDetails is not null) intent["preparation_details"] = scope.PreparationDetails.DeepClone();
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
