using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public static partial class CropTeacherSeedLifecycleRuntime
{
    private static async Task<CropTeacherSeedRelease> CancelOnceAsync(HttpClient http, CropTeacherSeedLifecycleRuntimeOptions options,
        CropTeacherSeedLifecycleSupport scope, CropTeacherSeedCancellation cancellation, JsonObject diagnostic,
        Func<string, string, Task<JsonObject>> post, Func<string, string, Task> write)
    {
        var raw = JsonSerializer.Serialize(cancellation.Request, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(raw);
        if (bytes.Length > RuntimeExecutionRequestJournal.MaximumRequestBytes)
            throw new InvalidDataException("Crop cancellation request exceeds the original journal budget.");
        var requestPath = Path.Combine(options.EvidenceDirectory, "crop-cancel-request.json");
        var journalPath = Path.Combine(options.EvidenceDirectory, "crop-cancel-journal.json");
        var endpoint = options.BackendUrl + "/api/v1/strategy/commitments/crops/" + Uri.EscapeDataString(scope.CommitmentId) + "/cancel";
        var hash = Hash(bytes);
        var journal = CreateCancellationJournal(endpoint, requestPath, hash, scope.CommitmentId, options.RunId);
        await write(requestPath, raw);
        await write(journalPath, journal.ToJsonString(JsonOptions));
        diagnostic["cancellation_request_path"] = requestPath;
        diagnostic["cancellation_request_sha256"] = hash;
        diagnostic["cancellation_journal_path"] = journalPath;
        scope.ValidateInputsUnchanged();
        JsonObject reply;
        try { reply = await post(endpoint, raw); }
        catch (Exception transport) when (transport is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or IOException)
        {
            transport.Data["crop_cancellation_journal_path"] = journalPath;
            await CropTeacherSeedRuntimeTransaction.WriteSummaryAsync(write, Path.Combine(options.EvidenceDirectory, "crop-cancel-failure.json"),
                new JsonObject { ["error"] = transport.Message, ["cancellation_resent"] = false }, transport);
            if (transport.Data["RuntimeHttpStatusCode"] is int status && status is >= 400 and < 500) throw;
            try
            {
                foreach (var path in new[] { journalPath, requestPath })
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException("Crop cancellation journal must be a regular file.");
                if (new FileInfo(journalPath).Length > 16 * 1024 || new FileInfo(requestPath).Length > RuntimeExecutionRequestJournal.MaximumRequestBytes ||
                    !JsonNode.DeepEquals(journal, JsonNode.Parse(await File.ReadAllTextAsync(journalPath))) ||
                    Hash(await File.ReadAllBytesAsync(requestPath)) != hash)
                    throw new InvalidDataException("Crop cancellation original request journal changed.");
                scope.ValidateInputsUnchanged();
                var observed = await ReadAsync(http, options, cancellation.Request.StateHash, "Crop cancellation original readback");
                var release = cancellation.ResumeFromReadback(observed.Ledger);
                diagnostic["cancellation_recovered_by_exact_readback"] = true;
                diagnostic["cancellation_readback_path"] = observed.Path;
                return release;
            }
            catch (Exception readback) when (readback is InvalidDataException or IOException or UnauthorizedAccessException or
                HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
            {
                transport.Data["crop_cancellation_readback_failure"] = readback.Message;
                diagnostic["cancellation_readback_failure"] = readback.Message;
            }
            throw;
        }
        await write(Path.Combine(options.EvidenceDirectory, "crop-cancel-reply.json"), reply.ToJsonString(JsonOptions));
        var verified = cancellation.VerifyReply(reply.Deserialize<StrategyCommitmentMutationResult>(JsonOptions)
            ?? throw new InvalidDataException("Crop cancellation reply is empty."));
        diagnostic["cancellation_recovered_by_exact_readback"] = false;
        return verified;
    }

    private static string Hash(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    internal static JsonObject CreateCancellationJournal(string endpoint, string requestPath, string hash,
        string commitmentId, string runId) => new()
    {
        ["schema_version"] = "crop_teacher_cancel_rpc_journal.v1", ["endpoint"] = endpoint,
        ["request_file"] = Path.GetFileName(requestPath), ["request_sha256"] = hash,
        ["commitment_id"] = commitmentId, ["run_id"] = runId
    };
}
