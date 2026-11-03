using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StardewAI.Contracts.Execution;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class RainTotemMaterialReservationPlanBinderTests
{
    [Theory]
    [InlineData("normal", 0)]
    [InlineData("lost", 1)]
    public async Task ActualLiveRainTotemHookUsesSameJournaledSettlementRecovery(string mode, int lookups)
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-rain-live-recovery", Guid.NewGuid().ToString("N"));
        var repository = new FileStrategyCommitmentRepository(Path.Combine(root, "ledger"));
        var before = Snapshot();
        var plan = Plan(before, RainTotemMaterialReservationContract.Authorization(101, "Default"));
        var committed = RainTotemMaterialReservationPlanBinder.Bind(plan, before, repository)!;
        Assert.True(committed.Accepted, string.Join(";", committed.Errors));
        var item = new ActionQueueItem
        {
            QueueItemId = "isolated-live-rain-item", OptionId = "executor.craft_rain_totem",
            NormalizedCommand = new NormalizedCommand { Parameters = plan.Steps[0].Parameters }
        };
        var after = Snapshot();
        after.StateHash = "synthetic-after-rain-craft";
        await using var app = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IStrategyCommitmentRepository>();
            services.AddSingleton<IStrategyCommitmentRepository>(new FileStrategyCommitmentRepository(Path.Combine(root, "ledger")));
        }));
        using var setup = app.CreateClient();
        app.Services.GetRequiredService<StateStore>().Snapshots[after.StateHash] = after;
        var options = new LiveTrainingOptions { Root = root, RunId = "isolated-live-rain", BackendUrl = "http://localhost" };
        using var transport = new LivePortfolioSettlementTestHandler(app.Server.CreateHandler(), mode, options.SnapshotDir);
        using var http = new HttpClient(transport);
        var execution = new JsonObject
        {
            ["status"] = "applied", ["primitive_verification_status"] = "verified", ["after_snapshot_fresh"] = true,
            ["effective_before_state_hash"] = before.StateHash
        };
        var method = typeof(LiveTrainingOptions).Assembly.GetType("Program", true)!
            .GetMethod("SettleRainTotemMaterialReservationsAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        await (Task)method.Invoke(null, new object[]
        {
            http, options, JsonSerializer.SerializeToNode(item)!.AsObject(), execution, JsonSerializer.SerializeToNode(after)!.AsObject()
        })!;
        Assert.Equal(1, transport.MutationCalls);
        Assert.Equal(lookups, transport.LookupCalls);
        Assert.Equal("completed", execution["material_reservation_settlement_status"]!.GetValue<string>());
        Assert.Equal(lookups == 1, execution["material_reservation_settlement_reply_recovered"]?.GetValue<bool>() == true);
        Assert.Equal(3, execution["material_reservation_settlement_completed_ids"]!.AsArray().Count);
        Assert.All(new FileStrategyCommitmentRepository(Path.Combine(root, "ledger")).Get(after).MaterialReservations,
            row => Assert.Equal("completed", row.Status));
    }
}
