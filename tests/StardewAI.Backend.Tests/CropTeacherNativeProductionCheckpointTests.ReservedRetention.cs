using System.Reflection;
using System.Text.Json;
using StardewAI.LiveTrainingLoop;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherNativeProductionCheckpointTests
{
    [Fact]
    public async Task ReservedRetentionUsesExplicitProducerProofAndCheckpointConsumersWithoutGrantingTraining()
    {
        var f = new CropTeacherProductionEvidenceTests.Fixture(2, "declared.reserved.checkpoint", true, 1);
        var original = await Prepare(f);
        var evidence = await f.BuildJointSaleStock();
        var path = Assert.Single(evidence.OutputRetentionProofPaths);
        var retention = JsonSerializer.Deserialize<MoneyRevenueCropOutputRetentionProof>(File.ReadAllText(path), JsonOptions)!;
        Assert.Equal("money_revenue_crop_output_retention_proof.v3", retention.SchemaVersion);
        var request = JsonSerializer.Deserialize<MoneyRevenueCropProductionProofCheckpointRequest>(File.ReadAllText(original.Native), JsonOptions)!;
        var index = JsonSerializer.Deserialize<MoneyRevenueCropProductionProofFragmentIndex>(File.ReadAllText(request.FragmentIndexPath), JsonOptions)!;
        index.PlantingLayoutProofPaths = evidence.PlantingLayoutProofPaths;
        index.DailyCapacityProofPaths = evidence.DailyCapacityProofPaths;
        index.NativeLifecycleProofPaths = evidence.NativeLifecycleProofPaths;
        index.OutputRetentionProofPaths = evidence.OutputRetentionProofPaths;
        var output = Path.GetDirectoryName(evidence.ManifestPath)!;
        request.FragmentIndexPath = Path.Combine(output, "checkpoint-fragments.json");
        await File.WriteAllTextAsync(request.FragmentIndexPath, JsonSerializer.Serialize(index, JsonOptions));
        request.SchemaVersion = "money_revenue_crop_production_proof_checkpoint_request.v15";
        var requestPath = Path.Combine(output, "checkpoint-v15-request.json");
        await File.WriteAllTextAsync(requestPath, JsonSerializer.Serialize(request, JsonOptions));
        var checkpoint = MoneyRevenueCropProductionProofCheckpointBuilder.BuildNativeObserved(requestPath);
        Assert.Equal("money_revenue_crop_production_proof_checkpoint.v15", checkpoint.SchemaVersion);
        Assert.Equal(retention.SchemaVersion, Assert.Single(checkpoint.VerifiedOutputRetentions).SchemaVersion);
        Assert.Empty(checkpoint.VerifiedTerminalSettlements);
        Assert.False(checkpoint.FutureCropProductionProofReady); Assert.False(checkpoint.FormalProductTrainingAuthorized);
        foreach (var old in new[] { "v13", "v14" })
        {
            request.SchemaVersion = "money_revenue_crop_production_proof_checkpoint_request." + old;
            var oldPath = Path.Combine(output, "checkpoint-" + old + "-request.json");
            await File.WriteAllTextAsync(oldPath, JsonSerializer.Serialize(request, JsonOptions));
            Assert.Throws<InvalidDataException>(() => MoneyRevenueCropProductionProofCheckpointBuilder.BuildNativeObserved(oldPath));
        }
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropProductionProofCheckpointBuilder.Build(requestPath));

        var selectionType = typeof(CropTeacherManagedSeedOwner).Assembly.GetType("StardewAI.LiveTrainingLoop.CropTeacherTerminalShippingSelection")!;
        var selected = selectionType.GetMethod("OpenNativeVariantsConsumingSaleStock", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [evidence, f.NativePlanPath, f.SourcePath, f.LedgerPath])!;
        var target = (CropTeacherTerminalShippingTarget)selectionType.GetMethod("CreateTarget", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(selected, [0])!;
        Assert.True(target.RequireSaleStockReservations);
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropOutputRetentionProofBuilder.VerifyProducerTerminalTarget(
            retention, target with { RequireSaleStockReservations = false },
            JsonSerializer.Deserialize<StardewAI.Contracts.State.SnapshotEnvelope>(File.ReadAllText(f.SourcePath), JsonOptions)!));
    }
}
