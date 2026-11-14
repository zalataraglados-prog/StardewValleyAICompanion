using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    private sealed class WateringFixture : IDisposable
    {
        internal OwnedRouteFixture Shared { get; }
        internal SnapshotEnvelope Before => Shared.Source.Before;
        internal SnapshotEnvelope After { get; private set; } = null!;
        internal MoneyRevenueCropNativeLifecycleTransitionRequest Transition { get; private set; } = null!;
        internal string QueuePath => Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "water-queue.json");
        internal CropTeacherRunStep Step => new(CropTeacherRunStageKinds.ExecuteCropBatch, TransitionDay, Id,
            "Farm", "FarmHouse", "(O)745", string.Empty, 0, 0, Shared.Inputs.Plan.Days.Single(day => day.TotalDay == TransitionDay)
                .ActionBatches.Single(row => row.OptionId == "executor.water_crop"), "original_watering_fixture");

        internal WateringFixture(int water = 20, bool bottomless = false, bool enchanted = false, bool efficient = false,
            int farmingLevel = 0, bool bowlMirrors = false, float energy = 100f, bool inconsistentBeforeMirror = false,
            bool atRefillApproach = false, Action<Fixture>? prepareSource = null, bool productExecutor = false)
        {
            Shared = new(productExecutor: productExecutor, prepareSource: source =>
            {
                var crop = source.Ledger.CropPlantingCommitments.Single();
                source.Before.State["menus"] = JsonSerializer.SerializeToElement(new
                {
                    active_menu = new { status = "available", value = new { is_open = false, type = string.Empty } }
                }, JsonOptions);
                ChangeState(source.Before, "time", node =>
                {
                    node["total_days"]!["value"] = crop.PlantingTotalDay;
                    node["year"]!["value"] = 2;
                    node["season"]!["value"] = "spring";
                    node["day"]!["value"] = 1;
                });
                ChangeState(source.Before, "farm", node => node["crops"]!["value"] = JsonSerializer.SerializeToNode(
                    new[] { 0, 1, 9 }.Select(x => new
                    {
                        location_id = "Farm", tile_x = x, tile_y = x == 9 ? 9 : 0,
                        harvest_source_seed_id = crop.SeedId, harvest_item_qualified_id = crop.HarvestItemQualifiedId,
                        phase_days = new[] { crop.BaseGrowDays, 99999 }, current_phase = 0, phase_count = 2,
                        day_of_current_phase = 0, dead = false, fully_grown = false, ready_for_harvest = false,
                        days_until_next_harvest_if_watered = crop.BaseGrowDays, regrow_days = crop.RegrowDays,
                        requires_watering_for_growth = true, watered = false, needs_watering = true,
                        unknown_native_crop_detail = "retained"
                    }), JsonOptions));
                ChangeState(source.Before, "current_location", node => node["crops"] =
                    JsonNode.Parse(source.Before.State["farm"].GetRawText())!["crops"]!.DeepClone());
                ChangeState(source.Before, "player", node =>
                {
                    if (atRefillApproach) node["tile_y"]!["value"] = 1;
                    node["energy"] = JsonSerializer.SerializeToNode(new { status = "available", value = energy });
                    node["watering_can_state"] = JsonSerializer.SerializeToNode(new { status = "available", value = new
                    {
                        projection_status = "exact_vanilla_power_zero", runtime_type = "StardewValley.Tools.WateringCan",
                        slot_index = 1, upgrade_level = 0, water_left = water, capacity = 40,
                        is_bottomless = bottomless, farmer_has_watering_can_enchantment = enchanted,
                        is_efficient = efficient, farming_level = farmingLevel,
                        energy_cost_per_power_zero_action = efficient ? 0d : Math.Max(0d, 2d - farmingLevel * 0.1d)
                    } });
                });
                if (bowlMirrors)
                {
                    RefreshBowlMirrors(source.Before, create: true);
                    if (inconsistentBeforeMirror) ChangeState(source.Before, "farm", node => node["pet_bowls"]!["value"]![0]!["energy_before"] = 99);
                }
                prepareSource?.Invoke(source);
            });
        }

        internal async Task<ActionQueueEnvelope> Compile(int? executableTileCount = null)
        {
            var task = InvokeLive<Task>("BuildCropTeacherActionBatchQueueAsync",
                Shared.Runtime.Client, Shared.Runtime.LiveOptions, 1, Step, Shared.Source.SourcePath,
                JsonNode.Parse(JsonSerializer.Serialize(Before, JsonOptions))!.AsObject(), Before.StateHash, executableTileCount!);
            try { await task; }
            catch (InvalidDataException error) when (Shared.Runtime.Handler.LastCompiledQueue is { })
            {
                var rejected = Shared.Runtime.Handler.LastCompiledQueue.Deserialize<ActionQueueEnvelope>(JsonOptions)!;
                throw new InvalidDataException(error.Message + ":" + string.Join(",", rejected.CompilerDiagnostics.Concat(
                    rejected.Items.SelectMany(item => item.BlockingReasons.Concat(item.MissingStateFactors)))), error);
            }
            var result = task.GetType().GetProperty("Result")!.GetValue(task)!;
            var queue = ((JsonObject)result.GetType().GetProperty("Queue")!.GetValue(result)!).Deserialize<ActionQueueEnvelope>(JsonOptions)!;
            Fixture.Save(QueuePath, queue);
            return queue;
        }

        internal async Task Emit(int applied = 2, bool failedSuffix = false, bool refillOnlySuffix = false,
            Action<SnapshotEnvelope>? change = null, Action<QueueExecutionReceiptEnvelope>? receiptChange = null, int? executableTileCount = null,
            string? retainedQueuePath = null)
        {
            var queue = retainedQueuePath is null ? await Compile(executableTileCount) :
                JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(retainedQueuePath), JsonOptions)!;
            var snapshots = new List<SnapshotEnvelope>();
            var paths = new List<string>();
            var attempted = applied + (failedSuffix ? 1 : 0);
            var current = Before;
            for (var index = 0; index < queue.Items.Length; index++)
            {
                var next = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(current, JsonOptions), JsonOptions)!;
                next.GameTick++;
                if (index < applied)
                {
                    ChangeState(next, "farm", node =>
                    {
                        node["crops"]!["value"]![index]!["watered"] = true;
                        node["crops"]!["value"]![index]!["needs_watering"] = false;
                    });
                    ChangeState(next, "current_location", node => node["crops"] =
                        JsonNode.Parse(next.State["farm"].GetRawText())!["crops"]!.DeepClone());
                    ChangeState(next, "player", node =>
                    {
                        var can = node["watering_can_state"]!["value"]!;
                        var available = can["water_left"]!.GetValue<int>();
                        if (available <= 0 && !can["farmer_has_watering_can_enchantment"]!.GetValue<bool>()) available = can["capacity"]!.GetValue<int>();
                        can["water_left"] = can["is_bottomless"]!.GetValue<bool>() ? available : available - 1;
                        var cost = can["is_efficient"]!.GetValue<bool>() ? 0f : 2f - can["farming_level"]!.GetValue<int>() * 0.1f;
                        node["energy"]!["value"] = node["energy"]!["value"]!.GetValue<float>() - cost;
                    });
                }
                if (refillOnlySuffix && index == applied)
                    ChangeState(next, "player", node => node["watering_can_state"]!["value"]!["water_left"] = 40);
                RefreshBowlMirrors(next);
                if (index == attempted - 1) change?.Invoke(next);
                next.StateHash = SnapshotHash.ComputeStateHash(next.State);
                snapshots.Add(next);
                paths.Add(Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "water-step-" + index + ".json"));
                current = next;
            }
            After = snapshots[attempted - 1];
            Shared.Runtime.Store.Snapshots[After.StateHash] = After;
            var candidate = queue.Items[0].NormalizedCommand.Parameters.Single(row => row.Name == "precondition" &&
                row.Value.StartsWith("candidate_id:", StringComparison.Ordinal)).Value["candidate_id:".Length..];
            var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("SeedAcquisitionReceipt", queue,
                Before, After, snapshots.ToArray(), paths.ToArray(), Shared.Files.Options.RunId, candidate);
            receipt.StepResults = receipt.StepResults.Take(attempted).ToArray();
            receipt.ExecutedItemCount = attempted;
            receipt.FinalPendingItemCount = queue.Items.Length - attempted;
            receipt.Success = receipt.SelectedCandidateCompleted = applied == queue.Items.Length;
            receipt.Status = failedSuffix ? "blocked" : "applied";
            foreach (var native in receipt.StepResults)
            {
                native.SelectedQueueCandidateCompleted = receipt.Success && native.QueueItemIndex == attempted - 1;
                native.PrimitiveVerificationReasons = new[] { "fixture_native_watering_observed" };
            }
            if (failedSuffix)
            {
                var failed = receipt.StepResults[^1];
                failed.Status = "blocked"; failed.PrimitiveVerificationStatus = "blocked";
                failed.BlockReasons = receipt.BlockReasons = new[] { "fixture_native_watering_blocked" };
            }
            receiptChange?.Invoke(receipt);
            for (var index = 0; index < attempted; index++) Fixture.Save(paths[index], snapshots[index]);
            var receiptPath = Path.Combine(Shared.Runtime.LiveOptions.SnapshotDir, "water-receipt.json");
            Fixture.Save(receiptPath, receipt);
            Transition = new("crop_actions", TransitionDay, retainedQueuePath ?? QueuePath, Shared.Source.SourcePath, receiptPath, paths[attempted - 1],
                Shared.Files.Options.RunId, Shared.Files.Options.ExecutorVersion, candidate);
        }

        private int TransitionDay => Before.State["time"].GetProperty("total_days").GetProperty("value").GetInt32();
        internal CropTeacherWateringMaintenance Open() => CropTeacherWateringMaintenance.Open(Shared.Inputs.Plan.RequestPath,
            Shared.Source.SourcePath, Shared.Source.LedgerPath, new[] { Id }, Id, Transition, "watering.step.1");
        public void Dispose() => Shared.Dispose();

        private static void RefreshBowlMirrors(SnapshotEnvelope snapshot, bool create = false)
        {
            ChangeState(snapshot, "farm", farm =>
            {
                if (create) farm["pet_bowls"] = JsonSerializer.SerializeToNode(new { status = "available", value = new[] { new
                {
                    location_id = "Farm", runtime_type = "StardewValley.Buildings.PetBowl",
                    building_tile_x = 7, building_tile_y = 8, watered = false, assigned_pet_present = true,
                    assigned_pet_id = "00000000-0000-0000-0000-000000000001", friendship_before_next_day = 10,
                    friendship_after_fill_and_next_day_update = 16, pet_love_mail_before = false,
                    action_status = "ready", unknown_native_bowl_detail = "retained"
                } } }, JsonOptions);
                if (farm["pet_bowls"] is not JsonObject bowls) return;
                var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
                var can = player["watering_can_state"]!["value"]!;
                var water = can["water_left"]!.GetValue<int>();
                var energy = player["energy"]!["value"]!.GetValue<float>();
                var cost = can["energy_cost_per_power_zero_action"]!.GetValue<double>();
                foreach (var row in bowls["value"]!.AsArray().OfType<JsonObject>())
                {
                    row["watering_can_slot_index"] = can["slot_index"]!.DeepClone();
                    row["watering_can_runtime_type"] = can["runtime_type"]!.DeepClone();
                    row["watering_can_upgrade_level"] = can["upgrade_level"]!.DeepClone();
                    row["watering_can_bottomless"] = can["is_bottomless"]!.DeepClone();
                    row["farmer_has_watering_can_enchantment"] = can["farmer_has_watering_can_enchantment"]!.DeepClone();
                    row["watering_can_efficient"] = can["is_efficient"]!.DeepClone();
                    row["watering_can_water_left"] = water;
                    row["expected_watering_can_water_after"] = can["is_bottomless"]!.GetValue<bool>() ? water : water - 1;
                    row["energy_before"] = energy;
                    row["watering_energy_cost"] = cost;
                    row["expected_energy_after"] = energy - cost;
                    row["energy_delta_projection_status"] = "exact_vanilla_power_zero";
                    row["action_status"] = water <= 0 && !can["farmer_has_watering_can_enchantment"]!.GetValue<bool>() ? "watering_can_empty" :
                        energy < cost ? "insufficient_stamina" : "ready";
                }
            });
        }
    }
}
