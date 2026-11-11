using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    public void RefillSelectedResourceTreeComponentReusesOriginalEvidenceWithoutXml(int experience)
    {
        var (request, receipt, before, after) = NativeWildTreeClearanceBoundaryTests.Fixture(experience);
        var first = JsonSerializer.Serialize(before);
        var next = JsonSerializer.Serialize(after);
        var effects = ReadSelectedRefillEffects(request, receipt, before, after);
        Assert.NotNull(effects.WildTree);
        Assert.Null(effects.Pickup);
        Assert.False(effects.ResourceNeutralBoundaryVerified);
        Assert.Same(effects.Evidence, effects.WildTree.Terrain.Consumption.ResourceEvidence);
        Assert.True(effects.WildTree.Terrain.TerrainRemovalVerified);
        var consumption = effects.WildTree.Terrain.Consumption;
        Assert.True(consumption.StaminaContinuityVerified);
        Assert.Equal(receipt.BeforeGameTick - before.GameTick, consumption.BeforeCaptureGapTicks);
        Assert.Equal(after.GameTick - receipt.AfterGameTick, consumption.AfterCaptureGapTicks);
        Assert.False(request.ActionOriginInventoryPayload);
        AssertSelectedRefillEffectsHaveNoCredit(effects);
        Assert.Equal(first, JsonSerializer.Serialize(before));
        Assert.Equal(next, JsonSerializer.Serialize(after));
        Assert.Equal(before.StateHash, SnapshotHash.ComputeStateHash(before.State));
        Assert.Equal(after.StateHash, SnapshotHash.ComputeStateHash(after.State));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    public void RefillSelectedResourcePickupComponentReusesAdmissionEvidenceAndRetainsCaptureGap(bool newSlot, bool newKind, bool gap)
    {
        var f = NativePickupInventoryBoundaryTests.ResourceFixture(newSlot, newKind);
        f.Request.OptionId = "executor.move_to_tile";
        if (gap) f.Before.GameTick -= 2;
        var first = JsonSerializer.Serialize(f.Before);
        var next = JsonSerializer.Serialize(f.After);
        var effects = ReadSelectedRefillEffects(f.Request, f.Receipt, f.Before, f.After);
        Assert.NotNull(effects.Pickup);
        Assert.Null(effects.WildTree);
        Assert.False(effects.ResourceNeutralBoundaryVerified);
        Assert.Same(effects.Evidence, effects.Pickup.Inventory.Evidence);
        Assert.Equal(6L, effects.Evidence!.ObservedPickupUnits);
        Assert.True(effects.Pickup.PhysicalInventoryDeltaVerified);
        Assert.True(effects.Pickup.MaterialGraphAndMirrorsVerified);
        Assert.True(effects.Pickup.UnrelatedPersistentStateVerified);
        Assert.Equal(f.Receipt.InventoryPayloadBefore!.GameTick - f.Before.GameTick, effects.Pickup.Inventory.BeforeCaptureGapTicks);
        if (gap) Assert.True(effects.Pickup.Inventory.BeforeCaptureGapTicks > 0);
        AssertSelectedRefillEffectsHaveNoCredit(effects);
        Assert.Equal(first, JsonSerializer.Serialize(f.Before));
        Assert.Equal(next, JsonSerializer.Serialize(f.After));
        Assert.Equal(f.Before.StateHash, SnapshotHash.ComputeStateHash(f.Before.State));
        Assert.Equal(f.After.StateHash, SnapshotHash.ComputeStateHash(f.After.State));
    }

    [Theory]
    [InlineData("wrong-option")]
    [InlineData("unrequested")]
    [InlineData("missing-observation")]
    [InlineData("missing-payload")]
    [InlineData("foreign-source")]
    public void RefillSelectedResourceComponentRefusesUnsupportedOrUnboundPickup(string change)
    {
        var f = NativePickupInventoryBoundaryTests.ResourceFixture();
        f.Request.OptionId = "executor.move_to_tile";
        ActionOriginObservationReceipt? receipt = f.Receipt;
        switch (change)
        {
            case "wrong-option": f.Request.OptionId = "executor.water_tile"; break;
            case "unrequested": f.Request.ActionOriginObservationScopeId = null; break;
            case "missing-observation": receipt = null; break;
            case "missing-payload": f.Receipt.InventoryPayloadBefore = null; break;
            case "foreign-source": f.Receipt.BeforeStateHash = "foreign"; break;
        }
        Assert.Throws<InvalidDataException>(() => ReadSelectedRefillEffects(f.Request, receipt, f.Before, f.After));
    }

    [Theory]
    [InlineData("missing-observation")]
    [InlineData("unsupported-clear")]
    [InlineData("foreign-source")]
    public void RefillSelectedResourceComponentRefusesUnsupportedOrUnboundClearance(string change)
    {
        var (request, receipt, before, after) = NativeWildTreeClearanceBoundaryTests.Fixture(0);
        if (change == "unsupported-clear") request.ClearCompletionMode = "grass_removed";
        if (change == "foreign-source") receipt.BeforeStateHash = "foreign";
        Assert.Throws<InvalidDataException>(() => ReadSelectedRefillEffects(request,
            change == "missing-observation" ? null : receipt, before, after));
    }

    private static NativeRefillFrameResourceEffects ReadSelectedRefillEffects(TrainingExecutionRequest request,
        ActionOriginObservationReceipt? receipt, SnapshotEnvelope before, SnapshotEnvelope after)
    {
        var type = typeof(CropTeacherRefillApproachNativeTrace).Assembly.GetType(
            "StardewAI.GoalConditionedBootstrap.CropTeacherRefillResourceEffects", throwOnError: true)!;
        var method = type.GetMethod("Read", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        try
        {
            return Assert.IsType<NativeRefillFrameResourceEffects>(method.Invoke(null, [request, receipt, before, after, "Farm"]));
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
    }
}
