using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Locations;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class ProgressQuestReadAdapter
{
    internal const string AdventureGuildRewardNativeContract =
        "AdventureGuild.checkAction_gil_tile->gil_all_complete_unclaimed_goals->DialogueBox_optional->ItemGrabMenu->receiveLeftClick_each_reward->OnRewardCollected_Gil_goalId";

    private AdventureGuildRewardProjectionRef? ReadAdventureGuildReward(Farmer? player)
    {
        if (player is null)
            return null;

        var guild = Game1.getLocationFromName("AdventureGuild") as AdventureGuild;
        var endpoint = FindAdventureGuildRewardEndpoint(guild);
        var pending = new List<AdventureGuildRewardGoalRef>();
        var rewardItems = new List<Item>();
        foreach (var pair in DataLoader.MonsterSlayerQuests(Game1.content))
        {
            var data = pair.Value;
            var targets = data.Targets?.ToArray() ?? Array.Empty<string>();
            var currentKills = targets.Sum(player.stats.getMonstersKilled);
            var complete = AdventureGuild.IsComplete(data);
            var collected = AdventureGuild.HasCollectedReward(player, pair.Key);
            if (!complete || collected)
                continue;

            Item? rewardItem = null;
            if (!string.IsNullOrWhiteSpace(data.RewardItemId))
            {
                rewardItem = ItemRegistry.Create(data.RewardItemId);
                rewardItem.SpecialVariable = pending.Count;
                if (rewardItem is StardewObject rewardObject)
                    rewardObject.specialItem = true;
                rewardItems.Add(rewardItem);
            }
            pending.Add(new AdventureGuildRewardGoalRef
            {
                GoalId = pair.Key,
                DisplayName = data.DisplayName ?? string.Empty,
                Targets = targets,
                RequiredKills = data.Count,
                CurrentKills = currentKills,
                Complete = complete,
                Collected = collected,
                GilMailFlag = "Gil_" + pair.Key,
                RewardItemId = rewardItem?.QualifiedItemId ?? data.RewardItemId ?? string.Empty,
                RewardItemRuntimeType = rewardItem?.GetType().FullName ?? string.Empty,
                RewardItemStack = rewardItem?.Stack ?? 0,
                RewardItemQuality = rewardItem?.Quality ?? 0,
                RewardItemSpecialVariable = rewardItem?.SpecialVariable ?? -1,
                RewardItemSpecialItem = rewardItem is StardewObject { specialItem: true },
                RewardDialogue = data.RewardDialogue ?? string.Empty,
                RewardDialogueFlag = data.RewardDialogueFlag ?? string.Empty,
                RewardDialogueShouldShow = !string.IsNullOrWhiteSpace(data.RewardDialogue) &&
                    (string.IsNullOrWhiteSpace(data.RewardDialogueFlag) || !player.mailReceived.Contains(data.RewardDialogueFlag)),
                RewardMail = data.RewardMail ?? string.Empty,
                RewardMailAll = data.RewardMailAll ?? string.Empty,
                RewardFlag = data.RewardFlag ?? string.Empty,
                RewardFlagAll = data.RewardFlagAll ?? string.Empty
            });
        }

        var capacitySufficient = SimulateAdventureGuildRewardCapacity(player, rewardItems);
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var currentLocationMatches = ReferenceEquals(Game1.currentLocation, guild);
        var reasons = new List<string>();
        if (guild?.GetType() != typeof(AdventureGuild)) reasons.Add("adventure_guild_exact_base_location_unavailable");
        if (pending.Count == 0) reasons.Add("adventure_guild_no_complete_unclaimed_goal");
        if (pending.Any(goal => string.IsNullOrWhiteSpace(goal.RewardItemId))) reasons.Add("adventure_guild_no_item_reward_goal_not_supported");
        if (!capacitySufficient) reasons.Add("adventure_guild_reward_batch_inventory_capacity_insufficient");
        if (endpoint is null) reasons.Add("adventure_guild_gil_action_endpoint_unavailable");
        if (!currentLocationMatches) reasons.Add("adventure_guild_not_current_location");
        if (!menuClear) reasons.Add("adventure_guild_menu_or_dialogue_not_clear");

        var goals = pending.ToArray();
        return new AdventureGuildRewardProjectionRef
        {
            ProjectionStatus = "complete_locked_base_1.6.15",
            NativeContract = AdventureGuildRewardNativeContract,
            LocationId = guild?.NameOrUniqueName ?? "AdventureGuild",
            CurrentLocationMatches = currentLocationMatches,
            ActionTileX = endpoint?.Action.X,
            ActionTileY = endpoint?.Action.Y,
            ActionTileIndex = endpoint?.TileIndex,
            StandTileX = endpoint?.Stand.X,
            StandTileY = endpoint?.Stand.Y,
            MenuClear = menuClear,
            BatchFingerprint = AdventureGuildRewardIdentity.Compute(goals),
            PendingGoalCount = goals.Length,
            RewardItemCount = rewardItems.Count,
            RewardDialogueCount = goals.Count(goal => goal.RewardDialogueShouldShow),
            InventoryMaxItems = player.MaxItems,
            InventoryOccupiedSlots = player.Items.Take(player.MaxItems).Count(item => item is not null),
            InventoryCapacitySufficient = capacitySufficient,
            Goals = goals,
            Status = reasons.Count == 0 ? "ready" : "blocked",
            BlockedDiagnostics = reasons.Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    private static bool SimulateAdventureGuildRewardCapacity(Farmer player, IEnumerable<Item> rewards)
    {
        var projected = player.Items.Select(CloneAdventureGuildInventoryItem).ToList();
        foreach (var reward in rewards)
        {
            var candidate = CloneAdventureGuildInventoryItem(reward)!;
            if (Utility.addItemToThisInventoryList(candidate, projected, player.MaxItems) is not null)
                return false;
        }
        return true;
    }

    private static Item? CloneAdventureGuildInventoryItem(Item? item)
    {
        if (item is null) return null;
        var clone = item.getOne();
        clone.Stack = item.Stack;
        return clone;
    }

    private static AdventureGuildRewardEndpoint? FindAdventureGuildRewardEndpoint(AdventureGuild? guild)
    {
        var buildings = guild?.Map?.GetLayer("Buildings");
        if (guild is null || buildings is null)
            return null;
        var endpoints = new List<AdventureGuildRewardEndpoint>();
        var indices = new HashSet<int> { 1291, 1292, 1355, 1356, 1357, 1358 };
        for (var y = 0; y < buildings.LayerHeight; y++)
        {
            for (var x = 0; x < buildings.LayerWidth; x++)
            {
                var tileIndex = buildings.Tiles[x, y]?.TileIndex ?? -1;
                if (!indices.Contains(tileIndex)) continue;
                var action = new Point(x, y);
                foreach (var stand in AdventureGuildRewardStandCandidates(action))
                {
                    if (guild.isTilePassable(new xTile.Dimensions.Location(stand.X, stand.Y), Game1.viewport) &&
                        !guild.IsTileBlockedBy(new Vector2(stand.X, stand.Y), CollisionMask.All & ~CollisionMask.Farmers,
                            CollisionMask.None, useFarmerTile: true))
                    {
                        endpoints.Add(new AdventureGuildRewardEndpoint(action, stand, tileIndex));
                    }
                }
            }
        }
        var playerTile = Game1.player?.TilePoint ?? Point.Zero;
        return endpoints.OrderBy(row => Math.Abs(row.Stand.X - playerTile.X) + Math.Abs(row.Stand.Y - playerTile.Y))
            .ThenBy(row => row.Action.Y).ThenBy(row => row.Action.X)
            .ThenBy(row => row.Stand.Y).ThenBy(row => row.Stand.X).FirstOrDefault();
    }

    private static IEnumerable<Point> AdventureGuildRewardStandCandidates(Point action)
    {
        yield return new Point(action.X, action.Y + 1);
        yield return new Point(action.X - 1, action.Y);
        yield return new Point(action.X + 1, action.Y);
        yield return new Point(action.X, action.Y - 1);
    }

    private sealed record AdventureGuildRewardEndpoint(Point Action, Point Stand, int TileIndex);
}
