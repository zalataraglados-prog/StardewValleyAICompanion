using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("clear", 0)]
    [InlineData("access", 1)]
    public void SiteClearanceCompilationBindsOriginalFirstFrontierWithoutMutationOrCredit(string frontier, int targetX)
    {
        var f = new SiteTillFiles(frontier: frontier); using var host = f.F.Runtime;
        var paths = new[] { f.Request, f.Plan, f.Narrow, f.Full, f.Queue, f.Observation, f.F.Source.SourcePath, f.F.Source.LedgerPath };
        var bytes = paths.ToDictionary(path => path, File.ReadAllBytes);
        var scope = f.OpenClear();
        var step = MoneyRevenueCropSitePreparationPlanBuilder.SelectNextExecutableStep(f.SitePlan)!;
        Assert.Equal(step.CandidateId, scope.CandidateId);
        Assert.Equal(new MoneyRevenueCropPlantingTile(0, 0), scope.PreparationTarget);
        Assert.Equal(new MoneyRevenueCropPlantingTile(targetX, 0), scope.Target);
        Assert.Equal(new MoneyRevenueCropPlantingTile(
            int.Parse(step.Parameters.Single(p => p.Name == "stand_tile_x").Value),
            int.Parse(step.Parameters.Single(p => p.Name == "stand_tile_y").Value)), scope.Stand);
        Assert.Equal("(O)784", scope.ClearSourceId);
        Assert.Equal(Path.GetFullPath(f.F.Source.SourcePath), scope.SourcePath);
        Assert.Equal(Path.GetFullPath(f.Observation), scope.ObservationPath);
        Assert.Equal(Path.GetFullPath(f.Queue), scope.QueuePath);
        Assert.Equal(f.F.Source.Before.StateHash, scope.SourceStateHash);
        Assert.NotEqual(scope.SourceStateHash, scope.ObservationStateHash);
        Assert.Equal(f.SitePlan.PreparationTotalDay, scope.TotalDay);
        var productionPath = f.F.Inputs.Request.CropProductionPlanPath;
        var production = JsonSerializer.Deserialize<MoneyRevenueCropProductionObligationPlan>(
            File.ReadAllText(productionPath), JsonOptions)!;
        var productionRequest = JsonSerializer.Deserialize<MoneyRevenueCropProductionObligationPlanRequest>(
            File.ReadAllText(production.RequestPath), JsonOptions)!;
        var expectedPaths = paths.Concat(new[] { f.F.Inputs.Plan.RequestPath, productionPath, production.RequestPath,
            production.SnapshotPath, production.StrategyLedgerPath,
            productionRequest.SnapshotPath, productionRequest.StrategyLedgerPath }).Select(Path.GetFullPath)
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        Assert.Equal(expectedPaths.Order(StringComparer.Ordinal), scope.InputHashes.Keys.Order(StringComparer.Ordinal));
        Assert.False(scope.NativeOutcomeVerified); Assert.False(scope.ResourceBoundaryVerified);
        Assert.False(scope.FormalProductTrainingAuthorized);
        Assert.IsAssignableFrom<ICropTeacherSupportingPreparation>(scope);
        Assert.NotSame(scope.BaseLedger, scope.BaseLedger);
        scope.ValidateInputsUnchanged();
        foreach (var path in paths) Assert.Equal(bytes[path], File.ReadAllBytes(path));
        Assert.Throws<InvalidDataException>(() => f.Open()); // Cannot skip this clear for a later till.
        Assert.Equal(0, f.F.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("till")]
    [InlineData("noop")]
    public void SiteClearanceCompilationCannotTakeTillOrNoopFrontier(string frontier)
    {
        var f = new SiteTillFiles(frontier: frontier); using var host = f.F.Runtime;
        Assert.Throws<InvalidDataException>(() => f.OpenClear());
        Assert.Equal(0, f.F.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("request")]
    [InlineData("run")]
    [InlineData("plan")]
    [InlineData("foreign")]
    [InlineData("later")]
    [InlineData("narrow")]
    [InlineData("full")]
    [InlineData("queue")]
    [InlineData("actor")]
    [InlineData("layout")]
    [InlineData("target")]
    [InlineData("stand")]
    [InlineData("source_id")]
    [InlineData("alias")]
    [InlineData("swap")]
    public void SiteClearanceCompilationRejectsChangedOriginalBinding(string change)
    {
        var f = new SiteTillFiles(frontier: "clear"); using var host = f.F.Runtime;
        void Edit(string path, Action<JsonNode> mutate)
        { var json = JsonNode.Parse(File.ReadAllText(path))!; mutate(json); Fixture.Save(path, json); }
        switch (change)
        {
            case "source": Edit(f.F.Source.SourcePath, node => node["game_tick"] = -1); break;
            case "request": Edit(f.Request, node => node["crop_production_plan_path"] = f.Plan); break;
            case "run": Edit(f.F.Inputs.Plan.RequestPath, node => node["formal_product_training_authorized"] = true); break;
            case "plan": Edit(f.Plan, node => node["preparation_total_day"] = 999); break;
            case "foreign": Edit(f.Plan, node => node["steps"]![0]!["action_option_id"] = "executor.break_farm_resource_clump"); break;
            case "later": Edit(f.Plan, node => node["steps"]![0] = node["steps"]![1]!.DeepClone()); break;
            case "narrow": Edit(f.Narrow, node => node["state_hash"] = f.F.Source.Before.StateHash); break;
            case "full": Edit(f.Full, node => node["state_hash"] = "narrow"); break;
            case "queue": Edit(f.Queue, node => node["items"]![0]!["normalized_command"]!["state_hash"] = "narrow"); break;
            case "actor": Edit(f.Full, node => node["actor"]!["player_id"] = "foreign"); break;
            case "layout": Edit(f.Request, node => node["tiles"]![0]!["tile_x"] = 3); break;
            case "target": case "stand": case "source_id":
                Edit(f.Full, node => node["actions"]![0]!["parameters"]!.AsArray().Single(row =>
                    row!["name"]!.GetValue<string>() == (change == "target" ? "target_tile_x" :
                        change == "stand" ? "stand_tile_x" : "clear_source_id"))!["value"] = "999"); break;
        }
        Assert.Throws<InvalidDataException>(() => f.OpenClear(original: change == "alias" ? f.Full :
            change == "swap" ? f.Full : null, full: change == "swap" ? f.Narrow : null));
        Assert.Equal(0, f.F.Runtime.Handler.Commits);
    }

    [Fact]
    public void SiteClearanceCompilationRetainsEveryInputAgainstPhysicalChanges()
    {
        var f = new SiteTillFiles(frontier: "clear"); using var host = f.F.Runtime;
        var scope = f.OpenClear();
        foreach (var path in scope.InputHashes.Keys)
        {
            var bytes = File.ReadAllBytes(path);
            File.AppendAllText(path, " ");
            Assert.Throws<InvalidDataException>(scope.ValidateInputsUnchanged);
            File.WriteAllBytes(path, bytes);
            scope.ValidateInputsUnchanged();
        }
        Assert.Equal(0, f.F.Runtime.Handler.Commits);
    }
}
