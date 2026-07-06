using System;
using StardewAI.Contracts.Execution;

namespace StardewAI.Core.MockModel
{
    public sealed class TaskIntentClassification
    {
        public string Category { get; set; } = TaskIntentCategory.Recovery;

        public string OptionId { get; set; } = "recovery.stabilize_day";

        public SmallModelActionParameter[] Parameters { get; set; } = Array.Empty<SmallModelActionParameter>();
    }

    public static class TaskIntentCategory
    {
        public const string Mechanical = "mechanical";
        public const string ParameterizedMechanical = "parameterized_mechanical";
        public const string SpatialPlanning = "spatial_planning";
        public const string EconomicStrategic = "economic_strategic";
        public const string Recovery = "recovery";
    }

    public sealed class TaskIntentClassifier
    {
        public TaskIntentClassification Classify(string goal)
        {
            var text = (goal ?? string.Empty).ToLowerInvariant();
            if (ContainsAny(text, "mine", "mining", "level", "floor", "矿", "层"))
            {
                return new TaskIntentClassification
                {
                    Category = TaskIntentCategory.ParameterizedMechanical,
                    OptionId = "exploration.visit_location",
                    Parameters = new[]
                    {
                        Parameter("target_activity", "mining"),
                        Parameter("target_depth", ExtractFirstInteger(text) ?? "unknown")
                    }
                };
            }

            if (ContainsAny(text, "fish", "fishing", "catch", "钓鱼", "鱼"))
            {
                return new TaskIntentClassification
                {
                    Category = TaskIntentCategory.ParameterizedMechanical,
                    OptionId = "exploration.visit_location",
                    Parameters = new[]
                    {
                        Parameter("target_activity", "fishing"),
                        Parameter("target_catches", ExtractFirstInteger(text) ?? "1")
                    }
                };
            }

            if (ContainsAny(text, "clear", "tree", "plant tree", "layout", "sprinkler", "building", "开荒", "种树", "布局", "洒水器", "建筑"))
            {
                return new TaskIntentClassification
                {
                    Category = TaskIntentCategory.SpatialPlanning,
                    OptionId = "exploration.visit_location",
                    Parameters = new[]
                    {
                        Parameter("requires_position_plan", "true")
                    }
                };
            }

            if (ContainsAny(text, "grandpa", "four_candles", "four candles", "candles", "grandpa_four_candles_year3", "爷爷"))
            {
                return new TaskIntentClassification
                {
                    Category = TaskIntentCategory.EconomicStrategic,
                    OptionId = "strategy.grandpa_progress",
                    Parameters = new[]
                    {
                        Parameter("strategic_goal", "grandpa_four_candles_year3"),
                        Parameter("target_score", "12"),
                        Parameter("requires_direction_selection", "true")
                    }
                };
            }

            if (ContainsAny(text, "buy", "sell", "shop", "gift", "npc", "quest", "bundle", "购买", "卖", "送礼", "任务"))
            {
                return new TaskIntentClassification
                {
                    Category = TaskIntentCategory.EconomicStrategic,
                    OptionId = SelectEconomicOption(text),
                    Parameters = new[]
                    {
                        Parameter("requires_detailed_plan", "true")
                    }
                };
            }

            if (ContainsAny(text, "crop", "water", "farm", "machine", "keg", "jar", "耕地", "浇水", "作物", "机器"))
            {
                return new TaskIntentClassification
                {
                    Category = TaskIntentCategory.Mechanical,
                    OptionId = ContainsAny(text, "machine", "keg", "jar", "机器")
                        ? "farm.process_machines"
                        : "farm.maintain_crops",
                    Parameters = Array.Empty<SmallModelActionParameter>()
                };
            }

            return new TaskIntentClassification
            {
                Category = TaskIntentCategory.Recovery,
                OptionId = "recovery.stabilize_day",
                Parameters = new[]
                {
                    Parameter("fallback_reason", "unclassified_goal")
                }
            };
        }

        private static string SelectEconomicOption(string text)
        {
            if (ContainsAny(text, "buy", "shop", "seed", "购买", "种子"))
            {
                return "economy.buy_supplies";
            }

            if (ContainsAny(text, "sell", "ship", "卖", "出售", "出货"))
            {
                return "economy.sell_items";
            }

            if (ContainsAny(text, "gift", "npc", "friend", "送礼", "好感"))
            {
                return "social.gift_npc";
            }

            return "quest.advance";
        }

        private static SmallModelActionParameter Parameter(string name, string value)
        {
            return new SmallModelActionParameter
            {
                Name = name,
                Value = value
            };
        }

        private static string? ExtractFirstInteger(string text)
        {
            var start = -1;
            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsDigit(text[i]))
                {
                    start = i;
                    break;
                }
            }

            if (start < 0)
            {
                return null;
            }

            var end = start;
            while (end < text.Length && char.IsDigit(text[end]))
            {
                end++;
            }

            return text.Substring(start, end - start);
        }

        private static bool ContainsAny(string text, params string[] tokens)
        {
            foreach (var token in tokens)
            {
                if (text.Contains(token))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
