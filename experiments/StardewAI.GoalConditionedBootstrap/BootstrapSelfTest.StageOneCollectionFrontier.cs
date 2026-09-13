using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
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
        var targetDateCalendarPath = Path.Combine(root, "target-date-calendar.json");
        var targetDateUnlockPath = Path.Combine(root, "target-date-unlock.json");
        var targetDateFestivalPath = Path.Combine(root, "target-date-festival.json");
        var targetDateLocationPath = Path.Combine(root, "target-date-location.json");
        var targetDateFacilityPath = Path.Combine(root, "target-date-facility.json");
        var targetDateResourcePath = Path.Combine(root, "target-date-resource.json");
        var targetDateCurrencyPath = Path.Combine(root, "target-date-currency.json");
        var targetDateReservationPath = Path.Combine(
            root,
            "target-date-inventory-reservation.json");
        var targetDateProcessingPath = Path.Combine(
            root,
            "target-date-processing-lead-time.json");
        var tamperedTargetDateReservationPath = Path.Combine(
            root,
            "tampered-target-date-inventory-reservation.json");
        var strategyLedgerPath = Path.Combine(root, "strategy-ledger.json");
        var materialReservedLedgerPath = Path.Combine(
            root,
            "material-reserved-strategy-ledger.json");
        var currencyReservedLedgerPath = Path.Combine(
            root,
            "currency-reserved-strategy-ledger.json");
        var cancelledReservationLedgerPath = Path.Combine(
            root,
            "cancelled-reservation-strategy-ledger.json");
        var committedReservationLedgerPath = Path.Combine(
            root,
            "committed-reservation-strategy-ledger.json");
        var mismatchedStrategyLedgerPath = Path.Combine(
            root,
            "mismatched-strategy-ledger.json");
        var missingShopQuoteSnapshotPath = Path.Combine(
            root,
            "missing-shop-quote-snapshot.json");
        var missingCurrencySnapshotPath = Path.Combine(
            root,
            "missing-currency-snapshot.json");
        var insufficientCurrencySnapshotPath = Path.Combine(
            root,
            "insufficient-currency-snapshot.json");
        var currencyVariantUnlockPath = Path.Combine(
            root,
            "currency-variant-unlock.json");
        var currencyVariantFestivalPath = Path.Combine(
            root,
            "currency-variant-festival.json");
        var currencyVariantLocationPath = Path.Combine(
            root,
            "currency-variant-location.json");
        var currencyVariantFacilityPath = Path.Combine(
            root,
            "currency-variant-facility.json");
        var currencyVariantResourcePath = Path.Combine(
            root,
            "currency-variant-resource.json");
        var targetDateRouteCalibrationPath = Path.Combine(
            root,
            "target-date-route-timing.json");
        var targetDateUnlockSnapshotPath = Path.Combine(
            root,
            "target-date-unlock-snapshot.json");
        var missingUnlockStateSnapshotPath = Path.Combine(
            root,
            "missing-unlock-state-snapshot.json");
        var mismatchedUnlockDateSnapshotPath = Path.Combine(
            root,
            "mismatched-unlock-date-snapshot.json");
        var missingCalendarStateSnapshotPath = Path.Combine(
            root,
            "missing-calendar-state-snapshot.json");
        var missingCalendarStateUnlockPath = Path.Combine(
            root,
            "missing-calendar-state-unlock.json");
        var mismatchedCalendarTimeSnapshotPath = Path.Combine(
            root,
            "mismatched-calendar-time-snapshot.json");
        var tamperedTargetDateUnlockPath = Path.Combine(
            root,
            "tampered-target-date-unlock.json");
        var tamperedTargetDateFestivalPath = Path.Combine(
            root,
            "tampered-target-date-festival.json");
        var tamperedTargetDateLocationPath = Path.Combine(
            root,
            "tampered-target-date-location.json");
        var tamperedTargetDateFacilityPath = Path.Combine(
            root,
            "tampered-target-date-facility.json");
        var missingResourceSnapshotPath = Path.Combine(
            root,
            "missing-resource-evidence-snapshot.json");
        var missingResourceUnlockPath = Path.Combine(
            root,
            "missing-resource-evidence-unlock.json");
        var missingResourceFestivalPath = Path.Combine(
            root,
            "missing-resource-evidence-festival.json");
        var missingResourceLocationPath = Path.Combine(
            root,
            "missing-resource-evidence-location.json");
        var missingResourceFacilityPath = Path.Combine(
            root,
            "missing-resource-evidence-facility.json");
        var insufficientSeedSnapshotPath = Path.Combine(
            root,
            "insufficient-seed-snapshot.json");
        var insufficientSeedUnlockPath = Path.Combine(
            root,
            "insufficient-seed-unlock.json");
        var insufficientSeedFestivalPath = Path.Combine(
            root,
            "insufficient-seed-festival.json");
        var insufficientSeedLocationPath = Path.Combine(
            root,
            "insufficient-seed-location.json");
        var insufficientSeedFacilityPath = Path.Combine(
            root,
            "insufficient-seed-facility.json");
        var missingFacilitySnapshotPath = Path.Combine(
            root,
            "missing-facility-evidence-snapshot.json");
        var missingFacilityUnlockPath = Path.Combine(
            root,
            "missing-facility-evidence-unlock.json");
        var missingFacilityFestivalPath = Path.Combine(
            root,
            "missing-facility-evidence-festival.json");
        var missingFacilityLocationPath = Path.Combine(
            root,
            "missing-facility-evidence-location.json");
        var zeroFacilitySnapshotPath = Path.Combine(
            root,
            "zero-facility-capacity-snapshot.json");
        var zeroFacilityUnlockPath = Path.Combine(
            root,
            "zero-facility-capacity-unlock.json");
        var zeroFacilityFestivalPath = Path.Combine(
            root,
            "zero-facility-capacity-festival.json");
        var zeroFacilityLocationPath = Path.Combine(
            root,
            "zero-facility-capacity-location.json");
        var missingRouteSnapshotPath = Path.Combine(
            root,
            "missing-route-evidence-snapshot.json");
        var missingRouteUnlockPath = Path.Combine(
            root,
            "missing-route-evidence-unlock.json");
        var missingRouteFestivalPath = Path.Combine(
            root,
            "missing-route-evidence-festival.json");
        var deniedRouteSnapshotPath = Path.Combine(
            root,
            "denied-route-snapshot.json");
        var deniedRouteUnlockPath = Path.Combine(
            root,
            "denied-route-unlock.json");
        var deniedRouteFestivalPath = Path.Combine(
            root,
            "denied-route-festival.json");
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
            route_occurrence_count = 76,
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

        var targetDateCalendar = AcquisitionRouteTargetDateCalendarBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            0);
        Write(targetDateCalendarPath, targetDateCalendar);
        var targetDateShop = targetDateCalendar.Routes.Single(route =>
            route.RequirementId == "community_center:bundle:Pantry/5" &&
            route.RouteKind == "sells");
        Require(targetDateCalendar.Status ==
                    "complete_target_date_calendar_axis_downstream_pending" &&
                targetDateCalendar.RouteOccurrenceInventoryComplete &&
                targetDateCalendar.CalendarAxisResolutionComplete &&
                !targetDateCalendar.TrainingLabelEligible &&
                targetDateCalendar.TargetTotalDay == 0 &&
                targetDateCalendar.RouteOccurrenceCount == 76 &&
                targetDateCalendar.CalendarAxisResolvedCount == 76 &&
                targetDateCalendar.StaticWindowMatchCount == 4 &&
                targetDateCalendar.StaticWindowMissCount == 72 &&
                targetDateCalendar.BlockedStaticSourceCount == 0 &&
                targetDateCalendar.StaticCalendarResolutionSha256 ==
                    HashFile(routeCalendarPath) &&
                targetDateShop.CalendarAxisResolved &&
                targetDateShop.StaticWindowMatchesTargetDate &&
                targetDateShop.CalendarAxisStatus ==
                    "resolved_target_date_inside_static_window_downstream_pending" &&
                targetDateShop.MatchingWindows.Length == 1 &&
                targetDateShop.MatchingWindows[0].TimeWindows.Length == 1 &&
                targetDateShop.MatchingWindows[0].TimeWindows[0].StartTime == 700 &&
                targetDateShop.MatchingWindows[0].TimeWindows[0].EndTime == 1810 &&
                targetDateShop.PendingDynamicConditions.SequenceEqual(new[]
                {
                    "!IS_FESTIVAL_DAY",
                    "!PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY",
                    "DAYS_PLAYED 1 1",
                    "IS_ISLAND_NORTH_BRIDGE_FIXED",
                    "IS_PASSIVE_FESTIVAL_OPEN FixtureFest",
                    "PLAYER_HAS_MAIL Current fixtureGate",
                    "PLAYER_HAS_MAIL Host hostGate",
                    "PLAYER_SPECIAL_ORDER_ACTIVE Current Gunther",
                    "PLAYER_STAT Current Book_Woodcutting 1",
                    "SYNCED_RANDOM day fixture 0.25"
                }),
            "Explicit target-date calendar-axis resolution drifted.");

        WriteStageOneCollectionSnapshot(
            targetDateUnlockSnapshotPath,
            "target-date-unlock-state",
            fish,
            totalDays: 0,
            timeOfDay: 700);
        var targetDateUnlock = AcquisitionRouteTargetDateUnlockBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            targetDateUnlockSnapshotPath);
        Write(targetDateUnlockPath, targetDateUnlock);
        var targetDateUnlockShop = targetDateUnlock.Routes.Single(route =>
            route.RequirementId == "community_center:bundle:Pantry/5" &&
            route.RouteKind == "sells");
        Require(targetDateUnlock.Status ==
                    "complete_target_date_unlock_axis_downstream_pending" &&
                targetDateUnlock.RouteOccurrenceInventoryComplete &&
                targetDateUnlock.UnlockAxisResolutionComplete &&
                !targetDateUnlock.TrainingLabelEligible &&
                targetDateUnlock.TargetTotalDay == 0 &&
                targetDateUnlock.RouteOccurrenceCount == 76 &&
                targetDateUnlock.UnlockAxisResolvedCount == 76 &&
                targetDateUnlock.UnlockStateMatchCount == 4 &&
                targetDateUnlock.UnlockStateMissCount == 0 &&
                targetDateUnlock.StaticWindowMissCount == 72 &&
                targetDateUnlock.BlockedUpstreamCalendarCount == 0 &&
                targetDateUnlock.BlockedUnlockEvidenceCount == 0 &&
                targetDateUnlock.PendingCalendarConditionCount == 3 &&
                targetDateUnlock.PendingStochasticConditionCount == 1 &&
                targetDateUnlock.UnsupportedConditionCount == 0 &&
                targetDateUnlockShop.UnlockAxisResolved &&
                targetDateUnlockShop.UnlockStateMatchesTargetDate == true &&
                targetDateUnlockShop.SourceResolutionStatus ==
                    "resolved_static_source_window_target_date_pending" &&
                targetDateUnlockShop.MatchingWindows.Length == 1 &&
                targetDateUnlockShop.MatchingWindows[0].TimeWindows.Length == 1 &&
                targetDateUnlockShop.MatchingWindows[0].TimeWindows[0]
                    .StartTime == 700 &&
                targetDateUnlockShop.MatchingWindows[0].TimeWindows[0]
                    .EndTime == 1810 &&
                targetDateUnlockShop.UnlockConditions.Length == 6 &&
                targetDateUnlockShop.UnlockConditions.All(value =>
                    value.Status == "resolved_match" &&
                    value.ConditionMatches == true) &&
                targetDateUnlockShop.PendingStochasticConditions
                    .SequenceEqual(new[]
                    {
                        "SYNCED_RANDOM day fixture 0.25"
                    }),
            "Explicit target-date unlock-state axis resolution drifted.");

        using (var unlockSnapshot = JsonDocument.Parse(
                   File.ReadAllText(targetDateUnlockSnapshotPath)))
        {
            var evaluator = new AcquisitionUnlockConditionEvaluator(
                unlockSnapshot.RootElement);
            Require(
                evaluator.Evaluate("PLAYER_HAS_MAIL Any anyGate")
                    .ConditionMatches == true &&
                evaluator.Evaluate("PLAYER_HAS_MAIL All allGate")
                    .ConditionMatches == true &&
                evaluator.Evaluate("PLAYER_HAS_MAIL 200 hostGate")
                    .ConditionMatches == true &&
                evaluator.Evaluate("PLAYER_HAS_MAIL Current pendingGate")
                    .ConditionMatches == true &&
                evaluator.Evaluate("PLAYER_HAS_MAIL Current pendingGate Tomorrow")
                    .ConditionMatches == false &&
                evaluator.Evaluate("PLAYER_STAT Host Book_Woodcutting 0 0")
                    .ConditionMatches == true &&
                evaluator.Evaluate("PLAYER_STAT Current Book_Woodcutting 2")
                    .ConditionMatches == false &&
                evaluator.Evaluate(
                        "PLAYER_HAS_MAIL Current fixtureGate Received ignored")
                    .ConditionMatches == true &&
                evaluator.Evaluate(
                        "PLAYER_STAT Current Book_Woodcutting 1 1 ignored")
                    .ConditionMatches == true &&
                evaluator.Evaluate(
                        "IS_ISLAND_NORTH_BRIDGE_FIXED ignored")
                    .ConditionMatches == true &&
                evaluator.Evaluate("PLAYER_HAS_MAIL Target fixtureGate")
                    .Status == "blocked" &&
                evaluator.Evaluate("PLAYER_HAS_MAIL Target fixtureGate")
                    .BlockingReason == "target_player_context_unavailable" &&
                evaluator.Evaluate("!!PLAYER_HAS_MAIL Current fixtureGate")
                    .Status == "blocked",
                "Native Current/Host/Any/All/numeric player selection drifted.");

            var calendarState = AcquisitionCalendarSnapshotState.Read(
                unlockSnapshot.RootElement);
            var calendarEvaluator = new AcquisitionCalendarConditionEvaluator(
                calendarState);
            Require(
                calendarEvaluator.Evaluate("DAYS_PLAYED 1 1 ignored")
                    .ConditionMatches == true &&
                calendarEvaluator.Evaluate("IS_FESTIVAL_DAY")
                    .ConditionMatches == false &&
                calendarEvaluator.Evaluate("!IS_FESTIVAL_DAY")
                    .ConditionMatches == true &&
                calendarEvaluator.Evaluate("IS_FESTIVAL_DAY any 12")
                    .ConditionMatches == true &&
                calendarEvaluator.Evaluate("IS_FESTIVAL_DAY Here")
                    .Status == "blocked" &&
                calendarEvaluator.Evaluate(
                        "IS_PASSIVE_FESTIVAL_OPEN FixtureFest ignored")
                    .ConditionMatches == true &&
                calendarEvaluator.Evaluate(
                        "IS_PASSIVE_FESTIVAL_OPEN MissingFestival")
                    .ConditionMatches == false &&
                calendarEvaluator.Evaluate("!!IS_FESTIVAL_DAY")
                    .Status == "blocked",
                "Native target-date calendar condition semantics drifted.");
        }

        var mismatchedCalendarTimeSnapshot = JsonNode.Parse(
            File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
        mismatchedCalendarTimeSnapshot["state"]!["world_progress"]!
            ["game_state_query_calendar_state"]!["value"]!["time_of_day"] = 600;
        File.WriteAllText(
            mismatchedCalendarTimeSnapshotPath,
            mismatchedCalendarTimeSnapshot.ToJsonString(JsonDefaults.Options));
        var mismatchedCalendarTimeRejected = false;
        try
        {
            using var mismatchedCalendarTimeDocument = JsonDocument.Parse(
                File.ReadAllText(mismatchedCalendarTimeSnapshotPath));
            _ = AcquisitionCalendarSnapshotState.Read(
                mismatchedCalendarTimeDocument.RootElement);
        }
        catch (InvalidDataException)
        {
            mismatchedCalendarTimeRejected = true;
        }
        Require(mismatchedCalendarTimeRejected,
            "Calendar bridge time drift did not fail closed.");

        var targetDateFestival =
            AcquisitionRouteTargetDateFestivalBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockPath,
                targetDateUnlockSnapshotPath);
        Write(targetDateFestivalPath, targetDateFestival);
        var targetDateFestivalShop = targetDateFestival.Routes.Single(route =>
            route.UpstreamRoute.RequirementId ==
                "community_center:bundle:Pantry/5" &&
            route.UpstreamRoute.RouteKind == "sells");
        var sourceUnlockShopJson = JsonSerializer.Serialize(
            targetDateUnlockShop,
            JsonDefaults.Options);
        var carriedUnlockShopJson = JsonSerializer.Serialize(
            targetDateFestivalShop.UpstreamRoute,
            JsonDefaults.Options);
        Require(targetDateFestival.Status ==
                    "complete_target_date_festival_axis_downstream_pending" &&
                targetDateFestival.RouteOccurrenceInventoryComplete &&
                targetDateFestival.CalendarConditionAxisResolutionComplete &&
                !targetDateFestival.TrainingLabelEligible &&
                targetDateFestival.TargetTotalDay == 0 &&
                targetDateFestival.RouteOccurrenceCount == 76 &&
                targetDateFestival.CalendarConditionAxisResolvedCount == 76 &&
                targetDateFestival.CalendarConditionMatchCount == 4 &&
                targetDateFestival.CalendarConditionMissCount == 0 &&
                targetDateFestival.NotApplicableStaticWindowCount == 72 &&
                targetDateFestival.NotApplicableUnlockStateCount == 0 &&
                targetDateFestival.BlockedUpstreamCount == 0 &&
                targetDateFestival.BlockedCalendarEvidenceCount == 0 &&
                targetDateFestival.PendingStochasticConditionCount == 1 &&
                targetDateFestivalShop.CalendarConditionAxisResolved &&
                targetDateFestivalShop.CalendarConditionsMatchTargetDate == true &&
                carriedUnlockShopJson == sourceUnlockShopJson &&
                targetDateFestivalShop.CalendarConditionEvaluations.Length == 3 &&
                targetDateFestivalShop.CalendarConditionEvaluations.All(value =>
                    value.Status == "resolved_match" &&
                    value.ConditionMatches == true),
            "Explicit target-date festival-state axis resolution drifted.");

        Write(
            targetDateRouteCalibrationPath,
            StageOneRouteTimingCalibration(totalDays: 0));
        var targetDateLocation =
            AcquisitionRouteTargetDateLocationBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockPath,
                targetDateFestivalPath,
                targetDateUnlockSnapshotPath,
                targetDateRouteCalibrationPath);
        Write(targetDateLocationPath, targetDateLocation);
        var targetDateLocationShop = targetDateLocation.Routes.Single(route =>
            route.UpstreamRoute.UpstreamRoute.RequirementId ==
                "community_center:bundle:Pantry/5" &&
            route.UpstreamRoute.UpstreamRoute.RouteKind == "sells");
        var sourceFestivalShopJson = JsonSerializer.Serialize(
            targetDateFestivalShop,
            JsonDefaults.Options);
        var carriedFestivalShopJson = JsonSerializer.Serialize(
            targetDateLocationShop.UpstreamRoute,
            JsonDefaults.Options);
        Require(targetDateLocation.Status ==
                    "complete_target_date_location_route_axis_downstream_pending" &&
                targetDateLocation.RouteOccurrenceInventoryComplete &&
                targetDateLocation.LocationRouteAxisResolutionComplete &&
                !targetDateLocation.TrainingLabelEligible &&
                targetDateLocation.TargetTotalDay == 0 &&
                targetDateLocation.RouteOccurrenceCount == 76 &&
                targetDateLocation.LocationRouteAxisResolvedCount == 76 &&
                targetDateLocation.LocationRouteMatchCount == 4 &&
                targetDateLocation.LocationRouteMissCount == 0 &&
                targetDateLocation.NotApplicableStaticWindowCount == 72 &&
                targetDateLocation.NotApplicableUnlockStateCount == 0 &&
                targetDateLocation.NotApplicableCalendarConditionCount == 0 &&
                targetDateLocation.BlockedUpstreamCount == 0 &&
                targetDateLocation.BlockedLocationEvidenceCount == 0 &&
                carriedFestivalShopJson == sourceFestivalShopJson &&
                targetDateLocationShop.LocationRouteMatchesTargetDate == true &&
                targetDateLocationShop.TargetEvaluations.Length == 1 &&
                targetDateLocationShop.TargetEvaluations[0].TargetLocationId ==
                    "FixtureShop" &&
                targetDateLocationShop.TargetEvaluations[0]
                    .GuaranteedArrivalByTime == 902 &&
                targetDateLocationShop.TargetEvaluations[0].Path.Length == 2,
            "Target-date location-route axis resolution drifted.");

        var targetDateFacility =
            AcquisitionRouteTargetDateFacilityBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockPath,
                targetDateFestivalPath,
                targetDateLocationPath,
                targetDateUnlockSnapshotPath,
                targetDateRouteCalibrationPath);
        Write(targetDateFacilityPath, targetDateFacility);
        var targetDateFacilityShop = targetDateFacility.Routes.Single(route =>
            route.UpstreamRoute.UpstreamRoute.UpstreamRoute.RequirementId ==
                "community_center:bundle:Pantry/5" &&
            route.UpstreamRoute.UpstreamRoute.UpstreamRoute.RouteKind ==
                "sells");
        var targetDateFacilityCrops = targetDateFacility.Routes.Where(route =>
                route.UpstreamRoute.UpstreamRoute.UpstreamRoute.RouteKind ==
                    "harvests_as" &&
                route.FacilityCapacityMatchesTargetDate == true)
            .ToArray();
        var sourceLocationShopJson = JsonSerializer.Serialize(
            targetDateLocationShop,
            JsonDefaults.Options);
        var carriedLocationShopJson = JsonSerializer.Serialize(
            targetDateFacilityShop.UpstreamRoute,
            JsonDefaults.Options);
        Require(targetDateFacility.Status ==
                    "complete_target_date_facility_capacity_axis_downstream_pending" &&
                targetDateFacility.RouteOccurrenceInventoryComplete &&
                targetDateFacility.FacilityCapacityAxisResolutionComplete &&
                !targetDateFacility.TrainingLabelEligible &&
                targetDateFacility.TargetTotalDay == 0 &&
                targetDateFacility.RouteOccurrenceCount == 76 &&
                targetDateFacility.FacilityCapacityAxisResolvedCount == 76 &&
                targetDateFacility.FacilityCapacityMatchCount == 4 &&
                targetDateFacility.FacilityCapacityMissCount == 0 &&
                targetDateFacility.FacilityCapacityNotRequiredCount == 2 &&
                targetDateFacility.NotApplicableUpstreamCount == 72 &&
                targetDateFacility.BlockedUpstreamCount == 0 &&
                targetDateFacility.BlockedFacilityEvidenceCount == 0 &&
                targetDateFacilityCrops.Length == 2 &&
                targetDateFacilityCrops.All(route =>
                    route.FacilityRequirementKind ==
                        "prepared_cultivation_slot" &&
                    route.TargetEvaluations.Single()
                        .OpenPreparedSoilSlotCount == 1) &&
                targetDateFacilityShop.FacilityCapacityAxisStatus ==
                    "resolved_facility_capacity_not_required" &&
                carriedLocationShopJson == sourceLocationShopJson,
            "Target-date facility-capacity axis resolution drifted.");

        var tamperedTargetDateLocation = JsonNode.Parse(
            File.ReadAllText(targetDateLocationPath))!.AsObject();
        tamperedTargetDateLocation["routes"]![0]!["upstream_route"]!
            ["upstream_route"]!["source_id"] = "tampered";
        File.WriteAllText(
            tamperedTargetDateLocationPath,
            tamperedTargetDateLocation.ToJsonString(JsonDefaults.Options));
        var tamperedTargetDateLocationRejected = false;
        try
        {
            _ = AcquisitionRouteTargetDateFacilityBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockPath,
                targetDateFestivalPath,
                tamperedTargetDateLocationPath,
                targetDateUnlockSnapshotPath,
                targetDateRouteCalibrationPath);
        }
        catch (InvalidDataException)
        {
            tamperedTargetDateLocationRejected = true;
        }
        Require(tamperedTargetDateLocationRejected,
            "A tampered target-date location report did not fail closed.");

        var missingFacilitySnapshot = JsonNode.Parse(
            File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
        var missingFacilityFarm = missingFacilitySnapshot["state"]!
            ["locations"]!["social_route_date_evidence"]!["value"]!
            ["locations"]!.AsArray()
            .Single(row => row!["location_id"]!.GetValue<string>() == "Farm")!;
        missingFacilityFarm.AsObject().Remove("cultivation_capacity");
        File.WriteAllText(
            missingFacilitySnapshotPath,
            missingFacilitySnapshot.ToJsonString(JsonDefaults.Options));
        var missingFacilityReport = BuildFacilityFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            missingFacilitySnapshotPath,
            targetDateRouteCalibrationPath,
            missingFacilityUnlockPath,
            missingFacilityFestivalPath,
            missingFacilityLocationPath);
        Require(missingFacilityReport.Status ==
                    "partial_target_date_facility_capacity_axis_blocks" &&
                missingFacilityReport.RouteOccurrenceInventoryComplete &&
                !missingFacilityReport.FacilityCapacityAxisResolutionComplete &&
                missingFacilityReport.FacilityCapacityAxisResolvedCount == 74 &&
                missingFacilityReport.FacilityCapacityMatchCount == 2 &&
                missingFacilityReport.FacilityCapacityMissCount == 0 &&
                missingFacilityReport.FacilityCapacityNotRequiredCount == 2 &&
                missingFacilityReport.NotApplicableUpstreamCount == 72 &&
                missingFacilityReport.BlockedFacilityEvidenceCount == 2 &&
                missingFacilityReport.Routes.Where(route =>
                    route.FacilityCapacityAxisStatus ==
                        "blocked_facility_capacity_evidence").All(route =>
                    route.BlockingReasons.Contains(
                        "Farm:prepared_cultivation_capacity_missing")),
            "Missing prepared-soil capacity did not fail closed per crop route.");

        var zeroFacilitySnapshot = JsonNode.Parse(
            File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
        var zeroFacilityCapacity = zeroFacilitySnapshot["state"]!
            ["locations"]!["social_route_date_evidence"]!["value"]!
            ["locations"]!.AsArray()
            .Single(row => row!["location_id"]!.GetValue<string>() == "Farm")!
            ["cultivation_capacity"]!.AsObject();
        zeroFacilityCapacity["total_prepared_soil_slot_count"] = 0;
        zeroFacilityCapacity["open_prepared_soil_slot_count"] = 0;
        File.WriteAllText(
            zeroFacilitySnapshotPath,
            zeroFacilitySnapshot.ToJsonString(JsonDefaults.Options));
        var zeroFacilityReport = BuildFacilityFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            zeroFacilitySnapshotPath,
            targetDateRouteCalibrationPath,
            zeroFacilityUnlockPath,
            zeroFacilityFestivalPath,
            zeroFacilityLocationPath);
        Require(zeroFacilityReport.Status ==
                    "complete_target_date_facility_capacity_axis_downstream_pending" &&
                zeroFacilityReport.FacilityCapacityAxisResolutionComplete &&
                zeroFacilityReport.FacilityCapacityAxisResolvedCount == 76 &&
                zeroFacilityReport.FacilityCapacityMatchCount == 2 &&
                zeroFacilityReport.FacilityCapacityMissCount == 2 &&
                zeroFacilityReport.FacilityCapacityNotRequiredCount == 2 &&
                zeroFacilityReport.BlockedFacilityEvidenceCount == 0 &&
                zeroFacilityReport.Routes.Where(route =>
                    route.FacilityCapacityMatchesTargetDate == false).All(route =>
                    route.FacilityCapacityAxisStatus ==
                        "resolved_prepared_cultivation_capacity_miss" &&
                    route.BlockingReasons.Length == 0),
            "A complete zero-capacity fact was misclassified as missing evidence.");

        zeroFacilityCapacity["total_prepared_soil_slot_count"] = 1;
        zeroFacilityCapacity["occupied_crop_slot_count"] = 1;
        zeroFacilityCapacity["unresolved_harvest_item_slot_count"] = 1;
        zeroFacilityCapacity["occupied_harvest_items"] = new JsonArray(
            new JsonObject
            {
                ["harvest_item_qualified_id"] = string.Empty,
                ["is_garden_pot"] = false,
                ["slot_count"] = 1
            });
        File.WriteAllText(
            zeroFacilitySnapshotPath,
            zeroFacilitySnapshot.ToJsonString(JsonDefaults.Options));
        var unresolvedCropIdentityReport = BuildFacilityFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            zeroFacilitySnapshotPath,
            targetDateRouteCalibrationPath,
            zeroFacilityUnlockPath,
            zeroFacilityFestivalPath,
            zeroFacilityLocationPath);
        Require(unresolvedCropIdentityReport.Status ==
                    "partial_target_date_facility_capacity_axis_blocks" &&
                unresolvedCropIdentityReport.FacilityCapacityAxisResolvedCount ==
                    74 &&
                unresolvedCropIdentityReport.FacilityCapacityMatchCount == 2 &&
                unresolvedCropIdentityReport.BlockedFacilityEvidenceCount == 2 &&
                unresolvedCropIdentityReport.Routes.Where(route =>
                    route.FacilityCapacityAxisStatus ==
                        "blocked_facility_capacity_evidence").All(route =>
                    route.BlockingReasons.Contains(
                        "Farm:prepared_crop_harvest_identity_unresolved")),
            "An unresolved occupied crop identity was treated as a capacity miss.");

        var targetDateResource =
            AcquisitionRouteTargetDateResourceBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockPath,
                targetDateFestivalPath,
                targetDateLocationPath,
                targetDateFacilityPath,
                targetDateUnlockSnapshotPath,
                targetDateRouteCalibrationPath);
        Write(targetDateResourcePath, targetDateResource);
        var targetDateResourceCrop = targetDateResource.Routes.Single(route =>
            route.RouteOccurrenceId ==
                "full_shipment:full_shipment:item:24:0:0");
        var targetDateResourceShop = targetDateResource.Routes.Single(route =>
            route.RouteOccurrenceId ==
                "community_center_standard:community_center:bundle:Pantry/5:0:1");
        var targetDateResourceFish = targetDateResource.Routes.Single(route =>
            route.RouteOccurrenceId ==
                "master_angler:master_angler:item:145:0:0");
        var sourceFacilityCrop = targetDateFacility.Routes.Single(route =>
            route.RouteOccurrenceId == targetDateResourceCrop.RouteOccurrenceId);
        Require(targetDateResource.Status ==
                    "complete_target_date_resource_inputs_axis_downstream_pending" &&
                targetDateResource.RouteOccurrenceInventoryComplete &&
                targetDateResource.ResourceInputAxisResolutionComplete &&
                !targetDateResource.TrainingLabelEligible &&
                targetDateResource.TargetTotalDay == 0 &&
                targetDateResource.StaticCalendarResolutionSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(routeCalendarPath) &&
                targetDateResource.RouteOccurrenceCount == 76 &&
                targetDateResource.ResourceInputAxisResolvedCount == 76 &&
                targetDateResource.ResourceInputMatchCount == 4 &&
                targetDateResource.ResourceInputMissCount == 0 &&
                targetDateResource.ResourceInputNotRequiredCount == 1 &&
                targetDateResource.NotApplicableUpstreamCount == 72 &&
                targetDateResource.BlockedUpstreamCount == 0 &&
                targetDateResource.BlockedResourceEvidenceCount == 0 &&
                targetDateResourceCrop.ResourceRequirementKind ==
                    "crop_seed_or_existing_crop" &&
                targetDateResourceCrop.InputEvaluations.Single()
                    .QualifiedItemId == "(O)472" &&
                targetDateResourceCrop.InputEvaluations.Single()
                    .AvailableQuantity == 1 &&
                targetDateResourceShop.ResourceRequirementKind ==
                    "shop_trade_item_or_currency_only" &&
                targetDateResourceShop.InputEvaluations.Single()
                    .QualifiedItemId == "(O)388" &&
                targetDateResourceShop.InputEvaluations.Single()
                    .RequiredQuantity == 5 &&
                targetDateResourceShop.InputEvaluations.Single()
                    .AvailableQuantity == 5 &&
                targetDateResourceFish.ResourceInputAxisStatus ==
                    "resolved_resource_inputs_not_required" &&
                JsonSerializer.Serialize(
                    targetDateResourceCrop.UpstreamRoute,
                    JsonDefaults.Options) == JsonSerializer.Serialize(
                    sourceFacilityCrop,
                    JsonDefaults.Options),
            "Target-date resource-input axis resolution drifted.");

        var targetDateCurrency =
            AcquisitionRouteTargetDateCurrencyBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockPath,
                targetDateFestivalPath,
                targetDateLocationPath,
                targetDateFacilityPath,
                targetDateResourcePath,
                targetDateUnlockSnapshotPath,
                targetDateRouteCalibrationPath);
        Write(targetDateCurrencyPath, targetDateCurrency);
        var targetDateCurrencyShop = targetDateCurrency.Routes.Single(route =>
            route.RouteOccurrenceId == targetDateResourceShop.RouteOccurrenceId);
        Require(targetDateCurrency.Status ==
                    "complete_target_date_currency_budget_axis_downstream_pending" &&
                targetDateCurrency.RouteOccurrenceInventoryComplete &&
                targetDateCurrency.CurrencyAxisResolutionComplete &&
                !targetDateCurrency.TrainingLabelEligible &&
                targetDateCurrency.RouteOccurrenceCount == 76 &&
                targetDateCurrency.CurrencyAxisResolvedCount == 76 &&
                targetDateCurrency.CurrencyBudgetMatchCount == 4 &&
                targetDateCurrency.CurrencyBudgetMissCount == 0 &&
                targetDateCurrency.CurrencyNotRequiredCount == 3 &&
                targetDateCurrency.NotApplicableUpstreamCount == 72 &&
                targetDateCurrency.BlockedUpstreamCount == 0 &&
                targetDateCurrency.BlockedCurrencyEvidenceCount == 0 &&
                targetDateCurrencyShop.CurrencyRequirementKind ==
                    "current_native_shop_purchase_quote" &&
                targetDateCurrencyShop.CurrencyEvaluation is not null &&
                targetDateCurrencyShop.CurrencyEvaluation.CurrencyId == 0 &&
                targetDateCurrencyShop.CurrencyEvaluation.CurrencyKey ==
                    "money" &&
                targetDateCurrencyShop.CurrencyEvaluation.RequiredAmount == 100 &&
                targetDateCurrencyShop.CurrencyEvaluation.AvailableAmount == 500 &&
                targetDateCurrencyShop.CurrencyEvaluation.PurchaseQuoteStatus ==
                    "current_native_shop_quote_available" &&
                targetDateCurrency.StaticCalendarResolutionSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(routeCalendarPath) &&
                targetDateCurrency.AcquisitionLoweringSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(loweringPath) &&
                JsonSerializer.Serialize(
                    targetDateCurrencyShop.UpstreamRoute,
                    JsonDefaults.Options) == JsonSerializer.Serialize(
                    targetDateResourceShop,
                    JsonDefaults.Options),
            "Target-date currency-budget axis resolution drifted.");

        WriteEmptyStrategyLedger(strategyLedgerPath, "target-date-unlock-state");
        AcquisitionRouteTargetDateReservationReport BuildReservation(
            string ledgerPath) =>
            AcquisitionRouteTargetDateReservationBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockPath,
                targetDateFestivalPath,
                targetDateLocationPath,
                targetDateFacilityPath,
                targetDateResourcePath,
                targetDateCurrencyPath,
                ledgerPath,
                targetDateUnlockSnapshotPath,
                targetDateRouteCalibrationPath);
        var targetDateReservation = BuildReservation(strategyLedgerPath);
        Write(targetDateReservationPath, targetDateReservation);
        var targetDateReservationShop = targetDateReservation.Routes.Single(
            route => route.RouteOccurrenceId ==
                targetDateCurrencyShop.RouteOccurrenceId);
        Require(targetDateReservation.Status ==
                    "complete_target_date_inventory_reservation_axis_downstream_pending" &&
                targetDateReservation.RouteOccurrenceInventoryComplete &&
                targetDateReservation.ReservationAxisResolutionComplete &&
                !targetDateReservation.TrainingLabelEligible &&
                targetDateReservation.RouteOccurrenceCount == 76 &&
                targetDateReservation.ReservationAxisResolvedCount == 76 &&
                targetDateReservation.ReservationMatchCount == 4 &&
                targetDateReservation.ReservationConflictCount == 0 &&
                targetDateReservation.ReservationNotRequiredCount == 1 &&
                targetDateReservation.ClaimProposedCount == 3 &&
                targetDateReservation.ClaimCommittedCount == 0 &&
                targetDateReservation.ClaimReplacementCount == 0 &&
                targetDateReservation.MaterialClaimCount == 3 &&
                targetDateReservation.CurrencyClaimCount == 1 &&
                targetDateReservation.NotApplicableUpstreamCount == 72 &&
                targetDateReservation.BlockedUpstreamCount == 0 &&
                targetDateReservation.BlockedReservationEvidenceCount == 0 &&
                targetDateReservation.StrategyLedgerRevision == 0 &&
                targetDateReservation.StrategyLedgerSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(strategyLedgerPath) &&
                targetDateReservationShop.ClaimDisposition == "claim_proposed" &&
                targetDateReservationShop.ClaimSet is not null &&
                targetDateReservationShop.ClaimSet.AtomicCommitRequired &&
                !targetDateReservationShop.ClaimSet.ReplacementRequired &&
                targetDateReservationShop.ClaimSet.ExpectedLedgerRevision == 0 &&
                targetDateReservationShop.ClaimSet.MaterialClaims.Single()
                    .QualifiedItemId == "(O)388" &&
                targetDateReservationShop.ClaimSet.MaterialClaims.Single()
                    .NodeId == "player:1" &&
                targetDateReservationShop.ClaimSet.MaterialClaims.Single()
                    .SlotIndex == 1 &&
                targetDateReservationShop.ClaimSet.MaterialClaims.Single()
                    .Quantity == 5 &&
                targetDateReservationShop.ClaimSet.CurrencyClaims.Single()
                    .CurrencyId == NativeShopCurrencies.Money &&
                targetDateReservationShop.ClaimSet.CurrencyClaims.Single()
                    .Amount == 100,
            "Target-date inventory-reservation axis resolution drifted.");
        Require(JsonSerializer.Serialize(
                    targetDateReservation,
                    JsonDefaults.Options) == JsonSerializer.Serialize(
                    BuildReservation(strategyLedgerPath),
                    JsonDefaults.Options),
            "Target-date reservation claims are not deterministic.");

        AcquisitionRouteTargetDateProcessingReport BuildProcessing(
            string reservationPath,
            string ledgerPath,
            string snapshotPath) =>
            AcquisitionRouteTargetDateProcessingBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockPath,
                targetDateFestivalPath,
                targetDateLocationPath,
                targetDateFacilityPath,
                targetDateResourcePath,
                targetDateCurrencyPath,
                reservationPath,
                ledgerPath,
                snapshotPath,
                targetDateRouteCalibrationPath);
        var targetDateProcessing = BuildProcessing(
            targetDateReservationPath,
            strategyLedgerPath,
            targetDateUnlockSnapshotPath);
        Write(targetDateProcessingPath, targetDateProcessing);
        var targetDateProcessingCrops = targetDateProcessing.Routes.Where(
                route => route.ProcessingLeadTimeRequirementKind ==
                    "crop_growth_or_ready_crop")
            .ToArray();
        var targetDateProcessingShop = targetDateProcessing.Routes.Single(
            route => route.RouteOccurrenceId ==
                targetDateCurrencyShop.RouteOccurrenceId);
        Require(targetDateProcessing.Status ==
                    "complete_target_date_processing_lead_time_axis_downstream_pending" &&
                targetDateProcessing.RouteOccurrenceInventoryComplete &&
                targetDateProcessing
                    .ProcessingLeadTimeAxisResolutionComplete &&
                !targetDateProcessing.TrainingLabelEligible &&
                targetDateProcessing.RouteOccurrenceCount == 76 &&
                targetDateProcessing.ProcessingLeadTimeAxisResolvedCount == 76 &&
                targetDateProcessing.ProcessingLeadTimeMatchCount == 2 &&
                targetDateProcessing.ProcessingLeadTimeMissCount == 2 &&
                targetDateProcessing.ProcessingLeadTimeNotRequiredCount == 2 &&
                targetDateProcessing.NotApplicableUpstreamCount == 72 &&
                targetDateProcessing.BlockedUpstreamCount == 0 &&
                targetDateProcessing.BlockedProcessingEvidenceCount == 0 &&
                targetDateProcessingCrops.Length == 2 &&
                targetDateProcessingCrops.All(route =>
                    route.ProcessingLeadTimeMatchesTargetDate == false &&
                    route.Evaluations.Single().ProductionStateKind ==
                        "new_crop_from_seed" &&
                    route.Evaluations.Single().AuthoritativeBaseGrowthDays == 4 &&
                    route.Evaluations.Single()
                        .ProvenLeadTimeDaysLowerBound == 1 &&
                    route.Evaluations.Single()
                        .ProvenNotBeforeTotalDay == 1) &&
                targetDateProcessingShop.ProcessingLeadTimeAxisStatus ==
                    "resolved_processing_lead_time_not_required" &&
                JsonSerializer.Serialize(
                    targetDateProcessingShop.UpstreamRoute,
                    JsonDefaults.Options) == JsonSerializer.Serialize(
                    targetDateReservationShop,
                    JsonDefaults.Options),
            "Target-date processing-lead-time axis resolution drifted.");
        Require(JsonSerializer.Serialize(
                    targetDateProcessing,
                    JsonDefaults.Options) == JsonSerializer.Serialize(
                    BuildProcessing(
                        targetDateReservationPath,
                        strategyLedgerPath,
                        targetDateUnlockSnapshotPath),
                    JsonDefaults.Options),
            "Target-date processing-lead-time resolution is not deterministic.");

        JsonElement CrabPotNetwork(
            bool ready,
            bool readyStateConsistent,
            string currentOutput,
            string serviceStatus,
            string bait) => JsonSerializer.SerializeToElement(new
        {
            schema_version = "crab_pot_network.v1",
            projection_status =
                "complete_crab_pots_across_loaded_persistent_locations",
            rows = new[]
            {
                new
                {
                    location_id = "Beach",
                    exact_base_crab_pot = true,
                    production_domain_complete = true,
                    ready_state_consistent = readyStateConsistent,
                    ready_for_harvest = ready,
                    current_output_qualified_item_id = currentOutput,
                    service_status = serviceStatus,
                    bait_qualified_item_id = bait,
                    owner_has_luremaster = false,
                    possible_qualified_item_ids = new[] { "(O)715" }
                }
            }
        }, JsonDefaults.Options);
        var readyCrabPot = AcquisitionCrabPotLeadTimeEvaluator.Evaluate(
            CrabPotNetwork(
                ready: true,
                readyStateConsistent: true,
                currentOutput: "(O)715",
                serviceStatus: "ready_for_collection",
                bait: string.Empty),
            new[] { "Beach" },
            "(O)715",
            0);
        var waitingCrabPot = AcquisitionCrabPotLeadTimeEvaluator.Evaluate(
            CrabPotNetwork(
                ready: false,
                readyStateConsistent: true,
                currentOutput: string.Empty,
                serviceStatus: "producing_or_waiting",
                bait: "(O)685"),
            new[] { "Beach" },
            "(O)715",
            0);
        var inconsistentCrabPot =
            AcquisitionCrabPotLeadTimeEvaluator.Evaluate(
                CrabPotNetwork(
                    ready: true,
                    readyStateConsistent: false,
                    currentOutput: "(O)715",
                    serviceStatus: "ready_for_collection",
                    bait: string.Empty),
                new[] { "Beach" },
                "(O)715",
                0);
        var inconsistentServiceCrabPot =
            AcquisitionCrabPotLeadTimeEvaluator.Evaluate(
                CrabPotNetwork(
                    ready: false,
                    readyStateConsistent: true,
                    currentOutput: string.Empty,
                    serviceStatus: "bait_required",
                    bait: "(O)685"),
                new[] { "Beach" },
                "(O)715",
                0);
        Require(readyCrabPot.EvidenceAvailable &&
                readyCrabPot.Evaluations.Single()
                    .OutputReadyOnTargetDate == true &&
                readyCrabPot.Evaluations.Single()
                    .ProvenLeadTimeDaysLowerBound == 0 &&
                waitingCrabPot.EvidenceAvailable &&
                waitingCrabPot.Evaluations.Single()
                    .OutputReadyOnTargetDate == false &&
                waitingCrabPot.Evaluations.Single()
                    .ProvenNotBeforeTotalDay == 1 &&
                !inconsistentCrabPot.EvidenceAvailable &&
                inconsistentCrabPot.BlockingReasons.Contains(
                    "crab_pot_runtime_row_invalid_or_incomplete") &&
                !inconsistentServiceCrabPot.EvidenceAvailable &&
                inconsistentServiceCrabPot.BlockingReasons.Contains(
                    "crab_pot_service_state_does_not_prove_next_production"),
            "Native crab-pot lead-time classification drifted.");

        var readyCropSnapshot = JsonNode.Parse(
            File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
        readyCropSnapshot["state_hash"] = "target-date-ready-crop-state";
        var readyCropCapacity = readyCropSnapshot["state"]!["locations"]!
            ["social_route_date_evidence"]!["value"]!["locations"]!.AsArray()
            .Single(row => row!["location_id"]!.GetValue<string>() == "Farm")!
            ["cultivation_capacity"]!.AsObject();
        readyCropCapacity["total_prepared_soil_slot_count"] = 1;
        readyCropCapacity["open_prepared_soil_slot_count"] = 0;
        readyCropCapacity["occupied_crop_slot_count"] = 1;
        readyCropCapacity["unresolved_harvest_item_slot_count"] = 0;
        readyCropCapacity["occupied_harvest_items"] = new JsonArray(
            new JsonObject
            {
                ["harvest_item_qualified_id"] = "(O)24",
                ["is_garden_pot"] = false,
                ["slot_count"] = 1
            });
        readyCropSnapshot["state"]!["farm"]!["crops"]!["value"] =
            new JsonArray(
                new JsonObject
                {
                    ["location_id"] = "Farm",
                    ["tile_x"] = 1,
                    ["tile_y"] = 1,
                    ["harvest_item_qualified_id"] = "(O)24",
                    ["harvest_item_projection_status"] =
                        "exact_from_live_index_of_harvest",
                    ["dead"] = false,
                    ["ready_for_harvest"] = true,
                    ["days_until_next_harvest_if_watered"] = 0
                });
        var readyCropRoot = Path.Combine(root, "ready-crop-processing");
        Directory.CreateDirectory(readyCropRoot);
        var readyCropSnapshotPath = Path.Combine(
            readyCropRoot,
            "snapshot.json");
        var readyCropLedgerPath = Path.Combine(
            readyCropRoot,
            "strategy-ledger.json");
        File.WriteAllText(
            readyCropSnapshotPath,
            readyCropSnapshot.ToJsonString(JsonDefaults.Options));
        WriteEmptyStrategyLedger(
            readyCropLedgerPath,
            "target-date-ready-crop-state");
        var readyCropProcessing = BuildProcessingFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            readyCropSnapshotPath,
            targetDateRouteCalibrationPath,
            readyCropLedgerPath,
            readyCropRoot);
        Require(readyCropProcessing.ProcessingLeadTimeAxisResolutionComplete &&
                readyCropProcessing.ProcessingLeadTimeMatchCount == 4 &&
                readyCropProcessing.ProcessingLeadTimeMissCount == 0 &&
                readyCropProcessing.Routes.Where(route =>
                    route.ProcessingLeadTimeRequirementKind ==
                        "crop_growth_or_ready_crop").All(route =>
                    route.ProcessingLeadTimeMatchesTargetDate == true &&
                    route.Evaluations.Single().Status ==
                        "resolved_existing_crop_ready_on_target_date"),
            "A harvest-ready live crop did not satisfy same-day lead time.");

        var missingLiveCropSnapshot = JsonNode.Parse(
            readyCropSnapshot.ToJsonString())!.AsObject();
        missingLiveCropSnapshot["state_hash"] =
            "target-date-missing-live-crop-state";
        missingLiveCropSnapshot["state"]!["farm"]!.AsObject()
            .Remove("crops");
        var missingLiveCropRoot = Path.Combine(
            root,
            "missing-live-crop-processing");
        Directory.CreateDirectory(missingLiveCropRoot);
        var missingLiveCropSnapshotPath = Path.Combine(
            missingLiveCropRoot,
            "snapshot.json");
        var missingLiveCropLedgerPath = Path.Combine(
            missingLiveCropRoot,
            "strategy-ledger.json");
        File.WriteAllText(
            missingLiveCropSnapshotPath,
            missingLiveCropSnapshot.ToJsonString(JsonDefaults.Options));
        WriteEmptyStrategyLedger(
            missingLiveCropLedgerPath,
            "target-date-missing-live-crop-state");
        var missingLiveCropProcessing = BuildProcessingFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            missingLiveCropSnapshotPath,
            targetDateRouteCalibrationPath,
            missingLiveCropLedgerPath,
            missingLiveCropRoot);
        Require(missingLiveCropProcessing.Status ==
                    "partial_target_date_processing_lead_time_axis_blocks" &&
                !missingLiveCropProcessing
                    .ProcessingLeadTimeAxisResolutionComplete &&
                missingLiveCropProcessing
                    .ProcessingLeadTimeAxisResolvedCount == 74 &&
                missingLiveCropProcessing.ProcessingLeadTimeMatchCount == 2 &&
                missingLiveCropProcessing.ProcessingLeadTimeMissCount == 0 &&
                missingLiveCropProcessing.BlockedProcessingEvidenceCount == 2 &&
                missingLiveCropProcessing.Routes.Where(route =>
                    route.ProcessingLeadTimeAxisStatus ==
                        "blocked_processing_lead_time_evidence").All(route =>
                    route.BlockingReasons.Contains(
                        "state.farm.crops.value:missing_or_unavailable")),
            "Missing per-tile crop state did not fail closed.");

        File.Copy(
            targetDateReservationPath,
            tamperedTargetDateReservationPath,
            overwrite: true);
        var tamperedReservation = JsonNode.Parse(File.ReadAllText(
            tamperedTargetDateReservationPath))!.AsObject();
        tamperedReservation["routes"]![0]!["claim_disposition"] =
            "tampered";
        File.WriteAllText(
            tamperedTargetDateReservationPath,
            tamperedReservation.ToJsonString(JsonDefaults.Options));
        var tamperedReservationRejected = false;
        try
        {
            _ = BuildProcessing(
                tamperedTargetDateReservationPath,
                strategyLedgerPath,
                targetDateUnlockSnapshotPath);
        }
        catch (InvalidDataException)
        {
            tamperedReservationRejected = true;
        }
        Require(tamperedReservationRejected,
            "A tampered inventory-reservation report was accepted.");

        StrategyCommitmentLedger FixtureLedger(int revision) => new()
        {
            LedgerId = "strategy-ledger:fixture-save:1",
            SaveId = "fixture-save",
            PlayerId = "1",
            Revision = revision,
            UpdatedAt = "2026-09-10T00:00:00Z",
            SourceStateHash = "target-date-unlock-state"
        };
        var materialReservedLedger = FixtureLedger(1);
        materialReservedLedger.MaterialReservations = new[]
        {
            new MaterialReservation
            {
                ReservationId = "other-route-material",
                Revision = 1,
                Status = StrategyCommitmentStatuses.Active,
                SourceDecisionId = "other-route",
                SourceStateHash = "earlier-state",
                GoalId = "grandpa_score_21",
                OwnerPlayerId = 1,
                NodeId = "player:1",
                SlotIndex = 1,
                QualifiedItemId = "(O)388",
                Quantity = 1,
                Purpose = "reserve another route"
            }
        };
        Write(materialReservedLedgerPath, materialReservedLedger);
        var materialReservedReport =
            BuildReservation(materialReservedLedgerPath);
        var materialConflict = materialReservedReport.Routes.Single(route =>
            route.InventoryReservationMatchesTargetDate == false);
        Require(materialReservedReport.ReservationAxisResolutionComplete &&
                materialReservedReport.ReservationMatchCount == 3 &&
                materialReservedReport.ReservationConflictCount == 1 &&
                materialReservedReport.ClaimProposedCount == 2 &&
                materialReservedReport.MaterialClaimCount == 2 &&
                materialReservedReport.CurrencyClaimCount == 0 &&
                materialConflict.RouteOccurrenceId ==
                    targetDateCurrencyShop.RouteOccurrenceId &&
                materialConflict.NonMatchingReasons.Contains(
                    "unreserved_material_quantity_unavailable:(O)388"),
            "An active material reservation did not prevent cross-route spending.");
        materialReservedLedger.MaterialReservations[0].Quantity = 6;
        Write(materialReservedLedgerPath, materialReservedLedger);
        var overbookedMaterialLedgerRejected = false;
        try
        {
            _ = BuildReservation(materialReservedLedgerPath);
        }
        catch (InvalidDataException)
        {
            overbookedMaterialLedgerRejected = true;
        }
        Require(overbookedMaterialLedgerRejected,
            "A globally overbooked material ledger was accepted.");

        var currencyReservedLedger = FixtureLedger(1);
        currencyReservedLedger.CurrencyReservations = new[]
        {
            new CurrencyReservation
            {
                ReservationId = "other-route-currency",
                Revision = 1,
                Status = StrategyCommitmentStatuses.Active,
                SourceDecisionId = "other-route",
                SourceStateHash = "earlier-state",
                GoalId = "grandpa_score_21",
                OwnerPlayerId = 1,
                CurrencyId = NativeShopCurrencies.Money,
                CurrencyKey = "money",
                Amount = 450,
                Purpose = "reserve another route"
            }
        };
        Write(currencyReservedLedgerPath, currencyReservedLedger);
        var currencyReservedReport =
            BuildReservation(currencyReservedLedgerPath);
        var currencyConflict = currencyReservedReport.Routes.Single(route =>
            route.InventoryReservationMatchesTargetDate == false);
        Require(currencyReservedReport.ReservationAxisResolutionComplete &&
                currencyReservedReport.ReservationMatchCount == 3 &&
                currencyReservedReport.ReservationConflictCount == 1 &&
                currencyReservedReport.ClaimProposedCount == 2 &&
                currencyReservedReport.MaterialClaimCount == 2 &&
                currencyReservedReport.CurrencyClaimCount == 0 &&
                currencyConflict.RouteOccurrenceId ==
                    targetDateCurrencyShop.RouteOccurrenceId &&
                currencyConflict.NonMatchingReasons.Contains(
                    "unreserved_currency_amount_unavailable:money"),
            "An active currency reservation did not prevent cross-route spending.");
        currencyReservedLedger.CurrencyReservations[0].Amount = 501;
        Write(currencyReservedLedgerPath, currencyReservedLedger);
        var overbookedCurrencyLedgerRejected = false;
        try
        {
            _ = BuildReservation(currencyReservedLedgerPath);
        }
        catch (InvalidDataException)
        {
            overbookedCurrencyLedgerRejected = true;
        }
        Require(overbookedCurrencyLedgerRejected,
            "A globally overbooked currency ledger was accepted.");

        var cancelledLedger = FixtureLedger(1);
        cancelledLedger.MaterialReservations = new[]
        {
            new MaterialReservation
            {
                ReservationId = "cancelled-material",
                Revision = 2,
                Status = StrategyCommitmentStatuses.Cancelled,
                SourceDecisionId = "other-route",
                SourceStateHash = "earlier-state",
                GoalId = "grandpa_score_21",
                OwnerPlayerId = 1,
                NodeId = "player:1",
                SlotIndex = 1,
                QualifiedItemId = "(O)388",
                Quantity = 5,
                Purpose = "reserve another route",
                CancelReason = "route_replanned"
            }
        };
        cancelledLedger.CurrencyReservations = new[]
        {
            new CurrencyReservation
            {
                ReservationId = "cancelled-currency",
                Revision = 2,
                Status = StrategyCommitmentStatuses.Cancelled,
                SourceDecisionId = "other-route",
                SourceStateHash = "earlier-state",
                GoalId = "grandpa_score_21",
                OwnerPlayerId = 1,
                CurrencyId = NativeShopCurrencies.Money,
                CurrencyKey = "money",
                Amount = 500,
                Purpose = "reserve another route",
                CancelReason = "route_replanned"
            }
        };
        Write(cancelledReservationLedgerPath, cancelledLedger);
        var cancelledReservationReport =
            BuildReservation(cancelledReservationLedgerPath);
        Require(cancelledReservationReport.ReservationMatchCount == 4 &&
                cancelledReservationReport.ReservationConflictCount == 0 &&
                cancelledReservationReport.ClaimProposedCount == 3,
            "Cancelled reservations incorrectly reduced available supply.");

        var proposedShopClaims = targetDateReservationShop.ClaimSet!;
        var committedLedger = FixtureLedger(2);
        committedLedger.MaterialReservations = proposedShopClaims.MaterialClaims
            .Select(claim => new MaterialReservation
            {
                ReservationId = claim.ReservationId,
                Revision = 1,
                Status = StrategyCommitmentStatuses.Active,
                SourceDecisionId = claim.SourceDecisionId,
                SourceStateHash = claim.StateHash,
                GoalId = claim.GoalId,
                OwnerPlayerId = 1,
                NodeId = claim.NodeId,
                SlotIndex = claim.SlotIndex,
                QualifiedItemId = claim.QualifiedItemId,
                Quantity = claim.Quantity,
                Purpose = claim.Purpose
            }).ToArray();
        committedLedger.CurrencyReservations = proposedShopClaims.CurrencyClaims
            .Select(claim => new CurrencyReservation
            {
                ReservationId = claim.ReservationId,
                Revision = 1,
                Status = StrategyCommitmentStatuses.Active,
                SourceDecisionId = claim.SourceDecisionId,
                SourceStateHash = claim.StateHash,
                GoalId = claim.GoalId,
                OwnerPlayerId = 1,
                CurrencyId = claim.CurrencyId,
                CurrencyKey = "money",
                Amount = claim.Amount,
                Purpose = claim.Purpose
            }).ToArray();
        Write(committedReservationLedgerPath, committedLedger);
        var committedReservationReport =
            BuildReservation(committedReservationLedgerPath);
        var committedShop = committedReservationReport.Routes.Single(route =>
            route.RouteOccurrenceId == targetDateCurrencyShop.RouteOccurrenceId);
        Require(committedReservationReport.ReservationMatchCount == 4 &&
                committedReservationReport.ClaimProposedCount == 2 &&
                committedReservationReport.ClaimCommittedCount == 1 &&
                committedShop.ClaimDisposition == "claim_already_committed" &&
                committedShop.ClaimSet is not null &&
                !committedShop.ClaimSet.ReplacementRequired &&
                committedShop.ClaimSet.ExistingActiveReservationIds.Length == 2,
            "An exact committed route claim was not recognized idempotently.");

        var mismatchedLedger = FixtureLedger(0);
        mismatchedLedger.PlayerId = "2";
        Write(mismatchedStrategyLedgerPath, mismatchedLedger);
        var mismatchedLedgerRejected = false;
        try
        {
            _ = BuildReservation(mismatchedStrategyLedgerPath);
        }
        catch (InvalidDataException)
        {
            mismatchedLedgerRejected = true;
        }
        Require(mismatchedLedgerRejected,
            "A strategy ledger for another player was accepted.");

        var tamperedTargetDateFacility = JsonNode.Parse(
            File.ReadAllText(targetDateFacilityPath))!.AsObject();
        tamperedTargetDateFacility["routes"]![0]!["upstream_route"]!
            ["upstream_route"]!["upstream_route"]!["source_id"] =
            "tampered";
        File.WriteAllText(
            tamperedTargetDateFacilityPath,
            tamperedTargetDateFacility.ToJsonString(JsonDefaults.Options));
        var tamperedTargetDateFacilityRejected = false;
        try
        {
            _ = AcquisitionRouteTargetDateResourceBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockPath,
                targetDateFestivalPath,
                targetDateLocationPath,
                tamperedTargetDateFacilityPath,
                targetDateUnlockSnapshotPath,
                targetDateRouteCalibrationPath);
        }
        catch (InvalidDataException)
        {
            tamperedTargetDateFacilityRejected = true;
        }
        Require(tamperedTargetDateFacilityRejected,
            "A tampered target-date facility report was accepted.");

        var missingResourceSnapshot = JsonNode.Parse(
            File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
        missingResourceSnapshot["state"]!["farm"]!.AsObject()
            .Remove("material_inventory_graph");
        File.WriteAllText(
            missingResourceSnapshotPath,
            missingResourceSnapshot.ToJsonString(JsonDefaults.Options));
        var missingResourceReport = BuildResourceFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            missingResourceSnapshotPath,
            targetDateRouteCalibrationPath,
            missingResourceUnlockPath,
            missingResourceFestivalPath,
            missingResourceLocationPath,
            missingResourceFacilityPath);
        Require(missingResourceReport.Status ==
                    "partial_target_date_resource_inputs_axis_blocks" &&
                missingResourceReport.RouteOccurrenceInventoryComplete &&
                !missingResourceReport.ResourceInputAxisResolutionComplete &&
                missingResourceReport.ResourceInputAxisResolvedCount == 73 &&
                missingResourceReport.ResourceInputMatchCount == 1 &&
                missingResourceReport.ResourceInputMissCount == 0 &&
                missingResourceReport.ResourceInputNotRequiredCount == 1 &&
                missingResourceReport.NotApplicableUpstreamCount == 72 &&
                missingResourceReport.BlockedResourceEvidenceCount == 3 &&
                missingResourceReport.Routes.Where(route =>
                    route.ResourceInputAxisStatus ==
                        "blocked_resource_input_evidence").All(route =>
                    route.BlockingReasons.Contains(
                        "material_inventory_graph_missing_or_unavailable")),
            "Missing canonical material evidence did not fail closed per input route.");

        var insufficientSeedSnapshot = JsonNode.Parse(
            File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
        var insufficientSeedGraph = insufficientSeedSnapshot["state"]!["farm"]!
            ["material_inventory_graph"]!["value"]!;
        var insufficientSeedSlots = insufficientSeedGraph["inventory_nodes"]![0]!
            ["slots"]!.AsArray();
        insufficientSeedSlots.Remove(insufficientSeedSlots.Single(row =>
            row!["qualified_item_id"]!.GetValue<string>() == "(O)472"));
        var insufficientSeedQuantities = insufficientSeedGraph["quantity_rows"]!
            .AsArray();
        insufficientSeedQuantities.Remove(insufficientSeedQuantities.Single(row =>
            row!["qualified_item_id"]!.GetValue<string>() == "(O)472"));
        File.WriteAllText(
            insufficientSeedSnapshotPath,
            insufficientSeedSnapshot.ToJsonString(JsonDefaults.Options));
        var insufficientSeedReport = BuildResourceFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            insufficientSeedSnapshotPath,
            targetDateRouteCalibrationPath,
            insufficientSeedUnlockPath,
            insufficientSeedFestivalPath,
            insufficientSeedLocationPath,
            insufficientSeedFacilityPath);
        Require(insufficientSeedReport.Status ==
                    "complete_target_date_resource_inputs_axis_downstream_pending" &&
                insufficientSeedReport.ResourceInputAxisResolutionComplete &&
                insufficientSeedReport.ResourceInputAxisResolvedCount == 76 &&
                insufficientSeedReport.ResourceInputMatchCount == 2 &&
                insufficientSeedReport.ResourceInputMissCount == 2 &&
                insufficientSeedReport.ResourceInputNotRequiredCount == 1 &&
                insufficientSeedReport.BlockedResourceEvidenceCount == 0 &&
                insufficientSeedReport.Routes.Where(route =>
                    route.ResourceInputsMatchTargetDate == false).All(route =>
                    route.ResourceRequirementKind ==
                        "crop_seed_or_existing_crop" &&
                    route.NonMatchingReasons.Contains(
                        "required_resource_quantity_unavailable:(O)472")),
            "An exact zero-seed fact was not retained as a resolved resource miss.");

        var existingCropCapacity = insufficientSeedSnapshot["state"]!
            ["locations"]!["social_route_date_evidence"]!["value"]!
            ["locations"]!.AsArray()
            .Single(row => row!["location_id"]!.GetValue<string>() == "Farm")!
            ["cultivation_capacity"]!.AsObject();
        existingCropCapacity["total_prepared_soil_slot_count"] = 1;
        existingCropCapacity["open_prepared_soil_slot_count"] = 0;
        existingCropCapacity["occupied_crop_slot_count"] = 1;
        existingCropCapacity["unresolved_harvest_item_slot_count"] = 0;
        existingCropCapacity["occupied_harvest_items"] = new JsonArray(
            new JsonObject
            {
                ["harvest_item_qualified_id"] = "(O)24",
                ["is_garden_pot"] = false,
                ["slot_count"] = 1
            });
        File.WriteAllText(
            insufficientSeedSnapshotPath,
            insufficientSeedSnapshot.ToJsonString(JsonDefaults.Options));
        var existingCropReport = BuildResourceFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            insufficientSeedSnapshotPath,
            targetDateRouteCalibrationPath,
            insufficientSeedUnlockPath,
            insufficientSeedFestivalPath,
            insufficientSeedLocationPath,
            insufficientSeedFacilityPath);
        var existingCropRoutes = existingCropReport.Routes.Where(route =>
                route.ResourceRequirementKind ==
                    "crop_seed_or_existing_crop" &&
                route.ResourceInputsMatchTargetDate == true)
            .ToArray();
        Require(existingCropReport.ResourceInputAxisResolutionComplete &&
                existingCropReport.ResourceInputMatchCount == 4 &&
                existingCropReport.ResourceInputMissCount == 0 &&
                existingCropReport.ResourceInputNotRequiredCount == 3 &&
                existingCropRoutes.Length == 2 &&
                existingCropRoutes.All(route =>
                    route.InputEvaluations.Single().InputKind ==
                        "existing_target_crop" &&
                    route.InputEvaluations.Single().RequiredQuantity == 0 &&
                    route.InputEvaluations.Single().AvailableQuantity == 1),
            "An existing target crop incorrectly required a new seed.");

        var missingShopQuoteSnapshot = JsonNode.Parse(
            File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
        missingShopQuoteSnapshot["state"]!["locations"]!.AsObject()
            .Remove("shops");
        File.WriteAllText(
            missingShopQuoteSnapshotPath,
            missingShopQuoteSnapshot.ToJsonString(JsonDefaults.Options));
        var missingShopQuoteCurrency = BuildCurrencyFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            missingShopQuoteSnapshotPath,
            targetDateRouteCalibrationPath,
            currencyVariantUnlockPath,
            currencyVariantFestivalPath,
            currencyVariantLocationPath,
            currencyVariantFacilityPath,
            currencyVariantResourcePath);
        Require(missingShopQuoteCurrency.Status ==
                    "partial_target_date_currency_budget_axis_blocks" &&
                missingShopQuoteCurrency.CurrencyAxisResolvedCount == 75 &&
                missingShopQuoteCurrency.CurrencyBudgetMatchCount == 3 &&
                missingShopQuoteCurrency.CurrencyBudgetMissCount == 0 &&
                missingShopQuoteCurrency.CurrencyNotRequiredCount == 3 &&
                missingShopQuoteCurrency.NotApplicableUpstreamCount == 72 &&
                missingShopQuoteCurrency.BlockedUpstreamCount == 1 &&
                missingShopQuoteCurrency.BlockedCurrencyEvidenceCount == 0 &&
                missingShopQuoteCurrency.Routes.Single(route =>
                    route.CurrencyAxisStatus ==
                        "blocked_upstream_resource_input_axis")
                    .BlockingReasons.Contains(
                        "current_native_shop_quotes_missing_or_incomplete"),
            "Missing native shop quotes did not block the exact shop chain.");

        var missingCurrencySnapshot = JsonNode.Parse(
            File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
        missingCurrencySnapshot["state"]!["player"]!.AsObject()
            .Remove("shop_currency_balances");
        File.WriteAllText(
            missingCurrencySnapshotPath,
            missingCurrencySnapshot.ToJsonString(JsonDefaults.Options));
        var missingCurrencyReport = BuildCurrencyFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            missingCurrencySnapshotPath,
            targetDateRouteCalibrationPath,
            currencyVariantUnlockPath,
            currencyVariantFestivalPath,
            currencyVariantLocationPath,
            currencyVariantFacilityPath,
            currencyVariantResourcePath);
        Require(missingCurrencyReport.CurrencyAxisResolvedCount == 75 &&
                missingCurrencyReport.CurrencyBudgetMatchCount == 3 &&
                missingCurrencyReport.CurrencyNotRequiredCount == 3 &&
                missingCurrencyReport.NotApplicableUpstreamCount == 72 &&
                missingCurrencyReport.BlockedUpstreamCount == 0 &&
                missingCurrencyReport.BlockedCurrencyEvidenceCount == 1 &&
                missingCurrencyReport.Routes.Single(route =>
                    route.CurrencyAxisStatus ==
                        "blocked_currency_budget_evidence")
                    .BlockingReasons.Contains(
                        "shop_currency_balances_missing_or_incomplete"),
            "Missing native currency balances did not fail closed per shop route.");

        var insufficientCurrencySnapshot = JsonNode.Parse(
            File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
        insufficientCurrencySnapshot["state"]!["player"]!["money"]!["value"] =
            50;
        var currencyRows = insufficientCurrencySnapshot["state"]!["player"]!
            ["shop_currency_balances"]!["value"]!["rows"]!.AsArray();
        currencyRows.Single(row =>
            row!["currency_id"]!.GetValue<int>() == 0)!["balance"] = 50;
        File.WriteAllText(
            insufficientCurrencySnapshotPath,
            insufficientCurrencySnapshot.ToJsonString(JsonDefaults.Options));
        var insufficientCurrencyReport = BuildCurrencyFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            insufficientCurrencySnapshotPath,
            targetDateRouteCalibrationPath,
            currencyVariantUnlockPath,
            currencyVariantFestivalPath,
            currencyVariantLocationPath,
            currencyVariantFacilityPath,
            currencyVariantResourcePath);
        var insufficientCurrencyRoute = insufficientCurrencyReport.Routes
            .Single(route => route.CurrencyBudgetMatchesTargetDate == false);
        Require(insufficientCurrencyReport.CurrencyAxisResolutionComplete &&
                insufficientCurrencyReport.CurrencyAxisResolvedCount == 76 &&
                insufficientCurrencyReport.CurrencyBudgetMatchCount == 3 &&
                insufficientCurrencyReport.CurrencyBudgetMissCount == 1 &&
                insufficientCurrencyReport.CurrencyNotRequiredCount == 3 &&
                insufficientCurrencyReport.BlockedCurrencyEvidenceCount == 0 &&
                insufficientCurrencyRoute.CurrencyEvaluation is not null &&
                insufficientCurrencyRoute.CurrencyEvaluation.RequiredAmount ==
                    100 &&
                insufficientCurrencyRoute.CurrencyEvaluation.AvailableAmount ==
                    50 &&
                insufficientCurrencyRoute.NonMatchingReasons.Contains(
                    "required_currency_amount_unavailable:money"),
            "An exact insufficient-money fact was not retained as a currency miss.");

        var tamperedTargetDateFestival = JsonNode.Parse(
            File.ReadAllText(targetDateFestivalPath))!.AsObject();
        tamperedTargetDateFestival["routes"]![0]!["upstream_route"]!
            ["source_id"] = "tampered";
        File.WriteAllText(
            tamperedTargetDateFestivalPath,
            tamperedTargetDateFestival.ToJsonString(JsonDefaults.Options));
        var tamperedTargetDateFestivalRejected = false;
        try
        {
            _ = AcquisitionRouteTargetDateLocationBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                targetDateUnlockPath,
                tamperedTargetDateFestivalPath,
                targetDateUnlockSnapshotPath,
                targetDateRouteCalibrationPath);
        }
        catch (InvalidDataException)
        {
            tamperedTargetDateFestivalRejected = true;
        }
        Require(tamperedTargetDateFestivalRejected,
            "A tampered target-date festival report did not fail closed.");

        var missingRouteSnapshot = JsonNode.Parse(
            File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
        missingRouteSnapshot["state"]!["locations"]!.AsObject()
            .Remove("social_route_date_evidence");
        File.WriteAllText(
            missingRouteSnapshotPath,
            missingRouteSnapshot.ToJsonString(JsonDefaults.Options));
        var missingRouteUnlock = AcquisitionRouteTargetDateUnlockBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            missingRouteSnapshotPath);
        Write(missingRouteUnlockPath, missingRouteUnlock);
        var missingRouteFestival =
            AcquisitionRouteTargetDateFestivalBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                missingRouteUnlockPath,
                missingRouteSnapshotPath);
        Write(missingRouteFestivalPath, missingRouteFestival);
        var missingRouteReport =
            AcquisitionRouteTargetDateLocationBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                missingRouteUnlockPath,
                missingRouteFestivalPath,
                missingRouteSnapshotPath,
                targetDateRouteCalibrationPath);
        Require(missingRouteReport.Status ==
                    "partial_target_date_location_route_axis_blocks" &&
                missingRouteReport.RouteOccurrenceInventoryComplete &&
                !missingRouteReport.LocationRouteAxisResolutionComplete &&
                !missingRouteReport.TrainingLabelEligible &&
                missingRouteReport.LocationRouteAxisResolvedCount == 72 &&
                missingRouteReport.LocationRouteMatchCount == 0 &&
                missingRouteReport.LocationRouteMissCount == 0 &&
                missingRouteReport.NotApplicableStaticWindowCount == 72 &&
                missingRouteReport.BlockedLocationEvidenceCount == 4 &&
                missingRouteReport.Routes.Where(route =>
                    route.LocationRouteAxisStatus ==
                        "blocked_location_route_evidence").All(route =>
                    route.BlockingReasons.Contains(
                        "location_route_date_evidence_missing")),
            "Missing transparent route evidence did not fail closed per active route.");

        WriteStageOneCollectionSnapshot(
            deniedRouteSnapshotPath,
            "denied-route-state",
            fish,
            totalDays: 0,
            timeOfDay: 700,
            shopDoorAllowed: false);
        var deniedRouteUnlock = AcquisitionRouteTargetDateUnlockBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            deniedRouteSnapshotPath);
        Write(deniedRouteUnlockPath, deniedRouteUnlock);
        var deniedRouteFestival =
            AcquisitionRouteTargetDateFestivalBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                deniedRouteUnlockPath,
                deniedRouteSnapshotPath);
        Write(deniedRouteFestivalPath, deniedRouteFestival);
        var deniedRouteReport =
            AcquisitionRouteTargetDateLocationBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                deniedRouteUnlockPath,
                deniedRouteFestivalPath,
                deniedRouteSnapshotPath,
                targetDateRouteCalibrationPath);
        var deniedShop = deniedRouteReport.Routes.Single(route =>
            route.UpstreamRoute.UpstreamRoute.RequirementId ==
                "community_center:bundle:Pantry/5" &&
            route.UpstreamRoute.UpstreamRoute.RouteKind == "sells");
        Require(deniedRouteReport.Status ==
                    "complete_target_date_location_route_axis_downstream_pending" &&
                deniedRouteReport.LocationRouteAxisResolutionComplete &&
                deniedRouteReport.LocationRouteAxisResolvedCount == 76 &&
                deniedRouteReport.LocationRouteMatchCount == 3 &&
                deniedRouteReport.LocationRouteMissCount == 1 &&
                deniedRouteReport.BlockedLocationEvidenceCount == 0 &&
                deniedShop.LocationRouteAxisStatus ==
                    "resolved_location_route_miss" &&
                deniedShop.LocationRouteMatchesTargetDate == false &&
                deniedShop.TargetEvaluations.Single().Status ==
                    "resolved_route_unavailable_on_target_date" &&
                deniedShop.BlockingReasons.Length == 0,
            "A complete negative route fact was misclassified as missing evidence.");

        var missingCalendarSnapshot = JsonNode.Parse(
            File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
        missingCalendarSnapshot["state"]!["world_progress"]!.AsObject()
            .Remove("game_state_query_calendar_state");
        File.WriteAllText(
            missingCalendarStateSnapshotPath,
            missingCalendarSnapshot.ToJsonString(JsonDefaults.Options));
        var missingCalendarUnlock = AcquisitionRouteTargetDateUnlockBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            missingCalendarStateSnapshotPath);
        Write(missingCalendarStateUnlockPath, missingCalendarUnlock);
        var missingCalendarReport =
            AcquisitionRouteTargetDateFestivalBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                missingCalendarStateUnlockPath,
                missingCalendarStateSnapshotPath);
        Require(missingCalendarReport.Status ==
                    "partial_target_date_festival_axis_blocks" &&
                !missingCalendarReport.CalendarConditionAxisResolutionComplete &&
                missingCalendarReport.CalendarConditionAxisResolvedCount == 75 &&
                missingCalendarReport.CalendarConditionMatchCount == 3 &&
                missingCalendarReport.BlockedCalendarEvidenceCount == 1 &&
                missingCalendarReport.Routes.Single(route =>
                    route.UpstreamRoute.RequirementId ==
                        "community_center:bundle:Pantry/5" &&
                    route.UpstreamRoute.RouteKind == "sells").BlockingReasons
                    .Contains("game_state_query_calendar_state_missing"),
            "Missing transparent calendar state did not fail closed per route.");

        var tamperedTargetDateUnlock = JsonNode.Parse(
            File.ReadAllText(targetDateUnlockPath))!.AsObject();
        tamperedTargetDateUnlock["routes"]![0]!["source_id"] = "tampered";
        File.WriteAllText(
            tamperedTargetDateUnlockPath,
            tamperedTargetDateUnlock.ToJsonString(JsonDefaults.Options));
        var tamperedTargetDateUnlockRejected = false;
        try
        {
            _ = AcquisitionRouteTargetDateFestivalBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                tamperedTargetDateUnlockPath,
                targetDateUnlockSnapshotPath);
        }
        catch (InvalidDataException)
        {
            tamperedTargetDateUnlockRejected = true;
        }
        Require(tamperedTargetDateUnlockRejected,
            "A tampered target-date unlock report did not fail closed.");

        var missingUnlockSnapshot = JsonNode.Parse(
            File.ReadAllText(targetDateUnlockSnapshotPath))!.AsObject();
        missingUnlockSnapshot["state"]!["world_progress"]!.AsObject()
            .Remove("game_state_query_unlock_state");
        File.WriteAllText(
            missingUnlockStateSnapshotPath,
            missingUnlockSnapshot.ToJsonString(JsonDefaults.Options));
        var missingUnlockReport =
            AcquisitionRouteTargetDateUnlockBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                missingUnlockStateSnapshotPath);
        Require(missingUnlockReport.Status ==
                    "partial_target_date_unlock_axis_blocks" &&
                !missingUnlockReport.UnlockAxisResolutionComplete &&
                missingUnlockReport.BlockedUnlockEvidenceCount == 1 &&
                missingUnlockReport.UnlockStateMatchCount == 3 &&
                missingUnlockReport.Routes.Single(route =>
                    route.RequirementId ==
                        "community_center:bundle:Pantry/5" &&
                    route.RouteKind == "sells").BlockingReasons.Contains(
                        "game_state_query_unlock_state_missing"),
            "Missing transparent unlock state did not fail closed per route.");

        WriteStageOneCollectionSnapshot(
            mismatchedUnlockDateSnapshotPath,
            "mismatched-target-date-unlock-state",
            fish,
            totalDays: 1);
        var mismatchedUnlockDateRejected = false;
        try
        {
            _ = AcquisitionRouteTargetDateUnlockBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                targetDateCalendarPath,
                mismatchedUnlockDateSnapshotPath);
        }
        catch (InvalidDataException)
        {
            mismatchedUnlockDateRejected = true;
        }
        Require(mismatchedUnlockDateRejected,
            "A snapshot from a different day did not fail closed.");

        var tamperedTargetDatePath = Path.Combine(
            root,
            "tampered-target-date-calendar.json");
        var tamperedTargetDate = JsonNode.Parse(
            File.ReadAllText(targetDateCalendarPath))!.AsObject();
        tamperedTargetDate["routes"]![0]!["source_id"] = "tampered";
        File.WriteAllText(
            tamperedTargetDatePath,
            tamperedTargetDate.ToJsonString(JsonDefaults.Options));
        var tamperedTargetDateRejected = false;
        try
        {
            _ = AcquisitionRouteTargetDateUnlockBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                tamperedTargetDatePath,
                targetDateUnlockSnapshotPath);
        }
        catch (InvalidDataException)
        {
            tamperedTargetDateRejected = true;
        }
        Require(tamperedTargetDateRejected,
            "A tampered target-date calendar did not fail closed.");

        var secondYearCalendar = AcquisitionRouteTargetDateCalendarBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            112);
        var secondYearShop = secondYearCalendar.Routes.Single(route =>
            route.RequirementId == "community_center:bundle:Pantry/5" &&
            route.RouteKind == "sells");
        Require(secondYearCalendar.StaticWindowMatchCount == 4 &&
                secondYearShop.CalendarAxisResolved &&
                !secondYearShop.StaticWindowMatchesTargetDate &&
                secondYearShop.CalendarAxisStatus ==
                    "resolved_target_date_outside_static_window" &&
                secondYearShop.MatchingWindows.Length == 0 &&
                secondYearShop.PendingDynamicConditions.Length == 0,
            "Negated shop year condition did not reject the second year.");

        var tamperedRouteCalendarPath = Path.Combine(
            root,
            "tampered-route-calendar-resolution.json");
        var tamperedRouteCalendar = JsonNode.Parse(
            File.ReadAllText(routeCalendarPath))!.AsObject();
        tamperedRouteCalendar["routes"]![0]!["source_id"] = "tampered";
        File.WriteAllText(
            tamperedRouteCalendarPath,
            tamperedRouteCalendar.ToJsonString(JsonDefaults.Options));
        var tamperedRouteCalendarRejected = false;
        try
        {
            _ = AcquisitionRouteTargetDateCalendarBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                tamperedRouteCalendarPath,
                0);
        }
        catch (InvalidDataException)
        {
            tamperedRouteCalendarRejected = true;
        }
        Require(tamperedRouteCalendarRejected,
            "A tampered static calendar report did not fail closed.");

        var outOfHorizonTargetRejected = false;
        try
        {
            _ = AcquisitionRouteTargetDateCalendarBuilder.Build(
                inventoryPath,
                loweringPath,
                windowsPath,
                routeCalendarPath,
                routeCalendar.DeadlineTotalDayExclusive);
        }
        catch (InvalidDataException)
        {
            outOfHorizonTargetRejected = true;
        }
        Require(outOfHorizonTargetRejected,
            "An out-of-horizon target date did not fail closed.");

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

    private static AcquisitionRouteTargetDateFacilityReport
        BuildFacilityFixtureChain(
            string inventoryPath,
            string loweringPath,
            string windowsPath,
            string routeCalendarPath,
            string targetDateCalendarPath,
            string snapshotPath,
            string routeTimingCalibrationPath,
            string unlockOutputPath,
            string festivalOutputPath,
            string locationOutputPath)
    {
        var unlock = AcquisitionRouteTargetDateUnlockBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            snapshotPath);
        Write(unlockOutputPath, unlock);
        var festival = AcquisitionRouteTargetDateFestivalBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            unlockOutputPath,
            snapshotPath);
        Write(festivalOutputPath, festival);
        var location = AcquisitionRouteTargetDateLocationBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            unlockOutputPath,
            festivalOutputPath,
            snapshotPath,
            routeTimingCalibrationPath);
        Write(locationOutputPath, location);
        return AcquisitionRouteTargetDateFacilityBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            unlockOutputPath,
            festivalOutputPath,
            locationOutputPath,
            snapshotPath,
            routeTimingCalibrationPath);
    }

    private static AcquisitionRouteTargetDateResourceReport
        BuildResourceFixtureChain(
            string inventoryPath,
            string loweringPath,
            string windowsPath,
            string routeCalendarPath,
            string targetDateCalendarPath,
            string snapshotPath,
            string routeTimingCalibrationPath,
            string unlockOutputPath,
            string festivalOutputPath,
            string locationOutputPath,
            string facilityOutputPath)
    {
        var facility = BuildFacilityFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            snapshotPath,
            routeTimingCalibrationPath,
            unlockOutputPath,
            festivalOutputPath,
            locationOutputPath);
        Write(facilityOutputPath, facility);
        return AcquisitionRouteTargetDateResourceBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            unlockOutputPath,
            festivalOutputPath,
            locationOutputPath,
            facilityOutputPath,
            snapshotPath,
            routeTimingCalibrationPath);
    }

    private static AcquisitionRouteTargetDateCurrencyReport
        BuildCurrencyFixtureChain(
            string inventoryPath,
            string loweringPath,
            string windowsPath,
            string routeCalendarPath,
            string targetDateCalendarPath,
            string snapshotPath,
            string routeTimingCalibrationPath,
            string unlockOutputPath,
            string festivalOutputPath,
            string locationOutputPath,
            string facilityOutputPath,
            string resourceOutputPath)
    {
        var resource = BuildResourceFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            snapshotPath,
            routeTimingCalibrationPath,
            unlockOutputPath,
            festivalOutputPath,
            locationOutputPath,
            facilityOutputPath);
        Write(resourceOutputPath, resource);
        return AcquisitionRouteTargetDateCurrencyBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            unlockOutputPath,
            festivalOutputPath,
            locationOutputPath,
            facilityOutputPath,
            resourceOutputPath,
            snapshotPath,
            routeTimingCalibrationPath);
    }

    private static AcquisitionRouteTargetDateProcessingReport
        BuildProcessingFixtureChain(
            string inventoryPath,
            string loweringPath,
            string windowsPath,
            string routeCalendarPath,
            string targetDateCalendarPath,
            string snapshotPath,
            string routeTimingCalibrationPath,
            string strategyLedgerPath,
            string outputRoot)
    {
        var unlockPath = Path.Combine(outputRoot, "target-date-unlock.json");
        var festivalPath = Path.Combine(
            outputRoot,
            "target-date-festival.json");
        var locationPath = Path.Combine(
            outputRoot,
            "target-date-location.json");
        var facilityPath = Path.Combine(
            outputRoot,
            "target-date-facility.json");
        var resourcePath = Path.Combine(
            outputRoot,
            "target-date-resource.json");
        var currencyPath = Path.Combine(
            outputRoot,
            "target-date-currency.json");
        var reservationPath = Path.Combine(
            outputRoot,
            "target-date-reservation.json");
        var currency = BuildCurrencyFixtureChain(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            snapshotPath,
            routeTimingCalibrationPath,
            unlockPath,
            festivalPath,
            locationPath,
            facilityPath,
            resourcePath);
        Write(currencyPath, currency);
        var reservation = AcquisitionRouteTargetDateReservationBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            unlockPath,
            festivalPath,
            locationPath,
            facilityPath,
            resourcePath,
            currencyPath,
            strategyLedgerPath,
            snapshotPath,
            routeTimingCalibrationPath);
        Write(reservationPath, reservation);
        return AcquisitionRouteTargetDateProcessingBuilder.Build(
            inventoryPath,
            loweringPath,
            windowsPath,
            routeCalendarPath,
            targetDateCalendarPath,
            unlockPath,
            festivalPath,
            locationPath,
            facilityPath,
            resourcePath,
            currencyPath,
            reservationPath,
            strategyLedgerPath,
            snapshotPath,
            routeTimingCalibrationPath);
    }

}
