using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private sealed record OriginalSupportingOutcome(ICropTeacherSupportingTransition Scope, JsonObject Result, CropSeedRuntimeArtifacts Artifacts);

    private async Task EnsureOriginalSupportingOutcomeAsync(CropTeacherManagedSeedOperation operation, CropTeacherHarvest? verifiedHarvest = null)
    {
        if (operation.SupportingTransition is not null) { operation.RuntimeArtifactGuard?.Invoke(); return; }
        var outcome = ReadOriginalSupportingOutcome(operation, deferOvernightSave: operation.IsOvernight, verifiedHarvest: verifiedHarvest);
        if (operation.IsOvernight)
        {
            await EnsureOriginalOvernightSaveEvidenceAsync(operation, (CropTeacherOvernight)outcome.Scope).ConfigureAwait(false);
            BindOriginalOvernightSaveEvidence(operation, outcome);
        }
        var path = NativeSupportingPath(operation, "result");
        if (!File.Exists(path)) await write(path, outcome.Result.ToJsonString(JsonOptions)).ConfigureAwait(false);
        InstallOriginalSupportingOutcome(operation, outcome);
    }

    private void LoadOriginalSupportingOutcome(CropTeacherManagedSeedOperation operation)
    {
        if (operation.SupportingTransition is not null) { operation.RuntimeArtifactGuard?.Invoke(); return; }
        InstallOriginalSupportingOutcome(operation, ReadOriginalSupportingOutcome(operation));
    }

    private OriginalSupportingOutcome ReadOriginalSupportingOutcome(CropTeacherManagedSeedOperation operation, bool deferOvernightSave = false,
        CropTeacherHarvest? verifiedHarvest = null)
    {
        var label = operation.IsTerminalShipping ? "Original terminal shipping" : operation.IsControlRecovery ? "Original control recovery" : operation.IsOvernight ? "Original overnight" : operation.IsHarvest ? "Original harvest" : operation.IsWateringMaintenance ? "Original watering" : "Original route";
        if (operation.OriginalStart is null) throw new InvalidOperationException(label + " preparation is missing.");
        var artifacts = ReadOriginalSupportingClaim(operation, label);
        var claimPath = NativeSupportingPath(operation, "claim");
        var scope = (ICropTeacherSupportingTransition?)verifiedHarvest ?? VerifyOriginalSupportingScope(operation);
        foreach (var (path, hash) in scope.InputHashes)
            if (artifacts.Pin(path) != hash) throw new InvalidDataException(label + " outcome changed while binding.");
        var result = new JsonObject
        {
            ["schema_version"] = "crop_teacher_owned_" + NativeSupportingRole(operation) + "_native_result.v1", ["original_intent_sha256"] = Text(operation.Record, "original_intent_sha256"),
            [NativeSupportingRole(operation) + "_native_claim_sha256"] = artifacts.Hashes[claimPath],
            [NativeSupportingRole(operation) + "_transition"] = operation.IsSharedSourceReturn
                ? JsonSerializer.SerializeToNode(OriginalSupportingTransition(operation), JsonOptions)
                : operation.Record[NativeSupportingRole(operation) + "_transition"]!.DeepClone(),
            ["input_artifact_hashes"] = JsonSerializer.SerializeToNode(artifacts.Hashes, JsonOptions),
            ["native_outcome_verified"] = true
        };
        if (scope is CropTeacherSupportingRoute route) AddRouteOutcomeEvidence(route, result);
        if (scope is CropTeacherWateringMaintenance watering) AddWateringOutcomeEvidence(watering, result);
        if (scope is CropTeacherHarvest harvest) AddHarvestOutcomeEvidence(harvest, result);
        if (scope is CropTeacherOvernight overnight) AddOvernightOutcomeEvidence(overnight, result);
        if (scope is CropTeacherControlRecovery control) AddControlOutcomeEvidence(control, result);
        if (scope is CropTeacherTerminalShipping terminal) AddTerminalOutcomeEvidence(terminal, result);
        if (scope is CropTeacherSiteTilling tilling) AddTillingOutcomeEvidence(tilling, operation.TillingStart!, result);
        if (scope is CropTeacherSiteClearance clearance) AddClearanceOutcomeEvidence(clearance, operation.ClearanceStart!, result);
        if (scope is CropTeacherSharedSourceReturn shared)
        {
            result["crop_production_credit_authorized"] = false;
            if (shared.SimulatedEquivalentStepCount > 0)
            {
                result["shared_simulated_equivalent_step_count"] = shared.SimulatedEquivalentStepCount;
                result["native_perfect_play_verified"] = false;
            }
            if (shared.AppliedNativeWorkQueue && VerifyCompletedSharedNativeObjective(operation, shared, artifacts))
                result["native_shared_objective_completed"] = true;
        }
        result["native_crop_lifecycle_verified"] = false;
        result["teacher_preference_label_eligible"] = false;
        result["formal_product_training_authorized"] = false;
        scope.ValidateInputsUnchanged();
        artifacts.Validate();
        var outcome = new OriginalSupportingOutcome(scope, result, artifacts);
        if (operation.IsOvernight && !deferOvernightSave) BindOriginalOvernightSaveEvidence(operation, outcome);
        return outcome;
    }

    private CropSeedRuntimeArtifacts ReadOriginalSupportingClaim(CropTeacherManagedSeedOperation operation, string label)
    {
        operation.OriginalStart!.ValidateInputsUnchanged();
        var artifacts = new CropSeedRuntimeArtifacts(operation.OriginalStart.InputHashes);
        if (operation.SharedPortfolioSupport is not null) artifacts.Pin(RecordPath(operation.Sequence, "prepared"));
        var intentPath = SupportingIntentPath(operation);
        if (!JsonNode.DeepEquals(CreateSupportingTransitionIntent(operation.Record, operation.SupportingTransition), artifacts.Read<JsonObject>(intentPath)) ||
            artifacts.Hashes[intentPath] != Text(operation.Record, "original_intent_sha256"))
            throw new InvalidDataException(label + " execution intent changed.");
        var claimPath = NativeSupportingPath(operation, "claim");
        if (!JsonNode.DeepEquals(CreateOriginalSupportingNativeClaim(operation), artifacts.Read<JsonObject>(claimPath)))
            throw new InvalidDataException(label + " native invocation claim changed.");
        artifacts.Validate();
        return artifacts;
    }

    private void InstallOriginalSupportingOutcome(CropTeacherManagedSeedOperation operation, OriginalSupportingOutcome outcome)
    {
        var path = NativeSupportingPath(operation, "result");
        if (!JsonNode.DeepEquals(outcome.Result, outcome.Artifacts.Read<JsonObject>(path)))
            throw new InvalidDataException(operation.IsWateringMaintenance ? "Original watering native result differs from its independently verified receipt boundary." : "Original route native result differs from its independently verified receipt boundary.");
        if (outcome.Scope is CropTeacherSupportingRoute route) operation.SupportingRoute = route;
        else if (outcome.Scope is CropTeacherWateringMaintenance watering) operation.Watering = watering;
        else if (outcome.Scope is CropTeacherHarvest harvest) operation.Harvest = harvest;
        else if (outcome.Scope is CropTeacherOvernight overnight) operation.Overnight = overnight;
        else if (outcome.Scope is CropTeacherControlRecovery control) operation.Control = control;
        else if (outcome.Scope is CropTeacherTerminalShipping terminal) operation.Terminal = terminal;
        else if (outcome.Scope is CropTeacherSiteTilling tilling) operation.Tilling = tilling;
        else if (outcome.Scope is CropTeacherSiteClearance clearance) operation.Clearance = clearance;
        else if (outcome.Scope is CropTeacherSharedSourceReturn shared)
        {
            operation.SharedSourceReturn = shared;
            operation.NativeSharedObjectiveCompleted = outcome.Result["native_shared_objective_completed"]?.GetValue<bool>() == true;
        }
        else throw new InvalidDataException("Owned crop outcome has an unsupported supporting type.");
        operation.RuntimeArtifactGuard = () =>
        { operation.OriginalStart!.ValidateInputsUnchanged(); outcome.Scope.ValidateInputsUnchanged(); outcome.Artifacts.Validate(); };
        operation.RuntimeArtifactGuard();
    }
}
