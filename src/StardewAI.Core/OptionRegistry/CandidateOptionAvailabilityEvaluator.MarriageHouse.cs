using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] FarmhouseUpgradeCandidates(SnapshotEnvelope snapshot)
    {
        var progress = ReadStateFieldValue(snapshot, "world_progress", "marriage_house");
        if (!progress.HasValue || progress.Value.ValueKind != JsonValueKind.Object ||
            !progress.Value.TryGetProperty("house_upgrade", out var upgrade) || upgrade.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<EventCandidate>();
        }

        var row = progress.Value;
        var levelBefore = ReadInt(upgrade, "level_before");
        var levelAfter = ReadInt(upgrade, "level_after");
        var price = ReadInt(upgrade, "price");
        var requiredItemId = ReadString(upgrade, "required_item_id");
        var requiredCount = ReadInt(upgrade, "required_item_count");
        var inventoryCount = ReadInt(upgrade, "inventory_item_count");
        var constructionDays = ReadInt(upgrade, "construction_days");
        var money = ReadInt(row, "money");
        var actionX = NullableReadInt(row, "carpenter_action_tile_x");
        var actionY = NullableReadInt(row, "carpenter_action_tile_y");
        var stand = actionX.HasValue && actionY.HasValue ? FindBestStandTile(snapshot, actionX.Value, actionY.Value) : null;
        var reasons = new List<string>();
        var status = ReadString(upgrade, "action_status");
        if (status != "ready")
        {
            reasons.Add(string.IsNullOrWhiteSpace(status) ? "farmhouse_upgrade_projection_unavailable" : status);
        }
        if (ReadBool(row, "location_accessible") != true)
        {
            reasons.Add("science_house_not_accessible");
        }
        if (!actionX.HasValue || !actionY.HasValue || ReadString(row, "carpenter_action_raw") != "Carpenter")
        {
            reasons.Add("carpenter_action_tile_unavailable");
        }
        if (stand is null)
        {
            reasons.Add("carpenter_no_reachable_counter_stand_tile");
        }
        if (!FarmhouseUpgradeTupleExact(levelBefore, levelAfter, price, requiredItemId, requiredCount, constructionDays))
        {
            reasons.Add("farmhouse_upgrade_native_tuple_invalid");
        }

        var parameters = stand is null || !actionX.HasValue || !actionY.HasValue
            ? Array.Empty<SmallModelActionParameter>()
            : new[]
            {
                Parameter("target_location", "ScienceHouse"),
                Parameter("stand_tile_x", stand.X.ToString()),
                Parameter("stand_tile_y", stand.Y.ToString()),
                Parameter("carpenter_action_tile_x", actionX.Value.ToString()),
                Parameter("carpenter_action_tile_y", actionY.Value.ToString()),
                Parameter("carpenter_action_raw", "Carpenter"),
                Parameter("purchase_kind", "farmhouse_upgrade"),
                Parameter("project_id", ReadString(upgrade, "upgrade_id")),
                Parameter("expected_house_upgrade_level_before", levelBefore.ToString()),
                Parameter("expected_house_upgrade_level_after_construction", levelAfter.ToString()),
                Parameter("expected_days_until_house_upgrade_before", ReadInt(row, "days_until_farmhouse_upgrade").ToString()),
                Parameter("expected_days_until_house_upgrade_after", constructionDays.ToString()),
                Parameter("expected_money_before", money.ToString()),
                Parameter("price", price.ToString()),
                Parameter("expected_money_after", (money - price).ToString()),
                Parameter("qualified_item_id", requiredItemId),
                Parameter("required_stack", requiredCount.ToString()),
                Parameter("inventory_item_total_before", inventoryCount.ToString()),
                Parameter("inventory_item_total_after", (inventoryCount - requiredCount).ToString()),
                Parameter("native_contract", "GameLocation.checkAction_Carpenter_then_answerDialogue_carpenter_Upgrade_then_upgrade_Yes")
            };
        var expectedEffect = "player.money=" + (money - price) +
            ";player.days_until_farmhouse_upgrade=" + constructionDays +
            (requiredCount > 0 ? ";inventory." + requiredItemId + "=" + (inventoryCount - requiredCount) : string.Empty) +
            ";eventual.player.farmhouse_upgrade_level=" + levelAfter;
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
        var candidateKind = levelAfter <= 2 ? "purchase_farmhouse_upgrade" : "purchase_farmhouse_expansion";
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "farmhouse-upgrade:" + ReadString(upgrade, "upgrade_id"),
                Kind = candidateKind,
                Available = reasons.Count == 0,
                LocationId = "ScienceHouse",
                TileX = actionX,
                TileY = actionY,
                ExpectedEffect = expectedEffect,
                Quantity = 1,
                EstimatedTicks = Math.Max(300, distance * 60 + 300),
                AvailabilityClass = "transparent_native_farmhouse_upgrade",
                AllowedNow = reasons.Count == 0,
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            }
        };
    }

    private static bool FarmhouseUpgradeTupleExact(int before, int after, int price, string itemId, int count, int days) =>
        days == 3 && (before, after, price, itemId, count) switch
        {
            (0, 1, 10000, "(O)388", 450) => true,
            (1, 2, 65000, "(O)709", 100) => true,
            (2, 3, 100000, "", 0) => true,
            _ => false
        };
}
