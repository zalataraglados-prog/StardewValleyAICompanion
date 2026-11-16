using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void SiteTillCompilationRetainsOriginalWholeRecipeFullSourceAndReadOnlyFalseFlags()
    {
        var f = new SiteTillFiles(); using var host = f.F.Runtime;
        var paths = new[] { f.Observation, f.Request, f.Plan, f.Narrow, f.Full, f.Queue, f.F.Source.SourcePath, f.F.Source.LedgerPath };
        var before = paths.ToDictionary(path => path, File.ReadAllBytes);
        var scope = f.Open();
        Assert.Equal(f.F.Source.Before.StateHash, scope.SourceStateHash);
        Assert.NotEqual(scope.SourceStateHash, scope.ObservationStateHash);
        Assert.Equal(f.SitePlan.Steps[0].CandidateId, scope.CandidateId);
        Assert.Equal(new MoneyRevenueCropPlantingTile(0, 0), scope.Target);
        Assert.Equal(1, scope.ToolSlotIndex); Assert.Equal("(T)Hoe", scope.ToolQualifiedItemId);
        Assert.Equal(2d, scope.ProjectedEnergyCost);
        Assert.False(scope.NativeOutcomeVerified); Assert.False(scope.ResourceBoundaryVerified);
        Assert.False(scope.FormalProductTrainingAuthorized); scope.ValidateInputsUnchanged();
        foreach (var path in paths) Assert.Equal(before[path], File.ReadAllBytes(path));
        Assert.Equal(0, f.F.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("frame")]
    [InlineData("actor")]
    [InlineData("commitment")]
    [InlineData("layout")]
    [InlineData("request")]
    [InlineData("plan")]
    [InlineData("clear")]
    [InlineData("model")]
    [InlineData("tool")]
    [InlineData("target")]
    [InlineData("stand")]
    [InlineData("parameters")]
    [InlineData("queue")]
    [InlineData("alias")]
    public void SiteTillCompilationRejectsChangedOriginalScopeAndCannotSkipClear(string change)
    {
        var f = new SiteTillFiles(); using var host = f.F.Runtime;
        void Edit(string path, Action<JsonNode> mutate)
        { var json = JsonNode.Parse(File.ReadAllText(path))!; mutate(json); Fixture.Save(path, json); }
        switch (change)
        {
            case "frame": Edit(f.Observation, node => node["game_tick"] = -1); break;
            case "actor": Edit(f.Full, node => node["actor"]!["player_id"] = "other"); break;
            case "layout": Edit(f.Request, node => node["tiles"]![0]!["tile_x"] = 3); break;
            case "request": Edit(f.Request, node => node["crop_production_plan_path"] = f.Plan); break;
            case "plan": Edit(f.Plan, node => node["preparation_total_day"] = 999); break;
            case "clear": Edit(f.Plan, node => node["steps"]![0]!["plan_status"] = "clear_then_fresh_snapshot_rebind"); break;
            case "model": Edit(f.Narrow, node => node["state_hash"] = "wrong"); break;
            case "tool": case "target": case "stand": case "parameters":
                Edit(f.Full, node =>
                {
                    var rows = node["actions"]![0]!["parameters"]!.AsArray();
                    if (change == "parameters") rows.RemoveAt(0);
                    else rows.Single(row => row!["name"]!.GetValue<string>() ==
                        (change == "tool" ? "tool_slot_index" : change == "target" ? "target_tile_x" : "stand_tile_x"))!["value"] = "999";
                }); break;
            case "queue": Edit(f.Queue, node => node["items"]![0]!["normalized_command"]!["state_hash"] = "narrow"); break;
        }
        Assert.Throws<InvalidDataException>(() => f.Open(original: change == "alias" ? f.Full : null,
            commitment: change == "commitment" ? "other" : null));
        Assert.Equal(0, f.F.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("hoe_tool_slot_index")]
    [InlineData("hoe_tool_qualified_item_id")]
    [InlineData("hoe_energy_cost")]
    public void SiteTillCompilationRejectsContradictoryNativeToolMirrors(string field)
    {
        var f = new SiteTillFiles(context =>
        {
            var target = context["tiles"]!.AsArray().Single(row => row!["tile_x"]!.GetValue<int>() == 0 && row["tile_y"]!.GetValue<int>() == 0)!;
            if (field == "hoe_tool_qualified_item_id") target[field] = "(T)Pickaxe";
            else target[field] = 999;
        });
        using var host = f.F.Runtime;
        Assert.Throws<InvalidDataException>(() => f.Open());
        Assert.Equal(0, f.F.Runtime.Handler.Commits);
    }

    [Fact]
    public void SiteTillCompilationPinsEveryExplicitAndTransitiveInputWithFreshPhysicalIntegrity()
    {
        var f = new SiteTillFiles(); using var host = f.F.Runtime;
        var scope = f.Open();
        Assert.Contains(Path.GetFullPath(f.F.Inputs.Request.CropProductionPlanPath), scope.InputHashes.Keys);
        foreach (var path in scope.InputHashes.Keys)
        {
            var original = File.ReadAllBytes(path);
            File.AppendAllText(path, " ");
            Assert.Throws<InvalidDataException>(scope.ValidateInputsUnchanged);
            File.WriteAllBytes(path, original);
            scope.ValidateInputsUnchanged();
        }
    }
}
