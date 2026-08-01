using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupMaterialTransferTarget(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var farm = Game1.getFarm();
        var requested = request.TargetTileX is >= 0 && request.TargetTileY is >= 0
            ? new Point(request.TargetTileX.Value, request.TargetTileY.Value)
            : (Point?)null;
        var target = requested ?? FindMaterialTransferFixtureTarget(farm);
        if (!target.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_material_transfer_target",
                "ordinary_owned_chest=ready",
                "target=unavailable",
                "material_transfer_fixture_target_unavailable");
        }

        var tile = target.Value.ToVector2();
        farm.objects.Remove(tile);
        var chest = CreateFixtureChest(tile, "130", "(O)388", 11);
        farm.objects[tile] = chest;
        var stand = FindMaterialTransferFixtureStand(farm, target.Value);
        var moved = stand.HasValue;
        var moveReason = moved
            ? string.Empty
            : "fixture_no_collision_safe_adjacent_tile";
        if (stand.HasValue)
        {
            Game1.currentLocation = farm;
            Game1.player.currentLocation = farm;
            Game1.player.Position = stand.Value.ToVector2() * Game1.tileSize;
            Game1.player.faceDirection(DirectionTo(stand.Value, target.Value));
        }
        var verified = moved &&
            farm.objects.TryGetValue(tile, out var value) &&
            ReferenceEquals(value, chest) &&
            chest.GetType() == typeof(Chest) &&
            chest.playerChest.Value &&
            chest.SpecialChestType == Chest.SpecialChestTypes.None &&
            chest.owner.Value == Game1.player.UniqueMultiplayerID &&
            chest.Items.Count == 1 &&
            chest.Items[0].QualifiedItemId == "(O)388" &&
            chest.Items[0].Quality == 4 &&
            chest.Items[0].Stack == 11;
        var observed = "target=" + TileText(tile) +
            ";stand=" + (stand.HasValue ? TileText(stand.Value.ToVector2()) : "unavailable") +
            ";move_reason=" + moveReason;
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
            PrimitiveKind = "debug_setup_material_transfer_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_ordinary_owned_chest_ready" }
                : new[] { "isolated_runtime_material_transfer_fixture_incomplete", moveReason },
            RequestedEffect = "ordinary_owned_chest=ready",
            ObservedEffect = observed,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "material_transfer_fixture_not_verified", moveReason },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.material_inventory_graph.fixture_transfer_target",
                        Before = "unknown",
                        After = observed
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static Point? FindMaterialTransferFixtureTarget(GameLocation farm)
    {
        var width = farm.Map.Layers[0].LayerWidth;
        var height = farm.Map.Layers[0].LayerHeight;
        var player = Game1.player.TilePoint;
        var probe = new Chest(playerChest: true, Vector2.Zero, "130")
        {
            Location = farm
        };
        return Enumerable.Range(2, Math.Max(0, width - 16))
            .SelectMany(x => Enumerable.Range(2, Math.Max(0, height - 4))
                .Select(y => new Point(x, y)))
            .Where(target => IsTileWalkable(farm, target))
            .Where(target => probe.canBePlacedHere(
                farm,
                target.ToVector2(),
                ~(CollisionMask.Characters | CollisionMask.Farmers)))
            .Where(target => FindMaterialTransferFixtureStand(farm, target).HasValue)
            .OrderBy(target => ManhattanDistance(player, target))
            .ThenBy(target => target.Y)
            .ThenBy(target => target.X)
            .Cast<Point?>()
            .FirstOrDefault();
    }

    private static Point? FindMaterialTransferFixtureStand(
        GameLocation farm,
        Point target)
    {
        return new[]
            {
                new Point(target.X, target.Y + 1),
                new Point(target.X, target.Y - 1),
                new Point(target.X + 1, target.Y),
                new Point(target.X - 1, target.Y)
            }
            .Where(stand =>
                IsTileOnMap(farm, stand) &&
                IsTileWalkable(farm, stand))
            .Cast<Point?>()
            .FirstOrDefault();
    }

    private TrainingExecutionResult ExecuteSetupMaterialInventoryGraph(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_material_inventory_graph",
                "farm.material_inventory_graph.fixture_matrix=ready",
                "target_tile=missing",
                "target_tile_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        var player = Game1.player;
        var baseTile = new Vector2(request.TargetTileX.Value, request.TargetTileY.Value);
        Game1.currentLocation = farm;
        player.currentLocation = farm;

        var normalTile = baseTile;
        var workbenchTile = baseTile + new Vector2(1f, 0f);
        var bigTile = baseTile + new Vector2(2f, 0f);
        var junimoATile = baseTile + new Vector2(4f, 0f);
        var junimoBTile = baseTile + new Vector2(6f, 0f);
        var autoGrabberTile = baseTile + new Vector2(8f, 0f);
        var readyMachineTile = baseTile + new Vector2(10f, 0f);
        var processingMachineTile = baseTile + new Vector2(12f, 0f);

        foreach (var tile in new[]
        {
            normalTile,
            workbenchTile,
            bigTile,
            junimoATile,
            junimoBTile,
            autoGrabberTile,
            readyMachineTile,
            processingMachineTile
        })
        {
            farm.objects.Remove(tile);
        }

        var normalChest = CreateFixtureChest(normalTile, "130", "(O)388", 11);
        var bigChest = CreateFixtureChest(bigTile, "BigChest", "(O)390", 13);
        farm.objects[normalTile] = normalChest;
        farm.objects[workbenchTile] = new Workbench(workbenchTile);
        farm.objects[bigTile] = bigChest;

        var junimoInventory = player.team.GetOrCreateGlobalInventory(FarmerTeam.GlobalInventoryId_JunimoChest);
        junimoInventory.Clear();
        junimoInventory.Add(CreateFixtureItem("(O)382", 17));
        farm.objects[junimoATile] = CreateOwnedChest(junimoATile, "256");
        farm.objects[junimoBTile] = CreateOwnedChest(junimoBTile, "256");

        var autoGrabberChest = new Chest(playerChest: true);
        autoGrabberChest.Items.Add(CreateFixtureItem("(O)384", 3));
        var autoGrabber = new StardewValley.Object(autoGrabberTile, "165");
        autoGrabber.owner.Value = player.UniqueMultiplayerID;
        autoGrabber.heldObject.Value = autoGrabberChest;
        farm.objects[autoGrabberTile] = autoGrabber;

        farm.objects[readyMachineTile] = CreateFixtureMachine(readyMachineTile, ready: true);
        farm.objects[processingMachineTile] = CreateFixtureMachine(processingMachineTile, ready: false);

        var home = Utility.getHomeOfFarmer(player);
        var builtInFridgeReady = SetupBuiltInFridge(home);
        var miniFridgeReady = SetupMiniFridge(home, out var miniFridgeTile);
        MoveFixtureFarmerToFarmAdjacent(baseTile.ToPoint());

        var junimoA = farm.objects.TryGetValue(junimoATile, out var firstJunimo) &&
            firstJunimo is Chest firstJunimoChest &&
            firstJunimoChest.SpecialChestType == Chest.SpecialChestTypes.JunimoChest;
        var junimoB = farm.objects.TryGetValue(junimoBTile, out var secondJunimo) &&
            secondJunimo is Chest secondJunimoChest &&
            secondJunimoChest.SpecialChestType == Chest.SpecialChestTypes.JunimoChest;
        var verified = normalChest.Items.Count == 1 &&
            bigChest.SpecialChestType == Chest.SpecialChestTypes.BigChest &&
            junimoA &&
            junimoB &&
            junimoInventory.Count == 1 &&
            builtInFridgeReady &&
            miniFridgeReady;

        var observed = string.Join(";", new[]
        {
            "normal=" + TileText(normalTile),
            "workbench=" + TileText(workbenchTile),
            "big=" + TileText(bigTile),
            "junimo_a=" + TileText(junimoATile),
            "junimo_b=" + TileText(junimoBTile),
            "auto_grabber=" + TileText(autoGrabberTile),
            "ready_machine=" + TileText(readyMachineTile),
            "processing_machine=" + TileText(processingMachineTile),
            "home=" + home.NameOrUniqueName,
            "mini_fridge=" + TileText(miniFridgeTile)
        });

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
            PrimitiveKind = "debug_setup_material_inventory_graph",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_material_inventory_matrix_ready" }
                : new[] { "isolated_runtime_material_inventory_matrix_incomplete" },
            RequestedEffect = "farm.material_inventory_graph.fixture_matrix=ready",
            ObservedEffect = observed,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "material_inventory_graph_fixture_not_verified" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "farm.material_inventory_graph.fixture_matrix",
                        Before = "unknown",
                        After = observed
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static Chest CreateFixtureChest(Vector2 tile, string itemId, string contentId, int stack)
    {
        var chest = CreateOwnedChest(tile, itemId);
        chest.Items.Clear();
        chest.Items.Add(CreateFixtureItem(contentId, stack));
        return chest;
    }

    private static Chest CreateOwnedChest(Vector2 tile, string itemId)
    {
        var chest = new Chest(playerChest: true, tile, itemId);
        chest.owner.Value = Game1.player.UniqueMultiplayerID;
        return chest;
    }

    private static StardewValley.Object CreateFixtureItem(string qualifiedItemId, int stack) =>
        ItemRegistry.Create<StardewValley.Object>(qualifiedItemId, stack, quality: 4);

    private static StardewValley.Object CreateFixtureMachine(Vector2 tile, bool ready)
    {
        var machine = new StardewValley.Object(tile, "12");
        machine.owner.Value = Game1.player.UniqueMultiplayerID;
        machine.heldObject.Value = CreateFixtureItem("(O)386", 1);
        machine.readyForHarvest.Value = ready;
        machine.MinutesUntilReady = ready ? 0 : 100;
        return machine;
    }

    private static bool SetupBuiltInFridge(FarmHouse home)
    {
        if (!home.GetFridgePosition().HasValue)
        {
            home.setMapForUpgradeLevel(Math.Max(1, home.upgradeLevel));
        }

        var fridge = home.GetFridge(onlyUnlocked: true);
        if (fridge is null || !home.GetFridgePosition().HasValue)
        {
            return false;
        }

        fridge.owner.Value = Game1.player.UniqueMultiplayerID;
        fridge.Items.Clear();
        fridge.Items.Add(CreateFixtureItem("(O)378", 5));
        return true;
    }

    private static bool SetupMiniFridge(FarmHouse home, out Vector2 tile)
    {
        var fridgePosition = home.GetFridgePosition() ?? new Point(4, 4);
        tile = new Vector2(fridgePosition.X + 2, fridgePosition.Y);
        home.objects.Remove(tile);
        var miniFridge = new Chest("216", tile, 217, 2);
        miniFridge.owner.Value = Game1.player.UniqueMultiplayerID;
        miniFridge.fridge.Value = true;
        miniFridge.Items.Add(CreateFixtureItem("(O)380", 7));
        home.objects[tile] = miniFridge;
        return miniFridge.playerChest.Value && miniFridge.fridge.Value;
    }

    private static string TileText(Vector2 tile) => (int)tile.X + "," + (int)tile.Y;
}
