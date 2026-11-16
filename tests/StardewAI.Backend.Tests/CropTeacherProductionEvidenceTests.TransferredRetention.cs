using System.Text.Json;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherProductionEvidenceTests
{
    [Fact]
    public async Task TransferredRetentionProducerUsesAnExplicitVersionAndKeepsOriginalInventoryAndDefaultConsumers()
    {
        var f = new Fixture(2, "declared.transferred.retention", true, 1);
        var old = await f.BuildJointSaleStock(); var current = await f.BuildJointSaleStock(transferred: true);
        var legacy = Read<MoneyRevenueCropOutputRetentionProof>(Assert.Single(old.OutputRetentionProofPaths));
        var proof = Read<MoneyRevenueCropOutputRetentionProof>(Assert.Single(current.OutputRetentionProofPaths));
        Assert.Equal("money_revenue_crop_output_retention_proof.v3", legacy.SchemaVersion);
        Assert.Equal("money_revenue_crop_output_retention_proof.v4", proof.SchemaVersion);
        Assert.True(MoneyRevenueCropOutputRetentionProofBuilder.HasReservedProducerRetention(proof));
        Assert.Equal(legacy.InventoryQuantityBefore, proof.InventoryQuantityBefore);
        Assert.Equal(legacy.InventoryQuantityAfter, proof.InventoryQuantityAfter);
        Assert.Equal(legacy.InventoryQuantityIncrease, proof.InventoryQuantityIncrease);
        Assert.Equal(legacy.ProducerRetention!.RetainedQuantity, proof.ProducerRetention!.RetainedQuantity);
        Assert.Equal(JsonSerializer.Serialize(proof), JsonSerializer.Serialize(MoneyRevenueCropOutputRetentionProofBuilder.Build(proof.RequestPath)));
        Assert.False(proof.FormalProductTrainingAuthorized);
        Assert.Equal("money_revenue_crop_output_retention_proof_request.v5",
            Read<MoneyRevenueCropOutputRetentionProofRequest>(proof.RequestPath).SchemaVersion);
    }
}
