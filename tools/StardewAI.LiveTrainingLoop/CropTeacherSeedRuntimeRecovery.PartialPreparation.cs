using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.LiveTrainingLoop;

public static partial class CropTeacherSeedRuntimeRecovery
{
    internal static async Task<CropSeedPreparationReference> ContinueOriginalPreparationAsync(HttpClient http,
        CropTeacherSeedRuntimeRecoveryOptions options, CropTeacherManagedSeedScopes scopes, CropTeacherRunStep step,
        Func<string, string, Task<JsonObject>> post, Func<string, string, Task> write,
        Func<CropTeacherSeedCompilationContext, Task<string>> compileOriginal)
    {
        var markers = new CompilationMarkers();
        var context = OpenOriginalIdentity(options, scopes, step, markers);
        var (origin, root, output, binding, request, artifacts, expected) = context;
        var parent = Path.Combine(root, "preparation-continuations");
        if (!Contains(parent, output) || SamePath(parent, output))
            throw new IOException("Owned preparation continuation must use its original runtime namespace.");
        var attempt = new JsonObject
        {
            ["schema_version"] = "crop_teacher_seed_preparation_attempt.v1", ["run_id"] = options.RunId,
            ["executor_version"] = options.ExecutorVersion, ["original_runtime_directory"] = root,
            ["original_intent_sha256"] = options.ExpectedIntentSha256, ["support_step_id"] = request.PortfolioId,
            ["output_directory"] = output, ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(binding.InputHashes, JsonOptions),
            ["compilation_only"] = true, ["formal_product_training_authorized"] = false
        };
        var attemptPath = Path.Combine(output, "attempt-intent.json");
        if (File.Exists(attemptPath))
        {
            if (!JsonNode.DeepEquals(attempt, artifacts.Read<JsonObject>(attemptPath)))
                throw new InvalidDataException("Original preparation attempt identity changed.");
        }
        else
        {
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Preparation attempt has contents without its original identity.");
            Directory.CreateDirectory(output);
            await write(attemptPath, attempt.ToJsonString(JsonOptions));
            artifacts.Pin(attemptPath);
        }
        try
        {
            JsonObject? failed = null;
            var failedPath = Path.Combine(root, "preparation-summary.json");
            if (File.Exists(failedPath))
            {
                failed = artifacts.Read<JsonObject>(failedPath);
                if (failed["status"]?.GetValue<string>() is not ("crop_seed_support_preparation_incomplete" or "crop_seed_support_prepared") ||
                    expected.Any(row => !JsonNode.DeepEquals(row.Value, failed[row.Key])))
                    throw new InvalidDataException("Retained original preparation is inconsistent with its admitted intent.");
            }
            var reply = await ReadReplyAsync(options, origin, root, output, artifacts, "marker", "commit", request, post, write);
            var marker = binding.VerifyPreparationReply(reply.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)
                ?? throw new InvalidDataException("Original continuation marker reply is empty."));
            var basePath = failed?["base_ledger_path"] is null ? FindRetainedBaseLedger(context) : RequiredPath(failed!, "base_ledger_path");
            var baseView = new JsonObject { ["base_ledger_path"] = basePath };
            ValidateRetainedLedger(artifacts, baseView, "base_ledger_path", root, binding.ReadBaseLedger());
            binding.ValidateInputsUnchanged();
            artifacts.Validate();
            var authority = await StrategyLedgerEvidenceStore.ReadAsync(http, origin, root, marker.Ledger.SourceStateHash,
                JsonOptions, "Original partial preparation marker");
            CropTeacherSeedRuntimeTransaction.RequireLedger(marker.Ledger, authority.Ledger, "partial preparation marker readback");
            artifacts.Pin(authority.Path);
            await RetainOriginalMarkerReplyAsync(root, artifacts, reply, write);
            var queuePath = failed?["compiled_queue_path"] is null ? null : RequiredPath(failed!, "compiled_queue_path");
            if (queuePath is null)
            {
                var retained = Path.Combine(output, "compiled-seed-queue.json");
                var original = Path.Combine(root, "compiled-seed-queue.json");
                queuePath = File.Exists(retained) ? retained : File.Exists(original) ? original : null;
            }
            if (queuePath is null)
            {
                var plan = scopes.Transfer?.PlanExecution();
                if (plan is not null && plan.LocationId != step.TargetLocationId)
                    throw new InvalidDataException("Original continuation transfer location differs from its admitted step.");
                queuePath = Path.GetFullPath(await compileOriginal(new(output, step, markers.Transfer, markers.Planting, plan)));
                if (!SamePath(queuePath, Path.Combine(output, "compiled-seed-queue.json")))
                    throw new InvalidDataException("Continuation compiler must retain its original queue in the owned attempt.");
            }
            else markers.BindRetainedPlantingQueue?.Invoke();
            var queueHash = marker.ValidateQueue(queuePath);
            artifacts.Read<JsonNode>(queuePath);
            if (artifacts.Hashes[queuePath] != queueHash || marker.ValidateQueue(queuePath) != queueHash ||
                failed?["compiled_queue_sha256"] is JsonValue oldHash && oldHash.GetValue<string>() != queueHash)
                throw new InvalidDataException("Retained original continuation queue changed.");
            scopes.ValidateStep(step);
            artifacts.Validate();
            var view = expected.DeepClone().AsObject();
            view["status"] = "crop_seed_support_prepared";
            view["base_ledger_path"] = basePath;
            view["marker_ledger_path"] = authority.Path;
            view["compiled_queue_path"] = queuePath;
            view["compiled_queue_sha256"] = queueHash;
            view["recovery_schema_version"] = "crop_teacher_seed_preparation_continuation.v1";
            view["original_intent_sha256"] = options.ExpectedIntentSha256;
            view["artifact_hashes"] = JsonSerializer.SerializeToNode(artifacts.Hashes, JsonOptions);
            var path = Path.Combine(output, "continued-preparation.json");
            var raw = view.ToJsonString(JsonOptions);
            await write(path, raw);
            var hash = artifacts.Pin(path);
            if (hash != PortfolioMutationRequestJournal.Hash(Encoding.UTF8.GetBytes(raw)))
                throw new InvalidDataException("Original continued preparation changed while recording it.");
            artifacts.Validate();
            return new(path, hash);
        }
        catch (Exception error)
        {
            attempt["status"] = "original_preparation_continuation_incomplete";
            attempt["error"] = error.Message;
            await CropTeacherSeedRuntimeTransaction.WriteSummaryAsync(write,
                Path.Combine(output, "continuation-error.json"), attempt, error);
            throw;
        }
    }

    private static string FindRetainedBaseLedger(PreparationContext context)
    {
        var expected = JsonSerializer.SerializeToNode(context.Binding.ReadBaseLedger(), JsonOptions);
        foreach (var path in Directory.EnumerateFiles(Path.Combine(context.Root, "teacher-ledgers"), "*.json").OrderBy(value => value, PathComparer))
            if (JsonNode.DeepEquals(expected, JsonSerializer.SerializeToNode(context.Artifacts.Read<StrategyCommitmentLedger>(path), JsonOptions)))
                return path;
        throw new InvalidDataException("Original partial preparation has no retained full base authority.");
    }
}
