using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private static readonly string[] SiteApproachIntentFields =
        { "route_site_approach_observation_path", "route_site_approach_commitment_id" };
    private static readonly string[] SiteApproachOutcomeFields =
        { "native_site_approach_verified", "site_approach_tile_x", "site_approach_tile_y" };

    private static void AddSiteApproachAdmission(JsonObject record, CropTeacherOriginalRouteCompilation value)
    {
        if (string.IsNullOrEmpty(value.SiteApproachObservationPath) && string.IsNullOrEmpty(value.SiteApproachCommitmentId)) return;
        if (value.NativeControlObservation || value.Kind != "supporting_route_to_crop" ||
            !Path.IsPathFullyQualified(value.SiteApproachObservationPath) || string.IsNullOrWhiteSpace(value.SiteApproachCommitmentId))
            throw new InvalidDataException("Owned site approach requires its explicit observation/commitment, not another route role.");
        record[SiteApproachIntentFields[0]] = Path.GetFullPath(value.SiteApproachObservationPath);
        record[SiteApproachIntentFields[1]] = value.SiteApproachCommitmentId;
    }

    private CropTeacherSupportingRoute.Preparation OpenOriginalRoutePreparation(JsonObject record,
        MoneyRevenueCropNativeLifecycleTransitionRequest transition, string[] active, bool nativeControl)
    {
        var site = SiteApproachIntentFields.Any(field => record[field] is not null);
        if (!site) return CropTeacherSupportingRoute.Preparation.Open(plan.RequestPath,
            StoredPath(record, "before_snapshot_path"), headLedgerPath, active, Text(record, "support_step_id"),
            transition.TransitionKind, StoredPath(record, "original_model_plan_path"), transition.QueuePath, nativeControl);
        if (nativeControl || transition.TransitionKind != "supporting_route_to_crop" ||
            SiteApproachIntentFields.Any(field => record[field] is not JsonValue value ||
                !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text)) ||
            !Path.IsPathFullyQualified(Text(record, SiteApproachIntentFields[0])))
            throw new InvalidDataException("Retained site approach changed its original role/observation/commitment.");
        return CropTeacherSupportingRoute.Preparation.OpenSiteApproach(plan.RequestPath,
            StoredPath(record, "before_snapshot_path"), headLedgerPath, active, Text(record, SiteApproachIntentFields[1]),
            Text(record, "support_step_id"), StoredPath(record, SiteApproachIntentFields[0]),
            StoredPath(record, "original_model_plan_path"), transition.QueuePath);
    }

    private static void CopySiteApproachIntentFields(JsonObject record, JsonObject intent)
    {
        foreach (var field in SiteApproachIntentFields)
            if (record[field] is not null) intent[field] = record[field]!.DeepClone();
    }

    private static void AddRouteOutcomeEvidence(CropTeacherSupportingRoute route, JsonObject result)
    {
        result["route_target_reached"] = route.RouteTargetReached;
        if (!route.IsSiteApproach) return;
        var target = route.SiteApproachTarget ?? throw new InvalidDataException("Verified site approach lost its original stand.");
        if (route.RouteTargetReached) throw new InvalidDataException("Site approach cannot claim general route completion.");
        result[SiteApproachOutcomeFields[0]] = true;
        result[SiteApproachOutcomeFields[1]] = target.TileX;
        result[SiteApproachOutcomeFields[2]] = target.TileY;
    }

    private static void ValidateSiteApproachAcceptance(CropTeacherManagedSeedOperation operation, JsonObject accepted)
    {
        var route = operation.SupportingRoute;
        if (route?.IsSiteApproach != true)
        {
            if (SiteApproachOutcomeFields.Any(field => accepted[field] is not null))
                throw new InvalidDataException("A different supporting phase cannot claim site approach evidence.");
            return;
        }
        var expected = new JsonObject(); AddRouteOutcomeEvidence(route, expected);
        if (expected.Any(field => !JsonNode.DeepEquals(field.Value, accepted[field.Key])))
            throw new InvalidDataException("Managed site approach acceptance changed its original stand/role.");
    }
}
