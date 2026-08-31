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

            if (!InteractTargetWithinOneTile(snapshot, targetX.Value, targetY.Value))
            {
                return new[] { "interact_target_not_adjacent" };
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

            if (string.Equals(interactionKind, "overlay_object", StringComparison.Ordinal))
            {
                if (!string.Equals(expectedActionType, "SkullKeyChest", StringComparison.Ordinal))
                {
                    return new[] { "interact_overlay_object_type_unsupported" };
                }
                return SkullKeyRewardChestMatches(snapshot, targetX.Value, targetY.Value)
                    ? Array.Empty<string>()
                    : new[] { "skull_key_reward_chest_not_observed_at_target" };
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

        private static bool InteractTargetWithinOneTile(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var playerX = ReadStateFieldIntOptional(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldIntOptional(snapshot, "player", "tile_y");
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
