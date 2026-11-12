using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task ManagedLoopCannotDispatchAnUnownedPriorityControlInsteadOfItsPendingPhase()
    {
        using var f = new OwnedRouteFixture();
        await using var owner = await f.Files.Open();
        var error = Assert.Throws<TargetInvocationException>(() => InvokeLive<object>("RequireManagedCropDispatch", owner, null!));
        Assert.IsType<InvalidOperationException>(error.InnerException);
        var operation = (await BuildManagedOperation(f.Runtime, f.Inputs, owner, f.Step))!;
        InvokeLive<object>("RequireManagedCropDispatch", owner, operation);
        Assert.Equal(0, f.Runtime.Handler.Commits);
    }

    [Fact]
    public async Task ManagedLoopSelectorContinuesTheOriginalOwnerAcrossOvernightAndHarvest()
    {
        using var f = new OvernightFixture();
        await using var owner = await f.Open();
        await f.Home(owner);
        var day = JsonNode.Parse(File.ReadAllText(owner.CurrentSnapshotPath))!["state"]!["time"]!["total_days"]!["value"]!.GetValue<int>();
        var sleep = new CropTeacherRunStep(CropTeacherRunStageKinds.NativeSleep, day, "", "FarmHouse", "FarmHouse", "", "", 0, 0, null, "declared_managed_selector");
        var night = (await BuildManagedOperation(f.Shared.Runtime, f.Shared.Inputs, owner, sleep, 3))!;
        Assert.True(night.IsOvernight);
        await Assert.ThrowsAsync<InvalidOperationException>(() => BuildManagedOperation(f.Shared.Runtime, f.Shared.Inputs, owner, sleep, 9));
        await owner.ExecuteOriginalOvernightAsync(night, f.Shared.Runtime.Client, () => f.EmitNight(owner));
        await f.RebindNight(owner, night);
        await f.Farm(owner);
        var nextDay = day + 1;
        var batch = f.Shared.Inputs.Plan.Days.Single(row => row.TotalDay == nextDay).ActionBatches.Single(row => row.OptionId == "executor.harvest_crop");
        var harvestStep = new CropTeacherRunStep(CropTeacherRunStageKinds.ExecuteCropBatch, nextDay, Id, "Farm", "FarmHouse", "", "", 0, 0, batch, "declared_managed_selector");
        var harvest = (await BuildManagedOperation(f.Shared.Runtime, f.Shared.Inputs, owner, harvestStep, 5))!;
        Assert.True(harvest.IsHarvest);
        await owner.ExecuteOriginalHarvestAsync(harvest, f.Shared.Runtime.Client, () => f.EmitHarvest(owner));
        await f.RebindHarvest(owner, harvest);
        Assert.Equal(1, f.NativeNightCalls); Assert.Equal(1, f.NativeHarvestCalls);
        Assert.Equal(5, f.Shared.Runtime.Handler.Commits); Assert.Null(owner.Pending);
        Assert.Equal(f.HarvestAfter, owner.CurrentSnapshotPath);
    }

    [Fact]
    public async Task ManagedLoopRouteUsesOriginalOwnedExecutionWithoutCallingTheGenericFallback()
    {
        using var f = new OwnedRouteFixture();
        await using var owner = await f.Files.Open();
        var operation = await BuildManagedOperation(f.Runtime, f.Inputs, owner, f.Step);
        Assert.NotNull(operation); Assert.True(operation.IsSupportingRoute);
        using var transport = new OriginalRouteTransport(f);
        using var client = new HttpClient(transport);
        var result = await InvokeLive<Task<JsonObject>>("ExecuteCropTeacherManagedOperationAsync", client, client,
            f.Runtime.LiveOptions, 1, owner, operation, f.QueuePath,
            (Func<Task<JsonObject>>)(() => throw new InvalidOperationException("Generic fallback must never execute.")));
        Assert.Equal(f.AfterPath, result["after_snapshot_path"]!.GetValue<string>());
        Assert.Equal(1, transport.NativeRequests); Assert.Equal(1, f.Runtime.Handler.Commits);
        Assert.Null(owner.Pending); Assert.Equal(f.AfterPath, owner.CurrentSnapshotPath);
    }

    [Fact]
    public async Task ManagedLoopSeedPreparationRemainsInsideItsOwnerAndDoesNotLoseThePreparedRuntime()
    {
        using var setup = new OwnedRouteFixture(prepareSource: source =>
        {
            if (!source.Before.State.ContainsKey("menus")) source.Before.State["menus"] = JsonSerializer.SerializeToElement(new JsonObject());
            ChangeState(source.Before, "time", node => node["time"] = JsonSerializer.SerializeToNode(new { status = "available", value = 900 }));
            ChangeState(source.Before, "menus", node =>
            {
                node["active_menu"] = JsonSerializer.SerializeToNode(new { status = "available", value = new { is_open = true, type = "ShopMenu" } });
                node["shop_stock"] = JsonSerializer.SerializeToNode(new { status = "available", value = new
                {
                    shop_id = "SeedShop", read_only = false, entries = new[] { new
                    {
                        item_id = "745", qualified_item_id = "(O)745", synced_key = "(O)745", stack = 1, quality = 0, price = 75,
                        executor_purchase_enabled = true, executor_block_reasons = Array.Empty<string>(), can_buy_item = true,
                        infinite_stock = true, stock = 999, can_afford_one_with_currency = true,
                        can_afford_one_with_trade_item = true, could_inventory_accept = true
                    } }
                } });
            });
        });
        var f = setup.Source; var runtime = setup.Runtime; var inputs = setup.Inputs; var files = setup.Files;
        await using var owner = await files.Open();
        var step = CropTeacherRunPlanner.Evaluate(inputs.Plan, inputs.Request, f.Before, resourceLedger: f.Ledger);
        CropTeacherManagedSeedOperation? operation;
        try { operation = await BuildManagedOperation(runtime, inputs, owner, step); }
        catch (InvalidDataException error)
        {
            var path = Path.Combine(runtime.LiveOptions.SnapshotDir, "crop-teacher-seed-0001-ranking.json");
            if (!File.Exists(path)) throw;
            var ranking = JsonNode.Parse(File.ReadAllText(path))!;
            throw new InvalidDataException(error.Message + "; resolution=" + ranking["goal_resolution"]?.ToJsonString() +
                "; candidates=" + ranking["ranked_event_candidates"]?.AsArray().Count, error);
        }
        Assert.NotNull(operation); Assert.Equal(CropTeacherRunStageKinds.AcquireSeed, operation.ReadStep().Stage);
        Assert.Same(operation, owner.Pending);
        Assert.StartsWith(files.OwnerRoot + Path.DirectorySeparatorChar, operation.RuntimeDirectory);
        var prepared = JsonNode.Parse(File.ReadAllText(Path.Combine(files.OwnerRoot, "operation-000001-prepared.json")))!;
        Assert.Equal("owner_loop.v1", prepared["dispatch_protocol"]!.GetValue<string>());
        Assert.Equal(Path.Combine(operation.RuntimeDirectory, "compiled-seed-queue.json"), prepared["queue_path"]!.GetValue<string>());
        Assert.Equal(1, runtime.Handler.Commits); Assert.Equal(0, runtime.Handler.Settlements);
    }

    private static async Task<CropTeacherManagedSeedOperation?> BuildManagedOperation(RuntimeFixture runtime,
        OwnerInputs inputs, CropTeacherManagedSeedOwner owner, CropTeacherRunStep step, int iteration = 1)
    {
        var task = InvokeLive<Task>("BuildCropTeacherOwnedControlQueueAsync", runtime.Client, runtime.LiveOptions,
            iteration, step, inputs.Plan, inputs.Request, owner);
        await task;
        var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
        return (CropTeacherManagedSeedOperation?)result.GetType().GetProperty("Operation")!.GetValue(result);
    }
}
