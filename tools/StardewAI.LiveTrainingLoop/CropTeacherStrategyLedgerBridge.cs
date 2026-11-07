using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public static class CropTeacherStrategyLedgerBridge
{
    public static async Task<StrategyLedgerEvidence> PrepareAsync(HttpClient http, string backendUrl,
        string evidenceRoot, string stateHash, string snapshotPath, string decisionId,
        JsonSerializerOptions options,
        Func<JsonObject, ReservationPortfolioCommitRequest, Task<JsonObject>> commit)
    {
        var before = await StrategyLedgerEvidenceStore.ReadAsync(http, backendUrl,
            evidenceRoot, stateHash, options, "Crop Teacher");
        if (before.Ledger.SourceStateHash == stateHash)
        {
            CropTeacherLedgerSourceBinder.ValidateCurrent(snapshotPath, before.Path);
            return before;
        }
        var request = CropTeacherLedgerSourceBinder.BuildRefreshRequest(snapshotPath, before.Path, decisionId);
        if (request.StateHash != stateHash)
            throw new InvalidDataException("Crop Teacher ledger query and capture identity disagree.");
        var diagnostic = new JsonObject
        {
            ["schema_version"] = "crop_teacher_ledger_refresh.v1",
            ["source_snapshot_path"] = snapshotPath, ["source_state_hash"] = stateHash,
            ["before_ledger_path"] = before.Path, ["before_ledger_revision"] = before.Ledger.Revision,
            ["native_outcome_verified"] = false, ["formal_product_training_authorized"] = false
        };
        var diagnosticPath = System.IO.Path.Combine(evidenceRoot,
            "crop-ledger-refresh-" + System.IO.Path.GetFileNameWithoutExtension(before.Path) + ".json");
        Exception? failure = null;
        try
        {
            var reply = await commit(diagnostic, request);
            var result = reply.Deserialize<ReservationPortfolioCommitResult>(options)
                ?? throw new InvalidDataException("Crop Teacher ledger refresh result is empty.");
            var ledger = CropTeacherLedgerSourceBinder.VerifyRefreshResult(snapshotPath, before.Path, request, result);
            var after = await StrategyLedgerEvidenceStore.SaveAsync(
                JsonSerializer.SerializeToNode(ledger, options)!.AsObject(), evidenceRoot, options, "Crop Teacher");
            diagnostic["after_ledger_path"] = after.Path;
            diagnostic["after_ledger_revision"] = ledger.Revision;
            return after;
        }
        catch (Exception error)
        {
            failure = error;
            throw;
        }
        finally
        {
            try
            {
                await using var stream = new FileStream(diagnosticPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await JsonSerializer.SerializeAsync(stream, diagnostic, options);
            }
            catch (Exception error) when (failure is not null && error is IOException or UnauthorizedAccessException)
            {
                failure.Data["crop_ledger_diagnostic_write_failure"] = error.Message;
            }
        }
    }
}
