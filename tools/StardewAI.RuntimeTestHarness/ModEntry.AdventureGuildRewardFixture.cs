using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupAdventureGuildRewardFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (request.AdventureGuildRewardFixtureCase != "single_item")
            reasons.Add("adventure_guild_reward_fixture_case_invalid");
        var guild = Game1.getLocationFromName("AdventureGuild") as AdventureGuild;
        var endpoint = FindAdventureGuildFixtureEndpoint(guild);
        var data = DataLoader.MonsterSlayerQuests(Game1.content);
        var selected = data.FirstOrDefault(pair => !string.IsNullOrWhiteSpace(pair.Value.RewardItemId) &&
            pair.Value.Targets is { Count: > 0 });
        if (guild?.GetType() != typeof(AdventureGuild) || endpoint is null || string.IsNullOrWhiteSpace(selected.Key))
            reasons.Add("adventure_guild_reward_fixture_native_data_or_endpoint_unavailable");
        if (reasons.Count > 0 || guild is null || endpoint is null)
            return BlockedWithPrimitive(request, "debug_setup_adventure_guild_reward",
                "adventure_guild_reward_fixture=ready", "adventure_guild_reward_fixture=blocked", reasons.ToArray());

        Game1.exitActiveMenu();
        Game1.dialogueUp = false;
        Game1.currentSpeaker = null;
        Game1.eventUp = false;
        Game1.eventOver = false;
        Game1.currentMinigame = null;
        Game1.timeOfDay = 1500;
        Game1.currentLocation = guild;
        Game1.player.currentLocation = guild;
        guild.currentEvent = null;
        Game1.player.Position = endpoint.Value.Stand.ToVector2() * Game1.tileSize;
        Game1.player.UsingTool = false;
        Game1.player.canMove = true;
        Game1.player.CurrentToolIndex = 0;
        for (var index = 0; index < Math.Min(Game1.player.MaxItems, Game1.player.Items.Count); index++)
            Game1.player.Items[index] = null;
        foreach (var pair in data)
            Game1.player.mailReceived.Add("Gil_" + pair.Key);
        Game1.player.mailReceived.Remove("Gil_" + selected.Key);
        foreach (var target in selected.Value.Targets!)
            Game1.player.stats.specificMonstersKilled[target] = 0;
        Game1.player.stats.specificMonstersKilled[selected.Value.Targets![0]] = selected.Value.Count;

        var pending = ReadLiveAdventureGuildRewardGoals(Game1.player);
        var verified = pending.Length == 1 && pending[0].GoalId == selected.Key &&
            pending[0].RewardItemId == ItemRegistry.Create(selected.Value.RewardItemId).QualifiedItemId &&
            AdventureGuildRewardEndpointMatches(guild, endpoint.Value.Action, endpoint.Value.Stand, endpoint.Value.TileIndex);
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_adventure_guild_reward",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_single_complete_unclaimed_item_backed_monster_slayer_goal_installed" }
                : new[] { "adventure_guild_reward_fixture_receipt_mismatch" },
            RequestedEffect = "adventure_guild_reward_fixture=single_item",
            ObservedEffect = "pending_goals=" + pending.Length + ";selected_goal=" + selected.Key +
                ";location=" + Game1.currentLocation.NameOrUniqueName,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "adventure_guild_reward_fixture_receipt_mismatch" }
        };
    }

    private static (Point Action, Point Stand, int TileIndex)? FindAdventureGuildFixtureEndpoint(AdventureGuild? guild)
    {
        var buildings = guild?.Map?.GetLayer("Buildings");
        if (guild is null || buildings is null) return null;
        var indices = new HashSet<int> { 1291, 1292, 1355, 1356, 1357, 1358 };
        for (var y = 0; y < buildings.LayerHeight; y++)
        for (var x = 0; x < buildings.LayerWidth; x++)
        {
            var index = buildings.Tiles[x, y]?.TileIndex ?? -1;
            if (!indices.Contains(index)) continue;
            var action = new Point(x, y);
            foreach (var stand in new[] { new Point(x, y + 1), new Point(x - 1, y), new Point(x + 1, y), new Point(x, y - 1) })
                if (IsTileOnMap(guild, stand) && IsTileWalkable(guild, stand) && !IsTileOccupiedByCharacter(guild, stand))
                    return (action, stand, index);
        }
        return null;
    }
}
