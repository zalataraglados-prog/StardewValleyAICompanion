using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    private static LifecycleCommitmentContext[] LoadLifecycleContexts(
        MoneyRevenueCropProductionObligationPlan plan,
        string planPath,
        string planSha256,
        MoneyRevenueCropDailyCapacityProof capacity,
        SnapshotEnvelope sourceSnapshot)
    {
        var bindings = capacity.PlantingLayoutProofSha256ByCommitment ??
            Array.Empty<MoneyRevenueCropDailyCapacityLayoutBinding>();
        Require(bindings.Length == plan.Commitments.Length &&
                bindings.Select(value => value.CommitmentId)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(
                        plan.Commitments.Select(value => value.CommitmentId)
                            .OrderBy(value => value, StringComparer.Ordinal),
                        StringComparer.Ordinal) &&
                bindings.Select(value => value.PlantingLayoutProofPath)
                    .Distinct(PathComparer).Count() == bindings.Length,
            "Money Revenue crop lifecycle aggregate layout denominator is incomplete or duplicated.");

        var contexts = bindings.Select(binding =>
        {
            var path = RequiredFile(
                binding.PlantingLayoutProofPath,
                "Money Revenue crop lifecycle planting-layout proof");
            Require(CurrentTeacherFrontierSupport.HashFile(path) ==
                        binding.PlantingLayoutProofSha256,
                "Money Revenue crop lifecycle planting-layout proof hash drifted.");
            var layout = CurrentTeacherFrontierSupport.Read<
                MoneyRevenueCropPlantingLayoutProof>(
                    path,
                    "Money Revenue crop lifecycle planting-layout proof");
            var rebuilt = MoneyRevenueCropPlantingLayoutProofBuilder.Build(
                layout.RequestPath);
            var commitment = plan.Commitments.SingleOrDefault(value =>
                value.CommitmentId == binding.CommitmentId);
            Require(commitment is not null &&
                    layout.SchemaVersion ==
                        "money_revenue_crop_planting_layout_proof.v1" &&
                    BootstrapArtifactGuard.EqualJson(layout, rebuilt) &&
                    SamePath(layout.CropProductionPlanPath, planPath) &&
                    layout.CropProductionPlanSha256 == planSha256 &&
                    layout.CommitmentId == binding.CommitmentId &&
                    layout.PlantingLayoutVerified &&
                    !layout.FormalProductTrainingAuthorized,
                "Money Revenue crop lifecycle planting-layout proof is not exact.");
            var verifiedCommitment = commitment ??
                throw new InvalidDataException(
                    "Money Revenue crop lifecycle commitment is missing.");
            Require(layout.SeedQualifiedItemId ==
                        verifiedCommitment.SeedQualifiedItemId &&
                    layout.TileCount == verifiedCommitment.TileCount,
                "Money Revenue crop lifecycle planting layout drifted from its commitment.");
            return new LifecycleCommitmentContext(
                path,
                layout,
                verifiedCommitment,
                ReadCropCatalogRow(
                    sourceSnapshot,
                    verifiedCommitment.SeedQualifiedItemId),
                layout.Tiles.Select(value => (value.TileX, value.TileY))
                    .ToHashSet(),
                verifiedCommitment.HarvestWaves.Max(value =>
                    value.HarvestTotalDay));
        }).OrderBy(value => value.Commitment.CommitmentId, StringComparer.Ordinal)
            .ToArray();

        Require(contexts.Select(value => value.Layout.LocationId)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() == 1 &&
                contexts.SelectMany(value => value.Tiles).Distinct().Count() ==
                    contexts.Sum(value => value.Tiles.Count),
            "Money Revenue crop lifecycle requires one shared location with non-overlapping layouts.");
        return contexts;
    }

    private static string ResolveActionCommitmentId(
        LoadedTransition transition,
        LifecycleCommitmentContext[] contexts)
    {
        if (transition.Request.TransitionKind == DayRolloverKind)
            return string.Empty;
        var owners = transition.Queue.Items.Select(item =>
        {
            var tile = ReadTarget(
                item,
                contexts[0].Layout.LocationId,
                transition.Before);
            return contexts.SingleOrDefault(context =>
                context.Tiles.Contains(tile))?.Commitment.CommitmentId ??
                string.Empty;
        }).Distinct(StringComparer.Ordinal).ToArray();
        Require(owners.Length == 1 && owners[0].Length > 0,
            "Money Revenue crop action queue does not belong to exactly one commitment.");
        return owners[0];
    }
}
