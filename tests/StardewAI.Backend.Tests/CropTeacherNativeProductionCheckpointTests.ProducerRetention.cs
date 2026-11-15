using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherNativeProductionCheckpointTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExplicitProducerCheckpointCollectsRebuiltRetentionWithoutInventingMissingSettlement(bool useProducer)
    {
        var paths = await PrepareProducerCheckpoint(useProducer);
        var proof = MoneyRevenueCropProductionProofCheckpointBuilder.BuildNativeObserved(paths.Request);
        Assert.Equal("money_revenue_crop_production_proof_checkpoint.v14", proof.SchemaVersion);
        var retention = Assert.Single(proof.VerifiedOutputRetentions);
        Assert.Equal(useProducer ? "money_revenue_crop_output_retention_proof.v2" :
            "money_revenue_crop_output_retention_proof.v1", retention.SchemaVersion);
        Assert.Equal(useProducer, retention.ProducerRetention is not null);
        Assert.Empty(proof.VerifiedTerminalSettlements);
        Assert.True(proof.PendingRequestCount > 0);
        Assert.False(proof.FutureCropProductionProofReady);
        Assert.False(proof.FreshSaveRevenueScheduleReady);
        Assert.False(proof.FormalProductTrainingAuthorized);
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropProductionProofCheckpointBuilder.Build(paths.Request));
        if (useProducer) Assert.Throws<InvalidDataException>(() =>
            MoneyRevenueCropProductionProofCheckpointBuilder.BuildNativeObserved(paths.OldNative));
    }

    [Theory]
    [InlineData("credit")]
    [InlineData("variant")]
    [InlineData("inventory_delta")]
    [InlineData("formal")]
    public async Task ExplicitProducerCheckpointRebuildRejectsSelfReportedOrChangedRetention(string fault)
    {
        var paths = await PrepareProducerCheckpoint(useProducer: true);
        var proof = JsonNode.Parse(File.ReadAllText(paths.Retention))!;
        if (fault == "credit") proof["producer_retention"]!["retained_quantity"] = 999;
        if (fault == "variant") proof["producer_retention"]!["variants"]![0]!["inventory_identity_sha256"] = new string('a', 64);
        if (fault == "inventory_delta") proof["inventory_quantity_increase"] = 999;
        if (fault == "formal") proof["formal_product_training_authorized"] = true;
        File.WriteAllText(paths.Retention, proof.ToJsonString(JsonOptions));
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropProductionProofCheckpointBuilder.BuildNativeObserved(paths.Request));
    }

    private static async Task<(string Request, string OldNative, string Retention)> PrepareProducerCheckpoint(bool useProducer)
    {
        var f = new CropTeacherProductionEvidenceTests.Fixture();
        var original = await Prepare(f);
        var request = JsonSerializer.Deserialize<MoneyRevenueCropProductionProofCheckpointRequest>(File.ReadAllText(original.Native), JsonOptions)!;
        var index = JsonSerializer.Deserialize<MoneyRevenueCropProductionProofFragmentIndex>(File.ReadAllText(request.FragmentIndexPath), JsonOptions)!;
        var retentionPath = Assert.Single(index.OutputRetentionProofPaths);
        if (useProducer)
        {
            var retention = JsonSerializer.Deserialize<MoneyRevenueCropOutputRetentionProof>(File.ReadAllText(retentionPath), JsonOptions)!;
            var retentionRequest = JsonSerializer.Deserialize<MoneyRevenueCropOutputRetentionProofRequest>(File.ReadAllText(retention.RequestPath), JsonOptions)!;
            retentionRequest.SchemaVersion = "money_revenue_crop_output_retention_proof_request.v2";
            var retentionRequestPath = Path.Combine(f.Output, "joint-retention-request.json");
            File.WriteAllText(retentionRequestPath, JsonSerializer.Serialize(retentionRequest, JsonOptions));
            retentionPath = Path.Combine(f.Output, "joint-retention-proof.json");
            File.WriteAllText(retentionPath, JsonSerializer.Serialize(MoneyRevenueCropOutputRetentionProofBuilder.Build(retentionRequestPath), JsonOptions));
            index.OutputRetentionProofPaths = [retentionPath];
        }
        request.FragmentIndexPath = Path.Combine(f.Output, "joint-checkpoint-fragments.json");
        File.WriteAllText(request.FragmentIndexPath, JsonSerializer.Serialize(index, JsonOptions));
        var oldNative = Path.Combine(f.Output, "joint-checkpoint-v13-request.json");
        File.WriteAllText(oldNative, JsonSerializer.Serialize(request, JsonOptions));
        request.SchemaVersion = "money_revenue_crop_production_proof_checkpoint_request.v14";
        var path = Path.Combine(f.Output, "joint-checkpoint-v14-request.json");
        File.WriteAllText(path, JsonSerializer.Serialize(request, JsonOptions));
        return (path, oldNative, retentionPath);
    }
}
