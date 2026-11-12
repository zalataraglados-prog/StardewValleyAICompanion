using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<StrategyLedgerEvidence> InstallCropReservationsCoreAsync(HttpClient http,
        CropTeacherRuntimeInstallation installation, string backendUrl, string outputDirectory)
    {
        var output = Path.GetFullPath(outputDirectory);
        if (Directory.Exists(output) || File.Exists(output))
            throw new IOException("Crop installation evidence directory already exists; refusing mutation or overwrite.");
        Directory.CreateDirectory(output);
        var options = new LiveTrainingOptions
        {
            BackendUrl = backendUrl, ManifestPath = Path.Combine(output, "installation-intent.json")
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
            return readback;
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
}
