using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<(string Path, CropTeacherTerminalShippingSelection Selection)> PrepareCropTeacherManagedTerminalAsync(CropTeacherManagedSeedOwner owner,
        CropTeacherManagedProductionEvidence production)
    {
        var selection = await owner.SelectTerminalShippingAsync(production).ConfigureAwait(false);
        var output = Path.GetDirectoryName(production.ManifestPath)!;
        selection.ValidateInputsUnchanged();
        var result = Path.Combine(output, "shipping-selection.json");
        await WritePortfolioJournalFileAsync(result, new JsonObject
        {
            ["schema_version"] = "crop_teacher_managed_shipping_selection.v1", ["production_manifest_path"] = production.ManifestPath,
            ["production_manifest_sha256"] = production.ManifestSha256, ["retention_proof_path"] = selection.RetentionProofPath,
            ["source_snapshot_path"] = owner.CurrentSnapshotPath, ["source_ledger_path"] = owner.CurrentLedgerPath,
            ["qualified_item_id"] = selection.QualifiedItemId, ["minimum_units"] = selection.MinimumUnits,
            ["minimum_gross_revenue"] = selection.MinimumGrossRevenue, ["slot_index"] = selection.SlotIndex,
            ["quality"] = selection.Quality, ["unit_price"] = selection.UnitPrice, ["bin_tile_x"] = selection.BinX,
            ["bin_tile_y"] = selection.BinY, ["stand_tile_x"] = selection.StandX, ["stand_tile_y"] = selection.StandY,
            ["accepted_deposit_count"] = 0, ["native_dispatch_performed"] = false, ["terminal_settlement_verified"] = false,
            ["execution_authorized"] = false, ["formal_product_training_authorized"] = false
        }.ToJsonString(JsonOptions)).ConfigureAwait(false);
        selection.ValidateInputsUnchanged();
        return (result, selection);
    }

    private static async Task<ManagedCropControl?> BuildCropTeacherOwnedTerminalQueueAsync(LiveTrainingOptions options,
        int iteration, CropTeacherManagedSeedOwner owner, CropTeacherTerminalShippingSelection selection)
    {
        owner.ValidateOriginalSupportingExecutor(options, watering: false);
        if (owner.Pending is not null || owner.HeadRecoveryRequired)
            throw new InvalidOperationException("Recover the retained terminal phase before compiling another deposit.");
        var sourcePath = owner.CurrentSnapshotPath;
        var source = ContentAddressedJsonArtifactReader.ReadAllText(sourcePath);
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(source, JsonOptions)!;
        var model = selection.BuildOriginalAction(snapshot, owner.AcceptedTerminalDepositCount);
        if (model is null) return null;
        var prefix = CropTeacherArtifactPrefix(options, iteration, "terminal-shipping");
        var modelPath = prefix + "-model-action.json";
        var queuePath = prefix + "-compiled-queue.json";
        var receipt = Path.Combine(options.SnapshotDir, "execution-" + iteration.ToString("D4") + ".json");
        var after = Path.Combine(options.SnapshotDir, "after-snapshot-" + iteration.ToString("D4") + ".json");
        JsonObject? compiled = null;
        var operation = await owner.CompileAndAdmitOriginalTerminalAsync(selection.CreateTarget(owner.AcceptedTerminalDepositCount), async () =>
        {
            if (new[] { modelPath, queuePath, receipt, after }.Any(path => File.Exists(path) || Directory.Exists(path)))
                throw new IOException("Owned terminal requires new output slots; recover retained evidence instead of overwriting it.");
            var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(
                ContentAddressedJsonArtifactReader.ReadAllText(owner.CurrentLedgerPath), JsonOptions)!;
            var queue = new ActionQueueCompiler().Compile(model, snapshot, ledger);
            await WritePortfolioJournalFileAsync(modelPath, JsonSerializer.Serialize(model, JsonOptions)).ConfigureAwait(false);
            await WritePortfolioJournalFileAsync(queuePath, JsonSerializer.Serialize(queue, JsonOptions)).ConfigureAwait(false);
            selection.ValidateInputsUnchanged();
            compiled = JsonSerializer.SerializeToNode(queue, JsonOptions)!.AsObject();
            return new(modelPath, queuePath, receipt, after, Path.Combine(owner.EvidenceDirectory, "native-" + iteration.ToString("D4")));
        }, selection.ProductionReference).ConfigureAwait(false);
        return new(StandardResult(compiled!, modelPath, string.Empty, sourcePath, JsonNode.Parse(source)!.AsObject(),
            snapshot.StateHash, options, "crop_teacher_terminal_shipping", "full"), operation);
    }
}
