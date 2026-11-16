using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private static readonly string[] ClearanceEvidenceFields = { "native_site_clearance_verified", "site_clearance_tile_x", "site_clearance_tile_y",
        "site_clearance_outcome_authority", "site_clearance_private_effects_predicted", "site_clearance_producer_provenance_verified",
        "site_clearance_resource_credit_authorized" };

    private static void AddClearanceOutcomeEvidence(CropTeacherSiteClearance scope, CropTeacherSiteClearanceCompilation start, JsonObject result)
    {
        result[ClearanceEvidenceFields[0]] = scope.NativeOutcomeVerified;
        result[ClearanceEvidenceFields[1]] = start.Target.TileX;
        result[ClearanceEvidenceFields[2]] = start.Target.TileY;
        result[ClearanceEvidenceFields[3]] = scope.OutcomeAuthority;
        result[ClearanceEvidenceFields[4]] = scope.PrivateResourceEffectsPredicted;
        result[ClearanceEvidenceFields[5]] = scope.ProducerProvenanceVerified;
        result[ClearanceEvidenceFields[6]] = scope.ResourceCreditAuthorized;
    }

    private void ValidateClearanceAcceptance(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        if (!operation.IsSiteClearance)
        {
            if (ClearanceEvidenceFields.Concat(new[] { "clearance_native_claim_sha256", "clearance_native_result_sha256" }).Any(field => accepted[field] is not null))
                throw new InvalidDataException("A different phase cannot claim site clearance evidence.");
            return;
        }
        LoadOriginalSupportingOutcome(operation);
        var expected = new JsonObject(); AddClearanceOutcomeEvidence(operation.Clearance!, operation.ClearanceStart!, expected);
        var foreign = new[] { "route_target_reached", "native_site_approach_verified", "native_watering_prefix_verified",
            "native_harvest_prefix_verified", "native_overnight_control_verified", "native_control_recovery_verified",
            "native_terminal_shipping_step_verified", "native_save_boundary_verified", "reload_observation_sha256" };
        if (expected.Any(field => !JsonNode.DeepEquals(field.Value, accepted[field.Key])) || foreign.Any(field => accepted[field] is not null) ||
            new[] { "route", "maintenance", "harvest", "overnight", "control", "terminal", "till" }.Any(role =>
                accepted[role + "_native_claim_sha256"] is not null || accepted[role + "_native_result_sha256"] is not null))
            throw new InvalidDataException("Managed site clearance acceptance changed its native target/authority or claimed unrelated progress.");
    }
}
