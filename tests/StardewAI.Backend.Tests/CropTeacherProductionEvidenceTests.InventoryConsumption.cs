using System.Text.Json;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherProductionEvidenceTests
{
    [Fact]
    public async Task SharedInventoryProductionExplicitlyPairsNewLifecycleAndRetentionWithoutChangingOldProducerCredit()
    {
        var f = new Fixture(2, "declared.shared.inventory.retention", true, 1);
        var previous = await f.BuildJointSaleStock(transferred: true);
        var current = await f.BuildJointSaleStock(inventoryEffects: true);
        var old = Read<MoneyRevenueCropOutputRetentionProof>(Assert.Single(previous.OutputRetentionProofPaths));
        var proof = Read<MoneyRevenueCropOutputRetentionProof>(Assert.Single(current.OutputRetentionProofPaths));
        Assert.Equal("money_revenue_crop_output_retention_proof.v4", old.SchemaVersion);
        Assert.Equal("money_revenue_crop_output_retention_proof.v5", proof.SchemaVersion);
        Assert.True(MoneyRevenueCropOutputRetentionProofBuilder.HasReservedProducerRetention(proof));
        Assert.Equal(old.InventoryQuantityBefore, proof.InventoryQuantityBefore);
        Assert.Equal(old.InventoryQuantityAfter, proof.InventoryQuantityAfter);
        Assert.Equal(old.InventoryQuantityIncrease, proof.InventoryQuantityIncrease);
        Assert.Equal(old.ProducerRetention!.RetainedQuantity, proof.ProducerRetention!.RetainedQuantity);
        Assert.Equal(JsonSerializer.Serialize(old.ProducerRetention), JsonSerializer.Serialize(proof.ProducerRetention));
        Assert.Equal(JsonSerializer.Serialize(proof), JsonSerializer.Serialize(MoneyRevenueCropOutputRetentionProofBuilder.Build(proof.RequestPath)));
        Assert.Equal("money_revenue_crop_output_retention_proof_request.v6",
            Read<MoneyRevenueCropOutputRetentionProofRequest>(proof.RequestPath).SchemaVersion);
        var lifecycle = Read<MoneyRevenueCropNativeLifecycleProof>(proof.NativeLifecycleProofPath);
        Assert.Equal("money_revenue_crop_native_lifecycle_proof_request.v8",
            Read<MoneyRevenueCropNativeLifecycleProofRequest>(lifecycle.RequestPath).SchemaVersion);
        Assert.False(proof.FormalProductTrainingAuthorized);
    }
}
