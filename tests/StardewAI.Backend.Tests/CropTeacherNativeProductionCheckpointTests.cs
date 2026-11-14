using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class CropTeacherNativeProductionCheckpointTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task NativeCheckpointCollectsOriginalNativeCertificatesWithoutInventingMissingTerminalOrTrainingCredit()
    {
        var f = new CropTeacherProductionEvidenceTests.Fixture();
        var paths = await Prepare(f);
        var checkpoint = MoneyRevenueCropProductionProofCheckpointBuilder.BuildNativeObserved(paths.Native);
        Assert.Equal("money_revenue_crop_production_proof_checkpoint.v13", checkpoint.SchemaVersion);
        Assert.True(Assert.Single(checkpoint.VerifiedDailyCapacities).DailyCapacityVerified);
        Assert.True(Assert.Single(checkpoint.VerifiedNativeLifecycles).NativeCropLifecycleVerified);
        Assert.True(Assert.Single(checkpoint.VerifiedOutputRetentions).NativeOutputRetentionVerified);
        Assert.Empty(checkpoint.VerifiedTerminalSettlements);
        Assert.True(checkpoint.PendingRequestCount > 0);
        Assert.False(checkpoint.FutureCropProductionProofReady);
        Assert.False(checkpoint.FreshSaveRevenueScheduleReady);
        Assert.False(checkpoint.FormalProductTrainingAuthorized);
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropProductionProofCheckpointBuilder.Build(paths.Native));
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropProductionProofCheckpointBuilder.Build(paths.Legacy));
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropProductionProofCheckpointBuilder.BuildNativeObserved(paths.Legacy));
    }

    [Fact]
    public async Task NativeCheckpointRebuildRejectsChangedNativeCapacityRatherThanTrustingItsFlag()
    {
        var f = new CropTeacherProductionEvidenceTests.Fixture();
        var paths = await Prepare(f);
        var proof = JsonNode.Parse(File.ReadAllText(paths.Capacity))!;
        proof["daily_capacity_verified"] = false;
        File.WriteAllText(paths.Capacity, proof.ToJsonString(JsonOptions));
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropProductionProofCheckpointBuilder.BuildNativeObserved(paths.Native));
    }

    private static async Task<(string Native, string Legacy, string Capacity)> Prepare(CropTeacherProductionEvidenceTests.Fixture f)
    {
        var production = await f.Build();
        var plan = JsonSerializer.Deserialize<MoneyRevenueCropNativeProductionRunPlan>(File.ReadAllText(f.NativePlanPath), JsonOptions)!;
        var index = Path.Combine(f.Output, "checkpoint-fragments.json");
        File.WriteAllText(index, JsonSerializer.Serialize(new MoneyRevenueCropProductionProofFragmentIndex
        {
            CropProductionPlanPath = plan.CropProductionPlanPath, CropProductionPlanSha256 = Hash(plan.CropProductionPlanPath),
            PlantingLayoutProofPaths = production.PlantingLayoutProofPaths, DailyCapacityProofPaths = production.DailyCapacityProofPaths,
            NativeLifecycleProofPaths = production.NativeLifecycleProofPaths, OutputRetentionProofPaths = production.OutputRetentionProofPaths
        }, JsonOptions));
        var request = new MoneyRevenueCropProductionProofCheckpointRequest
            { CropProductionPlanPath = plan.CropProductionPlanPath, FragmentIndexPath = index };
        var legacy = Path.Combine(f.Output, "legacy-checkpoint-request.json");
        File.WriteAllText(legacy, JsonSerializer.Serialize(request, JsonOptions));
        request.SchemaVersion = "money_revenue_crop_production_proof_checkpoint_request.v13";
        var native = Path.Combine(f.Output, "native-checkpoint-request.json");
        File.WriteAllText(native, JsonSerializer.Serialize(request, JsonOptions));
        return (native, legacy, Assert.Single(production.DailyCapacityProofPaths));
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
