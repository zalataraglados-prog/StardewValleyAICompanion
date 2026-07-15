using System.Linq;
using StardewAI.Contracts.State;
using StardewValley.Network;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Quests;
using StardewValley.SpecialOrders;
using StardewValley.SpecialOrders.Objectives;
using StardewValley.SpecialOrders.Rewards;

namespace StardewAI.TransparentBridge.Adapters;

public sealed class ProgressQuestReadAdapter : ReadAdapterBase
{
    private readonly IQuestProgressMapper mapper;

    public ProgressQuestReadAdapter()
        : this(new ReadQuestProgressMapper())
    {
    }

    public ProgressQuestReadAdapter(IQuestProgressMapper mapper)
    {
        this.mapper = mapper;
    }

    public override string Domain => "quests_progress";
    public override int Priority => 60;

    public override StateAdapterResult Collect(long tick)
    {
        var player = Context.IsWorldReady ? Game1.player : null;
        var team = player?.team;

        var fields = new Dictionary<string, object>
        {
            ["active_quests"] = Field(ReadActiveQuests(player), "Game1.player.questLog", tick),
            ["completed_quests"] = Field(ReadCompletedQuests(player), "Game1.stats.QuestsCompleted; Game1.player.questLog where Quest.completed", tick),
            ["mail_received"] = Field(player?.mailReceived.OrderBy(id => id).ToArray(), "Game1.player.mailReceived", tick),
            ["mail_for_tomorrow"] = Field(player?.mailForTomorrow.OrderBy(id => id).ToArray(), "Game1.player.mailForTomorrow", tick),
            ["mailbox"] = Field(player?.mailbox.OrderBy(id => id).ToArray(), "Game1.player.mailbox", tick),
            ["special_orders"] = Field(ReadSpecialOrders(team), "Game1.player.team.specialOrders", tick),
            ["completed_special_orders"] = Field(team?.completedSpecialOrders.OrderBy(id => id).ToArray(), "Game1.player.team.completedSpecialOrders", tick),
            ["accepted_special_order_types"] = Field(team?.acceptedSpecialOrderTypes.OrderBy(id => id).ToArray(), "Game1.player.team.acceptedSpecialOrderTypes", tick)
        };

        return Section("quests", fields, Array.Empty<string>());
    }

    private QuestProgressRef[]? ReadActiveQuests(Farmer? player)
    {
        return player?.questLog
            .Select(quest => mapper.MapQuest(quest))
            .OrderBy(quest => quest.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private CompletedQuestProgressRef? ReadCompletedQuests(Farmer? player)
    {
        return mapper.MapCompletedQuests(player);
    }

    private SpecialOrderProgressRef[]? ReadSpecialOrders(FarmerTeam? team)
    {
        return team?.specialOrders
            .Select(order => mapper.MapSpecialOrder(order))
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
            ["golden_walnuts"] = Field(ReadGoldenWalnuts(world), "Game1.netWorldState.Value.GoldenWalnuts/GoldenWalnutsFound", tick),
            ["full_shipment_progress"] = Field(ReadFullShipmentProgress(master), "Game1.objectData raw parse; Game1.MasterPlayer.basicShipped; Object.isPotentialBasicShipped(itemId, category, objectType); category != -7 && category != -2", tick)
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
            LocationAccessible = Game1.isLocationAccessible("CommunityCenter"),
            Completed = master.hasCompletedCommunityCenter(),
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

    private static FullShipmentProgressRef? ReadFullShipmentProgress(Farmer? master)
    {
        if (master is null || !Context.IsWorldReady)
        {
            return null;
        }

        var shipped = master.basicShipped;

        var eligibleItems = new List<FullShipmentItemProgressRef>();
        var objectData = Game1.objectData;
        if (objectData is null)
        {
            return null;
        }

        foreach (var kv in objectData)
        {
            var itemId = kv.Key;
            var data = kv.Value;
            if (data is null)
            {
                continue;
            }

            var category = data.Category;
            var objectType = data.Type;

            if (!IsEligibleForFullShipment(itemId, category, objectType))
            {
                continue;
            }

            var qualifiedItemId = ItemRegistry.QualifyItemId(itemId) ?? "(O)" + itemId;
            var itemShippedCount = shipped.TryGetValue(itemId, out var count) ? count : 0;

            eligibleItems.Add(new FullShipmentItemProgressRef
            {
                ItemId = itemId,
                QualifiedItemId = qualifiedItemId,
                DisplayName = data.DisplayName ?? data.Name ?? itemId,
                Category = category,
                ObjectType = objectType ?? string.Empty,
                CurrentShippedCount = itemShippedCount,
                Shipped = itemShippedCount > 0
            });
        }

        var sorted = eligibleItems
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .ThenBy(item => item.QualifiedItemId, StringComparer.Ordinal)
            .ToArray();

        var shippedEligibleCount = sorted.Count(item => item.Shipped);
        var totalCount = sorted.Length;
        var missing = sorted.Where(item => !item.Shipped).Select(item => item.ItemId).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        return new FullShipmentProgressRef
        {
            EligibleItemCount = totalCount,
            ShippedEligibleItemCount = shippedEligibleCount,
            MissingItemCount = totalCount - shippedEligibleCount,
            CompletionRatio = totalCount > 0 ? (double)shippedEligibleCount / totalCount : 0,
            Complete = shippedEligibleCount == totalCount && totalCount > 0,
            Items = sorted,
            MissingItemIds = missing
        };
    }

    private static bool IsEligibleForFullShipment(string itemId, int category, string objectType)
    {
        return category != -7 && category != -2 && StardewValley.Object.isPotentialBasicShipped(itemId, category, objectType);
    }
}
