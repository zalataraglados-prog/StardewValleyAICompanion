using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.Core.Verifier;
using StardewAI.Core.WorldModel;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static string[] ValidateFaceDirectionPlan(SmallModelAction action)
        {
            if (action.OptionId != "executor.face_direction")
            {
                return Array.Empty<string>();
            }

            var direction = ReadIntParameter(action, "direction");
            return direction is >= 0 and <= 3 ? Array.Empty<string>() : new[] { "direction_0_3_required" };
        }

        private static string[] ValidateRecoveryPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "recovery.stabilize_day")
            {
                return Array.Empty<string>();
            }

            if (Infrastructure.SleepPromptResumeProjection.IsAvailable(
                    snapshot))
            {
                return Array.Empty<string>();
            }

            var time = ReadStateFieldInt(snapshot, "time", "time");
            if (!GameClockBudgetPolicy.RecoveryWindowStarted(time))
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                reasons.Add("sleep_prompt_menu_must_be_clear");
            }

            var prompt = ReadStateFieldValue(snapshot, "menus", "sleep_prompt_context");
            if (prompt.HasValue && prompt.Value.ValueKind == JsonValueKind.Object && ReadBool(prompt.Value, "prompt_open"))
            {
                reasons.Add("sleep_confirm_executor_requires_compiler_terminal_macro");
            }

            if (SleepTarget(snapshot) is null)
            {
                reasons.AddRange(BuildRecoveryRoutePlan(snapshot).BlockReasons);
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateWaitTicksPlan(SmallModelAction action)
        {
            if (action.OptionId != "executor.wait_ticks")
            {
                return Array.Empty<string>();
            }

            var waitTicks = ReadIntParameter(action, "wait_ticks");
            return waitTicks is >= 1 and <= 600 ? Array.Empty<string>() : new[] { "wait_ticks_1_600_required" };
        }

        private static string[] ValidateSelectSafeItemSlotPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.select_safe_item_slot")
            {
                return Array.Empty<string>();
            }

            var safeSlot = ReadIntParameter(action, "safe_slot_index") ?? SafeSlotIndex(snapshot);
            if (!safeSlot.HasValue)
            {
                return new[] { "safe_item_slot_unavailable" };
            }

            return safeSlot.Value is >= 0 and <= 11
                ? Array.Empty<string>()
                : new[] { "safe_slot_index_0_11_required" };
        }

        private static string[] ValidateCloseMenuPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.close_menu")
            {
                return Array.Empty<string>();
            }

            var prompt = ReadStateFieldValue(snapshot, "menus", "sleep_prompt_context");
            if (prompt.HasValue && prompt.Value.ValueKind == JsonValueKind.Object && ReadBool(prompt.Value, "prompt_open"))
            {
                return new[] { "close_menu_sleep_prompt_unsupported" };
            }

            if (!ActiveMenuOpen(snapshot))
            {
                return Array.Empty<string>();
            }

            var type = ActiveMenuType(snapshot);
            if (string.IsNullOrWhiteSpace(type))
            {
                return new[] { "close_menu_type_unknown" };
            }

            if (type == "LetterViewerMenu")
            {
                return ValidateLetterViewerMenuPlan(action, snapshot);
            }

            if (type == "MineElevatorMenu")
            {
                return ValidateMineElevatorMenuPlan(action, snapshot);
            }

            if (IsSafeCloseMenuType(type))
            {
                return Array.Empty<string>();
            }

            if (type == "LevelUpMenu")
            {
                return ValidateLevelUpMenuPlan(action, snapshot);
            }

            if (type == "DialogueBox")
            {
                var dialogueReasons = SafeOrdinaryDialogueBlockReasons(snapshot);
                if (Infrastructure.IncubatorSnapshotProjection
                    .IsBirthMessage(snapshot))
                {
                    dialogueReasons = dialogueReasons
                        .Where(reason =>
                            reason !=
                                "dialogue_close_event_up_true" &&
                            reason !=
                                "dialogue_close_character_present_false" &&
                            reason !=
                                "dialogue_close_speaker_name_empty")
                        .ToArray();
                }
                if (string.Equals(ReadParameter(action, "social_continuation_dialogue_recovery"), "true", StringComparison.OrdinalIgnoreCase))
                {
                    dialogueReasons = dialogueReasons
                        .Where(reason => reason != "dialogue_close_character_present_false" &&
                            reason != "dialogue_close_speaker_name_empty")
                        .ToArray();
                }
                return dialogueReasons;
            }

            if (type == "ShippingMenu")
            {
                return ValidateShippingMenuPlan(snapshot);
            }

            return new[] { "close_menu_type_not_whitelisted" };
        }

        private static string[] ValidateShippingMenuPlan(SnapshotEnvelope snapshot)
        {
            var state = ReadStateFieldValue(snapshot, "menus", "menu_specific_state");
            if (!state.HasValue ||
                state.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(ReadString(state.Value, "kind"), "shipping_summary", StringComparison.Ordinal))
            {
                return new[] { "shipping_summary_transparent_state_missing" };
            }

            var reasons = new List<string>();
            if (ReadNullableBool(state.Value, "can_receive_input") is null)
                reasons.Add("shipping_summary_can_receive_input_missing");
            if (!state.Value.TryGetProperty("current_page", out var currentPage) ||
                currentPage.ValueKind != JsonValueKind.Number ||
                !currentPage.TryGetInt32(out _))
            {
                reasons.Add("shipping_summary_current_page_missing");
            }
            if (ReadNullableBool(state.Value, "ok_button_present") != true)
                reasons.Add("shipping_summary_ok_button_missing");
            return reasons.ToArray();
        }

        private static string[] ValidateLevelUpMenuPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var state = ReadStateFieldValue(snapshot, "menus", "menu_specific_state");
            if (!state.HasValue ||
                state.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(ReadString(state.Value, "kind"), "level_up", StringComparison.Ordinal))
            {
                return new[] { "level_up_menu_transparent_state_missing" };
            }

            var reasons = new List<string>();
            if (ReadBool(state.Value, "reflection_fields_complete") != true)
                reasons.Add("level_up_menu_reflection_fields_incomplete");
            if (ReadBool(state.Value, "is_active") != true)
                reasons.Add("level_up_menu_not_active");
            if (ReadBool(state.Value, "can_receive_input") != true)
                reasons.Add("level_up_menu_input_not_ready");

            if (ReadBool(state.Value, "is_profession_chooser") == true)
            {
                var requestedChoice = ReadIntParameter(action, "profession_choice_id");
                var validChoices = state.Value.TryGetProperty("profession_choices", out var choices) &&
                    choices.ValueKind == JsonValueKind.Array
                    ? choices.EnumerateArray()
                        .Where(row => row.TryGetProperty("profession_id", out var id) && id.TryGetInt32(out _))
                        .Select(row => row.GetProperty("profession_id").GetInt32())
                        .ToArray()
                    : Array.Empty<int>();
                if (!requestedChoice.HasValue)
                    reasons.Add("profession_choice_id_required");
                else if (!validChoices.Contains(requestedChoice.Value))
                    reasons.Add("profession_choice_id_not_offered");
            }

            return reasons.ToArray();
        }

        private static string[] SafeOrdinaryDialogueBlockReasons(SnapshotEnvelope snapshot)
        {
            var reasons = new List<string>();
            var activeMenu = ReadStateFieldValue(snapshot, "menus", "active_menu");
            if (!activeMenu.HasValue || activeMenu.Value.ValueKind != JsonValueKind.Object)
            {
                return new[] { "dialogue_close_transparent_active_menu_fields_missing" };
            }

            var lastQuestionKey = ReadString(activeMenu.Value, "last_question_key");
            if (IsTerminalSleepQuestionKey(lastQuestionKey))
            {
                reasons.Add("dialogue_close_terminal_question_key_present:" + lastQuestionKey);
            }

            if (ReadBool(activeMenu.Value, "is_sleep_prompt"))
            {
                reasons.Add("dialogue_close_is_sleep_prompt");
            }

            var eventUp = ReadNullableBool(activeMenu.Value, "event_up");
            if (eventUp is null)
            {
                reasons.Add("dialogue_close_event_up_field_missing_or_ambiguous");
            }
            else if (eventUp.Value)
            {
                reasons.Add("dialogue_close_event_up_true");
            }

            var isQuestion = ReadNullableBool(activeMenu.Value, "dialogue_is_question");
            if (isQuestion is null)
            {
                reasons.Add("dialogue_close_is_question_field_missing_or_ambiguous");
            }
            else if (isQuestion.Value)
            {
                reasons.Add("dialogue_close_is_question_true");
            }

            var responseCount = ReadNullableInt(activeMenu.Value, "dialogue_response_count");
            if (responseCount is null)
            {
                reasons.Add("dialogue_close_response_count_field_missing_or_ambiguous");
            }
            else if (responseCount.Value > 0)
            {
                reasons.Add("dialogue_close_responses_present:" + responseCount.Value);
            }

            var transitioning = ReadNullableBool(activeMenu.Value, "dialogue_transitioning");
            if (transitioning is null)
            {
                reasons.Add("dialogue_close_transitioning_field_missing_or_ambiguous");
            }

            var characterPresent = ReadNullableBool(activeMenu.Value, "dialogue_character_present");
            if (characterPresent is null)
            {
                reasons.Add("dialogue_close_character_present_field_missing_or_ambiguous");
            }
            else if (!characterPresent.Value)
            {
                reasons.Add("dialogue_close_character_present_false");
            }

            var speakerName = ReadString(activeMenu.Value, "dialogue_speaker_name");
            var speakerNamePresent = activeMenu.Value.TryGetProperty("dialogue_speaker_name", out _);
            if (!speakerNamePresent)
            {
                reasons.Add("dialogue_close_speaker_name_field_missing");
            }
            else if (string.IsNullOrWhiteSpace(speakerName))
            {
                reasons.Add("dialogue_close_speaker_name_empty");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static bool IsTerminalSleepQuestionKey(string questionKey)
        {
            return string.Equals(questionKey, "Sleep", StringComparison.Ordinal) ||
                string.Equals(questionKey, "SleepTent", StringComparison.Ordinal);
        }

        private static bool? ReadNullableBool(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            if (value.ValueKind == JsonValueKind.False)
            {
                return false;
            }

            return null;
        }

        private static string[] ValidateBuyShopItemPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.buy_shop_item")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            if (!ActionSeesShopMenuOpen(action, snapshot))
            {
                reasons.Add("shop_menu_not_open");
            }

            var quantity = ReadIntParameter(action, "quantity") ?? 1;
            if (quantity != 1)
            {
                reasons.Add("quantity_one_required_for_safe_purchase_slice");
            }

            var shopStock = ReadStateFieldValue(snapshot, "menus", "shop_stock");
            if (!shopStock.HasValue || shopStock.Value.ValueKind != JsonValueKind.Object)
            {
                if (HasRuntimeShopStockRecheckParameters(action))
                {
                    return reasons.Distinct(StringComparer.Ordinal).ToArray();
                }

                reasons.Add("menus_shop_stock_unavailable");
                return reasons.ToArray();
            }

            var candidate = FindPurchaseCandidate(snapshot, ReadParameter(action, "qualified_item_id"), ReadParameter(action, "shop_item_id"))
                ?? FirstSafePurchaseCandidate(snapshot);
            if (candidate is null)
            {
                reasons.Add("no_safe_executor_purchase_candidate");
                return reasons.Distinct(StringComparer.Ordinal).ToArray();
            }

            if (ReadBool(candidate.Value, "executor_purchase_enabled") != true)
            {
                reasons.Add("purchase_candidate_not_executor_enabled");
            }

            var maxUnitPrice = ReadIntParameter(action, "max_unit_price");
            var price = ReadInt(candidate.Value, "price");
            if (maxUnitPrice.HasValue && price > maxUnitPrice.Value)
            {
                reasons.Add("purchase_price_exceeds_request_limit");
            }

            var expectedShopId = ReadParameter(action, "expected_shop_id");
            var actualShopId = ReadString(shopStock.Value, "shop_id");
            if (!string.IsNullOrWhiteSpace(expectedShopId) &&
                !string.Equals(expectedShopId, actualShopId, StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("shop_menu_id_mismatch");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateChooseDialogueResponsePlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.choose_dialogue_response")
            {
                return Array.Empty<string>();
            }

            if (!ActionSeesDialogueBoxOpen(action, snapshot))
            {
                return new[] { "dialogue_box_not_open" };
            }

            var expectedDialogueKey = ReadParameter(action, "expected_dialogue_key");
            var responseKey = ReadParameter(action, "dialogue_response_key");
            var expectedShopId = ReadParameter(action, "expected_shop_id");
            if (!DialogueResponseOpensExpectedShop(expectedDialogueKey, responseKey, expectedShopId))
            {
                return new[] { "dialogue_response_not_whitelisted" };
            }

            return Array.Empty<string>();
        }

        private static string[] ValidateQuestAdvancePlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "quest.advance")
            {
                return Array.Empty<string>();
            }

            var candidateId = ReadParameter(action, "candidate_id");
            var questId = ReadParameter(action, "quest_id");
            var questKey = ReadParameter(action, "quest_key");
            if (string.IsNullOrWhiteSpace(candidateId) && string.IsNullOrWhiteSpace(questId) && string.IsNullOrWhiteSpace(questKey))
            {
                return new[] { "quest_identity_not_specified" };
            }

            return Array.Empty<string>();
        }

    }
}
