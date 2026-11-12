using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<string> PrepareCropTeacherManagedTerminalAsync(CropTeacherManagedSeedOwner owner,
        CropTeacherManagedProductionEvidence production)
    {
        var selection = await owner.SelectTerminalShippingAsync(production).ConfigureAwait(false);
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(ContentAddressedJsonArtifactReader.ReadAllText(owner.CurrentSnapshotPath), JsonOptions)!;
        var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(ContentAddressedJsonArtifactReader.ReadAllText(owner.CurrentLedgerPath), JsonOptions)!;
        var model = selection.BuildOriginalAction(snapshot, completedDeposits: 0)
            ?? throw new InvalidDataException("New crop terminal selection cannot start with already-completed deposits.");
        var queue = new ActionQueueCompiler().Compile(model, snapshot, ledger);
        var output = Path.GetDirectoryName(production.ManifestPath)!;
        var modelPath = Path.Combine(output, "shipping-model-action.json");
        var queuePath = Path.Combine(output, "shipping-compiled-queue.json");
        await WritePortfolioJournalFileAsync(modelPath, JsonSerializer.Serialize(model, JsonOptions)).ConfigureAwait(false);
        await WritePortfolioJournalFileAsync(queuePath, JsonSerializer.Serialize(queue, JsonOptions)).ConfigureAwait(false);
        selection.ValidateInputsUnchanged();
        var result = Path.Combine(output, "shipping-selection.json");
        await WritePortfolioJournalFileAsync(result, new JsonObject
        {
            ["schema_version"] = "crop_teacher_managed_shipping_selection.v1", ["production_manifest_path"] = production.ManifestPath,
            ["production_manifest_sha256"] = production.ManifestSha256, ["retention_proof_path"] = selection.RetentionProofPath,
            ["source_snapshot_path"] = owner.CurrentSnapshotPath, ["source_ledger_path"] = owner.CurrentLedgerPath,
            ["model_action_path"] = modelPath, ["queue_path"] = queuePath, ["queue_status"] = queue.Status,
            ["qualified_item_id"] = selection.QualifiedItemId, ["minimum_units"] = selection.MinimumUnits,
            ["minimum_gross_revenue"] = selection.MinimumGrossRevenue, ["slot_index"] = selection.SlotIndex,
            ["quality"] = selection.Quality, ["unit_price"] = selection.UnitPrice, ["bin_tile_x"] = selection.BinX,
            ["bin_tile_y"] = selection.BinY, ["stand_tile_x"] = selection.StandX, ["stand_tile_y"] = selection.StandY,
            ["accepted_deposit_count"] = 0, ["native_dispatch_performed"] = false, ["terminal_settlement_verified"] = false,
            ["execution_authorized"] = false, ["formal_product_training_authorized"] = false
        }.ToJsonString(JsonOptions)).ConfigureAwait(false);
        selection.ValidateInputsUnchanged();
        return result;
    }
}
