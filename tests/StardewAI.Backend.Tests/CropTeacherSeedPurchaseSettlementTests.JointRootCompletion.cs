using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedRootConsumesOnlyItsRetainedDecisionAndCanRestoreItAfterReopen(bool retainForeign)
    {
        // The existing fixture is regrowing. This explicit synthetic one-shot catalog isolates root completion,
        // without changing its default multi-wave intent or claiming vanilla growth evidence.
        using var f = new OvernightFixture(prepareSource: source =>
        {
            CompleteDailySelectionSurface(source);
            ChangeState(source.Before, "farm", node =>
            {
                node["crop_catalog"]!["value"]!.AsArray().Single(row => row!["seed_id"]!.GetValue<string>() == "745")!["regrow_days"] = null;
                foreach (var crop in node["crops"]!["value"]!.AsArray()) crop!["regrow_days"] = null;
            });
            ChangeState(source.Before, "current_location", node =>
                node["crops"] = JsonNode.Parse(source.Before.State["farm"].GetRawText())!["crops"]!.DeepClone());
        });
        await using var owner = await f.Open();
        await f.Home(owner);
        var night = await f.PrepareNight(owner);
        await owner.ExecuteOriginalOvernightAsync(night, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        await f.RebindNight(owner, night);
        await f.Farm(owner);
        var source = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(owner.CurrentSnapshotPath), JsonOptions)!;
        using var response = await f.Shared.Runtime.Client.PostAsJsonAsync("http://test/api/v1/planner/options/availability", new
        {
            state_hash = source.StateHash, candidates = new[] { new { option_id = "farm.maintain_crops",
                explicit_confirmation_granted = false, invocation_source = f.Shared.Runtime.LiveOptions.DailyPlanInvocationSource,
                parameters = Array.Empty<object>() } }
        }, JsonOptions);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var availability = await response.Content.ReadFromJsonAsync<OptionAvailabilityEnvelope>(JsonOptions);
        var candidates = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability!, "earn_money")
            .Where(row => row.Kind == "harvest_crop_tile").ToArray();
        Assert.Equal(3, candidates.Length);
        foreach (var row in candidates) row.Rank = row.TileX == 9 ? 3 : row.TileX!.Value + 1;
        var options = f.Shared.Runtime.LiveOptions;
        options.UseJointDailyCalendar = true; options.DailyPlanMaxCandidates = 3;
        using var handler = new DailyCompositionRankingHandler(f.Shared.Runtime.Handler, candidates);
        using var http = new HttpClient(handler, disposeHandler: false);
        async Task<object> Compose(CropTeacherManagedSeedOwner currentOwner, int iteration, object? selection)
        {
            var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(currentOwner.CurrentSnapshotPath), JsonOptions)!;
            var task = InvokeLive<Task>("ComposeCropTeacherDailyDispatchAsync", http, options, iteration, f.Shared.Inputs.Plan,
                f.Shared.Inputs.Request, currentOwner, new HashSet<string>(StringComparer.Ordinal), snapshot, selection!);
            await task;
            return task.GetType().GetProperty("Result")!.GetValue(task)!;
        }
        object? selection = null;
        if (retainForeign)
        {
            var first = await Compose(owner, 6, null);
            Assert.Equal(2, SelectionProperty<CropTeacherRunStep>(first, "Step")!.ActionBatch!.Tiles.Length);
            selection = SelectionProperty<object>(first, "Selection");
            Assert.NotNull(selection);
        }
        var harvest = await f.PrepareHarvest(owner);
        await owner.ExecuteOriginalHarvestAsync(harvest, f.Shared.Runtime.Client, () => f.EmitHarvest(owner));
        await f.RebindHarvest(owner, harvest);
        var progress = ReadManagedPlanningProgress(owner);
        var afterHarvest = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(owner.CurrentSnapshotPath), JsonOptions)!;
        var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(owner.CurrentLedgerPath), JsonOptions)!;
        var next = CropTeacherRunPlanner.Evaluate(f.Shared.Inputs.Plan, f.Shared.Inputs.Request, afterHarvest,
            completedHarvestTileKeys: progress.Harvest, completedDayBoundaryKeys: progress.Nights,
            resourceLedger: ledger);
        Assert.True(next.Stage == CropTeacherRunStageKinds.Complete, "stage=" + next.Stage + " reason=" + next.Reason);
        var result = await Compose(owner, 7, selection);
        Assert.Equal(retainForeign ? 1 : 0, handler.Ranks);
        Assert.Equal(!retainForeign, SelectionProperty<bool>(result, "ControlPreempted"));
        Assert.Null(SelectionProperty<CropTeacherRunStep>(result, "Step"));
        Assert.Equal(retainForeign, SelectionProperty<object>(result, "OtherGoalPreparation") is not null);
        Assert.Null(owner.Pending); Assert.Equal(5, f.Shared.Runtime.Handler.Commits);
        if (!retainForeign) return;
        Assert.Equal("pending", SelectionProperty<string>(SelectionProperty<object>(result, "OtherGoalPreparation")!, "QueueStatus"));
        await owner.DisposeAsync();
        await using var reopened = await f.Open();
        await reopened.RestoreHeadAsync(f.Shared.Runtime.Client, f.Shared.Files.Recovery, f.Shared.Files.Post);
        var restored = await Compose(reopened, 8, null);
        Assert.NotNull(SelectionProperty<object>(restored, "OtherGoalPreparation"));
        Assert.Equal("pending", SelectionProperty<string>(SelectionProperty<object>(restored, "OtherGoalPreparation")!, "QueueStatus"));
        Assert.Equal(1, handler.Ranks);
        Assert.Null(reopened.Pending); Assert.Equal(5, f.Shared.Runtime.Handler.Commits);
    }
}
