using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string PrizeTicketRewardCompilerNativeContract =
        "Town.SpecialOrdersPrizeTickets->inventory_PrizeTicket_and_pending_stat_minus_one;ManorHouse.PrizeMachine->PrizeTicketMenu.currentPrizeTrack[0]->inventory_else_debris->PrizeTicket_minus_one->ticketPrizesClaimed_plus_one";

    private static readonly string[] PrizeTicketRewardBoundNames =
    {
        "prize_ticket_stage", "prize_ticket_projection_fingerprint", "prize_ticket_current_reward_fingerprint",
        "prize_ticket_preview_json", "prize_ticket_inventory_count_before", "prize_ticket_pending_count_before",
        "prize_ticket_claimed_count_before", "prize_ticket_prize_level", "prize_ticket_reward_qualified_item_id",
        "prize_ticket_reward_item_id", "prize_ticket_reward_stack", "prize_ticket_reward_quality",
        "prize_ticket_reward_runtime_type", "prize_ticket_inventory_max_items", "prize_ticket_inventory_occupied_slots",
        "prize_ticket_pending_capacity_sufficient", "target_location", "target_tile_x", "target_tile_y",
        "stand_tile_x", "stand_tile_y", "prize_ticket_action_raw", "native_contract", "max_movement_tiles"
    };

    private static SmallModelActionParameter[] BuildPrizeTicketRewardParameters(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var parameters = action.Parameters
            .Where(parameter => !PrizeTicketRewardBoundNames.Contains(parameter.Name, StringComparer.Ordinal)).ToList();
        var projection = ReadStateFieldValue(snapshot, "player", "prize_ticket_reward");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            !projection.Value.TryGetProperty("current_reward", out var reward) || reward.ValueKind != JsonValueKind.Object)
            return parameters.ToArray();
        var level = ReadInt(projection.Value, "current_prize_level").ToString(CultureInfo.InvariantCulture);
        var rewardFingerprint = ReadString(projection.Value, "current_reward_fingerprint");
        var requestedLevel = ReadParameter(action, "continuation.expected_prize_level");
        var requestedReward = ReadParameter(action, "continuation.expected_reward_fingerprint");
        if ((!string.IsNullOrWhiteSpace(requestedLevel) && requestedLevel != level) ||
            (!string.IsNullOrWhiteSpace(requestedReward) && requestedReward != rewardFingerprint))
            return parameters.ToArray();
        var target = ResolvePrizeTicketRewardCompilerTarget(projection.Value, action, snapshot);
        if (target is null) return parameters.ToArray();
        parameters.AddRange(new[]
        {
            Parameter("prize_ticket_stage", ReadString(projection.Value, "stage")),
            Parameter("prize_ticket_projection_fingerprint", ReadString(projection.Value, "projection_fingerprint")),
            Parameter("prize_ticket_current_reward_fingerprint", rewardFingerprint),
            Parameter("prize_ticket_preview_json", projection.Value.GetProperty("preview_track").GetRawText()),
            Parameter("prize_ticket_inventory_count_before", ReadInt(projection.Value, "inventory_ticket_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_pending_count_before", ReadInt(projection.Value, "pending_special_order_ticket_count").ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_claimed_count_before", ReadInt(projection.Value, "ticket_prizes_claimed").ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_prize_level", level),
            Parameter("prize_ticket_reward_qualified_item_id", ReadString(reward, "qualified_item_id")),
            Parameter("prize_ticket_reward_item_id", ReadString(reward, "item_id")),
            Parameter("prize_ticket_reward_stack", ReadInt(reward, "stack").ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_reward_quality", ReadInt(reward, "quality").ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_reward_runtime_type", ReadString(reward, "runtime_type")),
            Parameter("prize_ticket_inventory_max_items", ReadInt(projection.Value, "inventory_max_items").ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_inventory_occupied_slots", ReadInt(projection.Value, "inventory_occupied_slots").ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_pending_capacity_sufficient", (ReadBool(projection.Value, "pending_ticket_capacity_sufficient") == true).ToString().ToLowerInvariant()),
            Parameter("target_location", target.LocationId),
            Parameter("target_tile_x", target.TargetX.ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", target.TargetY.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", target.StandX.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", target.StandY.ToString(CultureInfo.InvariantCulture)),
            Parameter("prize_ticket_action_raw", target.ActionRaw),
            Parameter("native_contract", ReadString(projection.Value, "native_contract")),
            Parameter("max_movement_tiles", "512")
        });
        return parameters.ToArray();
    }

    private static CompiledActionStep[] CompilePrizeTicketRewardStep(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var bound = BoundPrizeTicketRewardAction(action, snapshot);
        var stage = ReadParameter(bound, "prize_ticket_stage");
        var x = ReadIntParameter(bound, "target_tile_x");
        var y = ReadIntParameter(bound, "target_tile_y");
        if (stage is not ("collect_pending_ticket" or "redeem_prize") || !x.HasValue || !y.HasValue)
            return Array.Empty<CompiledActionStep>();
        var expected = stage == "redeem_prize"
            ? "PrizeTicket=-1;ticketPrizesClaimed=+1;reward=" + ReadParameter(bound, "prize_ticket_reward_qualified_item_id") + "*" + ReadParameter(bound, "prize_ticket_reward_stack")
            : "specialOrderPrizeTickets=-1;PrizeTicket=+1;continuation=true";
        return new[]
        {
            Step("claim_prize_ticket", ReadParameter(bound, "target_location") + "(" + x + "," + y + "):" + stage, expected, 900)
        };
    }

    private static string[] ValidatePrizeTicketRewardPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId is not ("rewards.claim_prize_ticket" or "executor.claim_prize_ticket"))
            return Array.Empty<string>();
        var reasons = new List<string>();
        if (ActionSeesActiveMenuOpen(action, snapshot)) reasons.Add("prize_ticket_reward_requires_clear_menu");
        var projection = ReadStateFieldValue(snapshot, "player", "prize_ticket_reward");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return new[] { "prize_ticket_reward_projection_unavailable" };
        var stage = ReadString(projection.Value, "stage");
        if (ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(projection.Value, "invocation_policy") != "autonomous_positive_reward" ||
            ReadString(projection.Value, "native_contract") != PrizeTicketRewardCompilerNativeContract ||
            ReadString(projection.Value, "service_status") != "ready" ||
            stage is not ("collect_pending_ticket" or "redeem_prize"))
            reasons.Add("prize_ticket_reward_complete_ready_projection_required");
        if (stage == "redeem_prize" && ReadInt(projection.Value, "inventory_ticket_count") <= 0)
            reasons.Add("prize_ticket_reward_inventory_ticket_required");
        if (stage == "collect_pending_ticket" &&
            (ReadInt(projection.Value, "pending_special_order_ticket_count") <= 0 ||
             ReadBool(projection.Value, "pending_ticket_capacity_sufficient") != true))
            reasons.Add("prize_ticket_reward_pending_ticket_and_capacity_required");

        var bound = BoundPrizeTicketRewardAction(action, snapshot);
        var target = ResolvePrizeTicketRewardCompilerTarget(projection.Value, action, snapshot);
        var reward = projection.Value.GetProperty("current_reward");
        var exact = target is not null &&
            ReadParameter(bound, "prize_ticket_stage") == stage &&
            ReadParameter(bound, "prize_ticket_projection_fingerprint") == ReadString(projection.Value, "projection_fingerprint") &&
            ReadParameter(bound, "prize_ticket_current_reward_fingerprint") == ReadString(projection.Value, "current_reward_fingerprint") &&
            ReadParameter(bound, "prize_ticket_preview_json") == projection.Value.GetProperty("preview_track").GetRawText() &&
            ReadIntParameter(bound, "prize_ticket_inventory_count_before") == ReadInt(projection.Value, "inventory_ticket_count") &&
            ReadIntParameter(bound, "prize_ticket_pending_count_before") == ReadInt(projection.Value, "pending_special_order_ticket_count") &&
            ReadIntParameter(bound, "prize_ticket_claimed_count_before") == ReadInt(projection.Value, "ticket_prizes_claimed") &&
            ReadIntParameter(bound, "prize_ticket_prize_level") == ReadInt(projection.Value, "current_prize_level") &&
            ReadParameter(bound, "prize_ticket_reward_qualified_item_id") == ReadString(reward, "qualified_item_id") &&
            ReadIntParameter(bound, "prize_ticket_reward_stack") == ReadInt(reward, "stack") &&
            ReadParameter(bound, "target_location") == target.LocationId &&
            ReadIntParameter(bound, "target_tile_x") == target.TargetX &&
            ReadIntParameter(bound, "target_tile_y") == target.TargetY &&
            ReadIntParameter(bound, "stand_tile_x") == target.StandX &&
            ReadIntParameter(bound, "stand_tile_y") == target.StandY &&
            ReadParameter(bound, "prize_ticket_action_raw") == target.ActionRaw &&
            ReadParameter(bound, "native_contract") == PrizeTicketRewardCompilerNativeContract;
        if (!exact) reasons.Add("prize_ticket_reward_complete_fresh_typed_binding_required");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static SmallModelAction BoundPrizeTicketRewardAction(SmallModelAction action, SnapshotEnvelope snapshot) => new()
    {
        ActionId = action.ActionId,
        OptionId = action.OptionId,
        Rationale = action.Rationale,
        Parameters = BuildPrizeTicketRewardParameters(action, snapshot)
    };

    private static PrizeTicketCompilerTarget? ResolvePrizeTicketRewardCompilerTarget(
        JsonElement projection,
        SmallModelAction action,
        SnapshotEnvelope snapshot)
    {
        var stage = ReadString(projection, "stage");
        var property = stage == "redeem_prize" ? "prize_machine_action_tiles" : "special_order_ticket_action_tiles";
        if (!projection.TryGetProperty(property, out var rows) || rows.ValueKind != JsonValueKind.Array)
            return null;
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object)
            .Select(row =>
            {
                var x = ReadInt(row, "tile_x");
                var y = ReadInt(row, "tile_y");
                var requestedX = ReadIntParameter(action, "stand_tile_x");
                var requestedY = ReadIntParameter(action, "stand_tile_y");
                var stand = requestedX.HasValue && requestedY.HasValue && Math.Abs(x - requestedX.Value) + Math.Abs(y - requestedY.Value) == 1 &&
                    SleepStandTileReachable(snapshot, requestedX.Value, requestedY.Value)
                        ? new SleepStandTile(requestedX.Value, requestedY.Value)
                        : FindBestSleepStandTile(snapshot, x, y);
                return stand is null ? null : new PrizeTicketCompilerTarget(
                    ReadString(row, "location_id"), x, y, stand.X, stand.Y, ReadString(row, "action_raw"),
                    Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y));
            })
            .Where(target => target is not null)
            .OrderBy(target => target!.Distance).ThenBy(target => target!.TargetY).ThenBy(target => target!.TargetX)
            .FirstOrDefault();
    }

    private sealed record PrizeTicketCompilerTarget(
        string LocationId, int TargetX, int TargetY, int StandX, int StandY, string ActionRaw, int Distance);
}
