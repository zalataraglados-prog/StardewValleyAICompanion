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
    private EventCandidate[] AdventureGuildRewardCandidates(SnapshotEnvelope snapshot)
    {
        var projection = ReadStateFieldValue(snapshot, "quests", "adventure_guild_reward");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
            return Array.Empty<EventCandidate>();

        var parameters = AdventureGuildRewardCandidateParameters(projection.Value);
        var reasons = AdventureGuildRewardStringArray(projection.Value, "blocked_diagnostics").ToList();
        if (ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(projection.Value, "invocation_policy") != "autonomous_positive_reward" ||
            ReadString(projection.Value, "status") != "ready")
            reasons.Add("adventure_guild_reward_projection_not_ready");
        if (ReadBool(projection.Value, "inventory_capacity_sufficient") != true)
            reasons.Add("adventure_guild_reward_batch_capacity_not_proven");
        if (ReadInt(projection.Value, "pending_goal_count") <= 0 ||
            ReadInt(projection.Value, "reward_item_count") != ReadInt(projection.Value, "pending_goal_count"))
            reasons.Add("adventure_guild_reward_complete_item_backed_batch_required");
        reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "rewards.claim_adventure_guild_reward",
            Parameters = parameters
        }));
        var blocking = reasons.Distinct(StringComparer.Ordinal).ToArray();
        var fingerprint = ReadString(projection.Value, "batch_fingerprint");
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "adventure-guild-reward:" + fingerprint,
                Kind = "claim_adventure_guild_reward",
                Available = blocking.Length == 0,
                AllowedNow = blocking.Length == 0,
                AllowedToday = blocking.Length == 0,
                LocationId = ReadString(projection.Value, "location_id"),
                TileX = ReadAdventureGuildNullableInt(projection.Value, "action_tile_x") ?? 0,
                TileY = ReadAdventureGuildNullableInt(projection.Value, "action_tile_y") ?? 0,
                DisplayName = "Claim completed monster eradication rewards",
                EstimatedTicks = 720,
                EnergyCost = 0,
                AvailabilityClass = "autonomous_positive_native_adventure_guild_reward_batch",
                ExpectedEffect = "all_pending_Gil_goal_flags=true;all_projected_reward_items_collected=true;native_reward_mail_and_flags_applied=true",
                BlockReasons = blocking,
                Parameters = parameters
            }
        };
    }

    private static SmallModelActionParameter[] AdventureGuildRewardCandidateParameters(JsonElement projection) => new[]
    {
        Parameter("adventure_guild_reward_batch_fingerprint", ReadString(projection, "batch_fingerprint")),
        Parameter("adventure_guild_reward_goals_json", projection.GetProperty("goals").GetRawText()),
        Parameter("adventure_guild_reward_pending_goal_count", ReadInt(projection, "pending_goal_count").ToString(CultureInfo.InvariantCulture)),
        Parameter("adventure_guild_reward_item_count", ReadInt(projection, "reward_item_count").ToString(CultureInfo.InvariantCulture)),
        Parameter("adventure_guild_reward_dialogue_count", ReadInt(projection, "reward_dialogue_count").ToString(CultureInfo.InvariantCulture)),
        Parameter("adventure_guild_reward_inventory_max_items", ReadInt(projection, "inventory_max_items").ToString(CultureInfo.InvariantCulture)),
        Parameter("adventure_guild_reward_inventory_occupied_slots", ReadInt(projection, "inventory_occupied_slots").ToString(CultureInfo.InvariantCulture)),
        Parameter("adventure_guild_reward_inventory_capacity_sufficient", (ReadBool(projection, "inventory_capacity_sufficient") == true).ToString().ToLowerInvariant()),
        Parameter("target_location", ReadString(projection, "location_id")),
        Parameter("target_tile_x", (ReadAdventureGuildNullableInt(projection, "action_tile_x") ?? -1).ToString(CultureInfo.InvariantCulture)),
        Parameter("target_tile_y", (ReadAdventureGuildNullableInt(projection, "action_tile_y") ?? -1).ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_x", (ReadAdventureGuildNullableInt(projection, "stand_tile_x") ?? -1).ToString(CultureInfo.InvariantCulture)),
        Parameter("stand_tile_y", (ReadAdventureGuildNullableInt(projection, "stand_tile_y") ?? -1).ToString(CultureInfo.InvariantCulture)),
        Parameter("adventure_guild_reward_action_tile_index", (ReadAdventureGuildNullableInt(projection, "action_tile_index") ?? -1).ToString(CultureInfo.InvariantCulture)),
        Parameter("native_contract", ReadString(projection, "native_contract")),
        Parameter("max_movement_tiles", "512")
    };

    private static int? ReadAdventureGuildNullableInt(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number
            : null;

    private static string[] AdventureGuildRewardStringArray(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty).Where(item => item.Length > 0).ToArray()
            : Array.Empty<string>();
}
