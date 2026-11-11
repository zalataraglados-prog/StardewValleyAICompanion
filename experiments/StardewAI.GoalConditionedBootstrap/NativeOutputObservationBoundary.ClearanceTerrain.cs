using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record NativeClearanceTerrainSummary(NativeClearanceConsumptionSummary Consumption,
    int TileX, int TileY, string RuntimeType)
{
    public bool TerrainRemovalVerified => true;
    public bool FullResourceVerified => false;
    public bool ProducerProvenanceVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

public static partial class NativeOutputObservationBoundary
{
    // Exact raw terrain presence delta only, not tree health, XP, provenance or full-world settlement.
    public static NativeClearanceTerrainSummary ReadClearanceTerrain(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, SnapshotEnvelope before, SnapshotEnvelope after, string locationId)
    {
        var consumption = ReadClearanceConsumption(request, receipt, before, after, locationId);
        var target = CropSeedResourceBoundary.VerifyClearanceTerrain(request, before, after);
        return new(consumption, target.TileX, target.TileY, target.RuntimeType);
    }
}
