using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] CommunityCenterRewardCandidates(
        SnapshotEnvelope snapshot,
        JsonElement progress,
        JsonElement bundles,
        string currentLocation,
        string routeState,
        bool canReadJunimoText,
        bool rowCountExact)
    {
        var pending = bundles.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object &&
                          ReadBool(row, "reward_available") == true &&
                          row.TryGetProperty("reward", out var reward) &&
                          reward.ValueKind == JsonValueKind.Object)
            .OrderBy(row => ReadInt(row, "area_id"))
            .ThenBy(row => ReadInt(row, "bundle_id"))
            .ToArray();
        if (pending.Length == 0)
        {
            return Array.Empty<EventCandidate>();
        }

        if (ReadBool(progress, "community_center_is_current_location") != true)
        {
            return CommunityCenterRewardRouteCandidates(
                snapshot,
                pending,
                currentLocation,
                routeState,
                canReadJunimoText,
                rowCountExact);
        }

        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var result = new List<EventCandidate>();
        foreach (var bundle in pending)
        {
            var reward = bundle.GetProperty("reward");
            var interactionX = NullableReadInt(reward, "interaction_tile_x");
            var interactionY = NullableReadInt(reward, "interaction_tile_y");
            var stand = interactionX.HasValue && interactionY.HasValue
                ? FindBestStandTile(snapshot, interactionX.Value, interactionY.Value)
                : null;
            var reasons = CommunityCenterRewardBlockReasons(
                bundle,
                reward,
                routeState,
                canReadJunimoText,
                rowCountExact,
                requireStand: true,
                standAvailable: stand is not null);
            var distance = stand is null
                ? 0
                : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
            var parameters = stand is null || !interactionX.HasValue || !interactionY.HasValue
                ? Array.Empty<SmallModelActionParameter>()
                : CommunityCenterRewardParameters(
                    progress,
                    bundle,
                    reward,
                    stand.X,
                    stand.Y,
                    interactionX.Value,
                    interactionY.Value);
            var blocks = reasons.Distinct(StringComparer.Ordinal).ToArray();
            result.Add(new EventCandidate
            {
                CandidateId = "community-center-claim-reward:" + ReadInt(bundle, "bundle_id"),
                Kind = "claim_community_center_bundle_reward",
                Available = blocks.Length == 0,
                LocationId = "CommunityCenter",
                TileX = interactionX,
                TileY = interactionY,
                ItemId = ReadString(reward, "item_id"),
                QualifiedItemId = ReadString(reward, "qualified_item_id"),
                Quantity = ReadInt(reward, "stack"),
                EstimatedTicks = Math.Max(180, distance * 60 + 180),
                EnergyCost = 0,
                ExpectedEffect = CommunityCenterRewardExpectedEffect(bundle, reward),
                AvailabilityClass = "transparent_native_community_center_bundle_reward",
                AllowedNow = blocks.Length == 0,
                BlockReasons = blocks,
                Parameters = parameters
            });
        }
        return result.ToArray();
    }

    private EventCandidate[] CommunityCenterRewardRouteCandidates(
        SnapshotEnvelope snapshot,
        JsonElement[] pending,
        string currentLocation,
        string routeState,
        bool canReadJunimoText,
        bool rowCountExact)
    {
        var route = FindResolvedRoutePlan(
            snapshot,
            currentLocation,
            "CommunityCenter",
            RouteConnectorCandidates(snapshot, int.MaxValue)
                .Where(candidate => candidate.Kind == "route_connector_tile")
                .ToArray());
        var connector = route?.FirstActionCandidate;
        return pending.Select(bundle =>
        {
            var reward = bundle.GetProperty("reward");
            var reasons = CommunityCenterRewardBlockReasons(
                bundle,
                reward,
                routeState,
                canReadJunimoText,
                rowCountExact,
                requireStand: false,
                standAvailable: true);
            if (ReadString(reward, "action_status") != "community_center_not_current_location")
            {
                reasons.Add("community_center_remote_reward_projection_unavailable");
            }
            reasons.AddRange(connector?.BlockReasons ??
                new[] { "community_center_reward_cross_map_route_unavailable" });
            var blocks = reasons.Distinct(StringComparer.Ordinal).ToArray();
            return new EventCandidate
            {
                CandidateId = "community-center-claim-reward-route:" +
                    ReadInt(bundle, "bundle_id") + ":" + currentLocation,
                Kind = "route_connector_tile",
                Available = connector is not null && connector.Available && blocks.Length == 0,
                LocationId = currentLocation,
                TileX = connector?.TileX,
                TileY = connector?.TileY,
                ItemId = ReadString(reward, "item_id"),
                QualifiedItemId = ReadString(reward, "qualified_item_id"),
                Quantity = ReadInt(reward, "stack"),
                EstimatedTicks = connector?.EstimatedTicks ?? -1,
                EnergyCost = connector?.EnergyCost ?? 0,
                ExpectedEffect = (connector?.ExpectedEffect ?? string.Empty) +
                    ";community_center_reward_bundle=" + ReadInt(bundle, "bundle_id") +
                    ";one_connector_then_fresh_snapshot=true",
                AvailabilityClass = connector is null
                    ? "community_center_reward_route_blocked"
                    : "community_center_reward_rolling_route",
                AllowedNow = connector?.AllowedNow,
                AllowedToday = connector?.AllowedToday,
                NextOpenTime = connector?.NextOpenTime,
                EffectiveOpenTime = connector?.EffectiveOpenTime,
                ClosesAt = connector?.ClosesAt,
                WaitCost = connector?.WaitCost,
                GateReasons = connector?.GateReasons ?? Array.Empty<string>(),
                BlockReasons = blocks,
                Parameters = (connector?.Parameters ?? Array.Empty<SmallModelActionParameter>())
                    .Concat(new[]
                    {
                        Parameter("continuation.option_id", "community_center.donate_bundle_items"),
                        Parameter("continuation.target_location", "CommunityCenter"),
                        Parameter("continuation.bundle_data_key", ReadString(bundle, "bundle_data_key")),
                        Parameter("continuation.bundle_id", ReadInt(bundle, "bundle_id").ToString(CultureInfo.InvariantCulture)),
                        Parameter("continuation.qualified_item_id", ReadString(reward, "qualified_item_id")),
                        Parameter("continuation.reward_claim_mode", ReadString(reward, "claim_mode"))
                    })
                    .ToArray()
            };
        }).ToArray();
    }

    private static List<string> CommunityCenterRewardBlockReasons(
        JsonElement bundle,
        JsonElement reward,
        string routeState,
        bool canReadJunimoText,
        bool rowCountExact,
        bool requireStand,
        bool standAvailable)
    {
        var reasons = new List<string>();
        var claimMode = ReadString(reward, "claim_mode");
        if (routeState is not ("undecided" or "community_center_locked"))
            reasons.Add(routeState == "conflicting_irreversible_flags"
                ? "community_center_route_state_conflict"
                : "community_center_route_locked_out_by_joja");
        if (!rowCountExact)
            reasons.Add("community_center_bundle_projection_incomplete");
        if (claimMode == "junimo_note_present_button" && !canReadJunimoText)
            reasons.Add("community_center_junimo_text_not_readable");
        if (ReadString(bundle, "projection_status") != "exact" ||
            ReadString(reward, "projection_status") != "exact" ||
            ReadBool(bundle, "reward_available") != true ||
            ReadBool(reward, "inventory_accepts_reward") != true ||
            string.IsNullOrWhiteSpace(ReadString(reward, "qualified_item_id")) ||
            ReadInt(reward, "stack") < 1 ||
            claimMode is not ("junimo_note_present_button" or "missed_rewards_chest") ||
            !NullableReadInt(reward, "interaction_tile_x").HasValue ||
            !NullableReadInt(reward, "interaction_tile_y").HasValue)
        {
            reasons.Add("community_center_bundle_reward_typed_projection_invalid");
        }
        if (ReadString(reward, "action_status") is not ("ready" or "community_center_not_current_location"))
            reasons.Add(ReadString(reward, "action_status"));
        if (requireStand && !standAvailable)
            reasons.Add("community_center_bundle_reward_no_reachable_stand_tile");
        return reasons.Where(reason => !string.IsNullOrWhiteSpace(reason)).ToList();
    }

    private static SmallModelActionParameter[] CommunityCenterRewardParameters(
        JsonElement progress,
        JsonElement bundle,
        JsonElement reward,
        int standX,
        int standY,
        int interactionX,
        int interactionY) =>
        new[]
        {
            Parameter("target_location", "CommunityCenter"),
            Parameter("stand_tile_x", standX.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", standY.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_x", interactionX.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", interactionY.ToString(CultureInfo.InvariantCulture)),
            Parameter("community_center_note_tile_x", NullableReadInt(bundle, "note_tile_x")?.ToString(CultureInfo.InvariantCulture) ?? "-1"),
            Parameter("community_center_note_tile_y", NullableReadInt(bundle, "note_tile_y")?.ToString(CultureInfo.InvariantCulture) ?? "-1"),
            Parameter("route_state", ReadString(progress, "route_state")),
            Parameter("bundle_data_key", ReadString(bundle, "bundle_data_key")),
            Parameter("bundle_id", ReadInt(bundle, "bundle_id").ToString(CultureInfo.InvariantCulture)),
            Parameter("bundle_area_id", ReadInt(bundle, "area_id").ToString(CultureInfo.InvariantCulture)),
            Parameter("bundle_area_name", ReadString(bundle, "area_name")),
            Parameter("reward_claim_mode", ReadString(reward, "claim_mode")),
            Parameter("item_id", ReadString(reward, "item_id")),
            Parameter("qualified_item_id", ReadString(reward, "qualified_item_id")),
            Parameter("target_runtime_type", ReadString(reward, "runtime_type")),
            Parameter("expected_item_quality", ReadInt(reward, "quality").ToString(CultureInfo.InvariantCulture)),
            Parameter("required_stack", ReadInt(reward, "stack").ToString(CultureInfo.InvariantCulture)),
            Parameter("inventory_item_total_before", ReadInt(reward, "inventory_item_total_before").ToString(CultureInfo.InvariantCulture)),
            Parameter("inventory_item_total_after", ReadInt(reward, "inventory_item_total_after").ToString(CultureInfo.InvariantCulture)),
            Parameter("expected_bundle_reward_available_after", "false"),
            Parameter("authoritative_route_sources_json", RawJson(reward, "authoritative_route_sources")),
            Parameter("native_contract", "CommunityCenter.checkBundle_presentButton_or_MissedRewards_then_ItemGrabMenu.receiveLeftClick_exact_bundle_reward"),
            Parameter("max_movement_tiles", "512")
        };

    private static string CommunityCenterRewardExpectedEffect(
        JsonElement bundle,
        JsonElement reward) =>
        "community_center.bundle=" + ReadInt(bundle, "bundle_id") +
        ":reward_available=false;player.inventory.qualified_item_total[" +
        ReadString(reward, "qualified_item_id") + "]=" +
        ReadInt(reward, "inventory_item_total_after") +
        ";native_claim_mode=" + ReadString(reward, "claim_mode");
}
