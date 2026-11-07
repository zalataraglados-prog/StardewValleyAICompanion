using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<int> InstallCropReservationsAsync(string[] arguments, HttpClient http)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Length; index += 2)
            if (index + 1 >= arguments.Length ||
                arguments[index] is not ("--decision" or "--backend-url" or "--output-dir") ||
                !values.TryAdd(arguments[index], arguments[index + 1]))
                throw new ArgumentException("Crop installation requires decision, backend-url and output-dir once each.");
        if (values.Count != 3 || !Uri.TryCreate(values["--backend-url"], UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https") || uri.UserInfo.Length != 0 ||
            uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath != "/")
            throw new ArgumentException("Crop installation requires an HTTP(S) backend origin and all three arguments.");
        var installation = CropTeacherRuntimeInstallation.Open(values["--decision"]);
        var output = Path.GetFullPath(values["--output-dir"]);
        if (Directory.Exists(output) || File.Exists(output))
            throw new IOException("Crop installation evidence directory already exists; refusing mutation or overwrite.");
        Directory.CreateDirectory(output);
        var options = new LiveTrainingOptions
        {
            BackendUrl = uri.AbsoluteUri.TrimEnd('/'), ManifestPath = Path.Combine(output, "installation-intent.json")
        };
        var diagnostic = new JsonObject
        {
            ["schema_version"] = "crop_teacher_runtime_installation.v1",
            ["decision_path"] = installation.DecisionPath,
            ["decision_sha256"] = PortfolioWireHash(await File.ReadAllBytesAsync(installation.DecisionPath)),
            ["backend_url"] = options.BackendUrl, ["source_state_hash"] = installation.SourceStateHash,
            ["runtime_ledger_installed"] = false, ["execution_authorized"] = false,
            ["native_outcome_verified"] = false, ["formal_product_training_authorized"] = false
        };
        await WritePortfolioJournalFileAsync(options.ManifestPath, diagnostic.ToJsonString(JsonOptions));
        Exception? failure = null;
        try
        {
            var before = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, output,
                installation.SourceStateHash, JsonOptions, "Crop installation base");
            installation.VerifyBaseLedger(before.Ledger);
            diagnostic["base_ledger_path"] = before.Path;
            var crop = await InstallCropCommitmentAsync(http, options, installation, output, diagnostic);
            installation.ValidateInputsUnchanged();
            var resourceReply = await PostJournaledPortfolioCommitAsync(http, options, diagnostic, installation.ResourceRequest);
            await WritePortfolioJournalFileAsync(Path.Combine(output, "resource-commit-reply.json"),
                resourceReply.ToJsonString(JsonOptions));
            var result = resourceReply.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)
                ?? throw new InvalidDataException("Crop resource installation reply is empty.");
            var installed = installation.VerifyResourceReply(crop.Ledger, result);
            var readback = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, output,
                installation.SourceStateHash, JsonOptions, "Crop installation persisted");
            installation.VerifyPersistedLedger(installed, readback.Ledger);
            diagnostic["installed_ledger_path"] = readback.Path;
            diagnostic["installed_ledger_sha256"] = PortfolioWireHash(await File.ReadAllBytesAsync(readback.Path));
            diagnostic["installed_ledger_revision"] = installed.Revision;
            diagnostic["runtime_ledger_installed"] = true;
            diagnostic["status"] = "crop_and_resource_installation_roundtrip_verified";
            Console.WriteLine("Crop/resource reservations installed; no gameplay or training performed: " + readback.Path);
            return 0;
        }
        catch (Exception error)
        {
            failure = error;
            diagnostic["status"] = "installation_incomplete";
            diagnostic["error"] = error.Message;
            throw;
        }
        finally
        {
            try
            {
                await WritePortfolioJournalFileAsync(Path.Combine(output, "installation-summary.json"),
                    diagnostic.ToJsonString(JsonOptions));
            }
            catch (Exception error) when (failure is not null && error is IOException or UnauthorizedAccessException)
            {
                failure.Data["crop_installation_diagnostic_write_failure"] = error.Message;
            }
        }
    }

    private static async Task<StrategyLedgerEvidence> InstallCropCommitmentAsync(HttpClient http,
        LiveTrainingOptions options, CropTeacherRuntimeInstallation installation, string output, JsonObject diagnostic)
    {
        var raw = JsonSerializer.Serialize(installation.CropRequest, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(raw);
        if (bytes.Length > RuntimeExecutionRequestJournal.MaximumRequestBytes)
            throw new InvalidDataException("Crop upsert request exceeds journal budget.");
        var requestPath = Path.Combine(output, "crop-upsert-request.json");
        await WritePortfolioJournalFileAsync(requestPath, raw);
        var endpoint = options.BackendUrl + "/api/v1/strategy/commitments/crops/upsert";
        var journalPath = Path.Combine(output, "crop-upsert-journal.json");
        var requestHash = PortfolioWireHash(bytes);
        var journal = new JsonObject
        {
            ["schema_version"] = "crop_teacher_upsert_rpc_journal.v1", ["endpoint"] = endpoint,
            ["request_file"] = Path.GetFileName(requestPath),
            ["request_sha256"] = requestHash
        };
        await WritePortfolioJournalFileAsync(journalPath, journal.ToJsonString(JsonOptions));
        diagnostic["crop_upsert_request_path"] = requestPath;
        diagnostic["crop_upsert_request_sha256"] = requestHash;
        installation.ValidateInputsUnchanged();
        JsonObject reply;
        try { reply = await PostJsonStringAsync(http, endpoint, raw); }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or
            JsonException or InvalidOperationException or IOException)
        {
            await RecordPortfolioFailureAsync(Path.Combine(output, "crop-upsert-failure.txt"), error.Message, error);
            if (error.Data["RuntimeHttpStatusCode"] is int status && status is >= 400 and < 500)
                throw;
            try
            {
                foreach (var path in new[] { requestPath, journalPath })
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException("Crop upsert journal must be a regular file.");
                if (new FileInfo(journalPath).Length > 16 * 1024 ||
                    new FileInfo(requestPath).Length > RuntimeExecutionRequestJournal.MaximumRequestBytes ||
                    PortfolioWireHash(await File.ReadAllBytesAsync(requestPath)) != requestHash ||
                    !JsonNode.DeepEquals(journal, JsonNode.Parse(await File.ReadAllTextAsync(journalPath))))
                    throw new InvalidDataException("Crop upsert original request journal changed.");
                // This GET uses the same source day: it cannot complete this newly selected crop.
                // Require exact replay and revision; a missing or intervening mutation is not recoverable here.
                var readback = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, output,
                    installation.SourceStateHash, JsonOptions, "Crop upsert recovery");
                installation.VerifyCropLedger(readback.Ledger);
                diagnostic["crop_upsert_recovered_by_exact_ledger_readback"] = true;
                diagnostic["crop_ledger_path"] = readback.Path;
                return readback;
            }
            catch (Exception lookupError) when (lookupError is IOException or InvalidDataException or UnauthorizedAccessException or
                HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
            {
                error.Data["crop_upsert_readback_failure"] = lookupError.Message;
            }
            throw;
        }
        await WritePortfolioJournalFileAsync(Path.Combine(output, "crop-upsert-reply.json"), reply.ToJsonString(JsonOptions));
        var result = reply.Deserialize<StrategyCommitmentMutationResult>(JsonOptions)
            ?? throw new InvalidDataException("Crop upsert installation reply is empty.");
        var ledger = installation.VerifyCropReply(result);
        var evidence = await StrategyLedgerEvidenceStore.SaveAsync(
            JsonSerializer.SerializeToNode(ledger, JsonOptions)!.AsObject(), output, JsonOptions, "Installed crop");
        diagnostic["crop_upsert_recovered_by_exact_ledger_readback"] = false;
        diagnostic["crop_ledger_path"] = evidence.Path;
        return evidence;
    }
}
