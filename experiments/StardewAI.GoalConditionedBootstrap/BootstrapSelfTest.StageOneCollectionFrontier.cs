using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyCurrentStageOneCollectionTeacherFrontier(
        string outputRoot)
    {
        var root = Path.Combine(
            outputRoot,
            "current-stage-one-collection-teacher-frontier-fixture");
        Directory.CreateDirectory(root);
        var inventoryPath = Path.Combine(root, "requirements.json");
        var loweringPath = Path.Combine(root, "lowering.json");
        var catalogPath = Path.Combine(root, "master-angler-catalog.json");
        var windowsPath = Path.Combine(root, "master-angler-windows.json");
        var locationsPath = Path.Combine(root, "data-locations.json");
        var cropsPath = Path.Combine(root, "data-crops.json");
        var shopsPath = Path.Combine(root, "data-shops.json");
        var accessConstraintPath = Path.Combine(root, "access-constraint-index.json");
        var cropGrowthSourcePath = Path.Combine(root, "Crop.cs");
        var cropPlantingSourcePath = Path.Combine(root, "HoeDirt.cs");
        var shopStockSourcePath = Path.Combine(root, "ShopBuilder.cs");
        var shopOpenSourcePath = Path.Combine(root, "Utility.cs");
        var shopPurchaseSourcePath = Path.Combine(root, "ShopMenu.cs");
        var gameStateQuerySourcePath = Path.Combine(root, "GameStateQuery.cs");
        var routeCalendarPath = Path.Combine(root, "route-calendar-resolution.json");
        var calibrationPath = Path.Combine(root, "route-timing.json");
        var snapshotPath = Path.Combine(root, "snapshot.json");
        var intentsPath = Path.Combine(root, "master-angler-intents.json");
        var rankingPath = Path.Combine(root, "ranking.json");
        var preferencePath = Path.Combine(root, "teacher-preference.json");
        var afterSnapshotPath = Path.Combine(root, "after-snapshot.json");
        var receiptPath = Path.Combine(root, "execution-receipt.json");
        const string stateHash = "stage-one-collection-frontier-state";

        var fish = Enumerable.Range(0, 72)
            .Select(index => new StageOneFishFixture(
                index == 0 ? "145" : "test_" + index,
                index == 0 ? "(O)145" : "(O)test_" + index,
                index == 0 ? "Sunfish" : "Test Fish " + index))
            .ToArray();
        var masterAlternatives = fish
            .Select((value, index) => CollectionAlternative(
                value.ItemId,
                value.QualifiedItemId,
                value.DisplayName,
                "item_id",
                1,
                0,
                "native_location_fish_spawn",
                value.ItemId == "145" ? "Beach:0" : "fixture:" + index))
            .ToArray();
        var masterGroups = fish.Zip(masterAlternatives)
            .Select(value => CollectionRequirementGroup(
                "master_angler:item:" + value.First.ItemId,
                "all_required",
                1,
                value.Second))
            .ToArray();
        Write(locationsPath, new
        {
            payload = new Dictionary<string, object>
            {
                ["Town"] = new Dictionary<string, object>
                {
                    ["ArtifactSpots"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["Id"] = "fixture-artifact-row",
                            ["ItemId"] = "(O)96",
                            ["Condition"] =
                                "YEAR 2, TIME 0600 1800, WEATHER Here Rain Storm GreenRain, PLAYER_HAS_MAIL Current fixtureGate",
                            ["PerItemCondition"] = "RANDOM 0.4"
                        }
                    }
                }
            }
        });
        Write(cropsPath, new
        {
            payload = new Dictionary<string, object>
            {
                ["472"] = new Dictionary<string, object?>
                {
                    ["Seasons"] = new[] { 0 },
                    ["DaysInPhase"] = new[] { 1, 1, 1, 1 },
                    ["RegrowDays"] = -1,
                    ["IsPaddyCrop"] = false,
                    ["NeedsWatering"] = true,
                    ["PlantableLocationRules"] = null,
                    ["HarvestItemId"] = "24",
                    ["Texture"] = @"TileSheets\crops",
                    ["SpriteIndex"] = 0
                }
            }
        });
        const string fixtureShopCondition =
            "!YEAR 2, DAY_OF_WEEK Monday, TIME 700 1800, " +
            "SYNCED_RANDOM day fixture 0.25";
        Write(shopsPath, new
        {
            payload = new Dictionary<string, object>
            {
                ["FixtureShop"] = new Dictionary<string, object?>
                {
                    ["Currency"] = 0,
                    ["ApplyProfitMargins"] = null,
                    ["PriceModifiers"] = Array.Empty<object>(),
                    ["Owners"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["Id"] = "FixtureOwner",
                            ["Name"] = "FixtureOwner",
                            ["Type"] = 0,
                            ["Condition"] = null
                        }
                    },
                    ["Items"] = new object[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["Id"] = "fixture-parsnip",
                            ["ItemId"] = "(O)24",
                            ["RandomItemId"] = null,
                            ["Price"] = 0,
                            ["AvailableStock"] = 1,
                            ["AvailableStockLimit"] = 1,
                            ["TradeItemId"] = "(O)388",
                            ["TradeItemAmount"] = 5,
                            ["IsRecipe"] = false,
                            ["Condition"] = fixtureShopCondition,
                            ["PerItemCondition"] = null,
                            ["AvoidRepeat"] = false,
                            ["UseObjectDataPrice"] = false,
                            ["ApplyProfitMargins"] = null,
                            ["IgnoreShopPriceModifiers"] = true,
                            ["PriceModifiers"] = null,
                            ["AvailableStockModifiers"] = null,
                            ["MaxItems"] = null,
                            ["ActionsOnPurchase"] = new[] { "AddMail fixtureBought" }
                        }
                    }
                }
            }
        });
        Write(accessConstraintPath, new
        {
            schema_version = "stardewai.access_constraint_index.v1",
            authority = "fixture exact native access index",
            semantic_limit = "fixture target-date state remains pending",
            summary = new { blockingIssueCount = 0 },
            shops = new[]
            {
                new
                {
                    shopId = "FixtureShop",
                    owners = new[]
                    {
                        new
                        {
                            id = "FixtureOwner",
                            name = "FixtureOwner",
                            type = 0,
                            condition = (string?)null
                        }
                    },
                    stock = new[]
                    {
                        new
                        {
                            id = "fixture-parsnip",
                            itemId = "(O)24",
                            condition = fixtureShopCondition,
                            perItemCondition = (string?)null,
                            parsedCondition = new
                            {
                                clauses = new[]
                                {
                                    NativeConditionClause("YEAR"),
                                    NativeConditionClause("DAY_OF_WEEK"),
                                    NativeConditionClause("TIME"),
                                    NativeConditionClause("SYNCED_RANDOM")
                                }
                            },
                            parsedPerItemCondition = (object?)null
                        }
                    }
                }
            },
            shop_endpoints = new[]
            {
                new
                {
                    mapAsset = "Maps/FixtureShop",
                    layer = "Buildings",
                    x = 4,
                    y = 18,
                    shopId = "FixtureShop",
                    handlerKey = "Shop",
                    rawAction = "Shop FixtureShop",
                    resolution = "exact_fixture_mapping"
                }
            },
            door_windows = new[]
            {
                new
                {
                    mapAsset = "Maps/Town",
                    x = 10,
                    y = 12,
                    destinationLocation = "FixtureShop",
                    destinationX = 4,
                    destinationY = 19,
                    openTime = 900,
                    closeTime = 1700,
                    requiredNpc = (string?)null,
                    minimumFriendship = 0,
                    rawAction = "LockedDoorWarp 4 19 FixtureShop 900 1700"
                }
            }
        });
        File.WriteAllText(cropGrowthSourcePath, "fixture Crop source");
        File.WriteAllText(cropPlantingSourcePath, "fixture HoeDirt source");
        File.WriteAllText(shopStockSourcePath, "fixture ShopBuilder source");
        File.WriteAllText(shopOpenSourcePath, "fixture Utility source");
        File.WriteAllText(shopPurchaseSourcePath, "fixture ShopMenu source");
        File.WriteAllText(gameStateQuerySourcePath, "fixture GameStateQuery source");
        Write(inventoryPath, new
        {
            schema_version = "authoritative_goal_requirement_inventory.v1",
            status = "complete",
            goal_id = "goal.grandpa_21",
            game_version = "1.6.15",
            denominator_complete = true,
            acquisition_routes_complete = true,
            source_evidence = new[]
            {
                new
                {
                    source_id = "runtime_data_locations",
                    path = Path.GetFullPath(locationsPath),
                    sha256 = HashFile(locationsPath),
                    authority = "runtime DataLoader.Locations export"
                },
                new
                {
                    source_id = "runtime_data_crops",
                    path = Path.GetFullPath(cropsPath),
                    sha256 = HashFile(cropsPath),
                    authority = "runtime DataLoader.Crops export"
                },
                new
                {
                    source_id = "runtime_data_shops",
                    path = Path.GetFullPath(shopsPath),
                    sha256 = HashFile(shopsPath),
                    authority = "runtime DataLoader.Shops export"
                },
                new
                {
                    source_id = "access_constraint_index",
                    path = Path.GetFullPath(accessConstraintPath),
                    sha256 = HashFile(accessConstraintPath),
                    authority = "compiled shop access fixture"
                },
                new
                {
                    source_id = "native_crop_growth_rule",
                    path = Path.GetFullPath(cropGrowthSourcePath),
                    sha256 = HashFile(cropGrowthSourcePath),
                    authority = "decompiled Crop season and growth branches"
                },
                new
                {
                    source_id = "native_crop_planting_rule",
                    path = Path.GetFullPath(cropPlantingSourcePath),
                    sha256 = HashFile(cropPlantingSourcePath),
                    authority = "decompiled HoeDirt planting and speed branches"
                },
                new
                {
                    source_id = "native_shop_stock_rule",
                    path = Path.GetFullPath(shopStockSourcePath),
                    sha256 = HashFile(shopStockSourcePath),
                    authority = "decompiled ShopBuilder fixture"
                },
                new
                {
                    source_id = "native_shop_open_rule",
                    path = Path.GetFullPath(shopOpenSourcePath),
                    sha256 = HashFile(shopOpenSourcePath),
                    authority = "decompiled shop opening fixture"
                },
                new
                {
                    source_id = "native_shop_purchase_rule",
                    path = Path.GetFullPath(shopPurchaseSourcePath),
                    sha256 = HashFile(shopPurchaseSourcePath),
                    authority = "decompiled shop purchase fixture"
                },
                new
                {
                    source_id = "native_game_state_query_rule",
                    path = Path.GetFullPath(gameStateQuerySourcePath),
                    sha256 = HashFile(gameStateQuerySourcePath),
                    authority = "decompiled calendar query fixture"
                }
            },
            requirement_sets = new object[]
            {
                RequirementSet(
                    "full_shipment",
                    CollectionRequirementGroup(
                        "full_shipment:item:24", "all_required", 1,
                        CollectionAlternative(
                            "24", "(O)24", "Parsnip", "item_id", 1, 0,
                            "harvests_as", "crop:472"))),
                RequirementSet("master_angler", masterGroups),
                RequirementSet(
                    "museum_collection",
                    CollectionRequirementGroup(
                        "museum:item:96", "all_required", 1,
                        CollectionAlternative(
                            "96", "(O)96", "Dwarf Scroll I", "item_id", 1, 0,
                            "native_location_artifact_spot", "location:Town:0"))),
                RequirementSet(
                    "community_center_standard",
                    CollectionRequirementGroup(
                        "community_center:bundle:Pantry/5",
                        "choose_at_least_required_slots",
                        1,
                        CollectionAlternative(
                            "24", "(O)24", "Parsnip", "item_id", 1, 0,
                            "harvests_as", "crop:472", includeFixtureShopRoute: true)))
            }
        });

        var masterLoweredGroups = fish
            .Select((value, index) => CollectionLoweredGroup(
                    "master_angler:item:" + value.ItemId,
                    1,
                    CollectionLoweredAlternative(
                        value.ItemId,
                        value.QualifiedItemId,
                        value.DisplayName,
                        "item_id",
                        1,
                        0,
                        "native_location_fish_spawn",
                        value.ItemId == "145" ? "Beach:0" : "fixture:" + index,
                        "fishing.catch_fish")))
            .ToArray();
        Write(loweringPath, new
        {
            schema_version = "acquisition_route_option_lowering.v1",
            status = "complete",
            goal_id = "goal.grandpa_21",
            requirement_inventory_sha256 = HashFile(inventoryPath),
            dependency_axis_inventory_complete = true,
            required_downstream_dependency_axes =
                StageOneCollectionRouteDependencyAxes.Required,
            requirement_sets = new object[]
            {
                LoweringSet(
                    "full_shipment",
                    CollectionLoweredGroup(
                        "full_shipment:item:24", 1,
                        CollectionLoweredAlternative(
                            "24", "(O)24", "Parsnip", "item_id", 1, 0,
                            "harvests_as", "crop:472", "farm.maintain_crops"))),
                LoweringSet("master_angler", masterLoweredGroups),
                LoweringSet(
                    "museum_collection",
                    CollectionLoweredGroup(
                        "museum:item:96", 1,
                        CollectionLoweredAlternative(
                            "96", "(O)96", "Dwarf Scroll I", "item_id", 1, 0,
                            "native_location_artifact_spot", "location:Town:0",
                            "foraging.excavate_artifact_spots"))),
                LoweringSet(
                    "community_center_standard",
                    CollectionLoweredGroup(
                        "community_center:bundle:Pantry/5", 1,
                        CollectionLoweredAlternative(
                            "24", "(O)24", "Parsnip", "item_id", 1, 0,
                            "harvests_as", "crop:472", "farm.maintain_crops",
                            includeFixtureShopRoute: true)))
            }
        });

        Write(catalogPath, new
        {
            schema_version = "master_angler_opportunity_catalog.v1",
            status = "complete",
            goal_id = "goal.grandpa_21",
            game_version = "1.6.15",
            native_denominator_count = 72,
            source_inventory_complete = true,
            static_calendar_constraint_complete = true,
            requirement_inventory_path = Path.GetFullPath(inventoryPath),
            requirement_inventory_sha256 = HashFile(inventoryPath),
            species = fish.Select((value, index) => new
            {
                item_id = value.ItemId,
                qualified_item_id = value.QualifiedItemId,
                display_name = value.DisplayName,
                acquisition_class = "rod_location_rule",
                route_status = "source_complete",
                fish_data = new
                {
                    parse_status = "parsed",
                    minimum_fishing_level = 0
                },
                location_rules = new[]
                {
                    new
                    {
                        location_id = index == 0 ? "Beach" : "fixture",
                        rule_index = index,
                        rule_id = "fixture-rule-" + index,
                        minimum_fishing_level = 0,
                        ignore_fish_data_requirements = false,
                        calendar = new
                        {
                            parse_status = "complete",
                            minimum_year = 1,
                            maximum_year = (int?)null,
                            seasons = new[] { index == 0 ? "spring" : "winter" },
                            time_windows = new[]
                            {
                                new { start_time = 600, end_time = 2600 }
                            },
                            weather_modes = new[] { "sun" },
                            dynamic_conditions = Array.Empty<string>(),
                            unparsed_conditions = Array.Empty<string>(),
                            static_calendar_possible = true
                        }
                    }
                },
                mine_overrides = Array.Empty<object>()
            }).ToArray(),
            unresolved_species_ids = Array.Empty<string>(),
            unresolved_calendar_rule_ids = Array.Empty<string>()
        });
        Write(
            windowsPath,
            MasterAnglerStageOneWindowIndexBuilder.Build(catalogPath, 3));
        var routeCalendar = AcquisitionRouteCalendarResolutionBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath);
        Write(routeCalendarPath, routeCalendar);
        var resolvedSunfishRoute = routeCalendar.Routes.Single(route =>
            route.RequirementSetId == "master_angler" &&
            route.QualifiedItemId == "(O)145");
        var resolvedArtifactRoute = routeCalendar.Routes.Single(route =>
            route.RequirementSetId == "museum_collection" &&
            route.QualifiedItemId == "(O)96");
        var resolvedCropRoute = routeCalendar.Routes.Single(route =>
            route.RequirementSetId == "full_shipment" &&
            route.QualifiedItemId == "(O)24");
        var resolvedShopRoute = routeCalendar.Routes.Single(route =>
            route.RequirementId == "community_center:bundle:Pantry/5" &&
            route.RouteKind == "sells");
        Require(routeCalendar.Status == "complete_static_sources_target_date_pending" &&
                routeCalendar.RouteOccurrenceInventoryComplete &&
                routeCalendar.StaticCalendarSourceResolutionComplete &&
                !routeCalendar.TrainingLabelEligible &&
                routeCalendar.RouteOccurrenceCount == 76 &&
                routeCalendar.ResolvedStaticSourceCount == 76 &&
                routeCalendar.BlockedStaticSourceCount == 0 &&
                routeCalendar.CropDataSha256 == HashFile(cropsPath) &&
                routeCalendar.NativeCropGrowthSourceSha256 ==
                    HashFile(cropGrowthSourcePath) &&
                routeCalendar.NativeCropPlantingSourceSha256 ==
                    HashFile(cropPlantingSourcePath) &&
                routeCalendar.ShopDataSha256 == HashFile(shopsPath) &&
                routeCalendar.AccessConstraintIndexSha256 ==
                    HashFile(accessConstraintPath) &&
                routeCalendar.NativeShopStockSourceSha256 ==
                    HashFile(shopStockSourcePath) &&
                routeCalendar.NativeShopOpenSourceSha256 ==
                    HashFile(shopOpenSourcePath) &&
                routeCalendar.NativeShopPurchaseSourceSha256 ==
                    HashFile(shopPurchaseSourcePath) &&
                routeCalendar.NativeGameStateQuerySourceSha256 ==
                    HashFile(gameStateQuerySourcePath) &&
                resolvedSunfishRoute.Status ==
                    "resolved_static_source_window_target_date_pending" &&
                resolvedSunfishRoute.EvidenceClass ==
                    "master_angler_location_rule_window" &&
                resolvedSunfishRoute.CalendarWindows.Length == 2 &&
                resolvedSunfishRoute.CalendarWindows[0].SourceKey == "Beach:0" &&
                resolvedArtifactRoute.Status ==
                    "resolved_static_source_window_target_date_pending" &&
                resolvedArtifactRoute.EvidenceClass ==
                    "runtime_location_artifact_spot_window" &&
                resolvedArtifactRoute.CalendarWindows.Length == 4 &&
                resolvedArtifactRoute.CalendarWindows.All(window =>
                    window.Year == 2 &&
                    window.TimeWindows.Length == 1 &&
                    window.TimeWindows[0].StartTime == 600 &&
                    window.TimeWindows[0].EndTime == 1800 &&
                    window.WeatherModes.SequenceEqual(
                        new[] { "green_rain", "rain", "storm" }) &&
                    window.DynamicConditions.SequenceEqual(new[]
                    {
                        "PLAYER_HAS_MAIL Current fixtureGate",
                        "RANDOM 0.4"
                    })) &&
                resolvedCropRoute.Status ==
                    "resolved_static_source_window_target_date_pending" &&
                resolvedCropRoute.EvidenceClass ==
                    "runtime_crop_native_season_window" &&
                resolvedCropRoute.CropSource is not null &&
                resolvedCropRoute.CropSource.SeedItemId == "472" &&
                resolvedCropRoute.CropSource.DataHarvestQualifiedItemId == "(O)24" &&
                resolvedCropRoute.CropSource.PossibleHarvestQualifiedItemIds
                    .SequenceEqual(new[] { "(O)24" }) &&
                resolvedCropRoute.CropSource.NativeSeasons
                    .SequenceEqual(new[] { "spring" }) &&
                resolvedCropRoute.CropSource.DaysInPhase
                    .SequenceEqual(new[] { 1, 1, 1, 1 }) &&
                resolvedCropRoute.CropSource.BaseGrowthDays == 4 &&
                resolvedCropRoute.CropSource.RegrowDays == -1 &&
                resolvedCropRoute.CropSource.NeedsWatering &&
                !resolvedCropRoute.CropSource.IsPaddyCrop &&
                !resolvedCropRoute.CropSource.StochasticOutcome &&
                resolvedCropRoute.CalendarWindows.Count(window =>
                    window.SourceKind == "crop_native_season" &&
                    window.Season == "spring" &&
                    window.RequiredLocationCapability is null) == 2 &&
                resolvedCropRoute.CalendarWindows.Count(window =>
                    window.SourceKind == "crop_season_ignored_location" &&
                    window.RequiredLocationCapability == "seeds_ignore_seasons") == 6 &&
                resolvedShopRoute.Status ==
                    "resolved_static_source_window_target_date_pending" &&
                resolvedShopRoute.EvidenceClass ==
                    "runtime_shop_stock_calendar_projection" &&
                resolvedShopRoute.CalendarWindows.Length == 16 &&
                resolvedShopRoute.CalendarWindows.All(window =>
                    window.Year == 1 &&
                    window.FirstTotalDay == window.LastTotalDay &&
                    window.TimeWindows.Length == 1 &&
                    window.TimeWindows[0].StartTime == 700 &&
                    window.TimeWindows[0].EndTime == 1810 &&
                    window.RequiredLocationCapability == "shop_access" &&
                    window.StochasticOutcome &&
                    window.DynamicConditions.SequenceEqual(new[]
                    {
                        "SYNCED_RANDOM day fixture 0.25"
                    })) &&
                resolvedShopRoute.ShopSource is not null &&
                resolvedShopRoute.ShopSource.ShopId == "FixtureShop" &&
                resolvedShopRoute.ShopSource.StockRowIndex == 0 &&
                resolvedShopRoute.ShopSource.DataItemQualifiedId == "(O)24" &&
                resolvedShopRoute.ShopSource.AvailableStock == 1 &&
                resolvedShopRoute.ShopSource.AvailableStockLimit == 1 &&
                resolvedShopRoute.ShopSource.TradeItemId == "(O)388" &&
                resolvedShopRoute.ShopSource.TradeItemAmount == 5 &&
                resolvedShopRoute.ShopSource.ActionsOnPurchase.SequenceEqual(
                    new[] { "AddMail fixtureBought" }) &&
                resolvedShopRoute.ShopSource.NativeConditionHandlersComplete &&
                resolvedShopRoute.ShopSource.RequiresStockModifierResolution &&
                resolvedShopRoute.ShopSource.RequiresOwnerScheduleResolution &&
                resolvedShopRoute.ShopSource.InteractionEndpoints.Length == 1 &&
                resolvedShopRoute.ShopSource.DoorWindows.Length == 1 &&
                resolvedShopRoute.ShopSource.DoorWindows[0].OpenTime == 900 &&
                resolvedShopRoute.ShopSource.DoorWindows[0].CloseTime == 1700,
            "Authoritative route calendar source resolution drifted.");

        var originalCropEvidence = File.ReadAllText(cropsPath);
        var staleCropEvidenceRejected = false;
        try
        {
            File.AppendAllText(cropsPath, " ");
            _ = AcquisitionRouteCalendarResolutionBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath);
        }
        catch (InvalidDataException)
        {
            staleCropEvidenceRejected = true;
        }
        finally
        {
            File.WriteAllText(cropsPath, originalCropEvidence);
        }
        Require(staleCropEvidenceRejected,
            "Stale runtime Data/Crops evidence did not fail closed.");

        var originalShopEvidence = File.ReadAllText(shopsPath);
        var staleShopEvidenceRejected = false;
        try
        {
            File.AppendAllText(shopsPath, " ");
            _ = AcquisitionRouteCalendarResolutionBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath);
        }
        catch (InvalidDataException)
        {
            staleShopEvidenceRejected = true;
        }
        finally
        {
            File.WriteAllText(shopsPath, originalShopEvidence);
        }
        Require(staleShopEvidenceRejected,
            "Stale runtime Data/Shops evidence did not fail closed.");

        var originalAccessEvidence = File.ReadAllText(accessConstraintPath);
        var staleAccessEvidenceRejected = false;
        try
        {
            File.AppendAllText(accessConstraintPath, " ");
            _ = AcquisitionRouteCalendarResolutionBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath);
        }
        catch (InvalidDataException)
        {
            staleAccessEvidenceRejected = true;
        }
        finally
        {
            File.WriteAllText(accessConstraintPath, originalAccessEvidence);
        }
        Require(staleAccessEvidenceRejected,
            "Stale access constraint evidence did not fail closed.");

        var unknownCalendar = NativeCalendarConstraintNormalizer.Normalize(
            NativeCalendarConstraintNormalizer.AllSeasons,
            NativeCalendarConstraintNormalizer.AllDay,
            NativeCalendarConstraintNormalizer.AllWeatherModes,
            string.Empty,
            "UNKNOWN_NATIVE_PREDICATE Current value");
        Require(unknownCalendar.ParseStatus == "unparsed_dynamic_condition" &&
                unknownCalendar.StaticCalendarPossible &&
                unknownCalendar.UnparsedConditions.SequenceEqual(
                    new[] { "UNKNOWN_NATIVE_PREDICATE Current value" }),
            "Unknown native calendar predicates did not remain explicit.");

        var tamperedWindowsPath = Path.Combine(root, "tampered-master-angler-windows.json");
        var tamperedWindows = JsonNode.Parse(File.ReadAllText(windowsPath))!.AsObject();
        tamperedWindows["catalog_sha256"] = new string('0', 64);
        File.WriteAllText(
            tamperedWindowsPath,
            tamperedWindows.ToJsonString(JsonDefaults.Options));
        var tamperedCalendarSourceRejected = false;
        try
        {
            _ = AcquisitionRouteCalendarResolutionBuilder.Build(
                inventoryPath,
                loweringPath,
                tamperedWindowsPath);
        }
        catch (InvalidDataException)
        {
            tamperedCalendarSourceRejected = true;
        }
        Require(tamperedCalendarSourceRejected,
            "A tampered calendar source chain did not fail closed.");

        var tamperedWindowContentPath = Path.Combine(
            root,
            "tampered-master-angler-window-content.json");
        var tamperedWindowContent = JsonNode.Parse(File.ReadAllText(windowsPath))!
            .AsObject();
        tamperedWindowContent["species"]![0]!["windows"]![0]!["source_key"] =
            "Beach:999";
        File.WriteAllText(
            tamperedWindowContentPath,
            tamperedWindowContent.ToJsonString(JsonDefaults.Options));
        var tamperedWindowContentRejected = false;
        try
        {
            _ = AcquisitionRouteCalendarResolutionBuilder.Build(
                inventoryPath,
                loweringPath,
                tamperedWindowContentPath);
        }
        catch (InvalidDataException)
        {
            tamperedWindowContentRejected = true;
        }
        Require(tamperedWindowContentRejected,
            "Tampered compiled calendar windows did not fail closed.");

        Write(calibrationPath, StageOneRouteTimingCalibration());
        WriteStageOneCollectionSnapshot(snapshotPath, stateHash, fish);

        var intents = MasterAnglerTargetDateIntentBuilder.Build(
            windowsPath,
            snapshotPath,
            calibrationPath);
        Write(intentsPath, intents);
        var parameters = intents.Candidates.Single().Parameters;
        var sharedCandidate = CollectionCandidate(
                "shared-parsnip-harvest",
                "farm.maintain_crops",
                "harvest_crop_tile",
                "24",
                "(O)24",
                99,
                1);
        sharedCandidate.LocationId = "Farm";
        sharedCandidate.TileX = 4;
        sharedCandidate.TileY = 5;
        sharedCandidate.EstimatedTicks = 60;
        sharedCandidate.Score = 9000;
        sharedCandidate.ModelScore = 8000;
        sharedCandidate.ExpectedReward = 7000;
        var routeCandidate = CollectionCandidate(
                "master-angler-route",
                "fishing.catch_fish",
                "route_connector_tile",
                string.Empty,
                string.Empty,
                100,
                0,
                parameters.Concat(new[]
                {
                    Parameter("continuation.option_id", "fishing.catch_fish"),
                    Parameter(
                        "master_angler_time_budget_status",
                        "conservative_full_remaining_connector_path_and_terminal_reserve"),
                    Parameter("connector_kind", "building_door"),
                    Parameter("expected_target_location", "Town"),
                    Parameter("expected_arrival_tile_x", "1"),
                    Parameter("expected_arrival_tile_y", "5"),
                    Parameter("estimated_minutes", "2")
                }).ToArray());
        routeCandidate.AvailabilityClass = "master_angler_rolling_route";
        routeCandidate.LocationId = "Farm";
        routeCandidate.TileX = 2;
        routeCandidate.TileY = 4;
        routeCandidate.EstimatedTicks = 120;
        routeCandidate.Score = -9000;
        routeCandidate.ModelScore = -8000;
        routeCandidate.ExpectedReward = -7000;
        var wrongCrabCandidate = CollectionCandidate(
                "wrong-crab-output",
                "fishing.collect_crab_pots",
                "collect_crab_pot",
                "test_1",
                "(O)test_1",
                1,
                1,
                parameters.Concat(new[]
                {
                    Parameter(
                        "continuation.option_id",
                        "fishing.collect_crab_pots"),
                    Parameter(
                        "master_angler_time_budget_status",
                        "conservative_full_remaining_connector_path_and_terminal_reserve"),
                    Parameter("expected_fish_collection_eligible", "1")
                }).ToArray());
        wrongCrabCandidate.AvailabilityClass =
            "master_angler_exact_ready_crab_pot";
        Write(rankingPath, CollectionRanking(
            stateHash,
            sharedCandidate,
            routeCandidate,
            wrongCrabCandidate));

        var result = CurrentStageOneCollectionTeacherFrontierBuilder.Build(
            inventoryPath,
            loweringPath,
            rankingPath,
            snapshotPath,
            intentsPath);
        Require(result.Status == "candidate_contract_ready" &&
                result.RequirementSetCount == 4 &&
                result.CurrentCandidateMembershipEligible &&
                !result.TeacherPreferenceLabelEligible &&
                !result.UsesLearnerRankOrScore &&
                !result.EmitsNegativeLabelsForUnavailableRoutes,
            "Unified Stage 1 collection candidate contract policy drifted.");
        Require(result.MasterAngler.RequiredGroupCount == 72 &&
                result.MasterAngler.ObservedGroupCount == 72 &&
                result.MasterAngler.MissingGroupCount == 1 &&
                result.MasterAngler.CurrentIntentCount == 1 &&
                result.MasterAngler.CandidateBindings.Length == 1 &&
                result.MasterAngler.RejectedIntentCandidates.Single().CandidateId ==
                    "wrong-crab-output",
            "Master Angler current requirement binding or strict crab-pot identity drifted.");
        var shared = result.SelectionContract.CandidateChoices.Single(value =>
            value.CandidateId == "shared-parsnip-harvest");
        Require(shared.RequirementCredits.Length == 2 &&
                shared.RequirementCredits.Select(value => value.RequirementSetId)
                    .ToHashSet(StringComparer.Ordinal)
                    .SetEquals(new[]
                    {
                        "full_shipment",
                        "community_center_standard"
                    }),
            "One current candidate was not deduplicated across exact requirement credits.");
        Require(result.SelectionContract.SelectionGroups.Length == 4 &&
                result.SelectionContract.CandidateChoices.Length == 2 &&
                result.SelectionContract.SelectionGroups.All(value =>
                    value.MaximumSelectedCandidateCountThisDecision is 0 or 1),
            "Unified collection selection cardinality drifted.");

        var preference =
            CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath);
        Require(preference.Status == "ready" &&
                preference.TeacherPreferenceLabelEligible &&
                !preference.FormalTrainingAuthorized &&
                !preference.UsesLearnerRankOrScore &&
                !preference.EmitsNegativeLabelsForUnavailableRoutes &&
                preference.SelectedCandidate?.CandidateId ==
                    "master-angler-route" &&
                preference.SelectedCandidate.SelectionReason ==
                    "authoritative_current_day_deadline" &&
                preference.PairwisePreferences.Length == 1 &&
                preference.PairwisePreferences[0]
                    .FirstDifferingAuthoritativeCriterion ==
                    "authoritative_current_day_deadline" &&
                preference.CompiledPlan?.Steps.Length == 1 &&
                preference.CompiledQueue?.Status == "pending" &&
                preference.CompiledQueue.Items.Length == 1,
            "Independent current collection Teacher preference did not select and compile the authoritative deadline candidate.");
        Write(preferencePath, preference);

        const string teacherRunId = "fixture-teacher-receipt-run";
        const string afterStateHash =
            "stage-one-collection-frontier-after-route-state";
        WriteStageOneCollectionSnapshot(
            afterSnapshotPath,
            afterStateHash,
            fish,
            gameTick: 2,
            playerLocation: "Town",
            playerTileX: 1,
            playerTileY: 5);
        Write(
            receiptPath,
            StageOneRouteReceipt(
                preference,
                teacherRunId,
                afterStateHash,
                2,
                snapshotPath,
                afterSnapshotPath));
        var receiptAdmission =
            CurrentStageOneCollectionTeacherReceiptBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath,
                preferencePath,
                receiptPath,
                afterSnapshotPath,
                "fixture-teacher-trajectory",
                teacherRunId,
                PolicyTrajectoryVersionPins.KnowledgeDictionary,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(receiptAdmission.Status == "ready" &&
                receiptAdmission.TeacherTrainingRowEligible &&
                !receiptAdmission.FormalTrainingAuthorized &&
                receiptAdmission.VerifiedRequirementTransitions.Length == 1 &&
                receiptAdmission.VerifiedRequirementTransitions[0].Verified &&
                receiptAdmission.TrainingRow?.Candidates.Length == 2 &&
                receiptAdmission.TrainingRow.Candidates.All(candidate =>
                    candidate.Available &&
                    candidate.Score == 0 &&
                    candidate.SourceCandidate.ModelScore is null) &&
                receiptAdmission.TrainingRow.Audit.TeacherSupervision is { } teacherSupervision &&
                teacherSupervision.SelectedCandidateId ==
                    "master-angler-route" &&
                teacherSupervision.RequirementTransitions.Length == 1,
            "A fresh exact route receipt did not produce a standalone Teacher-supervised policy row.");
        var teacherDatasetPath = Path.Combine(root, "teacher-receipt.jsonl");
        WriteJsonl(
            teacherDatasetPath,
            new[] { receiptAdmission.TrainingRow! });
        var dataset = new PolicyTrajectoryDatasetBuilder().Build(
            teacherDatasetPath,
            Path.Combine(root, "teacher-receipt-dataset"),
            expectedKnowledgeDictionary:
                PolicyTrajectoryVersionPins.KnowledgeDictionary);
        Require(dataset.Manifest.Counts.AcceptedRows == 1 &&
                dataset.Manifest.Counts.RejectedRows == 0,
            "The canonical policy dataset rejected an exact Teacher-supervised receipt row.");
        var tamperedTeacherRow = JsonSerializer.Deserialize<
            PolicyDecisionTrajectoryEnvelope>(
            JsonSerializer.Serialize(
                receiptAdmission.TrainingRow,
                JsonDefaults.Options),
            JsonDefaults.Options)!;
        tamperedTeacherRow.TrajectoryId = "fixture-tampered-teacher-evidence";
        tamperedTeacherRow.Audit.TeacherSupervision!.AfterSnapshotSha256 =
            "not-a-sha256";
        var tamperedDatasetPath = Path.Combine(
            root,
            "tampered-teacher-receipt.jsonl");
        WriteJsonl(
            tamperedDatasetPath,
            new[] { tamperedTeacherRow, receiptAdmission.TrainingRow! });
        var tamperedDataset = new PolicyTrajectoryDatasetBuilder().Build(
            tamperedDatasetPath,
            Path.Combine(root, "tampered-teacher-receipt-dataset"),
            expectedKnowledgeDictionary:
                PolicyTrajectoryVersionPins.KnowledgeDictionary);
        Require(tamperedDataset.Manifest.Counts.AcceptedRows == 1 &&
                tamperedDataset.Manifest.Counts.RejectedRows == 1 &&
                tamperedDataset.Manifest.Rejections.Any(value =>
                    value.Reason == "teacher_supervision_invalid" &&
                    value.Count == 1),
            "The canonical policy dataset accepted tampered Teacher provenance.");

        var uppercaseHashPreference = JsonSerializer.Deserialize<
            CurrentStageOneCollectionTeacherPreferenceLabel>(
            JsonSerializer.Serialize(preference, JsonDefaults.Options),
            JsonDefaults.Options)!;
        var hashCharacters = uppercaseHashPreference
            .RequirementInventorySha256.ToCharArray();
        var letterIndex = Array.FindIndex(hashCharacters, char.IsLetter);
        Require(letterIndex >= 0,
            "The fixture SHA-256 unexpectedly contains no hexadecimal letters.");
        hashCharacters[letterIndex] = char.ToUpperInvariant(
            hashCharacters[letterIndex]);
        uppercaseHashPreference.RequirementInventorySha256 =
            new string(hashCharacters);
        var uppercaseHashPreferencePath = Path.Combine(
            root,
            "uppercase-hash-preference.json");
        Write(uppercaseHashPreferencePath, uppercaseHashPreference);
        var uppercaseHashAdmission =
            CurrentStageOneCollectionTeacherReceiptBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath,
                uppercaseHashPreferencePath,
                receiptPath,
                afterSnapshotPath,
                "fixture-uppercase-hash",
                teacherRunId,
                PolicyTrajectoryVersionPins.KnowledgeDictionary,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(uppercaseHashAdmission.Status ==
                    "blocked_teacher_preference_not_ready" &&
                uppercaseHashAdmission.BlockingReasons.Contains(
                    "teacher_preference_source_hash_mismatch",
                    StringComparer.Ordinal),
            "A non-canonical Teacher source hash was admitted.");

        WriteStageOneCollectionSnapshot(
            afterSnapshotPath,
            "stage-one-collection-no-route-transition",
            fish,
            gameTick: 2,
            playerLocation: "Farm",
            playerTileX: 3,
            playerTileY: 5);
        Write(
            receiptPath,
            StageOneRouteReceipt(
                preference,
                teacherRunId,
                "stage-one-collection-no-route-transition",
                2,
                snapshotPath,
                afterSnapshotPath));
        var noTransition =
            CurrentStageOneCollectionTeacherReceiptBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath,
                preferencePath,
                receiptPath,
                afterSnapshotPath,
                "fixture-no-transition",
                teacherRunId,
                PolicyTrajectoryVersionPins.KnowledgeDictionary,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(noTransition.Status ==
                    "blocked_exact_requirement_transition_missing" &&
                !noTransition.TeacherTrainingRowEligible &&
                noTransition.TrainingRow is null,
            "A successful receipt without the credited route transition emitted a training row.");

        WriteStageOneCollectionSnapshot(
            afterSnapshotPath,
            afterStateHash,
            fish,
            gameTick: 2,
            playerLocation: "Town",
            playerTileX: 1,
            playerTileY: 5);
        var mismatchedReceipt = StageOneRouteReceipt(
            preference,
            teacherRunId,
            afterStateHash,
            2,
            snapshotPath,
            afterSnapshotPath);
        mismatchedReceipt.QueueId = "wrong-queue";
        Write(receiptPath, mismatchedReceipt);
        var wrongQueue =
            CurrentStageOneCollectionTeacherReceiptBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath,
                preferencePath,
                receiptPath,
                afterSnapshotPath,
                "fixture-wrong-queue",
                teacherRunId,
                PolicyTrajectoryVersionPins.KnowledgeDictionary,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(wrongQueue.Status == "blocked_execution_receipt_not_exact" &&
                wrongQueue.BlockingReasons.Contains(
                    "execution_receipt_queue_id_mismatch",
                    StringComparer.Ordinal),
            "A receipt from another compiled queue was admitted.");

        var mismatchedPrimitiveReceipt = StageOneRouteReceipt(
            preference,
            teacherRunId,
            afterStateHash,
            2,
            snapshotPath,
            afterSnapshotPath);
        mismatchedPrimitiveReceipt.PrimitiveKind = "move_to_tile";
        mismatchedPrimitiveReceipt.EffectiveQueueItem = null;
        Write(receiptPath, mismatchedPrimitiveReceipt);
        var wrongPrimitive =
            CurrentStageOneCollectionTeacherReceiptBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath,
                preferencePath,
                receiptPath,
                afterSnapshotPath,
                "fixture-wrong-primitive",
                teacherRunId,
                PolicyTrajectoryVersionPins.KnowledgeDictionary,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(wrongPrimitive.Status ==
                    "blocked_execution_receipt_not_exact" &&
                wrongPrimitive.BlockingReasons.Contains(
                    "execution_receipt_primitive_kind_mismatch",
                    StringComparer.Ordinal) &&
                wrongPrimitive.BlockingReasons.Contains(
                    "execution_receipt_effective_queue_item_mismatch",
                    StringComparer.Ordinal),
            "A receipt for another primitive or without its effective queue item was admitted.");

        sharedCandidate.Rank = 1;
        sharedCandidate.Score = 1_000_000;
        sharedCandidate.ModelScore = 1_000_000;
        sharedCandidate.ExpectedReward = 1_000_000;
        routeCandidate.Rank = 1000;
        routeCandidate.Score = -1_000_000;
        routeCandidate.ModelScore = -1_000_000;
        routeCandidate.ExpectedReward = -1_000_000;
        Write(rankingPath, CollectionRanking(
            stateHash,
            sharedCandidate,
            routeCandidate,
            wrongCrabCandidate));
        var learnerSignalInvariant =
            CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath);
        Require(learnerSignalInvariant.Status == "ready" &&
                learnerSignalInvariant.SelectedCandidate?.CandidateId ==
                    "master-angler-route" &&
                learnerSignalInvariant.CompiledPlan?.Steps.Length == 1 &&
                learnerSignalInvariant.CompiledQueue?.Items.Length == 1,
            "Learner rank or score changed the deterministic Teacher preference.");

        var tieA = CollectionCandidate(
            "tied-parsnip-a",
            "farm.maintain_crops",
            "harvest_crop_tile",
            "24",
            "(O)24",
            1,
            1);
        var tieB = CollectionCandidate(
            "tied-parsnip-b",
            "farm.maintain_crops",
            "harvest_crop_tile",
            "24",
            "(O)24",
            2,
            1);
        foreach (var candidate in new[] { tieA, tieB })
        {
            candidate.LocationId = "Farm";
            candidate.TileX = 4;
            candidate.TileY = 5;
            candidate.EstimatedTicks = 60;
        }
        Write(rankingPath, CollectionRanking(stateHash, tieA, tieB));
        var tiedPreference =
            CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath);
        Require(tiedPreference.Status ==
                    "blocked_authoritatively_tied_top_candidates" &&
                !tiedPreference.TeacherPreferenceLabelEligible &&
                tiedPreference.SelectedCandidate is null &&
                tiedPreference.BlockingReasons.Length == 1,
            "An arbitrary candidate ID was used to manufacture a Teacher preference tie-break.");

        intents.SourceStateHash = "stale-master-angler-intent-state";
        Write(intentsPath, intents);
        var staleIntentRejected = false;
        try
        {
            _ = CurrentStageOneCollectionTeacherFrontierBuilder.Build(
                inventoryPath,
                loweringPath,
                rankingPath,
                snapshotPath,
                intentsPath);
        }
        catch (InvalidDataException)
        {
            staleIntentRejected = true;
        }
        Require(staleIntentRejected,
            "A Master Angler intent from a different decision state was admitted.");
    }

}
