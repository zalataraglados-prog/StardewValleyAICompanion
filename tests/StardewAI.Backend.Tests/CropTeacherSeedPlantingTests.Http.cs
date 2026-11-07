using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StardewAI.Contracts.Execution;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingTests
{
    [Fact]
    public async Task ActualBackendKeepsOwnedBatchMechanicalWithoutNestedGoalTeacher()
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var actions = marker.BuildActions(f.Batch(), f.OwnedSlotOne);
        using var app = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IStrategyCommitmentRepository>();
            services.AddSingleton<IStrategyCommitmentRepository>(f.Repository);
        }));
        using var client = app.CreateClient();
        app.Services.GetRequiredService<StateStore>().Snapshots[f.Before.StateHash] = f.Before;
        using var reply = await client.PostAsJsonAsync("/api/v1/small-model/action-queue/compile", new SmallModelActionEnvelope
        {
            ModelOutputId = "owned-planting-http-fixture", SourceModel = "deterministic.fixture", GoalId = "earn_money",
            StateHash = f.Before.StateHash, Actor = ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer), Actions = actions
        });
        Assert.True(reply.IsSuccessStatusCode, await reply.Content.ReadAsStringAsync());
        var queue = (await reply.Content.ReadFromJsonAsync<JsonObject>())!;
        Assert.Equal("pending", queue["status"]!.GetValue<string>());
        var items = queue["items"]!.AsArray();
        Assert.Equal(8, items.Count);
        Assert.All(items, row => Assert.Equal("executor.plant_seed", row!["option_id"]!.GetValue<string>()));
        Fixture.Save(f.QueuePath, queue);
        marker.ValidateQueue(f.QueuePath);
        Assert.Equal(marker.Ledger.Revision, f.Repository.Get(f.Before).Revision);
    }
}
