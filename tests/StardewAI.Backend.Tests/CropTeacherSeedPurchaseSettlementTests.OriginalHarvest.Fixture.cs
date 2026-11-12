using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private sealed class HarvestFixture : IDisposable
    {
        internal OwnedRouteFixture Shared { get; }
        internal SnapshotEnvelope Before { get; }
        internal SnapshotEnvelope After { get; private set; } = null!;
        internal StrategyCommitmentLedger Ledger { get; }
        internal MoneyRevenueCropNativeProductionActionBatch Batch { get; }
        internal int Day { get; }
        internal string Output { get; }
        internal string SourcePath => Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "harvest-before.json");
        internal string LedgerPath => Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "harvest-ledger.json");
        internal string ModelPath => Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "harvest-model.json");
        internal string QueuePath => Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "harvest-queue.json");
        internal string Candidate => $"crop:{Day}:{Id}:{Batch.BatchIndex}";
        internal ActionQueueEnvelope Queue { get; private set; } = null!;
        internal MoneyRevenueCropNativeLifecycleTransitionRequest Transition { get; private set; } = null!;

        internal HarvestFixture()
        {
            Shared = new(prepareSource: PrepareSource);
            var crop = Shared.Source.Ledger.CropPlantingCommitments.Single();
            Output = crop.HarvestItemQualifiedId; Day = crop.FirstHarvestTotalDay;
            Batch = Shared.Inputs.Plan.Days.Single(row => row.TotalDay == Day).ActionBatches.Single(row => row.OptionId == "executor.harvest_crop");
            Before = Copy(Shared.Source.Before);
            ChangeState(Before, "time", node => { node["total_days"]!["value"] = Day; node["year"]!["value"] = 2;
                node["season"]!["value"] = "spring"; node["day"]!["value"] = 1 + Day - crop.PlantingTotalDay; });
            Put(Before, "farm", "crops", JsonSerializer.SerializeToNode(Batch.Tiles.Select(tile => new
            {
                location_id = "Farm", tile_x = tile.TileX, tile_y = tile.TileY, harvest_source_seed_id = crop.SeedId,
                harvest_item_qualified_id = Output, phase_days = new[] { crop.BaseGrowDays, 99999 }, current_phase = 1, phase_count = 2,
                day_of_current_phase = 0, dead = false, fully_grown = false, ready_for_harvest = true, days_until_next_harvest_if_watered = 0,
                regrow_days = crop.RegrowDays, watered = true, harvest_experience_skill_id = "farming", harvest_experience_skill_index = 0,
                harvest_experience_on_success_min = 23, harvest_experience_on_success_max = 23,
                harvest_experience_projection_status = "exact_from_decompiled_native_harvest", harvest_experience_condition = "declared_fixture_projection"
            }))!);
            Put(Before, "current_location", "crops", Field(Before, "farm", "crops"));
            Put(Before, "menus", "active_menu", new JsonObject { ["is_open"] = false, ["type"] = "" });
            Before.StateHash = SnapshotHash.ComputeStateHash(Before.State);
            Ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(JsonSerializer.Serialize(Shared.Source.Ledger, JsonOptions), JsonOptions)!;
            Ledger.SourceStateHash = Before.StateHash;
            Fixture.Save(SourcePath, Before); Fixture.Save(LedgerPath, Ledger);
            Compile();
        }

        internal static void PrepareSource(Fixture source)
        {
            var skills = NativeCropHarvestSkillBoundaryTests.Fixture(0, mirrors: false).Before;
            foreach (var field in new[] { "skills_detail", "level" }) Put(source.Before, "player", field, Field(skills, "player", field));
            var output = source.Ledger.CropPlantingCommitments.Single().HarvestItemQualifiedId;
            var raw = Field(source.Before, "player", "inventory").AsArray();
            var graph = Field(source.Before, "farm", "material_inventory_graph");
            var nodes = graph["inventory_nodes"]!.AsArray();
            var player = nodes.OfType<JsonObject>().Single(row => row["node_id"]!.GetValue<string>() == "player:123");
            foreach (var item in player["slots"]!.AsArray().OfType<JsonObject>())
            {
                var row = raw.OfType<JsonObject>().Single(value => value["slot_index"]!.GetValue<int>() == item["slot_index"]!.GetValue<int>());
                foreach (var name in new[] { "item_id", "qualified_item_id", "display_name", "runtime_type", "context_tags", "stack", "maximum_stack_size", "quality", "sale_price" })
                {
                    row[name] ??= item[name]?.DeepClone() ?? (name is "context_tags" ? new JsonArray() : name is "sale_price" ? JsonValue.Create(0) : JsonValue.Create("fixture"));
                    item[name] = row[name]!.DeepClone();
                }
            }
            var occupied = raw.OfType<JsonObject>().Where(row => row["is_empty"]?.GetValue<bool>() == false)
                .Select(row => row["slot_index"]!.GetValue<int>()).ToHashSet();
            var selected = Enumerable.Range(0, 12).First(index => !occupied.Contains(index));
            var template = Field(NativePickupInventoryBoundaryTests.ResourceFixture().Before, "player", "inventory").AsArray()[0]!.DeepClone();
            template["slot_index"] = selected; template["item_id"] = output[3..]; template["qualified_item_id"] = output;
            template["display_name"] = "Declared harvest fixture"; template["stack"] = 1;
            template["context_tags"] = new JsonArray(); template["category"] = -79;
            foreach (var empty in raw.OfType<JsonObject>().Where(row => row["slot_index"]!.GetValue<int>() == selected).ToArray()) raw.Remove(empty);
            raw.Add(template);
            player["slots"]!.AsArray().Add(template.Deserialize<MaterialInventorySlot>(JsonOptions) is { } material
                ? JsonSerializer.SerializeToNode(material, JsonOptions) : throw new InvalidDataException());
            graph["quantity_rows"] = new JsonArray(nodes.OfType<JsonObject>().SelectMany(node => node["slots"]!.AsArray().OfType<JsonObject>()
                .Select(item => (Node: node, Item: item))).Where(row => row.Item["stack"]!.GetValue<int>() > 0)
                .GroupBy(row => (Id: row.Item["qualified_item_id"]!.GetValue<string>(), Quality: row.Item["quality"]!.GetValue<int>()))
                .OrderBy(group => group.Key.Id, StringComparer.Ordinal).ThenBy(group => group.Key.Quality).Select(group => (JsonNode)new JsonObject
                {
                    ["qualified_item_id"] = group.Key.Id, ["quality"] = group.Key.Quality,
                    ["available_quantity"] = group.Where(row => row.Node["actor_use_authorized"]!.GetValue<bool>()).Sum(row => row.Item["stack"]!.GetValue<int>()),
                    ["restricted_quantity"] = group.Where(row => !row.Node["actor_use_authorized"]!.GetValue<bool>()).Sum(row => row.Item["stack"]!.GetValue<int>()),
                    ["ready_output_quantity"] = 0, ["in_process_quantity"] = 0, ["source_slot_count"] = group.Count()
                }).ToArray());
            Put(source.Before, "player", "inventory", raw);
            Put(source.Before, "farm", "material_inventory_graph", graph);
            Put(source.Before, "player", "inventory_capacity", JsonSerializer.SerializeToNode(new { max_items = 12,
                occupied_item_stacks = occupied.Count + 1, empty_slots = 11 - occupied.Count, has_empty_slot = occupied.Count < 11 })!);
        }

        internal void Compile(bool reversed = false, int? count = null)
        {
            var tiles = (reversed ? Batch.Tiles.Reverse() : Batch.Tiles).Take(count ?? Batch.Tiles.Length);
            var model = JsonSerializer.SerializeToNode(new { schema_version = "small_model_action.v1", model_output_id = "model-output:" + Candidate,
                source_model = "authoritative_crop_native_run_plan.v1", state_hash = Before.StateHash, goal_id = "daily.closed_loop",
                execution_mode = Shared.Runtime.LiveOptions.TargetExecutionMode, actor = Shared.Runtime.LiveOptions.TargetActor, actions = tiles.Select(tile => new
                {
                    action_id = $"{Candidate}:{tile.TileX}:{tile.TileY}", option_id = "executor.harvest_crop", rationale = "authoritative_crop_native_run_plan",
                    parameters = new[] { new { name = "target_tile_x", value = tile.TileX.ToString() }, new { name = "target_tile_y", value = tile.TileY.ToString() },
                        new { name = "target_location", value = "Farm" }, new { name = "action_commitment_id", value = Id }, new { name = "precondition", value = "candidate_id:" + Candidate } }
                }).ToArray() })!;
            Queue = new ActionQueueCompiler().Compile(model.Deserialize<SmallModelActionEnvelope>(JsonOptions)!, Before, Ledger);
            Assert.True(Queue.Status == "pending", string.Join(",", Queue.CompilerDiagnostics.Concat(
                Queue.Items.SelectMany(item => item.BlockingReasons.Concat(item.MissingStateFactors)))));
            Fixture.Save(ModelPath, model); Fixture.Save(QueuePath, Queue);
        }

        internal CropTeacherHarvest.Preparation Open(int? day = null, int? batch = null) => CropTeacherHarvest.Preparation.Open(
            Shared.Inputs.PlanPath, SourcePath, LedgerPath, new[] { Id }, Id, "harvest.step.1", day ?? Day, batch ?? Batch.BatchIndex, ModelPath, QueuePath);

        internal void Emit(int applied = 2, bool failedSuffix = false)
        {
            var snapshots = new List<SnapshotEnvelope>(); var paths = new List<string>(); var current = Before;
            var count = applied + (failedSuffix ? 1 : 0);
            for (var index = 0; index < count; index++)
            {
                var next = Copy(current); next.GameTick++;
                if (index < applied)
                {
                    ApplyHarvestDelta(next, Batch.Tiles[index], Output);
                }
                next.StateHash = SnapshotHash.ComputeStateHash(next.State);
                var path = Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, $"harvest-step-{index}.json");
                snapshots.Add(next); paths.Add(path); Fixture.Save(path, next); current = next;
            }
            After = current;
            // The original fixture factory enumerates the full queue; unused rows are discarded, not emitted as evidence.
            var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("SeedAcquisitionReceipt", Queue, Before, After,
                snapshots.Concat(Enumerable.Repeat(After, Queue.Items.Length - count)).ToArray(),
                paths.Concat(Enumerable.Repeat(paths[^1], Queue.Items.Length - count)).ToArray(), Shared.Files.Options.RunId, Candidate);
            receipt.StepResults = receipt.StepResults.Take(count).ToArray(); receipt.ExecutedItemCount = count;
            receipt.FinalPendingItemCount = Queue.Items.Length - count; receipt.Success = receipt.SelectedCandidateCompleted = applied == Queue.Items.Length;
            receipt.Status = failedSuffix ? "blocked" : "applied";
            foreach (var step in receipt.StepResults) { step.SelectedQueueCandidateCompleted = receipt.Success && step.QueueItemIndex == count - 1;
                step.PrimitiveVerificationReasons = new[] { "declared_fixture_harvest_observed" }; }
            if (failedSuffix) { var step = receipt.StepResults[^1]; step.Status = "blocked"; step.PrimitiveVerificationStatus = "blocked";
                step.BlockReasons = receipt.BlockReasons = new[] { "declared_fixture_harvest_blocked" }; }
            var receiptPath = Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "harvest-receipt.json"); Fixture.Save(receiptPath, receipt);
            Transition = new("crop_actions", Day, QueuePath, SourcePath, receiptPath, paths[^1], Shared.Files.Options.RunId, Shared.Files.Options.ExecutorVersion, Candidate);
        }

        internal static void ApplyHarvestDelta(SnapshotEnvelope next, MoneyRevenueCropPlantingTile tile, string output)
        {
            var rows = Field(next, "farm", "crops").AsArray();
            var target = rows.Single(row => row!["tile_x"]!.GetValue<int>() == tile.TileX && row["tile_y"]!.GetValue<int>() == tile.TileY)!;
            if (target["regrow_days"] is null) rows.Remove(target);
            else { target["fully_grown"] = true; target["ready_for_harvest"] = false; target["day_of_current_phase"] = target["regrow_days"]!.DeepClone();
                target["days_until_next_harvest_if_watered"] = target["regrow_days"]!.DeepClone(); }
            ChangeState(next, "farm", node => node["crops"]!["value"] = rows);
            ChangeState(next, "current_location", node => node["crops"]!["value"] = Field(next, "farm", "crops"));
            var inventory = Field(next, "player", "inventory");
            var item = inventory.AsArray().OfType<JsonObject>().Single(row => row["qualified_item_id"]?.GetValue<string>() == output);
            var index = item["slot_index"]!.GetValue<int>();
            item["stack"] = item["stack"]!.GetValue<int>() + 1; Put(next, "player", "inventory", inventory);
            var graph = Field(next, "farm", "material_inventory_graph");
            var graphItem = graph["inventory_nodes"]![0]!["slots"]!.AsArray().OfType<JsonObject>().Single(row => row["slot_index"]!.GetValue<int>() == index);
            graphItem["stack"] = item["stack"]!.DeepClone();
            var quantity = graph["quantity_rows"]!.AsArray().OfType<JsonObject>().Single(row => row["qualified_item_id"]!.GetValue<string>() == output);
            quantity["available_quantity"] = item["stack"]!.DeepClone(); Put(next, "farm", "material_inventory_graph", graph);
            var experience = Field(next, "player", "skills_detail")["skills"]!.AsArray().Single(row => row!["skill_index"]!.GetValue<int>() == 0)!["experience"]!.GetValue<int>();
            var skill = NativeCropHarvestSkillBoundaryTests.Fixture(experience, mirrors: false).After;
            foreach (var field in new[] { "skills_detail", "level" }) Put(next, "player", field, Field(skill, "player", field));
        }

        private static SnapshotEnvelope Copy(SnapshotEnvelope snapshot) => JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(snapshot, JsonOptions), JsonOptions)!;
        private static JsonNode Field(SnapshotEnvelope snapshot, string domain, string field) => JsonNode.Parse(snapshot.State[domain].GetProperty(field).GetProperty("value").GetRawText())!;
        private static void Put(SnapshotEnvelope snapshot, string domain, string field, JsonNode value)
        {
            var fields = JsonNode.Parse(snapshot.State[domain].GetRawText())!.AsObject();
            fields[field] = new JsonObject { ["status"] = "available", ["value"] = value, ["confidence"] = 1d,
                ["read_at_tick"] = snapshot.GameTick, ["adapter"] = "test", ["source"] = new JsonObject { ["kind"] = "test", ["path"] = "declared-harvest-fixture" } };
            snapshot.State[domain] = JsonSerializer.SerializeToElement(fields);
        }
        public void Dispose() => Shared.Dispose();
    }
}
