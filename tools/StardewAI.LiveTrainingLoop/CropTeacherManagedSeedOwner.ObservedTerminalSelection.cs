using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private bool hasObservedTerminalSelection;

    internal async Task<CropTeacherManagedProductionEvidence?> ReadObservedTerminalProductionAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            ValidateState();
            return hasObservedTerminalSelection && terminalProductionReference is { } reference ?
                ReadObservedProductionEvidence(reference) : null;
        }
        finally { gate.Release(); }
    }

    private CropTeacherManagedProductionEvidence ReadObservedProductionEvidence(CropTeacherTerminalProductionReference reference)
    {
        if (HashFile(reference.ManifestPath) != reference.ManifestSha256)
            throw new InvalidDataException("Observed terminal production reference changed.");
        var manifest = Read<JsonObject>(reference.ManifestPath);
        string[] Paths(string name) => manifest[name]?.Deserialize<string[]>(JsonOptions) ??
            throw new InvalidDataException("Observed terminal production lacks " + name + ".");
        return new(reference.ManifestPath, Paths("planting_layout_proof_paths"), Paths("daily_capacity_proof_paths"),
            Paths("native_lifecycle_proof_paths"), Paths("output_retention_proof_paths"), reference.ManifestSha256);
    }

    private void AdoptObservedTerminalSelection(CropTeacherTerminalShippingSelection selection)
    {
        var prefix = selection.ObservedPrefix;
        if (prefix is null) return;
        if (terminalTarget is not null)
        {
            if (!hasObservedTerminalSelection || selection.CreateTarget(AcceptedTerminalDepositCount) != terminalTarget ||
                selection.ProductionReference != terminalProductionReference)
                throw new InvalidDataException("Observed terminal selection changed the accepted target/cursor.");
            return;
        }
        if (!Within(options.EvidenceDirectory, selection.ProductionReference.ManifestPath) ||
            SamePath(options.EvidenceDirectory, selection.ProductionReference.ManifestPath) ||
            HashFile(selection.ProductionReference.ManifestPath) != selection.ProductionReference.ManifestSha256)
            throw new InvalidDataException("Observed terminal production must be this owner's immutable owned certificate.");
        RequireDirectoryPath(Path.GetDirectoryName(selection.ProductionReference.ManifestPath)!);
        var manifest = Read<JsonObject>(selection.ProductionReference.ManifestPath);
        if (!SamePath(StoredPath(manifest, "accepted_snapshot_path"), headSnapshotPath) ||
            !SamePath(StoredPath(manifest, "accepted_ledger_path"), headLedgerPath))
            throw new InvalidDataException("Observed terminal selection must start at this owner's accepted production head.");
        ValidateObservedPrefixHistory(prefix, manifest);
        terminalTarget = selection.CreateTarget(selection.InitialDepositCount);
        terminalProductionReference = selection.ProductionReference;
        hasObservedTerminalSelection = true;
    }

    private void ValidateObservedPrefixHistory(CropTeacherObservedShippingPrefix prefix, JsonObject manifest)
    {
        var hashes = manifest["input_artifact_hashes"]!.Deserialize<Dictionary<string, string>>(JsonOptions)!
            .ToDictionary(row => Path.GetFullPath(row.Key), row => row.Value, PathComparer);
        var shared = manifest["owner_shared_source_returns"]?.Deserialize<CropTeacherManagedSharedSourceReturn[]>(JsonOptions) ?? [];
        var owned = manifest["owner_transitions"]?.Deserialize<CropTeacherManagedProductionTransition[]>(JsonOptions) ?? [];
        var rows = owned.Concat(shared.Select(row => new CropTeacherManagedProductionTransition(row.OwnerSequence,
            "shared_source_return", row.NativeTransition with { NativeSharedSource = row.SourceReference }))).ToArray();
        foreach (var native in prefix.Transitions)
        {
            var matches = rows.Where(row => JsonNode.DeepEquals(JsonSerializer.SerializeToNode(row.NativeTransition, JsonOptions),
                JsonSerializer.SerializeToNode(native, JsonOptions))).ToArray();
            if (matches is not [var match] || native.RunId != options.RunId || native.ExecutorVersion != options.ExecutorVersion)
                throw new InvalidDataException("Observed terminal prefix is outside this owner's original transitions.");
            var acceptedPath = RecordPath(match.OwnerSequence, "accepted");
            if (!hashes.TryGetValue(acceptedPath, out var expected) ||
                !acceptedHistory.Any(row => row.Sequence == match.OwnerSequence && row.Hash == expected) || HashFile(acceptedPath) != expected)
                throw new InvalidDataException("Observed terminal prefix lacks its original owner acceptance.");
            var intentPath = RecordPath(match.OwnerSequence, "intent");
            if (!hashes.TryGetValue(intentPath, out var intentHash) || HashFile(intentPath) != intentHash)
                throw new InvalidDataException("Observed terminal prefix admission changed.");
            var record = Read<JsonObject>(intentPath);
            var kind = record["operation_kind"]?.GetValue<string>();
            var original = kind == "shared_source_return" ? SharedExecutionRecord(record)["shared_source_transition"] :
                kind == "supporting_route" ? record["route_transition"] : kind == "control_recovery" ? record["control_transition"] : null;
            if (original is null || !JsonNode.DeepEquals(original, JsonSerializer.SerializeToNode(native with { NativeSharedSource = null }, JsonOptions)) ||
                native.NativeSharedSource is { } source && (source.StepId != Text(record, "support_step_id") ||
                    !SamePath(source.RunRequestPath, plan.RequestPath) || !SamePath(source.LedgerPath, StoredPath(record, "before_ledger_path"))))
                throw new InvalidDataException("Observed terminal prefix does not bind the actual original admission.");
        }
    }

    private JsonObject AddObservedTerminalSelection(JsonObject record)
    {
        if (hasObservedTerminalSelection)
            record["observed_terminal_selection"] = new JsonObject
            {
                ["schema_version"] = "crop_teacher_observed_terminal_selection.v1",
                ["target"] = JsonSerializer.SerializeToNode(terminalTarget, JsonOptions),
                ["production_reference"] = JsonSerializer.SerializeToNode(terminalProductionReference, JsonOptions)
            };
        return record;
    }

    private void RestoreObservedTerminalSelection(JsonObject record)
    {
        if (record["observed_terminal_selection"] is null) return;
        if (record["observed_terminal_selection"] is not JsonObject retained)
            throw new InvalidDataException("Observed terminal retained selection is not an explicit object.");
        var target = retained["target"]?.Deserialize<CropTeacherTerminalShippingTarget>(JsonOptions);
        var reference = retained["production_reference"]?.Deserialize<CropTeacherTerminalProductionReference>(JsonOptions);
        if (retained["schema_version"]?.GetValue<string>() != "crop_teacher_observed_terminal_selection.v1" ||
            target?.NativeVariants?.SchemaVersion != "crop_teacher_native_terminal_variants.v2" || reference is null ||
            !Within(options.EvidenceDirectory, reference.ManifestPath) || SamePath(options.EvidenceDirectory, reference.ManifestPath))
            throw new InvalidDataException("Observed terminal retained selection changed its explicit owner binding.");
        RequireDirectoryPath(Path.GetDirectoryName(reference.ManifestPath)!);
        if (terminalTarget is not null)
        {
            if (!hasObservedTerminalSelection || target != terminalTarget || reference != terminalProductionReference ||
                HashFile(reference.ManifestPath) != reference.ManifestSha256)
                throw new InvalidDataException("Observed terminal retained cursor/reference changed.");
            return;
        }
        var production = ReadObservedProductionEvidence(reference);
        var selection = target.RequireSaleStockReservations ? CropTeacherTerminalShippingSelection.OpenNativeVariantsConsumingSaleStock(
            production, options.NativeRunPlanPath, headSnapshotPath, headLedgerPath) :
            CropTeacherTerminalShippingSelection.OpenNativeVariants(production, options.NativeRunPlanPath, headSnapshotPath, headLedgerPath);
        if (selection.ObservedPrefix is null || selection.CreateTarget(selection.InitialDepositCount) != target)
            throw new InvalidDataException("Observed terminal retained cursor is not derived from the original prefix.");
        AdoptObservedTerminalSelection(selection);
        selection.ValidateInputsUnchanged();
    }
}
