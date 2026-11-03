using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class BackendEndpointTests
{
    [Theory]
    [InlineData("executor.donate_museum_item", false)]
    [InlineData("executor.donate_museum_item", true)]
    [InlineData("executor.craft_machine_item", false)]
    [InlineData("executor.craft_machine_item", true)]
    public async Task LiveDispatchChecksUnregisteredLocalItemsAgainstTheCurrentLedger(
        string optionId, bool changeLedger)
    {
        using var isolatedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStrategyCommitmentRepository>();
                services.AddSingleton<IStrategyCommitmentRepository>(new InMemoryStrategyCommitmentRepository());
            }));
        using var client = isolatedFactory.CreateClient();
        using var ingest = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent(includeMaterialGraph: true));
        ingest.EnsureSuccessStatusCode();
        var stateHash = (await ingest.Content.ReadFromJsonAsync<JsonObject>())!["state_hash"]!.GetValue<string>();
        var item = LocalDispatchItem(stateHash, optionId);
        const string queueId = "locally-compiled-teacher-queue";
        var url = DispatchUrl(queueId, item.QueueItemId, stateHash);
        using var unknown = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        if (changeLedger)
        {
            using var mutation = await client.PostAsJsonAsync("/api/v1/strategy/commitments/materials/upsert", new
            {
                state_hash = stateHash, expected_ledger_revision = 0, reservation_id = "wood",
                source_decision_id = "other-goal", goal_id = "goal.other", node_id = "player:123",
                slot_index = 1, qualified_item_id = "(O)388", quantity = 20, purpose = "other goal"
            });
            mutation.EnsureSuccessStatusCode();
        }

        // Invoke the live controller method against the real HTTP handler, not a mocked guard.
        var program = typeof(LiveTrainingOptions).Assembly.GetType("Program", throwOnError: true)!;
        var method = program.GetMethod("ReadDispatchReadinessAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = await (Task<JsonObject?>)method.Invoke(null, new object[]
        {
            client, new LiveTrainingOptions { BackendUrl = client.BaseAddress!.ToString().TrimEnd('/') },
            JsonSerializer.SerializeToNode(item)!.AsObject(), stateHash, queueId
        })!;
        Assert.NotNull(result);
        Assert.Equal(!changeLedger, result!["ready"]!.GetValue<bool>());
        Assert.Equal(queueId, result["queue_id"]!.GetValue<string>());
        if (changeLedger)
            Assert.Contains("dispatch_strategy_ledger_revision_drifted", result["blocking_reasons"]!.AsArray()
                .Select(value => value!.GetValue<string>()));
        Assert.False(isolatedFactory.Services.GetRequiredService<StateStore>().ActionQueues.ContainsKey(queueId));
    }

    [Theory]
    [InlineData("state", "dispatch_museum_donation_state_hash_drifted")]
    [InlineData("actor", "dispatch_museum_donation_actor_identity_drifted")]
    [InlineData("binding", "dispatch_strategy_ledger_binding_incomplete")]
    [InlineData("revision", "dispatch_strategy_ledger_revision_drifted")]
    public async Task PostedDispatchChecksTheActualItemInsteadOfAStoredCopy(string changed, string reason)
    {
        using var isolatedFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStrategyCommitmentRepository>();
                services.AddSingleton<IStrategyCommitmentRepository>(new InMemoryStrategyCommitmentRepository());
            }));
        using var client = isolatedFactory.CreateClient();
        using var ingest = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
        ingest.EnsureSuccessStatusCode();
        var stateHash = (await ingest.Content.ReadFromJsonAsync<JsonObject>())!["state_hash"]!.GetValue<string>();
        const string queueId = "stored-but-not-dispatched-copy";
        var stored = LocalDispatchItem(stateHash, "executor.donate_museum_item");
        isolatedFactory.Services.GetRequiredService<StateStore>().ActionQueues[queueId] = new ActionQueueEnvelope
        {
            QueueId = queueId, StateHash = stateHash, Items = new[] { stored }
        };
        var item = LocalDispatchItem(stateHash, stored.OptionId);
        if (changed == "state") item.NormalizedCommand.StateHash = "stale-state";
        if (changed == "actor") item.NormalizedCommand.Parameters.Single(row => row.Name == "museum_donation_player_id").Value = "other-player";
        if (changed == "binding") item.NormalizedCommand.Parameters = Array.Empty<SmallModelActionParameter>();
        if (changed == "revision") item.NormalizedCommand.Parameters.Single(row => row.Name == "material_reservation_ledger_revision").Value = "8";
        var url = DispatchUrl(queueId, item.QueueItemId, stateHash);
        using var posted = await client.PostAsJsonAsync(url, item);
        posted.EnsureSuccessStatusCode();
        var result = await posted.Content.ReadFromJsonAsync<ActionQueueDispatchReadiness>();
        Assert.False(result!.Ready);
        Assert.Contains(reason, result.BlockingReasons);
        using var get = await client.GetAsync(url);
        get.EnsureSuccessStatusCode();
        Assert.True((await get.Content.ReadFromJsonAsync<ActionQueueDispatchReadiness>())!.Ready);
    }

    [Theory]
    [InlineData("wrong-item", "unavailable-state")]
    [InlineData("local-item", "unavailable-state")]
    public async Task PostedDispatchRejectsUnknownStateOrMismatchedItem(string routeItem, string stateHash)
    {
        using var client = factory.CreateClient();
        using var response = await client.PostAsJsonAsync(
            DispatchUrl("local-queue", routeItem, stateHash), LocalDispatchItem(stateHash, "executor.donate_museum_item"));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PostedDispatchRejectsAnUnnamedParameterBeforeEvaluation()
    {
        using var client = factory.CreateClient();
        var item = LocalDispatchItem("unused", "executor.donate_museum_item");
        item.NormalizedCommand.Parameters[0].Name = null!;
        using var response = await client.PostAsJsonAsync(DispatchUrl("local", item.QueueItemId, "unused"), item);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private static string DispatchUrl(string queueId, string itemId, string stateHash) =>
        $"/api/v1/action-queues/{queueId}/items/{itemId}/dispatch-readiness?stateHash={stateHash}";

    private static ActionQueueItem LocalDispatchItem(string stateHash, string optionId) => new()
    {
        QueueItemId = "local-item", OptionId = optionId, Status = "pending",
        NormalizedCommand = new NormalizedCommand
        {
            StateHash = stateHash,
            Parameters = new[]
            {
                Parameter("material_reservation_guard_status", "ready_no_active_material_reservations"),
                Parameter("material_reservation_ledger_id", "test-ledger"),
                Parameter("material_reservation_ledger_revision", "0"),
                Parameter("material_reservation_ids_json", "[]"),
                Parameter("commitment_ledger_id", "test-ledger"),
                Parameter("commitment_ledger_revision", "0"),
                Parameter("museum_donation_save_id", "Farm"),
                Parameter("museum_donation_player_id", "123"),
                Parameter("inventory_slot_index", "0"),
                Parameter("qualified_item_id", "(O)96"),
                Parameter("expected_stack_before", "2")
            }
        }
    };
}
