using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingTests
{
    [Fact]
    public void InventoryContractPreservesExactNativeQualityAndRejectsMalformedBindings()
    {
        foreach (var quality in new[] { 0, 1, 2, 4 })
        {
            var request = new TrainingExecutionRequest { CropSeedPlantingVersion = 1, InventorySlotIndex = 1,
                QualifiedItemId = "(O)745", SeedId = "745", SeedInventoryQuality = quality, ExpectedStackBefore = 9, ExpectedStackAfter = 8 };
            Assert.True(CropSeedPlantingInventoryContract.MatchesNativeSlot(request, 1, "(O)745", "745", "745", quality, 9));
        }
        Assert.False(CropSeedPlantingBinding.TryInventory(null, out _));
        Assert.False(CropSeedPlantingBinding.TryInventory(new SmallModelActionParameter[] { null! }, out _));
    }

    [Fact]
    public void OriginalCompilerAndRequestMapperCarryEightOwnedSlotUsesWithoutChoosingEarlierSeedStack()
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var actions = marker.BuildActions(f.Batch(), f.OwnedSlotOne);
        Assert.Equal(8, actions.Length);
        var queue = f.Compile(marker, actions);
        Assert.True(queue.Status == "pending", string.Join(",", queue.CompilerDiagnostics.Concat(queue.Items.SelectMany(row => row.BlockingReasons))));
        Fixture.Save(f.QueuePath, queue);
        marker.ValidateQueue(f.QueuePath);
        for (var index = 0; index < 8; index++)
        {
            var request = InvokeLive<TrainingExecutionRequest>("BuildExecutionRequest", new LiveTrainingOptions
                { RunId = "owned.planting.fixture" }, JsonSerializer.SerializeToNode(queue.Items[index], JsonOptions)!.AsObject(),
                f.Before.StateHash, queue.QueueId, "executor.plant_seed");
            Assert.Equal(1, request.CropSeedPlantingVersion);
            Assert.Equal(1, request.InventorySlotIndex);
            Assert.Equal(9 - index, request.ExpectedStackBefore);
            Assert.Equal(8 - index, request.ExpectedStackAfter);
            Assert.True(CropSeedPlantingInventoryContract.MatchesNativeSlot(request, 1, "(O)745", "745", "745", 0, 9 - index));
            Assert.False(CropSeedPlantingInventoryContract.MatchesNativeSlot(request, 0, "(O)745", "745", "745", 0, 999));
            Assert.True(new ActionQueueDispatchReadinessService().Evaluate(queue, queue.Items[index], marker.Ledger, f.Before.StateHash).Ready);
        }
    }

    [Fact]
    public void OwnClaimQuantityBoundsBatchAndCallerMutationsDoNotChangeMarker()
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var actions = marker.BuildActions(f.Batch());
        Assert.Single(actions);
        Assert.Single(marker.BoundBatch.Tiles);
        marker.BoundBatch.Tiles[0] = new(79, 60);
        Assert.Equal(0, marker.BoundBatch.Tiles[0].TileX);
        var queue = f.Compile(marker, actions);
        Fixture.Save(f.QueuePath, queue);
        actions[0].Parameters[0].Value = "999";
        marker.ValidateQueue(f.QueuePath);
        Assert.Throws<InvalidDataException>(() => marker.BuildActions(f.Batch()));
    }

    [Theory]
    [InlineData("quality")]
    [InlineData("stack")]
    [InlineData("slot")]
    [InlineData("identity")]
    [InlineData("missing_version")]
    public void NativeInventoryContractRejectsDriftWithoutFallback(string fault)
    {
        var request = new TrainingExecutionRequest { CropSeedPlantingVersion = 1, InventorySlotIndex = 1,
            QualifiedItemId = "(O)745", SeedId = "745", SeedInventoryQuality = 2, ExpectedStackBefore = 9, ExpectedStackAfter = 8 };
        if (fault == "missing_version") request.CropSeedPlantingVersion = null;
        Assert.True(CropSeedPlantingInventoryContract.IsPresent(request));
        Assert.False(CropSeedPlantingInventoryContract.MatchesNativeSlot(request, fault == "slot" ? 0 : 1,
            fault == "identity" ? "(O)472" : "(O)745", "745", "745", fault == "quality" ? 0 : 2, fault == "stack" ? 8 : 9));
    }

    [Fact]
    public void UnownedRequestHasNoNewWireFieldsOrNativeSlotOptIn()
    {
        var request = new TrainingExecutionRequest { InventorySlotIndex = 7, SeedId = "745" };
        Assert.False(CropSeedPlantingInventoryContract.IsPresent(request));
        var json = JsonSerializer.Serialize(request, JsonOptions);
        Assert.DoesNotContain("crop_seed_planting_version", json);
        Assert.DoesNotContain("seed_inventory_quality", json);
    }
}
