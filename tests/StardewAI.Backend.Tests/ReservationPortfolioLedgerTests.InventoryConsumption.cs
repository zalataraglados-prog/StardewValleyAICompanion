using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task CookingConsumptionUsesOriginalPersistenceAndReplyRecoveryAfterSourceEviction(bool losePreparationReply, bool loseSettlementReply)
    {
        // Declared inventory/recipe projection; these HTTP tests do not execute the game.
        var before = CookingConsumptionSnapshot();
        var root = NewPortfolioReplyRoot();
        StrategyCommitmentLedger prepared;
        ReservationPortfolioCommitRequest preparation;
        await using (var app = PortfolioReplyApplication(root))
        {
            using var client = app.CreateClient(); app.Services.GetRequiredService<StateStore>().StoreSnapshot(before);
            for (var index = 0; index < 2; index++)
            {
                var claim = new ReservationPortfolioCommitRequest
                {
                    StateHash = before.StateHash, ExpectedLedgerRevision = index, PortfolioId = "declared-cooking-root-" + index,
                    GoalId = index == 0 ? "earn_money" : "skull_key", SourceDecisionId = "declared-cooking-root-" + index,
                    MaterialClaims = [new() { StateHash = before.StateHash, ExpectedLedgerRevision = index,
                        ReservationId = index == 0 ? CropSaleStockReservationContract.ReservationId("declared-cooking-stock", 0) : "foreign-cooking-stone",
                        SourceDecisionId = index == 0 ? "declared-cooking-stock" : "foreign-stone-source", GoalId = index == 0 ? "earn_money" : "skull_key",
                        NodeId = "chest:FarmHouse:9,8", SlotIndex = index == 0 ? 2 : 4, QualifiedItemId = index == 0 ? "(O)388" : "(O)390", Quantity = 1,
                        Purpose = index == 0 ? CropSaleStockReservationContract.TransferablePurpose("declared-crop", "declared-wave",
                            MoneyRevenueCropRetentionAccount.InventoryIdentity(JsonSerializer.SerializeToElement(new { qualified_item_id = "(O)388", quality = 0 })), 0) : "declared foreign root" }]
                };
                await PostPortfolioAccepted(client, "commit", claim);
            }
            var baseLedger = app.Services.GetRequiredService<IStrategyCommitmentRepository>().Get(before);
            preparation = new()
            {
                StateHash = before.StateHash, ExpectedLedgerRevision = 2, PortfolioId = "declared-cooking-consumption", SourceDecisionId = "declared-cooking-consumption", GoalId = "cook",
                InventoryConsumptionPreparation = new() { SchemaVersion = PreparedInventoryConsumptionContract.RequestSchema,
                    ConsumerOptionId = PreparedInventoryConsumptionContract.Consumer, RouteSourceDecisionId = "declared-cooking-selection",
                    RecipeName = "Fried Egg", CookingSourceId = "kitchen:FarmHouse:5,5" }
            };
            using var transport = new LostPortfolioReplyHandler(app.Server.CreateHandler(), "commit");
            using var lost = new HttpClient(transport) { BaseAddress = new Uri("http://localhost") };
            if (losePreparationReply)
                await Assert.ThrowsAsync<HttpRequestException>(() => PostPortfolioAccepted(lost, "commit", preparation));
            else await PostPortfolioAccepted(client, "commit", preparation);
            using var lookup = await client.PostAsync(PortfolioEndpoint + "lookup-mutation", ReplyContent(new { operation = "commit", request = preparation }));
            Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
            var json = JsonNode.Parse(await lookup.Content.ReadAsStringAsync())!;
            Assert.Equal("found", json["status"]!.GetValue<string>());
            var reply = json["reply"]!.Deserialize<ReservationPortfolioCommitResult>()!;
            prepared = reply.Ledger!;
            var reasons = new List<string>();
            var verifier = typeof(CropTeacherSharedSourceReturn).Assembly.GetType("StardewAI.GoalConditionedBootstrap.ReservationPortfolioCommitEvidenceVerifier", true)!;
            var valid = (bool)verifier.GetMethod("ValidateMutation", BindingFlags.NonPublic | BindingFlags.Static)!
                .Invoke(null, [preparation, baseLedger, prepared, before, reply, reasons])!;
            Assert.True(valid, string.Join(",", reasons));
            var item = new ActionQueueItem { QueueItemId = "declared-prepared-cooking", OptionId = PreparedInventoryConsumptionContract.Consumer, Status = "pending",
                NormalizedCommand = new() { OptionId = PreparedInventoryConsumptionContract.Consumer, GoalId = prepared.PreparedInventoryConsumption!.GoalId,
                    StateHash = before.StateHash, Parameters = PreparedCookingConsumptionBinding.ToParameters(prepared).Concat(new SmallModelActionParameter[]
                    { new() { Name = "recipe_name", Value = "Fried Egg" }, new() { Name = "cooking_source_id", Value = "kitchen:FarmHouse:5,5" } }).ToArray() } };
            var dispatch = await ReadActualLiveTransferDispatch(client, JsonSerializer.SerializeToNode(item)!.AsObject(), before.StateHash);
            Assert.True(dispatch?["ready"]?.GetValue<bool>(), dispatch?.ToJsonString());
            item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters.Where(row => row.Name != PreparedInventoryConsumptionContract.BindingParameter).ToArray();
            var missing = await ReadActualLiveTransferDispatch(client, JsonSerializer.SerializeToNode(item)!.AsObject(), before.StateHash);
            Assert.False(missing?["ready"]?.GetValue<bool>());
            item.NormalizedCommand.GoalId = null;
            Assert.Null(await ReadActualLiveTransferDispatch(client, JsonSerializer.SerializeToNode(item)!.AsObject(), before.StateHash));
            Assert.Equal(losePreparationReply ? 1 : 0, transport.MutationCalls);
        }
        var file = Assert.Single(Directory.GetFiles(root));
        var originalBytes = File.ReadAllBytes(file);
        Assert.NotNull(JsonNode.Parse(originalBytes)!["prepared_inventory_consumption"]);
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before))!; after.GameTick++;
        Assert.True(MaterialInventoryGraphReader.TryRead(after, out var graph));
        graph!.InventoryNodes[0].Slots = [CookingPhysicalSlot(0, "(O)194", 1, 2)];
        graph.InventoryNodes[1].Slots = graph.InventoryNodes[1].Slots.Skip(1).ToArray();
        graph.InventoryNodes[1].Slots[0].SlotIndex = 0; graph.InventoryNodes[1].Slots[1].SlotIndex = 1;
        after.State["farm"] = JsonSerializer.SerializeToElement(new { material_inventory_graph = new { status = "available", value = graph } });
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        var binding = prepared.PreparedInventoryConsumption!;
        var request = new ReservationPortfolioSupportingTransitionSettlementRequest
        {
            StateHash = after.StateHash, ExpectedLedgerRevision = prepared.Revision, PortfolioId = binding.PortfolioId, GoalId = binding.GoalId,
            RouteSourceDecisionId = binding.RouteSourceDecisionId, ConsumerOptionId = PreparedInventoryConsumptionContract.Consumer,
            PreparedInventoryConsumptionSha256 = binding.BindingSha256, SupportingTransitionReceiptSha256 = new string('a', 64), Reason = "declared native consumption"
        };
        await using var restarted = PortfolioReplyApplication(root);
        using var http = restarted.CreateClient(); var store = restarted.Services.GetRequiredService<StateStore>();
        store.StoreSnapshot(before); store.StoreSnapshot(new() { StateHash = "declared-eviction-frame", GameTick = 1 }); store.StoreSnapshot(after);
        Assert.Equal(2, store.Snapshots.Count); Assert.False(store.Snapshots.ContainsKey(before.StateHash));
        using var lostSettlement = new LostPortfolioReplyHandler(restarted.Server.CreateHandler(), "settle-supporting-transition");
        using var post = new HttpClient(lostSettlement) { BaseAddress = new Uri("http://localhost") };
        if (loseSettlementReply)
            await Assert.ThrowsAsync<HttpRequestException>(() => PostPortfolioAccepted(post, "settle-supporting-transition", request));
        else await PostPortfolioAccepted(http, "settle-supporting-transition", request);
        using var recovered = await http.PostAsync(PortfolioEndpoint + "lookup-mutation", ReplyContent(new { operation = "settle-supporting-transition", request }));
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        var recoveredJson = JsonNode.Parse(await recovered.Content.ReadAsStringAsync())!;
        Assert.Equal("found", recoveredJson["status"]!.GetValue<string>());
        Assert.False(recoveredJson["ledger_mutation_performed"]!.GetValue<bool>());
        Assert.False(recoveredJson["native_outcome_verified"]!.GetValue<bool>());
        var result = recoveredJson["reply"]!.Deserialize<ReservationPortfolioSupportingTransitionSettlementResult>()!;
        Assert.True(result.Accepted); Assert.Equal(4, result.CommittedLedgerRevision); Assert.Null(result.Ledger!.PreparedInventoryConsumption);
        Assert.Equal(2, result.ReboundActiveReservationIds.Length); Assert.Null(result.MaterialTransferReservations); Assert.Equal(0, result.ConsumedQuantity);
        var acceptedBytes = File.ReadAllBytes(file);
        Assert.False(JsonNode.Parse(acceptedBytes)!.AsObject().ContainsKey("prepared_inventory_consumption"));
        var disk = new FileStrategyCommitmentRepository(root).Get(after);
        Assert.Equal(JsonSerializer.Serialize(result.Ledger), JsonSerializer.Serialize(disk));
        using var duplicate = await http.PostAsJsonAsync(PortfolioEndpoint + "settle-supporting-transition", request);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode); Assert.Equal(acceptedBytes, File.ReadAllBytes(file));
        Assert.Equal(loseSettlementReply ? 1 : 0, lostSettlement.MutationCalls);
    }

    private static MaterialInventorySlot CookingPhysicalSlot(int slot, string id, int stack, int quality = 0) => new()
    { SlotIndex = slot, QualifiedItemId = id, Stack = stack, Quality = quality, RuntimeType = "StardewValley.Object", MaximumStackSize = 999 };

    private static SnapshotEnvelope CookingConsumptionSnapshot()
    {
        var snapshot = Snapshot();
        var graph = new MaterialInventoryGraph
        {
            PlayerId = 123, InventoryNodes = [new() { NodeId = "player:123", InventoryKind = "player_inventory", LocationId = "FarmHouse", OwnerPlayerId = 123,
                ActorUseAuthorized = true, SupplyState = "available", Slots = [CookingPhysicalSlot(1, "(O)917", 1)] },
                new() { NodeId = "chest:FarmHouse:9,8", InventoryKind = "fridge", LocationId = "FarmHouse", TileX = 9, TileY = 8, OwnerPlayerId = 123,
                    ActorUseAuthorized = true, SupplyState = "available", Slots = [CookingPhysicalSlot(0, "(O)176", 1), CookingPhysicalSlot(2, "(O)388", 2), CookingPhysicalSlot(4, "(O)390", 2)] }],
            AccessPoints = [new() { AccessPointId = "declared-kitchen-fridge", NodeId = "chest:FarmHouse:9,8", AccessKind = "built_in_fridge",
                LocationId = "FarmHouse", TileX = 9, TileY = 8, IsFridge = true, ActorUseAuthorized = true }]
        };
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(new { material_inventory_graph = new { status = "available", value = graph } });
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!.AsObject();
        player["cooking"] = JsonNode.Parse("""
            {"status":"available","value":{"projection_status":"complete_learned_cooking_recipe_and_native_source_projection","rows":[{
              "recipe_name":"Fried Egg","cooking_source_id":"kitchen:FarmHouse:5,5","location_id":"FarmHouse",
              "material_container_ids":["kitchen-fridge:FarmHouse"],"output_qualified_item_id":"(O)194","output_count_per_craft":1,"output_quality":2,
              "output_inventory_acceptance_after_material_consumption":true,"craft_candidate_status":"ready_for_native_cooking_page",
              "ingredient_rows":[{"satisfied":true,"required_count":1,"native_consumption_plan":[{"source_id":"kitchen-fridge:FarmHouse","slot_index":0,"qualified_item_id":"(O)176","amount":1}]}],
              "seasoning_rows":[{"satisfied":true,"required_count":1,"native_consumption_plan":[{"source_id":"player:123","slot_index":1,"qualified_item_id":"(O)917","amount":1}]}]
            }]}}
            """);
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player); snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }
}
