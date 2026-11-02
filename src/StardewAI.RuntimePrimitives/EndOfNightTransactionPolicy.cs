using System;

namespace StardewAI.RuntimePrimitives
{
    public static class EndOfNightTransactionPolicy
    {
        public const int MaximumOverallTicks = 36000;

        public static int TransitionTickBudget(string transitionKey)
        {
            if (transitionKey.StartsWith("farm_event:", StringComparison.Ordinal) ||
                transitionKey.StartsWith("story_event:", StringComparison.Ordinal) ||
                transitionKey.StartsWith("minigame:", StringComparison.Ordinal))
            {
                return 7200;
            }
            if (transitionKey.StartsWith("menu:ShippingMenu", StringComparison.Ordinal) ||
                transitionKey.StartsWith("menu:SaveGameMenu", StringComparison.Ordinal) ||
                string.Equals(transitionKey, "native_save_processing", StringComparison.Ordinal))
            {
                return 3600;
            }
            if (transitionKey.StartsWith("menu:LevelUpMenu", StringComparison.Ordinal) ||
                transitionKey.StartsWith("menu:DialogueBox", StringComparison.Ordinal))
            {
                return 1800;
            }
            if (transitionKey.StartsWith("menu:", StringComparison.Ordinal))
            {
                return 900;
            }
            return 2400;
        }

        public static bool IsSafeNextDayExecutionOption(string optionId)
        {
            return !string.IsNullOrWhiteSpace(optionId) &&
                optionId.StartsWith("executor.", StringComparison.Ordinal) &&
                !string.Equals(optionId, "executor.advance_story_event", StringComparison.Ordinal) &&
                !string.Equals(optionId, "executor.advance_story_event_minigame", StringComparison.Ordinal) &&
                !string.Equals(optionId, "executor.choose_dialogue_response", StringComparison.Ordinal) &&
                !string.Equals(optionId, "executor.close_menu", StringComparison.Ordinal) &&
                !string.Equals(optionId, "executor.close_shipping_summary", StringComparison.Ordinal) &&
                !string.Equals(optionId, "executor.face_direction", StringComparison.Ordinal) &&
                !string.Equals(optionId, "executor.select_safe_item_slot", StringComparison.Ordinal) &&
                !string.Equals(optionId, "executor.sleep", StringComparison.Ordinal) &&
                !string.Equals(optionId, "executor.wait", StringComparison.Ordinal) &&
                !string.Equals(optionId, "executor.wait_ticks", StringComparison.Ordinal);
        }

        public static bool IsSafeNextDayPlanningOption(string optionId)
        {
            return !string.IsNullOrWhiteSpace(optionId) &&
                !optionId.StartsWith("debug.", StringComparison.Ordinal) &&
                !optionId.StartsWith("executor.", StringComparison.Ordinal) &&
                !optionId.StartsWith("recovery.", StringComparison.Ordinal) &&
                !optionId.StartsWith("story.", StringComparison.Ordinal) &&
                !optionId.StartsWith("minigame.", StringComparison.Ordinal) &&
                !string.Equals(
                    optionId,
                    "skills.choose_profession",
                    StringComparison.Ordinal);
        }
    }
}
