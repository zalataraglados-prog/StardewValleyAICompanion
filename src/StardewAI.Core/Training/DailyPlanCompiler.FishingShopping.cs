using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
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
                    EstimatedMinutes = Execution.GameClockBudgetPolicy.MovementTilesToGameMinutes(routeDistance),
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
            var continuation = ShopObjectiveContinuation(candidate);
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
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" },
                    Parameters = continuation
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
                    .Concat(continuation)
                    .ToArray()
            });
            var isAnimalPurchase = string.Equals(
                continuation.FirstOrDefault(parameter => string.Equals(parameter.Name, "continuation.option_id", StringComparison.Ordinal))?.Value,
                "animals.purchase",
                StringComparison.Ordinal);
            if (isAnimalPurchase)
            {
                return steps;
            }
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
                        .Concat(continuation)
                        .ToArray()
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

            if (string.Equals(expectedActionType, "Marnie", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(expectedActionType, "AnimalShop", StringComparison.OrdinalIgnoreCase))
            {
                return new DialogueShopResponseSpec("Marnie", "Supplies", string.IsNullOrWhiteSpace(shopId) ? "AnimalShop" : shopId);
            }

            if (string.Equals(expectedActionType, "AdventureGuild", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(expectedActionType, "adventureGuild", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(expectedActionType, "AdventureShop", StringComparison.OrdinalIgnoreCase))
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
            parameters.AddRange(ShopObjectiveContinuation(candidate));

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

        private static SmallModelActionParameter[] ShopObjectiveContinuation(
            PolicyEventCandidatePrediction candidate)
        {
            return candidate.Parameters
                .Where(parameter => parameter.Name.StartsWith(
                    "continuation.",
                    StringComparison.Ordinal))
                .ToArray();
        }

    }
}
