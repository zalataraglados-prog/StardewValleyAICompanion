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
            if (optionId != "social.talk_npc" &&
                optionId != "social.gift_npc" &&
                optionId != "social.advance_partnership")
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
                else if (optionId == "social.gift_npc")
                {
                    results.AddRange(GiftCandidates(snapshot, npc));
                }
                else
                {
                    results.AddRange(PartnershipCandidates(snapshot, npc));
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

        private static IEnumerable<EventCandidate> PartnershipCandidates(SnapshotEnvelope snapshot, JsonElement npc)
        {
            var npcName = ReadString(npc, "name");
            var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
            if (string.IsNullOrWhiteSpace(npcName) || !inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            var friendship = Friendship(snapshot, npcName);
            foreach (var item in inventory.Value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object || ReadBool(item, "is_empty"))
                {
                    continue;
                }

                var qualifiedItemId = ReadString(item, "qualified_item_id");
                var actionKind = qualifiedItemId == "(O)458"
                    ? "bouquet"
                    : qualifiedItemId == "(O)460"
                        ? "propose_marriage"
                        : HasExactContextTag(item, "propose_roommate_" + npcName)
                            ? "propose_roommate"
                            : string.Empty;
                if (string.IsNullOrWhiteSpace(actionKind))
                {
                    continue;
                }

                var reasons = PartnershipBaseBlockReasons(snapshot, npc, item);
                var points = friendship.HasValue ? ReadInt(friendship.Value, "points") : 0;
                var playerMarriageKnown = TryReadStateBool(snapshot, "player", "married_or_roommate", out var playerMarriedOrRoommate);
                var playerEngagementKnown = TryReadStateBool(snapshot, "player", "engaged", out var playerEngaged);
                var farmhouseUpgradeLevel = ReadStateFieldIntOptional(snapshot, "player", "farmhouse_upgrade_level");
                var playerCommitted = playerMarriedOrRoommate || playerEngaged;
                if (!friendship.HasValue)
                {
                    reasons.Add("partnership_friendship_row_missing");
                }

                if (actionKind == "bouquet")
                {
                    if (!TryReadBool(npc, "is_datably_flagged", out _)) reasons.Add("partnership_target_datable_fact_incomplete");
                    if (!ReadBool(npc, "is_datably_flagged")) reasons.Add("partnership_target_not_datable");
                    if (ReadBool(npc, "is_married_or_engaged")) reasons.Add("partnership_target_already_committed");
                    if (friendship.HasValue && !TryReadBool(friendship.Value, "is_dating", out _)) reasons.Add("partnership_dating_fact_incomplete");
                    if (friendship.HasValue && !TryReadBool(friendship.Value, "is_divorced", out _)) reasons.Add("partnership_divorced_fact_incomplete");
                    if (ReadBool(friendship, "is_dating")) reasons.Add("partnership_already_dating");
                    if (ReadBool(friendship, "is_divorced")) reasons.Add("partnership_divorced_target_rejects_bouquet");
                    if (points < 2000) reasons.Add("partnership_bouquet_requires_2000_points");
                }
                else if (actionKind == "propose_marriage")
                {
                    if (!TryReadBool(npc, "is_datably_flagged", out _)) reasons.Add("partnership_target_datable_fact_incomplete");
                    if (!ReadBool(npc, "is_datably_flagged")) reasons.Add("partnership_target_not_datable");
                    if (ReadBool(npc, "is_married_or_engaged")) reasons.Add("partnership_target_already_committed");
                    if (friendship.HasValue && !TryReadBool(friendship.Value, "is_divorced", out _)) reasons.Add("partnership_divorced_fact_incomplete");
                    if (ReadBool(friendship, "is_divorced")) reasons.Add("partnership_divorced_target_rejects_proposal");
                    if (points < 2500) reasons.Add("partnership_marriage_proposal_requires_2500_points");
                    if (!playerMarriageKnown || !playerEngagementKnown) reasons.Add("partnership_player_commitment_fact_incomplete");
                    if (playerCommitted) reasons.Add("partnership_player_already_committed");
                    if (!farmhouseUpgradeLevel.HasValue) reasons.Add("partnership_farmhouse_upgrade_fact_incomplete");
                    else if (farmhouseUpgradeLevel.Value < 1)
                    {
                        reasons.Add("partnership_proposal_requires_house_upgrade");
                    }
                }
                else
                {
                    if (!string.Equals(npcName, "Krobus", StringComparison.Ordinal))
                    {
                        reasons.Add("partnership_vanilla_roommate_target_must_be_krobus");
                    }
                    if (ReadBool(npc, "is_married_or_engaged")) reasons.Add("partnership_target_already_committed");
                    if (points < 2500) reasons.Add("partnership_roommate_proposal_requires_10_hearts");
                    if (!playerMarriageKnown || !playerEngagementKnown) reasons.Add("partnership_player_commitment_fact_incomplete");
                    if (playerCommitted) reasons.Add("partnership_player_already_committed");
                    if (!farmhouseUpgradeLevel.HasValue) reasons.Add("partnership_farmhouse_upgrade_fact_incomplete");
                    else if (farmhouseUpgradeLevel.Value < 1)
                    {
                        reasons.Add("partnership_proposal_requires_house_upgrade");
                    }
                }

                var tileX = ReadInt(npc, "tile_x");
                var tileY = ReadInt(npc, "tile_y");
                var locationId = ReadString(npc, "location_id") ?? string.Empty;
                var standTile = SelectReachableStandTile(snapshot, tileX, tileY, locationId);
                reasons.AddRange(standTile.BlockReasons);
                var routeDistanceTicks = standTile.RouteDistance >= 0 ? standTile.RouteDistance * 12 : -1;
                var estimatedTicks = routeDistanceTicks >= 0 ? routeDistanceTicks + 120 : -1;
                var slotIndex = ReadInt(item, "slot_index");
                yield return new EventCandidate
                {
                    CandidateId = "partnership:" + actionKind + ":" + npcName + ":slot:" + slotIndex,
                    Kind = PartnershipCandidateKind(actionKind),
                    Available = reasons.Count == 0,
                    EstimatedTicks = estimatedTicks,
                    EnergyCost = 0,
                    LocationId = locationId,
                    TileX = standTile.Tile?.X,
                    TileY = standTile.Tile?.Y,
                    QualifiedItemId = qualifiedItemId,
                    ItemId = ReadString(item, "item_id"),
                    SlotIndex = slotIndex,
                    Quantity = 1,
                    ExpectedEffect = actionKind == "bouquet"
                        ? "friendship_status=Dating;relationship_item_consumed=1"
                        : "friendship_status=Engaged;spouse=" + npcName + ";wedding_date_scheduled=true;roommate_marriage=" + (actionKind == "propose_roommate").ToString().ToLowerInvariant() + ";relationship_item_consumed=1",
                    AvailabilityClass = reasons.Count == 0 ? "current_state_complete" : "current_state_blocked_with_diagnostics",
                    BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = new[]
                    {
                        Parameter("npc_name", npcName),
                        Parameter("partnership_action_kind", actionKind),
                        Parameter("slot_index", slotIndex.ToString()),
                        Parameter("qualified_item_id", qualifiedItemId),
                        Parameter("item_stack_before", ReadInt(item, "stack").ToString()),
                        Parameter("friendship_points_before", points.ToString()),
                        Parameter("friendship_status_before", friendship.HasValue ? ReadString(friendship.Value, "status") : string.Empty),
                        Parameter("expected_friendship_status_after", actionKind == "bouquet" ? "Dating" : "Engaged"),
                        Parameter("expected_roommate_marriage_after", (actionKind == "propose_roommate").ToString().ToLowerInvariant()),
                        Parameter("target_location", locationId),
                        Parameter("npc_tile_x", tileX.ToString()),
                        Parameter("npc_tile_y", tileY.ToString()),
                        Parameter("stand_tile_x", standTile.Tile?.X.ToString() ?? string.Empty),
                        Parameter("stand_tile_y", standTile.Tile?.Y.ToString() ?? string.Empty),
                        Parameter("route_distance_tiles", standTile.RouteDistance.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        Parameter("route_distance_ticks", routeDistanceTicks.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        Parameter("native_interaction_planner_budget_ticks", "120"),
                        Parameter("social_legality_evidence", "player.inventory;player.married_or_roommate;player.engaged;player.farmhouse_upgrade_level;npcs.social_interaction;npcs.friendships")
                    }
                };
            }
        }

        private static List<string> PartnershipBaseBlockReasons(SnapshotEnvelope snapshot, JsonElement npc, JsonElement item)
        {
            var reasons = new List<string>();
            if (!ReadBool(npc, "master_data_present")) reasons.Add("partnership_npc_master_data_missing");
            if (!ReadBool(npc, "current_instance_loaded")) reasons.Add("partnership_npc_not_loaded_currently");
            if (!ReadBool(npc, "current_route_window_complete")) reasons.Add("partnership_current_route_window_incomplete");
            if (!ReadBool(npc, "can_socialize_complete")) reasons.Add("partnership_can_socialize_incomplete");
            else if (!ReadBool(npc, "can_socialize")) reasons.Add("partnership_can_socialize_false");
            if (ReadBool(npc, "is_sleeping")) reasons.Add("partnership_npc_sleeping");
            if (ReadBool(npc, "is_invisible")) reasons.Add("partnership_npc_invisible");
            if (ReadBool(npc, "simple_non_villager_npc")) reasons.Add("partnership_simple_non_villager_unsupported");
            if (!TryReadBool(npc, "is_married_or_engaged", out _)) reasons.Add("partnership_target_commitment_fact_incomplete");
            if (!string.Equals(ReadString(npc, "location_id"), ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.Ordinal))
            {
                reasons.Add("partnership_npc_not_in_player_location");
            }
            if (ActiveMenuOpen(snapshot)) reasons.Add("partnership_menu_must_be_clear");
            if (!ReadBool(item, "is_object")) reasons.Add("partnership_item_not_object");
            if (ReadInt(item, "stack") <= 0) reasons.Add("partnership_item_stack_empty");
            if (!item.TryGetProperty("context_tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
            {
                reasons.Add("partnership_item_context_tags_incomplete");
            }
            return reasons;
        }

        private static bool HasExactContextTag(JsonElement item, string expected)
        {
            if (!item.TryGetProperty("context_tags", out var tags) || tags.ValueKind != JsonValueKind.Array)
            {
                return false;
            }
            return tags.EnumerateArray().Any(tag =>
                tag.ValueKind == JsonValueKind.String &&
                string.Equals(tag.GetString(), expected, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryReadStateBool(SnapshotEnvelope snapshot, string section, string field, out bool value)
        {
            var stateValue = ReadStateFieldValue(snapshot, section, field);
            if (stateValue.HasValue && stateValue.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                value = stateValue.Value.GetBoolean();
                return true;
            }

            value = false;
            return false;
        }

        private static string PartnershipCandidateKind(string actionKind)
        {
            return actionKind switch
            {
                "bouquet" => "partnership_bouquet_current",
                "propose_marriage" => "partnership_propose_marriage_current",
                "propose_roommate" => "partnership_propose_roommate_current",
                _ => string.Empty
            };
        }

        private static EventCandidate? TalkCandidate(SnapshotEnvelope snapshot, JsonElement npc)
        {
            var reasons = BaseNpcBlockReasons(snapshot, npc, requireGift: false);
            var npcName = ReadString(npc, "name");
            if (string.IsNullOrWhiteSpace(npcName))
            {
                reasons.Add("social_npc_identity_missing");
            }

            var expectedDeltaComplete = TryReadBool(npc, "expected_talk_friendship_delta_complete", out var deltaComplete) && deltaComplete;
            var expectedDeltaKnown = TryReadInt(npc, "expected_talk_friendship_delta", out var expectedDelta);
            var friendshipPointsKnown = TryReadInt(npc, "friendship_points", out var friendshipPoints);
            var expectedPointsAfterKnown = TryReadInt(npc, "expected_talk_friendship_points_after", out var expectedPointsAfter);
            var talkedToTodayKnown = TryReadBool(npc, "talked_to_today", out var talkedToToday);
            if (!expectedDeltaComplete || !expectedDeltaKnown || !friendshipPointsKnown || !expectedPointsAfterKnown || !talkedToTodayKnown)
            {
                reasons.Add("social_talk_friendship_projection_incomplete");
            }
            else
            {
                if (talkedToToday)
                {
                    reasons.Add("social_talk_already_completed_today");
                }
                if (expectedDelta <= 0)
                {
                    reasons.Add("social_talk_no_positive_friendship_delta");
                }
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
                ExpectedEffect = "native_social_talk_target=" + npcName + ";expected_friendship_delta=" + (expectedDeltaKnown ? expectedDelta : 0) + ";executor_required=social_native_executor.v1;estimated_duration_ticks=" + estimatedTicks + ";duration_estimate_status=planner_budget_assumption_pending_runtime_calibration",
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
                    Parameter("friendship_row_exists_before", ReadBool(npc, "friendship_row_exists").ToString().ToLowerInvariant()),
                    Parameter("friendship_points_before", friendshipPointsKnown ? friendshipPoints.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty),
                    Parameter("expected_friendship_delta", expectedDeltaKnown ? expectedDelta.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty),
                    Parameter("expected_friendship_points_after", expectedPointsAfterKnown ? expectedPointsAfter.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty),
                    Parameter("expected_talked_to_today_before", talkedToTodayKnown ? talkedToToday.ToString().ToLowerInvariant() : string.Empty),
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
            var resolution = new SocialGiftInventoryBindingResolver().Resolve(
                snapshot,
                npcName);
            foreach (var binding in resolution.Bindings)
            {
                var reasons = new List<string>(baseReasons.Concat(
                    binding.BlockReasons));
                var friendship = Friendship(snapshot, npcName);
                if (ReadStateFieldBool(snapshot, "time", "is_green_rain") &&
                    ReadStateFieldInt(snapshot, "time", "year") == 1 &&
                    !ReadBool(friendship, "is_married"))
                {
                    reasons.Add("social_gift_green_rain_year_one_rejection");
                }

                var tileX = ReadInt(npc, "tile_x");
                var tileY = ReadInt(npc, "tile_y");
                var npcLocationId = ReadString(npc, "location_id") ?? "";
                var standTile = SelectReachableStandTile(snapshot, tileX, tileY, npcLocationId);
                reasons.AddRange(standTile.BlockReasons);
                var slotIndex = binding.SlotIndex;
                var hasValidStand = standTile.RouteDistance >= 0;
                var routeDistanceTicks = hasValidStand ? standTile.RouteDistance * 12 : -1;
                var plannerBudgetTicks = 120;
                var estimatedTicks = hasValidStand ? routeDistanceTicks + plannerBudgetTicks : -1;
                yield return new EventCandidate
                {
                    CandidateId = "social:gift:" + npcName + ":slot:" + slotIndex + ":" + binding.QualifiedItemId,
                    Kind = "social_gift_current",
                    Available = reasons.Count == 0,
                    EstimatedTicks = estimatedTicks,
                    EnergyCost = 0,
                    LocationId = ReadString(npc, "location_id"),
                    TileX = standTile.Tile?.X,
                    TileY = standTile.Tile?.Y,
                    QualifiedItemId = binding.QualifiedItemId,
                    ItemId = binding.ItemId,
                    SlotIndex = slotIndex,
                    Quantity = 1,
                    ExpectedEffect = "native_social_gift_target=" + npcName + ";slot=" + slotIndex + ";item=" + binding.QualifiedItemId + ";executor_required=social_native_executor.v1;estimated_duration_ticks=" + estimatedTicks + ";duration_estimate_status=planner_budget_assumption_pending_runtime_calibration",
                    AvailabilityClass = reasons.Count == 0 ? "current_state_complete" : "current_state_blocked_with_diagnostics",
                    BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = new[]
                    {
                        Parameter("npc_name", npcName),
                        Parameter("slot_index", slotIndex.ToString()),
                        Parameter("qualified_item_id", binding.QualifiedItemId),
                        Parameter("item_quality", binding.Quality.ToString()),
                        Parameter("item_stack_before", binding.StackBefore.ToString()),
                        Parameter("gift_taste", binding.GiftTaste),
                        Parameter("expected_friendship_delta", binding.ExpectedFriendshipDelta?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty),
                        Parameter("friendship_points_before", binding.FriendshipPointsBefore.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        Parameter("expected_friendship_points_after", binding.ExpectedFriendshipPointsAfter?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty),
                        Parameter("friendship_row_exists_before", friendship.HasValue.ToString().ToLowerInvariant()),
                        Parameter("gift_updates_normal_limits", binding.GiftUpdatesNormalLimits.ToString().ToLowerInvariant()),
                        Parameter("gift_side_effect_risk", binding.GiftSideEffectRisk),
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
            var itemMatches = !string.IsNullOrWhiteSpace(requestedSlot) &&
                !string.IsNullOrWhiteSpace(requestedItem) &&
                string.Equals(CandidateParameter(candidate, "slot_index"), requestedSlot, StringComparison.Ordinal) &&
                string.Equals(CandidateParameter(candidate, "qualified_item_id"), requestedItem, StringComparison.OrdinalIgnoreCase);
            if (action.OptionId != "social.advance_partnership")
            {
                return itemMatches;
            }

            var requestedKind = ReadParameter(action, "partnership_action_kind");
            return itemMatches &&
                !string.IsNullOrWhiteSpace(requestedKind) &&
                string.Equals(CandidateParameter(candidate, "partnership_action_kind"), requestedKind, StringComparison.Ordinal);
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

        private static bool TryReadInt(JsonElement item, string property, out int result)
        {
            if (item.TryGetProperty(property, out var value) && value.TryGetInt32(out result))
            {
                return true;
            }
            result = 0;
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
