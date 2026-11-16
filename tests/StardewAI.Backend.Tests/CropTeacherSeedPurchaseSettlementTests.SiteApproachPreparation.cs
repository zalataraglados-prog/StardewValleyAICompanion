using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void SiteApproachPreparationBindsFullSourceAndExactNativeStandWithoutCropArrivalCredit()
    {
        var f = SiteObservationFixture(); using var host = f.Runtime;
        var files = CompileSitePreparation(f);
        var before = SitePreparationBytes(f, files);
        var scope = OpenSitePreparation(f, files);
        Assert.True(scope.IsSiteApproach);
        Assert.Equal(new MoneyRevenueCropPlantingTile(2, 0), scope.SiteApproachTarget);
        Assert.Equal(f.Source.SourcePath, scope.SourcePath);
        Assert.Equal(f.Source.Before.StateHash, files.Queue.Items.Single().NormalizedCommand.StateHash);
        Assert.Contains(files.Observation, scope.InputHashes.Keys);
        Assert.NotEqual(ReadSiteSnapshot(files.Observation).StateHash, f.Source.Before.StateHash);
        var transition = SitePreparationTransition(f, files, scope);
        var result = scope.VerifyTransition(transition);
        Assert.True(result.IsSiteApproach); Assert.Equal(scope.SiteApproachTarget, result.SiteApproachTarget);
        Assert.False(result.RouteTargetReached); Assert.False(result.NativeCropLifecycleVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
        Assert.Contains(files.Observation, result.InputHashes.Keys);
        Assert.Equal(ReadSiteSnapshot(transition.AfterSnapshotPath).StateHash, result.RebindRequest.StateHash);
        foreach (var (path, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Throws<InvalidDataException>(() => CropTeacherSupportingRoute.Preparation.Open(f.Inputs.Plan.RequestPath,
            f.Source.SourcePath, f.Source.LedgerPath, f.Inputs.Plan.CoveredCommitmentIds,
            "site.preparation", "supporting_route_to_crop", files.Model, files.QueuePath));
        Assert.Equal(0, f.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("stand")]
    [InlineData("resource")]
    [InlineData("day")]
    [InlineData("actor")]
    [InlineData("receipt")]
    [InlineData("candidate")]
    public void SiteApproachPreparationRejectsUnprovenOrNonNeutralNativeOutcome(string change)
    {
        var f = SiteObservationFixture(); using var host = f.Runtime;
        var files = CompileSitePreparation(f); var scope = OpenSitePreparation(f, files);
        var transition = SitePreparationTransition(f, files, scope, after =>
        {
            if (change == "stand") ChangeState(after, "player", player => player["tile_x"]!["value"] = 4);
            if (change == "resource") ChangeState(after, "player", player => player["money"]!["value"] = 1);
            if (change == "day") ChangeState(after, "time", time => time["total_days"]!["value"] = scope.TotalDay + 1);
            if (change == "actor") after.PlayerId.Value += ".foreign";
        });
        if (change == "receipt")
        {
            var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(transition.ExecutionReceiptPath), JsonOptions)!;
            receipt.RunId += ".foreign"; Fixture.Save(transition.ExecutionReceiptPath, receipt);
        }
        if (change == "candidate") transition = transition with { SelectedCandidateId = "foreign" };
        var error = Assert.Throws<InvalidDataException>(() => scope.VerifyTransition(transition));
        if (change == "stand") Assert.Contains("exact selected stand", error.Message);
        Assert.Equal(0, f.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("target")]
    [InlineData("source")]
    [InlineData("actor")]
    [InlineData("candidate")]
    [InlineData("repair")]
    [InlineData("minutes")]
    [InlineData("failure")]
    [InlineData("fresh")]
    [InlineData("queue")]
    [InlineData("clear")]
    public void SiteApproachPreparationRejectsChangedOriginalModelOrQueue(string change)
    {
        var f = SiteObservationFixture(); using var host = f.Runtime;
        var files = CompileSitePreparation(f);
        var plan = JsonNode.Parse(File.ReadAllText(files.Model))!;
        var move = plan["steps"]![0]!;
        switch (change)
        {
            case "target": move["target_tile_x"] = 4; break;
            case "source": plan["state_hash"] = ReadSiteSnapshot(files.Observation).StateHash; break;
            case "actor": plan["execution_mode"] = "foreign"; break;
            case "candidate": plan["plan_id"] = "plan:foreign:1:3,2"; break;
            case "repair": move["parameters"]![0]!["value"] = "0"; break;
            case "minutes": move["estimated_minutes"] = 13; break;
            case "failure": move["failure_policy"] = new JsonArray(); break;
            case "fresh": move["parameters"]![2]!["value"] = "false"; break;
            case "queue": files.Queue.Items[0].NormalizedCommand.StateHash = "foreign"; break;
            case "clear": files.Queue.Items[0].OptionId = "executor.clear_obstacle"; break;
        }
        File.WriteAllText(files.Model, plan.ToJsonString(JsonOptions)); Fixture.Save(files.QueuePath, files.Queue);
        Assert.Throws<InvalidDataException>(() => OpenSitePreparation(f, files));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("observation")]
    [InlineData("ledger")]
    [InlineData("request")]
    [InlineData("model")]
    [InlineData("queue")]
    public void SiteApproachPreparationRetainsEveryInputThroughOutcome(string change)
    {
        var f = SiteObservationFixture(); using var host = f.Runtime;
        var files = CompileSitePreparation(f); var scope = OpenSitePreparation(f, files);
        var transition = SitePreparationTransition(f, files, scope); var result = scope.VerifyTransition(transition);
        var path = change switch
        {
            "source" => f.Source.SourcePath, "observation" => files.Observation, "ledger" => f.Source.LedgerPath,
            "request" => f.Inputs.Plan.RequestPath, "model" => files.Model, _ => files.QueuePath
        };
        File.AppendAllText(path, " ");
        Assert.Throws<InvalidDataException>(scope.ValidateInputsUnchanged);
        Assert.Throws<InvalidDataException>(() => scope.VerifyTransition(transition));
        if (change is not ("model" or "queue")) Assert.Throws<InvalidDataException>(result.ValidateInputsUnchanged);
    }

    [Fact]
    public void SiteApproachPreparationRejectsAlreadyAtStandAndPreservesLegacyOutcomeRole()
    {
        var f = SiteObservationFixture(atStand: true); using var host = f.Runtime;
        var files = CompileSitePreparation(f, new MoneyRevenueCropPlantingTile(3, 2));
        Assert.Throws<InvalidDataException>(() => OpenSitePreparation(f, files));
        var legacy = new Fixture();
        var (transition, _) = CreateSupportingRoute(legacy);
        var result = OpenRoute(legacy, transition);
        Assert.False(result.IsSiteApproach); Assert.Null(result.SiteApproachTarget); Assert.True(result.RouteTargetReached);
    }

    private sealed record SitePreparationFiles(string Observation, string Model, string QueuePath, ActionQueueEnvelope Queue);

    private static SitePreparationFiles CompileSitePreparation(OwnedRouteFixture f, MoneyRevenueCropPlantingTile? forced = null)
    {
        var observation = WriteSiteObservation(f);
        var selected = forced ?? new MoneyRevenueCropPlantingTile(OpenSiteObservation(f, observation).Target!.TileX,
            OpenSiteObservation(f, observation).Target!.TileY);
        var candidate = $"crop-site-approach:1:{selected.TileX},{selected.TileY}";
        var node = CropBoundedApproachPlanBuilder.Build(f.Source.Before.StateHash, candidate, "authoritative_crop_site_approach.v1",
            "site_preparation_starts_only_after_approach", selected.TileX, selected.TileY, ExecutionTargetProfiles.TrainingSingleplayer,
            JsonSerializer.SerializeToNode(ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer), JsonOptions)!);
        var model = Path.Combine(f.Runtime.LiveOptions.SnapshotDir, "site-model.json");
        File.WriteAllText(model, node.ToJsonString(JsonOptions));
        var plan = node.Deserialize<SmallModelPlanEnvelope>(JsonOptions)!;
        var queue = new ActionQueueCompiler().Compile(plan, f.Source.Before, f.Source.Ledger);
        Fixture.Save(f.QueuePath, queue);
        return new(observation, model, f.QueuePath, queue);
    }

    private static CropTeacherSupportingRoute.Preparation OpenSitePreparation(OwnedRouteFixture f, SitePreparationFiles files) =>
        CropTeacherSupportingRoute.Preparation.OpenSiteApproach(f.Inputs.Plan.RequestPath, f.Source.SourcePath,
            f.Source.LedgerPath, f.Inputs.Plan.CoveredCommitmentIds, Id, "site.preparation", files.Observation, files.Model, files.QueuePath);

    private static MoneyRevenueCropNativeLifecycleTransitionRequest SitePreparationTransition(OwnedRouteFixture f,
        SitePreparationFiles files, CropTeacherSupportingRoute.Preparation scope, Action<SnapshotEnvelope>? mutate = null)
    {
        var after = ReadSiteSnapshot(f.Source.SourcePath);
        ChangeState(after, "player", player =>
        { player["tile_x"]!["value"] = scope.SiteApproachTarget!.TileX; player["tile_y"]!["value"] = scope.SiteApproachTarget.TileY; });
        after.GameTick++; mutate?.Invoke(after); after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("LifecycleReceipt", files.Queue,
            f.Source.Before, after, "site.run", scope.CandidateId, "move_to_tile");
        Fixture.Save(f.AfterPath, after); Fixture.Save(f.ReceiptPath, receipt);
        return new("supporting_route_to_crop", scope.TotalDay, files.QueuePath, f.Source.SourcePath, f.ReceiptPath, f.AfterPath,
            receipt.RunId, PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, scope.CandidateId);
    }

    private static SnapshotEnvelope ReadSiteSnapshot(string path) => JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(path), JsonOptions)!;

    private static Dictionary<string, byte[]> SitePreparationBytes(OwnedRouteFixture f, SitePreparationFiles files) =>
        new[] { f.Inputs.Plan.RequestPath, f.Source.SourcePath, f.Source.LedgerPath, files.Observation, files.Model, files.QueuePath }
            .ToDictionary(path => path, File.ReadAllBytes);
}
