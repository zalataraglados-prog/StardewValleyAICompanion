using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherNativeObservedCapacityTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NativeRefillDiscoveryReadsTheOriginalExecutionResultNotFlattenedSummaryFields(bool refilled)
    {
        var root = Path.Combine(Path.GetTempPath(), "refill-result-discovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var result = Save(root, "execution", new TrainingExecutionResult
        { OptionId = "executor.water_crop", WaterBefore = 0, WaterAfter = refilled ? 39 : 0, ActualTicks = 420 });
        var receipt = Save(root, "aggregate", new { step_results = new[] { new
        { option_id = "executor.water_crop", water_before = 0, water_after = refilled ? 0 : 99, execution_path = result } } });
        var method = typeof(MoneyRevenueCropDailyCapacityProofBuilder).GetMethod("HasObservedRefill", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Equal(refilled, (bool)method.Invoke(null, [receipt])!);
        var legacy = Save(root, "legacy-summary", new { step_results = new[] { new
        { option_id = "executor.water_crop", water_before = 0, water_after = 99 } } });
        Assert.False((bool)method.Invoke(null, [legacy])!);
    }

    [Theory]
    [InlineData(420, true, true)]
    [InlineData(420, false, true)]
    [InlineData(421, true, false)]
    public async Task MeasuredCompleteRefillPrefixReplacesTheSameEstimatedWorkExactlyOnce(int ticks, bool oldTimeFits, bool fits)
    {
        var test = await RefillTimingCase(ticks, oldTimeFits);
        var actual = test.Apply();
        Assert.Equal(ticks, actual.TotalTicks);
        Assert.Equal(70, actual.MovementTicks);
        Assert.Equal(ticks - 70, actual.ActionTicks);
        Assert.Equal(40, actual.WateringCanRefillTicks);
        Assert.Equal(fits, actual.TimeBudgetFits);
        Assert.Equal(!fits, actual.BlockingReasons.Contains("crop_capacity_time_budget_exceeded"));
        Assert.True(actual.WaterBudgetFits);
        Assert.Equal(test.Original.EnergyBudgetFits, actual.EnergyBudgetFits);
    }

    [Theory]
    [InlineData("missing_water")]
    [InlineData("uncovered_plant")]
    [InlineData("uncovered_harvest")]
    [InlineData("dry")]
    [InlineData("changed_capture")]
    public async Task MeasuredRefillBudgetCannotReplaceUncoveredWorkOrChangedFinalCapture(string fault)
    {
        var test = await RefillTimingCase(420, false, fault);
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => test.Apply()).InnerException);
    }

    [Theory]
    [InlineData("energy")]
    [InlineData("unreachable")]
    public async Task MeasuredRefillTimeDoesNotRemoveIndependentEnergyOrRouteBlocks(string fault)
    {
        var test = await RefillTimingCase(420, false, fault);
        var actual = test.Apply();
        Assert.True(actual.TimeBudgetFits);
        Assert.Equal(test.Original.EnergyBudgetFits, actual.EnergyBudgetFits);
        Assert.Equal(test.Original.AllTargetsReachable, actual.AllTargetsReachable);
        Assert.Equal(test.Original.BlockingReasons.Where(reason => reason is not
            "crop_capacity_time_budget_exceeded" and not "crop_capacity_watering_can_requires_unmodeled_refill_route"), actual.BlockingReasons);
    }

    // Declared timing-kernel inputs; this is not a native execution or whole-prefix provenance certificate.
    private static async Task<(CropMaintenanceCapacityEvaluation Original, Func<CropMaintenanceCapacityEvaluation> Apply)>
        RefillTimingCase(int ticks, bool oldTimeFits, string fault = "")
    {
        var baseline = await Golden.Value;
        var request = JsonSerializer.Deserialize<MoneyRevenueCropDailyCapacityProofRequest>(baseline.RequestJson, JsonOptions)!;
        var layoutPath = Assert.Single(request.PlantingLayoutProofPaths);
        var layout = Read<MoneyRevenueCropPlantingLayoutProof>(layoutPath);
        var plan = Read<MoneyRevenueCropProductionObligationPlan>(request.CropProductionPlanPath);
        var commitment = plan.Commitments.Single(row => row.CommitmentId == layout.CommitmentId);
        var root = Path.Combine(Path.GetTempPath(), "refill-measured-timing-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var before = Read<SnapshotEnvelope>(baseline.DayPath);
        var after = Read<SnapshotEnvelope>(baseline.DayPath);
        var farm = JsonNode.Parse(after.State["farm"].GetRawText())!;
        foreach (var crop in farm["crops"]!["value"]!.AsArray().OfType<JsonObject>())
        { crop["watered"] = true; crop["needs_watering"] = false; }
        if (fault == "dry") farm["crops"]!["value"]![0]!["needs_watering"] = true;
        after.State["farm"] = JsonSerializer.SerializeToElement(farm);
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        var afterPath = Save(root, "after", after);
        var targets = layout.Tiles.Select(tile => new CropMaintenanceTileTarget(tile.TileX, tile.TileY, false, true, false, false)).ToArray();
        var work = targets.Select(tile => ("executor.water_crop", layout.LocationId, tile.TileX, tile.TileY)).ToHashSet();
        if (fault == "missing_water") work.Remove(work.First());
        if (fault == "uncovered_plant") targets[0] = targets[0] with { Plant = true };
        if (fault == "uncovered_harvest") targets[0] = targets[0] with { Harvest = true };
        var reasons = new List<string> { "crop_capacity_watering_can_requires_unmodeled_refill_route" };
        if (!oldTimeFits) reasons.Add("crop_capacity_time_budget_exceeded");
        if (fault == "energy") reasons.Add("crop_capacity_energy_budget_exceeded");
        if (fault == "unreachable") reasons.Add("crop_capacity_target_route_unreachable");
        var original = CropMaintenanceCapacityEvaluator.Evaluate(before, layout.LocationId, targets) with
        {
            AvailableGameMinutes = 10, WaterBudgetFits = false, TimeBudgetFits = oldTimeFits,
            EnergyBudgetFits = fault != "energy", AllTargetsReachable = fault != "unreachable", BlockingReasons = reasons.ToArray()
        };
        var type = typeof(MoneyRevenueCropDailyCapacityProofBuilder);
        object New(string name, params object[] args) => Activator.CreateInstance(type.GetNestedType(name, BindingFlags.NonPublic)!, args)!;
        string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        var layouts = Array.CreateInstance(type.GetNestedType("LayoutState", BindingFlags.NonPublic)!, 1);
        layouts.SetValue(New("LayoutState", layoutPath, Hash(layoutPath), layout, commitment, false), 0);
        var day = New("SnapshotState", baseline.DayPath, before, baseline.Day);
        var receiptPath = Save(root, "declared-refill", new { declared_timing_only = true });
        var receipt = New("RefillReceiptState", receiptPath, Hash(receiptPath), ticks);
        var flow = New("NativeRefillFlow", receipt, afterPath, Hash(afterPath), after.StateHash, 70, 40, work);
        if (fault == "changed_capture") File.AppendAllText(afterPath, " ");
        CropMaintenanceCapacityEvaluation Apply() => (CropMaintenanceCapacityEvaluation)type
            .GetMethod("ApplyObservedRefillFlow", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [day, layouts, targets, original, flow])!;
        return (original, Apply);
    }
}
