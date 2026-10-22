using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class ActionQueueCompilerTests
{
    [Theory]
    [InlineData("none", "construct_barn", "construct_building")]
    [InlineData("barn", "upgrade_big_barn", "upgrade_building")]
    [InlineData("big_barn", "upgrade_deluxe_barn", "upgrade_building")]
    public void TruffleOilReserveProjectsAndBindsExactBarnStage(
        string farmState,
        string expectedStage,
        string expectedCandidateKind)
    {
        using var fixture = TruffleReserveFixture.Create(farmState);

        Assert.True(fixture.TryBuild(out var parameters, out var validation, out var reason), reason);
        Assert.Equal("truffle_oil_capacity", validation.ReserveKind);
        Assert.Equal(expectedStage, validation.ReserveStage);

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            [new OptionAvailabilityCandidate
            {
                OptionId = "fishing.catch_fish",
                Parameters = parameters
            }],
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.Equal(expectedCandidateKind, candidate.Kind);
        Assert.Contains(candidate.Parameters, row =>
            row.Name == MasterAnglerWindowIntentValidator.TruffleOilReserveStageParameter &&
            row.Value == expectedStage);
    }

    [Fact]
    public void DeluxeBarnWithoutPigProjectsExactPigPurchase()
    {
        using var fixture = TruffleReserveFixture.Create("deluxe_barn");

        Assert.True(fixture.TryBuild(out _, out var validation, out var reason), reason);
        Assert.Equal("purchase_pig", validation.ReserveStage);
        Assert.Equal("Deluxe Barn", validation.TargetBuildingType);
        Assert.Equal(10, validation.TargetBuildingTileX);
        Assert.Equal(10, validation.TargetBuildingTileY);
    }

    [Fact]
    public void ImmaturePigUsesGrowthWindowToEstablishOilMaker()
    {
        using var fixture = TruffleReserveFixture.Create("immature_pig");

        Assert.True(fixture.TryBuild(out _, out var validation, out var reason), reason);
        Assert.Equal("establish_oil_maker", validation.ReserveStage);
        Assert.Equal(10, validation.MinimumPigDaysToMaturity);
        Assert.Equal(11, validation.RequiredLeadDays);
    }

    [Fact]
    public void ExistingTruffleBuildsOilMakerWithoutRedundantPigInfrastructure()
    {
        using var fixture = TruffleReserveFixture.Create(
            "none",
            truffleInventory: 1);

        Assert.True(fixture.TryBuild(out _, out var validation, out var reason), reason);
        Assert.Equal("establish_oil_maker", validation.ReserveStage);
        Assert.Equal(1, validation.RequiredLeadDays);
    }

    [Fact]
    public void RemoteFarmTruffleIsObservedBeforePlanningNewProduction()
    {
        using var fixture = TruffleReserveFixture.Create(
            "adult_pig_open",
            farmTruffle: true,
            currentLocation: "Town");

        Assert.True(fixture.TryBuild(out _, out var validation, out var reason), reason);
        Assert.Equal("collect_spawned_truffle", validation.ReserveStage);
    }

    [Fact]
    public void BarnConstructionLeadIncludesBothRequiredUpgrades()
    {
        using var fixture = TruffleReserveFixture.Create("barn_constructing");

        Assert.True(fixture.TryBuild(out _, out var validation, out var reason), reason);
        Assert.Equal("wait_for_barn_upgrade", validation.ReserveStage);
        Assert.Equal(18, validation.RequiredLeadDays);
    }

    [Fact]
    public void AdultPigClosedDoorCompilesThroughNativeInteractExecutor()
    {
        using var fixture = TruffleReserveFixture.Create("adult_pig_closed");
        Assert.True(fixture.TryBuild(out var parameters, out var validation, out var reason), reason);
        Assert.Equal("open_pig_home_animal_door", validation.ReserveStage);

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            [new OptionAvailabilityCandidate
            {
                OptionId = "fishing.catch_fish",
                Parameters = parameters
            }],
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("toggle_animal_door", candidate.Kind);

        var ranked = new EventCandidateRanker().Rank(new(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, fixture.Snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, fixture.Snapshot);
        var item = Assert.Single(queue.Items.Where(row => row.OptionId == "executor.interact"));
        Assert.True(
            item.Status == "pending",
            string.Join(";", item.BlockingReasons) + "|missing=" +
            string.Join(",", item.MissingStateFactors));
        Assert.Contains(item.NormalizedCommand.Parameters, row =>
            row.Name == "interaction_kind" && row.Value == "animal_door");
    }

    [Fact]
    public void AnimalDoorBuildingIdentityTamperFailsClosed()
    {
        using var fixture = TruffleReserveFixture.Create("adult_pig_closed");
        Assert.True(fixture.TryBuild(out var parameters, out _, out var reason), reason);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            [new OptionAvailabilityCandidate
            {
                OptionId = "fishing.catch_fish",
                Parameters = parameters
            }],
            includeExecutorCalibrationOptions: true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new(), availability),
            fixture.Snapshot.StateHash);
        var interaction = Assert.Single(plan.Steps.Where(step => step.Kind == "interact"));
        interaction.Parameters = interaction.Parameters.Select(row =>
            row.Name == "building_type"
                ? RainReserveParameter(row.Name, "Big Barn")
                : row).ToArray();

        var queue = new ActionQueueCompiler().Compile(plan, fixture.Snapshot);
        Assert.Contains(
            "animal_door_projection_drifted",
            Assert.Single(queue.Items.Where(row => row.OptionId == "executor.interact")).BlockingReasons);
    }

    [Fact]
    public void PendingNativeTruffleOilOutputAdvancesToHardwoodReserve()
    {
        using var fixture = TruffleReserveFixture.Create(
            "adult_pig_open",
            includePendingTruffleOil: true);

        Assert.True(fixture.TryBuild(out _, out var validation, out var reason), reason);
        Assert.Equal("hardwood_reserve", validation.ReserveKind);
        Assert.Equal("blocked_no_exact_hardwood_source", validation.ReserveStage);
    }

    [Fact]
    public void HardwoodReserveBindsExactFarmClumpThroughExistingExecutor()
    {
        using var fixture = TruffleReserveFixture.Create(
            "adult_pig_open",
            includePendingTruffleOil: true,
            farmResourceClumpStatus: "ready");

        Assert.True(fixture.TryBuild(out var parameters, out var validation, out var reason), reason);
        Assert.Equal("hardwood_reserve", validation.ReserveKind);
        Assert.Equal("clear_loaded_farm_resource_clump", validation.ReserveStage);
        Assert.Equal(2, validation.HardwoodSourceGuaranteedQuantity);

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            [new OptionAvailabilityCandidate
            {
                OptionId = "fishing.catch_fish",
                Parameters = parameters
            }],
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("clear_farm_resource_clump", candidate.Kind);
        Assert.Contains(candidate.Parameters, row =>
            row.Name == MasterAnglerWindowIntentValidator.HardwoodReserveStageParameter &&
            row.Value == "clear_loaded_farm_resource_clump");

        Assert.Contains(candidate.Parameters, row =>
            row.Name == "expected_output_items_json" &&
            row.Value.Contains("\"quantity\":2", StringComparison.Ordinal));
    }

    [Fact]
    public void HardwoodReserveMissingAxeUpgradeFailsClosedWithPreciseStage()
    {
        using var fixture = TruffleReserveFixture.Create(
            "adult_pig_open",
            includePendingTruffleOil: true,
            farmResourceClumpStatus: "blocked_required_axe_upgrade_missing");

        Assert.True(fixture.TryBuild(out var parameters, out var validation, out var reason), reason);
        Assert.Equal("blocked_tool_upgrade_projection_missing", validation.ReserveStage);
        Assert.Equal(1, validation.HardwoodRequiredAxeUpgradeLevel);

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            [new OptionAvailabilityCandidate
            {
                OptionId = "fishing.catch_fish",
                Parameters = parameters
            }],
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains(
            "master_angler_hardwood_reserve_blocked_tool_upgrade_projection_missing",
            candidate.BlockReasons);
    }

    [Fact]
    public void HardwoodReserveCompilesExactNativeAxeUpgradeSubmission()
    {
        using var fixture = TruffleReserveFixture.Create(
            "adult_pig_open",
            includePendingTruffleOil: true,
            currentLocation: "Blacksmith",
            farmResourceClumpStatus: "blocked_required_axe_upgrade_missing",
            toolUpgradeStage: "submit_next_axe_upgrade");

        Assert.True(fixture.TryBuild(out var parameters, out var validation, out var reason), reason);
        Assert.Equal("submit_next_axe_upgrade", validation.ReserveStage);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            [new OptionAvailabilityCandidate
            {
                OptionId = "fishing.catch_fish",
                Parameters = parameters
            }],
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("submit_tool_upgrade", candidate.Kind);

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new(), availability),
            fixture.Snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, fixture.Snapshot);
        var item = Assert.Single(queue.Items.Where(row =>
            row.OptionId == "executor.submit_tool_upgrade"));
        Assert.Equal("pending", item.Status);
        Assert.Contains(item.NormalizedCommand.Parameters, row =>
            row.Name == "tool_upgrade_target_qualified_item_id" &&
            row.Value == "(T)CopperAxe");
        Assert.Contains(item.NormalizedCommand.Parameters, row =>
            row.Name == "tool_upgrade_trade_qualified_item_id" &&
            row.Value == "(O)334");
    }

    [Fact]
    public void NativeAxeUpgradePriceTamperFailsClosedBeforeExecution()
    {
        using var fixture = TruffleReserveFixture.Create(
            "adult_pig_open",
            includePendingTruffleOil: true,
            currentLocation: "Blacksmith",
            farmResourceClumpStatus: "blocked_required_axe_upgrade_missing",
            toolUpgradeStage: "submit_next_axe_upgrade");
        Assert.True(fixture.TryBuild(out var parameters, out _, out var reason), reason);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            [new OptionAvailabilityCandidate
            {
                OptionId = "fishing.catch_fish",
                Parameters = parameters
            }],
            includeExecutorCalibrationOptions: true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new(), availability),
            fixture.Snapshot.StateHash);
        var step = Assert.Single(plan.Steps.Where(row =>
            row.Kind == "submit_tool_upgrade"));
        step.Parameters = step.Parameters.Select(row =>
            row.Name == "tool_upgrade_price_gold"
                ? RainReserveParameter(row.Name, "1999")
                : row).ToArray();

        var item = Assert.Single(new ActionQueueCompiler()
            .Compile(plan, fixture.Snapshot).Items.Where(row =>
                row.OptionId == "executor.submit_tool_upgrade"));
        Assert.Equal("blocked", item.Status);
        Assert.Contains(
            "tool_upgrade_submission_state_or_cost_drifted",
            item.BlockingReasons);
    }

    [Fact]
    public void HardwoodReserveCompilesCompletedNativeAxeClaim()
    {
        using var fixture = TruffleReserveFixture.Create(
            "adult_pig_open",
            includePendingTruffleOil: true,
            currentLocation: "Blacksmith",
            farmResourceClumpStatus: "blocked_required_axe_upgrade_missing",
            toolUpgradeStage: "claim_ready_axe_upgrade");
        Assert.True(fixture.TryBuild(out var parameters, out var validation, out var reason), reason);
        Assert.Equal("claim_ready_axe_upgrade", validation.ReserveStage);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            [new OptionAvailabilityCandidate
            {
                OptionId = "fishing.catch_fish",
                Parameters = parameters
            }],
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.Equal("claim_tool_upgrade", candidate.Kind);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new(), availability),
            fixture.Snapshot.StateHash);
        var item = Assert.Single(new ActionQueueCompiler()
            .Compile(plan, fixture.Snapshot).Items.Where(row =>
                row.OptionId == "executor.claim_tool_upgrade"));
        Assert.Equal("pending", item.Status);
    }

    [Fact]
    public void HardwoodReserveSourceTamperFailsFreshValidation()
    {
        using var fixture = TruffleReserveFixture.Create(
            "adult_pig_open",
            includePendingTruffleOil: true,
            farmResourceClumpStatus: "ready");
        Assert.True(fixture.TryBuild(out var parameters, out _, out var reason), reason);
        var tampered = parameters.Select(row =>
            row.Name == MasterAnglerWindowIntentValidator.HardwoodReserveSourceQuantityParameter
                ? RainReserveParameter(row.Name, "8")
                : row).ToArray();

        Assert.False(MasterAnglerWindowIntentValidator.TryValidateRainTotemReserveIntent(
            fixture.Snapshot,
            tampered,
            out _,
            out var rejectionReason));
        Assert.Equal(
            "master_angler_hardwood_reserve_projection_drifted",
            rejectionReason);
    }

    [Fact]
    public void ExistingHardwoodSuppressesDuplicateReserveAfterOtherMaterialsSatisfied()
    {
        using var fixture = TruffleReserveFixture.Create(
            "adult_pig_open",
            includePendingTruffleOil: true,
            hardwoodInventory: 1,
            farmResourceClumpStatus: "ready");

        Assert.False(fixture.TryBuild(out _, out _, out var reason));
        Assert.Equal("master_angler_hardwood_inventory_already_sufficient", reason);
    }

    [Fact]
    public void RemoteFarmHardwoodSourceRoutesBeforeFreshReplan()
    {
        using var fixture = TruffleReserveFixture.Create(
            "adult_pig_open",
            includePendingTruffleOil: true,
            currentLocation: "Town",
            farmResourceClumpStatus: "ready");

        Assert.True(fixture.TryBuild(out var parameters, out var validation, out var reason), reason);
        Assert.Equal("route_to_farm_resource_clump", validation.ReserveStage);

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            [new OptionAvailabilityCandidate
            {
                OptionId = "fishing.catch_fish",
                Parameters = parameters
            }],
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.Contains(
            "fresh_snapshot_replan_required=true",
            candidate.ExpectedEffect,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WinterOnlyOpportunityIsSkippedForLaterNonWinterWindow()
    {
        using var fixture = TruffleReserveFixture.Create(
            "immature_pig",
            currentTotalDay: 83,
            rainyTargetDays: [112, 140]);

        Assert.True(fixture.TryBuild(out _, out var validation, out var reason), reason);
        Assert.Equal(140, validation.TargetTotalDay);
        Assert.True(validation.EligibleTruffleOpportunityDays > 0);
    }

    [Fact]
    public void AnimalDoorRuntimeUsesNativeBuildingActionWithoutDirectMutation()
    {
        var root = FindTruffleReserveRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Interact.cs"));

        Assert.Contains("building.doAction", source, StringComparison.Ordinal);
        Assert.Contains("NativeRightClickEdgePatch", source, StringComparison.Ordinal);
        Assert.DoesNotContain("animalDoorOpen.Value = true", source, StringComparison.Ordinal);
    }

    private static string FindTruffleReserveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "StardewValleyAICompanion.sln")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("StardewAI repository root not found.");
    }

    private sealed class TruffleReserveFixture : IDisposable
    {
        private TruffleReserveFixture(
            StardewAI.Contracts.State.SnapshotEnvelope snapshot,
            string windowIndexPath,
            string windowIndexSha256)
        {
            Snapshot = snapshot;
            WindowIndexPath = windowIndexPath;
            WindowIndexSha256 = windowIndexSha256;
        }

        public StardewAI.Contracts.State.SnapshotEnvelope Snapshot { get; }
        private string WindowIndexPath { get; }
        private string WindowIndexSha256 { get; }

        public bool TryBuild(
            out StardewAI.Contracts.Execution.SmallModelActionParameter[] parameters,
            out MasterAnglerRainTotemReserveIntentValidation validation,
            out string rejectionReason) =>
            MasterAnglerWindowIntentValidator.TryBuildRainTotemReserveIntent(
                Snapshot,
                WindowIndexPath,
                WindowIndexSha256,
                out parameters,
                out validation,
                out rejectionReason);

        public static TruffleReserveFixture Create(
            string farmState,
            bool includePendingTruffleOil = false,
            int currentTotalDay = 100,
            int[]? rainyTargetDays = null,
            int truffleInventory = 0,
            bool farmTruffle = false,
            string currentLocation = "Farm",
            int hardwoodInventory = 0,
            string? farmResourceClumpStatus = null,
            string? toolUpgradeStage = null)
        {
            rainyTargetDays ??= [139];
            var windowPath = WriteWindowIndex(rainyTargetDays);
            var windowHash = Convert.ToHexString(
                    SHA256.HashData(File.ReadAllBytes(windowPath)))
                .ToLowerInvariant();
            var hasBarn = farmState != "none";
            var type = farmState switch
            {
                "barn" or "barn_constructing" => "Barn",
                "big_barn" => "Big Barn",
                _ when hasBarn => "Deluxe Barn",
                _ => string.Empty
            };
            var hasPig = farmState.StartsWith("immature_pig", StringComparison.Ordinal) ||
                farmState.StartsWith("adult_pig", StringComparison.Ordinal);
            var adultPig = farmState.StartsWith("adult_pig", StringComparison.Ordinal);
            var doorOpen = farmState == "adult_pig_open";
            var buildings = hasBarn
                ? new object[]
                {
                    Building(
                        type,
                        doorOpen,
                        farmState == "barn_constructing" ? 3 : 0)
                }
                : Array.Empty<object>();
            var animals = hasPig
                ? new object[]
                {
                    new
                    {
                        type = "Pig",
                        age = adultPig ? 10 : 0,
                        days_to_mature = 10,
                        is_adult = adultPig,
                        management_home_building_type = "Deluxe Barn",
                        management_home_building_tile_x = 10,
                        management_home_building_tile_y = 10
                    }
                }
                : Array.Empty<object>();
            var machines = includePendingTruffleOil
                ? new object[]
                {
                    new
                    {
                        location_id = "Farm",
                        tile_x = 7,
                        tile_y = 7,
                        qualified_item_id = "(BC)19",
                        ready_for_harvest = false,
                        minutes_until_ready = 100,
                        held_item = new
                        {
                            item_id = "432",
                            qualified_item_id = "(O)432",
                            stack = 1
                        }
                    }
                }
                : Array.Empty<object>();
            var fish = Enumerable.Range(0, 72)
                .Select(index => new
                {
                    qualified_item_id = index == 0
                        ? "(O)145"
                        : "(O)" + (1000 + index),
                    caught = index != 0
                })
                .ToArray();
            var inventory = new List<object>
            {
                new
                {
                    slot_index = 0,
                    item_id = "726",
                    qualified_item_id = "(O)726",
                    stack = 5
                }
            };
            if (truffleInventory > 0)
            {
                inventory.Add(new
                {
                    slot_index = 1,
                    item_id = "430",
                    qualified_item_id = "(O)430",
                    stack = truffleInventory
                });
            }
            if (hardwoodInventory > 0)
            {
                inventory.Add(new
                {
                    slot_index = 2,
                    item_id = "709",
                    qualified_item_id = "(O)709",
                    stack = hardwoodInventory
                });
            }
            var resourceClumps = farmResourceClumpStatus is null
                ? Array.Empty<object>()
                : new object[]
                {
                    new
                    {
                        tile_x = 7,
                        tile_y = 5,
                        width = 2,
                        height = 2,
                        parent_sheet_index = 600,
                        clear_kind = "resource_stump",
                        minimum_tool_upgrade_level = 1,
                        tool_slot_index = farmResourceClumpStatus == "ready" ? 1 : -1,
                        expected_tool_hits_to_clear = 5,
                        clear_obstacle_executor_status = farmResourceClumpStatus,
                        clear_expected_output_items_json =
                            "[{\"runtimeType\":\"StardewValley.Object\",\"qualifiedItemId\":\"(O)709\",\"quality\":0,\"unitStateSha256\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\",\"quantity\":2}]",
                        clear_output_projection_status =
                            "exact_vanilla_conservative_minimum",
                        clear_output_native_contract =
                            "native_resource_clump_destroy_contract",
                        harvest_experience_projection_status = "exact",
                        harvest_experience_skill_id = "foraging",
                        harvest_experience_on_success_min = 25,
                        harvest_experience_on_success_max = 25,
                        harvest_experience_condition =
                            "native_player_axe_destroys_resource_stump_or_hollow_log"
                    }
                };
            var routeConnectors = string.Equals(
                    currentLocation,
                    "Farm",
                    StringComparison.OrdinalIgnoreCase)
                ? Array.Empty<object>()
                : new object[]
                {
                    new
                    {
                        kind = "warp",
                        tile_x = 10,
                        tile_y = 13,
                        target_location = "Farm",
                        target_x = 9,
                        target_y = 13,
                        resolved = true
                    }
                };
            var routeEdges = string.Equals(
                    currentLocation,
                    "Farm",
                    StringComparison.OrdinalIgnoreCase)
                ? Array.Empty<object>()
                : new object[]
                {
                    new
                    {
                        kind = "warp",
                        from_location = currentLocation,
                        from_x = 10,
                        from_y = 13,
                        target_location = "Farm",
                        target_x = 9,
                        target_y = 13,
                        resolved = true
                    }
                };
            object toolUpgrade = toolUpgradeStage is null
                ? new { projection_status = "unavailable" }
                : ToolUpgradeProjection(toolUpgradeStage, currentLocation);
            var state = new
            {
                time = new
                {
                    total_days = Field(currentTotalDay),
                    time = Field(600)
                },
                player = new
                {
                    location_id = Field(currentLocation),
                    tile_x = Field(9),
                    tile_y = Field(13),
                    facing_direction = Field(1),
                    money = Field(100000),
                    energy = Field(270),
                    inventory = Field(inventory),
                    inventory_capacity = Field(new
                    {
                        capacity = 12,
                        occupied_slots = 1,
                        free_slots = 11,
                        empty_slots = 11,
                        has_empty_slot = true
                    }),
                    safe_item_context = Field(new { safe_slot_index = 1 }),
                    building_construction_catalog = Field(BuildingCatalog()),
                    rain_totem = Field(new
                    {
                        projection_status =
                            "complete_current_native_rain_totem_context",
                        context_routing = new
                        {
                            affected_location_context_id = "Default",
                            allow_rain_totem = true
                        }
                    }),
                    tool_upgrade = Field(toolUpgrade)
                },
                farm = new
                {
                    buildings = Field(buildings),
                    animals = Field(animals),
                    objects = Field(farmTruffle
                        ? new object[]
                        {
                            new
                            {
                                tile_x = 14,
                                tile_y = 14,
                                item_id = "430",
                                qualified_item_id = "(O)430",
                                stack = 1,
                                big_craftable = false
                            }
                        }
                        : Array.Empty<object>()),
                    machines = Field(machines),
                    resource_clumps = Field(resourceClumps),
                    animal_purchase_catalog = Field(AnimalPurchaseCatalog())
                },
                fishing = new
                {
                    location_context = Field(new { }),
                    fishable_tiles = Field(Array.Empty<object>()),
                    rod_inventory = Field(Array.Empty<object>()),
                    rod_contexts = Field(new[]
                    {
                        new { has_magic_bait = false, uses_training_rod = false }
                    }),
                    active_cast_state = Field(new { is_active = false })
                },
                world_progress = new
                {
                    fish_collection_progress = Field(new
                    {
                        eligible_species_count = 72,
                        caught_eligible_species_count = 71,
                        missing_species_count = 1,
                        items = fish
                    }),
                    game_state_query_calendar_state = Field(new
                    {
                        current_total_day = currentTotalDay,
                        festival_date_keys = Array.Empty<string>()
                    })
                },
                locations = new
                {
                    collision_grid = Field(new
                    {
                        location_id = currentLocation,
                        width = 40,
                        height = 40,
                        notable_tiles = Array.Empty<object>()
                    }),
                    route_connectors = Field(new
                    {
                        location_id = currentLocation,
                        connectors = routeConnectors
                    }),
                    route_graph = Field(new
                    {
                        status = "complete",
                        edges = routeEdges
                    }),
                    route_gate_context = Field(new
                    {
                        location_id = currentLocation,
                        action_gates = Array.Empty<object>()
                    }),
                    route_action_branch_coverage = Field(new
                    {
                        location_id = currentLocation,
                        rows = Array.Empty<object>()
                    }),
                    social_route_date_evidence = Field(new
                    {
                        locations = new[]
                        {
                            new { location_id = "Farm", location_context_id = "Default" },
                            new { location_id = "Beach", location_context_id = "Default" }
                        }
                    })
                },
                current_location = new
                {
                    map = Field(new { location_id = currentLocation, width = 40, height = 40 }),
                    objects = Field(Array.Empty<object>())
                },
                menus = new
                {
                    active_menu = Field(new { is_open = false, type = "none" })
                }
            };
            return new TruffleReserveFixture(
                Snapshot(JsonSerializer.Serialize(state)),
                windowPath,
                windowHash);
        }

        private static object ToolUpgradeProjection(
            string lifecycleStage,
            string currentLocation)
        {
            const string contract =
                "shared_route->Blacksmith_checkAction->answerDialogue(Upgrade)->ShopMenu(ClintUpgrade).receiveLeftClick_exact_row->Tool.actionWhenPurchased->two_day_wait->Blacksmith_checkAction->native_tool_claim";
            var claim = lifecycleStage == "claim_ready_axe_upgrade";
            return new
            {
                schema_version = "tool_upgrade.v1",
                projection_status = "complete_live_native_contract",
                projection_fingerprint = new string('b', 64),
                location_id = "Blacksmith",
                counter_action_tiles = new[]
                {
                    new
                    {
                        tile_x = 5,
                        tile_y = 5,
                        action_raw = "Blacksmith",
                        action_token = "Blacksmith"
                    }
                },
                current_location_id = currentLocation,
                actor_control_ready = true,
                free_inventory_slots = 11,
                money_before = 100000,
                service_status = "ready",
                lifecycle_stage = lifecycleStage,
                pending_tool = new
                {
                    present = claim,
                    qualified_item_id = claim ? "(T)CopperAxe" : string.Empty,
                    item_id = claim ? "CopperAxe" : string.Empty,
                    runtime_type = claim ? "StardewValley.Tools.Axe" : string.Empty,
                    upgrade_level = claim ? 1 : -1,
                    days_left = claim ? 0 : -1,
                    claim_ready = claim,
                    claim_capacity_satisfied = true
                },
                current_axe = new
                {
                    present = !claim,
                    slot_index = claim ? -1 : 1,
                    qualified_item_id = claim ? string.Empty : "(T)Axe",
                    item_id = claim ? string.Empty : "Axe",
                    upgrade_level = claim ? -1 : 0
                },
                next_axe_upgrade = claim ? null : new
                {
                    available = true,
                    source_slot_index = 1,
                    source_qualified_item_id = "(T)Axe",
                    source_item_id = "Axe",
                    source_upgrade_level = 0,
                    target_qualified_item_id = "(T)CopperAxe",
                    target_item_id = "CopperAxe",
                    target_upgrade_level = 1,
                    price_gold = 2000,
                    trade_item_qualified_item_id = "(O)334",
                    trade_item_id = "334",
                    trade_item_amount = 5,
                    trade_item_available = 5,
                    money_available = 100000,
                    requirements_satisfied = true,
                    source_contract = "Game1.toolData.ConventionalUpgradeFrom+ShopBuilder.GetToolUpgradeData"
                },
                native_contract = contract
            };
        }

        private static object Building(
            string type,
            bool doorOpen,
            int constructionDays) => new
        {
            type,
            tile_x = 10,
            tile_y = 10,
            days_of_construction_left = constructionDays,
            upgrade_name = string.Empty,
            days_until_upgrade = 0,
            is_under_construction = constructionDays > 0,
            animal_door = new
            {
                present = type == "Deluxe Barn",
                is_open = doorOpen,
                target_tile_x = 11,
                target_tile_y = 13,
                tile_width = 2,
                tile_height = 1,
                interaction_status = "ready_native_right_click_toggle",
                native_contract =
                    "Building.doAction->didPlayerJustRightClick->Building.ToggleAnimalDoor->animalDoorOpen"
            }
        };

        private static object BuildingCatalog() => new
        {
            rows = new[]
            {
                BuildingRow("Barn", "construction", "", 3),
                BuildingRow("Big Barn", "upgrade", "Barn", 2),
                BuildingRow("Deluxe Barn", "upgrade", "Big Barn", 2)
            },
            upgrade_definitions = new[]
            {
                new { target_building_type = "Big Barn", native_countdown_days = 2 },
                new { target_building_type = "Deluxe Barn", native_countdown_days = 2 }
            }
        };

        private static object BuildingRow(
            string type,
            string projectKind,
            string sourceType,
            int days) => new
        {
            building_type = type,
            display_name = type,
            project_kind = projectKind,
            upgrade_from_building_type = sourceType,
            builder = "Robin",
            build_days = days,
            build_cost = 0,
            build_materials = Array.Empty<object>(),
            expected_money_before = 100000,
            expected_money_after = 100000,
            service_location_id = "Farm",
            service_action_tile_x = 2,
            service_action_tile_y = 2,
            service_action_raw = "Carpenter",
            placement_location_id = "Farm",
            placement_tile_x = 10,
            placement_tile_y = 10,
            placement_verification = "exact",
            action_status = projectKind == "upgrade"
                ? "ready_for_native_upgrade"
                : "ready_for_native_construction",
            native_contract = "CarpenterMenu"
        };

        private static object[] AnimalPurchaseCatalog() =>
        [
            new
            {
                target_location_id = "Farm",
                native_location_choice_index = 0,
                stock = new[]
                {
                    new
                    {
                        candidate_identity_sha256 = new string('a', 64),
                        animal_type_id = "Pig",
                        display_name = "Pig",
                        possible_actual_type_ids = new[] { "Pig" },
                        price = 8000,
                        player_money = 100000,
                        required_building_met = true,
                        can_afford = true,
                        generated_unique_name = "ReservePig",
                        compatible_homes = new[]
                        {
                            new
                            {
                                building_type = "Deluxe Barn",
                                building_tile_x = 10,
                                building_tile_y = 10,
                                indoor_location_id = "Barn10",
                                compatible_with_all_possible_types = true,
                                is_under_construction = false,
                                occupant_count = 0,
                                capacity = 12,
                                available_slots = 12
                            }
                        }
                    }
                }
            }
        ];

        private static object Field(object value) => new
        {
            value,
            status = "available",
            source = new { kind = "game_object", path = "test" },
            adapter = "test",
            read_at_tick = 1,
            confidence = 1
        };

        private static string WriteWindowIndex(int[] rainyTargetDays)
        {
            var species = Enumerable.Range(0, 72)
                .Select(index => new
                {
                    qualified_item_id = index == 0
                        ? "(O)145"
                        : "(O)" + (1000 + index),
                    windows = rainyTargetDays.Select(day => new
                    {
                        source_kind = "location_rule",
                        source_key = "Beach:" + day,
                        location_id = "Beach",
                        first_total_day = day,
                        last_total_day = day,
                        minimum_fishing_level = 0,
                        require_magic_bait = false,
                        training_rod_allowed = true,
                        dynamic_conditions = Array.Empty<string>(),
                        weather_modes = new[] { "rain", "storm" },
                        time_windows = new[]
                        {
                            new { start_time = 600, end_time = 2600 }
                        }
                    }).ToArray()
                })
                .ToArray();
            var path = Path.Combine(
                Path.GetTempPath(),
                "stardewai-truffle-reserve-" + Guid.NewGuid().ToString("N") +
                ".json");
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(new
                {
                    schema_version = "master_angler_stage_one_window_index.v1",
                    status = "complete_static_windows_dynamic_execution_pending",
                    game_version = string.Empty,
                    deadline_total_day_exclusive = 224,
                    native_denominator_count = 72,
                    static_window_coverage_complete = true,
                    training_label_eligible = false,
                    species
                }),
                new UTF8Encoding(false));
            return path;
        }

        public void Dispose()
        {
            if (File.Exists(WindowIndexPath))
                File.Delete(WindowIndexPath);
        }
    }
}
