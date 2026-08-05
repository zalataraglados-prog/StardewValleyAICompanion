using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> SellShopItemSteps(PolicyEventCandidatePrediction candidate)
        {
            if (!candidate.SlotIndex.HasValue ||
                candidate.SlotIndex.Value < 0 ||
                string.IsNullOrWhiteSpace(candidate.QualifiedItemId) ||
                string.IsNullOrWhiteSpace(candidate.ShopId) ||
                candidate.Quantity <= 0 ||
                candidate.UnitPrice <= 0 ||
                !candidate.CanShopSell)
            {
                return Array.Empty<SmallModelPlanStep>();
            }

            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "sell_shop_item", 0),
                    Kind = "sell_shop_item",
                    EstimatedMinutes = 1,
                    Preconditions = new[]
                    {
                        "shop_menu_open=true",
                        "candidate_id:" + candidate.CandidateId,
                        "inventory_stack_identity_unchanged=true"
                    },
                    ExpectedEffects = new[]
                    {
                        "player.inventory_count_decreases",
                        "player.money_increases"
                    },
                    SafetyConstraints = new[]
                    {
                        "never_sell_protected_items",
                        "native_shop_menu_click_only",
                        "exact_stack_and_price_recheck_before_click"
                    },
                    FailurePolicy = new[] { "close_menu_refresh_snapshot_and_replan" },
                    Parameters = new[]
                    {
                        Parameter("slot_index", candidate.SlotIndex.Value.ToString()),
                        Parameter("item_id", candidate.ItemId),
                        Parameter("qualified_item_id", candidate.QualifiedItemId),
                        Parameter("quantity", candidate.Quantity.ToString()),
                        Parameter("expected_unit_price", candidate.UnitPrice.ToString()),
                        Parameter("expected_total_value", candidate.TotalValue.ToString()),
                        Parameter("expected_shop_id", candidate.ShopId)
                    }
                    .Concat(candidate.Parameters.Where(parameter =>
                        parameter.Name.StartsWith("continuation.", StringComparison.Ordinal)))
                    .ToArray()
                },
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "close_shop_menu", 1),
                    Kind = "close_menu",
                    EstimatedMinutes = 1,
                    Preconditions = new[]
                    {
                        "shop_menu_open=true",
                        "candidate_id:" + candidate.CandidateId,
                        "sale_attempt_completed=true"
                    },
                    ExpectedEffects = new[] { "menus.active_menu.is_open=false" },
                    SafetyConstraints = new[] { "close_only_safe_whitelisted_menu", "post_sale_menu_cleanup" },
                    FailurePolicy = new[] { "refresh_snapshot_and_replan" }
                }
            };
        }
    }
}
