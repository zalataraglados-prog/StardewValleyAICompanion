using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed record CropHarvestResourceEffects(CropHarvestStockSummary Stock)
{
    public bool SpecificPersistentResourceBoundaryVerified => true;
    public bool ProducerProvenanceVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

internal static partial class CropSeedResourceBoundary
{
    internal static CropHarvestResourceEffects VerifyHarvestResources(SnapshotEnvelope before, SnapshotEnvelope after,
        TrainingExecutionRequest request, string qualifiedItemId, int minimumUnits)
    {
        var target = ReadCropHarvestTarget(before, after, request);
        Require(target["harvest_item_qualified_id"]?.GetValue<string>() == qualifiedItemId,
            "Harvest retained stock differs from the original crop target.");
        _ = VerifyCropHarvestSkillEffects(before, after, request);
        VerifyCropHarvestTarget(before, after, request);
        var stock = VerifyHarvestStock(before, after, qualifiedItemId, minimumUnits);
        var community = CompareHarvestCommunityCenterProgress(before, after);
        var view = new SnapshotEnvelope { SaveId = after.SaveId, PlayerId = after.PlayerId, GameVersion = after.GameVersion,
            State = new Dictionary<string, JsonElement>(after.State, StringComparer.Ordinal) };
        var compared = new Dictionary<string, Dictionary<string, JsonElement>>(StringComparer.Ordinal);
        foreach (var (domain, names) in new[]
        {
            ("farm", new[] { "crops", "pet_bowls" }),
            ("player", new[] { "skills_detail", "level", "watering_can_state", "mastery_claim" })
        })
            foreach (var name in names)
            {
                var prior = Field(before, domain, name, required: false);
                var current = Field(after, domain, name, required: false);
                if (!Readable(prior) || !Readable(current)) continue;
                // Only independently verified values change in the comparison view; retain the actual envelope metadata.
                current!["value"] = Value(prior!).DeepClone();
                SetHarvestComparisonField(after, compared, domain, name, current);
            }
        if (community is not null) SetHarvestComparisonField(after, compared, "world_progress", "community_center", community);
        var priorHouse = Field(before, "world_progress", "marriage_house", required: false);
        var nextHouse = Field(after, "world_progress", "marriage_house", required: false);
        if (priorHouse is not null && nextHouse is not null)
        {
            var first = FullShipmentRecoverySettlementBoundaryProjector.ProjectExpectedResourceEnvelope("world_progress", "marriage_house", JsonSerializer.SerializeToElement(priorHouse));
            var next = FullShipmentRecoverySettlementBoundaryProjector.ProjectExpectedResourceEnvelope("world_progress", "marriage_house", JsonSerializer.SerializeToElement(nextHouse));
            if (SnapshotHash.CanonicalizeSemantic(first) != SnapshotHash.CanonicalizeSemantic(next))
            {
                var house = VerifyPickupHouseMirror(before, after);
                if (house is not null) SetHarvestComparisonField(after, compared, "world_progress", "marriage_house", house);
            }
        }
        foreach (var (domain, fields) in compared) view.State[domain] = JsonSerializer.SerializeToElement(fields);
        Require(FullShipmentRecoverySettlementBoundaryProjector.TryProjectExpectedResourceMutationInvariant(before, out var priorBoundary) &&
                FullShipmentRecoverySettlementBoundaryProjector.TryProjectExpectedResourceMutationInvariant(view, out var nextBoundary) && priorBoundary == nextBoundary,
            "Harvest changed unrelated persistent resources/state.");
        return new(stock);
    }

    private static void SetHarvestComparisonField(SnapshotEnvelope source, Dictionary<string, Dictionary<string, JsonElement>> compared,
        string domain, string field, JsonObject value)
    {
        if (!compared.TryGetValue(domain, out var fields))
            compared[domain] = fields = source.State[domain].EnumerateObject().ToDictionary(row => row.Name, row => row.Value, StringComparer.Ordinal);
        fields[field] = JsonSerializer.SerializeToElement(value);
    }
}
