using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeOutputResourceEvidenceTests
{
    private static (TrainingExecutionRequest Request, ActionOriginObservationReceipt Receipt,
        SnapshotEnvelope Before, SnapshotEnvelope After) Fixture()
    {
        var (request, receipt) = NativeOutputObservationBoundaryTests.Fixture();
        var before = NativeOutputObservationSnapshotBoundaryTests.Snapshot(99, [NativeOutputObservationSnapshotBoundaryTests.Row(0, 10)]);
        var after = NativeOutputObservationSnapshotBoundaryTests.Snapshot(111, [NativeOutputObservationSnapshotBoundaryTests.Row(0, 16)]);
        request.BeforeStateHash = receipt.BeforeStateHash = before.StateHash;
        return (request, receipt, before, after);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SameInventoryGainHasDistinctReferenceEvidenceButNoInventedProduction(bool prior)
    {
        var (request, receipt, before, after) = Fixture();
        if (!prior) receipt.BeforeDebris = [];
        var result = NativeOutputObservationBoundary.ReadResourceEvidence(request, receipt, before, after, "Farm");
        var admission = Assert.Single(result.Admissions);
        Assert.Equal(6, admission.Quantity);
        Assert.Equal(prior ? NativePickupReferenceStatus.DebrisAndChunkObservedAtScopeStart :
            NativePickupReferenceStatus.DebrisNotObservedAtScopeStart, admission.ScopeStartReferenceStatus);
        Assert.Equal(6L, result.ObservedPickupUnits);
        Assert.Equal(6L, result.UnsettledPickupUnits);
        Assert.Equal(0L, result.CurrentActionProductionCreditUnits);
        Assert.False(result.ProducerProvenanceVerified);
        Assert.False(result.ResourceCreditAuthorized);
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
        Assert.False(request.ActionOriginInventoryGraph);
        Assert.False(request.ActionOriginDebrisState);
        Assert.Null(result.ClearanceToolInputs);
    }

    [Fact]
    public void AbsentChunkIsNotFabricatedFromAnExistingDebrisReference()
    {
        var (request, receipt, before, after) = Fixture(); receipt.Collections[0].ChunkId = null;
        var value = NativeOutputObservationBoundary.ReadResourceEvidence(request, receipt, before, after, "Farm");
        Assert.Equal(NativePickupReferenceStatus.DebrisObservedAtScopeStartChunkUnresolved,
            Assert.Single(value.Admissions).ScopeStartReferenceStatus);
    }

    [Fact]
    public void FalseNativeReturnsStillUseActualAdmissionDeltaNotOfferedOrRemainingStack()
    {
        var (request, receipt, before, _) = Fixture(); var row = receipt.Collections[0];
        row.NativeReturn = false; row.InventoryAdmissions[0].NativeReturn = false;
        row.InventoryAdmissions[0].ItemStackAfterCall = 4;
        row.InventoryAdmissions[0].InventoryAfter = receipt.InventoryAfter[0].Quantity = 12;
        var after = NativeOutputObservationSnapshotBoundaryTests.Snapshot(111, [NativeOutputObservationSnapshotBoundaryTests.Row(0, 12)]);
        var value = NativeOutputObservationBoundary.ReadResourceEvidence(request, receipt, before, after, "Farm");
        var admission = Assert.Single(value.Admissions);
        Assert.Equal(2, admission.Quantity);
        Assert.False(admission.CollectionReturned); Assert.False(admission.AdmissionReturned);
        Assert.Equal(0L, value.CurrentActionProductionCreditUnits);
    }

    [Fact]
    public void MixedCollectionsPreserveNativeOrderAndDoNotDoubleCount()
    {
        var (request, receipt, before, after) = Fixture();
        var first = receipt.Collections[0]; first.InventoryAdmissions[0].InventoryAfter = 12;
        var second = JsonSerializer.Deserialize<ActionOriginCollectionObservation>(JsonSerializer.Serialize(first))!;
        second.Ordinal = 2;
        second.DebrisId = second.DebrisBefore!.DebrisId = Guid.NewGuid().ToString("N");
        second.ChunkId = Guid.NewGuid().ToString("N"); second.DebrisBefore.ChunkIds = [second.ChunkId];
        second.InventoryAdmissions[0].InventoryBefore = 12; second.InventoryAdmissions[0].InventoryAfter = 16;
        receipt.Collections = [first, second];
        var value = NativeOutputObservationBoundary.ReadResourceEvidence(request, receipt, before, after, "Farm");
        Assert.Equal(new[] { 1, 2 }, value.Admissions.Select(row => row.CollectionOrdinal));
        Assert.Equal(new[] { 2, 4 }, value.Admissions.Select(row => row.Quantity));
        Assert.Equal(NativePickupReferenceStatus.DebrisAndChunkObservedAtScopeStart, value.Admissions[0].ScopeStartReferenceStatus);
        Assert.Equal(NativePickupReferenceStatus.DebrisNotObservedAtScopeStart, value.Admissions[1].ScopeStartReferenceStatus);
        Assert.Equal(6L, value.UnsettledPickupUnits);
    }

    [Fact]
    public void CollectionWithoutAdmissionCannotCreateUnitsAndResultIsDetached()
    {
        var (request, receipt, before, _) = Fixture();
        receipt.Collections[0].InventoryAdmissions = []; receipt.InventoryAfter[0].Quantity = 10;
        var after = NativeOutputObservationSnapshotBoundaryTests.Snapshot(111, [NativeOutputObservationSnapshotBoundaryTests.Row(0, 10)]);
        var value = NativeOutputObservationBoundary.ReadResourceEvidence(request, receipt, before, after, "Farm");
        Assert.Empty(value.Admissions); Assert.Equal(0L, value.UnsettledPickupUnits);
        Assert.Throws<NotSupportedException>(() => ((IList<NativeObservedPickupAdmission>)value.Admissions).Clear());
        receipt.InventoryAfter[0].Quantity = 999;
        Assert.Equal(10, Assert.Single(value.Observation.Inventory).InventoryAfter);
    }

    [Theory]
    [InlineData("before-gap")]
    [InlineData("after-gap")]
    [InlineData("nonce")]
    [InlineData("unavailable")]
    [InlineData("foreign-baseline")]
    public void OriginalStrictRejectionIsNotReplacedByClassification(string mutation)
    {
        var (request, receipt, before, after) = Fixture();
        _ = NativeOutputObservationBoundary.ReadResourceEvidence(request, receipt, before, after, "Farm");
        switch (mutation)
        {
            case "before-gap": before = NativeOutputObservationSnapshotBoundaryTests.Snapshot(99, [NativeOutputObservationSnapshotBoundaryTests.Row(0, 11)]);
                request.BeforeStateHash = receipt.BeforeStateHash = before.StateHash; break;
            case "after-gap": after = NativeOutputObservationSnapshotBoundaryTests.Snapshot(111, [NativeOutputObservationSnapshotBoundaryTests.Row(0, 17)]); break;
            case "nonce": receipt.RequestNonce += "foreign"; break;
            case "unavailable": receipt.Status = "unavailable"; break;
            case "foreign-baseline": receipt.Collections[0].InventoryAdmissions[0].InventoryBefore = 11; break;
        }
        Assert.Throws<InvalidDataException>(() => NativeOutputObservationBoundary.ReadResourceEvidence(request, receipt, before, after, "Farm"));
    }
}
