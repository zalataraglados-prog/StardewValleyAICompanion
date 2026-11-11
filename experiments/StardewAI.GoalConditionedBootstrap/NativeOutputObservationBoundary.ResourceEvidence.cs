using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public enum NativePickupReferenceStatus
{
    DebrisAndChunkObservedAtScopeStart,
    DebrisObservedAtScopeStartChunkUnresolved,
    DebrisNotObservedAtScopeStart
}

public sealed record NativeObservedPickupAdmission(int CollectionOrdinal, int AdmissionOrdinal,
    string DebrisId, string? ChunkId, string QualifiedItemId, int Quality, int Quantity,
    NativePickupReferenceStatus ScopeStartReferenceStatus, bool CollectionReturned, bool AdmissionReturned);

public sealed record NativeOutputResourceEvidence(NativeOutputObservationSummary Observation,
    IReadOnlyList<NativeObservedPickupAdmission> Admissions, NativeClearanceToolInputSummary? ClearanceToolInputs)
{
    public long ObservedPickupUnits => Admissions.Sum(row => (long)row.Quantity);
    public long UnsettledPickupUnits => ObservedPickupUnits;
    public long CurrentActionProductionCreditUnits => 0;
    public bool ProducerProvenanceVerified => false;
    public bool ResourceCreditAuthorized => false;
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

public static partial class NativeOutputObservationBoundary
{
    // Reuse the original strict reader once. Reference presence classifies observations,
    // not production: even a newly observed physical reference has no producer witness.
    public static NativeOutputResourceEvidence ReadResourceEvidence(TrainingExecutionRequest request,
        ActionOriginObservationReceipt receipt, SnapshotEnvelope before, SnapshotEnvelope after, string locationId)
    {
        var observation = NativeOutputObservationSnapshotBoundary.Read(request, receipt, before, after, locationId);
        var tools = request.ActionOriginToolInputs
            ? NativeClearanceToolObservationBoundary.ReadValidated(request, receipt, observation, locationId,
                before.PlayerId.Value ?? throw new InvalidDataException("Native resource evidence actor is unavailable."))
            : null;
        var admissions = new List<NativeObservedPickupAdmission>();
        for (var index = 0; index < receipt.Collections.Length; index++)
        {
            var collection = receipt.Collections[index];
            var reference = observation.Collections[index];
            var status = reference.ChunkPresentAtScopeStart
                ? NativePickupReferenceStatus.DebrisAndChunkObservedAtScopeStart
                : reference.DebrisPresentAtScopeStart
                    ? NativePickupReferenceStatus.DebrisObservedAtScopeStartChunkUnresolved
                    : NativePickupReferenceStatus.DebrisNotObservedAtScopeStart;
            for (var ordinal = 0; ordinal < collection.InventoryAdmissions.Length; ordinal++)
            {
                var admission = collection.InventoryAdmissions[ordinal];
                admissions.Add(new(collection.Ordinal, ordinal + 1, reference.DebrisId, reference.ChunkId,
                    admission.Item!.QualifiedItemId, admission.Item.Quality,
                    admission.InventoryAfter - admission.InventoryBefore, status,
                    collection.NativeReturn!.Value, admission.NativeReturn!.Value));
            }
        }
        return new(observation, admissions.AsReadOnly(), tools);
    }
}
