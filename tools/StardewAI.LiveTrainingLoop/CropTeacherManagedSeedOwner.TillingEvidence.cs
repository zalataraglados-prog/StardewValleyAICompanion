using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private static readonly string[] TillingEvidenceFields = { "native_site_till_verified", "site_till_tile_x", "site_till_tile_y",
        "site_till_observed_pickup_units", "site_till_producer_provenance_verified", "site_till_resource_credit_authorized" };

    private static void AddTillingOutcomeEvidence(CropTeacherSiteTilling scope, CropTeacherSiteTillCompilation start, JsonObject result)
    {
        result[TillingEvidenceFields[0]] = true;
        result[TillingEvidenceFields[1]] = start.Target.TileX;
        result[TillingEvidenceFields[2]] = start.Target.TileY;
        result[TillingEvidenceFields[3]] = scope.Resources.Inventory.Evidence.ObservedPickupUnits;
        result[TillingEvidenceFields[4]] = false;
        result[TillingEvidenceFields[5]] = false;
    }

    private void ValidateTillingAcceptance(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        if (!operation.IsSiteTilling)
        {
            if (TillingEvidenceFields.Concat(new[] { "till_native_claim_sha256", "till_native_result_sha256" }).Any(field => accepted[field] is not null))
                throw new InvalidDataException("A different phase cannot claim site till evidence.");
            return;
        }
        LoadOriginalSupportingOutcome(operation);
        var expected = new JsonObject(); AddTillingOutcomeEvidence(operation.Tilling!, operation.TillingStart!, expected);
        var foreign = new[] { "route_target_reached", "native_site_approach_verified", "native_watering_prefix_verified",
            "native_harvest_prefix_verified", "native_overnight_control_verified", "native_control_recovery_verified",
            "native_terminal_shipping_step_verified", "native_save_boundary_verified", "reload_observation_sha256" };
        if (expected.Any(field => !JsonNode.DeepEquals(field.Value, accepted[field.Key])) || foreign.Any(field => accepted[field] is not null) ||
            new[] { "route", "maintenance", "harvest", "overnight", "control", "terminal" }.Any(role =>
                accepted[role + "_native_claim_sha256"] is not null || accepted[role + "_native_result_sha256"] is not null))
            throw new InvalidDataException("Managed site till acceptance changed its target/resources or claimed unrelated progress.");
    }
}
