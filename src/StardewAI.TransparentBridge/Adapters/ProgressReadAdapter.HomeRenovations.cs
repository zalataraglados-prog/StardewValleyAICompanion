using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using Netcode;
using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.GameData.HomeRenovations;
using StardewValley.Locations;
using StardewValley.Objects;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class WorldProgressReadAdapter
{
    internal const string HomeRenovationDataPayloadSha256 = "26bdcd0681a57c1f749d249ad9305ffa1d58c433c86c1a0b954d0052c6d5d40b";
    internal const string HomeRenovationNativeContract =
        "GameLocation.checkAction Carpenter -> answerDialogue Renovate -> ShopMenu HouseRenovations exact row -> RenovateMenu hover and world-region click -> native validate, money/FirstPurchase, renovation actions, UpdateForRenovation, renovateEvent, animation and return; no direct money, mail, NetInt, map, furniture, menu, viewport or event mutation";

    private static readonly JsonSerializerOptions HomeRenovationPayloadOptions = new()
    {
        IncludeFields = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = false
    };

    private static HomeRenovationCatalogRef ReadHomeRenovations(
        Farmer actor,
        GameLocation scienceHouse,
        MapActionTileRef? actionTile,
        bool robinAtCounter,
        bool buildingUnderConstruction,
        bool menuClear)
    {
        var home = Utility.getHomeOfFarmer(actor) as FarmHouse;
        if (home is null)
        {
            return new HomeRenovationCatalogRef
            {
                ProjectionStatus = "home_location_unavailable",
                ServiceActionRaw = actionTile?.Action ?? string.Empty,
                ServiceActionTileX = actionTile?.X,
                ServiceActionTileY = actionTile?.Y,
                RobinPresentAtService = robinAtCounter,
                ServiceStatus = "home_location_unavailable",
                NativeContract = HomeRenovationNativeContract
            };
        }

        var data = DataLoader.HomeRenovations(Game1.content);
        var payloadHash = HomeRenovationPayloadHash(data);
        var dataStatus = string.Equals(payloadHash, HomeRenovationDataPayloadSha256, StringComparison.Ordinal)
            ? "exact_locked_base_1.6.15"
            : "drifted_data_home_renovations_payload";
        var nativeAvailable = HouseRenovation.GetAvailableRenovations()
            .OfType<HouseRenovation>()
            .Select(value => value.Name)
            .ToArray();
        var nativeIndexes = nativeAvailable
            .Select((id, index) => (id, index))
            .ToDictionary(value => value.id, value => value.index, StringComparer.Ordinal);
        var currentAtService = ReferenceEquals(Game1.currentLocation, scienceHouse);
        var serviceStatus = actor.HouseUpgradeLevel < 2
            ? "farmhouse_level_2_required"
            : actor.daysUntilHouseUpgrade.Value >= 0
                ? "farmhouse_upgrade_in_progress"
                : buildingUnderConstruction
                    ? "another_building_under_construction"
                    : actionTile is null || actionTile.Action != "Carpenter"
                        ? "carpenter_service_action_missing"
                        : !Game1.isLocationAccessible("ScienceHouse")
                            ? "science_house_not_accessible"
                            : !currentAtService
                                ? "route_to_carpenter_service_required"
                                : !robinAtCounter
                                    ? "robin_not_present_at_service"
                                    : !menuClear
                                        ? "carpenter_menu_or_dialogue_not_clear"
                                        : "ready";
        var cribReasons = CribModificationBlockReasons(home).ToArray();
        var options = data.Select(pair => ReadHomeRenovationOption(
                actor, home, pair.Key, pair.Value, payloadHash, dataStatus,
                nativeIndexes.TryGetValue(pair.Key, out var index) ? index : (int?)null))
            .ToList();
        var catalogExact = options.Count == 18 &&
            options.Select(value => value.RenovationId).SequenceEqual(data.Keys, StringComparer.Ordinal) &&
            options.Where(value => value.NativeMenuAvailable).Select(value => value.RenovationId)
                .SequenceEqual(nativeAvailable, StringComparer.Ordinal);

        return new HomeRenovationCatalogRef
        {
            ProjectionStatus = dataStatus == "exact_locked_base_1.6.15" && catalogExact
                ? "complete_live_native_home_renovation_catalog"
                : dataStatus == "exact_locked_base_1.6.15"
                    ? "native_available_order_or_option_count_drifted"
                    : dataStatus,
            DataPayloadSha256 = payloadHash,
            DataContractStatus = dataStatus,
            HomeLocationId = home.NameOrUniqueName,
            HomeRuntimeType = home.GetType().FullName ?? home.GetType().Name,
            HouseUpgradeLevel = home.upgradeLevel,
            CribStyle = home.cribStyle.Value,
            CanModifyCrib = home.CanModifyCrib(),
            CribModificationBlockReasons = cribReasons,
            ServiceActionRaw = actionTile?.Action ?? string.Empty,
            ServiceActionTileX = actionTile?.X,
            ServiceActionTileY = actionTile?.Y,
            RobinPresentAtService = robinAtCounter,
            ServiceStatus = serviceStatus,
            NativeAvailableRenovationIds = nativeAvailable,
            Options = options,
            NativeContract = HomeRenovationNativeContract
        };
    }

    private static HomeRenovationOptionRef ReadHomeRenovationOption(
        Farmer actor,
        FarmHouse home,
        string id,
        HomeRenovation data,
        string payloadHash,
        string dataStatus,
        int? nativeShopIndex)
    {
        var requirements = (data.Requirements ?? new List<RenovationValue>())
            .Select(value => ReadRenovationValue(home, actor, value, requirement: true))
            .ToList();
        var actions = (data.RenovateActions ?? new List<RenovationValue>())
            .Select(value => ReadRenovationValue(home, actor, value, requirement: false))
            .ToList();
        var regions = HomeRenovationRegions(home, data);
        var requirementSatisfied = requirements.All(value => value.Satisfied == true);
        var actionProjectionExact = actions.All(value => value.ProjectionStatus == "exact");
        var specialRectReady = string.IsNullOrWhiteSpace(data.SpecialRect) ||
            data.SpecialRect == "crib" && home.CanModifyCrib() && home.GetCribBounds().HasValue;
        var boundsReady = regions.Count > 0 && regions.All(value => value.Rectangles.Count > 0);
        var nativeMenuAvailable = requirementSatisfied && actionProjectionExact && specialRectReady && boundsReady;
        var roomId = string.IsNullOrWhiteSpace(data.RoomId) ? id : data.RoomId;
        var firstPurchaseMail = "FirstPurchase_" + roomId;
        var firstPurchaseBefore = actor.mailReceived.Contains(firstPurchaseMail);
        var refundEligible = data.Price < 0 && firstPurchaseBefore;
        var expectedMoneyAfter = data.Price < 0
            ? refundEligible ? actor.Money - data.Price : actor.Money
            : actor.Money - data.Price;
        var reasons = new List<string>();
        if (dataStatus != "exact_locked_base_1.6.15")
            reasons.Add("home_renovation_data_contract_drifted");
        if (!requirementSatisfied)
            reasons.AddRange(requirements.Where(value => value.Satisfied != true).Select(value => "requirement_unsatisfied:" + value.Type + ":" + value.Key + ":" + value.ValueExpression));
        if (!actionProjectionExact)
            reasons.Add("renovation_action_projection_unsupported_or_drifted");
        if (!specialRectReady)
            reasons.Add("special_rect_unavailable_or_crib_modification_blocked");
        if (!boundsReady)
            reasons.Add("renovation_bounds_unavailable");
        if (data.Price > 0 && actor.Money < data.Price)
            reasons.Add("insufficient_money");
        if (nativeMenuAvailable != nativeShopIndex.HasValue)
            reasons.Add("native_menu_membership_drifted");

        var strings = ReadRenovationStrings(data.TextStrings);
        var fingerprintSource = string.Join("|", id, payloadHash, home.NameOrUniqueName, home.upgradeLevel,
            data.Price, roomId, data.AnimationType, data.CheckForObstructions, data.SpecialRect,
            nativeShopIndex, actor.Money, firstPurchaseBefore,
            string.Join(",", requirements.Select(value => value.Type + ":" + value.Key + ":" + value.ValueExpression + ":" + value.CurrentIntValue + ":" + value.CurrentBoolValue + ":" + value.Satisfied)),
            string.Join(",", actions.Select(value => value.Type + ":" + value.Key + ":" + value.ValueExpression + ":" + value.CurrentIntValue + ":" + value.CurrentBoolValue)),
            string.Join(",", regions.Select(value => value.SelectedIndex + ":" + value.ObstructionStatus + ":" + string.Join(";", value.BlockedTiles) + ":" + string.Join(";", value.IntersectingFurniture))));

        return new HomeRenovationOptionRef
        {
            RenovationId = id,
            DisplayName = strings.DisplayName,
            Description = strings.Description,
            PlacementText = strings.PlacementText,
            Price = data.Price,
            RoomId = roomId,
            AnimationType = data.AnimationType ?? string.Empty,
            IsDestructive = string.Equals(data.AnimationType, "destroy", StringComparison.Ordinal),
            CheckForObstructions = data.CheckForObstructions,
            SpecialRect = data.SpecialRect ?? string.Empty,
            Requirements = requirements,
            RenovateActions = actions,
            Regions = regions,
            RequirementsSatisfied = requirementSatisfied,
            NativeMenuAvailable = nativeMenuAvailable,
            NativeShopIndex = nativeShopIndex,
            FirstPurchaseMailId = firstPurchaseMail,
            FirstPurchaseMailBefore = firstPurchaseBefore,
            ExpectedFirstPurchaseMailAfter = data.Price >= 0 || firstPurchaseBefore,
            MoneyBefore = actor.Money,
            ExpectedMoneyAfter = expectedMoneyAfter,
            RefundEligible = refundEligible,
            AvailabilityStatus = reasons.Count == 0 ? "available_in_native_renovation_shop" : "blocked",
            AvailabilityBlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            ProjectionFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource))).ToLowerInvariant()
        };
    }

    private static HomeRenovationValueRef ReadRenovationValue(
        FarmHouse home,
        Farmer actor,
        RenovationValue value,
        bool requirement)
    {
        if (value.Type == "Value")
        {
            var field = home.GetType().GetField(value.Key, BindingFlags.Instance | BindingFlags.Public);
            var netInt = field?.GetValue(home) as NetInt;
            var current = netInt?.Value;
            bool? satisfied = null;
            var status = netInt is null ? "missing_or_non_NetInt_field" : "exact";
            if (requirement && current.HasValue)
            {
                var expression = value.Value ?? string.Empty;
                var equals = !expression.StartsWith('!');
                var numberText = equals ? expression : expression[1..];
                if (int.TryParse(numberText, out var expected))
                    satisfied = (current.Value == expected) == equals;
                else
                    status = "invalid_integer_requirement";
            }
            return new HomeRenovationValueRef
            {
                Type = value.Type ?? string.Empty,
                Key = value.Key ?? string.Empty,
                ValueExpression = value.Value ?? string.Empty,
                CurrentIntValue = current,
                Satisfied = requirement ? satisfied : true,
                ProjectionStatus = status
            };
        }
        if (value.Type == "Mail")
        {
            var current = actor.hasOrWillReceiveMail(value.Key);
            return new HomeRenovationValueRef
            {
                Type = value.Type,
                Key = value.Key ?? string.Empty,
                ValueExpression = value.Value ?? string.Empty,
                CurrentBoolValue = current,
                Satisfied = requirement ? current == (value.Value == "1") : true,
                ProjectionStatus = "exact"
            };
        }
        return new HomeRenovationValueRef
        {
            Type = value.Type ?? string.Empty,
            Key = value.Key ?? string.Empty,
            ValueExpression = value.Value ?? string.Empty,
            Satisfied = false,
            ProjectionStatus = "unsupported_type"
        };
    }

    private static List<HomeRenovationRegionRef> HomeRenovationRegions(FarmHouse home, HomeRenovation data)
    {
        var groups = new List<List<Rectangle>>();
        if (data.SpecialRect == "crib")
        {
            var crib = home.GetCribBounds();
            if (crib.HasValue && home.CanModifyCrib())
                groups.Add(new List<Rectangle> { crib.Value });
        }
        else
        {
            foreach (var group in data.RectGroups ?? new List<RectGroup>())
            {
                groups.Add((group.Rects ?? new List<Rect>())
                    .Select(rect => new Rectangle(rect.X, rect.Y, rect.Width, rect.Height))
                    .ToList());
            }
        }

        return groups.Select((rectangles, index) => ReadRenovationRegion(home, data.CheckForObstructions, index, rectangles)).ToList();
    }

    private static HomeRenovationRegionRef ReadRenovationRegion(
        FarmHouse home,
        bool checkForObstructions,
        int index,
        List<Rectangle> rectangles)
    {
        var reasons = new HashSet<string>(StringComparer.Ordinal);
        var tiles = new HashSet<string>(StringComparer.Ordinal);
        var furniture = new HashSet<string>(StringComparer.Ordinal);
        if (checkForObstructions)
        {
            foreach (var rect in rectangles)
            {
                for (var y = rect.Top; y < rect.Bottom; y++)
                {
                    for (var x = rect.Left; x < rect.Right; x++)
                    {
                        var tile = new Vector2(x, y);
                        if (home.isTileOccupiedByFarmer(tile) is not null)
                        {
                            reasons.Add("farmer_occupies_region_tile");
                            tiles.Add(x + "," + y + ":farmer");
                        }
                        if (home.IsTileOccupiedBy(tile))
                        {
                            reasons.Add("location_tile_occupied");
                            tiles.Add(x + "," + y + ":occupied");
                        }
                    }
                }
                var pixelBounds = new Rectangle(rect.X * Game1.tileSize, rect.Y * Game1.tileSize,
                    rect.Width * Game1.tileSize, rect.Height * Game1.tileSize);
                foreach (var item in home.furniture.Where(item => item.GetBoundingBox().Intersects(pixelBounds)))
                {
                    reasons.Add("furniture_intersects_region");
                    furniture.Add(item.QualifiedItemId + "@" + item.TileLocation.X + "," + item.TileLocation.Y);
                }
            }
        }
        return new HomeRenovationRegionRef
        {
            SelectedIndex = index,
            Rectangles = rectangles.Select(rect => new HomeRenovationRectRef
            {
                X = rect.X,
                Y = rect.Y,
                Width = rect.Width,
                Height = rect.Height
            }).ToList(),
            ObstructionStatus = !checkForObstructions
                ? "native_obstruction_check_not_required"
                : reasons.Count == 0 ? "clear" : "blocked",
            ObstructionReasons = reasons.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            BlockedTiles = tiles.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            IntersectingFurniture = furniture.OrderBy(value => value, StringComparer.Ordinal).ToArray()
        };
    }

    private static IEnumerable<string> CribModificationBlockReasons(FarmHouse home)
    {
        if (!home.HasOwner)
            yield return "home_has_no_owner";
        if (home.owner?.isMarriedOrRoommates() == true && home.owner.GetSpouseFriendship().DaysUntilBirthing != -1)
            yield return "birth_or_adoption_pending";
        if (home.owner?.getChildren().Any(child => child.Age < 3) == true)
            yield return "baby_or_toddler_present";
        if (home.upgradeLevel < 2 || !home.GetCribBounds().HasValue)
            yield return "crib_bounds_require_house_upgrade_level_2";
    }

    private static (string DisplayName, string Description, string PlacementText) ReadRenovationStrings(string textAsset)
    {
        try
        {
            var parts = Game1.content.LoadString(textAsset).Split('/');
            return (parts.ElementAtOrDefault(0) ?? "?", parts.ElementAtOrDefault(1) ?? "?", parts.ElementAtOrDefault(2) ?? "?");
        }
        catch
        {
            return ("?", "?", "?");
        }
    }

    private static string HomeRenovationPayloadHash(Dictionary<string, HomeRenovation> data)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(data, typeof(Dictionary<string, HomeRenovation>), HomeRenovationPayloadOptions);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
