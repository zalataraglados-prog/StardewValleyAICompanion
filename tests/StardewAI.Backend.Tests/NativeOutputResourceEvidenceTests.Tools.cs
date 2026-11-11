using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class NativeOutputResourceEvidenceTests
{
    [Fact]
    public void OptionalToolEvidenceReusesTheValidatedScopeWithoutInventingNetEnergyCost()
    {
        var (request, receipt) = NativeClearanceToolObservationBoundaryTests.Fixture();
        var before = NativeOutputObservationSnapshotBoundaryTests.Snapshot(99, [NativeOutputObservationSnapshotBoundaryTests.Row(0, 10)]);
        var after = NativeOutputObservationSnapshotBoundaryTests.Snapshot(111, [NativeOutputObservationSnapshotBoundaryTests.Row(0, 16)]);
        before.PlayerId.Value = after.PlayerId.Value = "123";
        request.BeforeStateHash = receipt.BeforeStateHash = before.StateHash;
        var value = NativeOutputObservationBoundary.ReadResourceEvidence(request, receipt, before, after, "Farm");
        var tool = Assert.Single(value.ClearanceToolInputs!.Calls);
        Assert.Equal(value.Observation.ScopeId, value.ClearanceToolInputs.ScopeId);
        Assert.Equal(10f, tool.StaminaAtEntry); Assert.Equal(13f, tool.StaminaAtExit);
        Assert.Equal(14f, tool.Debit.Input.StaminaBefore); Assert.Equal(12f, tool.Debit.StaminaAfter);
        Assert.False(value.ResourceCreditAuthorized);
        receipt.ClearanceToolCalls![0].StaminaAfterDebit = 11f;
        Assert.Throws<InvalidDataException>(() => NativeOutputObservationBoundary.ReadResourceEvidence(request, receipt, before, after, "Farm"));
    }
}
