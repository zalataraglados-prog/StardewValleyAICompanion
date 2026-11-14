using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class CropTeacherNativeObservedCapacityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Lazy<Task<Baseline>> Golden = new(CreateBaseline);

    [Fact]
    public async Task DeclaredAlreadySatisfiedHouseDayHasZeroWorkWithoutRelaxingLegacyFarmRequirement()
    {
        var (path, day, request) = await Prepare();
        var proof = MoneyRevenueCropDailyCapacityProofBuilder.BuildNativeObserved(path);
        var actual = proof.Days.Single(row => row.TotalDay == day);
        Assert.Equal("money_revenue_crop_daily_capacity_proof.v2", proof.SchemaVersion);
        Assert.Equal(0, actual.WaterActionCount);
        Assert.Equal(0, actual.TotalTicks);
        Assert.True(actual.TimeBudgetFits && actual.EnergyBudgetFits && actual.WaterBudgetFits);
        Assert.False(proof.FormalProductTrainingAuthorized);
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropDailyCapacityProofBuilder.Build(path));
        request.SchemaVersion = "money_revenue_crop_daily_capacity_proof_request.v1";
        request.NativeDayTransitions = null;
        var legacy = Save(Path.GetDirectoryName(path)!, "legacy", request);
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropDailyCapacityProofBuilder.Build(legacy));
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropDailyCapacityProofBuilder.BuildNativeObserved(legacy));
    }

    [Fact]
    public async Task DeclaredSatisfiedDayRequiresNoNewEnergyOrAutonomousActionWindow()
    {
        var (path, day, _) = await Prepare("late_exhausted");
        var proof = MoneyRevenueCropDailyCapacityProofBuilder.BuildNativeObserved(path);
        var actual = proof.Days.Single(row => row.TotalDay == day);
        Assert.Equal(2230, actual.SnapshotStartTime);
        Assert.Equal(0d, actual.AvailableEnergy);
        Assert.Equal(0d, actual.MinimumEnergyReserve);
        Assert.Equal(0, actual.RequiredGameMinutes);
        Assert.True(actual.TimeBudgetFits && actual.EnergyBudgetFits);
    }

    [Theory]
    [InlineData("dry")]
    [InlineData("dead")]
    [InlineData("wrong_seed")]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("unavailable")]
    [InlineData("bad_time")]
    public async Task DeclaredSatisfiedDayDoesNotHideMissingOrUnsatisfiedWork(string fault)
    {
        var (path, _, _) = await Prepare(fault);
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropDailyCapacityProofBuilder.BuildNativeObserved(path));
    }

    private static async Task<(string Path, int Day, MoneyRevenueCropDailyCapacityProofRequest Request)> Prepare(string fault = "")
    {
        var baseline = await Golden.Value;
        var request = JsonSerializer.Deserialize<MoneyRevenueCropDailyCapacityProofRequest>(baseline.RequestJson, JsonOptions)!;
        var root = Path.Combine(Path.GetTempPath(), "native-observed-capacity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var snapshot = Read<SnapshotEnvelope>(baseline.DayPath);
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!;
        var crops = farm["crops"]!["value"]!.AsArray();
        foreach (var crop in crops.OfType<JsonObject>()) { crop["watered"] = true; crop["needs_watering"] = false; }
        if (fault == "dry") crops[0]!["needs_watering"] = true;
        if (fault == "dead") crops[0]!["dead"] = true;
        if (fault == "wrong_seed") crops[0]!["harvest_source_seed_id"] = "invalid";
        if (fault == "missing") crops.RemoveAt(0);
        if (fault == "duplicate") crops.Add(crops[0]!.DeepClone());
        if (fault == "unavailable") farm["crops"]!["status"] = "unavailable";
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        player["location_id"]!["value"] = "FarmHouse";
        if (fault == "late_exhausted") player["energy"]!["value"] = 0;
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);
        var locations = JsonNode.Parse(snapshot.State["locations"].GetRawText())!;
        locations["collision_grid"]!["value"]!["location_id"] = "FarmHouse";
        snapshot.State["locations"] = JsonSerializer.SerializeToElement(locations);
        if (fault is "bad_time" or "late_exhausted")
        {
            var time = JsonNode.Parse(snapshot.State["time"].GetRawText())!;
            time["time"]!["value"] = fault == "bad_time" ? 2700 : 2230;
            snapshot.State["time"] = JsonSerializer.SerializeToElement(time);
        }
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        var source = Save(root, "satisfied-source", snapshot);
        request.DailySnapshotPaths = request.DailySnapshotPaths.Select(path => path == baseline.DayPath ? source : path).ToArray();
        return (Save(root, "request", request), baseline.Day, request);
    }

    private static async Task<Baseline> CreateBaseline()
    {
        var fixture = new CropTeacherProductionEvidenceTests.Fixture();
        var evidence = await fixture.Build();
        var proof = Read<MoneyRevenueCropDailyCapacityProof>(Assert.Single(evidence.DailyCapacityProofPaths));
        var requestJson = ContentAddressedJsonArtifactReader.ReadAllText(proof.RequestPath);
        var nativePlan = Read<MoneyRevenueCropNativeProductionRunPlan>(fixture.NativePlanPath);
        var crop = Read<MoneyRevenueCropProductionObligationPlan>(nativePlan.CropProductionPlanPath).Commitments.Single();
        var day = crop.RequiredWateringTotalDays.First(value => value > crop.PlantingTotalDay &&
            crop.HarvestWaves.All(wave => wave.HarvestTotalDay != value));
        return new(requestJson, day, proof.Days.Single(row => row.TotalDay == day).SnapshotPath);
    }

    private sealed record Baseline(string RequestJson, int Day, string DayPath);
    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(ContentAddressedJsonArtifactReader.ReadAllText(path), JsonOptions)!;
    private static string Save<T>(string root, string name, T value)
    {
        var path = Path.Combine(root, name + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
        return path;
    }
}
