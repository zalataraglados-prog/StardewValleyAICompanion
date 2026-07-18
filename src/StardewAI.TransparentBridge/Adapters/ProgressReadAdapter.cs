using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using Netcode;
using StardewAI.Contracts.State;
using StardewValley.Network;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
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
        var communityCenter = Context.IsWorldReady ? Game1.getLocationFromName("CommunityCenter") as CommunityCenter : null;

        var fields = new Dictionary<string, object>
        {
            ["community_center"] = Field(ReadCommunityCenter(world, master, communityCenter), "NetWorldState.BundleData/Bundles/BundleRewards; CommunityCenter bundle mutex/note state; Bundle.IsValidItemForThisIngredientDescription; host JojaMember/ccIsComplete received-or-pending flags", tick),
            ["joja_membership"] = Field(Context.IsWorldReady ? master?.mailReceived.Contains("JojaMember") : null, "Game1.MasterPlayer.mailReceived.Contains(\"JojaMember\")", tick),
            ["museum"] = Field(ReadMuseum(museum, master), "LibraryMuseum.museumPieces/totalArtifacts/IsItemSuitableForDonation/isTileSuitableForMuseumPiece; Data/MuseumRewards[museum60]; Events/Farm[66]", tick),
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

    private static CommunityCenterProgressRef? ReadCommunityCenter(
        StardewValley.Network.NetWorldState? world,
        Farmer? master,
        CommunityCenter? communityCenter)
    {
        if (world is null || master is null || communityCenter is null || Game1.player is null)
        {
            return null;
        }

        var areaFlags = new[] { "ccBoilerRoom", "ccCraftsRoom", "ccPantry", "ccFishTank", "ccVault", "ccBulletin" };

        var jojaReceived = master.mailReceived.Contains("JojaMember");
        var jojaPending = HasPendingMail(master, "JojaMember");
        var ccCompleteFlag = master.hasOrWillReceiveMail("ccIsComplete");
        var ccCompleteNative = master.hasCompletedCommunityCenter();
        var jojaLocked = jojaReceived || jojaPending;
        var communityCenterLocked = ccCompleteFlag || ccCompleteNative;
        var routeState = jojaLocked && communityCenterLocked
            ? "conflicting_irreversible_flags"
            : jojaLocked
                ? "joja_locked"
                : communityCenterLocked
                    ? "community_center_locked"
                    : "undecided";
        var bundleRows = ReadCommunityCenterBundles(world, communityCenter, routeState);

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
                .ToArray(),
            RouteState = routeState,
            RouteStateReason = routeState switch
            {
                "conflicting_irreversible_flags" => "joja_and_community_center_irreversible_flags_both_present",
                "joja_locked" => jojaReceived ? "host_received_JojaMember" : "host_has_pending_JojaMember",
                "community_center_locked" => ccCompleteFlag
                    ? "host_ccIsComplete_received_or_pending"
                    : "host_native_community_center_completion_true",
                _ => "neither_irreversible_route_flag_present"
            },
            MaxGrandpaScoreRoute = routeState switch
            {
                "joja_locked" => "joja",
                "conflicting_irreversible_flags" => "unavailable_conflicting_irreversible_flags",
                _ => "community_center"
            },
            JojaMembershipReceived = jojaReceived,
            JojaMembershipPending = jojaPending,
            CommunityCenterCompleteFlagReceivedOrPending = ccCompleteFlag,
            CommunityCenterCompleteNative = ccCompleteNative,
            CommunityCenterIsCurrentLocation = ReferenceEquals(Game1.currentLocation, communityCenter),
            BundleDataRowCount = world.BundleData.Count,
            ProjectedBundleRowCount = bundleRows.Length,
            UnavailableBundleRowCount = bundleRows.Count(row => row.ProjectionStatus != "exact"),
            BundleRows = bundleRows
        };
    }

    private static CommunityCenterBundleProgressRef[] ReadCommunityCenterBundles(
        StardewValley.Network.NetWorldState world,
        CommunityCenter communityCenter,
        string routeState)
    {
        return world.BundleData
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => ReadCommunityCenterBundle(pair.Key, pair.Value, communityCenter, routeState))
            .ToArray();
    }

    private static CommunityCenterBundleProgressRef ReadCommunityCenterBundle(
        string dataKey,
        string raw,
        CommunityCenter communityCenter,
        string routeState)
    {
        var keyParts = dataKey.Split('/');
        var fields = raw.Split('/');
        if (keyParts.Length < 2 || fields.Length < Bundle.FieldCount ||
            !int.TryParse(keyParts[1], out var bundleId))
        {
            return FailedCommunityCenterBundle(dataKey, "bundle_data_key_or_field_count_invalid");
        }
        var areaName = keyParts[0];
        var areaId = CommunityCenter.getAreaNumberFromName(areaName);
        if (areaId < 0 || !communityCenter.bundles.TryGetValue(bundleId, out var completedBits))
        {
            return FailedCommunityCenterBundle(dataKey, "bundle_area_or_completion_bits_unavailable", bundleId, areaId, areaName);
        }
        var ingredientParts = ArgUtility.SplitBySpace(fields[Bundle.IngredientsIndex]);
        if (ingredientParts.Length % 3 != 0 || completedBits.Length < ingredientParts.Length / 3)
        {
            return FailedCommunityCenterBundle(dataKey, "bundle_ingredient_shape_or_completion_bits_invalid", bundleId, areaId, areaName);
        }
        var ingredients = new List<BundleIngredientDescription>();
        for (var index = 0; index < ingredientParts.Length / 3; index++)
        {
            if (!int.TryParse(ingredientParts[index * 3 + 1], out var stack) || stack <= 0 ||
                !int.TryParse(ingredientParts[index * 3 + 2], out var quality) || quality < 0)
            {
                return FailedCommunityCenterBundle(dataKey, "bundle_ingredient_stack_or_quality_invalid", bundleId, areaId, areaName);
            }
            ingredients.Add(new BundleIngredientDescription(ingredientParts[index * 3], stack, quality, completedBits[index]));
        }
        var requiredSlots = ArgUtility.GetInt(fields, Bundle.NumberOfSlotsIndex, ingredients.Count);
        var completedCount = ingredients.Count(ingredient => ingredient.completed);
        var noteTile = CommunityCenterNoteTile(communityCenter, areaId);
        var noteAppears = areaId < communityCenter.areasComplete.Count && communityCenter.shouldNoteAppearInArea(areaId) && communityCenter.isJunimoNoteAtArea(areaId);
        var mutex = areaId >= 0 && areaId < communityCenter.bundleMutexes.Count ? communityCenter.bundleMutexes[areaId] : null;
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var sharedStatus = routeState == "conflicting_irreversible_flags"
            ? "community_center_route_state_conflict"
            : routeState == "joja_locked"
            ? "community_center_route_locked_out_by_joja"
            : !ReferenceEquals(Game1.currentLocation, communityCenter)
                ? "community_center_not_current_location"
                : !menuClear
                    ? "community_center_menu_or_dialogue_not_clear"
                    : !noteAppears || noteTile is null
                        ? "community_center_area_note_unavailable"
                        : mutex?.IsLocked() == true
                            ? "community_center_area_mutex_locked"
                            : "ready";
        var matcher = new Bundle(fields[Bundle.NameIndex], fields[Bundle.DisplayNameIndex], ingredients, completedBits, fields[Bundle.RewardIndex]);

        return new CommunityCenterBundleProgressRef
        {
            ProjectionStatus = "exact",
            ProjectionFailure = string.Empty,
            BundleDataKey = dataKey,
            BundleId = bundleId,
            AreaId = areaId,
            AreaName = areaName,
            InternalName = fields[Bundle.NameIndex],
            DisplayName = fields[Bundle.DisplayNameIndex],
            RewardDescription = fields[Bundle.RewardIndex],
            RequiredSlotCount = requiredSlots,
            CompletedIngredientCount = completedCount,
            Complete = completedCount >= requiredSlots,
            NoteAppears = noteAppears,
            NoteTileX = noteTile?.X,
            NoteTileY = noteTile?.Y,
            AreaMutexLocked = mutex?.IsLocked(),
            Ingredients = ingredients.Select((ingredient, index) => new CommunityCenterIngredientProgressRef
            {
                IngredientIndex = index,
                ItemIdOrCategory = ingredient.category?.ToString() ?? ingredient.id ?? string.Empty,
                RequiredStack = ingredient.stack,
                MinimumQuality = ingredient.quality,
                Completed = ingredient.completed
            }).ToArray(),
            DonationCandidates = Game1.player.Items
                .Select((item, slot) => new { item, slot })
                .Where(entry => entry.item is not null && entry.item.Stack > 0)
                .Select(entry => new
                {
                    entry.item,
                    entry.slot,
                    index = matcher.GetBundleIngredientDescriptionIndexForItem(entry.item)
                })
                .Where(entry => entry.index >= 0 && entry.index < ingredients.Count)
                .Select(entry => new { entry.item, entry.slot, ingredient = ingredients[entry.index], entry.index })
                .Where(entry => !entry.ingredient.completed && entry.item.Stack >= entry.ingredient.stack)
                .Select(entry => new CommunityCenterDonationCandidateRef
                {
                    InventorySlotIndex = entry.slot,
                    IngredientIndex = entry.index,
                    ItemId = entry.item.ItemId,
                    QualifiedItemId = entry.item.QualifiedItemId,
                    RuntimeType = entry.item.GetType().FullName ?? string.Empty,
                    Quality = entry.item.Quality,
                    StackBefore = entry.item.Stack,
                    StackAfter = entry.item.Stack - entry.ingredient.stack,
                    RequiredStack = entry.ingredient.stack,
                    InventoryItemTotalBefore = Game1.player.Items
                        .Where(item => item?.QualifiedItemId == entry.item.QualifiedItemId)
                        .Sum(item => item?.Stack ?? 0),
                    InventoryItemTotalAfter = Game1.player.Items
                        .Where(item => item?.QualifiedItemId == entry.item.QualifiedItemId)
                        .Sum(item => item?.Stack ?? 0) - entry.ingredient.stack,
                    CompletedIngredientCountBefore = completedCount,
                    CompletedIngredientCountAfter = completedCount + 1 >= requiredSlots ? ingredients.Count : completedCount + 1,
                    CompletesBundle = completedCount + 1 >= requiredSlots,
                    ActionStatus = sharedStatus
                })
                .OrderBy(candidate => candidate.InventorySlotIndex)
                .ThenBy(candidate => candidate.IngredientIndex)
                .ToArray()
        };
    }

    private static CommunityCenterBundleProgressRef FailedCommunityCenterBundle(
        string dataKey,
        string failure,
        int bundleId = -1,
        int areaId = -1,
        string areaName = "")
    {
        return new CommunityCenterBundleProgressRef
        {
            ProjectionStatus = "unavailable",
            ProjectionFailure = failure,
            BundleDataKey = dataKey,
            BundleId = bundleId,
            AreaId = areaId,
            AreaName = areaName
        };
    }

    private static Point? CommunityCenterNoteTile(CommunityCenter communityCenter, int areaId)
    {
        var method = typeof(CommunityCenter).GetMethod("getNotePosition", BindingFlags.Instance | BindingFlags.NonPublic);
        return method?.Invoke(communityCenter, new object[] { areaId }) is Point point && point != Point.Zero ? point : null;
    }

    private static bool HasPendingMail(Farmer farmer, string mailId)
    {
        return farmer.mailForTomorrow.Any(value =>
            string.Equals(value, mailId, StringComparison.Ordinal) ||
            value.StartsWith(mailId + "%&NL&%", StringComparison.Ordinal));
    }

    private static MuseumProgressRef? ReadMuseum(LibraryMuseum? museum, Farmer? master)
    {
        if (museum is null || master is null || Game1.player is null)
        {
            return null;
        }

        var donatedCount = museum.museumPieces.Count();
        var total = LibraryMuseum.totalArtifacts;
        var freeTiles = ReadFreeMuseumDonationTiles(museum);
        var guntherAction = ReadGuntherActionTile(museum);
        var mutex = typeof(LibraryMuseum)
            .GetField("mutex", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.GetValue(museum) as NetMutex;
        var rewards = DataLoader.MuseumRewards(Game1.content);
        rewards.TryGetValue("museum60", out var rustyKeyReward);
        var rustyKeyThreshold = rustyKeyReward?.TargetContextTags
            .FirstOrDefault(requirement => string.IsNullOrEmpty(requirement.Tag))
            ?.Count ?? 0;
        var rustyKeyActions = rustyKeyReward?.RewardActions;
        var rustyKeyAction = rustyKeyActions?.Count == 1 ? rustyKeyActions[0] : string.Empty;
        var museumIsCurrent = ReferenceEquals(Game1.currentLocation, museum);
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var sharedStatus = !museumIsCurrent
            ? "museum_not_current_location"
            : rustyKeyThreshold <= 0 || string.IsNullOrWhiteSpace(rustyKeyAction)
                ? "museum_rusty_key_reward_projection_unavailable"
                : !menuClear
                    ? "museum_menu_or_dialogue_not_clear"
                    : mutex?.IsLocked() == true
                        ? "museum_mutex_locked"
                        : guntherAction is null
                            ? "gunther_action_tile_unavailable"
                            : freeTiles.Length == 0
                                ? "museum_no_free_donation_tile"
                                : "ready";

        return new MuseumProgressRef
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
            DonatedCount = donatedCount,
            TotalDonatableItems = total,
            CollectionComplete = donatedCount >= total,
            CompleteCollectionAchievementReceived = Game1.player.achievements.Contains(5),
            RustyKeyDonationThreshold = rustyKeyThreshold,
            RustyKeyRewardId = "museum60",
            RustyKeyRewardAction = rustyKeyAction,
            RustyKeyRewardClaimed = Game1.player.mailReceived.Contains("museum60"),
            RustyKeyPrerequisiteEventSeen = master.eventsSeen.Contains("295672"),
            RustyKeyEventSeen = master.eventsSeen.Contains("66"),
            HasRustyKey = master.hasRustyKey,
            MuseumLocationId = museum.NameOrUniqueName,
            MuseumIsCurrentLocation = museumIsCurrent,
            MuseumMutexLocked = mutex?.IsLocked(),
            GuntherActionTileX = guntherAction?.X,
            GuntherActionTileY = guntherAction?.Y,
            GuntherActionRaw = guntherAction?.Action ?? string.Empty,
            FreeDonationTileX = freeTiles.FirstOrDefault()?.X,
            FreeDonationTileY = freeTiles.FirstOrDefault()?.Y,
            FreeDonationTileCount = freeTiles.Length,
            DonationCandidates = Game1.player.Items
                .Select((item, slot) => new { item, slot })
                .Where(entry => entry.item is StardewValley.Object && entry.item.Stack > 0)
                .Where(entry => LibraryMuseum.IsItemSuitableForDonation(entry.item.QualifiedItemId))
                .Select(entry => new MuseumDonationCandidateRef
                {
                    SlotIndex = entry.slot,
                    ItemId = entry.item.ItemId,
                    QualifiedItemId = entry.item.QualifiedItemId,
                    DisplayName = entry.item.DisplayName,
                    RuntimeType = entry.item.GetType().FullName ?? string.Empty,
                    StackBefore = entry.item.Stack,
                    StackAfter = entry.item.Stack - 1,
                    DonatedCountBefore = donatedCount,
                    DonatedCountAfter = donatedCount + 1,
                    CompletesCollection = donatedCount + 1 >= total,
                    ReachesRustyKeyThreshold = donatedCount < rustyKeyThreshold && donatedCount + 1 >= rustyKeyThreshold,
                    ActionStatus = sharedStatus
                })
                .OrderBy(candidate => candidate.SlotIndex)
                .ToArray()
        };
    }

    private static MuseumTileRef[] ReadFreeMuseumDonationTiles(LibraryMuseum museum)
    {
        var bounds = museum.getMuseumDonationBounds();
        var tiles = new List<MuseumTileRef>();
        for (var x = bounds.X; x <= bounds.Right; x++)
        {
            for (var y = bounds.Y; y <= bounds.Bottom; y++)
            {
                if (museum.isTileSuitableForMuseumPiece(x, y))
                {
                    tiles.Add(new MuseumTileRef(x, y, string.Empty));
                }
            }
        }
        return tiles.ToArray();
    }

    private static MuseumTileRef? ReadGuntherActionTile(LibraryMuseum museum)
    {
        var layers = museum.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray();
        if (layers is null || layers.Length == 0)
        {
            return null;
        }

        var width = layers.Max(layer => layer.LayerWidth);
        var height = layers.Max(layer => layer.LayerHeight);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var action = museum.doesTileHaveProperty(x, y, "Action", "Buildings");
                if (string.Equals(action?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), "Gunther", StringComparison.OrdinalIgnoreCase))
                {
                    return new MuseumTileRef(x, y, action!);
                }
            }
        }
        return null;
    }

    private sealed record MuseumTileRef(int X, int Y, string Action);

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
