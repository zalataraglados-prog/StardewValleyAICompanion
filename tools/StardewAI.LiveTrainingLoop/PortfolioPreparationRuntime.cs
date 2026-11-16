using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.LiveTrainingLoop;

internal sealed record PortfolioPreparedRuntime(string QueuePath, string QueueSha256, JsonNode Queue,
    string IntentSha256, JsonObject Diagnostic);

internal static class PortfolioPreparationRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    // Shared original commit/readback/compile transaction; callers retain their own admission and semantics.
    internal static async Task<PortfolioPreparedRuntime> RunAsync(HttpClient http, CropSeedRuntimeOptions options,
        ReservationPortfolioCommitRequest request, JsonObject diagnostic, Action prepareDetails,
        Func<StrategyCommitmentLedger> readBase, Func<ReservationPortfolioCommitResult, StrategyCommitmentLedger> verify,
        Action validateInputs, Func<Task<string>> compileQueue, Func<string, string> validateQueue,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit, Func<string, string, Task> write,
        string label = "Crop seed support", string statusPrefix = "crop_seed_support")
    {
        Exception? failure = null;
        try
        {
            prepareDetails();
            var rawIntent = diagnostic.ToJsonString(JsonOptions);
            await write(Path.Combine(options.EvidenceDirectory, "support-intent.json"), rawIntent);
            var intentHash = PortfolioMutationRequestJournal.Hash(Encoding.UTF8.GetBytes(rawIntent));
            var before = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, options.EvidenceDirectory,
                request.StateHash, JsonOptions, label + " base");
            CropTeacherSeedRuntimeTransaction.RequireLedger(readBase(), before.Ledger, "base");
            diagnostic["base_ledger_path"] = before.Path;
            var markerDiagnostic = new JsonObject(); diagnostic["marker"] = markerDiagnostic;
            var reply = await commit(markerDiagnostic, request);
            await write(Path.Combine(options.EvidenceDirectory, "marker-reply.json"), reply.ToJsonString(JsonOptions));
            var result = reply.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)
                ?? throw new InvalidDataException("Crop seed runtime marker reply is empty.");
            var verified = verify(result);
            var readback = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, options.EvidenceDirectory,
                request.StateHash, JsonOptions, label + " marker readback");
            CropTeacherSeedRuntimeTransaction.RequireLedger(verified, readback.Ledger, "marker readback");
            diagnostic["marker_ledger_path"] = readback.Path;
            validateInputs();
            var queuePath = Path.GetFullPath(await compileQueue());
            var queueHash = validateQueue(queuePath);
            var queue = JsonNode.Parse(await File.ReadAllTextAsync(queuePath))
                ?? throw new InvalidDataException("Crop seed runtime prepared queue is empty.");
            if (validateQueue(queuePath) != queueHash)
                throw new InvalidDataException("Crop seed runtime prepared queue changed while reading.");
            diagnostic["compiled_queue_path"] = queuePath; diagnostic["compiled_queue_sha256"] = queueHash;
            diagnostic["status"] = statusPrefix + "_prepared";
            return new(queuePath, queueHash, queue, intentHash, diagnostic);
        }
        catch (Exception error)
        { failure = error; diagnostic["status"] = statusPrefix + "_preparation_incomplete"; diagnostic["error"] = error.Message; throw; }
        finally
        { await CropTeacherSeedRuntimeTransaction.WriteSummaryAsync(write, Path.Combine(options.EvidenceDirectory, "preparation-summary.json"), diagnostic, failure); }
    }
}
