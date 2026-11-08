using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public static partial class CropTeacherSeedRuntimeRecovery
{
    private sealed record PreparationContext(string Origin, string Root, string Output,
        CropSeedRuntimeScopeBinding Binding, ReservationPortfolioCommitRequest Request, Artifacts Artifacts, JsonObject Expected);
    private sealed record OriginalPreparation(CropSeedRuntimeMarkerBinding Marker, string QueuePath,
        string QueueSha256, JsonNode Queue, JsonObject Diagnostic, JsonObject MarkerReply);

    private static PreparationContext OpenOriginalPreparation(CropTeacherSeedRuntimeRecoveryOptions options,
        CropTeacherManagedSeedScopes scopes, CropTeacherRunStep step)
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
        CropTeacherSeedRuntimeTransaction.AddPreparationDetails(expected, binding);
        if (artifacts.Hashes[intentPath] != options.ExpectedIntentSha256 || !JsonNode.DeepEquals(expected, intent))
            throw new InvalidDataException("Crop seed recovery differs from its pinned original intent.");
        if (Directory.Exists(output) || File.Exists(output) || Contains(root, output) || Contains(output, root) ||
            binding.InputHashes.Keys.Any(path => Contains(output, path)))
            throw new IOException("Crop seed recovery requires a new, separate evidence directory.");
        Directory.CreateDirectory(output);
        return new(origin, root, output, binding, markerRequest, artifacts, expected);
    }

    private static async Task<OriginalPreparation> ReadOriginalPreparationAsync(CropTeacherSeedRuntimeRecoveryOptions options,
        PreparationContext context, Func<string, string, Task<JsonObject>> post, Func<string, string, Task> write)
    {
        var (origin, root, output, binding, markerRequest, artifacts, expected) = context;
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
        var queuePath = RequiredPath(preparation, "compiled_queue_path");
        var queue = artifacts.Read<JsonNode>(queuePath);
        if (artifacts.Hashes[queuePath] != preparation["compiled_queue_sha256"]?.GetValue<string>())
            throw new InvalidDataException("Original prepared crop queue hash drifted.");
        marker.ValidateQueue(queuePath);
        return new(marker, queuePath, artifacts.Hashes[queuePath], queue, preparation, markerReply);
    }

    internal sealed record RestoredPreparation(ICropTeacherSeedRuntime Runtime, Action Validate);

    internal static async Task<RestoredPreparation> RestorePreparationAsync(HttpClient http,
        CropTeacherSeedRuntimeRecoveryOptions options, CropTeacherManagedSeedScopes scopes, CropTeacherRunStep step,
        Func<string, string, Task<JsonObject>> post,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit,
        Func<JsonObject, ReservationPortfolioSupportingTransitionSettlementRequest, Task<JsonObject>> settle,
        Func<string, string, Task> write)
    {
        var context = OpenOriginalPreparation(options, scopes, step);
        var prepared = await ReadOriginalPreparationAsync(options, context, post, write);
        scopes.ValidateStep(step);
        context.Artifacts.Validate();
        var evidence = await StrategyLedgerEvidenceStore.ReadAsync(http, context.Origin, context.Output,
            prepared.Marker.Ledger.SourceStateHash, JsonOptions, "Restored original crop preparation");
        CropTeacherSeedRuntimeTransaction.RequireLedger(prepared.Marker.Ledger, evidence.Ledger, "prepared restore readback");
        context.Artifacts.Pin(evidence.Path);
        // Future native settlement may retire the backend lookup. Retain only the fully verified original reply, never resend it.
        var markerReplyPath = Path.Combine(context.Root, "marker-reply.json");
        context.Artifacts.Validate();
        if (!context.Artifacts.Hashes.ContainsKey(markerReplyPath))
        {
            await write(markerReplyPath, prepared.MarkerReply.ToJsonString(JsonOptions));
            if (!JsonNode.DeepEquals(prepared.MarkerReply, context.Artifacts.Read<JsonObject>(markerReplyPath)))
                throw new InvalidDataException("Restored original marker reply changed while retaining it.");
        }
        void Validate()
        {
            scopes.ValidateStep(step);
            context.Artifacts.Validate();
        }
        Validate();
        var transaction = CropTeacherSeedRuntimeTransaction.RestorePrepared(http,
            new(options.BackendUrl, context.Root, options.RunId, options.ExecutorVersion), context.Binding, prepared.Marker,
            prepared.QueuePath, prepared.QueueSha256, prepared.Queue, options.ExpectedIntentSha256,
            prepared.Diagnostic, commit, settle, write);
        ICropTeacherSeedRuntime runtime = scopes.Purchase is not null ? new CropTeacherSeedPurchaseRuntime(transaction) :
            scopes.Transfer is not null ? new CropTeacherSeedTransferRuntime(transaction) :
            new CropTeacherSeedPlantingRuntime(transaction, prepared.Marker.OpenPlantingPrefix!(prepared.QueuePath, prepared.QueueSha256), context.Root, write);
        await write(Path.Combine(context.Output, "prepared-restore.json"), new JsonObject
        {
            ["schema_version"] = "crop_teacher_seed_prepared_restore.v1", ["run_id"] = options.RunId,
            ["original_intent_sha256"] = options.ExpectedIntentSha256, ["runtime_directory"] = context.Root,
            ["native_dispatch_performed"] = false, ["portfolio_mutation_requested"] = false,
            ["formal_product_training_authorized"] = false,
            ["artifact_hashes"] = JsonSerializer.SerializeToNode(context.Artifacts.Hashes, JsonOptions)
        }.ToJsonString(JsonOptions));
        Validate();
        return new(runtime, Validate);
    }
}
