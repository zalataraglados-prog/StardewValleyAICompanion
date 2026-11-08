using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private sealed record OriginalRouteOutcome(CropTeacherSupportingRoute Scope, JsonObject Result, CropSeedRuntimeArtifacts Artifacts);

    private async Task EnsureOriginalRouteOutcomeAsync(CropTeacherManagedSeedOperation operation)
    {
        if (operation.SupportingRoute is not null) { operation.RuntimeArtifactGuard?.Invoke(); return; }
        var outcome = ReadOriginalRouteOutcome(operation);
        var path = Path.Combine(operation.RuntimeDirectory, "route-native-result.json");
        if (!File.Exists(path)) await write(path, outcome.Result.ToJsonString(JsonOptions)).ConfigureAwait(false);
        InstallOriginalRouteOutcome(operation, outcome);
    }

    private void LoadOriginalRouteOutcome(CropTeacherManagedSeedOperation operation)
    {
        if (operation.SupportingRoute is not null) { operation.RuntimeArtifactGuard?.Invoke(); return; }
        InstallOriginalRouteOutcome(operation, ReadOriginalRouteOutcome(operation));
    }

    private OriginalRouteOutcome ReadOriginalRouteOutcome(CropTeacherManagedSeedOperation operation)
    {
        var start = operation.RouteStart ?? throw new InvalidOperationException("Original route preparation is missing.");
        start.ValidateInputsUnchanged();
        var artifacts = new CropSeedRuntimeArtifacts(start.InputHashes);
        var root = operation.RuntimeDirectory;
        var intentPath = Path.Combine(root, "route-intent.json");
        if (!JsonNode.DeepEquals(CreateOriginalRouteIntent(operation.Record), artifacts.Read<JsonObject>(intentPath)) ||
            artifacts.Hashes[intentPath] != Text(operation.Record, "original_intent_sha256"))
            throw new InvalidDataException("Original route execution intent changed.");
        var claimPath = Path.Combine(root, "route-native-claim.json");
        if (!JsonNode.DeepEquals(CreateOriginalRouteNativeClaim(operation), artifacts.Read<JsonObject>(claimPath)))
            throw new InvalidDataException("Original route native invocation claim changed.");
        var scope = start.VerifyTransition(RouteTransition(operation.Record));
        foreach (var (path, hash) in scope.InputHashes)
            if (artifacts.Pin(path) != hash) throw new InvalidDataException("Original route outcome changed while binding.");
        var result = new JsonObject
        {
            ["schema_version"] = "crop_teacher_owned_route_native_result.v1", ["original_intent_sha256"] = Text(operation.Record, "original_intent_sha256"),
            ["route_native_claim_sha256"] = artifacts.Hashes[claimPath], ["route_transition"] = operation.Record["route_transition"]!.DeepClone(),
            ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(artifacts.Hashes, JsonOptions),
            ["native_outcome_verified"] = true, ["route_target_reached"] = scope.RouteTargetReached,
            ["native_crop_lifecycle_verified"] = false, ["teacher_preference_label_eligible"] = false,
            ["formal_product_training_authorized"] = false
        };
        scope.ValidateInputsUnchanged();
        artifacts.Validate();
        return new(scope, result, artifacts);
    }

    private void InstallOriginalRouteOutcome(CropTeacherManagedSeedOperation operation, OriginalRouteOutcome outcome)
    {
        var path = Path.Combine(operation.RuntimeDirectory, "route-native-result.json");
        if (!JsonNode.DeepEquals(outcome.Result, outcome.Artifacts.Read<JsonObject>(path)))
            throw new InvalidDataException("Original route native result differs from its independently verified receipt boundary.");
        operation.SupportingRoute = outcome.Scope;
        operation.RuntimeArtifactGuard = () =>
        { operation.RouteStart!.ValidateInputsUnchanged(); outcome.Scope.ValidateInputsUnchanged(); outcome.Artifacts.Validate(); };
        operation.RuntimeArtifactGuard();
    }
}
