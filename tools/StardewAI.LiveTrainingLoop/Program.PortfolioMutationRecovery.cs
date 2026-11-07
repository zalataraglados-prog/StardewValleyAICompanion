using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private const string PortfolioSettlementOperation = "settle-supporting-transition";

    private static async Task<JsonObject> PostJournaledPortfolioSettlementAsync(HttpClient http,
        LiveTrainingOptions options, JsonObject execution, ReservationPortfolioSupportingTransitionSettlementRequest request)
        => await PostJournaledPortfolioMutationAsync(http, options, execution, request,
            PortfolioSettlementOperation, "material_reservation_settlement", PortfolioSettlementReplyMatches);

    private static async Task<JsonObject> PostJournaledPortfolioCommitAsync(HttpClient http,
        LiveTrainingOptions options, JsonObject execution, ReservationPortfolioCommitRequest request)
        => await PostJournaledPortfolioMutationAsync(http, options, execution, request,
            "commit", "crop_teacher_ledger_commit", PortfolioCommitReplyMatches);

    private static async Task<JsonObject> PostJournaledPortfolioMutationAsync<T>(HttpClient http,
        LiveTrainingOptions options, JsonObject execution, T request, string operation, string evidencePrefix,
        Func<JsonObject, T, bool> replyMatches)
    {
        var raw = JsonSerializer.Serialize(request, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(raw);
        if (bytes.Length > RuntimeExecutionRequestJournal.MaximumRequestBytes)
            throw new InvalidDataException("Portfolio mutation request exceeds journal budget.");
        var hash = PortfolioWireHash(bytes);
        var stem = Path.Combine(options.SnapshotDir, "portfolio-mutation-" + hash);
        var requestPath = stem + "-rpc-request.json";
        var journalPath = stem + "-rpc-journal.json";
        var observationPath = stem + "-rpc-lookup.json";
        var journal = new JsonObject
        {
            ["schema_version"] = "stardewai.portfolio_rpc_journal.v1",
            ["backend_url"] = options.BackendUrl, ["operation"] = operation,
            ["request_file"] = Path.GetFileName(requestPath), ["request_sha256"] = hash
        };
        Directory.CreateDirectory(options.SnapshotDir);
        await WritePortfolioJournalFileAsync(requestPath, raw);
        await WritePortfolioJournalFileAsync(journalPath, journal.ToJsonString(JsonOptions));
        execution[evidencePrefix + "_request_journal_path"] = journalPath;
        execution[evidencePrefix + "_request_sha256"] = hash;
        var endpoint = options.BackendUrl + "/api/v1/strategy/commitments/reservation-portfolios/";
        try { return await PostJsonStringAsync(http, endpoint + operation, raw); }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException or
            JsonException or InvalidOperationException or IOException)
        {
            failure.Data["portfolio_request_journal_path"] = journalPath;
            await RecordPortfolioFailureAsync(stem + "-rpc-failure.txt", failure.Message, failure);
            if (failure.Data["RuntimeHttpStatusCode"] is int status && status is >= 400 and < 500)
                throw;
            execution[evidencePrefix + "_lookup_path"] = observationPath;
            JsonObject? restored = null;
            try
            {
                // Same on-disk contract as the existing PowerShell recovery reader.
                foreach (var path in new[] { journalPath, requestPath })
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException("Portfolio journal must be a regular file.");
                if (new FileInfo(journalPath).Length > 16 * 1024 ||
                    new FileInfo(requestPath).Length > RuntimeExecutionRequestJournal.MaximumRequestBytes ||
                    !JsonNode.DeepEquals(journal, JsonNode.Parse(await File.ReadAllTextAsync(journalPath))))
                    throw new InvalidDataException("Portfolio journal identity drifted.");
                var original = await File.ReadAllBytesAsync(requestPath);
                if (PortfolioWireHash(original) != hash)
                    throw new InvalidDataException("Portfolio original request hash drifted.");
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var lookup = await PostJsonStringAsync(http, endpoint + "lookup-mutation", new JsonObject
                {
                    ["operation"] = operation, ["request"] = JsonNode.Parse(original)
                }.ToJsonString(JsonOptions), timeout.Token);
                await WritePortfolioJournalFileAsync(observationPath, lookup.ToJsonString(JsonOptions));
                if (ReadString(lookup, "schema_version") != "stardewai.portfolio_mutation_lookup.v1" ||
                    ReadString(lookup, "status") != "found" ||
                    lookup["execution_performed"]?.GetValue<bool>() != false ||
                    lookup["ledger_mutation_performed"]?.GetValue<bool>() != false ||
                    lookup["native_outcome_verified"]?.GetValue<bool>() != false ||
                    lookup["formal_product_training"]?.GetValue<bool>() != false ||
                    lookup["requires_independent_verification"]?.GetValue<bool>() != true ||
                    lookup["reply"] is not JsonObject reply || !replyMatches(reply, request))
                    throw new InvalidDataException("No stored reply matches the original portfolio " +
                        (operation == "commit" ? "commit." : "settlement."));
                restored = (JsonObject)reply.DeepClone();
                execution[evidencePrefix + "_reply_recovered"] = true;
                execution[evidencePrefix + "_lookup_path"] = observationPath;
            }
            catch (Exception lookupFailure) when (lookupFailure is IOException or InvalidDataException or UnauthorizedAccessException or
                HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or FormatException)
            {
                failure.Data["portfolio_lookup_failure"] = lookupFailure.Message;
                execution[evidencePrefix + "_lookup_failure"] = lookupFailure.Message;
                if (!File.Exists(observationPath))
                    await RecordPortfolioFailureAsync(observationPath, new JsonObject
                    {
                        ["schema_version"] = "stardewai.portfolio_mutation_lookup_failure.v1",
                        ["request_journal_path"] = journalPath, ["error"] = lookupFailure.Message,
                        ["execution_performed"] = false, ["ledger_mutation_performed"] = false,
                        ["native_outcome_verified"] = false, ["formal_product_training"] = false
                    }.ToJsonString(JsonOptions), failure);
            }
            if (restored is null) throw;
            return restored;
        }
    }

    private static bool PortfolioSettlementReplyMatches(JsonObject reply,
        ReservationPortfolioSupportingTransitionSettlementRequest request)
    {
        var result = reply.Deserialize<ReservationPortfolioSupportingTransitionSettlementResult>(JsonOptions);
        return reply["errors"] is JsonArray { Count: 0 } && result is { Accepted: true, Ledger: not null } &&
            result.PortfolioId == request.PortfolioId && result.RouteSourceDecisionId == request.RouteSourceDecisionId &&
            result.SupportingTransitionReceiptSha256 == request.SupportingTransitionReceiptSha256 &&
            result.CommittedLedgerRevision == (long)request.ExpectedLedgerRevision + 1 &&
            result.Ledger.Revision == result.CommittedLedgerRevision && result.Ledger.SourceStateHash == request.StateHash;
    }

    private static bool PortfolioCommitReplyMatches(JsonObject reply, ReservationPortfolioCommitRequest request)
    {
        var result = reply.Deserialize<ReservationPortfolioCommitResult>(JsonOptions);
        return reply["errors"] is JsonArray { Count: 0 } && result is { Accepted: true, Ledger: not null } &&
            result.PortfolioId == request.PortfolioId &&
            result.MaterialClaimCount == request.MaterialClaims.Length &&
            result.CurrencyClaimCount == request.CurrencyClaims.Length &&
            result.CommittedLedgerRevision == (long)request.ExpectedLedgerRevision + 1 &&
            result.Ledger.Revision == result.CommittedLedgerRevision && result.Ledger.SourceStateHash == request.StateHash;
    }

    private static string PortfolioWireHash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static async Task WritePortfolioJournalFileAsync(string path, string value)
    {
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(value));
        await stream.FlushAsync();
        stream.Flush(flushToDisk: true);
    }

    private static async Task RecordPortfolioFailureAsync(string path, string value, Exception original)
    {
        try { await WritePortfolioJournalFileAsync(path, value); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            original.Data["portfolio_diagnostic_write_failure"] = error.Message;
        }
    }
}
