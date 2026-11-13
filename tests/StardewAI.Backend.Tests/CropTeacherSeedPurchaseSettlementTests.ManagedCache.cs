using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task ManagedSeedPurchaseCacheReimportsAcceptedSourceBeforePreparingOriginalPurchase()
    {
        using var setup = new OwnedRouteFixture(prepareSource: source =>
        {
            if (!source.Before.State.ContainsKey("menus")) source.Before.State["menus"] = JsonSerializer.SerializeToElement(new JsonObject());
            ChangeState(source.Before, "time", node => node["time"] = JsonSerializer.SerializeToNode(new { status = "available", value = 900 }));
            ChangeState(source.Before, "menus", node =>
            {
                node["active_menu"] = JsonSerializer.SerializeToNode(new { status = "available", value = new { is_open = true, type = "ShopMenu" } });
                node["sleep_prompt_context"] = JsonSerializer.SerializeToNode(new { status = "available", value = new { prompt_open = false } });
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
        var f = setup.Source;
        var runtime = setup.Runtime;
        runtime.LiveOptions.UseRuntimeTestHarnessExecutor = true;
        await using var owner = await setup.Files.Open();
        var step = CropTeacherRunPlanner.Evaluate(setup.Inputs.Plan, setup.Inputs.Request, f.Before, resourceLedger: f.Ledger);
        Assert.Equal(CropTeacherRunStageKinds.AcquireSeed, step.Stage);
        runtime.Store.Snapshots.Clear();
        using var missing = await runtime.Client.GetAsync(runtime.LiveOptions.BackendUrl +
            "/api/v1/strategy/commitments/latest?stateHash=" + f.Before.StateHash);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var sourceBytes = File.ReadAllBytes(f.SourcePath);
        var task = InvokeLive<Task>("BuildCropTeacherOwnedControlQueueAsync", runtime.Client, runtime.LiveOptions, 1, step,
            setup.Inputs.Plan, setup.Inputs.Request, owner);
        await task;
        var built = task.GetType().GetProperty("Result")!.GetValue(task)!;
        var build = built.GetType().GetProperty("Build")!.GetValue(built)!;
        var operation = (CropTeacherManagedSeedOperation)built.GetType().GetProperty("Operation")!.GetValue(built)!;
        var prepared = Assert.IsType<CropTeacherSeedPurchaseRuntime>(build.GetType().GetProperty("SeedPurchaseSupport")!.GetValue(build));
        var queue = ((JsonObject)build.GetType().GetProperty("Queue")!.GetValue(build)!).Deserialize<ActionQueueEnvelope>(JsonOptions)!;
        Assert.Same(owner.Pending, operation);
        Assert.Equal(CropTeacherRunStageKinds.AcquireSeed, operation.ReadStep().Stage);
        Assert.True(queue.Status == "pending", string.Join(",", queue.CompilerDiagnostics.Concat(
            queue.Items.SelectMany(item => item.BlockingReasons.Concat(item.MissingStateFactors)))));
        Assert.Equal(f.Before.StateHash, queue.StateHash);
        Assert.Empty(queue.CompilerDiagnostics);
        Assert.NotEmpty(queue.Items);
        Assert.Throws<InvalidOperationException>(() => prepared.ValidateDispatchQueue(
            Path.Combine(operation.RuntimeDirectory, "compiled-seed-queue.json")));
        Assert.Equal("full", build.GetType().GetProperty("SnapshotProfile")!.GetValue(build));
        Assert.True(runtime.Store.Snapshots.ContainsKey(f.Before.StateHash));
        Assert.Equal(sourceBytes, File.ReadAllBytes(f.SourcePath));
        Assert.Equal(1, runtime.Handler.Commits);
        Assert.Equal(0, runtime.Handler.Settlements);
        Assert.False(File.Exists(setup.ReceiptPath));
        Assert.Empty(Directory.EnumerateFiles(operation.RuntimeDirectory, "execution-*.json", SearchOption.AllDirectories));
    }
}
