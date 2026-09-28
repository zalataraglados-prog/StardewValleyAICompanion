using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private sealed partial class StageOneCollectionFrontierFixture
    {
        private AcquisitionRouteCalendarResolutionReport BuildStaticInputs()
        {
            fish = Enumerable.Range(0, 72)
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
                        ["HarvestMinStack"] = 1,
                        ["HarvestMaxStack"] = 1,
                        ["ExtraHarvestChance"] = 0d,
                        ["HarvestMinQuality"] = 1,
                        ["HarvestMaxQuality"] = null,
                        ["Texture"] = @"TileSheets\crops",
                        ["SpriteIndex"] = 0
                    },
                    ["473"] = new Dictionary<string, object?>
                    {
                        ["Seasons"] = new[] { 0 },
                        ["DaysInPhase"] = new[] { 1, 1, 1, 1 },
                        ["RegrowDays"] = 3,
                        ["IsPaddyCrop"] = false,
                        ["NeedsWatering"] = true,
                        ["PlantableLocationRules"] = null,
                        ["HarvestItemId"] = "188",
                        ["HarvestMinStack"] = 1,
                        ["HarvestMaxStack"] = 1,
                        ["ExtraHarvestChance"] = 0d,
                        ["HarvestMinQuality"] = 0,
                        ["HarvestMaxQuality"] = null,
                        ["Texture"] = @"TileSheets\crops",
                        ["SpriteIndex"] = 1
                    }
                }
            });
            const string fixtureShopCondition =
                "!YEAR 2, DAY_OF_WEEK Monday, TIME 700 1800, " +
                "!IS_FESTIVAL_DAY, " +
                "IS_PASSIVE_FESTIVAL_OPEN FixtureFest, " +
                "DAYS_PLAYED 1 1, " +
                "PLAYER_HAS_MAIL Current fixtureGate, " +
                "PLAYER_HAS_MAIL Host hostGate, " +
                "PLAYER_SPECIAL_ORDER_ACTIVE Current Gunther, " +
                "!PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY, " +
                "PLAYER_STAT Current Book_Woodcutting 1, " +
                "IS_ISLAND_NORTH_BRIDGE_FIXED, " +
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
                                ["Price"] = 100,
                                ["AvailableStock"] = 2,
                                ["AvailableStockLimit"] = 2,
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
                                "24", "(O)24", "Parsnip", "item_id", 2, 1,
                                "harvests_as", "crop:472", includeFixtureShopRoute: true),
                            CollectionAlternative(
                                "188", "(O)188", "Green Bean", "item_id", 1, 0,
                                "harvests_as", "crop:473")))
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
                route_occurrence_count = 77,
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
                                "24", "(O)24", "Parsnip", "item_id", 2, 1,
                                "harvests_as", "crop:472", "farm.maintain_crops",
                                includeFixtureShopRoute: true),
                            CollectionLoweredAlternative(
                                "188", "(O)188", "Green Bean", "item_id", 1, 0,
                                "harvests_as", "crop:473",
                                "farm.maintain_crops")))
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
                location_rule_spawn_chance_input_count = 72,
                location_rule_spawn_chance_input_inventory_complete = true,
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
                            item_id = value.QualifiedItemId,
                            random_item_ids = Array.Empty<string>(),
                            item_selection_mode = "direct_item_id",
                            spawn_chance_input_status =
                                "complete_static_spawn_chance_inputs_dynamic_context_pending",
                            base_chance = 1.0,
                            apply_daily_luck = false,
                            curiosity_lure_buff = -1.0,
                            specific_bait_buff = 0.0,
                            specific_bait_multiplier = 1.66,
                            chance_boost_per_luck_level = 0.0,
                            chance_modifier_mode = 0,
                            chance_modifiers = Array.Empty<object>(),
                            use_fish_caught_seeded_random = false,
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
                unresolved_calendar_rule_ids = Array.Empty<string>(),
                unresolved_spawn_chance_input_rule_ids = Array.Empty<string>()
            });
            var staleProbabilityCatalog = JsonNode.Parse(File.ReadAllText(catalogPath))!;
            staleProbabilityCatalog["location_rule_spawn_chance_input_inventory_complete"] = false;
            Write(staleProbabilityCatalogPath, staleProbabilityCatalog);
            var staleProbabilityCatalogRejected = false;
            try
            {
                MasterAnglerStageOneWindowIndexBuilder.Build(
                    staleProbabilityCatalogPath,
                    3);
            }
            catch (InvalidDataException)
            {
                staleProbabilityCatalogRejected = true;
            }
            Require(staleProbabilityCatalogRejected,
                "Master Angler window builder admitted a catalog without spawn-chance inputs.");
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
                    routeCalendar.RouteOccurrenceCount == 77 &&
                    routeCalendar.ResolvedStaticSourceCount == 77 &&
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
                    resolvedCropRoute.RequiredAmount == 1 &&
                    resolvedCropRoute.MinimumQuality == 0 &&
                    resolvedCropRoute.CropSource.HarvestMinStack == 1 &&
                    resolvedCropRoute.CropSource.HarvestMaxStack == 1 &&
                    resolvedCropRoute.CropSource.ExtraHarvestChance == 0d &&
                    resolvedCropRoute.CropSource.HarvestMinQuality == 1 &&
                    resolvedCropRoute.CropSource.HarvestMaxQuality is null &&
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
                            "!IS_FESTIVAL_DAY",
                            "IS_PASSIVE_FESTIVAL_OPEN FixtureFest",
                            "DAYS_PLAYED 1 1",
                            "PLAYER_HAS_MAIL Current fixtureGate",
                            "PLAYER_HAS_MAIL Host hostGate",
                            "PLAYER_SPECIAL_ORDER_ACTIVE Current Gunther",
                            "!PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY",
                            "PLAYER_STAT Current Book_Woodcutting 1",
                            "IS_ISLAND_NORTH_BRIDGE_FIXED",
                            "SYNCED_RANDOM day fixture 0.25"
                        })) &&
                    resolvedShopRoute.ShopSource is not null &&
                    resolvedShopRoute.ShopSource.ShopId == "FixtureShop" &&
                    resolvedShopRoute.ShopSource.StockRowIndex == 0 &&
                    resolvedShopRoute.ShopSource.DataItemQualifiedId == "(O)24" &&
                    resolvedShopRoute.ShopSource.AvailableStock == 2 &&
                    resolvedShopRoute.ShopSource.AvailableStockLimit == 2 &&
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

            var unresolvedWildSeedRoute = resolvedCropRoute with
            {
                QualifiedItemId = "(O)16",
                CropSource = resolvedCropRoute.CropSource! with
                {
                    SeedItemId = "495",
                    DataHarvestQualifiedItemId = "(O)16",
                    PossibleHarvestQualifiedItemIds =
                        new[] { "(O)16", "(O)18", "(O)20", "(O)22" },
                    StochasticOutcome = true
                }
            };
            Require(
                AcquisitionRouteTargetDateStochasticRetryBuilder
                    .SourceResolvedOutcomeGuaranteed(resolvedCropRoute) &&
                !AcquisitionRouteTargetDateStochasticRetryBuilder
                    .SourceResolvedRetryEvidenceRequired(
                        resolvedCropRoute,
                        currentOutputAlreadyMaterialized: false) &&
                !AcquisitionRouteTargetDateStochasticRetryBuilder
                    .SourceResolvedOutcomeGuaranteed(unresolvedWildSeedRoute) &&
                AcquisitionRouteTargetDateStochasticRetryBuilder
                    .SourceResolvedRetryEvidenceRequired(
                        unresolvedWildSeedRoute,
                        currentOutputAlreadyMaterialized: false) &&
                !AcquisitionRouteTargetDateStochasticRetryBuilder
                    .SourceResolvedRetryEvidenceRequired(
                        unresolvedWildSeedRoute,
                        currentOutputAlreadyMaterialized: true),
                "Source-resolved stochastic crop retry classification drifted.");

            return routeCalendar;
        }
    }
}
