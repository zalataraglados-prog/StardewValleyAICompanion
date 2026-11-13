using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    // Declared synthetic native-contract observations, not a game execution. Never clean up evidence.
    private sealed class SiteTillNativeFixture
    {
        internal SiteTillFiles Files { get; }
        internal CropTeacherSiteTillCompilation Preparation { get; }
        internal SnapshotEnvelope After { get; private set; } = null!;
        internal MoneyRevenueCropNativeLifecycleTransitionRequest Transition { get; private set; } = null!;
        internal string ResultPath { get; private set; } = "";
        internal string JournalPath { get; private set; } = "";
        internal string FramePath => Path.Combine(Files.F.Source.RuntimePath, "till-native-frame.json");
        internal string AfterPath => Path.Combine(Files.F.Source.RuntimePath, "till-native-final.json");
        internal string ReceiptPath => Path.Combine(Files.F.Source.RuntimePath, "till-native-aggregate.json");
        private readonly ActionOriginObservationReceipt observation;
        private readonly bool productTransport;

        internal SiteTillNativeFixture(Action<Fixture>? prepareSource = null, bool productTransport = false)
        {
            this.productTransport = productTransport;
            (_, observation) = NativeInventoryGraphBoundaryTests.Fixture();
            observation.BeforeDebris = observation.AfterDebris = [];
            observation.Collections = []; observation.ClearanceToolCalls = [];
            Files = new SiteTillFiles(prepareSource: source => { PrepareSource(source); prepareSource?.Invoke(source); });
            Preparation = Files.Open();
        }

        private void PrepareSource(Fixture source)
        {
            var before = source.Before;
            before.GameVersion = "1.6.15";
            var stock = NativePickupInventoryBoundaryTests.ResourceFixture(woodBefore: 40);
            var rawPlayer = JsonNode.Parse(stock.Before.State["player"].GetRawText())!;
            var graph = JsonNode.Parse(stock.Before.State["farm"].GetRawText())!["material_inventory_graph"]!["value"]!;
            graph["default_shared_resource_policy"] = "deny_without_explicit_authorization";
            graph["physical_inventory_count"] = graph["inventory_nodes"]!.AsArray().Count;
            graph["access_point_count"] = graph["access_points"]!.AsArray().Count;
            foreach (var rows in new[] { rawPlayer["inventory"]!["value"]!, graph["inventory_nodes"]![0]!["slots"]! })
                foreach (var row in rows.AsArray().OfType<JsonObject>().Where(row => row["qualified_item_id"] is not null))
                {
                    var seed = row["slot_index"]!.GetValue<int>() == 0;
                    row["qualified_item_id"] = seed ? "(O)745" : "(T)Hoe";
                    row["item_id"] = seed ? "745" : "Hoe";
                    row["display_name"] = seed ? "Strawberry Seeds" : "Hoe";
                    row["runtime_type"] = seed ? "StardewValley.Object" : "StardewValley.Tools.Hoe";
                    row["context_tags"] = new JsonArray(seed ? "item_strawberry_seeds" : "item_hoe");
                }
            graph["quantity_rows"] = JsonSerializer.SerializeToNode(new[]
            {
                new { qualified_item_id = "(O)388", quality = 0, available_quantity = 0, ready_output_quantity = 0, in_process_quantity = 0, restricted_quantity = 7, source_slot_count = 1 },
                new { qualified_item_id = "(O)745", quality = 0, available_quantity = 40, ready_output_quantity = 0, in_process_quantity = 0, restricted_quantity = 0, source_slot_count = 1 },
                new { qualified_item_id = "(T)Hoe", quality = 0, available_quantity = 1, ready_output_quantity = 0, in_process_quantity = 0, restricted_quantity = 0, source_slot_count = 1 }
            });
            ChangeState(before, "player", player =>
            {
                player["inventory"] = rawPlayer["inventory"]!.DeepClone();
                player["inventory_capacity"] = rawPlayer["inventory_capacity"]!.DeepClone();
                player["facing_direction"] = NativeField(JsonValue.Create(0)!);
                player["max_energy"] = NativeField(JsonValue.Create(270)!);
                player["skills_detail"] = NativeField(JsonSerializer.SerializeToNode(new
                { skills = new[] { new { skill_id = "farming", skill_index = 0, effective_level = 0 } } })!);
            });
            ChangeState(before, "farm", farm =>
            {
                farm["material_inventory_graph"]!["value"] = graph.DeepClone();
                farm["terrain_features"] = NativeField(new JsonArray());
                farm["debris"] = NativeField(new JsonArray());
            });
            ChangeState(before, "current_location", location =>
            {
                location["statue_blessing"] = NativeField(new JsonObject { ["active_blessing_buffs"] = new JsonArray() });
                location["site_preparation_context"]!["value"]!["hoe_is_efficient"] = false;
            });
            ChangeState(before, "world_progress", world => world["game_state_query_unlock_state"] = NativeField(
                new JsonObject { ["current_player_id"] = before.PlayerId.Value, ["players"] = new JsonArray(new JsonObject
                { ["player_id"] = before.PlayerId.Value, ["is_current"] = true, ["stats"] = new JsonObject { ["dirtHoed"] = 7u } }) }));
            before.State["environment"] = JsonSerializer.SerializeToElement(new
            {
                training_run_id = new { status = "available", value = "run.seed-acquisition.fixture" },
                training_mode = new { status = "available", value = "1" },
                save_isolation_path = new { status = "available", value = "isolated-single-till-fixture" }
            });
            foreach (var endpoint in new[] { observation.InventoryPayloadBefore!, observation.InventoryPayloadAfter! })
            {
                var isAfter = ReferenceEquals(endpoint, observation.InventoryPayloadAfter);
                endpoint.ActorId = before.PlayerId.Value!; endpoint.GameTick = before.GameTick + (isAfter ? 2 : 0);
                var seed = endpoint.Slots![0]; seed.QualifiedItemId = "(O)745"; seed.Stack = 40;
                var seedXml = XElement.Parse(seed.SerializedStateXml!);
                seedXml.Element("itemId")!.Value = "745"; seedXml.Element("stack")!.Value = "40";
                seedXml.Add(new XElement("edibility", -300)); NativeXml(seed, seedXml);
                endpoint.GraphState!.Nodes![0].QualifiedItemId = seed.QualifiedItemId;
                endpoint.GraphState.Nodes[0].Stack = 40;
                var tool = endpoint.Slots[1]; tool.RuntimeType = "StardewValley.Tools.Hoe"; tool.QualifiedItemId = "(T)Hoe";
                endpoint.GraphState.Nodes[1].RuntimeType = tool.RuntimeType; endpoint.GraphState.Nodes[1].QualifiedItemId = tool.QualifiedItemId;
                endpoint.SelectionTransientState!.CurrentItem!.RuntimeType = tool.RuntimeType;
                var xml = XElement.Parse(tool.SerializedStateXml!); xml.Name = "Hoe"; xml.Element("itemId")!.Value = "Hoe";
                xml.Add(new XElement("initialParentTileIndex", 21), new XElement("InitialParentTileIndex", 21),
                    new XElement("currentParentTileIndex", isAfter ? 25 : 21), new XElement("swingTicker", isAfter ? 8 : 7),
                    new XElement("hasBeenInInventory", isAfter ? "true" : "false")); NativeXml(tool, xml);
                var metadata = new NativeTillingToolMetadataContext
                {
                    SchemaVersion = "native_tilling_tool_metadata_context.v1", GameTick = endpoint.GameTick,
                    ActorReferenceId = endpoint.SelectionTransientState.ActorReferenceId, HoeReferenceId = tool.ItemReferenceId,
                    HoeSlotIndex = 1, LastEquippedToolIsNull = !isAfter, LastEquippedToolReferenceId = isAfter ? tool.ItemReferenceId : null,
                    LastEquippedToolRuntimeType = isAfter ? tool.RuntimeType : null, FacingDirection = 0, CurrentToolSlotIndex = 1,
                    ToolPower = 0, UsingTool = false, PauseForSingleAnimation = false, CanReleaseTool = false,
                    InitialParentTileIndex = 21, CurrentParentTileIndex = isAfter ? 25 : 21, SwingTicker = isAfter ? 8 : 7,
                    HasBeenInInventory = isAfter
                };
                if (isAfter) observation.TillingToolMetadataAfter = metadata; else observation.TillingToolMetadataBefore = metadata;
            }
            observation.InventoryBefore = [new() { QualifiedItemId = "(O)745", Quality = 0, Quantity = 40 },
                new() { QualifiedItemId = "(T)Hoe", Quality = 0, Quantity = 1 }];
            observation.InventoryAfter = JsonSerializer.Deserialize<ActionOriginInventoryCount[]>(JsonSerializer.Serialize(observation.InventoryBefore))!;
        }

        internal async Task Build()
        {
            var before = Files.F.Source.Before;
            After = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
            After.GameTick += 2;
            ChangeState(After, "player", player => player["energy"]!["value"] = 268f);
            ChangeState(After, "farm", farm => farm["terrain_features"]!["value"]!.AsArray().Add(new JsonObject
            { ["tile_x"] = 0, ["tile_y"] = 0, ["type"] = "StardewValley.TerrainFeatures.HoeDirt" }));
            ChangeState(After, "world_progress", world => world["game_state_query_unlock_state"]!["value"]!["players"]![0]!["stats"]!["dirtHoed"] = 8u);
            After.StateHash = SnapshotHash.ComputeStateHash(After.State);
            Fixture.Save(FramePath, After); Fixture.Save(AfterPath, After);
            var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(Files.Queue), JsonOptions)!;
            var item = Assert.Single(queue.Items);
            var options = Files.F.Runtime.LiveOptions; options.SkipTraining = true;
            options.SaveIsolationPath = "isolated-single-till-fixture";
            var request = InvokeLive<TrainingExecutionRequest>("BuildExecutionRequest", options,
                JsonSerializer.SerializeToNode(item, JsonOptions)!.AsObject(), before.StateHash, queue.QueueId, item.OptionId);
            // Explicit declared synthetic observation opt-ins; the live till dispatcher remains controller-owned.
            request.ActionOriginObservationScopeId = "native-output.v1:single-till-fixture";
            request.ActionOriginSnapshotAnchor = request.ActionOriginToolInputs = request.ActionOriginInventoryPayload = true;
            request.ActionOriginInventoryTransients = request.ActionOriginInventoryGraph = request.ActionOriginDebrisState = true;
            request.ActionOriginTillingResources = true;
            var journal = await RuntimeExecutionRequestJournal.WriteAsync(options.SnapshotDir, 300, 1,
                JsonSerializer.Serialize(request, JsonOptions), productTransport ? "http://test/api/v1/product/execute" :
                    "http://test/api/v1/training/execute", Preparation.SourcePath);
            JournalPath = journal.JournalPath;
            observation.ScopeId = request.ActionOriginObservationScopeId; observation.RunId = request.RunId;
            observation.QueueId = request.QueueId; observation.QueueItemId = request.QueueItemId;
            observation.RequestNonce = request.RequestNonce; observation.BeforeStateHash = before.StateHash;
            observation.BeforeGameTick = before.GameTick; observation.AfterGameTick = After.GameTick;
            var facts = new[] { new SimulatedFactChange { Path = "player.energy", Before = "270", After = "268" } };
            var result = JsonSerializer.SerializeToNode(new TrainingExecutionResult
            {
                RunId = request.RunId, QueueId = queue.QueueId, QueueItemId = item.QueueItemId, OptionId = item.OptionId,
                BeforeStateHash = before.StateHash, Status = "applied", FeedbackAvailable = true, PrimitiveKind = "till_soil",
                PrimitiveVerificationStatus = "verified", ChangedFacts = facts, ToolPower = 0, ToolQualifiedItemId = "(T)Hoe",
                TargetLocation = "Farm", TargetTileX = 0, TargetTileY = 0, ProductAfterStateHash = After.StateHash,
                ProductAfterGameTick = After.GameTick, ActionOriginObservation = observation
            }, JsonOptions)!.AsObject();
            result["execution_request_journal_path"] = JournalPath; result["execution_request_nonce"] = request.RequestNonce;
            result["execution_request_sha256"] = journal.RawRequestSha256; result["mechanical_compiled_queue_path"] = Files.Queue;
            result["after_snapshot_path"] = FramePath; result["after_state_hash"] = After.StateHash;
            result["before_game_tick"] = before.GameTick; result["after_game_tick"] = After.GameTick; result["after_snapshot_fresh"] = true;
            if (productTransport)
            {
                result["source"] = "product_executor"; result["product_executor_schema_version"] = "stardewai.product_executor.v1";
                result["product_authorization_status"] = "authorized";
                result["product_receipt_id"] = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.RunId + "\n" + request.RequestNonce))).ToLowerInvariant();
                result["product_request_sha256"] = journal.ProductRequestSha256;
                result["product_request_before_state_hash"] = before.StateHash; result["product_before_state_hash"] = before.StateHash;
                result["product_before_game_tick"] = before.GameTick; result["product_request_state_hash_drift"] = false;
                result["product_dispatch_guard"] = "native_action_preconditions"; result["product_replan_required"] = true;
                result["product_idempotent_replay"] = false;
            }
            ResultPath = Path.Combine(Files.F.Source.RuntimePath, "till-native-result.json"); Fixture.Save(ResultPath, result);
            var step = JsonSerializer.SerializeToNode(new QueueExecutionStepReceipt
            {
                QueueItemIndex = 0, QueueItemCount = 1, OriginalPlannedItemCount = 1, QueueId = queue.QueueId,
                QueueItemId = item.QueueItemId, OptionId = item.OptionId, SourceStateHash = before.StateHash,
                CompiledCommandStateHash = queue.StateHash, AfterStateHash = After.StateHash, StateHashChanged = true,
                BeforeGameTick = before.GameTick, AfterGameTick = After.GameTick, AfterSnapshotPath = FramePath, AfterSnapshotFresh = true,
                Status = "applied", PrimitiveKind = "till_soil", PrimitiveVerificationStatus = "verified",
                PrimitiveVerificationReasons = ["declared_synthetic_single_till"], ChangedFacts = JsonSerializer.SerializeToElement(facts, JsonOptions),
                EffectiveQueueItem = JsonSerializer.SerializeToElement(item, JsonOptions), SelectedQueueCandidateCompleted = true
            }, JsonOptions)!.AsObject();
            step["execution_path"] = ResultPath; step["execution_request_nonce"] = request.RequestNonce;
            var aggregate = JsonSerializer.SerializeToNode(new QueueExecutionReceiptEnvelope
            {
                RunId = request.RunId, QueueId = queue.QueueId, SourceStateHash = before.StateHash, AfterStateHash = After.StateHash,
                BeforeGameTick = before.GameTick, AfterGameTick = After.GameTick, AfterSnapshotFresh = true,
                QueueExecutionMode = "sequential_queue_items", Status = "applied", Success = true,
                PlannedItemCount = 1, ExecutedItemCount = 1, MaxQueueItemAttempts = 1,
                SelectedCandidateId = Preparation.CandidateId, SelectedCandidateCompleted = true
            }, JsonOptions)!.AsObject();
            aggregate["step_results"] = new JsonArray(step); aggregate["after_snapshot_path"] = AfterPath;
            Fixture.Save(ReceiptPath, aggregate);
            Transition = new("crop_actions", Preparation.TotalDay, Files.Queue, Preparation.SourcePath,
                ReceiptPath, AfterPath, request.RunId, PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, Preparation.CandidateId);
        }

        internal string[] NativePaths => [ReceiptPath, AfterPath, FramePath, ResultPath, JournalPath, JournalPath + ".gz"];
        internal static string FileHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        private static JsonObject NativeField(JsonNode value) => new() { ["status"] = "available", ["value"] = value };
        private static void NativeXml(ActionOriginInventoryPayloadSlot slot, XElement xml)
        {
            slot.SerializedStateXml = xml.ToString(SaveOptions.DisableFormatting);
            slot.SerializedStateSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(slot.SerializedStateXml))).ToLowerInvariant();
        }
    }
}
