using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Verifier;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private string[] CompilerProbeBlockingReasons(SnapshotEnvelope snapshot, OptionAvailabilityCandidate candidate)
        {
            if (candidate.Parameters.Length == 0 && candidate.OptionId != "executor.interact")
            {
                return Array.Empty<string>();
            }

            return CompilerProbeBlockingReasons(CompilerProbeItem(snapshot, candidate));
        }

        private ActionQueueItem? CompilerProbeItem(SnapshotEnvelope snapshot, OptionAvailabilityCandidate candidate)
        {
            var envelope = new SmallModelActionEnvelope
            {
                ModelOutputId = "availability.synthetic",
                SourceModel = "candidate-availability.evaluator",
                StateHash = snapshot.StateHash,
                GoalId = "candidate.availability",
                ExecutionMode = "training_singleplayer",
                Actor = new ActionActorRef
                {
                    ActorId = "training_farmer.availability",
                    ActorType = "training_farmer",
                    ControlSurface = "training_sandbox"
                },
                Actions = new[]
                {
                    new SmallModelAction
                    {
                        ActionId = "availability.synthetic.action",
                        OptionId = candidate.OptionId,
                        Rationale = "candidate availability parameter-bound validation",
                        Parameters = candidate.Parameters
                    }
                }
            };

            var queue = compiler.Compile(envelope, snapshot);
            return queue.Items.FirstOrDefault();
        }

        private static string[] CompilerProbeBlockingReasons(ActionQueueItem? item)
        {
            if (item is null)
            {
                return Array.Empty<string>();
            }

            return item.BlockingReasons
                .Where(reason => reason != "queue_global_compiler_block")
                .ToArray();
        }

        private static string ReadParameter(IEnumerable<SmallModelActionParameter> parameters, string name)
        {
            return parameters
                .FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))
                ?.Value ?? string.Empty;
        }

        private static int? ReadParameterInt(IEnumerable<SmallModelActionParameter> parameters, string name)
        {
            return int.TryParse(ReadParameter(parameters, name), out var value) ? value : null;
        }

        private static bool IsExecutorEnabled(string optionId)
        {
            return optionId == "recovery.stabilize_day" ||
                optionId == "farm.maintain_crops" ||
                optionId == "farm.process_machines" ||
                optionId == "farm.collect_animal_products" ||
                optionId == "fishing.catch_fish" ||
                optionId == "fishing.collect_crab_pots" ||
                optionId == "fishing.service_fish_ponds" ||
                optionId == "foraging.collect_spawned_objects" ||
                optionId == "foraging.pan_ore_spot" ||
                optionId == "mining.reach_depth" ||
                optionId == "mining.obtain_skull_key" ||
                optionId == "mining.acquire_golden_scythe" ||
                optionId == "volcano.reach_caldera" ||
                optionId == "economy.buy_supplies" ||
                optionId == "exploration.visit_location" ||
                optionId == "executor.move_to_tile" ||
                optionId == "executor.traverse_connector" ||
                optionId == "executor.face_direction" ||
                optionId == "executor.wait_ticks" ||
                optionId == "executor.mine_stone" ||
                optionId == "executor.break_container" ||
                optionId == "executor.combat_monster" ||
                optionId == "executor.shoot_monster" ||
                optionId == "executor.place_bomb" ||
                optionId == "executor.consume_food" ||
                optionId == "executor.descend_ladder" ||
                optionId == "executor.descend_shaft" ||
                optionId == "executor.exit_mine" ||
                optionId == "executor.cool_volcano_lava" ||
                optionId == "executor.break_volcano_stone" ||
                optionId == "executor.break_volcano_container" ||
                optionId == "executor.combat_volcano_monster" ||
                optionId == "executor.select_safe_item_slot" ||
                optionId == "executor.close_menu" ||
                optionId == "executor.buy_shop_item" ||
                optionId == "executor.clear_obstacle" ||
                optionId == "executor.break_farm_resource_clump" ||
                optionId == "executor.till_soil" ||
                optionId == "executor.plant_seed" ||
                optionId == "executor.harvest_crop" ||
                optionId == "executor.harvest_giant_crop" ||
                optionId == "executor.catch_fish" ||
                optionId == "executor.interact" ||
                optionId == "executor.sleep" ||
                optionId == "executor.pickup_debris" ||
                optionId == "executor.collect_spawned_object" ||
                optionId == "executor.collect_crab_pot" ||
                optionId == "executor.collect_fish_pond_output" ||
                optionId == "executor.complete_fish_pond_request" ||
                optionId == "executor.collect_animal_product" ||
                optionId == "executor.pan_ore_spot" ||
                optionId == "executor.collect_machine_output" ||
                optionId == "executor.load_machine_input" ||
                optionId == "executor.choose_dialogue_response" ||
                optionId == "executor.social_interact";
        }

        private static bool IsPreviewOnly(string optionId, string trainingRole, bool executorEnabled)
        {
            if (trainingRole == TrainingRoles.ExecutorCalibration)
            {
                return !executorEnabled;
            }

            return optionId == "economy.sell_items" ||
                optionId == "economy.ship_items" ||
                optionId == "social.talk_npc" ||
                optionId == "social.gift_npc" ||
                optionId == "quest.advance";
        }

        private static string ExecutorDisabledReason(string optionId)
        {
            if (optionId == "economy.sell_items")
            {
                return "sell_shipping_executor_disabled";
            }

            if (optionId == "social.talk_npc" || optionId == "social.gift_npc")
            {
                return "social_high_level_direct_executor_disabled_use_daily_plan_compiler";
            }

            if (optionId == "quest.advance")
            {
                return "quest_native_executor_not_implemented";
            }

            if (optionId == "executor.harvest_crop")
            {
                return "harvest_executor_disabled";
            }

            return "executor_disabled";
        }

        private sealed class FullShipmentItemIndexEntry
        {
            public int CurrentShippedCount { get; set; }
            public bool Shipped { get; set; }
        }

        private static IReadOnlyDictionary<string, FullShipmentItemIndexEntry>? ReadFullShipmentIndex(SnapshotEnvelope snapshot)
        {
            if (!snapshot.State.TryGetValue("world_progress", out var worldSection) ||
                worldSection.ValueKind != JsonValueKind.Object ||
                !worldSection.TryGetProperty("full_shipment_progress", out var envelope) ||
                envelope.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var status = ReadString(envelope, "status");
            if (status != "available" && status != "derived")
            {
                return null;
            }

            if (!envelope.TryGetProperty("value", out var value) ||
                value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!value.TryGetProperty("eligible_item_count", out var eligibleCount) ||
                eligibleCount.ValueKind != JsonValueKind.Number ||
                !eligibleCount.TryGetInt32(out var expectedCount) ||
                expectedCount < 0)
            {
                return null;
            }

            if (!value.TryGetProperty("items", out var items) ||
                items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var index = new Dictionary<string, FullShipmentItemIndexEntry>(StringComparer.Ordinal);
            foreach (var entry in items.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var itemId = ReadString(entry, "item_id");
                if (string.IsNullOrWhiteSpace(itemId))
                {
                    return null;
                }

                if (!entry.TryGetProperty("current_shipped_count", out var currentCountEl) ||
                    currentCountEl.ValueKind != JsonValueKind.Number ||
                    !currentCountEl.TryGetInt32(out var currentCount) ||
                    currentCount < 0)
                {
                    return null;
                }

                if (!entry.TryGetProperty("shipped", out var shippedEl) ||
                    (shippedEl.ValueKind != JsonValueKind.True && shippedEl.ValueKind != JsonValueKind.False))
                {
                    return null;
                }
                var shipped = shippedEl.ValueKind == JsonValueKind.True;

                if (shipped != (currentCount > 0))
                {
                    return null;
                }

                if (!index.TryAdd(itemId, new FullShipmentItemIndexEntry
                {
                    CurrentShippedCount = currentCount,
                    Shipped = shipped
                }))
                {
                    return null;
                }
            }

            if (index.Count != expectedCount)
            {
                return null;
            }

            return index;
        }    }
}
