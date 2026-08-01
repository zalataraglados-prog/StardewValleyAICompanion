using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private const int MaxWaitTicksPerStep = 600;
        private readonly struct DialogueShopResponseSpec
        {
            public DialogueShopResponseSpec(string dialogueKey, string responseKey, string shopId)
            {
                DialogueKey = dialogueKey;
                ResponseKey = responseKey;
                ShopId = shopId;
            }

            public string DialogueKey { get; }
            public string ResponseKey { get; }
            public string ShopId { get; }
        }

        private readonly struct AdditionalConsumedReservation
        {
            public AdditionalConsumedReservation(string qualifiedItemId, int amount, int available)
            {
                QualifiedItemId = qualifiedItemId;
                Amount = amount;
                Available = available;
            }

            public string QualifiedItemId { get; }
            public int Amount { get; }
            public int Available { get; }
        }

        public SmallModelPlanEnvelope Compile(
            IEnumerable<PolicyEventCandidatePrediction> candidates,
            string stateHash,
            string goalId = "daily.closed_loop",
            string executionMode = "training_singleplayer",
            int maxCandidates = 4,
            int? availableMinutes = null,
            int? energyBudget = null)
        {
            var steps = new List<SmallModelPlanStep>();
            var audit = new List<SmallModelPlanCandidateAudit>();
            var remainingMinutes = availableMinutes;
            var remainingEnergy = energyBudget;
            var selected = 0;
            var maxAcceptedCandidates = Math.Max(1, maxCandidates);
            var reservedPlantTiles = new HashSet<string>(StringComparer.Ordinal);
            var reservedSeedCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var reservedMachineInputCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var reservedMachineAdditionalConsumedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var reservedInventorySlots = new HashSet<int>();
            var reservedInventorySlotQuantities = new Dictionary<int, int>();
            var reservedDebrisTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var candidate in OrderedCandidates(candidates))
            {
                var rollingDeferred = string.Equals(candidate.TimelineStatus, "deferred", StringComparison.Ordinal) &&
                    (candidate.ScheduledWaitCost ?? 0) > 0;
                var waitSteps = rollingDeferred
                    ? WaitSteps(candidate).Take(1).ToArray()
                    : WaitSteps(candidate).ToArray();
                var candidateSteps = rollingDeferred
                    ? Array.Empty<SmallModelPlanStep>()
                    : CandidateSteps(candidate).ToArray();
                var effectiveEnergyCost = Math.Max(0, candidate.EnergyCost);
                var candidateMinutes = waitSteps
                    .Concat(candidateSteps)
                    .Sum(step => step.EstimatedMinutes ?? TicksToMinutes(step.WaitTicks ?? candidate.EstimatedTicks));
                if (candidateSteps.Length == 0 && waitSteps.Length == 0)
                {
                    var unsupportedReasons =
                        DailyPlanCandidateCapabilityCatalog.TryGet(candidate.Kind, out var capability) &&
                        !capability.Compilable
                            ? new[] { "candidate_kind_known_but_not_executable", capability.BlockReason }
                            : new[] { "unsupported_candidate_kind_or_missing_required_candidate_fields" };
                    audit.Add(CandidateAudit(
                        candidate,
                        "skipped",
                        unsupportedReasons,
                        candidateMinutes,
                        remainingMinutes,
                        remainingMinutes,
                        remainingEnergy,
                        remainingEnergy));
                    continue;
                }

                var reservationConflicts = CandidateReservationConflicts(
                    candidate,
                    reservedPlantTiles,
                    reservedSeedCounts,
                    reservedMachineInputCounts,
                    reservedMachineAdditionalConsumedCounts,
                    reservedInventorySlots,
                    reservedInventorySlotQuantities,
                    reservedDebrisTargets);
                if (reservationConflicts.Length > 0)
                {
                    audit.Add(CandidateAudit(
                        candidate,
                        "skipped",
                        reservationConflicts,
                        candidateMinutes,
                        remainingMinutes,
                        remainingMinutes,
                        remainingEnergy,
                        remainingEnergy));
                    continue;
                }

                if (selected >= maxAcceptedCandidates)
                {
                    audit.Add(CandidateAudit(
                        candidate,
                        "skipped",
                        new[] { "max_candidates_reached" },
                        candidateMinutes,
                        remainingMinutes,
                        remainingMinutes,
                        remainingEnergy,
                        remainingEnergy));
                    continue;
                }

                if (remainingMinutes.HasValue && candidateMinutes > remainingMinutes.Value)
                {
                    audit.Add(CandidateAudit(
                        candidate,
                        "skipped",
                        new[] { "aggregate_time_budget_exceeded" },
                        candidateMinutes,
                        remainingMinutes,
                        remainingMinutes,
                        remainingEnergy,
                        remainingEnergy));
                    continue;
                }

                if (remainingEnergy.HasValue && effectiveEnergyCost > remainingEnergy.Value)
                {
                    audit.Add(CandidateAudit(
                        candidate,
                        "skipped",
                        new[] { "aggregate_energy_budget_exceeded" },
                        candidateMinutes,
                        remainingMinutes,
                        remainingMinutes,
                        remainingEnergy,
                        remainingEnergy));
                    continue;
                }

                var nextRemainingMinutes = remainingMinutes.HasValue ? Math.Max(0, remainingMinutes.Value - candidateMinutes) : (int?)null;
                var nextRemainingEnergy = remainingEnergy.HasValue ? Math.Max(0, remainingEnergy.Value - effectiveEnergyCost) : (int?)null;
                var acceptedSteps = waitSteps
                    .Concat(candidateSteps)
                    .Select(step => AnnotateBudget(
                        step,
                        candidate,
                        selected,
                        candidateMinutes,
                        remainingMinutes,
                        nextRemainingMinutes,
                        remainingEnergy,
                        nextRemainingEnergy))
                    .ToArray();
                steps.AddRange(acceptedSteps);
                audit.Add(CandidateAudit(
                    candidate,
                    "accepted",
                    rollingDeferred
                        ? new[] { "fits_aggregate_budget", "rolling_horizon_wait_then_refresh_snapshot" }
                        : new[] { "fits_aggregate_budget" },
                    candidateMinutes,
                    remainingMinutes,
                    nextRemainingMinutes,
                    remainingEnergy,
                    nextRemainingEnergy));
                if (remainingMinutes.HasValue)
                {
                    remainingMinutes = nextRemainingMinutes;
                }

                if (remainingEnergy.HasValue)
                {
                    remainingEnergy = nextRemainingEnergy;
                }

                if (!rollingDeferred)
                {
                    ReserveCandidate(candidate, reservedPlantTiles, reservedSeedCounts, reservedMachineInputCounts, reservedMachineAdditionalConsumedCounts, reservedInventorySlots, reservedInventorySlotQuantities, reservedDebrisTargets);
                }
                selected++;
            }

            return new SmallModelPlanEnvelope
            {
                PlanId = "daily_plan." + Guid.NewGuid().ToString("N"),
                SourceModel = "StardewAI.Core.Training.DailyPlanCompiler",
                StateHash = stateHash,
                GoalId = goalId,
                ExecutionMode = executionMode,
                Actor = ExecutionTargetProfiles.CreateActor(executionMode),
                PlanType = "daily_candidate_plan",
                Steps = steps.ToArray(),
                CandidateAudit = audit.ToArray()
            };
        }

        private static SmallModelPlanCandidateAudit CandidateAudit(
            PolicyEventCandidatePrediction candidate,
            string decision,
            string[] reasons,
            int candidateMinutes,
            int? remainingMinutesBefore,
            int? remainingMinutesAfter,
            int? remainingEnergyBefore,
            int? remainingEnergyAfter)
        {
            return new SmallModelPlanCandidateAudit
            {
                CandidateId = candidate.CandidateId,
                Kind = candidate.Kind,
                Decision = decision,
                Reasons = reasons,
                CandidateMinutes = candidateMinutes,
                CandidateEnergyCost = candidate.EnergyCost,
                RemainingMinutesBefore = remainingMinutesBefore,
                RemainingMinutesAfter = remainingMinutesAfter,
                RemainingEnergyBefore = remainingEnergyBefore,
                RemainingEnergyAfter = remainingEnergyAfter
            };
        }

        private static SmallModelPlanStep AnnotateBudget(
            SmallModelPlanStep step,
            PolicyEventCandidatePrediction candidate,
            int acceptedCandidateIndex,
            int candidateMinutes,
            int? remainingMinutesBefore,
            int? remainingMinutesAfter,
            int? remainingEnergyBefore,
            int? remainingEnergyAfter)
        {
            var parameters = new List<SmallModelActionParameter>(step.Parameters ?? Array.Empty<SmallModelActionParameter>())
            {
                Parameter("budget.accepted_candidate_index", acceptedCandidateIndex.ToString()),
                Parameter("budget.candidate_minutes", candidateMinutes.ToString()),
                Parameter("budget.candidate_energy_cost", candidate.EnergyCost.ToString())
            };
            if (remainingMinutesBefore.HasValue)
            {
                parameters.Add(Parameter("budget.remaining_minutes_before", remainingMinutesBefore.Value.ToString()));
            }
            if (remainingMinutesAfter.HasValue)
            {
                parameters.Add(Parameter("budget.remaining_minutes_after", remainingMinutesAfter.Value.ToString()));
            }
            if (remainingEnergyBefore.HasValue)
            {
                parameters.Add(Parameter("budget.remaining_energy_before", remainingEnergyBefore.Value.ToString()));
            }
            if (remainingEnergyAfter.HasValue)
            {
                parameters.Add(Parameter("budget.remaining_energy_after", remainingEnergyAfter.Value.ToString()));
            }

            var constraints = new List<string>(step.SafetyConstraints ?? Array.Empty<string>())
            {
                "daily_plan_aggregate_budget_checked"
            };
            if (remainingMinutesBefore.HasValue)
            {
                constraints.Add("daily_plan_time_budget_checked");
            }
            if (remainingEnergyBefore.HasValue)
            {
                constraints.Add("daily_plan_energy_budget_checked");
            }

            step.Parameters = parameters.ToArray();
            step.SafetyConstraints = constraints.Distinct(StringComparer.Ordinal).ToArray();
            return step;
        }

        private static IEnumerable<PolicyEventCandidatePrediction> OrderedCandidates(IEnumerable<PolicyEventCandidatePrediction> candidates)
        {
            var candidatesArray = candidates
                .Where(candidate => candidate.TimelineStatus != "blocked")
                .ToArray();
            var shopOpenRanks = candidatesArray
                .Where(candidate => candidate.Kind == "interact_endpoint" && !string.IsNullOrWhiteSpace(candidate.ShopId))
                .GroupBy(candidate => candidate.ShopId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Min(candidate => candidate.Rank <= 0 ? int.MaxValue : candidate.Rank),
                    StringComparer.OrdinalIgnoreCase);

            return candidatesArray
                .OrderBy(candidate => SequenceRank(candidate, shopOpenRanks))
                .ThenBy(candidate => ShopSequenceBias(candidate, shopOpenRanks))
                .ThenBy(candidate => candidate.TimelineStatus == "ready_now" ? 0 : 1)
                .ThenBy(candidate => candidate.ScheduledWaitCost ?? 0)
                .ThenByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal);
        }

        private static int SequenceRank(
            PolicyEventCandidatePrediction candidate,
            IReadOnlyDictionary<string, int> shopOpenRanks)
        {
            if (candidate.Kind == "buy_shop_item" &&
                !string.IsNullOrWhiteSpace(candidate.ShopId) &&
                shopOpenRanks.TryGetValue(candidate.ShopId, out var openRank))
            {
                return openRank;
            }

            return candidate.Rank <= 0 ? int.MaxValue : candidate.Rank;
        }

        private static string[] CandidateReservationConflicts(
            PolicyEventCandidatePrediction candidate,
            ISet<string> reservedPlantTiles,
            IReadOnlyDictionary<string, int> reservedSeedCounts,
            IReadOnlyDictionary<string, int> reservedMachineInputCounts,
            IReadOnlyDictionary<string, int> reservedMachineAdditionalConsumedCounts,
            ISet<int> reservedInventorySlots,
            IReadOnlyDictionary<int, int> reservedInventorySlotQuantities,
            ISet<string> reservedDebrisTargets)
        {
            var reasons = new List<string>();
            var plantTileKey = PlantTileReservationKey(candidate);
            if (!string.IsNullOrWhiteSpace(plantTileKey) && reservedPlantTiles.Contains(plantTileKey))
            {
                reasons.Add("daily_plan_target_tile_already_reserved");
            }

            var seedId = PlantSeedId(candidate);
            if (!string.IsNullOrWhiteSpace(seedId) &&
                candidate.Quantity > 0 &&
                reservedSeedCounts.TryGetValue(seedId, out var reservedCount) &&
                reservedCount >= candidate.Quantity)
            {
                reasons.Add("daily_plan_seed_stack_already_reserved");
            }

            var machineInputKey = MachineInputReservationKey(candidate);
            if (!string.IsNullOrWhiteSpace(machineInputKey) &&
                candidate.Quantity > 0 &&
                reservedMachineInputCounts.TryGetValue(machineInputKey, out var reservedInputCount) &&
                reservedInputCount >= candidate.Quantity)
            {
                reasons.Add("daily_plan_machine_input_stack_already_reserved");
            }

            foreach (var consumedItem in MachineAdditionalConsumedItems(candidate))
            {
                if (consumedItem.Available <= 0)
                {
                    reasons.Add("daily_plan_machine_additional_consumed_inventory_unavailable");
                    continue;
                }

                reservedMachineAdditionalConsumedCounts.TryGetValue(consumedItem.QualifiedItemId, out var reservedAdditionalCount);
                if (reservedAdditionalCount + consumedItem.Amount > consumedItem.Available)
                {
                    reasons.Add("daily_plan_machine_additional_consumed_stack_already_reserved");
                    break;
                }
            }

            var inventorySlotIndex = InventorySlotReservationKey(candidate);
            if (inventorySlotIndex.HasValue)
            {
                if (reservedInventorySlots.Contains(inventorySlotIndex.Value))
                {
                    reasons.Add("daily_plan_inventory_slot_already_reserved");
                }

                var inventoryQuantity = InventorySlotReservationQuantity(candidate);
                if (inventoryQuantity.HasValue &&
                    reservedInventorySlotQuantities.TryGetValue(inventorySlotIndex.Value, out var reservedQty) &&
                    reservedQty >= inventoryQuantity.Value)
                {
                    reasons.Add("daily_plan_inventory_slot_quantity_already_reserved");
                }
            }

            var debrisTarget = DebrisReservationKey(candidate);
            if (!string.IsNullOrWhiteSpace(debrisTarget) &&
                reservedDebrisTargets.Contains(debrisTarget))
            {
                reasons.Add("daily_plan_debris_target_already_reserved");
            }

            return reasons.ToArray();
        }

        private static void ReserveCandidate(
            PolicyEventCandidatePrediction candidate,
            ISet<string> reservedPlantTiles,
            IDictionary<string, int> reservedSeedCounts,
            IDictionary<string, int> reservedMachineInputCounts,
            IDictionary<string, int> reservedMachineAdditionalConsumedCounts,
            ISet<int> reservedInventorySlots,
            IDictionary<int, int> reservedInventorySlotQuantities,
            ISet<string> reservedDebrisTargets)
        {
            var plantTileKey = PlantTileReservationKey(candidate);
            if (!string.IsNullOrWhiteSpace(plantTileKey))
            {
                reservedPlantTiles.Add(plantTileKey);
            }

            var seedId = PlantSeedId(candidate);
            if (!string.IsNullOrWhiteSpace(seedId))
            {
                reservedSeedCounts[seedId] = reservedSeedCounts.TryGetValue(seedId, out var current)
                    ? current + 1
                    : 1;
            }

            var machineInputKey = MachineInputReservationKey(candidate);
            if (!string.IsNullOrWhiteSpace(machineInputKey))
            {
                reservedMachineInputCounts[machineInputKey] = reservedMachineInputCounts.TryGetValue(machineInputKey, out var current)
                    ? current + 1
                    : 1;
            }

            foreach (var consumedItem in MachineAdditionalConsumedItems(candidate))
            {
                reservedMachineAdditionalConsumedCounts[consumedItem.QualifiedItemId] =
                    reservedMachineAdditionalConsumedCounts.TryGetValue(consumedItem.QualifiedItemId, out var current)
                        ? current + consumedItem.Amount
                        : consumedItem.Amount;
            }

            var inventorySlotIndex = InventorySlotReservationKey(candidate);
            if (inventorySlotIndex.HasValue)
            {
                reservedInventorySlots.Add(inventorySlotIndex.Value);
                var inventoryQuantity = InventorySlotReservationQuantity(candidate);
                if (inventoryQuantity.HasValue)
                {
                    reservedInventorySlotQuantities[inventorySlotIndex.Value] = reservedInventorySlotQuantities.TryGetValue(inventorySlotIndex.Value, out var current)
                        ? current + inventoryQuantity.Value
                        : inventoryQuantity.Value;
                }
            }

            var debrisTarget = DebrisReservationKey(candidate);
            if (!string.IsNullOrWhiteSpace(debrisTarget))
            {
                reservedDebrisTargets.Add(debrisTarget);
            }
        }

        private static string DebrisReservationKey(
            PolicyEventCandidatePrediction candidate)
        {
            var debrisIndex = CandidateParameter(candidate, "debris_index");
            if (string.IsNullOrWhiteSpace(debrisIndex))
            {
                debrisIndex = ParseValue(
                    candidate.ExpectedEffect,
                    "debris_index=");
            }
            if (string.IsNullOrWhiteSpace(debrisIndex))
            {
                return string.Empty;
            }

            var executionOptionId = ParseValue(
                candidate.ExpectedEffect,
                "execution_option_id=");
            if (candidate.Kind != "pickup_debris_item" &&
                !string.Equals(
                    executionOptionId,
                    "executor.pickup_debris",
                    StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var targetX = candidate.TileX?.ToString() ??
                CandidateParameter(candidate, "target_tile_x");
            var targetY = candidate.TileY?.ToString() ??
                CandidateParameter(candidate, "target_tile_y");
            var itemId = !string.IsNullOrWhiteSpace(candidate.QualifiedItemId)
                ? candidate.QualifiedItemId
                : CandidateParameter(candidate, "qualified_item_id");
            return candidate.LocationId + "|" + debrisIndex + "|" +
                targetX + "," + targetY + "|" + itemId;
        }

        private static string PlantTileReservationKey(PolicyEventCandidatePrediction candidate)
        {
            if (candidate.Kind != "plant_seed_tile" || !candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return string.Empty;
            }

            var location = string.IsNullOrWhiteSpace(candidate.LocationId) ? "current_location" : candidate.LocationId;
            return location + ":" + candidate.TileX.Value + "," + candidate.TileY.Value;
        }

        private static string PlantSeedId(PolicyEventCandidatePrediction candidate)
        {
            return candidate.Kind == "plant_seed_tile"
                ? !string.IsNullOrWhiteSpace(candidate.ItemId) ? candidate.ItemId : ParseValue(candidate.ExpectedEffect, "seed_id=")
                : string.Empty;
        }

        private static int? InventorySlotReservationKey(PolicyEventCandidatePrediction candidate)
        {
            var playerInventoryTransfer = candidate.Kind == "transfer_inventory_item" &&
                CandidateParameter(candidate, "source_node_id").StartsWith("player:", StringComparison.Ordinal);
            if (candidate.Kind != "ship_inventory_item_to_bin" && !playerInventoryTransfer)
            {
                return null;
            }

            if (candidate.SlotIndex.HasValue)
            {
                return candidate.SlotIndex.Value;
            }

            var slotStr = CandidateParameter(candidate, "slot_index");
            return int.TryParse(slotStr, out var slotIndex) ? slotIndex : null;
        }

        private static int? InventorySlotReservationQuantity(PolicyEventCandidatePrediction candidate)
        {
            var playerInventoryTransfer = candidate.Kind == "transfer_inventory_item" &&
                CandidateParameter(candidate, "source_node_id").StartsWith("player:", StringComparison.Ordinal);
            if (candidate.Kind != "ship_inventory_item_to_bin" && !playerInventoryTransfer)
            {
                return null;
            }

            return candidate.Quantity > 0 ? candidate.Quantity : null;
        }

        private static string MachineInputReservationKey(PolicyEventCandidatePrediction candidate)
        {
            if (candidate.Kind != "load_machine_input_tile")
            {
                return string.Empty;
            }

            var slotIndex = candidate.SlotIndex.HasValue
                ? candidate.SlotIndex.Value.ToString()
                : ParseValue(candidate.ExpectedEffect, "input_slot_index=");
            if (string.IsNullOrWhiteSpace(slotIndex))
            {
                return string.Empty;
            }

            var itemId = !string.IsNullOrWhiteSpace(candidate.QualifiedItemId)
                ? candidate.QualifiedItemId
                : !string.IsNullOrWhiteSpace(candidate.ItemId)
                    ? candidate.ItemId
                    : ParseValue(candidate.ExpectedEffect, "qualified_item_id=");
            if (string.IsNullOrWhiteSpace(itemId))
            {
                itemId = ParseValue(candidate.ExpectedEffect, "item_id=");
            }

            return string.IsNullOrWhiteSpace(itemId) ? "slot:" + slotIndex : "slot:" + slotIndex + ":" + itemId;
        }

        private static AdditionalConsumedReservation[] MachineAdditionalConsumedItems(PolicyEventCandidatePrediction candidate)
        {
            if (candidate.Kind != "load_machine_input_tile")
            {
                return Array.Empty<AdditionalConsumedReservation>();
            }

            var consumed = ParseQuantityMap(ParseValue(candidate.ExpectedEffect, "machine_additional_consumed_items="));
            if (consumed.Count == 0)
            {
                return Array.Empty<AdditionalConsumedReservation>();
            }

            var available = ParseQuantityMap(ParseValue(candidate.ExpectedEffect, "machine_additional_consumed_available="));
            return consumed
                .Select(pair =>
                {
                    available.TryGetValue(pair.Key, out var availableCount);
                    return new AdditionalConsumedReservation(pair.Key, pair.Value, availableCount);
                })
                .ToArray();
        }

        private static Dictionary<string, int> ParseQuantityMap(string value)
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(value))
            {
                return result;
            }

            foreach (var segment in value.Split(','))
            {
                var parts = segment.Split(':');
                if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || !int.TryParse(parts[1], out var amount))
                {
                    continue;
                }

                result[parts[0]] = result.TryGetValue(parts[0], out var current)
                    ? current + Math.Max(0, amount)
                    : Math.Max(0, amount);
            }

            return result;
        }

        private static int ShopSequenceBias(
            PolicyEventCandidatePrediction candidate,
            IReadOnlyDictionary<string, int> shopOpenRanks)
        {
            if (candidate.Kind == "interact_endpoint" && !string.IsNullOrWhiteSpace(candidate.ShopId))
            {
                return 0;
            }

            if (candidate.Kind == "buy_shop_item" &&
                !string.IsNullOrWhiteSpace(candidate.ShopId) &&
                shopOpenRanks.ContainsKey(candidate.ShopId))
            {
                return 1;
            }

            return 0;
        }

        private static void AppendWaitSteps(ICollection<SmallModelPlanStep> steps, PolicyEventCandidatePrediction candidate)
        {
            foreach (var step in WaitSteps(candidate))
            {
                steps.Add(step);
            }
        }

        private static IEnumerable<SmallModelPlanStep> WaitSteps(PolicyEventCandidatePrediction candidate)
        {
            var remaining = candidate.ScheduledWaitCost ?? 0;
            var index = 0;
            while (remaining > 0)
            {
                var ticks = Math.Min(MaxWaitTicksPerStep, remaining);
                yield return new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "wait", index++),
                    Kind = "wait_ticks",
                    WaitTicks = ticks,
                    EstimatedMinutes = Math.Max(1, ticks / 60),
                    Preconditions = new[] { "timeline_status:" + candidate.TimelineStatus },
                    ExpectedEffects = new[] { "time_advances", "fresh_snapshot_replan_required=true" },
                    SafetyConstraints = new[] { "do_not_wait_with_danger_or_active_menu" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = ContinuationParameters(candidate)
                };
                remaining -= ticks;
            }
        }

        private static SmallModelActionParameter[] ContinuationParameters(PolicyEventCandidatePrediction candidate)
        {
            var names = new HashSet<string>(new[]
            {
                "continuation.option_id",
                "continuation.npc_name",
                "continuation.target_location",
                "continuation.slot_index",
                "continuation.qualified_item_id",
                "social_route.position_source",
                "social_route.future_schedule_projection",
                "social_continuation_dialogue_recovery",
                "profession_choice_id",
                "profession_choice_source"
            }, StringComparer.Ordinal);
            return candidate.Parameters
                .Where(parameter => names.Contains(parameter.Name))
                .ToArray();
        }

    }
}
