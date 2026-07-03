using System;
using StardewAI.Contracts.Goals;

namespace StardewAI.Core.GoalCompiler
{
    public sealed class GoalCompiler
    {
        public GoalSpec Compile(string naturalLanguage, string mode)
        {
            var text = naturalLanguage ?? string.Empty;
            return new GoalSpec
            {
                GoalId = "goal." + Guid.NewGuid().ToString("N"),
                RawText = text,
                Mode = string.IsNullOrWhiteSpace(mode) ? "relaxed" : mode,
                Intent = ClassifyIntent(text)
            };
        }

        private static string ClassifyIntent(string text)
        {
            var lowered = text.ToLowerInvariant();
            if (ContainsAny(lowered, "crop", "water", "harvest", "farm", "作物", "浇水", "收菜", "农场"))
            {
                return "farm.maintain_crops";
            }

            if (ContainsAny(lowered, "machine", "keg", "jar", "process", "机器", "加工"))
            {
                return "farm.process_machines";
            }

            if (ContainsAny(lowered, "buy", "seed", "shop", "pierre", "购买", "买", "种子", "商店"))
            {
                return "economy.buy_supplies";
            }

            if (ContainsAny(lowered, "sell", "ship", "出售", "卖", "出货"))
            {
                return "economy.sell_items";
            }

            if (ContainsAny(lowered, "gift", "npc", "social", "friend", "送礼", "社交", "好感"))
            {
                return "social.gift_npc";
            }

            if (ContainsAny(lowered, "quest", "bundle", "任务", "献祭"))
            {
                return "quest.advance";
            }

            if (ContainsAny(lowered, "mine", "explore", "visit", "探索", "下矿", "去"))
            {
                return "exploration.visit_location";
            }

            return "recovery.stabilize_day";
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
