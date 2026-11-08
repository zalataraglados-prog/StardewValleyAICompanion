using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private async Task<RecoveredOwnerBoundary> RecoverSupportingRouteAsync(HttpClient http,
        CropTeacherManagedSeedOperation operation, string evidenceDirectory, Func<string, string, Task<JsonObject>> post)
    {
        if (operation.Started is null) throw new InvalidOperationException("Managed supporting route has not claimed its original rebind invocation.");
        if (operation.RouteStart is not null) LoadOriginalRouteOutcome(operation);
        var output = Path.GetFullPath(evidenceDirectory);
        if (Directory.Exists(output) || File.Exists(output) || Within(operation.RuntimeDirectory, output) ||
            Within(output, operation.RuntimeDirectory) || operation.SupportingRoute!.InputHashes.Keys.Any(path => Within(output, path)))
            throw new IOException("Managed supporting route recovery requires separate new evidence.");
        RequireDirectoryPath(output);
        Directory.CreateDirectory(output);
        return await ReadSupportingRouteBoundaryAsync(http, operation, output, post).ConfigureAwait(false);
    }

    private async Task<RecoveredOwnerBoundary> ReadSupportingRouteBoundaryAsync(HttpClient http,
        CropTeacherManagedSeedOperation operation, string output, Func<string, string, Task<JsonObject>> post)
    {
        ValidateSupportingRoute(operation);
        var scope = operation.SupportingRoute!;
        var artifacts = new CropSeedRuntimeArtifacts(scope.InputHashes);
        var root = operation.RuntimeDirectory;
        var intentPath = Path.Combine(root, "route-intent.json");
        if (!JsonNode.DeepEquals(CreateSupportingRouteIntent(operation.Record, scope), artifacts.Read<JsonObject>(intentPath)) ||
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
            verified.SourceStateHash, JsonOptions, "Original supporting route readback").ConfigureAwait(false);
        CropTeacherSeedRuntimeTransaction.RequireLedger(verified, final.Ledger, "supporting route full readback");
        artifacts.Pin(final.Path);
        void Validate() { ValidateSupportingRoute(operation); artifacts.Validate(); }
        Validate();
        return new(final, scope.AfterSnapshotPath, Validate);
    }

    private void AddSupportingRouteAcceptance(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        if (operation.Started is null) throw new InvalidDataException("Managed supporting route lacks its original rebind claim.");
        accepted["operation_kind"] = "supporting_route";
        accepted["runtime_start_sha256"] = HashFile(RecordPath(operation.Sequence, "started"));
        accepted["original_intent_sha256"] = Text(operation.Record, "original_intent_sha256");
        accepted["original_marker_reply_sha256"] = HashFile(Path.Combine(operation.RuntimeDirectory, "marker-reply.json"));
        accepted["native_outcome_verified"] = true;
        accepted["native_crop_lifecycle_verified"] = false;
        accepted["teacher_preference_label_eligible"] = false;
        if (operation.RouteStart is not null)
        {
            accepted["route_native_claim_sha256"] = HashFile(Path.Combine(operation.RuntimeDirectory, "route-native-claim.json"));
            accepted["route_native_result_sha256"] = HashFile(Path.Combine(operation.RuntimeDirectory, "route-native-result.json"));
            accepted["route_target_reached"] = operation.SupportingRoute!.RouteTargetReached;
        }
    }

    private void ValidateSupportingRouteAcceptance(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        if (operation.RouteStart is not null)
        {
            LoadOriginalRouteOutcome(operation);
            if (Text(accepted, "route_native_claim_sha256") != HashFile(Path.Combine(operation.RuntimeDirectory, "route-native-claim.json")) ||
                Text(accepted, "route_native_result_sha256") != HashFile(Path.Combine(operation.RuntimeDirectory, "route-native-result.json")) ||
                accepted["route_target_reached"]?.GetValue<bool>() != operation.SupportingRoute!.RouteTargetReached)
                throw new InvalidDataException("Owned crop native route acceptance changed.");
        }
        if (operation.Started is null || accepted["operation_kind"]?.GetValue<string>() != "supporting_route" ||
            accepted["prepared_sha256"] is not null || accepted["original_proof_sha256"] is not null || accepted["dispatch_sha256"] is not null ||
            Text(accepted, "runtime_start_sha256") != HashFile(RecordPath(operation.Sequence, "started")) ||
            Text(accepted, "original_intent_sha256") != Text(operation.Record, "original_intent_sha256") ||
            Text(accepted, "original_intent_sha256") != HashFile(Path.Combine(operation.RuntimeDirectory, "route-intent.json")) ||
            Text(accepted, "original_marker_reply_sha256") != HashFile(Path.Combine(operation.RuntimeDirectory, "marker-reply.json")) ||
            accepted["native_outcome_verified"]?.GetValue<bool>() != true || accepted["native_crop_lifecycle_verified"]?.GetValue<bool>() != false ||
            accepted["teacher_preference_label_eligible"]?.GetValue<bool>() != false ||
            !SamePath(StoredPath(accepted, "after_snapshot_path"), operation.SupportingRoute!.AfterSnapshotPath))
            throw new InvalidDataException("Managed crop supporting route acceptance evidence changed.");
    }
}
