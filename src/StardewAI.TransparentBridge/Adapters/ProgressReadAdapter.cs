using StardewAI.Contracts.State;
using StardewValley.Network;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class ProgressQuestReadAdapter : ReadAdapterBase
{
    public override string Domain => "quests_progress";
    public override int Priority => 60;

    public override StateAdapterResult Collect(long tick)
    {
        var player = Context.IsWorldReady ? Game1.player : null;
        var team = player?.team;

        var fields = new Dictionary<string, object>
        {
            ["active_quests"] = Field(ReadActiveQuests(player), "Game1.player.questLog", tick),
            ["completed_quests"] = Unavailable("no_verified_global_completed_quest_collection_found", "StardewValley.Farmer questLog contains current Quest.completed only", tick),
            ["mail_received"] = Field(player?.mailReceived.OrderBy(id => id).ToArray(), "Game1.player.mailReceived", tick),
            ["mail_for_tomorrow"] = Field(player?.mailForTomorrow.OrderBy(id => id).ToArray(), "Game1.player.mailForTomorrow", tick),
            ["mailbox"] = Field(player?.mailbox.OrderBy(id => id).ToArray(), "Game1.player.mailbox", tick),
            ["special_orders"] = Field(ReadSpecialOrders(team), "Game1.player.team.specialOrders", tick),
            ["completed_special_orders"] = Field(team?.completedSpecialOrders.OrderBy(id => id).ToArray(), "Game1.player.team.completedSpecialOrders", tick),
            ["accepted_special_order_types"] = Field(team?.acceptedSpecialOrderTypes.OrderBy(id => id).ToArray(), "Game1.player.team.acceptedSpecialOrderTypes", tick)
        };

        return Section("quests", fields, new[] { "quests.completed_quests" });
    }

    private static QuestProgressRef[]? ReadActiveQuests(Farmer? player)
    {
        return player?.questLog
            .Select(quest => new QuestProgressRef
            {
                Id = quest.id.Value,
                Title = quest.questTitle,
                Description = quest.questDescription,
                CurrentObjective = quest.currentObjective,
                QuestType = quest.questType.Value,
                Accepted = quest.accepted.Value,
                Completed = quest.completed.Value,
                DailyQuest = quest.dailyQuest.Value,
                DaysLeft = quest.daysLeft.Value,
                MoneyReward = quest.moneyReward.Value
            })
            .OrderBy(quest => quest.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static SpecialOrderProgressRef[]? ReadSpecialOrders(FarmerTeam? team)
    {
        return team?.specialOrders
            .Select(order => new SpecialOrderProgressRef
            {
                QuestKey = order.questKey.Value,
                QuestName = order.questName.Value,
                QuestDescription = order.questDescription.Value,
                Requester = order.requester.Value,
                OrderType = order.orderType.Value,
                QuestState = order.questState.Value.ToString(),
                DueDate = order.dueDate.Value,
                Duration = order.questDuration.Value.ToString(),
                Objectives = order.objectives
                    .Select(objective => new SpecialOrderObjectiveProgressRef
                    {
                        Description = objective.description.Value,
                        CurrentCount = objective.currentCount.Value,
                        MaxCount = objective.maxCount.Value
                    })
                    .ToArray()
            })
            .OrderBy(order => order.QuestKey, StringComparer.Ordinal)
            .ToArray();
    }
}

public sealed class WorldProgressReadAdapter : ReadAdapterBase
{
    private const int GoldenWalnutPerfectionTarget = 130;
    private const int GoldenWalnutQiRoomUnlockTarget = 100;

    public override string Domain => "world_progress";
    public override int Priority => 61;

    public override StateAdapterResult Collect(long tick)
    {
        var master = Context.IsWorldReady ? Game1.MasterPlayer : null;
        var world = Context.IsWorldReady ? Game1.netWorldState?.Value : null;
        var museum = Context.IsWorldReady ? Game1.getLocationFromName("ArchaeologyHouse") as LibraryMuseum : null;

        var fields = new Dictionary<string, object>
        {
            ["community_center"] = Field(ReadCommunityCenter(world, master), "Game1.netWorldState.Value.Bundles/BundleRewards; Game1.MasterPlayer.mailReceived cc* flags", tick),
            ["joja_membership"] = Field(Context.IsWorldReady ? master?.mailReceived.Contains("JojaMember") : null, "Game1.MasterPlayer.mailReceived.Contains(\"JojaMember\")", tick),
            ["museum"] = Field(ReadMuseum(museum), "LibraryMuseum.museumPieces", tick),
            ["shipping_collection"] = Field(ToSortedDictionary(master?.basicShipped), "Game1.MasterPlayer.basicShipped", tick),
            ["fish_collection"] = Field(ToSortedArrayDictionary(master?.fishCaught), "Game1.MasterPlayer.fishCaught", tick),
            ["artifact_collection"] = Field(ToSortedArrayDictionary(master?.archaeologyFound), "Game1.MasterPlayer.archaeologyFound", tick),
            ["mineral_collection"] = Field(ToSortedDictionary(master?.mineralsFound), "Game1.MasterPlayer.mineralsFound", tick),
            ["cooking_recipes"] = Field(ToSortedDictionary(master?.cookingRecipes), "Game1.MasterPlayer.cookingRecipes", tick),
            ["crafting_recipes"] = Field(ToSortedDictionary(master?.craftingRecipes), "Game1.MasterPlayer.craftingRecipes", tick),
            ["achievements"] = Field(master?.achievements.OrderBy(id => id).ToArray(), "Game1.MasterPlayer.achievements", tick),
            ["perfection"] = Field(ReadPerfection(world), "StardewValley.Utility.percentGameComplete(); Game1.netWorldState.Value.PerfectionWaivers", tick),
            ["golden_walnuts"] = Field(ReadGoldenWalnuts(world), "Game1.netWorldState.Value.GoldenWalnuts/GoldenWalnutsFound", tick)
        };

        return Section("world_progress", fields, Array.Empty<string>());
    }

    private static CommunityCenterProgressRef? ReadCommunityCenter(StardewValley.Network.NetWorldState? world, Farmer? master)
    {
        if (world is null || master is null)
        {
            return null;
        }

        var areaFlags = new[] { "ccBoilerRoom", "ccCraftsRoom", "ccPantry", "ccFishTank", "ccVault", "ccBulletin" };

        return new CommunityCenterProgressRef
        {
            Bundles = world.Bundles.Pairs
                .OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value.ToArray()),
            BundleRewards = world.BundleRewards.Pairs
                .OrderBy(pair => pair.Key)
                .ToDictionary(pair => pair.Key, pair => pair.Value),
            CompletedAreaMailFlags = areaFlags
                .Where(flag => master.mailReceived.Contains(flag))
                .OrderBy(flag => flag, StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static MuseumProgressRef? ReadMuseum(LibraryMuseum? museum)
    {
        return museum is null
            ? null
            : new MuseumProgressRef
            {
                Pieces = museum.museumPieces.Pairs
                    .Select(piece => new MuseumPieceProgressRef
                    {
                        TileX = (int)piece.Key.X,
                        TileY = (int)piece.Key.Y,
                        ItemId = piece.Value
                    })
                    .OrderBy(piece => piece.TileY)
                    .ThenBy(piece => piece.TileX)
                    .ThenBy(piece => piece.ItemId, StringComparer.Ordinal)
                    .ToArray(),
                DonatedCount = museum.museumPieces.Count()
            };
    }

    private static PerfectionProgressRef? ReadPerfection(StardewValley.Network.NetWorldState? world)
    {
        if (world is null)
        {
            return null;
        }

        var percentComplete = Utility.percentGameComplete();
        var waivers = world.PerfectionWaivers;
        var effectivePercent = percentComplete + waivers * 0.01f;

        return new PerfectionProgressRef
        {
            PercentComplete = percentComplete,
            PercentFloor = Math.Floor(percentComplete * 100f),
            PerfectionWaivers = waivers,
            EffectivePercentWithWaivers = effectivePercent,
            IsCompleteWithWaivers = effectivePercent >= 1f
        };
    }

    private static GoldenWalnutProgressRef? ReadGoldenWalnuts(StardewValley.Network.NetWorldState? world)
    {
        if (world is null)
        {
            return null;
        }

        var found = world.GoldenWalnutsFound;
        var qiRoomActualFound = Math.Max(0, found - 1);

        return new GoldenWalnutProgressRef
        {
            Current = world.GoldenWalnuts,
            Found = found,
            FoundCappedForPerfection = Math.Min(found, GoldenWalnutPerfectionTarget),
            PerfectionTarget = GoldenWalnutPerfectionTarget,
            QiRoomActualFound = qiRoomActualFound,
            QiRoomUnlockTarget = GoldenWalnutQiRoomUnlockTarget,
            QiRoomUnlocked = qiRoomActualFound >= GoldenWalnutQiRoomUnlockTarget
        };
    }

    private static Dictionary<string, int>? ToSortedDictionary(NetStringDictionary<int, Netcode.NetInt>? source)
    {
        return source?.Pairs
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    private static Dictionary<string, int[]>? ToSortedArrayDictionary(NetStringIntArrayDictionary? source)
    {
        return source?.Pairs
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
    }
}
