using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private sealed record OriginalSupportingOutcome(ICropTeacherSupportingTransition Scope, JsonObject Result, CropSeedRuntimeArtifacts Artifacts);

    private async Task EnsureOriginalSupportingOutcomeAsync(CropTeacherManagedSeedOperation operation)
    {
        if (operation.SupportingTransition is not null) { operation.RuntimeArtifactGuard?.Invoke(); return; }
        var outcome = ReadOriginalSupportingOutcome(operation);
        var path = NativeSupportingPath(operation, "result");
        if (!File.Exists(path)) await write(path, outcome.Result.ToJsonString(JsonOptions)).ConfigureAwait(false);
        InstallOriginalSupportingOutcome(operation, outcome);
    }

    private void LoadOriginalSupportingOutcome(CropTeacherManagedSeedOperation operation)
    {
        if (operation.SupportingTransition is not null) { operation.RuntimeArtifactGuard?.Invoke(); return; }
        InstallOriginalSupportingOutcome(operation, ReadOriginalSupportingOutcome(operation));
    }

    private OriginalSupportingOutcome ReadOriginalSupportingOutcome(CropTeacherManagedSeedOperation operation)
    {
        var label = operation.IsHarvest ? "Original harvest" : operation.IsWateringMaintenance ? "Original watering" : "Original route";
        var start = operation.OriginalStart ?? throw new InvalidOperationException(label + " preparation is missing.");
        start.ValidateInputsUnchanged();
        var artifacts = new CropSeedRuntimeArtifacts(start.InputHashes);
        var intentPath = SupportingIntentPath(operation);
        if (!JsonNode.DeepEquals(CreateSupportingTransitionIntent(operation.Record, operation.SupportingTransition), artifacts.Read<JsonObject>(intentPath)) ||
            artifacts.Hashes[intentPath] != Text(operation.Record, "original_intent_sha256"))
            throw new InvalidDataException(label + " execution intent changed.");
        var claimPath = NativeSupportingPath(operation, "claim");
        if (!JsonNode.DeepEquals(CreateOriginalSupportingNativeClaim(operation), artifacts.Read<JsonObject>(claimPath)))
            throw new InvalidDataException(label + " native invocation claim changed.");
        var scope = start.VerifyTransition(OriginalSupportingTransition(operation));
        foreach (var (path, hash) in scope.InputHashes)
            if (artifacts.Pin(path) != hash) throw new InvalidDataException(label + " outcome changed while binding.");
        var result = new JsonObject
        {
            ["schema_version"] = "crop_teacher_owned_" + NativeSupportingRole(operation) + "_native_result.v1", ["original_intent_sha256"] = Text(operation.Record, "original_intent_sha256"),
            [NativeSupportingRole(operation) + "_native_claim_sha256"] = artifacts.Hashes[claimPath],
            [NativeSupportingRole(operation) + "_transition"] = operation.Record[NativeSupportingRole(operation) + "_transition"]!.DeepClone(),
            ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(artifacts.Hashes, JsonOptions),
            ["native_outcome_verified"] = true
        };
        if (scope is CropTeacherSupportingRoute route) result["route_target_reached"] = route.RouteTargetReached;
        if (scope is CropTeacherWateringMaintenance watering) AddWateringOutcomeEvidence(watering, result);
        if (scope is CropTeacherHarvest harvest) AddHarvestOutcomeEvidence(harvest, result);
        result["native_crop_lifecycle_verified"] = false;
        result["teacher_preference_label_eligible"] = false;
        result["formal_product_training_authorized"] = false;
        scope.ValidateInputsUnchanged();
        artifacts.Validate();
        return new(scope, result, artifacts);
    }

    private void InstallOriginalSupportingOutcome(CropTeacherManagedSeedOperation operation, OriginalSupportingOutcome outcome)
    {
        var path = NativeSupportingPath(operation, "result");
        if (!JsonNode.DeepEquals(outcome.Result, outcome.Artifacts.Read<JsonObject>(path)))
            throw new InvalidDataException(operation.IsWateringMaintenance ? "Original watering native result differs from its independently verified receipt boundary." : "Original route native result differs from its independently verified receipt boundary.");
        if (outcome.Scope is CropTeacherSupportingRoute route) operation.SupportingRoute = route;
        else if (outcome.Scope is CropTeacherWateringMaintenance watering) operation.Watering = watering;
        else if (outcome.Scope is CropTeacherHarvest harvest) operation.Harvest = harvest;
        else throw new InvalidDataException("Owned crop outcome has an unsupported supporting type.");
        operation.RuntimeArtifactGuard = () =>
        { operation.OriginalStart!.ValidateInputsUnchanged(); outcome.Scope.ValidateInputsUnchanged(); outcome.Artifacts.Validate(); };
        operation.RuntimeArtifactGuard();
    }
}
