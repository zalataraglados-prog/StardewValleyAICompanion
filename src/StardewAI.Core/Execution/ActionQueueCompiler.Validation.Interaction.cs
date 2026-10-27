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
        private static string[] ValidateActiveMenuBracket(SmallModelAction action, SnapshotEnvelope snapshot, OptionSpec? option)
        {
            if (action.OptionId == "executor.close_menu" ||
                action.OptionId == "executor.buy_shop_item" ||
                action.OptionId == "executor.sell_shop_item" ||
                action.OptionId == "executor.choose_dialogue_response" ||
                action.OptionId == "executor.choose_animal_purchase_response" ||
                action.OptionId == "executor.purchase_animal" ||
                action.OptionId == "executor.accept_daily_quest" ||
                action.OptionId == "executor.accept_special_order" ||
                action.OptionId == "executor.claim_quest_reward" ||
                action.OptionId == "executor.name_hatched_animal" ||
                action.OptionId == "executor.advance_story_event" ||
                action.OptionId == "executor.advance_story_event_minigame" ||
                IsBoundedLetterViewerSettleWait(action, snapshot) ||
                IsBoundedShopMenuReadinessWait(action, snapshot) ||
                (action.OptionId == "executor.sleep" &&
                    string.Equals(
                        ReadParameter(action, "sleep_resume_mode"),
                        Infrastructure.SleepPromptResumeProjection.ResumeMode,
                        StringComparison.Ordinal)) ||
                (action.OptionId == "recovery.stabilize_day" &&
                    Infrastructure.SleepPromptResumeProjection.IsAvailable(
                        snapshot)) ||
                option is null)
            {
                return Array.Empty<string>();
            }

            if (option.CompilerResponsibility != CompilerResponsibilities.FullActionExpansion || option.TrainingRole != TrainingRoles.ExecutorCalibration)
            {
                return Array.Empty<string>();
            }

            return ActionSeesActiveMenuOpen(action, snapshot)
                ? new[] { "active_menu_must_be_closed_before_action" }
                : Array.Empty<string>();
        }

        private static bool IsBoundedLetterViewerSettleWait(
            SmallModelAction action,
            SnapshotEnvelope snapshot)
        {
            if (!string.Equals(
                    action.OptionId,
                    "executor.wait_ticks",
                    StringComparison.Ordinal) ||
                !int.TryParse(
                    ReadParameter(action, "wait_ticks"),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var waitTicks) ||
                waitTicks is < 1 or > 60 ||
                !string.Equals(
                    ReadParameter(action, "continuation.option_id"),
                    "mail.process_letter",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ReadParameter(action, "target_runtime_type"),
                    "LetterViewerMenu",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ActiveMenuType(snapshot),
                    "LetterViewerMenu",
                    StringComparison.Ordinal))
            {
                return false;
            }

            var expectedIdentity = ReadParameter(
                action,
                "mail_menu_identity_sha256");
            var menuState = ReadStateFieldValue(
                snapshot,
                "menus",
                "menu_specific_state");
            return !string.IsNullOrWhiteSpace(expectedIdentity) &&
                menuState.HasValue &&
                menuState.Value.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(menuState.Value, "kind"),
                    "letter_viewer",
                    StringComparison.Ordinal) &&
                string.Equals(
                    ReadString(menuState.Value, "menu_identity_sha256"),
                    expectedIdentity,
                    StringComparison.Ordinal) &&
                ReadBool(menuState.Value, "can_receive_input") == false;
        }

        private static bool IsBoundedShopMenuReadinessWait(
            SmallModelAction action,
            SnapshotEnvelope snapshot)
        {
            return string.Equals(
                    action.OptionId,
                    "executor.wait_ticks",
                    StringComparison.Ordinal) &&
                int.TryParse(
                    ReadParameter(action, "wait_ticks"),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var waitTicks) &&
                waitTicks is >= 1 and <= 60 &&
                HasExactActionParameter(
                    action,
                    "precondition",
                    "shop_menu_open=true") &&
                HasExactActionParameter(
                    action,
                    "precondition",
                    "shop_menu_safety_timer_active=true") &&
                HasExactActionParameter(
                    action,
                    "safety_constraint",
                    "bounded_native_shop_menu_readiness_wait") &&
                HasExactActionParameter(
                    action,
                    "safety_constraint",
                    "purchase_continuation_identity_preserved") &&
                TryFindSafetyTimerDeferredPurchaseCandidate(
                    action,
                    snapshot,
                    "continuation.",
                    out _);
        }

        private static bool TryFindSafetyTimerDeferredPurchaseCandidate(
            SmallModelAction action,
            SnapshotEnvelope snapshot,
            string identityPrefix,
            out JsonElement candidate)
        {
            candidate = default;
            var optionId = ReadParameter(action, identityPrefix + "option_id");
            var shopId = ReadParameter(action, identityPrefix + "shop_id");
            var qualifiedItemId = ReadParameter(
                action,
                identityPrefix + "qualified_item_id");
            var stockId = ReadParameter(action, identityPrefix + "stock_id");
            var outputStack = ReadIntParameter(
                action,
                identityPrefix + "output_stack_per_purchase");
            var outputQuality = ReadIntParameter(
                action,
                identityPrefix + "output_quality");
            var maxUnitPrice = ReadIntParameter(
                action,
                identityPrefix + "max_unit_price");
            var quantity = ReadIntParameter(action, identityPrefix + "quantity");
            if (!string.Equals(
                    optionId,
                    "economy.buy_supplies",
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(shopId) ||
                string.IsNullOrWhiteSpace(qualifiedItemId) ||
                string.IsNullOrWhiteSpace(stockId) ||
                outputStack is null or <= 0 ||
                outputQuality is null or < 0 ||
                maxUnitPrice is null or < 0 ||
                quantity != 1 ||
                !string.Equals(
                    ActiveMenuType(snapshot),
                    "ShopMenu",
                    StringComparison.Ordinal))
            {
                return false;
            }

            var menu = ReadStateFieldValue(
                snapshot,
                "menus",
                "menu_specific_state");
            var stock = ReadStateFieldValue(snapshot, "menus", "shop_stock");
            if (!menu.HasValue ||
                menu.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(
                    ReadString(menu.Value, "kind"),
                    "shop",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    ReadString(menu.Value, "shop_id"),
                    shopId,
                    StringComparison.OrdinalIgnoreCase) ||
                ReadNullableBool(menu.Value, "read_only") != false ||
                ReadIntOptional(menu.Value, "safety_timer") is not > 0 ||
                ReadNullableBool(menu.Value, "held_item_present") != false ||
                !stock.HasValue ||
                stock.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(
                    ReadString(stock.Value, "shop_id"),
                    shopId,
                    StringComparison.OrdinalIgnoreCase) ||
                ReadNullableBool(stock.Value, "read_only") != false ||
                ReadIntOptional(stock.Value, "safety_timer") is not > 0 ||
                ReadNullableBool(stock.Value, "held_item_present") != false ||
                ReadIntOptional(stock.Value, "currency") != 0 ||
                ReadNullableBool(stock.Value, "shop_on_purchase_present") != false ||
                !stock.Value.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object ||
                    !string.Equals(
                        ReadString(entry, "qualified_item_id"),
                        qualifiedItemId,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        ReadString(entry, "synced_key"),
                        stockId,
                        StringComparison.Ordinal) ||
                    ReadIntOptional(entry, "stack") != outputStack.Value ||
                    ReadIntOptional(entry, "quality") != outputQuality.Value ||
                    ReadIntOptional(entry, "price") is not int price ||
                    price > maxUnitPrice.Value ||
                    ReadNullableBool(entry, "can_buy_item") != true ||
                    ReadNullableBool(entry, "can_afford_one_with_currency") != true ||
                    ReadNullableBool(entry, "can_afford_one_with_trade_item") != true ||
                    ReadNullableBool(entry, "could_inventory_accept") != true ||
                    ReadNullableBool(entry, "executor_purchase_enabled") != false ||
                    !HasOnlyShopSafetyTimerBlockReason(entry))
                {
                    continue;
                }

                candidate = entry;
                return true;
            }

            return false;
        }

        private static bool HasOnlyShopSafetyTimerBlockReason(JsonElement entry)
        {
            return entry.TryGetProperty(
                    "executor_block_reasons",
                    out var reasons) &&
                reasons.ValueKind == JsonValueKind.Array &&
                reasons.GetArrayLength() == 1 &&
                reasons[0].ValueKind == JsonValueKind.String &&
                string.Equals(
                    reasons[0].GetString(),
                    "shop_menu_safety_timer_active",
                    StringComparison.Ordinal);
        }

        private static bool HasExactActionParameter(
            SmallModelAction action,
            string name,
            string value) => action.Parameters.Any(parameter =>
                string.Equals(parameter.Name, name, StringComparison.Ordinal) &&
                string.Equals(parameter.Value, value, StringComparison.Ordinal));

        private static string[] ValidateSleepPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.sleep")
            {
                return Array.Empty<string>();
            }

            var reasons = new List<string>();
            if (!string.Equals(ReadParameter(action, "compiler_context.is_terminal_step"), "true", StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("sleep_action_must_be_terminal");
            }

            if (string.Equals(
                    ReadParameter(action, "sleep_resume_mode"),
                    Infrastructure.SleepPromptResumeProjection.ResumeMode,
                    StringComparison.Ordinal))
            {
                reasons.AddRange(
                    Infrastructure.SleepPromptResumeProjection.BlockReasons(
                        snapshot));
                return reasons
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }

            if (ActiveMenuOpen(snapshot))
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
                reasons.Add("sleep_target_unavailable");
            }

            return reasons.Distinct(StringComparer.Ordinal).ToArray();
        }

        private static string[] ValidateInteractPlan(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            if (action.OptionId != "executor.interact")
            {
                return Array.Empty<string>();
            }

            var targetX = ReadIntParameter(action, "target_tile_x");
            var targetY = ReadIntParameter(action, "target_tile_y");
            if (!targetX.HasValue || !targetY.HasValue)
            {
                return new[] { "interact_target_tile_required" };
            }

            var interactionKind = ReadParameter(action, "interaction_kind");
            if (string.IsNullOrWhiteSpace(interactionKind))
            {
                return new[] { "interact_kind_required" };
            }

            if (ActionSeesActiveMenuOpen(action, snapshot))
            {
                return new[] { "interact_menu_must_be_clear" };
            }

            var expectedActionType = ReadParameter(action, "expected_action_type");
            if (string.IsNullOrWhiteSpace(expectedActionType))
            {
                return new[] { "interact_expected_action_type_required" };
            }

            if (string.Equals(interactionKind, "community_center_note", StringComparison.Ordinal))
            {
                return ValidateCommunityCenterFirstNoteInteraction(
                    action,
                    snapshot,
                    targetX.Value,
                    targetY.Value,
                    expectedActionType);
            }

            if (!InteractTargetWithinOneTile(
                    action,
                    snapshot,
                    targetX.Value,
                    targetY.Value))
            {
                return new[] { "interact_target_not_adjacent" };
            }

            if (string.Equals(interactionKind, "animal_door", StringComparison.Ordinal))
            {
                return ValidateAnimalDoorInteraction(
                    action,
                    snapshot,
                    targetX.Value,
                    targetY.Value,
                    expectedActionType);
            }

            if (string.Equals(interactionKind, "overlay_object", StringComparison.Ordinal))
            {
                if (string.Equals(expectedActionType, "SkullKeyChest", StringComparison.Ordinal))
                {
                    return SkullKeyRewardChestMatches(snapshot, targetX.Value, targetY.Value)
                        ? Array.Empty<string>()
                        : new[] { "skull_key_reward_chest_not_observed_at_target" };
                }
                if (string.Equals(expectedActionType, "DarkTalismanChest", StringComparison.Ordinal))
                {
                    if (!string.Equals(
                            ReadParameter(action, "required_executor_profile"),
                            "dark_talisman_story_executor",
                            StringComparison.Ordinal))
                    {
                        return new[] { "dark_talisman_executor_profile_required" };
                    }
                    return DarkTalismanChestMatches(snapshot, targetX.Value, targetY.Value)
                        ? Array.Empty<string>()
                        : new[] { "dark_talisman_chest_not_observed_at_target" };
                }
                return new[] { "interact_overlay_object_type_unsupported" };
            }

            if (string.Equals(interactionKind, "npc_check_action", StringComparison.Ordinal))
            {
                return string.Equals(
                           ReadParameter(action, "required_executor_profile"),
                           "dark_talisman_story_executor",
                           StringComparison.Ordinal) &&
                    string.Equals(expectedActionType, "DarkTalismanKrobus", StringComparison.Ordinal) &&
                    DarkTalismanKrobusMatches(snapshot, targetX.Value, targetY.Value)
                        ? Array.Empty<string>()
                        : new[] { "dark_talisman_krobus_not_observed_at_target" };
            }

            if (string.Equals(interactionKind, "location_check_action", StringComparison.Ordinal))
            {
                return string.Equals(
                           ReadParameter(action, "required_executor_profile"),
                           "dark_talisman_story_executor",
                           StringComparison.Ordinal) &&
                    string.Equals(expectedActionType, "DarkTalismanStatue", StringComparison.Ordinal) &&
                    DarkTalismanStatueMatches(snapshot, targetX.Value, targetY.Value)
                        ? Array.Empty<string>()
                        : new[] { "dark_talisman_statue_not_observed_at_target" };
            }

            if (string.Equals(interactionKind, "boat_tunnel_check_action", StringComparison.Ordinal))
            {
                return ValidateGingerIslandBoatInteraction(
                    action,
                    snapshot,
                    targetX.Value,
                    targetY.Value,
                    expectedActionType);
            }

            if (string.Equals(interactionKind, "parrot_upgrade_perch", StringComparison.Ordinal))
            {
                return ValidateGingerIslandPerchInteraction(
                    action,
                    snapshot,
                    targetX.Value,
                    targetY.Value,
                    expectedActionType);
            }

            if (!string.Equals(interactionKind, "map_action", StringComparison.Ordinal))
            {
                return new[] { "interact_kind_unsupported" };
            }

            if (string.Equals(expectedActionType, "Mailbox", StringComparison.Ordinal))
            {
                var mailbox = ReadStateFieldValue(snapshot, "quests", "mailbox_processing");
                return mailbox.HasValue && mailbox.Value.ValueKind == JsonValueKind.Object &&
                    ReadInt(mailbox.Value, "mailbox_action_tile_x", int.MinValue) == targetX.Value &&
                    ReadInt(mailbox.Value, "mailbox_action_tile_y", int.MinValue) == targetY.Value &&
                    string.Equals(ReadString(mailbox.Value, "pending_mail_id"), ReadParameter(action, "target_runtime_identity"), StringComparison.Ordinal)
                        ? Array.Empty<string>()
                        : new[] { "mailbox_transparent_target_identity_mismatch" };
            }

            if (string.Equals(expectedActionType, "GoldenScythe", StringComparison.Ordinal) &&
                string.Equals(
                    ReadParameter(action, "required_executor_profile"),
                    "mining_perfect_executor",
                    StringComparison.Ordinal))
            {
                return GoldenScytheAltarMatches(snapshot, targetX.Value, targetY.Value)
                    ? Array.Empty<string>()
                    : new[] { "golden_scythe_altar_not_observed_at_target" };
            }

            if (RouteActionBranchBlockedAtTile(snapshot, targetX.Value, targetY.Value))
            {
                return new[] { "interact_unsupported_action_branch_at_target" };
            }

            if (!TargetActionBranchMatches(snapshot, targetX.Value, targetY.Value, expectedActionType))
            {
                return new[] { "interact_expected_action_type_mismatch" };
            }

            return Array.Empty<string>();
        }

        private static string[] ValidateAnimalDoorInteraction(
            SmallModelAction action,
            SnapshotEnvelope snapshot,
            int targetX,
            int targetY,
            string expectedActionType)
        {
            if (!string.Equals(expectedActionType, "AnimalDoor", StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "expected_animal_door_open_before"), "false", StringComparison.Ordinal) ||
                !string.Equals(ReadParameter(action, "expected_animal_door_open_after"), "true", StringComparison.Ordinal) ||
                !string.Equals(
                    ReadParameter(action, "native_contract"),
                    "Building.doAction->didPlayerJustRightClick->Building.ToggleAnimalDoor->animalDoorOpen",
                    StringComparison.Ordinal))
            {
                return new[] { "animal_door_interaction_contract_invalid" };
            }
            var type = ReadParameter(action, "building_type");
            var buildingX = ReadIntParameter(action, "building_tile_x");
            var buildingY = ReadIntParameter(action, "building_tile_y");
            var safeSlot = ReadIntParameter(action, "safe_slot_index");
            var buildings = ReadStateFieldValue(snapshot, "farm", "buildings");
            if (string.IsNullOrWhiteSpace(type) || !buildingX.HasValue ||
                !buildingY.HasValue || !safeSlot.HasValue ||
                !buildings.HasValue || buildings.Value.ValueKind != JsonValueKind.Array)
            {
                return new[] { "animal_door_transparent_identity_incomplete" };
            }
            var matches = buildings.Value.EnumerateArray().Where(row =>
                row.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(row, "type"), type, StringComparison.Ordinal) &&
                ReadInt(row, "tile_x") == buildingX.Value &&
                ReadInt(row, "tile_y") == buildingY.Value &&
                row.TryGetProperty("animal_door", out var door) &&
                door.ValueKind == JsonValueKind.Object &&
                ReadBool(door, "present") == true &&
                ReadBool(door, "is_open") == false &&
                ReadInt(door, "target_tile_x", int.MinValue) == targetX &&
                ReadInt(door, "target_tile_y", int.MinValue) == targetY &&
                string.Equals(ReadString(door, "interaction_status"), "ready_native_right_click_toggle", StringComparison.Ordinal) &&
                string.Equals(ReadString(door, "native_contract"), ReadParameter(action, "native_contract"), StringComparison.Ordinal)).ToArray();
            return matches.Length == 1
                ? Array.Empty<string>()
                : new[] { "animal_door_projection_drifted" };
        }

        private static bool GoldenScytheAltarMatches(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var tiles = ReadStateFieldValue(snapshot, "mining", "tiles");
            if (!tiles.HasValue || tiles.Value.ValueKind != JsonValueKind.Object ||
                !tiles.Value.TryGetProperty("golden_scythe_altars", out var altars) ||
                altars.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return altars.EnumerateArray().Any(altar =>
                ReadInt(altar, "tile_x") == targetX &&
                ReadInt(altar, "tile_y") == targetY &&
                ReadBool(altar, "present") == true &&
                string.Equals(ReadString(altar, "action"), "GoldenScythe", StringComparison.Ordinal));
        }

        private static bool SkullKeyRewardChestMatches(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var chests = ReadStateFieldValue(snapshot, "mining", "floor_objectives");
            if (!chests.HasValue || chests.Value.ValueKind != JsonValueKind.Object ||
                !chests.Value.TryGetProperty("skull_key_reward_chests", out var rewardChests) ||
                rewardChests.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return rewardChests.EnumerateArray().Any(chest =>
                ReadInt(chest, "tile_x") == targetX &&
                ReadInt(chest, "tile_y") == targetY &&
                ReadBool(chest, "contains_skull_key") == true &&
                ReadInt(chest, "special_item_which") == 4 &&
                string.Equals(ReadString(chest, "interaction_kind"), "overlay_object", StringComparison.Ordinal) &&
                string.Equals(ReadString(chest, "expected_action_type"), "SkullKeyChest", StringComparison.Ordinal));
        }

        private static bool DarkTalismanChestMatches(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var access = DarkTalismanAccessProjection(snapshot);
            return access.HasValue &&
                string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), "BugLand", StringComparison.Ordinal) &&
                string.Equals(ReadString(access.Value, "status"), "ready_to_claim_dark_talisman", StringComparison.Ordinal) &&
                ReadBool(access.Value, "krobus_unseal_mail_received") == true &&
                ReadBool(access.Value, "player_has_dark_talisman") == false &&
                access.Value.TryGetProperty("bug_lair", out var bugLair) &&
                bugLair.ValueKind == JsonValueKind.Object &&
                ReadInt(bugLair, "chest_tile_x", int.MinValue) == targetX &&
                ReadInt(bugLair, "chest_tile_y", int.MinValue) == targetY &&
                ReadBool(bugLair, "overlay_object_present") == true &&
                ReadBool(bugLair, "contains_exact_dark_talisman") == true &&
                ReadInt(bugLair, "chest_item_count", -1) == 1 &&
                ReadInt(bugLair, "expected_special_item_which", -1) == 6 &&
                string.Equals(
                    ReadString(bugLair, "chest_runtime_type"),
                    "StardewValley.Objects.Chest",
                    StringComparison.Ordinal) &&
                bugLair.TryGetProperty("special_item_ids", out var specialItemIds) &&
                specialItemIds.ValueKind == JsonValueKind.Array &&
                specialItemIds.GetArrayLength() == 1 &&
                specialItemIds[0].ValueKind == JsonValueKind.Number &&
                specialItemIds[0].GetInt32() == 6;
        }

        private static bool DarkTalismanKrobusMatches(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var access = DarkTalismanAccessProjection(snapshot);
            return access.HasValue &&
                string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), "Sewer", StringComparison.Ordinal) &&
                string.Equals(ReadString(access.Value, "status"), "ready_to_talk_to_krobus", StringComparison.Ordinal) &&
                ReadBool(access.Value, "dark_talisman_quest_active") == true &&
                ReadBool(access.Value, "krobus_unseal_mail_received") == false &&
                access.Value.TryGetProperty("krobus", out var krobus) &&
                krobus.ValueKind == JsonValueKind.Object &&
                ReadBool(krobus, "present") == true &&
                ReadBool(krobus, "in_sewer") == true &&
                string.Equals(ReadString(krobus, "runtime_type"), "StardewValley.NPC", StringComparison.Ordinal) &&
                ReadInt(krobus, "tile_x", int.MinValue) == targetX &&
                ReadInt(krobus, "tile_y", int.MinValue) == targetY;
        }

        private static bool DarkTalismanStatueMatches(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var access = DarkTalismanAccessProjection(snapshot);
            return access.HasValue &&
                string.Equals(ReadStateFieldString(snapshot, "player", "location_id"), "Railroad", StringComparison.Ordinal) &&
                string.Equals(ReadString(access.Value, "status"), "ready_to_remove_railroad_statue", StringComparison.Ordinal) &&
                ReadBool(access.Value, "player_has_dark_talisman") == true &&
                access.Value.TryGetProperty("railroad_statue", out var statue) &&
                statue.ValueKind == JsonValueKind.Object &&
                ReadInt(statue, "tile_x", int.MinValue) == targetX &&
                ReadInt(statue, "tile_y", int.MinValue) == targetY &&
                ReadInt(statue, "buildings_tile_index", -1) == 287 &&
                ReadInt(statue, "expected_buildings_tile_index", -1) == 287 &&
                string.Equals(ReadString(statue, "expected_tilesheet"), "untitled tile sheet", StringComparison.Ordinal) &&
                ReadBool(statue, "identity_exact") == true &&
                ReadBool(statue, "gone") == false;
        }

        private static JsonElement? DarkTalismanAccessProjection(SnapshotEnvelope snapshot)
        {
            var access = ReadStateFieldValue(snapshot, "locations", "dark_talisman_access");
            if (!access.HasValue || access.Value.ValueKind != JsonValueKind.Object ||
                !string.Equals(ReadString(access.Value, "schema_version"), "dark_talisman_access.v1", StringComparison.Ordinal) ||
                !string.Equals(
                    ReadString(access.Value, "projection_status"),
                    "complete_locked_base_1.6.15_native_contract",
                    StringComparison.Ordinal))
            {
                return null;
            }
            return access.Value;
        }

        private static bool InteractTargetWithinOneTile(
            SmallModelAction action,
            SnapshotEnvelope snapshot,
            int targetX,
            int targetY)
        {
            var currentLocation = ReadStateFieldString(
                snapshot,
                "player",
                "location_id");
            var projectedLocation = ReadParameter(
                action,
                "compiler_context.player_location_before_step");
            var targetLocation = ReadParameter(action, "target_location");
            var projectedX = ReadIntParameter(
                action,
                "compiler_context.player_tile_x_before_step");
            var projectedY = ReadIntParameter(
                action,
                "compiler_context.player_tile_y_before_step");
            var useProjection = projectedX.HasValue &&
                projectedY.HasValue &&
                !string.IsNullOrWhiteSpace(projectedLocation) &&
                string.Equals(
                    projectedLocation,
                    currentLocation,
                    StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(targetLocation) ||
                 string.Equals(
                     targetLocation,
                     projectedLocation,
                     StringComparison.OrdinalIgnoreCase));
            var playerX = useProjection
                ? projectedX
                : ReadStateFieldIntOptional(snapshot, "player", "tile_x");
            var playerY = useProjection
                ? projectedY
                : ReadStateFieldIntOptional(snapshot, "player", "tile_y");
            return playerX.HasValue && playerY.HasValue && Math.Abs(playerX.Value - targetX) + Math.Abs(playerY.Value - targetY) == 1;
        }

        private static bool ActiveMenuOpen(SnapshotEnvelope snapshot)
        {
            var activeMenu = ReadStateFieldValue(snapshot, "menus", "active_menu");
            if (!activeMenu.HasValue)
            {
                return false;
            }

            if (activeMenu.Value.ValueKind == JsonValueKind.String)
            {
                return !string.Equals(activeMenu.Value.GetString(), "none", StringComparison.OrdinalIgnoreCase);
            }

            if (activeMenu.Value.ValueKind == JsonValueKind.Object &&
                activeMenu.Value.TryGetProperty("is_open", out var isOpen))
            {
                return isOpen.ValueKind == JsonValueKind.True;
            }

            return false;
        }

        private static bool ActionSeesActiveMenuOpen(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var beforeStep = ReadParameter(action, "compiler_context.active_menu_open_before_step");
            if (!string.IsNullOrWhiteSpace(beforeStep))
            {
                return string.Equals(beforeStep, "true", StringComparison.OrdinalIgnoreCase);
            }

            return ActiveMenuOpen(snapshot);
        }

        private static bool ActionSeesShopMenuOpen(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var beforeStepType = ReadParameter(action, "compiler_context.active_menu_type_before_step");
            if (!string.IsNullOrWhiteSpace(beforeStepType))
            {
                return string.Equals(beforeStepType, "ShopMenu", StringComparison.Ordinal);
            }

            return string.Equals(ActiveMenuType(snapshot), "ShopMenu", StringComparison.Ordinal);
        }

        private static bool ActionSeesDialogueBoxOpen(SmallModelAction action, SnapshotEnvelope snapshot)
        {
            var beforeStepType = ReadParameter(action, "compiler_context.active_menu_type_before_step");
            if (!string.IsNullOrWhiteSpace(beforeStepType))
            {
                return string.Equals(beforeStepType, "DialogueBox", StringComparison.Ordinal);
            }

            return string.Equals(ActiveMenuType(snapshot), "DialogueBox", StringComparison.Ordinal);
        }

        private static string ActiveMenuType(SnapshotEnvelope snapshot)
        {
            var activeMenu = ReadStateFieldValue(snapshot, "menus", "active_menu");
            if (!activeMenu.HasValue)
            {
                return string.Empty;
            }

            if (activeMenu.Value.ValueKind == JsonValueKind.String)
            {
                var value = activeMenu.Value.GetString() ?? string.Empty;
                return string.Equals(value, "none", StringComparison.OrdinalIgnoreCase) ? string.Empty : value;
            }

            return activeMenu.Value.ValueKind == JsonValueKind.Object ? ReadString(activeMenu.Value, "type") : string.Empty;
        }

        private static bool IsSafeCloseMenuType(string type)
        {
            return type is "GameMenu" or "InventoryMenu" or "QuestLog" or "MapPage" or "ProfileMenu" or "ShopMenu";
        }

        private static SleepMacroTarget? SleepTarget(SnapshotEnvelope snapshot)
        {
            var context = ReadStateFieldValue(snapshot, "current_location", "home_context");
            if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (ReadBool(context.Value, "home_available") != true || ReadBool(context.Value, "current_location_is_home") != true || ReadBool(context.Value, "bed_tile_has_bed") != true)
            {
                return null;
            }

            var homeLocation = ReadString(context.Value, "home_location_id");
            var bedX = ReadInt(context.Value, "bed_tile_x");
            var bedY = ReadInt(context.Value, "bed_tile_y");
            var stand = FindBestSleepStandTile(snapshot, bedX, bedY);
            if (stand is null)
            {
                return null;
            }

            var playerX = ReadStateFieldIntOptional(snapshot, "player", "tile_x") ?? stand.X;
            var playerY = ReadStateFieldIntOptional(snapshot, "player", "tile_y") ?? stand.Y;
            return new SleepMacroTarget
            {
                HomeLocation = string.IsNullOrWhiteSpace(homeLocation) ? "FarmHouse" : homeLocation,
                BedX = bedX,
                BedY = bedY,
                StandX = stand.X,
                StandY = stand.Y,
                FaceDirection = DirectionFromTo(stand.X, stand.Y, bedX, bedY),
                EstimatedTicks = Math.Max(60, (Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y)) * 60)
            };
        }

        private static SleepStandTile? FindBestSleepStandTile(SnapshotEnvelope snapshot, int bedX, int bedY)
        {
            var playerX = ReadStateFieldIntOptional(snapshot, "player", "tile_x") ?? bedX;
            var playerY = ReadStateFieldIntOptional(snapshot, "player", "tile_y") ?? bedY;
            return new[]
                {
                    new SleepStandTile(bedX + 1, bedY),
                    new SleepStandTile(bedX - 1, bedY),
                    new SleepStandTile(bedX, bedY + 1),
                    new SleepStandTile(bedX, bedY - 1)
                }
                .Where(tile => SleepStandTileReachable(snapshot, tile.X, tile.Y))
                .OrderBy(tile => Math.Abs(playerX - tile.X) + Math.Abs(playerY - tile.Y))
                .FirstOrDefault();
        }

        private static bool SleepStandTileReachable(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var startX = ReadStateFieldIntOptional(snapshot, "player", "tile_x");
            var startY = ReadStateFieldIntOptional(snapshot, "player", "tile_y");
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!startX.HasValue || !startY.HasValue || !grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width <= 0 || height <= 0 || !TileInBounds(startX.Value, startY.Value, width, height) || !TileInBounds(targetX, targetY, width, height))
            {
                return false;
            }

            var blocked = ReadBlockedCollisionTiles(grid.Value);
            var unsupported = ReadUnsupportedRouteActionTiles(snapshot);
            return PathExists(startX.Value, startY.Value, targetX, targetY, width, height, blocked, unsupported);
        }

        private static int DirectionFromTo(int fromX, int fromY, int toX, int toY)
        {
            if (toX > fromX)
            {
                return 1;
            }

            if (toX < fromX)
            {
                return 3;
            }

            return toY > fromY ? 2 : 0;
        }

        private static bool RouteActionBranchBlockedAtTile(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var row = ReadRouteActionBranchRow(snapshot, targetX, targetY);
            return row.HasValue && row.Value.TryGetProperty("route_training_blocked", out var blocked) && blocked.ValueKind == JsonValueKind.True;
        }

        private static bool TargetActionBranchMatches(SnapshotEnvelope snapshot, int targetX, int targetY, string expectedActionType)
        {
            var row = ReadRouteActionBranchRow(snapshot, targetX, targetY);
            return row.HasValue && string.Equals(ReadString(row.Value, "branch"), expectedActionType, StringComparison.OrdinalIgnoreCase);
        }

        private static JsonElement? ReadRouteActionBranchRow(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var coverage = ReadStateFieldValue(snapshot, "locations", "route_action_branch_coverage");
            if (!coverage.HasValue ||
                coverage.Value.ValueKind != JsonValueKind.Object ||
                !coverage.Value.TryGetProperty("rows", out var rows) ||
                rows.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind == JsonValueKind.Object &&
                    ReadInt(row, "tile_x") == targetX &&
                    ReadInt(row, "tile_y") == targetY)
                {
                    return row;
                }
            }

            return null;
        }

    }
}
