using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private sealed class OwnedWateringResourcesFixture : IDisposable
    {
        private readonly (NativePickupInventoryBoundaryTests.PickupFixture Stock, ActionOriginObservationReceipt Receipt,
            TrainingExecutionResult Result) resource = NativePickupInventoryBoundaryTests.WateringOwnerStock();
        internal OwnedWateringFixture Owned { get; }
        internal string JournalPath { get; private set; } = "";
        internal string ResultPath => Path.Combine(Owned.Shared.Runtime.LiveOptions.SnapshotDir, "water-native-result.json");

        internal OwnedWateringResourcesFixture()
        {
            Owned = new(prepareSource: source =>
            {
                CopyStock(source.Before, resource.Stock.Before);
                foreach (var claim in source.OtherRequest.MaterialClaims.Concat(source.OwnRequest.MaterialClaims)) claim.SlotIndex = 2;
                source.Before.State["environment"] = JsonSerializer.SerializeToElement(new
                {
                    training_run_id = new { status = "available", value = "run.seed-acquisition.fixture" },
                    training_mode = new { status = "available", value = "1" },
                    save_isolation_path = new { status = "available", value = "isolated-water-owner-fixture" }
                });
            });
            var options = Owned.Shared.Runtime.LiveOptions; options.UseManagedWateringResources = true;
            options.SkipTraining = true; options.SaveIsolationPath = "isolated-water-owner-fixture";
        }

        private static void CopyStock(SnapshotEnvelope target, SnapshotEnvelope source)
        {
            foreach (var (domain, name) in new[] { ("player", "inventory"), ("player", "inventory_capacity"),
                         ("farm", "material_inventory_graph"), ("world_progress", "marriage_house") })
                ChangeState(target, domain, node =>
                {
                    node[name] ??= new JsonObject { ["status"] = "available" };
                    node[name]!["value"] = JsonNode.Parse(source.State[domain].GetRawText())![name]!["value"]!.DeepClone();
                });
        }

        internal async Task<JsonObject> Emit()
        {
            var before = Owned.Basis.Before;
            await Owned.Basis.Emit(applied: 1, retainedQueuePath: Owned.QueuePath, change: after =>
            { after.GameTick += resource.Stock.After.GameTick - resource.Stock.Before.GameTick - 1; CopyStock(after, resource.Stock.After); });
            var after = Owned.Basis.After;
            var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(Owned.QueuePath), JsonOptions)!;
            var selected = (LiveTrainingOptions)typeof(LiveTrainingOptions).GetMethod("WithSelectedWateringResources",
                BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(Owned.Shared.Runtime.LiveOptions, null)!;
            var request = InvokeLive<TrainingExecutionRequest>("BuildExecutionRequest", selected,
                JsonSerializer.SerializeToNode(queue.Items[0], JsonOptions)!.AsObject(), before.StateHash, queue.QueueId, "executor.water_crop");
            JournalPath = (await RuntimeExecutionRequestJournal.WriteAsync(selected.SnapshotDir, 264, 1,
                JsonSerializer.Serialize(request, JsonOptions), "http://test/api/v1/training/execute", Owned.Shared.Source.SourcePath)).JournalPath;
            var journal = ExecutionRequestJournalReader.Read(JournalPath);
            var observation = resource.Receipt; var shift = before.GameTick - resource.Stock.Before.GameTick;
            observation.ScopeId = request.ActionOriginObservationScopeId!; observation.RunId = request.RunId;
            observation.QueueId = request.QueueId; observation.QueueItemId = request.QueueItemId;
            observation.RequestNonce = request.RequestNonce; observation.BeforeStateHash = before.StateHash;
            observation.BeforeGameTick += shift; observation.AfterGameTick += shift;
            foreach (var payload in new[] { observation.InventoryPayloadBefore!, observation.InventoryPayloadAfter! }) payload.GameTick += shift;
            foreach (var frame in new[] { observation.WateringToolMetadataBefore!, observation.WateringToolMetadataAfter! }) frame.GameTick += shift;
            foreach (var call in observation.WateringToolMetadataCalls!) call.GameTick += shift;
            foreach (var collection in observation.Collections) { collection.BeforeGameTick += shift; collection.AfterGameTick += shift; }
            var aggregate = JsonNode.Parse(File.ReadAllText(Owned.Basis.Transition.ExecutionReceiptPath))!.AsObject();
            var step = aggregate["step_results"]![0]!;
            var result = resource.Result; result.RunId = request.RunId; result.QueueId = request.QueueId; result.QueueItemId = request.QueueItemId;
            result.BeforeStateHash = before.StateHash; result.FeedbackAvailable = true; result.TargetTileX = 0; result.TargetTileY = 0;
            result.ProductAfterStateHash = after.StateHash; result.ProductAfterGameTick = after.GameTick;
            result.ChangedFacts = [new() { Path = "farm.crops[0].watered", Before = "false", After = "true" }];
            step["changed_facts"] = JsonSerializer.SerializeToNode(result.ChangedFacts, JsonOptions);
            var raw = JsonSerializer.SerializeToNode(result, JsonOptions)!.AsObject();
            raw["execution_request_journal_path"] = JournalPath; raw["execution_request_nonce"] = request.RequestNonce;
            raw["execution_request_sha256"] = journal.Metadata.GetProperty("raw_request_sha256").GetString();
            raw["effective_compiled_queue_path"] = Owned.QueuePath; raw["after_snapshot_path"] = step["after_snapshot_path"]!.DeepClone();
            raw["before_game_tick"] = before.GameTick; raw["after_game_tick"] = after.GameTick;
            raw["after_state_hash"] = after.StateHash; raw["after_snapshot_fresh"] = true;
            Fixture.Save(ResultPath, raw); step["execution_path"] = ResultPath; step["execution_request_nonce"] = request.RequestNonce;
            Fixture.Save(Owned.ReceiptPath, aggregate); Fixture.Save(Owned.AfterPath, after);
            return new() { ["execution_path"] = Owned.ReceiptPath, ["after_snapshot_path"] = Owned.AfterPath };
        }

        public void Dispose() => Owned.Dispose();
    }
}
