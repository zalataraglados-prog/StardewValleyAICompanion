using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string PagedAnimalPurchaseFixturePrefix = "StardewAIAnimalPurchaseFixture";

    private TrainingExecutionResult ExecuteSetupAnimalPurchase(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return AnimalPurchaseBlocked(request, "debug_setup_animal_purchase", reasons.ToArray());

        if (string.Equals(request.TargetRuntimeIdentity, "full_chain_paged", StringComparison.Ordinal))
        {
            return ExecuteSetupPagedAnimalPurchaseChain(request);
        }

        var farm = Game1.getFarm();
        var requestedType = string.IsNullOrWhiteSpace(request.AnimalTypeId) ? "White Chicken" : request.AnimalTypeId;
        var possibleTypes = ReadPossibleFixtureAnimalTypes(requestedType);
        var home = farm.buildings.FirstOrDefault(building =>
            building.GetIndoors() is AnimalHouse house &&
            !building.isUnderConstruction() &&
            !house.isFull() &&
            FixtureHomeAcceptsAll(building, possibleTypes));
        if (home is null)
        {
            home = new Building("Coop", Vector2.Zero);
            home.FinishConstruction(onGameStart: true);
            home.LoadFromBuildingData(home.GetData(), forUpgrade: false, forConstruction: true);
            if (!TryFindFixtureBuildingTile(farm, home, out var fixtureTile))
                return AnimalPurchaseBlocked(request, "debug_setup_animal_purchase", "animal_purchase_fixture_building_tile_unavailable");
            home.tileX.Value = fixtureTile.X;
            home.tileY.Value = fixtureTile.Y;
            home.load();
            farm.buildings.Add(home);
        }

        if (home.GetIndoors() is not AnimalHouse animalHouse || animalHouse.isFull() ||
            !FixtureHomeAcceptsAll(home, possibleTypes))
            return AnimalPurchaseBlocked(request, "debug_setup_animal_purchase", "animal_purchase_fixture_home_unavailable");

        Game1.exitActiveMenu();
        Game1.player.forceCanMove();
        Game1.player.Halt();
        Game1.player.Money = Math.Max(Game1.player.Money, 50000);
        var animalShop = Game1.getLocationFromName("AnimalShop");
        if (animalShop is null)
            return AnimalPurchaseBlocked(request, "debug_setup_animal_purchase", "animal_purchase_fixture_shop_missing");
        Game1.currentLocation = animalShop;
        Game1.player.currentLocation = animalShop;

        var stock = Utility.getPurchaseAnimalStock(farm);
        var targetStock = stock.FirstOrDefault(item => item.Name == requestedType && item.Type is null);
        if (targetStock is null)
            return AnimalPurchaseBlocked(request, "debug_setup_animal_purchase", "animal_purchase_fixture_stock_unavailable");
        Game1.activeClickableMenu = new PurchaseAnimalsMenu(stock, farm);

        var verified = Game1.activeClickableMenu is PurchaseAnimalsMenu menu &&
            menu.TargetLocation?.NameOrUniqueName == farm.NameOrUniqueName &&
            menu.animalsToPurchase.Any(button => button.hoverText == requestedType) &&
            animalHouse.animalsThatLiveHere.Count < animalHouse.animalLimit.Value;
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
            PrimitiveKind = "debug_setup_animal_purchase",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_fixture_native_PurchaseAnimalsMenu_open", "compatible_nonfull_animal_home_available" }
                : new[] { "animal_purchase_fixture_postcondition_mismatch" },
            RequestedEffect = "menu=PurchaseAnimalsMenu;animal_type=" + requestedType,
            ObservedEffect = "menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
                ";home=" + home.buildingType.Value + "@" + home.tileX.Value + "," + home.tileY.Value +
                ";occupancy=" + animalHouse.animalsThatLiveHere.Count + "/" + animalHouse.animalLimit.Value +
                ";money=" + Game1.player.Money,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "animal_purchase_fixture_postcondition_mismatch" }
        };
    }

    private TrainingExecutionResult ExecuteSetupPagedAnimalPurchaseChain(TrainingExecutionRequest request)
    {
        var requestedType = string.IsNullOrWhiteSpace(request.AnimalTypeId) ? "White Chicken" : request.AnimalTypeId;
        var possibleTypes = ReadPossibleFixtureAnimalTypes(requestedType);
        var targetLocationId = PagedAnimalPurchaseFixturePrefix + "7";
        foreach (var stale in Game1.locations
            .Where(location => location.Name.StartsWith(PagedAnimalPurchaseFixturePrefix, StringComparison.Ordinal))
            .ToArray())
        {
            Game1.locations.Remove(stale);
        }

        for (var index = 1; index <= 7; index++)
        {
            var location = new GameLocation("Maps\\Farm", PagedAnimalPurchaseFixturePrefix + index);
            var home = new Building("Coop", new Vector2(2, 2));
            home.FinishConstruction(onGameStart: true);
            home.LoadFromBuildingData(home.GetData(), forUpgrade: false, forConstruction: true);
            home.load();
            location.buildings.Add(home);
            Game1.locations.Add(location);
        }

        var targetLocation = Game1.getLocationFromName(targetLocationId);
        var targetHome = targetLocation?.buildings.SingleOrDefault();
        if (targetLocation is null || targetHome?.GetIndoors() is not AnimalHouse targetHouse ||
            targetHouse.isFull() || !FixtureHomeAcceptsAll(targetHome, possibleTypes))
        {
            return AnimalPurchaseBlocked(request, "debug_setup_animal_purchase", "animal_purchase_paged_fixture_home_unavailable");
        }

        var animalShop = Game1.getLocationFromName("AnimalShop");
        var marnie = Game1.getCharacterFromName("Marnie");
        if (animalShop is null || marnie is null)
        {
            return AnimalPurchaseBlocked(request, "debug_setup_animal_purchase", "animal_purchase_paged_fixture_shop_or_marnie_missing");
        }

        Game1.exitActiveMenu();
        Game1.player.forceCanMove();
        Game1.player.Halt();
        Game1.player.Money = Math.Max(Game1.player.Money, 50000);
        Game1.timeOfDay = 1000;
        Game1.currentLocation = animalShop;
        Game1.player.currentLocation = animalShop;
        Game1.player.Position = new Vector2(12, 16) * Game1.tileSize;
        Game1.warpCharacter(marnie, animalShop.NameOrUniqueName, new Vector2(12, 14));

        var action = animalShop.doesTileHaveProperty(12, 15, "Action", "Buildings") ?? string.Empty;
        var eligibleLocations = Game1.locations.Count(location =>
            location.buildings.Any(building => building.GetIndoors() is AnimalHouse) &&
            (!Game1.IsClient || location.CanBeRemotedlyViewed()));
        var stockAvailable = Utility.getPurchaseAnimalStock(targetLocation)
            .Any(item => item.Name == requestedType && item.Type is null);
        var verified = Game1.activeClickableMenu is null && Game1.timeOfDay == 1000 &&
            Game1.currentLocation.NameOrUniqueName == "AnimalShop" &&
            Game1.player.TilePoint == new Point(12, 16) &&
            marnie.currentLocation?.NameOrUniqueName == "AnimalShop" &&
            marnie.TilePoint == new Point(12, 14) &&
            action.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() is "AnimalShop" or "Marnie" &&
            eligibleLocations >= 7 && stockAvailable;
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
            PrimitiveKind = "debug_setup_animal_purchase_full_chain_paged",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_fixture_seven_or_more_native_animal_home_locations_available",
                    "native_AnimalShop_counter_and_Marnie_ready_without_open_menu"
                }
                : new[] { "animal_purchase_paged_fixture_postcondition_mismatch" },
            RequestedEffect = "menu=none;counter=AnimalShop@12,15;target_location=" + targetLocationId,
            ObservedEffect = "menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") +
                ";player=" + Game1.currentLocation.NameOrUniqueName + "@" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y +
                ";marnie=" + (marnie.currentLocation?.NameOrUniqueName ?? "none") + "@" + marnie.TilePoint.X + "," + marnie.TilePoint.Y +
                ";time=" + Game1.timeOfDay + ";action=" + action + ";eligible_locations=" + eligibleLocations + ";target_location=" + targetLocationId,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "animal_purchase_paged_fixture_postcondition_mismatch" }
        };
    }

    private static string[] ReadPossibleFixtureAnimalTypes(string baseTypeId)
    {
        if (Game1.farmAnimalData.TryGetValue(baseTypeId, out var data) && data.AlternatePurchaseTypes is not null)
        {
            foreach (var alternate in data.AlternatePurchaseTypes)
            {
                if (GameStateQuery.CheckConditions(alternate.Condition) && alternate.AnimalIds is { Count: > 0 })
                    return alternate.AnimalIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
            }
        }
        return new[] { baseTypeId };
    }

    private static bool FixtureHomeAcceptsAll(Building building, IEnumerable<string> animalTypeIds)
    {
        return animalTypeIds.All(typeId =>
            new FarmAnimal(typeId, -1, Game1.player.UniqueMultiplayerID).CanLiveIn(building));
    }

    private static bool TryFindFixtureBuildingTile(GameLocation location, Building building, out Point tile)
    {
        var layer = location.Map?.Layers.FirstOrDefault();
        var width = layer?.LayerWidth ?? 0;
        var height = layer?.LayerHeight ?? 0;
        for (var y = 2; y <= height - building.tilesHigh.Value - 2; y++)
        {
            for (var x = 2; x <= width - building.tilesWide.Value - 2; x++)
            {
                var overlapsBuilding = false;
                for (var offsetY = 0; offsetY < building.tilesHigh.Value && !overlapsBuilding; offsetY++)
                {
                    for (var offsetX = 0; offsetX < building.tilesWide.Value; offsetX++)
                    {
                        if (location.getBuildingAt(new Vector2(x + offsetX, y + offsetY)) is not null)
                        {
                            overlapsBuilding = true;
                            break;
                        }
                    }
                }
                if (!overlapsBuilding)
                {
                    tile = new Point(x, y);
                    return true;
                }
            }
        }
        tile = default;
        return false;
    }

    private sealed class ActiveAnimalPurchase
    {
        public ActiveAnimalPurchase(PendingExecution pending, PurchaseAnimalsMenu menu, GameLocation targetLocation,
            Building home, AnimalHouse house, HashSet<long> beforeAnimalIds, string[] possibleTypes)
        {
            Pending = pending;
            Menu = menu;
            TargetLocation = targetLocation;
            Home = home;
            House = house;
            BeforeAnimalIds = beforeAnimalIds;
            PossibleTypes = possibleTypes;
            MoneyBefore = Game1.player.Money;
            OccupantsBefore = house.animalsThatLiveHere.Count;
        }

        public PendingExecution Pending { get; }
        public PurchaseAnimalsMenu Menu { get; }
        public GameLocation TargetLocation { get; }
        public Building Home { get; }
        public AnimalHouse House { get; }
        public HashSet<long> BeforeAnimalIds { get; }
        public string[] PossibleTypes { get; }
        public int MoneyBefore { get; }
        public int OccupantsBefore { get; }
        public int ElapsedTicks { get; set; }
        public int Cooldown { get; set; }
        public bool StockClicked { get; set; }
        public bool HomeClicked { get; set; }
        public bool NameSubmitted { get; set; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
    }

    private TrainingExecutionResult ExecuteChooseAnimalPurchaseResponse(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return AnimalPurchaseBlocked(request, "choose_animal_purchase_response", reasons.ToArray());
        if (Game1.activeClickableMenu is not DialogueBox dialogue || !dialogue.isQuestion ||
            Game1.currentLocation is null ||
            !string.Equals(Game1.currentLocation.lastQuestionKey, request.ExpectedDialogueKey, StringComparison.Ordinal))
            return AnimalPurchaseBlocked(request, "choose_animal_purchase_response", "animal_purchase_expected_dialogue_missing");

        var serviceResponse = request.ExpectedDialogueKey == "Marnie" &&
            request.DialogueResponseKey == "Purchase" &&
            request.ExpectedMenuTypeAfter == "PurchaseAnimalsMenu|DialogueBox";
        var locationResponse = request.ExpectedDialogueKey == "pagedResponse" &&
            request.DialogueResponseKey == request.AnimalPurchaseTargetLocationId &&
            request.ExpectedMenuTypeAfter == "PurchaseAnimalsMenu";
        var pageResponse = request.ExpectedDialogueKey == "pagedResponse" &&
            request.DialogueResponseKey is "nextPage" or "previousPage" &&
            request.ExpectedMenuTypeAfter == "DialogueBox";
        if (!serviceResponse && !locationResponse && !pageResponse)
            return AnimalPurchaseBlocked(request, "choose_animal_purchase_response", "animal_purchase_response_not_whitelisted");

        var response = dialogue.responses?.FirstOrDefault(value =>
            string.Equals(value.responseKey, request.DialogueResponseKey, StringComparison.Ordinal));
        if (response is null)
            return AnimalPurchaseBlocked(request, "choose_animal_purchase_response", "animal_purchase_response_not_available");

        var beforeMenu = Game1.activeClickableMenu.GetType().Name;
        var started = DateTimeOffset.UtcNow.ToString("O");
        var handled = Game1.currentLocation.answerDialogue(response);
        var purchaseMenu = Game1.activeClickableMenu as PurchaseAnimalsMenu;
        var pagedDialogue = Game1.activeClickableMenu is DialogueBox &&
            Game1.currentLocation.lastQuestionKey == "pagedResponse";
        var verified = handled && (locationResponse
            ? purchaseMenu?.TargetLocation?.NameOrUniqueName == request.AnimalPurchaseTargetLocationId
            : pageResponse
                ? pagedDialogue
                : (purchaseMenu is not null && purchaseMenu.TargetLocation?.NameOrUniqueName == request.AnimalPurchaseTargetLocationId) || pagedDialogue);
        var afterMenu = Game1.activeClickableMenu?.GetType().Name ?? "none";
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "choose_animal_purchase_response",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_GameLocation_answerDialogue_handled", "expected_animal_purchase_menu_stage_observed" }
                : new[] { "animal_purchase_dialogue_transition_mismatch" },
            RequestedEffect = "dialogue=" + request.ExpectedDialogueKey + ":" + request.DialogueResponseKey,
            ObservedEffect = "menu=" + afterMenu + ";question_key=" + (Game1.currentLocation.lastQuestionKey ?? "none"),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "animal_purchase_dialogue_transition_mismatch" },
            ChangedFacts = new[] { new SimulatedFactChange { Path = "menus.active_menu.type", Before = beforeMenu, After = afterMenu } }
        };
    }

    private void StartAnimalPurchase(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(AnimalPurchaseBlocked(request, "purchase_animal", reasons.ToArray()));
            return;
        }
        if (activeAnimalPurchase is not null || HasActiveExecutorOperation() ||
            Game1.activeClickableMenu is not PurchaseAnimalsMenu menu || menu.onFarm || menu.namingAnimal)
        {
            pending.Completion.SetResult(AnimalPurchaseBlocked(request, "purchase_animal", "animal_purchase_menu_not_ready"));
            return;
        }
        if (!TryReadAnimalPurchaseRequest(request, out var possibleTypes, out var requestReason))
        {
            pending.Completion.SetResult(AnimalPurchaseBlocked(request, "purchase_animal", requestReason));
            return;
        }

        var location = Game1.getLocationFromName(request.AnimalPurchaseTargetLocationId);
        var home = location?.buildings.FirstOrDefault(building =>
            building.buildingType.Value == request.AnimalHomeBuildingType &&
            building.tileX.Value == request.AnimalHomeBuildingTileX &&
            building.tileY.Value == request.AnimalHomeBuildingTileY);
        if (location is null || home?.GetIndoors() is not AnimalHouse house ||
            menu.TargetLocation?.NameOrUniqueName != location.NameOrUniqueName || home.isUnderConstruction() || house.isFull() ||
            house.animalsThatLiveHere.Count != request.ExpectedAnimalHomeOccupantCountBefore ||
            house.animalLimit.Value != request.ExpectedAnimalHomeCapacity ||
            Game1.player.Money != request.ExpectedMoneyBefore ||
            Utility.areThereAnyOtherAnimalsWithThisName(request.GeneratedAnimalName))
        {
            pending.Completion.SetResult(AnimalPurchaseBlocked(request, "purchase_animal", "animal_purchase_live_preconditions_drifted"));
            return;
        }

        var stockButton = menu.animalsToPurchase.FirstOrDefault(button =>
            button.hoverText == request.AnimalTypeId && button.item is StardewValley.Object item &&
            item.Type is null && item.salePrice() == request.Price);
        if (stockButton is null)
        {
            pending.Completion.SetResult(AnimalPurchaseBlocked(request, "purchase_animal", "animal_purchase_exact_stock_missing"));
            return;
        }
        activeAnimalPurchase = new ActiveAnimalPurchase(pending, menu, location, home, house,
            house.animalsThatLiveHere.ToHashSet(), possibleTypes);
    }

    private void TickAnimalPurchase()
    {
        var active = activeAnimalPurchase;
        if (active is null) return;
        try
        {
            active.ElapsedTicks++;
            if (active.ElapsedTicks > 1800)
            {
                CompleteAnimalPurchaseBlocked(active, "animal_purchase_timeout");
                return;
            }
            if (active.Cooldown > 0)
            {
                active.Cooldown--;
                return;
            }

            var request = active.Pending.Request;
            if (!active.StockClicked)
            {
                var button = active.Menu.animalsToPurchase.FirstOrDefault(value =>
                    value.hoverText == request.AnimalTypeId && value.item?.salePrice() == request.Price);
                if (button is null)
                {
                    CompleteAnimalPurchaseBlocked(active, "animal_purchase_stock_button_unavailable");
                    return;
                }
                if (!button.visible && active.Menu.currentScroll < active.Menu.scrollRows)
                {
                    active.Menu.receiveLeftClick(active.Menu.downArrow.bounds.Center.X, active.Menu.downArrow.bounds.Center.Y);
                    active.Cooldown = 3;
                    return;
                }
                if (!button.visible)
                {
                    CompleteAnimalPurchaseBlocked(active, "animal_purchase_stock_button_not_reachable_by_native_scroll");
                    return;
                }
                active.Menu.receiveLeftClick(button.bounds.Center.X, button.bounds.Center.Y);
                active.StockClicked = true;
                active.Cooldown = 8;
                return;
            }

            if (!active.HomeClicked)
            {
                if (Game1.IsFading() || !active.Menu.onFarm || active.Menu.freeze ||
                    Game1.currentLocation?.NameOrUniqueName != active.TargetLocation.NameOrUniqueName ||
                    active.Menu.animalBeingPurchased is null)
                    return;
                if (!active.PossibleTypes.Contains(active.Menu.animalBeingPurchased.type.Value, StringComparer.Ordinal) ||
                    active.Menu.priceOfAnimal != request.Price || active.House.isFull())
                {
                    CompleteAnimalPurchaseBlocked(active, "animal_purchase_selected_animal_or_home_drifted");
                    return;
                }

                var worldX = active.Home.tileX.Value * Game1.tileSize + Game1.tileSize / 2;
                var worldY = active.Home.tileY.Value * Game1.tileSize + Game1.tileSize / 2;
                Game1.viewport.X = Math.Max(0, worldX - Game1.viewport.Width / 2);
                Game1.viewport.Y = Math.Max(0, worldY - Game1.viewport.Height / 2);
                var screenX = (int)Utility.ModifyCoordinateForUIScale(worldX - Game1.viewport.X);
                var screenY = (int)Utility.ModifyCoordinateForUIScale(worldY - Game1.viewport.Y);
                var projectedTile = new Vector2(
                    (int)((Utility.ModifyCoordinateFromUIScale(screenX) + Game1.viewport.X) / Game1.tileSize),
                    (int)((Utility.ModifyCoordinateFromUIScale(screenY) + Game1.viewport.Y) / Game1.tileSize));
                if (!ReferenceEquals(active.TargetLocation.getBuildingAt(projectedTile), active.Home))
                {
                    CompleteAnimalPurchaseBlocked(active, "animal_purchase_home_click_geometry_mismatch");
                    return;
                }
                if (!active.Menu.animalBeingPurchased.CanLiveIn(active.Home))
                {
                    CompleteAnimalPurchaseBlocked(active, "animal_purchase_actual_type_cannot_live_in_projected_home");
                    return;
                }
                active.Menu.receiveLeftClick(screenX, screenY);
                if (!active.Menu.namingAnimal || !ReferenceEquals(active.Menu.newAnimalHome, active.Home))
                {
                    CompleteAnimalPurchaseBlocked(active, "animal_purchase_native_home_click_rejected");
                    return;
                }
                active.HomeClicked = true;
                active.Cooldown = 6;
                return;
            }

            if (!active.NameSubmitted)
            {
                if (!active.Menu.namingAnimal || !ReferenceEquals(active.Menu.newAnimalHome, active.Home))
                {
                    if (active.ElapsedTicks > 600)
                        CompleteAnimalPurchaseBlocked(active, "animal_purchase_home_selection_not_accepted");
                    return;
                }
                if (Utility.areThereAnyOtherAnimalsWithThisName(request.GeneratedAnimalName))
                {
                    CompleteAnimalPurchaseBlocked(active, "animal_purchase_name_no_longer_unique");
                    return;
                }
                active.Menu.textBox.Text = request.GeneratedAnimalName;
                active.Menu.receiveLeftClick(active.Menu.doneNamingButton.bounds.Center.X,
                    active.Menu.doneNamingButton.bounds.Center.Y);
                active.NameSubmitted = true;
                active.Cooldown = 12;
                return;
            }

            var newAnimal = active.House.animals.Values.FirstOrDefault(animal =>
                !active.BeforeAnimalIds.Contains(animal.myID.Value));
            if (newAnimal is null || Game1.IsFading() || Game1.currentLocation?.NameOrUniqueName != "AnimalShop")
                return;
            var verified = active.House.animalsThatLiveHere.Count == active.OccupantsBefore + 1 &&
                active.House.animalsThatLiveHere.Contains(newAnimal.myID.Value) &&
                ReferenceEquals(newAnimal.home, active.Home) &&
                newAnimal.ownerID.Value == Game1.player.UniqueMultiplayerID &&
                newAnimal.Name == request.GeneratedAnimalName &&
                active.PossibleTypes.Contains(newAnimal.type.Value, StringComparer.Ordinal) &&
                Game1.player.Money == request.ExpectedMoneyAfter;
            if (!verified)
            {
                CompleteAnimalPurchaseBlocked(active, "animal_purchase_native_postconditions_mismatch");
                return;
            }

            activeAnimalPurchase = null;
            active.Pending.Completion.SetResult(new TrainingExecutionResult
            {
                RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
                BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId, Status = "applied",
                FeedbackAvailable = true, StartedAt = active.StartedAt,
                CompletedAt = DateTimeOffset.UtcNow.ToString("O"), PrimitiveKind = "purchase_animal",
                PrimitiveVerificationStatus = "verified",
                PrimitiveVerificationReasons = new[]
                {
                    "native_PurchaseAnimalsMenu_stock_home_and_name_controls_used",
                    "exact_new_animal_type_owner_home_name_occupancy_and_money_receipt_verified"
                },
                RequestedEffect = AnimalPurchaseRequestedEffect(request),
                ObservedEffect = "animal_id=" + newAnimal.myID.Value + ";type=" + newAnimal.type.Value +
                    ";name=" + newAnimal.Name + ";home=" + active.Home.buildingType.Value + "@" +
                    active.Home.tileX.Value + "," + active.Home.tileY.Value + ";money=" + Game1.player.Money,
                ActualTicks = active.ElapsedTicks, TargetLocation = active.TargetLocation.NameOrUniqueName,
                TargetTileX = active.Home.tileX.Value, TargetTileY = active.Home.tileY.Value,
                ChangedFacts = new[]
                {
                    new SimulatedFactChange { Path = "player.money", Before = active.MoneyBefore.ToString(), After = Game1.player.Money.ToString() },
                    new SimulatedFactChange { Path = "farm.animals.count", Before = active.OccupantsBefore.ToString(), After = active.House.animalsThatLiveHere.Count.ToString() }
                }
            });
        }
        catch (Exception ex)
        {
            CompleteAnimalPurchaseBlocked(active, "animal_purchase_exception:" + ex.GetType().Name + ":" + ex.Message);
        }
    }

    private static bool TryReadAnimalPurchaseRequest(TrainingExecutionRequest request, out string[] possibleTypes, out string reason)
    {
        possibleTypes = Array.Empty<string>();
        reason = "animal_purchase_typed_request_invalid";
        if (string.IsNullOrWhiteSpace(request.AnimalTypeId) ||
            string.IsNullOrWhiteSpace(request.AnimalPurchaseTargetLocationId) ||
            string.IsNullOrWhiteSpace(request.AnimalHomeBuildingType) ||
            !request.AnimalHomeBuildingTileX.HasValue || !request.AnimalHomeBuildingTileY.HasValue ||
            string.IsNullOrWhiteSpace(request.GeneratedAnimalName) || !request.Price.HasValue || request.Price < 0 ||
            request.ExpectedMoneyAfter != request.ExpectedMoneyBefore - request.Price ||
            !request.ExpectedAnimalHomeOccupantCountBefore.HasValue || !request.ExpectedAnimalHomeCapacity.HasValue ||
            request.ExpectedAnimalHomeOccupantCountBefore >= request.ExpectedAnimalHomeCapacity ||
            string.IsNullOrWhiteSpace(request.AnimalPurchaseCandidateIdentitySha256))
            return false;
        try
        {
            possibleTypes = JsonSerializer.Deserialize<string[]>(request.PossibleActualTypeIdsJson) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            return false;
        }
        if (possibleTypes.Length == 0 || possibleTypes.Any(string.IsNullOrWhiteSpace)) return false;
        reason = string.Empty;
        return true;
    }

    private void CompleteAnimalPurchaseBlocked(ActiveAnimalPurchase active, string reason)
    {
        activeAnimalPurchase = null;
        active.Pending.Completion.SetResult(AnimalPurchaseBlocked(active.Pending.Request, "purchase_animal", reason));
    }

    private static TrainingExecutionResult AnimalPurchaseBlocked(TrainingExecutionRequest request,
        string primitive, params string[] reasons) =>
        BlockedWithPrimitive(request, primitive, AnimalPurchaseRequestedEffect(request),
            "location=" + (Game1.currentLocation?.NameOrUniqueName ?? "none") +
            ";menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none") + ";money=" + Game1.player.Money,
            reasons);

    private static string AnimalPurchaseRequestedEffect(TrainingExecutionRequest request) =>
        "animal_type=" + request.AnimalTypeId + ";target_location=" + request.AnimalPurchaseTargetLocationId +
        ";home=" + request.AnimalHomeBuildingType + "@" + request.AnimalHomeBuildingTileX + "," +
        request.AnimalHomeBuildingTileY + ";name=" + request.GeneratedAnimalName +
        ";money_after=" + request.ExpectedMoneyAfter;
}
