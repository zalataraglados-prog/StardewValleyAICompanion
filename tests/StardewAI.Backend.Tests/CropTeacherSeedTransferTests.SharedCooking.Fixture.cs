using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.Options;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    // Declared native wire/observations through the original reader and Backend, not a running game.
    private sealed class SharedCookingFixture : IDisposable
    {
        internal Fixture Files { get; }
        internal RuntimeFixture Runtime { get; }
        internal CropTeacherSharedSourcePreparation Scope { get; private set; } = null!;
        internal CropTeacherSharedConsumptionPreparationReference Preparation { get; private set; } = null!;
        internal CropTeacherSharedConsumptionSettlementReference Settlement { get; private set; } = null!;
        internal ReservationPortfolioSupportingTransitionSettlementRequest SettlementRequest { get; private set; } = null!;
        internal ReservationPortfolioSupportingTransitionSettlementResult SettlementReply { get; private set; } = null!;
        internal MoneyRevenueCropNativeLifecycleTransitionRequest Transition { get; private set; } = null!;
        internal SnapshotEnvelope After { get; private set; } = null!;
        internal StrategyCommitmentLedger BeforeLedger => Files.Ledger;
        internal string ReferencePath => Path.Combine(Root, "cooking-settlement-reference.json");
        internal string NativeResultPath => Path.Combine(Root, "cooking-native-result.json");
        internal string Root => Path.GetDirectoryName(Files.SourcePath)!;

        internal SharedCookingFixture()
        {
            Files = new Fixture(nativeEvidence: true, emptyChest: true, noPlayerSeed: true, beforeChange: ConfigureCooking);
            Runtime = new(Files);
        }

        internal async Task Build(bool product, bool settle = true)
        {
            Runtime.Options.UseProductExecutor = product; Runtime.Options.SkipTraining = true;
            Runtime.Options.SaveIsolationPath = "isolated-declared-shared-cooking";
            foreach (var (id, slot, item, goal) in new[] { ("cooking-wood", 2, "(O)388", "earn_money"), ("cooking-stone", 4, "(O)390", "skull_key") })
            {
                var current = Runtime.Repository.Get(Files.Before);
                var result = Runtime.Repository.CommitReservationPortfolio(Files.Before, new()
                {
                    StateHash = Files.Before.StateHash, ExpectedLedgerRevision = current.Revision, PortfolioId = id, SourceDecisionId = id, GoalId = goal,
                    MaterialClaims = [new() { StateHash = Files.Before.StateHash, ExpectedLedgerRevision = current.Revision,
                        ReservationId = id, SourceDecisionId = id, GoalId = goal, NodeId = "chest:FarmHouse:9,8", SlotIndex = slot,
                        QualifiedItemId = item, Quantity = 1, Purpose = "declared unrelated protected material" }]
                });
                Assert.True(result.Accepted, string.Join(",", result.Errors));
            }
            Files.AdoptLedger(Runtime.Repository.Get(Files.Before));
            var request = new ReservationPortfolioCommitRequest
            {
                StateHash = Files.Before.StateHash, ExpectedLedgerRevision = Files.Ledger.Revision,
                PortfolioId = "declared.shared.cooking.preparation", SourceDecisionId = "declared.shared.cooking.preparation", GoalId = "earn_money",
                InventoryConsumptionPreparation = new() { SchemaVersion = PreparedInventoryConsumptionContract.RequestSchema,
                    ConsumerOptionId = PreparedInventoryConsumptionContract.Consumer, RouteSourceDecisionId = "declared.cooking.selection",
                    RecipeName = "Fried Egg", CookingSourceId = "kitchen:FarmHouse:5,5" }
            };
            var diagnostic = new JsonObject();
            var reply = await InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", Runtime.Client, Runtime.Options, diagnostic, request);
            Assert.True(reply["accepted"]!.GetValue<bool>(), reply.ToJsonString());
            var prepared = reply["ledger"]!.Deserialize<StrategyCommitmentLedger>(JsonOptions)!;
            var replyPath = Path.Combine(Root, "cooking-preparation-reply.json"); Fixture.Save(replyPath, reply);
            var preparationJournalPath = diagnostic["crop_teacher_ledger_commit_request_journal_path"]!.GetValue<string>();
            var preparationJournal = JsonNode.Parse(File.ReadAllText(preparationJournalPath))!;
            Preparation = new(Path.Combine(Path.GetDirectoryName(preparationJournalPath)!, preparationJournal["request_file"]!.GetValue<string>()),
                replyPath, preparationJournalPath, Runtime.Options.BackendUrl);
            var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(Files.Before, [new OptionAvailabilityCandidate
            { OptionId = "crafting.cook_recipe", ExplicitConfirmationGranted = true, Parameters = [new() { Name = "recipe_name", Value = "Fried Egg" },
                new() { Name = "craft_count", Value = "1" }, new() { Name = "cooking_reason", Value = "declared_shared_meal" }] }], true);
            Assert.True(availability.Options.Length == 1 && availability.Options[0].EventCandidates.Length == 1,
                JsonSerializer.Serialize(availability, JsonOptions));
            var eventCandidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
            Assert.True(eventCandidate.Available, string.Join(",", eventCandidate.BlockReasons));
            var ranked = new EventCandidateRanker().Rank(new(), availability);
            var candidate = Assert.Single(ranked);
            candidate.Parameters = candidate.Parameters.Concat(PreparedCookingConsumptionBinding.ToParameters(prepared)).ToArray();
            var model = new DailyPlanCompiler().Compile(ranked, Files.Before.StateHash, "earn_money");
            Assert.Single(model.Steps);
            var queue = new ActionQueueCompiler().Compile(model, Files.Before, prepared);
            Assert.True(queue.Status == "pending", JsonSerializer.Serialize(queue, JsonOptions));
            var modelPath = Path.Combine(Root, "cooking-selected-plan.json"); var queuePath = Path.Combine(Root, "cooking-selected-queue.json");
            Fixture.Save(modelPath, model); Fixture.Save(queuePath, queue);
            var afterPath = Path.Combine(Root, "cooking-native-after.json"); var aggregatePath = Path.Combine(Root, "cooking-native-receipt.json");
            Transition = new("shared_source_return", Files.Before.State["time"].GetProperty("total_days").GetProperty("value").GetInt32(),
                queuePath, Files.SourcePath, aggregatePath, afterPath, Runtime.Options.RunId, Runtime.Options.PolicyTrajectoryExecutorVersion, candidate.CandidateId);
            Scope = CropTeacherSharedSourcePreparation.OpenPreparedCookingConsumption(Files.RequestPath, Files.SourcePath, Files.LedgerPath,
                [Id], "declared.shared.cooking.return", modelPath, Transition, new Dictionary<string, string>(), Preparation, ReferencePath, product);
            await WriteNative(queue, product);
            SettlementRequest = Scope.ReadConsumptionSettlementRequest();
            Assert.Equal(Hash(aggregatePath), SettlementRequest.SupportingTransitionReceiptSha256);
            Assert.Null(SettlementRequest.PreparedMaterialTransferSha256); Assert.Empty(SettlementRequest.MaterialRelocations);
            Runtime.Store.StoreSnapshot(After);
            if (!settle) return;
            var settlementDiagnostic = new JsonObject();
            var settled = await InvokeLive<Task<JsonObject>>("PostJournaledPortfolioSettlementAsync", Runtime.Client, Runtime.Options, settlementDiagnostic, SettlementRequest);
            SettlementReply = settled.Deserialize<ReservationPortfolioSupportingTransitionSettlementResult>(JsonOptions)!;
            Assert.True(SettlementReply.Accepted, settled.ToJsonString());
            _ = Scope.VerifyConsumptionSettlementReply(SettlementRequest, SettlementReply);
            var settlementReplyPath = Path.Combine(Root, "cooking-settlement-reply.json"); Fixture.Save(settlementReplyPath, settled);
            var settlementJournalPath = settlementDiagnostic["material_reservation_settlement_request_journal_path"]!.GetValue<string>();
            var settlementJournal = JsonNode.Parse(File.ReadAllText(settlementJournalPath))!;
            Settlement = new(Path.Combine(Path.GetDirectoryName(settlementJournalPath)!, settlementJournal["request_file"]!.GetValue<string>()),
                settlementReplyPath, settlementJournalPath, Runtime.Options.BackendUrl, Preparation);
            Fixture.Save(ReferencePath, Settlement);
        }

        private async Task WriteNative(ActionQueueEnvelope queue, bool product)
        {
            After = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(Files.Before))!; After.GameTick += 5;
            var farm = JsonNode.Parse(After.State["farm"].GetRawText())!.AsObject(); var graph = farm["material_inventory_graph"]!["value"]!;
            var player = graph["inventory_nodes"]!.AsArray().Single(row => row!["node_id"]!.GetValue<string>() == "player:123")!;
            var slots = player["slots"]!.AsArray(); slots.Remove(slots.Single(row => row!["qualified_item_id"]!.GetValue<string>() == "(O)917"));
            slots.Add(JsonSerializer.SerializeToNode(Slot(0, "(O)194", 1, 2), JsonOptions));
            var fridge = graph["inventory_nodes"]!.AsArray().Single(row => row!["node_id"]!.GetValue<string>() == "chest:FarmHouse:9,8")!["slots"]!.AsArray();
            fridge.RemoveAt(0); fridge[0]!["slot_index"] = 0; fridge[1]!["slot_index"] = 1;
            var updated = graph.Deserialize<MaterialInventoryGraph>(JsonOptions)!; UpdateGraphTotals(updated);
            farm["material_inventory_graph"]!["value"] = JsonSerializer.SerializeToNode(updated, JsonOptions);
            var actor = JsonNode.Parse(After.State["player"].GetRawText())!.AsObject(); Fixture.ApplyNativeEchoes(farm, actor, After.GameTick);
            actor["cooking"]!["value"]!["rows"]![0]!["recipes_cooked_before"] = 3;
            actor["cooking"]!["value"]!["rows"]![0]!["craft_candidate_status"] = "missing_materials";
            After.State["farm"] = JsonSerializer.SerializeToElement(farm, JsonOptions); After.State["player"] = JsonSerializer.SerializeToElement(actor, JsonOptions);
            CropTeacherNativeSupportFixture.CompleteEnvelopeMetadata(After, "declared-shared-cooking-fixture", Timestamp);
            After.StateHash = SnapshotHash.ComputeStateHash(After.State); Fixture.Save(Transition.AfterSnapshotPath, After);
            var item = Assert.Single(queue.Items);
            var request = InvokeLive<TrainingExecutionRequest>("BuildExecutionRequest", Runtime.Options,
                JsonSerializer.SerializeToNode(item, JsonOptions)!.AsObject(), Files.Before.StateHash, queue.QueueId, item.OptionId);
            var journal = await RuntimeExecutionRequestJournal.WriteAsync(Runtime.Options.SnapshotDir, 401, 1,
                JsonSerializer.Serialize(request, JsonOptions), "http://test" + (product ? "/api/v1/product/execute" : "/api/v1/training/execute"), Files.SourcePath);
            var facts = new[] { new SimulatedFactChange { Path = "player.recipesCooked.Fried Egg", Before = "2", After = "3" } };
            var result = JsonSerializer.SerializeToNode(new TrainingExecutionResult
            {
                RunId = request.RunId, QueueId = queue.QueueId, QueueItemId = item.QueueItemId, OptionId = item.OptionId,
                BeforeStateHash = Files.Before.StateHash, Status = "applied", FeedbackAvailable = true, PrimitiveKind = "cook_recipe",
                PrimitiveVerificationStatus = "verified", ChangedFacts = facts, ProductAfterStateHash = After.StateHash, ProductAfterGameTick = After.GameTick
            }, JsonOptions)!.AsObject();
            result["execution_request_journal_path"] = journal.JournalPath; result["execution_request_nonce"] = request.RequestNonce;
            result["execution_request_sha256"] = journal.RawRequestSha256; result["mechanical_compiled_queue_path"] = Transition.QueuePath;
            result["after_snapshot_path"] = Transition.AfterSnapshotPath; result["after_state_hash"] = After.StateHash;
            result["before_game_tick"] = Files.Before.GameTick; result["after_game_tick"] = After.GameTick; result["after_snapshot_fresh"] = true;
            if (product)
            {
                result["source"] = "product_executor"; result["product_executor_schema_version"] = "stardewai.product_executor.v1";
                result["product_authorization_status"] = "authorized"; result["product_receipt_id"] = HashText(request.RunId + "\n" + request.RequestNonce);
                result["product_request_sha256"] = journal.ProductRequestSha256; result["product_request_before_state_hash"] = Files.Before.StateHash;
                result["product_before_state_hash"] = Files.Before.StateHash; result["product_before_game_tick"] = Files.Before.GameTick;
                result["product_request_state_hash_drift"] = false; result["product_dispatch_guard"] = "native_action_preconditions";
                result["product_replan_required"] = false; result["product_idempotent_replay"] = false;
            }
            Fixture.Save(NativeResultPath, result);
            var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("SeedAcquisitionReceipt", queue, Files.Before, After,
                new[] { After }, new[] { Transition.AfterSnapshotPath }, Runtime.Options.RunId, Transition.SelectedCandidateId);
            var raw = JsonSerializer.SerializeToNode(receipt, JsonOptions)!;
            var step = raw["step_results"]![0]!; step["execution_path"] = NativeResultPath; step["execution_request_nonce"] = request.RequestNonce;
            step["changed_facts"] = JsonSerializer.SerializeToNode(facts, JsonOptions); step["selected_queue_candidate_completed"] = true;
            raw["after_snapshot_path"] = Transition.AfterSnapshotPath; Fixture.Save(Transition.ExecutionReceiptPath, raw);
        }

        private static void ConfigureCooking(SnapshotEnvelope before)
        {
            Assert.True(MaterialInventoryGraphReader.TryRead(before, out var graph));
            graph!.InventoryNodes[0].LocationId = "FarmHouse";
            graph.InventoryNodes[0].Slots = graph.InventoryNodes[0].Slots.Append(Slot(1, "(O)917", 1)).ToArray();
            graph.InventoryNodes = graph.InventoryNodes.Append(new MaterialInventoryNode
            { NodeId = "chest:FarmHouse:9,8", InventoryKind = "fridge", SupplyState = "available", ActorUseAuthorized = true, OwnerPlayerId = 123,
                LocationId = "FarmHouse", TileX = 9, TileY = 8, Capacity = 36, Slots = [Slot(0, "(O)176", 1), Slot(2, "(O)388", 2), Slot(4, "(O)390", 2)] }).ToArray();
            graph.AccessPoints = graph.AccessPoints.Append(new MaterialInventoryAccessPoint
            { AccessPointId = "declared-cooking-main-fridge", NodeId = "chest:FarmHouse:9,8", AccessKind = "built_in_fridge", LocationId = "FarmHouse",
                TileX = 9, TileY = 8, IsFridge = true, ActorUseAuthorized = true }).ToArray();
            foreach (var access in graph.AccessPoints) access.LocationIsCurrent = access.LocationId == "FarmHouse";
            UpdateGraphTotals(graph);
            var farm = JsonNode.Parse(before.State["farm"].GetRawText())!.AsObject(); farm["material_inventory_graph"]!["value"] = JsonSerializer.SerializeToNode(graph, JsonOptions);
            var actor = JsonNode.Parse(before.State["player"].GetRawText())!.AsObject();
            actor["location_id"]!["value"] = "FarmHouse"; actor["tile_x"]!["value"] = 4; actor["tile_y"]!["value"] = 5;
            actor["cooking"] = JsonNode.Parse("""
                {"status":"available","value":{"projection_status":"complete_learned_cooking_recipe_and_native_source_projection","rows":[{
                "recipe_name":"Fried Egg","known_recipe":true,"cooking_source_id":"kitchen:FarmHouse:5,5","cooking_source_kind":"kitchen",
                "location_id":"FarmHouse","interaction_tile_x":5,"interaction_tile_y":5,"material_container_ids":["kitchen-fridge:FarmHouse"],
                "material_container_topology_json":"[\"kitchen-fridge:FarmHouse\"]","output_item_id":"194","output_qualified_item_id":"(O)194",
                "output_count_per_craft":1,"output_quality":2,"output_order_data":"","recipes_cooked_before":2,
                "ingredient_rows":[{"requirement_id_or_category":"176","required_count":1,"satisfied":true,"native_consumption_plan":[{"source_id":"kitchen-fridge:FarmHouse","slot_index":0,"qualified_item_id":"(O)176","amount":1}]}],
                "seasoning_rows":[{"requirement_id_or_category":"917","required_count":1,"satisfied":true,"native_consumption_plan":[{"source_id":"player:123","slot_index":1,"qualified_item_id":"(O)917","amount":1}]}],
                "output_inventory_acceptance_after_material_consumption":true,"craft_candidate_status":"ready_for_native_cooking_page"
                }]}}
                """);
            var row = actor["cooking"]!["value"]!["rows"]![0]!;
            row["ingredient_rows_json"] = row["ingredient_rows"]!.ToJsonString(); row["seasoning_rows_json"] = row["seasoning_rows"]!.ToJsonString();
            Fixture.ApplyNativeEchoes(farm, actor, before.GameTick);
            before.State["farm"] = JsonSerializer.SerializeToElement(farm, JsonOptions); before.State["player"] = JsonSerializer.SerializeToElement(actor, JsonOptions);
            var locations = JsonNode.Parse(before.State["locations"].GetRawText())!.AsObject();
            locations["collision_grid"]!["value"] = JsonSerializer.SerializeToNode(new { location_id = "FarmHouse", width = 40, height = 40, notable_tiles = Array.Empty<object>() });
            locations["route_graph"] = JsonSerializer.SerializeToNode(new { status = "available", value = new { edges = Array.Empty<object>() } });
            before.State["locations"] = JsonSerializer.SerializeToElement(locations, JsonOptions);
            var currentLocation = JsonNode.Parse(before.State["current_location"].GetRawText())!.AsObject();
            currentLocation["map"]!["value"] = JsonSerializer.SerializeToNode(new { location_id = "FarmHouse", width = 40, height = 40 });
            before.State["current_location"] = JsonSerializer.SerializeToElement(currentLocation, JsonOptions);
            before.State["environment"] = JsonSerializer.SerializeToElement(new
            { training_run_id = new { status = "available", value = "run.owned.seed.transfer" }, training_mode = new { status = "available", value = "1" },
                save_isolation_path = new { status = "available", value = "isolated-declared-shared-cooking" } });
            CropTeacherNativeSupportFixture.CompleteEnvelopeMetadata(before, "declared-shared-cooking-fixture", Timestamp);
        }

        private static MaterialInventorySlot Slot(int slot, string id, int count, int quality = 0) => new()
        { SlotIndex = slot, QualifiedItemId = id, ItemId = id[(id.IndexOf(')') + 1)..], DisplayName = "fixture:" + id,
            Stack = count, Quality = quality, RuntimeType = "StardewValley.Object", MaximumStackSize = 999 };
        private static void UpdateGraphTotals(MaterialInventoryGraph graph)
        {
            graph.PhysicalInventoryCount = graph.InventoryNodes.Length;
            graph.AccessPointCount = graph.DeduplicatedAccessPointCount = graph.AccessPoints.Length;
            graph.QuantityRows = graph.InventoryNodes.SelectMany(node => node.Slots).GroupBy(slot => (slot.QualifiedItemId, slot.Quality))
                .Select(group => new MaterialQuantityRow { QualifiedItemId = group.Key.QualifiedItemId, Quality = group.Key.Quality,
                    AvailableQuantity = group.Sum(slot => slot.Stack), SourceSlotCount = group.Count() }).ToArray();
        }
        internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        private static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        public void Dispose() => Runtime.Dispose();
    }
}
