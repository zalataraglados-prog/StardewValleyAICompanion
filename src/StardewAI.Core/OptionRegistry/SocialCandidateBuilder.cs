using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public static class SocialCandidateBuilder
    {
        public static EventCandidate[] Build(SnapshotEnvelope snapshot, string optionId, int maxCandidates = 64)
        {
            if (optionId != "social.talk_npc" && optionId != "social.gift_npc")
            {
                return Array.Empty<EventCandidate>();
            }

            var npcs = ReadStateFieldValue(snapshot, "npcs", "social_interaction");
            if (!npcs.HasValue || npcs.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<EventCandidate>();
            }

            var results = new List<EventCandidate>();
            foreach (var npc in npcs.Value.EnumerateArray())
            {
                if (npc.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (optionId == "social.talk_npc")
                {
                    var candidate = TalkCandidate(snapshot, npc);
                    if (candidate is not null)
                    {
                        results.Add(candidate);
                    }
                }
                else
                {
                    results.AddRange(GiftCandidates(snapshot, npc));
                }
            }

            var playerLocationId = ReadStateFieldString(snapshot, "player", "location_id");
            return results
                .OrderByDescending(candidate => candidate.Available)
                .ThenByDescending(candidate => string.Equals(candidate.LocationId, playerLocationId, StringComparison.Ordinal))
                .ThenBy(candidate => candidate.LocationId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.TileY ?? 0)
                .ThenBy(candidate => candidate.TileX ?? 0)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Take(Math.Max(1, maxCandidates))
                .ToArray();
        }

        public static EventCandidate? FindMatching(SnapshotEnvelope snapshot, SmallModelAction action)
        {
            return Build(snapshot, action.OptionId).FirstOrDefault(candidate => candidate.Available && ParametersMatch(candidate, action));
        }

        private static EventCandidate? TalkCandidate(SnapshotEnvelope snapshot, JsonElement npc)
        {
            var reasons = BaseNpcBlockReasons(snapshot, npc, requireGift: false);
            var npcName = ReadString(npc, "name");
            if (string.IsNullOrWhiteSpace(npcName))
            {
                reasons.Add("social_npc_identity_missing");
            }

            var tileX = ReadInt(npc, "tile_x");
            var tileY = ReadInt(npc, "tile_y");
            var npcLocationId = ReadString(npc, "location_id") ?? "";
            var standTile = SelectReachableStandTile(snapshot, tileX, tileY, npcLocationId);
            reasons.AddRange(standTile.BlockReasons);
            var hasValidStand = standTile.RouteDistance >= 0;
            var routeDistanceTicks = hasValidStand ? standTile.RouteDistance * 12 : -1;
            var plannerBudgetTicks = 120;
            var estimatedTicks = hasValidStand ? routeDistanceTicks + plannerBudgetTicks : -1;
            return new EventCandidate
            {
                CandidateId = "social:talk:" + npcName,
                Kind = "social_talk_current",
                Available = reasons.Count == 0,
                EstimatedTicks = estimatedTicks,
                EnergyCost = 0,
                LocationId = ReadString(npc, "location_id"),
                TileX = standTile.Tile?.X,
                TileY = standTile.Tile?.Y,
                ExpectedEffect = "native_social_talk_target=" + npcName + ";executor_required=social_native_executor.v1;estimated_duration_ticks=" + estimatedTicks + ";duration_estimate_status=planner_budget_assumption_pending_runtime_calibration",
                AvailabilityClass = reasons.Count == 0 ? "current_state_complete" : "current_state_blocked_with_diagnostics",
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = new[]
                {
                    Parameter("npc_name", npcName),
                    Parameter("target_location", ReadString(npc, "location_id")),
                    Parameter("npc_tile_x", tileX.ToString()),
                    Parameter("npc_tile_y", tileY.ToString()),
                    Parameter("stand_tile_x", standTile.Tile?.X.ToString() ?? string.Empty),
                    Parameter("stand_tile_y", standTile.Tile?.Y.ToString() ?? string.Empty),
                    Parameter("route_distance_tiles", standTile.RouteDistance.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    Parameter("route_distance_ticks", routeDistanceTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    Parameter("native_interaction_planner_budget_ticks", plannerBudgetTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    Parameter("expected_talked_to_today_before", FriendshipBool(snapshot, npcName, "talked_to_today").ToString().ToLowerInvariant()),
                    Parameter("schedule_loaded_for_evidential_provenance", ReadBool(npc, "schedule_loaded").ToString().ToLowerInvariant()),
                    Parameter("social_legality_evidence", "npcs.social_interaction")
                }
            };
        }

        private static IEnumerable<EventCandidate> GiftCandidates(SnapshotEnvelope snapshot, JsonElement npc)
        {
            var baseReasons = BaseNpcBlockReasons(snapshot, npc, requireGift: true);
            var npcName = ReadString(npc, "name");
            if (string.IsNullOrWhiteSpace(npcName))
            {
                baseReasons.Add("social_npc_identity_missing");
            }

            var friendship = Friendship(snapshot, npcName);
            var giftLimitExempt = string.Equals(ReadStateFieldString(snapshot, "player", "spouse"), npcName, StringComparison.Ordinal) ||
                ReadBool(npc, "is_child") ||
                ReadBool(npc, "is_birthday");
            if (friendship.HasValue)
            {
                if (ReadBool(friendship.Value, "is_divorced"))
                {
                    baseReasons.Add("social_gift_divorced_rejected");
                }
            }

            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (!inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            foreach (var item in inventory.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || ReadBool(item, "is_empty"))
                {
                    continue;
                }

                var reasons = new List<string>(baseReasons);
                AddItemBlockReasons(item, reasons);
                var qualifiedItemId = ReadString(item, "qualified_item_id");
                var isStardropTea = string.Equals(qualifiedItemId, "(O)StardropTea", StringComparison.OrdinalIgnoreCase);
                if (friendship.HasValue)
                {
                    if (ReadInt(friendship.Value, "gifts_today") >= 1 && !isStardropTea)
                    {
                        reasons.Add("social_gift_daily_limit_exhausted");
                    }
                    if (ReadInt(friendship.Value, "gifts_this_week") >= 2 && !giftLimitExempt && !isStardropTea)
                    {
                        reasons.Add("social_gift_weekly_limit_exhausted");
                    }
                }
                if (HasDumpedDialogueEvent(snapshot))
                {
                    reasons.Add("social_gift_dumped_dialogue_rejection");
                }
                if (ReadStateFieldBool(snapshot, "time", "is_green_rain") && ReadStateFieldInt(snapshot, "time", "year") == 1 && !ReadBool(friendship, "is_married"))
                {
                    reasons.Add("social_gift_green_rain_year_one_rejection");
                }
                var taste = GiftTaste(snapshot, npcName, ReadInt(item, "slot_index"), ReadString(item, "qualified_item_id"), ReadInt(item, "quality"));
                if (!taste.HasValue)
                {
                    reasons.Add("social_gift_taste_incomplete");
                }
                else if (!ReadBool(taste.Value, "expected_friendship_delta_complete"))
                {
                    reasons.Add("social_gift_delta_incomplete");
                }

                var tileX = ReadInt(npc, "tile_x");
                var tileY = ReadInt(npc, "tile_y");
                var npcLocationId = ReadString(npc, "location_id") ?? "";
                var standTile = SelectReachableStandTile(snapshot, tileX, tileY, npcLocationId);
                reasons.AddRange(standTile.BlockReasons);
                var slotIndex = ReadInt(item, "slot_index");
                var hasValidStand = standTile.RouteDistance >= 0;
                var routeDistanceTicks = hasValidStand ? standTile.RouteDistance * 12 : -1;
                var plannerBudgetTicks = 120;
                var estimatedTicks = hasValidStand ? routeDistanceTicks + plannerBudgetTicks : -1;
                yield return new EventCandidate
                {
                    CandidateId = "social:gift:" + npcName + ":slot:" + slotIndex + ":" + qualifiedItemId,
                    Kind = "social_gift_current",
                    Available = reasons.Count == 0,
                    EstimatedTicks = estimatedTicks,
                    EnergyCost = 0,
                    LocationId = ReadString(npc, "location_id"),
                    TileX = standTile.Tile?.X,
                    TileY = standTile.Tile?.Y,
                    QualifiedItemId = qualifiedItemId,
                    ItemId = ReadString(item, "item_id"),
                    SlotIndex = slotIndex,
                    Quantity = 1,
                    ExpectedEffect = "native_social_gift_target=" + npcName + ";slot=" + slotIndex + ";item=" + qualifiedItemId + ";executor_required=social_native_executor.v1;estimated_duration_ticks=" + estimatedTicks + ";duration_estimate_status=planner_budget_assumption_pending_runtime_calibration",
                    AvailabilityClass = reasons.Count == 0 ? "current_state_complete" : "current_state_blocked_with_diagnostics",
                    BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = new[]
                    {
                        Parameter("npc_name", npcName),
                        Parameter("slot_index", slotIndex.ToString()),
                        Parameter("qualified_item_id", qualifiedItemId),
                        Parameter("item_quality", ReadInt(item, "quality").ToString()),
                        Parameter("item_stack_before", ReadInt(item, "stack").ToString()),
                        Parameter("gift_taste", taste.HasValue ? ReadString(taste.Value, "taste") : string.Empty),
                        Parameter("expected_friendship_delta", taste.HasValue ? ReadString(taste.Value, "expected_friendship_delta") : string.Empty),
                        Parameter("friendship_row_exists_before", friendship.HasValue.ToString().ToLowerInvariant()),
                        Parameter("gift_updates_normal_limits", (!isStardropTea).ToString().ToLowerInvariant()),
                        Parameter("gift_side_effect_risk", GiftSideEffectRisk(snapshot, npc, npcName, isStardropTea)),
                        Parameter("schedule_loaded_for_evidential_provenance", ReadBool(npc, "schedule_loaded").ToString().ToLowerInvariant()),
                        Parameter("target_location", ReadString(npc, "location_id")),
                        Parameter("npc_tile_x", tileX.ToString()),
                        Parameter("npc_tile_y", tileY.ToString()),
                        Parameter("stand_tile_x", standTile.Tile?.X.ToString() ?? string.Empty),
                        Parameter("stand_tile_y", standTile.Tile?.Y.ToString() ?? string.Empty),
                        Parameter("route_distance_tiles", standTile.RouteDistance.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        Parameter("route_distance_ticks", routeDistanceTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    Parameter("native_interaction_planner_budget_ticks", plannerBudgetTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        Parameter("social_legality_evidence", "npcs.social_interaction;npcs.friendships;npcs.gift_tastes;player.inventory")
                    }
                };
            }
        }

        private static List<string> BaseNpcBlockReasons(SnapshotEnvelope snapshot, JsonElement npc, bool requireGift)
        {
            var reasons = new List<string>();
            if (!ReadBool(npc, "master_data_present"))
            {
                reasons.Add("social_npc_master_data_missing");
            }
            if (!ReadBool(npc, "current_instance_loaded"))
            {
                reasons.Add("social_npc_not_loaded_currently");
            }
            if (!ReadBool(npc, "current_route_window_complete"))
            {
                reasons.Add("social_current_route_window_incomplete");
            }
            if (!ReadBool(npc, "can_socialize_complete"))
            {
                reasons.Add("social_can_socialize_incomplete");
            }
            else if (!ReadBool(npc, "can_socialize"))
            {
                reasons.Add("social_can_socialize_false");
            }
            if (requireGift)
            {
                if (!ReadBool(npc, "can_receive_gifts_complete"))
                {
                    reasons.Add("social_can_receive_gifts_incomplete");
                }
                else if (!ReadBool(npc, "can_receive_gifts"))
                {
                    reasons.Add("social_can_receive_gifts_false");
                }
            }
            if (ReadBool(npc, "is_sleeping"))
            {
                reasons.Add("social_npc_sleeping");
            }
            if (ReadBool(npc, "is_invisible"))
            {
                reasons.Add("social_npc_invisible");
            }
            if (ReadBool(npc, "simple_non_villager_npc"))
            {
                reasons.Add("social_simple_non_villager_branch_unsupported");
            }
            AddSpecialNpcBlockReasons(ReadString(npc, "name"), reasons);
            if (!string.Equals(ReadString(npc, "location_id"), ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.Ordinal))
            {
                reasons.Add("social_npc_not_in_player_location");
            }
            if (ActiveMenuOpen(snapshot))
            {
                reasons.Add("social_menu_must_be_clear");
            }
            if (!requireGift && !string.IsNullOrWhiteSpace(ReadStateFieldString(snapshot, "player", "active_object_qualified_id")))
            {
                reasons.Add("social_talk_active_object_must_be_cleared_first");
            }

            return reasons;
        }

        private static void AddSpecialNpcBlockReasons(string npcName, List<string> reasons)
        {
            if (npcName is "Henchman" or "Krobus" or "Dwarf" or "Bouncer" or "Leo" or "Fizz")
            {
                reasons.Add("social_special_npc_check_action_branch_unsupported");
            }
        }

        private static void AddItemBlockReasons(JsonElement item, List<string> reasons)
        {
            if (!ReadBool(item, "is_object"))
            {
                reasons.Add("social_gift_item_not_object");
            }
            if (ReadInt(item, "stack") <= 0)
            {
                reasons.Add("social_gift_item_stack_empty");
            }
            if (ReadBool(item, "protected_from_auto_sell"))
            {
                reasons.Add("social_gift_protected_item");
            }
            if (ReadBool(item, "object_quest_item") || string.Equals(ReadString(item, "object_type"), "Quest", StringComparison.OrdinalIgnoreCase))
            {
                reasons.Add("social_gift_quest_delivery_ambiguous");
            }
            if (ReadBool(item, "object_big_craftable") || ReadBool(item, "is_furniture") || ReadBool(item, "is_wallpaper"))
            {
                reasons.Add("social_gift_item_not_giftable_shape");
            }
            if (!item.TryGetProperty("can_be_given_as_gift", out var canGift) || canGift.ValueKind != JsonValueKind.True && canGift.ValueKind != JsonValueKind.False)
            {
                reasons.Add("social_gift_item_can_be_given_missing_or_malformed");
            }
            else if (canGift.ValueKind == JsonValueKind.False)
            {
                reasons.Add("social_gift_item_can_be_given_false");
            }
            if (!item.TryGetProperty("base_tag_not_giftable", out var baseNotGiftable) || baseNotGiftable.ValueKind != JsonValueKind.True && baseNotGiftable.ValueKind != JsonValueKind.False)
            {
                reasons.Add("social_gift_item_base_tag_not_giftable_missing_or_malformed");
            }
            else if (baseNotGiftable.ValueKind == JsonValueKind.True)
            {
                reasons.Add("social_gift_item_base_tag_not_giftable");
            }
            if (ReadBool(item, "special_item"))
            {
                reasons.Add("social_gift_special_item_branch_unsupported");
            }
            if (!item.TryGetProperty("context_tags", out var contextTags) || contextTags.ValueKind != JsonValueKind.Array)
            {
                reasons.Add("social_gift_context_tags_incomplete");
            }
            var qualifiedItemId = ReadString(item, "qualified_item_id");
            if (qualifiedItemId is "(O)233" or "(O)897" or "(O)71" or "(O)864" or "(O)865" or "(O)866" or "(O)867" or "(O)868" or "(O)869" or "(O)870" or "(O)809" or "(O)458" or "(O)277" or "(O)460")
            {
                reasons.Add("social_gift_special_switch_item_branch_unsupported");
            }
            if (HasContextTagPrefix(item, "propose_roommate_"))
            {
                reasons.Add("social_gift_roommate_proposal_context_branch_unsupported");
            }
        }

        private static bool HasContextTagPrefix(JsonElement item, string prefix)
        {
            if (!item.TryGetProperty("context_tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var tag in tags.EnumerateArray())
            {
                if (tag.ValueKind == JsonValueKind.String && (tag.GetString() ?? string.Empty).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GiftSideEffectRisk(SnapshotEnvelope snapshot, JsonElement npc, string npcName, bool isStardropTea)
        {
            var spouse = ReadStateFieldString(snapshot, "player", "spouse");
            if (isStardropTea || string.IsNullOrWhiteSpace(spouse) || string.Equals(spouse, npcName, StringComparison.Ordinal) || !ReadBool(npc, "is_datably_flagged"))
            {
                return "none_identified_from_transparent_branch";
            }

            return "spouse_jealousy_stochastic_side_effect_possible_not_in_target_delta";
        }

        private static bool ParametersMatch(EventCandidate candidate, SmallModelAction action)
        {
            var npcName = CandidateParameter(candidate, "npc_name");
            var requestedNpc = ReadParameter(action, "npc_name") ?? ReadParameter(action, "target_npc") ?? string.Empty;
            if (!string.Equals(npcName, requestedNpc, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (action.OptionId == "social.talk_npc")
            {
                return true;
            }

            var requestedSlot = ReadParameter(action, "slot_index");
            var requestedItem = ReadParameter(action, "qualified_item_id");
            return !string.IsNullOrWhiteSpace(requestedSlot) &&
                !string.IsNullOrWhiteSpace(requestedItem) &&
                string.Equals(CandidateParameter(candidate, "slot_index"), requestedSlot, StringComparison.Ordinal) &&
                string.Equals(CandidateParameter(candidate, "qualified_item_id"), requestedItem, StringComparison.OrdinalIgnoreCase);
        }

        private static JsonElement? Friendship(SnapshotEnvelope snapshot, string npcName)
        {
            var friendships = ReadStateFieldValue(snapshot, "npcs", "friendships");
            if (!friendships.HasValue || friendships.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var friendship in friendships.Value.EnumerateArray())
            {
                if (friendship.ValueKind == JsonValueKind.Object && string.Equals(ReadString(friendship, "npc_name"), npcName, StringComparison.OrdinalIgnoreCase))
                {
                    return friendship;
                }
            }

            return null;
        }

        private static bool FriendshipBool(SnapshotEnvelope snapshot, string npcName, string property)
        {
            var friendship = Friendship(snapshot, npcName);
            return friendship.HasValue && ReadBool(friendship.Value, property);
        }

        private static JsonElement? GiftTaste(SnapshotEnvelope snapshot, string npcName, int slotIndex, string qualifiedItemId, int quality)
        {
            var tastes = ReadStateFieldValue(snapshot, "npcs", "gift_tastes");
            if (!tastes.HasValue || tastes.Value.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var taste in tastes.Value.EnumerateArray())
            {
                if (taste.ValueKind == JsonValueKind.Object &&
                    string.Equals(ReadString(taste, "npc_name"), npcName, StringComparison.OrdinalIgnoreCase) &&
                    ReadInt(taste, "slot_index") == slotIndex &&
                    string.Equals(ReadString(taste, "qualified_item_id"), qualifiedItemId, StringComparison.OrdinalIgnoreCase) &&
                    ReadInt(taste, "quality") == quality &&
                    ReadBool(taste, "complete"))
                {
                    return taste;
                }
            }

            return null;
        }

        private static bool HasDumpedDialogueEvent(SnapshotEnvelope snapshot)
        {
            var events = ReadStateFieldValue(snapshot, "player", "active_dialogue_events");
            if (!events.HasValue || events.Value.ValueKind != JsonValueKind.Array)
            {
                return true;
            }

            foreach (var item in events.Value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && (item.GetString() ?? string.Empty).Contains("dumped", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static StandTileSelection SelectReachableStandTile(SnapshotEnvelope snapshot, int targetX, int targetY, string npcLocationId)
        {
            var playerLocationId = ReadStateFieldString(snapshot, "player", "location_id");
            if (!string.Equals(npcLocationId, playerLocationId, StringComparison.Ordinal))
            {
                return new StandTileSelection(null, -1, new[] { "social_npc_not_in_player_location_stand_skipped" });
            }

            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            var grid = ReadStateFieldValue(snapshot, "locations", "collision_grid");
            if (!grid.HasValue || grid.Value.ValueKind != JsonValueKind.Object)
            {
                return new StandTileSelection(null, -1, new[] { "social_route_collision_grid_unavailable" });
            }

            var width = ReadInt(grid.Value, "width");
            var height = ReadInt(grid.Value, "height");
            if (width <= 0 || height <= 0)
            {
                return new StandTileSelection(null, -1, new[] { "social_route_collision_grid_incomplete" });
            }

            var blocked = ReadBlockedCollisionTileKeys(grid.Value, width, height);
            if (blocked is null)
            {
                return new StandTileSelection(null, -1, new[] { "social_route_collision_grid_incomplete" });
            }
            var unsupported = ReadUnsupportedRouteActionTileKeys(snapshot, width, height);
            if (unsupported is null)
            {
                return new StandTileSelection(null, -1, new[] { "social_route_action_coverage_incomplete" });
            }
            var legalAdjacent = new[] { new CandidateTile(targetX + 1, targetY), new CandidateTile(targetX - 1, targetY), new CandidateTile(targetX, targetY + 1), new CandidateTile(targetX, targetY - 1) }
                .Where(tile => TileInBounds(tile.X, tile.Y, width, height))
                .Where(tile => !blocked.Contains(TileKey(tile.X, tile.Y)))
                .ToArray();

            var bestDistance = int.MaxValue;
            CandidateTile? bestTile = null;
            foreach (var tile in legalAdjacent)
            {
                var distance = BfsDistance(playerX, playerY, tile.X, tile.Y, width, height, blocked, unsupported);
                if (distance < 0)
                {
                    continue;
                }
                if (distance < bestDistance || (distance == bestDistance && bestTile is not null && new TileComparer().Compare(tile, bestTile) < 0))
                {
                    bestDistance = distance;
                    bestTile = tile;
                }
            }

            if (bestTile is not null)
            {
                return new StandTileSelection(bestTile, bestDistance, Array.Empty<string>());
            }

            return new StandTileSelection(null, -1, new[] { "social_no_reachable_adjacent_stand_tile" });
        }

        private static int BfsDistance(int startX, int startY, int targetX, int targetY, int width, int height, HashSet<string> blockedTiles, HashSet<string> extraBlockedTiles)
        {
            if (!TileInBounds(startX, startY, width, height) || !TileInBounds(targetX, targetY, width, height))
            {
                return -1;
            }

            var startKey = TileKey(startX, startY);
            var targetKey = TileKey(targetX, targetY);
            if (blockedTiles.Contains(startKey) || blockedTiles.Contains(targetKey) || extraBlockedTiles.Contains(targetKey))
            {
                return -1;
            }

            var queue = new Queue<CandidateTile>();
            var seen = new HashSet<string>(StringComparer.Ordinal) { startKey };
            var distance = new Dictionary<string, int>(StringComparer.Ordinal) { [startKey] = 0 };
            queue.Enqueue(new CandidateTile(startX, startY));
            while (queue.Count > 0)
            {
                var tile = queue.Dequeue();
                var tileKey = TileKey(tile.X, tile.Y);
                if (tileKey == targetKey)
                {
                    return distance[tileKey];
                }

                var nextDist = distance[tileKey] + 1;
                foreach (var next in Neighbors(tile.X, tile.Y))
                {
                    var key = TileKey(next.X, next.Y);
                    if (!TileInBounds(next.X, next.Y, width, height) || blockedTiles.Contains(key) || extraBlockedTiles.Contains(key) || !seen.Add(key))
                    {
                        continue;
                    }

                    distance[key] = nextDist;
                    queue.Enqueue(next);
                }
            }

            return -1;
        }

        private static HashSet<string>? ReadBlockedCollisionTileKeys(JsonElement collisionGrid, int width, int height)
        {
            var blockedTiles = new HashSet<string>(StringComparer.Ordinal);
            if (!collisionGrid.TryGetProperty("notable_tiles", out var notableTiles) || notableTiles.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var tile in notableTiles.EnumerateArray())
            {
                if (!TryReadTileCoordinate(tile, width, height, out var x, out var y))
                {
                    return null;
                }

                if (!TryReadBool(tile, "collision_blocked", out var isBlocked))
                {
                    return null;
                }

                if (isBlocked)
                {
                    blockedTiles.Add(TileKey(x, y));
                }
            }

            return blockedTiles;
        }

        private static HashSet<string>? ReadUnsupportedRouteActionTileKeys(SnapshotEnvelope snapshot, int width, int height)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var coverage = ReadStateFieldValue(snapshot, "locations", "route_action_branch_coverage");
            if (!coverage.HasValue || coverage.Value.ValueKind != JsonValueKind.Object ||
                !coverage.Value.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var row in rows.EnumerateArray())
            {
                if (!TryReadTileCoordinate(row, width, height, out var x, out var y))
                {
                    return null;
                }

                if (!TryReadBool(row, "route_training_blocked", out var isTrainingBlocked))
                {
                    return null;
                }

                if (isTrainingBlocked)
                {
                    result.Add(TileKey(x, y));
                }
            }

            return result;
        }

        private static bool PathExists(int startX, int startY, int targetX, int targetY, int width, int height, HashSet<string> blockedTiles, HashSet<string> extraBlockedTiles)
        {
            if (!TileInBounds(startX, startY, width, height) || !TileInBounds(targetX, targetY, width, height))
            {
                return false;
            }

            var startKey = TileKey(startX, startY);
            var targetKey = TileKey(targetX, targetY);
            if (blockedTiles.Contains(startKey) || blockedTiles.Contains(targetKey) || extraBlockedTiles.Contains(targetKey))
            {
                return false;
            }

            var queue = new Queue<CandidateTile>();
            var seen = new HashSet<string>(StringComparer.Ordinal) { startKey };
            queue.Enqueue(new CandidateTile(startX, startY));
            while (queue.Count > 0)
            {
                var tile = queue.Dequeue();
                if (TileKey(tile.X, tile.Y) == targetKey)
                {
                    return true;
                }

                foreach (var next in Neighbors(tile.X, tile.Y))
                {
                    var key = TileKey(next.X, next.Y);
                    if (!TileInBounds(next.X, next.Y, width, height) || blockedTiles.Contains(key) || extraBlockedTiles.Contains(key) || !seen.Add(key))
                    {
                        continue;
                    }

                    queue.Enqueue(next);
                }
            }

            return false;
        }

        private static IEnumerable<CandidateTile> Neighbors(int x, int y)
        {
            yield return new CandidateTile(x + 1, y);
            yield return new CandidateTile(x - 1, y);
            yield return new CandidateTile(x, y + 1);
            yield return new CandidateTile(x, y - 1);
        }

        private static bool TileInBounds(int x, int y, int width, int height)
        {
            return x >= 0 && y >= 0 && x < width && y < height;
        }

        private static bool TryReadTileCoordinate(JsonElement row, int width, int height, out int x, out int y)
        {
            x = 0;
            y = 0;
            if (row.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!row.TryGetProperty("tile_x", out var xVal) || xVal.ValueKind != JsonValueKind.Number || !xVal.TryGetInt32(out x))
            {
                return false;
            }

            if (!row.TryGetProperty("tile_y", out var yVal) || yVal.ValueKind != JsonValueKind.Number || !yVal.TryGetInt32(out y))
            {
                return false;
            }

            return TileInBounds(x, y, width, height);
        }

        private static string TileKey(int x, int y)
        {
            return x.ToString() + "," + y.ToString();
        }

        private static bool ActiveMenuOpen(SnapshotEnvelope snapshot)
        {
            var activeMenu = ReadStateFieldValue(snapshot, "menus", "active_menu");
            if (!activeMenu.HasValue)
            {
                return true;
            }

            if (activeMenu.Value.ValueKind == JsonValueKind.String)
            {
                return !string.Equals(activeMenu.Value.GetString(), "none", StringComparison.OrdinalIgnoreCase);
            }

            return activeMenu.Value.ValueKind != JsonValueKind.Object || ReadBool(activeMenu.Value, "is_open");
        }

        public static string CandidateParameter(EventCandidate candidate, string name)
        {
            return candidate.Parameters.FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))?.Value ?? string.Empty;
        }

        private static bool TryReadBool(JsonElement item, string property, out bool result)
        {
            if (item.TryGetProperty(property, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                result = value.ValueKind == JsonValueKind.True;
                return true;
            }
            result = false;
            return false;
        }

        private static bool ReadBool(JsonElement? item, string property)
        {
            return item.HasValue && Infrastructure.SnapshotValueReader.ReadBool(item.Value, property);
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

        private sealed class TileComparer : IComparer<CandidateTile>
        {
            public int Compare(CandidateTile? a, CandidateTile? b)
            {
                if (a is null && b is null) return 0;
                if (a is null) return -1;
                if (b is null) return 1;
                var yCompare = a.Y.CompareTo(b.Y);
                if (yCompare != 0) return yCompare;
                return a.X.CompareTo(b.X);
            }
        }

        private readonly struct StandTileSelection
        {
            public StandTileSelection(CandidateTile? tile, int routeDistance, string[] blockReasons)
            {
                Tile = tile;
                RouteDistance = routeDistance;
                BlockReasons = blockReasons;
            }

            public CandidateTile? Tile { get; }
            public int RouteDistance { get; }
            public string[] BlockReasons { get; }
        }
    }
}
