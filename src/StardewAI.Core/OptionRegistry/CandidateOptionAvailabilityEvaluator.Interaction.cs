using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Verifier;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[] InteractEndpointCandidates(SnapshotEnvelope snapshot)
        {
            var shopActionTiles = ReadStateFieldValue(snapshot, "current_location", "shop_action_tiles");
            if (!shopActionTiles.HasValue || shopActionTiles.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var locationId = ReadStateFieldString(snapshot, "player", "location_id");
            return shopActionTiles.Value.EnumerateArray()
                .Where(tile => tile.ValueKind == JsonValueKind.Object && HasNumber(tile, "tile_x") && HasNumber(tile, "tile_y"))
                .Select(tile => InteractEndpointCandidate(snapshot, tile, locationId))
                .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.TileY ?? 0)
                .ThenBy(candidate => candidate.TileX ?? 0)
                .Take(32)
                .ToArray();
        }

        private EventCandidate InteractEndpointCandidate(SnapshotEnvelope snapshot, JsonElement tile, string locationId)
        {
            var x = ReadInt(tile, "tile_x");
            var y = ReadInt(tile, "tile_y");
            var parsed = tile.TryGetProperty("parsed", out var parsedValue) && parsedValue.ValueKind == JsonValueKind.Object
                ? parsedValue
                : default;
            var parsedKind = parsed.ValueKind == JsonValueKind.Object ? ReadString(parsed, "kind") : string.Empty;
            var expectedActionType = parsedKind switch
            {
                "legacy_buy" => "Buy",
                "joja_shop" => "JojaShop",
                "dialogue_shop" => ReadString(tile, "action").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
                "direct_or_dialogue_shop" => ReadString(tile, "action").Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty,
                _ => "OpenShop"
            };
            if (string.IsNullOrWhiteSpace(expectedActionType))
            {
                expectedActionType = "OpenShop";
            }

            var standTile = FindBestStandTile(snapshot, x, y);
            var reasons = new List<string>();
            if (ActiveMenuOpenForCandidate(snapshot))
            {
                reasons.Add("interact_menu_must_be_clear");
            }

            if (RouteActionBranchBlockedAtCandidateTile(snapshot, x, y))
            {
                reasons.Add("interact_unsupported_action_branch_at_target");
            }

            if (!TargetActionBranchMatchesForCandidate(snapshot, x, y, expectedActionType))
            {
                reasons.Add("interact_expected_action_type_mismatch");
            }

            var ownerNpc = parsed.ValueKind == JsonValueKind.Object
                ? ReadString(parsed, "owner_npc")
                : string.Empty;
            var ownerServiceArea = parsed.ValueKind == JsonValueKind.Object && parsed.TryGetProperty("owner_service_area", out var area) && area.ValueKind == JsonValueKind.Object
                ? area
                : default;
            var ownerServiceStatus = tile.TryGetProperty("owner_service_status", out var status) && status.ValueKind == JsonValueKind.Object
                ? status
                : default;
            if (OwnerServiceStatusBlocks(ownerServiceStatus) ||
                (ownerServiceStatus.ValueKind != JsonValueKind.Object &&
                    !string.IsNullOrWhiteSpace(ownerNpc) &&
                    !CurrentLocationContainsNpcAtServiceCounter(snapshot, ownerNpc, x, y, ownerServiceArea)))
            {
                reasons.Add("interact_shop_owner_npc_not_at_service_counter");
            }

            var serviceTimeStatus = tile.TryGetProperty("service_time_status", out var timeStatus) && timeStatus.ValueKind == JsonValueKind.Object
                ? timeStatus
                : default;
            if (ServiceTimeStatusBlocks(serviceTimeStatus))
            {
                reasons.Add("interact_shop_service_time_blocked");
            }

            if (standTile is null)
            {
                reasons.Add("interact_no_adjacent_route_stand_tile");
            }
            else
            {
                reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                {
                    OptionId = "exploration.visit_location",
                    Parameters = new[]
                    {
                        Parameter("target_tile_x", standTile.X.ToString()),
                        Parameter("target_tile_y", standTile.Y.ToString())
                    }
                }));
            }

            var shopId = parsed.ValueKind == JsonValueKind.Object
                ? ReadString(parsed, "shop_id")
                : string.Empty;
            var distance = standTile is not null
                ? Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - standTile.X) + Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - standTile.Y)
                : 0;
            var currentTime = NullableReadInt(serviceTimeStatus, "current_time") ?? ReadStateFieldInt(snapshot, "time", "time");
            var openTime = NullableReadInt(serviceTimeStatus, "open_time");
            var effectiveOpenTime = NullableReadInt(serviceTimeStatus, "effective_open_time") ?? openTime;
            var closeTime = NullableReadInt(serviceTimeStatus, "close_time");
            var allowedNow = serviceTimeStatus.ValueKind == JsonValueKind.Object
                ? ReadBool(serviceTimeStatus, "allowed_now")
                : (bool?)(reasons.Count == 0);
            var allowedToday = ServiceCouldOpenToday(serviceTimeStatus, currentTime);
            var waitCost = WaitCostTicks(currentTime, effectiveOpenTime, allowedNow, allowedToday);
            var gateReasons = ServiceTimeBlockReasons(serviceTimeStatus)
                .Concat(OwnerServiceBlockReason(ownerServiceStatus))
                .Concat(reasons.Where(reason =>
                    reason.StartsWith("interact_shop_", StringComparison.Ordinal) ||
                    reason.StartsWith("interact_unsupported_", StringComparison.Ordinal) ||
                    reason.StartsWith("interact_expected_", StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var suffix = string.IsNullOrWhiteSpace(shopId) ? expectedActionType : expectedActionType + ":" + shopId;
            return new EventCandidate
            {
                CandidateId = "interact:" + locationId + ":" + x + "," + y + ":" + suffix,
                Kind = "interact_endpoint",
                Available = reasons.Count == 0,
                LocationId = locationId,
                TileX = x,
                TileY = y,
                ExpectedEffect = standTile is not null
                    ? "move_to_adjacent=" + standTile.X + "," + standTile.Y + ";preview_interact=" + expectedActionType
                    : "preview_interact=" + expectedActionType,
                ShopId = shopId,
                EstimatedTicks = standTile is not null ? Math.Max(30, distance * 60 + 30) : 30,
                EnergyCost = 0,
                AvailabilityClass = serviceTimeStatus.ValueKind == JsonValueKind.Object && ReadBool(serviceTimeStatus, "time_gate_known") == true
                    ? "windowed_available"
                    : "state_gated",
                AllowedNow = allowedNow,
                AllowedToday = allowedToday,
                NextOpenTime = allowedNow == false && allowedToday == true ? effectiveOpenTime : null,
                EffectiveOpenTime = effectiveOpenTime,
                ClosesAt = closeTime,
                WaitCost = waitCost,
                GateReasons = gateReasons,
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).Where(reason => reason != "queue_global_compiler_block").ToArray()
            };
        }

        private static bool? ServiceCouldOpenToday(JsonElement serviceTimeStatus, int currentTime)
        {
            if (serviceTimeStatus.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (ReadBool(serviceTimeStatus, "allowed_now") == true)
            {
                return true;
            }

            var openTime = NullableReadInt(serviceTimeStatus, "effective_open_time") ?? NullableReadInt(serviceTimeStatus, "open_time");
            var closeTime = NullableReadInt(serviceTimeStatus, "close_time");
            if (!openTime.HasValue || !closeTime.HasValue)
            {
                return null;
            }

            return currentTime < closeTime.Value;
        }

        private static int? WaitCostTicks(int currentTime, int? effectiveOpenTime, bool? allowedNow, bool? allowedToday)
        {
            if (allowedNow == true || allowedToday != true || !effectiveOpenTime.HasValue || currentTime >= effectiveOpenTime.Value)
            {
                return null;
            }

            return Math.Max(0, GameTimeMinutesBetween(currentTime, effectiveOpenTime.Value) * 60);
        }

        private static int GameTimeMinutesBetween(int fromTime, int toTime)
        {
            var fromHours = fromTime / 100;
            var fromMinutes = fromTime % 100;
            var toHours = toTime / 100;
            var toMinutes = toTime % 100;
            return Math.Max(0, (toHours * 60 + toMinutes) - (fromHours * 60 + fromMinutes));
        }

        private static IEnumerable<string> ServiceTimeBlockReasons(JsonElement serviceTimeStatus)
        {
            if (serviceTimeStatus.ValueKind != JsonValueKind.Object ||
                !serviceTimeStatus.TryGetProperty("block_reasons", out var blockReasons) ||
                blockReasons.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<string>();
            }

            return blockReasons.EnumerateArray()
                .Where(reason => reason.ValueKind == JsonValueKind.String)
                .Select(reason => reason.GetString() ?? string.Empty)
                .Where(reason => !string.IsNullOrWhiteSpace(reason));
        }

        private static IEnumerable<string> OwnerServiceBlockReason(JsonElement ownerServiceStatus)
        {
            var reason = ReadString(ownerServiceStatus, "block_reason");
            return string.IsNullOrWhiteSpace(reason) ? Array.Empty<string>() : new[] { reason };
        }

        private static bool OwnerServiceStatusBlocks(JsonElement ownerServiceStatus)
        {
            if (ownerServiceStatus.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return ReadBool(ownerServiceStatus, "owner_required") == true &&
                ReadBool(ownerServiceStatus, "in_service_area") == false;
        }

        private static bool ServiceTimeStatusBlocks(JsonElement serviceTimeStatus)
        {
            return serviceTimeStatus.ValueKind == JsonValueKind.Object &&
                ReadBool(serviceTimeStatus, "allowed_now") == false;
        }

        private static bool CurrentLocationContainsNpcAtServiceCounter(SnapshotEnvelope snapshot, string npcName, int actionTileX, int actionTileY, JsonElement ownerServiceArea)
        {
            var positions = ReadStateFieldValue(snapshot, "npcs", "positions");
            if (!positions.HasValue || positions.Value.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            return positions.Value.EnumerateArray().Any(npc =>
                npc.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(npc, "name"), npcName, StringComparison.OrdinalIgnoreCase) &&
                IsNpcAtShopServiceCounter(npc, actionTileX, actionTileY, ownerServiceArea));
        }

        private static bool IsNpcAtShopServiceCounter(JsonElement npc, int actionTileX, int actionTileY, JsonElement ownerServiceArea)
        {
            var npcX = ReadInt(npc, "tile_x");
            var npcY = ReadInt(npc, "tile_y");
            if (ownerServiceArea.ValueKind == JsonValueKind.Object)
            {
                var areaX = ReadInt(ownerServiceArea, "x");
                var areaY = ReadInt(ownerServiceArea, "y");
                var areaWidth = ReadInt(ownerServiceArea, "width");
                var areaHeight = ReadInt(ownerServiceArea, "height");
                if (areaWidth > 0 && areaHeight > 0)
                {
                    return npcX >= areaX && npcX < areaX + areaWidth &&
                        npcY >= areaY && npcY < areaY + areaHeight;
                }
            }

            var distance = Math.Abs(npcX - actionTileX) + Math.Abs(npcY - actionTileY);
            return distance <= 2 && npcY <= actionTileY;
        }

        private static CandidateTile? FindBestStandTile(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            return new[]
                {
                    new CandidateTile(targetX + 1, targetY),
                    new CandidateTile(targetX - 1, targetY),
                    new CandidateTile(targetX, targetY + 1),
                    new CandidateTile(targetX, targetY - 1)
                }
                .Where(tile => !CollisionGridBlocksTile(snapshot, tile.X, tile.Y))
                .OrderBy(tile => Math.Abs(playerX - tile.X) + Math.Abs(playerY - tile.Y))
                .FirstOrDefault();
        }

        private MachineStandTileSelection FindBestMachineStandTile(SnapshotEnvelope snapshot, string locationId, int targetX, int targetY)
        {
            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            var occupiedMachineTiles = MachineTileKeys(snapshot, locationId);
            var candidates = new[]
                {
                    new CandidateTile(targetX + 1, targetY),
                    new CandidateTile(targetX - 1, targetY),
                    new CandidateTile(targetX, targetY + 1),
                    new CandidateTile(targetX, targetY - 1)
                }
                .OrderBy(tile => Math.Abs(playerX - tile.X) + Math.Abs(playerY - tile.Y))
                .ToArray();
            var sawMachineOccupied = false;
            var sawCollisionBlocked = false;
            var sawCompilerBlocked = false;
            foreach (var tile in candidates)
            {
                if (occupiedMachineTiles.Contains(TileKey(tile.X, tile.Y)))
                {
                    sawMachineOccupied = true;
                    continue;
                }

                if (CollisionGridBlocksTile(snapshot, tile.X, tile.Y))
                {
                    sawCollisionBlocked = true;
                    continue;
                }

                var compilerBlocks = CompilerProbeBlockingReasons(snapshot, MachineStandTileProbeCandidate(snapshot, tile))
                    .Where(reason => reason != "missing_required_state")
                    .ToArray();
                if (compilerBlocks.Length > 0)
                {
                    sawCompilerBlocked = true;
                    continue;
                }

                return new MachineStandTileSelection(tile, Array.Empty<string>());
            }

            var reasons = new List<string>();
            if (sawMachineOccupied)
            {
                reasons.Add("machine_adjacent_stand_tile_occupied_by_machine");
            }
            if (sawCollisionBlocked)
            {
                reasons.Add("machine_adjacent_stand_tile_blocked_by_collision_grid");
            }
            if (sawCompilerBlocked)
            {
                reasons.Add("machine_adjacent_stand_tile_blocked_by_movement_compiler_probe");
            }
            if (reasons.Count == 0)
            {
                reasons.Add("machine_adjacent_stand_tile_unavailable");
            }

            return new MachineStandTileSelection(null, reasons.ToArray());
        }

        private static ISet<string> MachineTileKeys(SnapshotEnvelope snapshot, string locationId)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var machines = ReadStateFieldValue(snapshot, "farm", "machines");
            if (!machines.HasValue || machines.Value.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var machine in machines.Value.EnumerateArray())
            {
                if (machine.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                var machineLocation = ReadString(machine, "location_id");
                if (string.IsNullOrWhiteSpace(machineLocation))
                {
                    machineLocation = "Farm";
                }
                if (string.Equals(machineLocation, locationId, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(TileKey(ReadInt(machine, "tile_x"), ReadInt(machine, "tile_y")));
                }
            }

            return result;
        }

        private static OptionAvailabilityCandidate MachineStandTileProbeCandidate(SnapshotEnvelope snapshot, CandidateTile tile)
        {
            if (RoutePathPreviewAvailable(snapshot))
            {
                return new OptionAvailabilityCandidate
                {
                    OptionId = "exploration.visit_location",
                    Parameters = new[]
                    {
                        Parameter("target_tile_x", tile.X.ToString()),
                        Parameter("target_tile_y", tile.Y.ToString()),
                        Parameter("target_location", ReadStateFieldString(snapshot, "player", "location_id"))
                    }
                };
            }

            return new OptionAvailabilityCandidate
            {
                OptionId = "executor.move_to_tile",
                Parameters = new[]
                {
                    Parameter("target_tile_x", tile.X.ToString()),
                    Parameter("target_tile_y", tile.Y.ToString())
                }
            };
        }

        private static bool RoutePathPreviewAvailable(SnapshotEnvelope snapshot)
        {
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            return grid.HasValue &&
                grid.Value.ValueKind == JsonValueKind.Object &&
                ReadInt(grid.Value, "width") > 0 &&
                ReadInt(grid.Value, "height") > 0;
        }

        private static bool ActiveMenuOpenForCandidate(SnapshotEnvelope snapshot)
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

            return activeMenu.Value.ValueKind == JsonValueKind.Object && ReadBool(activeMenu.Value, "is_open") == true;
        }

        private static bool SleepPromptOpenForCandidate(SnapshotEnvelope snapshot)
        {
            var prompt = ReadStateFieldValue(snapshot, "menus", "sleep_prompt_context");
            return prompt.HasValue && prompt.Value.ValueKind == JsonValueKind.Object && ReadBool(prompt.Value, "prompt_open") == true;
        }

        private static string[] CloseMenuCandidateBlockReasons(SnapshotEnvelope snapshot)
        {
            if (SleepPromptOpenForCandidate(snapshot))
            {
                return new[] { "close_menu_sleep_prompt_unsupported" };
            }

            var type = ActiveMenuTypeForCandidate(snapshot);
            if (string.IsNullOrWhiteSpace(type))
            {
                return new[] { "close_menu_type_unknown" };
            }

            if (IsSafeCloseMenuType(type))
            {
                return Array.Empty<string>();
            }

            if (type == "LevelUpMenu")
            {
                return LevelUpMenuBlockReasons(snapshot);
            }

            if (type == "DialogueBox")
            {
                var dialogueReasons =
                    SafeOrdinaryDialogueBlockReasons(snapshot);
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
                return dialogueReasons;
            }

            if (type == "ShippingMenu")
            {
                return ShippingMenuBlockReasons(snapshot);
            }

            return new[] { "close_menu_type_not_whitelisted" };
        }

        private static string[] ShippingMenuBlockReasons(SnapshotEnvelope snapshot)
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

        private static string[] LevelUpMenuBlockReasons(SnapshotEnvelope snapshot)
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
            if (ReadBool(state.Value, "is_profession_chooser") == true &&
                (!state.Value.TryGetProperty("profession_choices", out var choices) ||
                 choices.ValueKind != JsonValueKind.Array ||
                 choices.GetArrayLength() != 2))
            {
                reasons.Add("level_up_menu_profession_choices_not_exactly_two");
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
            if (!string.IsNullOrWhiteSpace(lastQuestionKey))
            {
                reasons.Add("dialogue_close_last_question_key_present:" + lastQuestionKey);
            }

            if (ReadBool(activeMenu.Value, "is_sleep_prompt") == true)
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

        private static int? ReadNullableInt(JsonElement element, string property)
        {
            if (!element.TryGetProperty(property, out var value))
            {
                return null;
            }

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            {
                return number;
            }

            return null;
        }

        private static string ActiveMenuTypeForCandidate(SnapshotEnvelope snapshot)
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

        private static bool RouteActionBranchBlockedAtCandidateTile(SnapshotEnvelope snapshot, int targetX, int targetY)
        {
            var row = ReadRouteActionBranchCandidateRow(snapshot, targetX, targetY);
            return row.HasValue && ReadBool(row.Value, "route_training_blocked") == true;
        }

        private static bool TargetActionBranchMatchesForCandidate(SnapshotEnvelope snapshot, int targetX, int targetY, string expectedActionType)
        {
            var row = ReadRouteActionBranchCandidateRow(snapshot, targetX, targetY);
            return row.HasValue && string.Equals(ReadString(row.Value, "branch"), expectedActionType, StringComparison.OrdinalIgnoreCase);
        }

        private static JsonElement? ReadRouteActionBranchCandidateRow(SnapshotEnvelope snapshot, int targetX, int targetY)
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
                if (row.ValueKind == JsonValueKind.Object && ReadInt(row, "tile_x") == targetX && ReadInt(row, "tile_y") == targetY)
                {
                    return row;
                }
            }

            return null;
        }

        private static bool CollisionGridBlocksTile(SnapshotEnvelope snapshot, int x, int y)
        {
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width > 0 && height > 0 && (x < 0 || y < 0 || x >= width || y >= height))
            {
                return true;
            }

            if (!grid.Value.TryGetProperty("notable_tiles", out var notableTiles) || notableTiles.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var tile in notableTiles.EnumerateArray())
            {
                if (tile.ValueKind == JsonValueKind.Object &&
                    ReadInt(tile, "tile_x") == x &&
                    ReadInt(tile, "tile_y") == y &&
                    ReadBool(tile, "collision_blocked") == true)
                {
                    return true;
                }
            }

            return false;
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

        private sealed class CandidateTile
        {
            public CandidateTile(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }
        }

        private readonly struct MachineStandTileSelection
        {
            public MachineStandTileSelection(CandidateTile? tile, string[] blockReasons)
            {
                Tile = tile;
                BlockReasons = blockReasons;
            }

            public CandidateTile? Tile { get; }
            public string[] BlockReasons { get; }
        }

    }
}
