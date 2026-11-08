using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private async Task<RecoveredOwnerBoundary> RecoverSupportingTransitionAsync(HttpClient http,
        CropTeacherManagedSeedOperation operation, string evidenceDirectory, Func<string, string, Task<JsonObject>> post)
    {
        if (operation.Started is null) throw new InvalidOperationException("Managed supporting route has not claimed its original rebind invocation.");
        if (operation.OriginalStart is not null) LoadOriginalSupportingOutcome(operation);
        var output = Path.GetFullPath(evidenceDirectory);
        if (Directory.Exists(output) || File.Exists(output) || Within(operation.RuntimeDirectory, output) ||
            Within(output, operation.RuntimeDirectory) || operation.SupportingTransition!.InputHashes.Keys.Any(path => Within(output, path)))
            throw new IOException("Managed supporting route recovery requires separate new evidence.");
        RequireDirectoryPath(output);
        Directory.CreateDirectory(output);
        return await ReadSupportingTransitionBoundaryAsync(http, operation, output, post).ConfigureAwait(false);
    }

    private async Task<RecoveredOwnerBoundary> ReadSupportingTransitionBoundaryAsync(HttpClient http,
        CropTeacherManagedSeedOperation operation, string output, Func<string, string, Task<JsonObject>> post)
    {
        ValidateSupportingTransition(operation);
        var scope = operation.SupportingTransition!;
        var artifacts = new CropSeedRuntimeArtifacts(scope.InputHashes);
        var root = operation.RuntimeDirectory;
        var intentPath = SupportingIntentPath(operation);
        if (!JsonNode.DeepEquals(CreateSupportingTransitionIntent(operation.Record, scope), artifacts.Read<JsonObject>(intentPath)) ||
            artifacts.Hashes[intentPath] != Text(operation.Record, "original_intent_sha256"))
            throw new InvalidDataException("Managed supporting route original runtime intent changed.");
        var reply = await CropTeacherSeedRuntimeRecovery.ReadReplyAsync(options, options.BackendUrl, root, output, artifacts,
            "marker", "commit", scope.RebindRequest, post, write).ConfigureAwait(false);
        var verified = scope.VerifyRebindReply(reply.Deserialize<ReservationPortfolioCommitResult>(JsonOptions)
            ?? throw new InvalidDataException("Managed supporting route original marker reply is empty."));
        var replyPath = Path.Combine(root, "marker-reply.json");
        if (!File.Exists(replyPath))
        {
            await write(replyPath, reply.ToJsonString(JsonOptions)).ConfigureAwait(false);
            if (!JsonNode.DeepEquals(reply, artifacts.Read<JsonObject>(replyPath)))
                throw new InvalidDataException("Retained supporting route marker reply changed.");
        }
        artifacts.Validate();
        scope.ValidateInputsUnchanged();
        // Lookup and independent replay precede the original full Get/reconciliation.
        var final = await StrategyLedgerEvidenceStore.ReadAsync(http, options.BackendUrl, output,
            verified.SourceStateHash, JsonOptions, operation.IsWateringMaintenance ? "Original watering maintenance readback" : "Original supporting route readback").ConfigureAwait(false);
        CropTeacherSeedRuntimeTransaction.RequireLedger(verified, final.Ledger, "supporting route full readback");
        artifacts.Pin(final.Path);
        void Validate() { ValidateSupportingTransition(operation); artifacts.Validate(); }
        Validate();
        return new(final, scope.AfterSnapshotPath, Validate);
    }

    private void AddSupportingTransitionAcceptance(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        if (operation.Started is null) throw new InvalidDataException("Managed supporting route lacks its original rebind claim.");
        accepted["operation_kind"] = operation.IsWateringMaintenance ? "watering_maintenance" : "supporting_route";
        accepted["runtime_start_sha256"] = HashFile(RecordPath(operation.Sequence, "started"));
        accepted["original_intent_sha256"] = Text(operation.Record, "original_intent_sha256");
        accepted["original_marker_reply_sha256"] = HashFile(Path.Combine(operation.RuntimeDirectory, "marker-reply.json"));
        accepted["native_outcome_verified"] = true;
        accepted["native_crop_lifecycle_verified"] = false;
        accepted["teacher_preference_label_eligible"] = false;
        if (operation.Watering is { } watering) AddWateringOutcomeEvidence(watering, accepted);
        if (operation.OriginalStart is not null)
        {
            accepted[NativeSupportingRole(operation) + "_native_claim_sha256"] = HashFile(NativeSupportingPath(operation, "claim"));
            accepted[NativeSupportingRole(operation) + "_native_result_sha256"] = HashFile(NativeSupportingPath(operation, "result"));
            if (operation.SupportingRoute is { } route) accepted["route_target_reached"] = route.RouteTargetReached;
        }
    }

    private void ValidateSupportingTransitionAcceptance(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        if (operation.OriginalStart is not null) LoadOriginalSupportingOutcome(operation);
        if (operation.Watering is { } watering)
        {
            var expected = new JsonObject();
            AddWateringOutcomeEvidence(watering, expected);
            if (expected.Any(field => !JsonNode.DeepEquals(field.Value, accepted[field.Key])) ||
                !watering.RefillControlVerified && accepted["native_refill_control_verified"] is not null ||
                accepted["route_native_claim_sha256"] is not null || accepted["route_native_result_sha256"] is not null || accepted["route_target_reached"] is not null)
                throw new InvalidDataException("Managed crop watering prefix acceptance changed.");
        }
        if (operation.OriginalStart is not null)
        {
            if (Text(accepted, NativeSupportingRole(operation) + "_native_claim_sha256") != HashFile(NativeSupportingPath(operation, "claim")) ||
                Text(accepted, NativeSupportingRole(operation) + "_native_result_sha256") != HashFile(NativeSupportingPath(operation, "result")) ||
                operation.SupportingRoute is { } route && accepted["route_target_reached"]?.GetValue<bool>() != route.RouteTargetReached)
                throw new InvalidDataException("Owned crop native route acceptance changed.");
        }
        else if (operation.IsWateringMaintenance && (accepted["maintenance_native_claim_sha256"] is not null || accepted["maintenance_native_result_sha256"] is not null))
            throw new InvalidDataException("Completed watering cannot claim ownership of a native invocation.");
        if (operation.Started is null || accepted["operation_kind"]?.GetValue<string>() != (operation.IsWateringMaintenance ? "watering_maintenance" : "supporting_route") ||
            accepted["prepared_sha256"] is not null || accepted["original_proof_sha256"] is not null || accepted["dispatch_sha256"] is not null ||
            Text(accepted, "runtime_start_sha256") != HashFile(RecordPath(operation.Sequence, "started")) ||
            Text(accepted, "original_intent_sha256") != Text(operation.Record, "original_intent_sha256") ||
            Text(accepted, "original_intent_sha256") != HashFile(SupportingIntentPath(operation)) ||
            Text(accepted, "original_marker_reply_sha256") != HashFile(Path.Combine(operation.RuntimeDirectory, "marker-reply.json")) ||
            accepted["native_outcome_verified"]?.GetValue<bool>() != true || accepted["native_crop_lifecycle_verified"]?.GetValue<bool>() != false ||
            accepted["teacher_preference_label_eligible"]?.GetValue<bool>() != false ||
            !SamePath(StoredPath(accepted, "after_snapshot_path"), operation.SupportingTransition!.AfterSnapshotPath))
            throw new InvalidDataException("Managed crop supporting route acceptance evidence changed.");
    }
}
