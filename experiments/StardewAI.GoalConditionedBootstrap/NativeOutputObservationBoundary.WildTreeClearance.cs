using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record NativeWildTreeClearanceSummary(NativeClearanceTerrainSummary Terrain)
{
    public bool SpecificPersistentResourceBoundaryVerified => true;
    public bool ProducerProvenanceVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

public static partial class NativeOutputObservationBoundary
{
    // Uses the original verified chop request; no assumed drops or private Tool XML requirement.
    public static NativeWildTreeClearanceSummary ReadWildTreeClearanceResources(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, SnapshotEnvelope before, SnapshotEnvelope after, string locationId)
    {
        var terrain = ReadClearanceTerrain(request, receipt, before, after, locationId);
        CropSeedResourceBoundary.VerifyWildTreeClearance(before, after, request, terrain);
        return new(terrain);
    }
}
