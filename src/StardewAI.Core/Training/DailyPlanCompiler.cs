using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed class DailyPlanCompiler
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
            foreach (var candidate in OrderedCandidates(candidates))
            {
                var waitSteps = WaitSteps(candidate).ToArray();
                var candidateSteps = CandidateSteps(candidate).ToArray();
                var candidateMinutes = waitSteps
                    .Concat(candidateSteps)
                    .Sum(step => step.EstimatedMinutes ?? TicksToMinutes(step.WaitTicks ?? candidate.EstimatedTicks));
                if (candidateSteps.Length == 0)
                {
                    audit.Add(CandidateAudit(
                        candidate,
                        "skipped",
                        new[] { "unsupported_candidate_kind_or_missing_required_candidate_fields" },
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
                    reservedInventorySlotQuantities);
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

                if (remainingEnergy.HasValue && candidate.EnergyCost > remainingEnergy.Value)
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
                var nextRemainingEnergy = remainingEnergy.HasValue ? Math.Max(0, remainingEnergy.Value - candidate.EnergyCost) : (int?)null;
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
                    new[] { "fits_aggregate_budget" },
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

                ReserveCandidate(candidate, reservedPlantTiles, reservedSeedCounts, reservedMachineInputCounts, reservedMachineAdditionalConsumedCounts, reservedInventorySlots, reservedInventorySlotQuantities);
                selected++;
            }

            return new SmallModelPlanEnvelope
            {
                PlanId = "daily_plan." + Guid.NewGuid().ToString("N"),
                SourceModel = "StardewAI.Core.Training.DailyPlanCompiler",
                StateHash = stateHash,
                GoalId = goalId,
                ExecutionMode = executionMode,
                Actor = executionMode == "training_singleplayer"
                    ? new ActionActorRef
                    {
                        ActorId = "training_farmer.main",
                        ActorType = "training_farmer",
                        ControlSurface = "training_sandbox"
                    }
                    : new ActionActorRef
                    {
                        ActorId = "companion.main",
                        ActorType = "ai_companion",
                        ControlSurface = "companion_actor"
                    },
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
            IReadOnlyDictionary<int, int> reservedInventorySlotQuantities)
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

            var shipSlotIndex = ShipSlotReservationKey(candidate);
            if (shipSlotIndex.HasValue)
            {
                if (reservedInventorySlots.Contains(shipSlotIndex.Value))
                {
                    reasons.Add("daily_plan_inventory_slot_already_reserved");
                }

                var shipQuantity = ShipQuantity(candidate);
                if (shipQuantity.HasValue &&
                    reservedInventorySlotQuantities.TryGetValue(shipSlotIndex.Value, out var reservedQty) &&
                    reservedQty >= shipQuantity.Value)
                {
                    reasons.Add("daily_plan_inventory_slot_quantity_already_reserved");
                }
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
            IDictionary<int, int> reservedInventorySlotQuantities)
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

            var shipSlotIndex = ShipSlotReservationKey(candidate);
            if (shipSlotIndex.HasValue)
            {
                reservedInventorySlots.Add(shipSlotIndex.Value);
                var shipQuantity = ShipQuantity(candidate);
                if (shipQuantity.HasValue)
                {
                    reservedInventorySlotQuantities[shipSlotIndex.Value] = reservedInventorySlotQuantities.TryGetValue(shipSlotIndex.Value, out var current)
                        ? current + shipQuantity.Value
                        : shipQuantity.Value;
                }
            }
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

        private static int? ShipSlotReservationKey(PolicyEventCandidatePrediction candidate)
        {
            if (candidate.Kind != "ship_inventory_item_to_bin")
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

        private static int? ShipQuantity(PolicyEventCandidatePrediction candidate)
        {
            if (candidate.Kind != "ship_inventory_item_to_bin")
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
                    ExpectedEffects = new[] { "time_advances_without_state_mutation" },
                    SafetyConstraints = new[] { "do_not_wait_with_danger_or_active_menu" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                };
                remaining -= ticks;
            }
        }

        private static IEnumerable<SmallModelPlanStep> CandidateSteps(PolicyEventCandidatePrediction candidate)
        {
            if (candidate.Kind == "interact_endpoint")
            {
                return InteractEndpointSteps(candidate);
            }

            if (candidate.Kind == "buy_shop_item")
            {
                return BuyShopItemSteps(candidate);
            }

            if (candidate.Kind == "recovery_refresh_plan")
            {
                return RecoveryRefreshSteps(candidate);
            }

            if (candidate.Kind == "recovery_close_menu")
            {
                return RecoveryCloseMenuSteps(candidate);
            }

            if (candidate.Kind == "recovery_return_home" ||
                candidate.Kind == "recovery_sleep_immediately" ||
                candidate.Kind == "recovery_sleep_before_collapse")
            {
                return RecoveryExecutionSteps(candidate);
            }

            if (candidate.Kind == "route_connector_tile")
            {
                return RouteConnectorSteps(candidate);
            }

            if (candidate.Kind == "water_crop_tile")
            {
                return WaterCropTileSteps(candidate);
            }

            if (candidate.Kind == "catch_fish")
            {
                return CatchFishSteps(candidate);
            }

            if (candidate.Kind == "harvest_crop_tile")
            {
                return HarvestCropTileSteps(candidate);
            }

            if (candidate.Kind == "harvest_giant_crop_tile")
            {
                return HarvestGiantCropTileSteps(candidate);
            }

            if (candidate.Kind == "pickup_debris_item")
            {
                return PickupDebrisItemSteps(candidate);
            }

            if (candidate.Kind == "collect_machine_output_tile")
            {
                return CollectMachineOutputSteps(candidate);
            }

            if (candidate.Kind == "load_machine_input_tile")
            {
                return LoadMachineInputSteps(candidate);
            }

            if (candidate.Kind == "clear_obstacle_tile")
            {
                return ClearObstacleTileSteps(candidate);
            }

            if (candidate.Kind == "plant_seed_tile")
            {
                return PlantSeedTileSteps(candidate);
            }

            if (candidate.Kind == "social_talk_current" || candidate.Kind == "social_gift_current")
            {
                return SocialInteractionSteps(candidate);
            }

            if (candidate.Kind == "ship_inventory_item_to_bin")
            {
                return ShipInventoryItemToBinSteps(candidate);
            }

            return Array.Empty<SmallModelPlanStep>();
        }

        private static IEnumerable<SmallModelPlanStep> CatchFishSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue ||
                !CandidateInt(candidate, "bobber_tile_x").HasValue ||
                !CandidateInt(candidate, "bobber_tile_y").HasValue ||
                !CandidateInt(candidate, "rod_slot_index").HasValue ||
                string.IsNullOrWhiteSpace(CandidateParameter(candidate, "rule_key")) ||
                !string.Equals(CandidateParameter(candidate, "outcome_distribution_complete"), "true", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(CandidateParameter(candidate, "outcome_distribution_json")) ||
                string.IsNullOrWhiteSpace(CandidateParameter(candidate, "possible_qualified_item_ids_json")) ||
                !string.IsNullOrWhiteSpace(CandidateParameter(candidate, "expected_qualified_item_id")))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var routeDistance = CandidateInt(candidate, "route_distance_tiles") ?? 0;
            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "move_to_fishing_stand", 0),
                    Kind = "move_to_tile",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = Math.Max(1, (int)Math.Ceiling(routeDistance / 5d)),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.tile=" + candidate.TileX + "," + candidate.TileY },
                    SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler", "no_direct_coordinate_teleport" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("max_movement_tiles", Math.Max(1, routeDistance).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    }
                },
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "catch_fish", 1),
                    Kind = "catch_fish",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(Math.Max(1, candidate.EstimatedTicks - routeDistance * 12)),
                    Preconditions = new[]
                    {
                        "candidate_id:" + candidate.CandidateId,
                        "player_at_fishing_stand=true",
                        "fishing_context_revalidated=true"
                    },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[]
                    {
                        "legal_player_equivalent_fishing_inputs_only",
                        "no_forced_catch_result",
                        "success_requires_observed_post_state"
                    },
                    FailurePolicy = new[] { "cancel_safely_refresh_snapshot_and_replan" },
                    Parameters = candidate.Parameters
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> InteractEndpointSteps(PolicyEventCandidatePrediction candidate)
        {
            var steps = new List<SmallModelPlanStep>();
            var standTile = ParseCoordinate(candidate.ExpectedEffect, "move_to_adjacent=");
            if (standTile.HasValue)
            {
                steps.Add(new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "move_to_adjacent", 0),
                    Kind = "move_to_tile",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = standTile.Value.X,
                    TargetTileY = standTile.Value.Y,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.tile=" + standTile.Value.X + "," + standTile.Value.Y },
                    SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                });
            }

            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return steps;
            }

            var expectedActionType = ParseValue(candidate.ExpectedEffect, "preview_interact=");
            if (string.IsNullOrWhiteSpace(expectedActionType))
            {
                expectedActionType = "OpenShop";
            }

            var dialogueResponse = DialogueShopResponse(expectedActionType, candidate.ShopId);
            steps.Add(new SmallModelPlanStep
            {
                StepId = StepId(candidate, "interact", 1),
                Kind = "interact",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 1,
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "target_tile_adjacent=true" },
                ExpectedEffects = dialogueResponse.HasValue
                    ? new[] { "menus.active_menu.is_open=true", "DialogueBox", "interact_map_action_" + expectedActionType }
                    : new[] { "menus.active_menu.is_open=true", "interact_map_action_" + expectedActionType },
                SafetyConstraints = new[] { "interaction_kind=map_action", "expected_action_type=" + expectedActionType },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = new[]
                {
                    Parameter("interaction_kind", "map_action"),
                    Parameter("expected_action_type", expectedActionType)
                }
            });
            if (dialogueResponse.HasValue)
            {
                steps.Add(new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "dialogue_shop_response", 2),
                    Kind = "choose_dialogue_response",
                    EstimatedMinutes = 1,
                    Preconditions = new[] { "active_menu.type=DialogueBox", "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "menus.active_menu.is_open=true", "ShopMenu" },
                    SafetyConstraints = new[] { "dialogue_response_whitelisted", "expected_shop_id=" + dialogueResponse.Value.ShopId },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("expected_dialogue_key", dialogueResponse.Value.DialogueKey),
                        Parameter("dialogue_response_key", dialogueResponse.Value.ResponseKey),
                        Parameter("expected_shop_id", dialogueResponse.Value.ShopId)
                    }
                });
            }
            return steps;
        }

        private static DialogueShopResponseSpec? DialogueShopResponse(string expectedActionType, string shopId)
        {
            if (string.Equals(expectedActionType, "Blacksmith", StringComparison.OrdinalIgnoreCase))
            {
                return new DialogueShopResponseSpec("Blacksmith", "Shop", string.IsNullOrWhiteSpace(shopId) ? "Blacksmith" : shopId);
            }

            if (string.Equals(expectedActionType, "Carpenter", StringComparison.OrdinalIgnoreCase))
            {
                return new DialogueShopResponseSpec("carpenter", "Shop", string.IsNullOrWhiteSpace(shopId) ? "Carpenter" : shopId);
            }

            if (string.Equals(expectedActionType, "Marnie", StringComparison.OrdinalIgnoreCase))
            {
                return new DialogueShopResponseSpec("Marnie", "Supplies", string.IsNullOrWhiteSpace(shopId) ? "AnimalShop" : shopId);
            }

            if (string.Equals(expectedActionType, "AdventureGuild", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(expectedActionType, "adventureGuild", StringComparison.OrdinalIgnoreCase))
            {
                return new DialogueShopResponseSpec("adventureGuild", "Shop", string.IsNullOrWhiteSpace(shopId) ? "AdventureShop" : shopId);
            }

            return null;
        }

        private static IEnumerable<SmallModelPlanStep> BuyShopItemSteps(PolicyEventCandidatePrediction candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate.QualifiedItemId))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("qualified_item_id", candidate.QualifiedItemId),
                Parameter("quantity", "1")
            };
            if (candidate.Quantity > 1)
            {
                parameters.Add(Parameter("requested_quantity", candidate.Quantity.ToString()));
            }
            if (!string.IsNullOrWhiteSpace(candidate.ItemId))
            {
                parameters.Add(Parameter("shop_item_id", candidate.ItemId));
            }
            if (candidate.UnitPrice > 0)
            {
                parameters.Add(Parameter("max_unit_price", candidate.UnitPrice.ToString()));
            }
            if (!string.IsNullOrWhiteSpace(candidate.ShopId))
            {
                parameters.Add(Parameter("expected_shop_id", candidate.ShopId));
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "buy_shop_item", 0),
                    Kind = "buy_shop_item",
                    EstimatedMinutes = 1,
                    Preconditions = new[] { "shop_menu_open=true", "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.inventory_count_increases", "player.money_decreases" },
                    SafetyConstraints = new[] { "purchase_parameters_from_transparent_shop_stock", "quantity_one_safe_purchase_slice" },
                    FailurePolicy = new[] { "close_menu_refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                },
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "close_shop_menu", 1),
                    Kind = "close_menu",
                    EstimatedMinutes = 1,
                    Preconditions = new[] { "shop_menu_open=true", "candidate_id:" + candidate.CandidateId, "purchase_attempt_completed=true" },
                    ExpectedEffects = new[] { "menus.active_menu.is_open=false" },
                    SafetyConstraints = new[] { "close_only_safe_whitelisted_menu", "post_purchase_menu_cleanup" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> RecoveryRefreshSteps(PolicyEventCandidatePrediction candidate)
        {
            var waitTicks = CandidateInt(candidate, "wait_ticks") ?? candidate.EstimatedTicks;
            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "refresh_wait", 0),
                    Kind = "wait_ticks",
                    WaitTicks = Math.Min(MaxWaitTicksPerStep, Math.Max(1, waitTicks)),
                    EstimatedMinutes = TicksToMinutes(waitTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "wait_only_recovery_candidate" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> RecoveryExecutionSteps(PolicyEventCandidatePrediction candidate)
        {
            return CandidateParameter(candidate, "execution_option_id") switch
            {
                "executor.sleep" => RecoverySleepSteps(candidate),
                "executor.traverse_connector" => RecoveryRouteSteps(candidate),
                _ => Array.Empty<SmallModelPlanStep>()
            };
        }

        private static IEnumerable<SmallModelPlanStep> RecoveryCloseMenuSteps(PolicyEventCandidatePrediction candidate)
        {
            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "close_blocking_menu", 0),
                    Kind = "close_menu",
                    EstimatedMinutes = 1,
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "menus.active_menu.is_open=true" },
                    ExpectedEffects = new[] { "menus.active_menu.is_open=false" },
                    SafetyConstraints = new[] { "close_only_safe_whitelisted_menu", "recovery_menu_close" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> RecoveryRouteSteps(PolicyEventCandidatePrediction candidate)
        {
            var connectorKind = CandidateParameter(candidate, "connector_kind");
            var expectedTargetLocation = CandidateParameter(candidate, "expected_target_location");
            var targetTileX = candidate.TileX ?? CandidateInt(candidate, "target_tile_x");
            var targetTileY = candidate.TileY ?? CandidateInt(candidate, "target_tile_y");
            if (!targetTileX.HasValue ||
                !targetTileY.HasValue ||
                string.IsNullOrWhiteSpace(candidate.LocationId) ||
                string.IsNullOrWhiteSpace(connectorKind) ||
                string.IsNullOrWhiteSpace(expectedTargetLocation))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("connector_kind", connectorKind),
                Parameter("expected_target_location", expectedTargetLocation)
            };
            foreach (var name in new[]
            {
                "expected_arrival_tile_x",
                "expected_arrival_tile_y",
                "max_movement_tiles",
                "estimated_ticks",
                "estimated_minutes",
                "compiler_context.remaining_connector_count"
            })
            {
                var value = CandidateParameter(candidate, name);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    parameters.Add(Parameter(name, value));
                }
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "return_home_connector", 0),
                    Kind = "traverse_connector",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = targetTileX,
                    TargetTileY = targetTileY,
                    EstimatedMinutes = CandidateInt(candidate, "estimated_minutes") ?? TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[]
                    {
                        "candidate_id:" + candidate.CandidateId,
                        "current_location=" + candidate.LocationId,
                        "transparent_connector_still_matches_snapshot=true"
                    },
                    ExpectedEffects = new[]
                    {
                        "player.location_id=" + expectedTargetLocation,
                        "fresh_snapshot_replan_required=true"
                    },
                    SafetyConstraints = new[]
                    {
                        "connector_target_from_transparent_route_graph",
                        "connector_gate_checked_upstream",
                        "no_direct_coordinate_teleport",
                        "one_connector_per_recovery_replan"
                    },
                    FailurePolicy = new[] { "stop_refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> RecoverySleepSteps(PolicyEventCandidatePrediction candidate)
        {
            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "terminal_sleep", 0),
                    Kind = "sleep",
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "player.at_home=true", "bed_reachable=true" },
                    ExpectedEffects = new[] { "day_safely_ended" },
                    SafetyConstraints = new[] { "terminal_sleep_only_via_recovery_candidate" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> WaterCropTileSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "maintain_crops", 0),
                    Kind = "maintain_crops",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "crop_needs_watering=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "target_crop_tile_from_transparent_farm_state" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("max_crops", "1")
                    }
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> HarvestCropTileSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var harvestItemId = ParseValue(candidate.ExpectedEffect, "harvest_item_id=");
            var harvestMethod = ParseValue(candidate.ExpectedEffect, "harvest_method=");
            var parameters = new List<SmallModelActionParameter>();
            if (!string.IsNullOrWhiteSpace(harvestItemId))
            {
                parameters.Add(Parameter("harvest_item_id", harvestItemId));
            }
            if (!string.IsNullOrWhiteSpace(harvestMethod))
            {
                parameters.Add(Parameter("harvest_method", harvestMethod));
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "harvest_crop", 0),
                    Kind = "harvest_crop",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "farm.crops.ready_for_harvest=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "target_crop_tile_from_transparent_farm_state", "runtime_verified_single_tile_harvest" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> HarvestGiantCropTileSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var giantCropId = ParseValue(candidate.ExpectedEffect, "giant_crop_id=");
            var parameters = new List<SmallModelActionParameter>();
            if (!string.IsNullOrWhiteSpace(giantCropId))
            {
                parameters.Add(Parameter("giant_crop_id", giantCropId));
            }
            parameters.Add(Parameter("required_tool", "axe"));

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "harvest_giant_crop", 0),
                    Kind = "harvest_giant_crop",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "farm.resource_clumps.is_giant_crop=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "target_giant_crop_from_transparent_resource_clumps", "runtime_verified_multi_hit_axe_harvest" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> PickupDebrisItemSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var debrisIndex = ParseValue(candidate.ExpectedEffect, "debris_index=");
            var qualifiedItemId = !string.IsNullOrWhiteSpace(candidate.QualifiedItemId)
                ? candidate.QualifiedItemId
                : ParseValue(candidate.ExpectedEffect, "qualified_item_id=");
            var itemId = !string.IsNullOrWhiteSpace(candidate.ItemId)
                ? candidate.ItemId
                : ParseValue(candidate.ExpectedEffect, "item_id=");
            var parameters = new List<SmallModelActionParameter>();
            if (!string.IsNullOrWhiteSpace(debrisIndex))
            {
                parameters.Add(Parameter("debris_index", debrisIndex));
            }
            if (!string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                parameters.Add(Parameter("qualified_item_id", qualifiedItemId));
            }
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                parameters.Add(Parameter("item_id", itemId));
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "move_to_debris", 0),
                    Kind = "move_to_tile",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.tile=" + candidate.TileX.Value + "," + candidate.TileY.Value },
                    SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                },
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "pickup_debris", 1),
                    Kind = "pickup_debris",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = 1,
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "farm.debris.target_exists=true", "player.inventory_can_accept=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "target_debris_from_transparent_farm_state", "runtime_verified_debris_collect" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> CollectMachineOutputSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var steps = new List<SmallModelPlanStep>();
            var standTile = ParseCoordinate(candidate.ExpectedEffect, "move_to_adjacent=");
            if (standTile.HasValue)
            {
                steps.Add(new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "move_to_machine_adjacent", 0),
                    Kind = "move_to_tile",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = standTile.Value.X,
                    TargetTileY = standTile.Value.Y,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.tile=" + standTile.Value.X + "," + standTile.Value.Y },
                    SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                });
            }

            var parameters = new List<SmallModelActionParameter>();
            var qualifiedItemId = !string.IsNullOrWhiteSpace(candidate.QualifiedItemId)
                ? candidate.QualifiedItemId
                : ParseValue(candidate.ExpectedEffect, "qualified_item_id=");
            var itemId = !string.IsNullOrWhiteSpace(candidate.ItemId)
                ? candidate.ItemId
                : ParseValue(candidate.ExpectedEffect, "item_id=");
            if (!string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                parameters.Add(Parameter("qualified_item_id", qualifiedItemId));
            }
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                parameters.Add(Parameter("item_id", itemId));
            }
            if (candidate.Quantity > 0)
            {
                parameters.Add(Parameter("quantity", candidate.Quantity.ToString()));
            }
            AddParsedParameter(parameters, candidate.ExpectedEffect, "output_stack");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "output_sale_price");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "output_total_value");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_value_basis");

            steps.Add(new SmallModelPlanStep
            {
                StepId = StepId(candidate, "collect_machine_output", 1),
                Kind = "collect_machine_output",
                TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 1,
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "farm.machines.target_ready=true", "player.inventory_can_accept=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "target_machine_from_transparent_farm_state", "runtime_verified_machine_output_collect" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = parameters.ToArray()
            });

            return steps;
        }

        private static IEnumerable<SmallModelPlanStep> LoadMachineInputSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var steps = new List<SmallModelPlanStep>();
            var standTile = ParseCoordinate(candidate.ExpectedEffect, "move_to_adjacent=");
            if (standTile.HasValue)
            {
                steps.Add(new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "move_to_machine_adjacent", 0),
                    Kind = "move_to_tile",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = standTile.Value.X,
                    TargetTileY = standTile.Value.Y,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.tile=" + standTile.Value.X + "," + standTile.Value.Y },
                    SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                });
            }

            var slotIndex = candidate.SlotIndex >= 0
                ? candidate.SlotIndex.ToString()
                : ParseValue(candidate.ExpectedEffect, "input_slot_index=");
            var parameters = new List<SmallModelActionParameter>();
            if (!string.IsNullOrWhiteSpace(slotIndex))
            {
                parameters.Add(Parameter("input_slot_index", slotIndex));
            }

            var qualifiedItemId = !string.IsNullOrWhiteSpace(candidate.QualifiedItemId)
                ? candidate.QualifiedItemId
                : ParseValue(candidate.ExpectedEffect, "qualified_item_id=");
            var itemId = !string.IsNullOrWhiteSpace(candidate.ItemId)
                ? candidate.ItemId
                : ParseValue(candidate.ExpectedEffect, "item_id=");
            if (!string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                parameters.Add(Parameter("qualified_item_id", qualifiedItemId));
            }
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                parameters.Add(Parameter("item_id", itemId));
            }
            if (candidate.Quantity > 0)
            {
                parameters.Add(Parameter("input_stack_available", candidate.Quantity.ToString()));
            }
            AddParsedParameter(parameters, candidate.ExpectedEffect, "input_sale_price");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_input_opportunity_cost");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_input_value_basis");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_output_rule_count");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_has_output_rule");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_output_prediction_status");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_qualified_item_id");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_item_id");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_stack");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_sale_price");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_price_source");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_total_value");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_additional_consumed_total_value");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_additional_consumed_items");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_additional_consumed_available");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_net_value");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_rule_required_item_id");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_rule_id");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_preserve_type");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_output_preserved_item_id");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "predicted_minutes_until_ready");
            AddParsedParameter(parameters, candidate.ExpectedEffect, "machine_input_probe_source");

            steps.Add(new SmallModelPlanStep
            {
                StepId = StepId(candidate, "load_machine_input", 1),
                Kind = "load_machine_input",
                TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = 1,
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "farm.machines.target_accepts_input_probe=true", "player.inventory_slot_contains_input=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[] { "target_machine_input_from_transparent_probe", "runtime_verified_machine_input_load" },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = parameters.ToArray()
            });

            return steps;
        }

        private static IEnumerable<SmallModelPlanStep> ClearObstacleTileSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var steps = new List<SmallModelPlanStep>();
            var standTile = ParseCoordinate(candidate.ExpectedEffect, "move_to_adjacent=");
            if (standTile.HasValue)
            {
                steps.Add(new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "move_to_adjacent", 0),
                    Kind = "move_to_tile",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "current_location" : candidate.LocationId,
                    TargetTileX = standTile.Value.X,
                    TargetTileY = standTile.Value.Y,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.tile=" + standTile.Value.X + "," + standTile.Value.Y },
                    SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                });
            }

            steps.Add(
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "clear_obstacle", 1),
                    Kind = "clear_obstacle",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "current_location" : candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "target_obstacle_clearable=true", "target_tile_adjacent=true" },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "target_obstacle_from_transparent_location_state", "executor_requires_adjacent_target" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("max_tool_swings", "8")
                    }
                });

            return steps;
        }

        private static IEnumerable<SmallModelPlanStep> PlantSeedTileSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var seedId = !string.IsNullOrWhiteSpace(candidate.ItemId)
                ? candidate.ItemId
                : ParseValue(candidate.ExpectedEffect, "seed_id=");
            if (string.IsNullOrWhiteSpace(seedId))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var parameters = new List<SmallModelActionParameter>
            {
                Parameter("seed_id", seedId)
            };
            if (!string.IsNullOrWhiteSpace(candidate.QualifiedItemId))
            {
                parameters.Add(Parameter("qualified_item_id", candidate.QualifiedItemId));
            }
            if (candidate.SlotIndex.HasValue)
            {
                parameters.Add(Parameter("slot_index", candidate.SlotIndex.Value.ToString()));
            }
            if (candidate.Quantity > 0)
            {
                parameters.Add(Parameter("seed_stack_available", candidate.Quantity.ToString()));
            }
            var adjustedGrowDays = ParseValue(candidate.ExpectedEffect, "adjusted_grow_days=");
            var daysRemaining = ParseValue(candidate.ExpectedEffect, "days_remaining_in_season=");
            var harvestItemId = ParseValue(candidate.ExpectedEffect, "harvest_item_id=");
            var harvestItemQualifiedId = ParseValue(candidate.ExpectedEffect, "harvest_item_qualified_id=");
            var harvestUnitSalePrice = ParseValue(candidate.ExpectedEffect, "harvest_unit_sale_price=");
            var harvestMinStack = ParseValue(candidate.ExpectedEffect, "harvest_min_stack=");
            var harvestMaxStack = ParseValue(candidate.ExpectedEffect, "harvest_max_stack=");
            var harvestMaxIncreasePerFarmingLevel = ParseValue(candidate.ExpectedEffect, "harvest_max_increase_per_farming_level=");
            var extraHarvestChance = ParseValue(candidate.ExpectedEffect, "extra_harvest_chance=");
            var harvestMinQuality = ParseValue(candidate.ExpectedEffect, "harvest_min_quality=");
            var harvestMaxQuality = ParseValue(candidate.ExpectedEffect, "harvest_max_quality=");
            var harvestMethod = ParseValue(candidate.ExpectedEffect, "harvest_method=");
            var regrowDays = ParseValue(candidate.ExpectedEffect, "regrow_days=");
            var expectedFirstHarvestValue = ParseValue(candidate.ExpectedEffect, "expected_first_harvest_value=");
            var expectedFirstHarvestQuantity = ParseValue(candidate.ExpectedEffect, "expected_first_harvest_quantity=");
            var expectedFirstHarvestValueBasis = ParseValue(candidate.ExpectedEffect, "expected_first_harvest_value_basis=");
            var estimatedFirstHarvestQuantity = ParseValue(candidate.ExpectedEffect, "estimated_first_harvest_quantity=");
            var estimatedFirstHarvestValue = ParseValue(candidate.ExpectedEffect, "estimated_first_harvest_value=");
            var estimatedFirstHarvestValueBasis = ParseValue(candidate.ExpectedEffect, "estimated_first_harvest_value_basis=");
            var estimatedRegrowHarvestCount = ParseValue(candidate.ExpectedEffect, "estimated_regrow_harvest_count=");
            var estimatedTotalHarvestCount = ParseValue(candidate.ExpectedEffect, "estimated_total_harvest_count=");
            var expectedSeasonHarvestValue = ParseValue(candidate.ExpectedEffect, "expected_season_harvest_value=");
            var estimatedSeasonHarvestValue = ParseValue(candidate.ExpectedEffect, "estimated_season_harvest_value=");
            var seedUnitCost = ParseValue(candidate.ExpectedEffect, "seed_unit_cost=");
            var expectedFirstHarvestNetValue = ParseValue(candidate.ExpectedEffect, "expected_first_harvest_net_value=");
            var estimatedFirstHarvestNetValue = ParseValue(candidate.ExpectedEffect, "estimated_first_harvest_net_value=");
            var expectedSeasonHarvestNetValue = ParseValue(candidate.ExpectedEffect, "expected_season_harvest_net_value=");
            var estimatedSeasonHarvestNetValue = ParseValue(candidate.ExpectedEffect, "estimated_season_harvest_net_value=");
            var seasonHarvestValueBasis = ParseValue(candidate.ExpectedEffect, "season_harvest_value_basis=");
            var regrowEstimateBasis = ParseValue(candidate.ExpectedEffect, "regrow_estimate_basis=");
            var netValueBasis = ParseValue(candidate.ExpectedEffect, "net_value_basis=");
            if (!string.IsNullOrWhiteSpace(adjustedGrowDays))
            {
                parameters.Add(Parameter("adjusted_grow_days", adjustedGrowDays));
            }
            if (!string.IsNullOrWhiteSpace(daysRemaining))
            {
                parameters.Add(Parameter("days_remaining_in_season", daysRemaining));
            }
            if (int.TryParse(adjustedGrowDays, out var growDays) &&
                int.TryParse(daysRemaining, out var remainingDays))
            {
                parameters.Add(Parameter("maturity_slack_days", (remainingDays - growDays).ToString()));
            }
            if (!string.IsNullOrWhiteSpace(harvestItemId))
            {
                parameters.Add(Parameter("harvest_item_id", harvestItemId));
            }
            if (!string.IsNullOrWhiteSpace(harvestItemQualifiedId))
            {
                parameters.Add(Parameter("harvest_item_qualified_id", harvestItemQualifiedId));
            }
            if (!string.IsNullOrWhiteSpace(harvestUnitSalePrice))
            {
                parameters.Add(Parameter("harvest_unit_sale_price", harvestUnitSalePrice));
            }
            if (!string.IsNullOrWhiteSpace(harvestMinStack))
            {
                parameters.Add(Parameter("harvest_min_stack", harvestMinStack));
            }
            if (!string.IsNullOrWhiteSpace(harvestMaxStack))
            {
                parameters.Add(Parameter("harvest_max_stack", harvestMaxStack));
            }
            if (!string.IsNullOrWhiteSpace(harvestMaxIncreasePerFarmingLevel))
            {
                parameters.Add(Parameter("harvest_max_increase_per_farming_level", harvestMaxIncreasePerFarmingLevel));
            }
            if (!string.IsNullOrWhiteSpace(extraHarvestChance))
            {
                parameters.Add(Parameter("extra_harvest_chance", extraHarvestChance));
            }
            if (!string.IsNullOrWhiteSpace(harvestMinQuality))
            {
                parameters.Add(Parameter("harvest_min_quality", harvestMinQuality));
            }
            if (!string.IsNullOrWhiteSpace(harvestMaxQuality))
            {
                parameters.Add(Parameter("harvest_max_quality", harvestMaxQuality));
            }
            if (!string.IsNullOrWhiteSpace(harvestMethod))
            {
                parameters.Add(Parameter("harvest_method", harvestMethod));
            }
            if (!string.IsNullOrWhiteSpace(regrowDays))
            {
                parameters.Add(Parameter("regrow_days", regrowDays));
            }
            if (!string.IsNullOrWhiteSpace(expectedFirstHarvestValue))
            {
                parameters.Add(Parameter("expected_first_harvest_value", expectedFirstHarvestValue));
            }
            if (!string.IsNullOrWhiteSpace(expectedFirstHarvestQuantity))
            {
                parameters.Add(Parameter("expected_first_harvest_quantity", expectedFirstHarvestQuantity));
            }
            if (!string.IsNullOrWhiteSpace(expectedFirstHarvestValueBasis))
            {
                parameters.Add(Parameter("expected_first_harvest_value_basis", expectedFirstHarvestValueBasis));
            }
            if (!string.IsNullOrWhiteSpace(estimatedFirstHarvestQuantity))
            {
                parameters.Add(Parameter("estimated_first_harvest_quantity", estimatedFirstHarvestQuantity));
            }
            if (!string.IsNullOrWhiteSpace(estimatedFirstHarvestValue))
            {
                parameters.Add(Parameter("estimated_first_harvest_value", estimatedFirstHarvestValue));
            }
            if (!string.IsNullOrWhiteSpace(estimatedFirstHarvestValueBasis))
            {
                parameters.Add(Parameter("estimated_first_harvest_value_basis", estimatedFirstHarvestValueBasis));
            }
            if (!string.IsNullOrWhiteSpace(estimatedRegrowHarvestCount))
            {
                parameters.Add(Parameter("estimated_regrow_harvest_count", estimatedRegrowHarvestCount));
            }
            if (!string.IsNullOrWhiteSpace(estimatedTotalHarvestCount))
            {
                parameters.Add(Parameter("estimated_total_harvest_count", estimatedTotalHarvestCount));
            }
            if (!string.IsNullOrWhiteSpace(expectedSeasonHarvestValue))
            {
                parameters.Add(Parameter("expected_season_harvest_value", expectedSeasonHarvestValue));
            }
            if (!string.IsNullOrWhiteSpace(estimatedSeasonHarvestValue))
            {
                parameters.Add(Parameter("estimated_season_harvest_value", estimatedSeasonHarvestValue));
            }
            if (!string.IsNullOrWhiteSpace(seedUnitCost))
            {
                parameters.Add(Parameter("seed_unit_cost", seedUnitCost));
            }
            if (!string.IsNullOrWhiteSpace(expectedFirstHarvestNetValue))
            {
                parameters.Add(Parameter("expected_first_harvest_net_value", expectedFirstHarvestNetValue));
            }
            if (!string.IsNullOrWhiteSpace(estimatedFirstHarvestNetValue))
            {
                parameters.Add(Parameter("estimated_first_harvest_net_value", estimatedFirstHarvestNetValue));
            }
            if (!string.IsNullOrWhiteSpace(expectedSeasonHarvestNetValue))
            {
                parameters.Add(Parameter("expected_season_harvest_net_value", expectedSeasonHarvestNetValue));
            }
            if (!string.IsNullOrWhiteSpace(estimatedSeasonHarvestNetValue))
            {
                parameters.Add(Parameter("estimated_season_harvest_net_value", estimatedSeasonHarvestNetValue));
            }
            if (!string.IsNullOrWhiteSpace(seasonHarvestValueBasis))
            {
                parameters.Add(Parameter("season_harvest_value_basis", seasonHarvestValueBasis));
            }
            if (!string.IsNullOrWhiteSpace(regrowEstimateBasis))
            {
                parameters.Add(Parameter("regrow_estimate_basis", regrowEstimateBasis));
            }
            if (!string.IsNullOrWhiteSpace(netValueBasis))
            {
                parameters.Add(Parameter("net_value_basis", netValueBasis));
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "plant_seed", 0),
                    Kind = "plant_seed",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "current_location" : candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "hard_rule_allows_planting=true", "seed_inventory_contains=" + seedId },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[] { "target_seed_tile_from_transparent_planting_context", "single_tile_single_seed_slice", "maturity_timing_from_transparent_planting_context", "harvest_value_from_transparent_crop_catalog_when_present" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };
        }

        private static IEnumerable<SmallModelPlanStep> ShipInventoryItemToBinSteps(PolicyEventCandidatePrediction candidate)
        {
            var slotIndexStr = CandidateParameter(candidate, "slot_index");
            var itemId = !string.IsNullOrWhiteSpace(candidate.ItemId)
                ? candidate.ItemId
                : CandidateParameter(candidate, "item_id");
            var qualifiedItemId = !string.IsNullOrWhiteSpace(candidate.QualifiedItemId)
                ? candidate.QualifiedItemId
                : CandidateParameter(candidate, "qualified_item_id");
            var quantity = candidate.Quantity > 0
                ? candidate.Quantity.ToString()
                : CandidateParameter(candidate, "quantity");

            if (!int.TryParse(slotIndexStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var slotIndex))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var steps = new List<SmallModelPlanStep>();
            var standTile = ParseCoordinate(candidate.ExpectedEffect, "route_stand_tile=");
            if (standTile.HasValue)
            {
                steps.Add(new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "move_to_shipping_bin", 0),
                    Kind = "move_to_tile",
                    TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                    TargetTileX = standTile.Value.X,
                    TargetTileY = standTile.Value.Y,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.tile=" + standTile.Value.X + "," + standTile.Value.Y },
                    SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                });
            }

            var binTile = ParseCoordinate(candidate.ExpectedEffect, "shipping_bin_tile=");
            var parameters = new List<SmallModelActionParameter>();
            if (!string.IsNullOrWhiteSpace(slotIndexStr))
            {
                parameters.Add(Parameter("slot_index", slotIndexStr));
            }
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                parameters.Add(Parameter("item_id", itemId));
            }
            if (!string.IsNullOrWhiteSpace(qualifiedItemId))
            {
                parameters.Add(Parameter("qualified_item_id", qualifiedItemId));
            }
            if (!string.IsNullOrWhiteSpace(quantity))
            {
                parameters.Add(Parameter("quantity", quantity));
            }

            var routeStandTileX = CandidateParameter(candidate, "route_stand_tile_x");
            var routeStandTileY = CandidateParameter(candidate, "route_stand_tile_y");
            if (!string.IsNullOrWhiteSpace(routeStandTileX))
            {
                parameters.Add(Parameter("stand_tile_x", routeStandTileX));
            }
            if (!string.IsNullOrWhiteSpace(routeStandTileY))
            {
                parameters.Add(Parameter("stand_tile_y", routeStandTileY));
            }

            steps.Add(new SmallModelPlanStep
            {
                StepId = StepId(candidate, "ship_inventory_item_to_bin", standTile.HasValue ? 1 : 0),
                Kind = "ship_inventory_item_to_bin",
                TargetLocation = string.IsNullOrWhiteSpace(candidate.LocationId) ? "Farm" : candidate.LocationId,
                TargetTileX = binTile?.X,
                TargetTileY = binTile?.Y,
                EstimatedMinutes = 1,
                Preconditions = new[]
                {
                    "candidate_id:" + candidate.CandidateId,
                    "shipping_bin_completed=true",
                    "inventory_slot_contains_item=" + slotIndex
                },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "target_item_from_transparent_inventory_state",
                    "shipping_bin_from_transparent_farm_state",
                    "never_ship_protected_items"
                },
                FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                Parameters = parameters.ToArray()
            });

            return steps;
        }

        private static IEnumerable<SmallModelPlanStep> SocialInteractionSteps(PolicyEventCandidatePrediction candidate)
        {
            var npcName = CandidateParameter(candidate, "npc_name");
            var standTileXStr = CandidateParameter(candidate, "stand_tile_x");
            var standTileYStr = CandidateParameter(candidate, "stand_tile_y");
            var npcTileXStr = CandidateParameter(candidate, "npc_tile_x");
            var npcTileYStr = CandidateParameter(candidate, "npc_tile_y");
            if (string.IsNullOrWhiteSpace(npcName) ||
                !int.TryParse(standTileXStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var standTileX) ||
                !int.TryParse(standTileYStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var standTileY) ||
                !int.TryParse(npcTileXStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var npcTileX) ||
                !int.TryParse(npcTileYStr, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var npcTileY))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var routeDistance = CandidateInt(candidate, "route_distance_tiles") ?? 0;
            var actionKind = candidate.Kind == "social_talk_current" ? "talk" : "gift";
            var parameters = new List<SmallModelActionParameter>(candidate.Parameters)
            {
                Parameter("social_action_kind", actionKind)
            };
            var steps = new List<SmallModelPlanStep>
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "move_to_social_stand", 0),
                    Kind = "move_to_tile",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = standTileX,
                    TargetTileY = standTileY,
                    EstimatedMinutes = Math.Max(1, (int)Math.Ceiling(routeDistance / 5d)),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.tile=" + standTileX + "," + standTileY },
                    SafetyConstraints = new[] { "collision_checked_by_action_queue_compiler", "no_direct_coordinate_teleport" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("max_movement_tiles", Math.Max(1, routeDistance).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    }
                },
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "social_interact", 1),
                    Kind = "social_interact",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = npcTileX,
                    TargetTileY = npcTileY,
                    EstimatedMinutes = 1,
                    Preconditions = new[]
                    {
                        "candidate_id:" + candidate.CandidateId,
                        "player_adjacent_to_npc_stand_tile=" + standTileX + "," + standTileY
                    },
                    ExpectedEffects = new[] { candidate.ExpectedEffect },
                    SafetyConstraints = new[]
                    {
                        "social_npc_from_transparent_current_state",
                        "social_adjacent_checked_by_move_to_tile_predecessor"
                    },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = parameters.ToArray()
                }
            };

            return steps;
        }

        private static IEnumerable<SmallModelPlanStep> RouteConnectorSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.TileX.HasValue || !candidate.TileY.HasValue)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            var expectedTargetLocation = ParseValue(candidate.ExpectedEffect, "expected_target_location=");
            if (string.IsNullOrWhiteSpace(expectedTargetLocation))
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "traverse_connector", 0),
                    Kind = "traverse_connector",
                    TargetLocation = candidate.LocationId,
                    TargetTileX = candidate.TileX,
                    TargetTileY = candidate.TileY,
                    EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                    Preconditions = new[] { "candidate_id:" + candidate.CandidateId },
                    ExpectedEffects = new[] { "player.location_id=" + expectedTargetLocation },
                    SafetyConstraints = new[] { "connector_target_from_transparent_route_graph" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("expected_target_location", expectedTargetLocation)
                    }
                }
            };
        }

        private static string StepId(PolicyEventCandidatePrediction candidate, string suffix, int index)
        {
            return Sanitize(candidate.CandidateId) + "." + suffix + "." + index;
        }

        private static string Sanitize(string value)
        {
            var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
            var sanitized = new string(chars);
            return string.IsNullOrWhiteSpace(sanitized) ? "candidate" : sanitized;
        }

        private static int TicksToMinutes(int ticks)
        {
            return Math.Max(1, (int)Math.Ceiling(Math.Max(1, ticks) / 60.0));
        }

        private static string CandidateParameter(PolicyEventCandidatePrediction candidate, string name)
        {
            return candidate.Parameters.FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))?.Value
                ?? string.Empty;
        }

        private static int? CandidateInt(PolicyEventCandidatePrediction candidate, string name)
        {
            return int.TryParse(CandidateParameter(candidate, name), out var value) ? value : null;
        }

        private static SmallModelActionParameter Parameter(string name, string value)
        {
            return new SmallModelActionParameter { Name = name, Value = value };
        }

        private static void AddParsedParameter(List<SmallModelActionParameter> parameters, string expectedEffect, string name)
        {
            var value = ParseValue(expectedEffect, name + "=");
            if (!string.IsNullOrWhiteSpace(value))
            {
                parameters.Add(Parameter(name, value));
            }
        }

        private static (int X, int Y)? ParseCoordinate(string source, string prefix)
        {
            var value = ParseValue(source, prefix);
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var parts = value.Split(',');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0], out var x) ||
                !int.TryParse(parts[1], out var y))
            {
                return null;
            }

            return (x, y);
        }

        private static string ParseValue(string source, string prefix)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return string.Empty;
            }

            foreach (var segment in source.Split(';'))
            {
                if (segment.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return segment.Substring(prefix.Length);
                }
            }

            return string.Empty;
        }
    }
}
