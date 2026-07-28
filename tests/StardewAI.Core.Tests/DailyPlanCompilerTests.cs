using StardewAI.Contracts.Training;
using StardewAI.Contracts.Execution;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class DailyPlanCompilerTests
{
    [Fact]
    public void CompileTurnsDeferredShopEndpointIntoOneRollingWaitThenReplan()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "interact:Town:11,10:OpenShop:SeedShop",
            Kind = "interact_endpoint",
            Rank = 1,
            TimelineStatus = "deferred",
            ScheduledWaitCost = 1200,
            LocationId = "Town",
            TileX = 11,
            TileY = 10,
            ExpectedEffect = "move_to_adjacent=10,10;preview_interact=OpenShop",
            EstimatedTicks = 90
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        Assert.Equal("small_model_plan.v1", plan.SchemaVersion);
        Assert.Equal("daily_candidate_plan", plan.PlanType);
        Assert.Equal("training_farmer", plan.Actor.ActorType);
        var wait = Assert.Single(plan.Steps);
        Assert.Equal("wait_ticks", wait.Kind);
        Assert.Equal(600, wait.WaitTicks);
        Assert.Contains("fresh_snapshot_replan_required=true", wait.ExpectedEffects);
        Assert.Contains(plan.CandidateAudit[0].Reasons, reason => reason == "rolling_horizon_wait_then_refresh_snapshot");
    }

    [Fact]
    public void CompileTurnsBuyCandidateIntoPurchasePlanStep()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "buy:SeedShop:(O)472",
            Kind = "buy_shop_item",
            Rank = 1,
            TimelineStatus = "ready_now",
            ShopId = "SeedShop",
            QualifiedItemId = "(O)472",
            Quantity = 5
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        Assert.Equal(2, plan.Steps.Length);
        var step = plan.Steps[0];
        Assert.Equal("buy_shop_item", step.Kind);
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "qualified_item_id" && parameter.Value == "(O)472");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "quantity" && parameter.Value == "1");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "requested_quantity" && parameter.Value == "5");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "expected_shop_id" && parameter.Value == "SeedShop");
        Assert.Contains(step.SafetyConstraints, constraint => constraint == "quantity_one_safe_purchase_slice");
        Assert.Equal("close_menu", plan.Steps[1].Kind);
        Assert.Contains(plan.Steps[1].SafetyConstraints, constraint => constraint == "post_purchase_menu_cleanup");
    }

    [Fact]
    public void CompileTurnsSellCandidateIntoExactNativeSaleStep()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:0",
            Kind = "sell_shop_item",
            Rank = 1,
            TimelineStatus = "ready_now",
            ShopId = "SeedShop",
            ItemId = "24",
            QualifiedItemId = "(O)24",
            SlotIndex = 0,
            Quantity = 3,
            UnitPrice = 35,
            TotalValue = 105,
            CanShopSell = true
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        Assert.Equal(2, plan.Steps.Length);
        var sale = plan.Steps[0];
        Assert.Equal("sell_shop_item", sale.Kind);
        Assert.Contains(sale.Parameters, parameter => parameter.Name == "slot_index" && parameter.Value == "0");
        Assert.Contains(sale.Parameters, parameter => parameter.Name == "quantity" && parameter.Value == "3");
        Assert.Contains(sale.Parameters, parameter => parameter.Name == "expected_unit_price" && parameter.Value == "35");
        Assert.Contains(sale.Parameters, parameter => parameter.Name == "expected_total_value" && parameter.Value == "105");
        Assert.Contains(sale.SafetyConstraints, value => value == "native_shop_menu_click_only");
        Assert.Equal("close_menu", plan.Steps[1].Kind);
    }

    [Fact]
    public void CompileOrdersShopOpenBeforeMatchingPurchaseCandidate()
    {
        var buy = new PolicyEventCandidatePrediction
        {
            CandidateId = "buy:SeedShop:(O)472",
            Kind = "buy_shop_item",
            Rank = 1,
            TimelineStatus = "ready_now",
            ShopId = "SeedShop",
            QualifiedItemId = "(O)472",
            Quantity = 1,
            Score = 10
        };
        var open = new PolicyEventCandidatePrediction
        {
            CandidateId = "interact:Town:11,10:OpenShop:SeedShop",
            Kind = "interact_endpoint",
            Rank = 2,
            TimelineStatus = "ready_now",
            ShopId = "SeedShop",
            LocationId = "Town",
            TileX = 11,
            TileY = 10,
            ExpectedEffect = "move_to_adjacent=10,10;preview_interact=OpenShop",
            EstimatedTicks = 90,
            Score = 1
        };

        var plan = new DailyPlanCompiler().Compile(new[] { buy, open }, "state.1");

        Assert.Equal("move_to_tile", plan.Steps[0].Kind);
        Assert.Equal("interact", plan.Steps[1].Kind);
        Assert.Equal("buy_shop_item", plan.Steps[2].Kind);
        Assert.Equal("close_menu", plan.Steps[3].Kind);
    }

    [Fact]
    public void CompileSkipsUnsupportedCandidateKindsInsteadOfPretendingExecutorSupport()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "mine:floor:40",
            Kind = "mining_floor_goal",
            Rank = 1,
            TimelineStatus = "ready_now"
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        Assert.Empty(plan.Steps);
        var audit = Assert.Single(plan.CandidateAudit);
        Assert.Equal("skipped", audit.Decision);
        Assert.Contains("unsupported_candidate_kind_or_missing_required_candidate_fields", audit.Reasons);
    }

    [Fact]
    public void CompileDoesNotLetUnsupportedCandidateConsumeMaxCandidateSlot()
    {
        var unsupported = new PolicyEventCandidatePrediction
        {
            CandidateId = "mine:floor:40",
            Kind = "mining_floor_goal",
            Rank = 1,
            TimelineStatus = "ready_now"
        };
        var supported = new PolicyEventCandidatePrediction
        {
            CandidateId = "water:Farm:1,2",
            Kind = "water_crop_tile",
            Rank = 2,
            TimelineStatus = "ready_now",
            LocationId = "Farm",
            TileX = 1,
            TileY = 2,
            ExpectedEffect = "farm.crops[1,2].needs_watering=false",
            EstimatedTicks = 60,
            EnergyCost = 2
        };

        var plan = new DailyPlanCompiler().Compile(new[] { unsupported, supported }, "state.1", maxCandidates: 1);

        var step = Assert.Single(plan.Steps);
        Assert.Equal("maintain_crops", step.Kind);
        Assert.Equal(2, plan.CandidateAudit.Length);
        Assert.Equal("skipped", plan.CandidateAudit[0].Decision);
        Assert.Contains("unsupported_candidate_kind_or_missing_required_candidate_fields", plan.CandidateAudit[0].Reasons);
        Assert.Equal("accepted", plan.CandidateAudit[1].Decision);
    }

    [Theory]
    [InlineData("quest_candidate", "quest_objective_binding_not_executable")]
    [InlineData("special_order_candidate", "special_order_objective_binding_not_executable")]
    public void CompileReportsKnownImplementationBlockers(string kind, string blockReason)
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "blocked:" + kind,
            Kind = kind,
            Rank = 1,
            TimelineStatus = "ready_now"
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        Assert.Empty(plan.Steps);
        var audit = Assert.Single(plan.CandidateAudit);
        Assert.Contains("candidate_kind_known_but_not_executable", audit.Reasons);
        Assert.Contains(blockReason, audit.Reasons);
    }

    [Fact]
    public void CompileSkipsCandidatesThatWouldExceedAggregateTimeBudget()
    {
        var first = WaterCandidate("water:Farm:1,2", 1, 2);
        var second = WaterCandidate("water:Farm:3,4", 3, 4);

        var plan = new DailyPlanCompiler().Compile(
            new[] { first, second },
            "state.1",
            maxCandidates: 4,
            availableMinutes: 1,
            energyBudget: 270);

        var step = Assert.Single(plan.Steps);
        Assert.Equal(1, step.TargetTileX);
        Assert.Equal(2, step.TargetTileY);
        Assert.Equal(2, plan.CandidateAudit.Length);
        Assert.Equal("accepted", plan.CandidateAudit[0].Decision);
        Assert.Equal("skipped", plan.CandidateAudit[1].Decision);
        Assert.Contains("aggregate_time_budget_exceeded", plan.CandidateAudit[1].Reasons);
        Assert.Equal(0, plan.CandidateAudit[1].RemainingMinutesBefore);
        Assert.Equal(0, plan.CandidateAudit[1].RemainingMinutesAfter);
    }

    [Fact]
    public void CompileSkipsCandidatesThatWouldExceedAggregateEnergyBudget()
    {
        var first = WaterCandidate("water:Farm:1,2", 1, 2);
        var second = WaterCandidate("water:Farm:3,4", 3, 4);

        var plan = new DailyPlanCompiler().Compile(
            new[] { first, second },
            "state.1",
            maxCandidates: 4,
            availableMinutes: 20,
            energyBudget: 2);

        var step = Assert.Single(plan.Steps);
        Assert.Equal(1, step.TargetTileX);
        Assert.Equal(2, step.TargetTileY);
        Assert.Equal(2, plan.CandidateAudit.Length);
        Assert.Equal("accepted", plan.CandidateAudit[0].Decision);
        Assert.Equal("skipped", plan.CandidateAudit[1].Decision);
        Assert.Contains("aggregate_energy_budget_exceeded", plan.CandidateAudit[1].Reasons);
        Assert.Equal(0, plan.CandidateAudit[1].RemainingEnergyBefore);
        Assert.Equal(0, plan.CandidateAudit[1].RemainingEnergyAfter);
    }

    [Fact]
    public void CompileAnnotatesAcceptedStepsWithAggregateBudgetContext()
    {
        var first = WaterCandidate("water:Farm:1,2", 1, 2);

        var plan = new DailyPlanCompiler().Compile(
            new[] { first },
            "state.1",
            maxCandidates: 4,
            availableMinutes: 10,
            energyBudget: 20);

        var step = Assert.Single(plan.Steps);
        Assert.Contains(step.SafetyConstraints, constraint => constraint == "daily_plan_aggregate_budget_checked");
        Assert.Contains(step.SafetyConstraints, constraint => constraint == "daily_plan_time_budget_checked");
        Assert.Contains(step.SafetyConstraints, constraint => constraint == "daily_plan_energy_budget_checked");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "budget.accepted_candidate_index" && parameter.Value == "0");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "budget.candidate_minutes" && parameter.Value == "1");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "budget.candidate_energy_cost" && parameter.Value == "2");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "budget.remaining_minutes_before" && parameter.Value == "10");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "budget.remaining_minutes_after" && parameter.Value == "9");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "budget.remaining_energy_before" && parameter.Value == "20");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "budget.remaining_energy_after" && parameter.Value == "18");
        var audit = Assert.Single(plan.CandidateAudit);
        Assert.Equal("accepted", audit.Decision);
        Assert.Contains("fits_aggregate_budget", audit.Reasons);
        Assert.Equal(10, audit.RemainingMinutesBefore);
        Assert.Equal(9, audit.RemainingMinutesAfter);
        Assert.Equal(20, audit.RemainingEnergyBefore);
        Assert.Equal(18, audit.RemainingEnergyAfter);
    }

    [Fact]
    public void CompileAuditsCandidatesSkippedAfterMaxCandidateLimit()
    {
        var first = WaterCandidate("water:Farm:1,2", 1, 2);
        var second = WaterCandidate("water:Farm:3,4", 3, 4);

        var plan = new DailyPlanCompiler().Compile(
            new[] { first, second },
            "state.1",
            maxCandidates: 1,
            availableMinutes: 20,
            energyBudget: 20);

        var step = Assert.Single(plan.Steps);
        Assert.Equal(1, step.TargetTileX);
        Assert.Equal(2, step.TargetTileY);
        Assert.Equal(2, plan.CandidateAudit.Length);
        Assert.Equal("accepted", plan.CandidateAudit[0].Decision);
        Assert.Equal("skipped", plan.CandidateAudit[1].Decision);
        Assert.Contains("max_candidates_reached", plan.CandidateAudit[1].Reasons);
    }

    [Fact]
    public void CompileTurnsWaterCropCandidateIntoTargetedCropMaintenanceStep()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "water:Farm:1,2",
            Kind = "water_crop_tile",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "Farm",
            TileX = 1,
            TileY = 2,
            ExpectedEffect = "farm.crops[1,2].needs_watering=false",
            EstimatedTicks = 60
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        var step = Assert.Single(plan.Steps);
        Assert.Equal("maintain_crops", step.Kind);
        Assert.Equal("Farm", step.TargetLocation);
        Assert.Equal(1, step.TargetTileX);
        Assert.Equal(2, step.TargetTileY);
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "max_crops" && parameter.Value == "1");
        Assert.Contains(step.ExpectedEffects, effect => effect == "farm.crops[1,2].needs_watering=false");
    }

    [Fact]
    public void CompileTurnsHarvestCandidateIntoHarvestCropStep()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "harvest:Farm:7,8",
            Kind = "harvest_crop_tile",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "Farm",
            TileX = 7,
            TileY = 8,
            ExpectedEffect = "farm.crops[7,8].ready_for_harvest=false;harvest_item_id=24;harvest_method=Grab;harvest_executor_status=runtime_verified",
            EstimatedTicks = 60,
            Available = true
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        var step = Assert.Single(plan.Steps);
        Assert.Equal("harvest_crop", step.Kind);
        Assert.Equal("Farm", step.TargetLocation);
        Assert.Equal(7, step.TargetTileX);
        Assert.Equal(8, step.TargetTileY);
        Assert.Contains(step.Preconditions, condition => condition == "farm.crops.ready_for_harvest=true");
        Assert.Contains(step.ExpectedEffects, effect => effect.Contains("harvest_executor_status=runtime_verified"));
        Assert.Contains(step.SafetyConstraints, constraint => constraint == "target_crop_tile_from_transparent_farm_state");
        Assert.Contains(step.SafetyConstraints, constraint => constraint == "runtime_verified_single_tile_harvest");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "harvest_item_id" && parameter.Value == "24");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "harvest_method" && parameter.Value == "Grab");
    }

    [Fact]
    public void CompileTurnsGiantCropHarvestCandidateIntoHarvestGiantCropStep()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "harvest-giant-crop:Farm:7,8",
            Kind = "harvest_giant_crop_tile",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "Farm",
            TileX = 7,
            TileY = 8,
            ExpectedEffect = "farm.resource_clumps[7,8].is_giant_crop=false;giant_crop_id=276;required_tool=axe;resource_clump_health=3;harvest_giant_crop_executor_status=runtime_verified",
            EstimatedTicks = 180,
            Available = true
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        var step = Assert.Single(plan.Steps);
        Assert.Equal("harvest_giant_crop", step.Kind);
        Assert.Equal("Farm", step.TargetLocation);
        Assert.Equal(7, step.TargetTileX);
        Assert.Equal(8, step.TargetTileY);
        Assert.Contains(step.Preconditions, condition => condition == "farm.resource_clumps.is_giant_crop=true");
        Assert.Contains(step.ExpectedEffects, effect => effect.Contains("harvest_giant_crop_executor_status=runtime_verified"));
        Assert.Contains(step.SafetyConstraints, constraint => constraint == "target_giant_crop_from_transparent_resource_clumps");
        Assert.Contains(step.SafetyConstraints, constraint => constraint == "runtime_verified_multi_hit_axe_harvest");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "giant_crop_id" && parameter.Value == "276");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "required_tool" && parameter.Value == "axe");
    }

    [Fact]
    public void CompileTurnsPickupDebrisCandidateIntoMoveThenPickupSteps()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "pickup-debris:Farm:0:65,15:(O)388",
            Kind = "pickup_debris_item",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "Farm",
            TileX = 65,
            TileY = 15,
            ItemId = "(O)388",
            QualifiedItemId = "(O)388",
            ExpectedEffect = "farm.debris[0].chunk_count_decreases_or_removed=true;qualified_item_id=(O)388;item_id=(O)388;debris_index=0;pickup_executor_status=runtime_collect",
            EstimatedTicks = 90,
            Available = true
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        Assert.Equal(2, plan.Steps.Length);
        Assert.Equal("move_to_tile", plan.Steps[0].Kind);
        Assert.Equal("Farm", plan.Steps[0].TargetLocation);
        Assert.Equal(65, plan.Steps[0].TargetTileX);
        Assert.Equal(15, plan.Steps[0].TargetTileY);
        Assert.Equal("pickup_debris", plan.Steps[1].Kind);
        Assert.Equal("Farm", plan.Steps[1].TargetLocation);
        Assert.Equal(65, plan.Steps[1].TargetTileX);
        Assert.Equal(15, plan.Steps[1].TargetTileY);
        Assert.Contains(plan.Steps[1].Preconditions, condition => condition == "current_location.debris.target_exists=true");
        Assert.Contains(plan.Steps[1].SafetyConstraints, constraint => constraint == "runtime_verified_debris_collect");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "debris_index" && parameter.Value == "0");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "qualified_item_id" && parameter.Value == "(O)388");
    }

    [Fact]
    public void CompileTurnsMachineOutputCandidateIntoMoveThenCollectSteps()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "machine-output:Farm:64,15:(O)388",
            Kind = "collect_machine_output_tile",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "Farm",
            TileX = 64,
            TileY = 15,
            ItemId = "388",
            QualifiedItemId = "(O)388",
            Quantity = 1,
            ExpectedEffect = "move_to_adjacent=63,15;farm.machines[Farm:64,15].held_item=null;qualified_item_id=(O)388;item_id=388;output_stack=1;output_sale_price=20;output_total_value=20;machine_value_basis=held_item_sale_price_times_stack;machine_output_executor_status=runtime_collect",
            EstimatedTicks = 90,
            Available = true
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        Assert.Equal(2, plan.Steps.Length);
        Assert.Equal("move_to_tile", plan.Steps[0].Kind);
        Assert.Equal("Farm", plan.Steps[0].TargetLocation);
        Assert.Equal(63, plan.Steps[0].TargetTileX);
        Assert.Equal(15, plan.Steps[0].TargetTileY);
        Assert.Equal("collect_machine_output", plan.Steps[1].Kind);
        Assert.Equal("Farm", plan.Steps[1].TargetLocation);
        Assert.Equal(64, plan.Steps[1].TargetTileX);
        Assert.Equal(15, plan.Steps[1].TargetTileY);
        Assert.Contains(plan.Steps[1].Preconditions, condition => condition == "farm.machines.target_ready=true");
        Assert.Contains(plan.Steps[1].SafetyConstraints, constraint => constraint == "runtime_verified_machine_output_collect");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "qualified_item_id" && parameter.Value == "(O)388");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "quantity" && parameter.Value == "1");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "output_total_value" && parameter.Value == "20");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "machine_value_basis" && parameter.Value == "held_item_sale_price_times_stack");
    }

    [Fact]
    public void CompileTurnsMachineInputCandidateIntoMoveThenLoadSteps()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "machine-input:Farm:64,15:slot0:(O)262",
            Kind = "load_machine_input_tile",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "Farm",
            TileX = 64,
            TileY = 15,
            ItemId = "262",
            QualifiedItemId = "(O)262",
            SlotIndex = 0,
            Quantity = 2,
            ExpectedEffect = "move_to_adjacent=63,15;farm.machines[Farm:64,15].minutes_until_ready>0_or_ready=true;input_slot_index=0;qualified_item_id=(O)262;item_id=262;input_stack_available=2;input_sale_price=15;machine_input_opportunity_cost=15;machine_input_value_basis=predicted_output_total_value_minus_transparent_input_sale_price;machine_output_rule_count=3;machine_has_output_rule=true;machine_output_prediction_status=machine_data_exact_required_item_match;predicted_output_qualified_item_id=(O)346;predicted_output_item_id=346;predicted_output_stack=1;predicted_output_sale_price=200;predicted_output_price_source=output_item_sale_price;predicted_output_total_value=200;machine_additional_consumed_total_value=0;predicted_output_net_value=185;predicted_output_rule_required_item_id=(O)262;predicted_minutes_until_ready=1750;machine_input_probe_source=Object.performObjectDropInAction(probe:true);machine_input_executor_status=runtime_load",
            EstimatedTicks = 90,
            Available = true
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        Assert.Equal(2, plan.Steps.Length);
        Assert.Equal("move_to_tile", plan.Steps[0].Kind);
        Assert.Equal("Farm", plan.Steps[0].TargetLocation);
        Assert.Equal(63, plan.Steps[0].TargetTileX);
        Assert.Equal(15, plan.Steps[0].TargetTileY);
        Assert.Equal("load_machine_input", plan.Steps[1].Kind);
        Assert.Equal("Farm", plan.Steps[1].TargetLocation);
        Assert.Equal(64, plan.Steps[1].TargetTileX);
        Assert.Equal(15, plan.Steps[1].TargetTileY);
        Assert.Contains(plan.Steps[1].Preconditions, condition => condition == "farm.machines.target_accepts_input_probe=true");
        Assert.Contains(plan.Steps[1].SafetyConstraints, constraint => constraint == "runtime_verified_machine_input_load");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "input_slot_index" && parameter.Value == "0");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "qualified_item_id" && parameter.Value == "(O)262");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "input_stack_available" && parameter.Value == "2");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "machine_input_opportunity_cost" && parameter.Value == "15");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "machine_input_value_basis" && parameter.Value == "predicted_output_total_value_minus_transparent_input_sale_price");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "machine_output_rule_count" && parameter.Value == "3");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "machine_output_prediction_status" && parameter.Value == "machine_data_exact_required_item_match");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "predicted_output_qualified_item_id" && parameter.Value == "(O)346");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "predicted_output_total_value" && parameter.Value == "200");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "predicted_output_price_source" && parameter.Value == "output_item_sale_price");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "machine_additional_consumed_total_value" && parameter.Value == "0");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "predicted_output_net_value" && parameter.Value == "185");
        Assert.Contains(plan.Steps[1].Parameters, parameter =>
            parameter.Name == "predicted_minutes_until_ready" && parameter.Value == "1750");
    }

    [Fact]
    public void CompilePreservesMachineDistributionContract()
    {
        var candidate =
            new PolicyEventCandidatePrediction
            {
                CandidateId =
                    "machine-input:Farm:64,15:slot0:(TR)IridiumSpur",
                Kind = "load_machine_input_tile",
                Rank = 1,
                TimelineStatus = "ready_now",
                LocationId = "Farm",
                TileX = 64,
                TileY = 15,
                ItemId = "IridiumSpur",
                QualifiedItemId =
                    "(TR)IridiumSpur",
                SlotIndex = 0,
                Quantity = 1,
                ExpectedEffect =
                    "move_to_adjacent=63,15;input_slot_index=0;qualified_item_id=(TR)IridiumSpur;machine_additional_consumed_items=(O)337:3;machine_additional_consumed_available=(O)337:3;machine_special_prediction_model_id=anvil_trinket_reforge_distribution.v1;machine_prediction_training_kind=complete_distribution;machine_prediction_contract_fingerprint=abc123;machine_output_distribution_outcome_kind=iridium_spur",
                EstimatedTicks = 90,
                Available = true
            };

        var plan = new DailyPlanCompiler()
            .Compile(
                new[] { candidate },
                "state.1");

        var load = Assert.Single(
            plan.Steps.Where(step =>
                step.Kind ==
                "load_machine_input"));
        Assert.Contains(
            load.Parameters,
            parameter =>
                parameter.Name ==
                    "machine_prediction_training_kind" &&
                parameter.Value ==
                    "complete_distribution");
        Assert.Contains(
            load.Parameters,
            parameter =>
                parameter.Name ==
                    "machine_prediction_contract_fingerprint" &&
                parameter.Value == "abc123");
        Assert.Contains(
            load.Parameters,
            parameter =>
                parameter.Name ==
                    "machine_output_distribution_outcome_kind" &&
                parameter.Value ==
                    "iridium_spur");
    }

    [Fact]
    public void CompileSkipsMachineInputCandidatesWhenInputStackAlreadyReserved()
    {
        var first = MachineInputCandidate("machine-input:Farm:64,15:slot0:(O)262", 64, 15, slotIndex: 0, stack: 1, score: 10);
        var second = MachineInputCandidate("machine-input:Farm:65,15:slot0:(O)262", 65, 15, slotIndex: 0, stack: 1, score: 9);

        var plan = new DailyPlanCompiler().Compile(new[] { first, second }, "state.1", maxCandidates: 4);

        Assert.Equal(2, plan.Steps.Length);
        Assert.Contains(plan.CandidateAudit, audit =>
            audit.CandidateId == first.CandidateId &&
            audit.Decision == "accepted");
        Assert.Contains(plan.CandidateAudit, audit =>
            audit.CandidateId == second.CandidateId &&
            audit.Decision == "skipped" &&
            audit.Reasons.Contains("daily_plan_machine_input_stack_already_reserved"));
    }

    [Fact]
    public void CompileSkipsMachineInputCandidatesWhenAdditionalConsumedStackAlreadyReserved()
    {
        var first = MachineInputCandidate("machine-input:Farm:64,15:slot0:(O)262", 64, 15, slotIndex: 0, stack: 1, score: 10);
        first.ExpectedEffect += ";machine_additional_consumed_items=(O)388:1;machine_additional_consumed_available=(O)388:1";
        var second = MachineInputCandidate("machine-input:Farm:65,15:slot1:(O)262", 65, 15, slotIndex: 1, stack: 1, score: 9);
        second.ExpectedEffect += ";machine_additional_consumed_items=(O)388:1;machine_additional_consumed_available=(O)388:1";

        var plan = new DailyPlanCompiler().Compile(new[] { first, second }, "state.1", maxCandidates: 4);

        Assert.Equal(2, plan.Steps.Length);
        Assert.Contains(plan.CandidateAudit, audit =>
            audit.CandidateId == first.CandidateId &&
            audit.Decision == "accepted");
        Assert.Contains(plan.CandidateAudit, audit =>
            audit.CandidateId == second.CandidateId &&
            audit.Decision == "skipped" &&
            audit.Reasons.Contains("daily_plan_machine_additional_consumed_stack_already_reserved"));
    }

    [Fact]
    public void CompileTurnsClearObstacleCandidateIntoClearObstacleStep()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "clear:Farm:11,10:grass",
            Kind = "clear_obstacle_tile",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "Farm",
            TileX = 11,
            TileY = 10,
            ExpectedEffect = "current_location.obstacle[11,10]=clear;clear_kind=grass;source=Grass",
            EstimatedTicks = 60
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        var step = Assert.Single(plan.Steps);
        Assert.Equal("clear_obstacle", step.Kind);
        Assert.Equal("Farm", step.TargetLocation);
        Assert.Equal(11, step.TargetTileX);
        Assert.Equal(10, step.TargetTileY);
        Assert.Contains(step.ExpectedEffects, effect =>
            effect == "current_location.obstacle[11,10]=clear;clear_kind=grass;source=Grass");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "max_tool_swings" && parameter.Value == "8");
    }

    [Fact]
    public void CompileTurnsClearObstacleCandidateWithStandTileIntoMoveThenClearSteps()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "clear:Farm:13,10:grass",
            Kind = "clear_obstacle_tile",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "Farm",
            TileX = 13,
            TileY = 10,
            ExpectedEffect = "move_to_adjacent=12,10;current_location.obstacle[13,10]=clear;clear_kind=grass;source=Grass",
            EstimatedTicks = 180
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        Assert.Equal(2, plan.Steps.Length);
        Assert.Equal("move_to_tile", plan.Steps[0].Kind);
        Assert.Equal("Farm", plan.Steps[0].TargetLocation);
        Assert.Equal(12, plan.Steps[0].TargetTileX);
        Assert.Equal(10, plan.Steps[0].TargetTileY);
        Assert.Equal("clear_obstacle", plan.Steps[1].Kind);
        Assert.Equal(13, plan.Steps[1].TargetTileX);
        Assert.Equal(10, plan.Steps[1].TargetTileY);
        Assert.Contains(plan.Steps[1].Preconditions, condition => condition == "target_tile_adjacent=true");
    }

    [Fact]
    public void CompileTurnsPlantSeedCandidateIntoSingleTilePlantStep()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "plant:Farm:5,6:472",
            Kind = "plant_seed_tile",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "Farm",
            TileX = 5,
            TileY = 6,
            ItemId = "472",
            QualifiedItemId = "(O)472",
            SlotIndex = 0,
            Quantity = 3,
            ExpectedEffect = "current_location.planting_context[5,6].has_crop=true;player.seed_inventory[472].stack_decreases;seed_id=472;adjusted_grow_days=4;days_remaining_in_season=20;harvest_item_id=24;harvest_item_qualified_id=(O)24;harvest_unit_sale_price=35;harvest_min_stack=1;harvest_max_stack=3;harvest_max_increase_per_farming_level=0;extra_harvest_chance=0.25;harvest_min_quality=0;harvest_max_quality=4;harvest_method=Grab;regrow_days=4;expected_first_harvest_value=35;expected_first_harvest_quantity=1;expected_first_harvest_value_basis=conservative_min_stack_only;estimated_first_harvest_quantity=2.3333;estimated_first_harvest_value=81.6667;estimated_first_harvest_value_basis=mean_stack_plus_extra_chance_quality0_no_farming_scaling;estimated_regrow_harvest_count=4;estimated_total_harvest_count=5;expected_season_harvest_value=175;estimated_season_harvest_value=408.3333;seed_unit_cost=20;expected_first_harvest_net_value=15;estimated_first_harvest_net_value=61.6667;expected_season_harvest_net_value=155;estimated_season_harvest_net_value=388.3333;season_harvest_value_basis=first_harvest_value_times_transparent_regrow_count_seed_cost_once;regrow_estimate_basis=adjusted_grow_days_days_remaining_regrow_days;net_value_basis=transparent_seed_unit_cost_subtracted",
            EstimatedTicks = 60
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        var step = Assert.Single(plan.Steps);
        Assert.Equal("plant_seed", step.Kind);
        Assert.Equal("Farm", step.TargetLocation);
        Assert.Equal(5, step.TargetTileX);
        Assert.Equal(6, step.TargetTileY);
        Assert.Contains(step.Preconditions, condition => condition == "hard_rule_allows_planting=true");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "seed_id" && parameter.Value == "472");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "qualified_item_id" && parameter.Value == "(O)472");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "seed_stack_available" && parameter.Value == "3");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "maturity_slack_days" && parameter.Value == "16");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "harvest_item_id" && parameter.Value == "24");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "expected_first_harvest_value" && parameter.Value == "35");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "expected_first_harvest_quantity" && parameter.Value == "1");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "expected_first_harvest_value_basis" && parameter.Value == "conservative_min_stack_only");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "harvest_method" && parameter.Value == "Grab");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "regrow_days" && parameter.Value == "4");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "extra_harvest_chance" && parameter.Value == "0.25");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "estimated_first_harvest_quantity" && parameter.Value == "2.3333");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "estimated_first_harvest_value" && parameter.Value == "81.6667");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "estimated_first_harvest_value_basis" && parameter.Value == "mean_stack_plus_extra_chance_quality0_no_farming_scaling");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "estimated_regrow_harvest_count" && parameter.Value == "4");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "estimated_total_harvest_count" && parameter.Value == "5");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "expected_season_harvest_value" && parameter.Value == "175");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "estimated_season_harvest_value" && parameter.Value == "408.3333");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "seed_unit_cost" && parameter.Value == "20");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "expected_first_harvest_net_value" && parameter.Value == "15");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "estimated_first_harvest_net_value" && parameter.Value == "61.6667");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "expected_season_harvest_net_value" && parameter.Value == "155");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "estimated_season_harvest_net_value" && parameter.Value == "388.3333");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "season_harvest_value_basis" && parameter.Value == "first_harvest_value_times_transparent_regrow_count_seed_cost_once");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "regrow_estimate_basis" && parameter.Value == "adjusted_grow_days_days_remaining_regrow_days");
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "net_value_basis" && parameter.Value == "transparent_seed_unit_cost_subtracted");
        Assert.Contains(step.SafetyConstraints, constraint =>
            constraint == "target_seed_tile_from_transparent_planting_context");
        Assert.Contains(step.SafetyConstraints, constraint =>
            constraint == "maturity_timing_from_transparent_planting_context");
        Assert.Contains(step.SafetyConstraints, constraint =>
            constraint == "harvest_value_from_transparent_crop_catalog_when_present");
    }

    [Fact]
    public void CompileSkipsSecondPlantSeedCandidateForSameTile()
    {
        var parsnip = PlantCandidate("plant:Farm:5,6:472", 5, 6, "472", stack: 3, score: 10);
        var bean = PlantCandidate("plant:Farm:5,6:473", 5, 6, "473", stack: 3, score: 9);

        var plan = new DailyPlanCompiler().Compile(new[] { parsnip, bean }, "state.1", maxCandidates: 4);

        var step = Assert.Single(plan.Steps);
        Assert.Equal("plant_seed", step.Kind);
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "seed_id" && parameter.Value == "472");
        Assert.Equal(2, plan.CandidateAudit.Length);
        Assert.Equal("accepted", plan.CandidateAudit[0].Decision);
        Assert.Equal("skipped", plan.CandidateAudit[1].Decision);
        Assert.Contains("daily_plan_target_tile_already_reserved", plan.CandidateAudit[1].Reasons);
    }

    [Fact]
    public void CompileSkipsPlantSeedCandidatesWhenSeedStackAlreadyReserved()
    {
        var first = PlantCandidate("plant:Farm:5,6:472", 5, 6, "472", stack: 1, score: 10);
        var second = PlantCandidate("plant:Farm:5,7:472", 5, 7, "472", stack: 1, score: 9);

        var plan = new DailyPlanCompiler().Compile(new[] { first, second }, "state.1", maxCandidates: 4);

        var step = Assert.Single(plan.Steps);
        Assert.Equal("plant_seed", step.Kind);
        Assert.Equal(6, step.TargetTileY);
        Assert.Contains(step.Parameters, parameter =>
            parameter.Name == "seed_stack_available" && parameter.Value == "1");
        Assert.Equal(2, plan.CandidateAudit.Length);
        Assert.Equal("accepted", plan.CandidateAudit[0].Decision);
        Assert.Equal("skipped", plan.CandidateAudit[1].Decision);
        Assert.Contains("daily_plan_seed_stack_already_reserved", plan.CandidateAudit[1].Reasons);
    }

    private static PolicyEventCandidatePrediction WaterCandidate(string candidateId, int x, int y)
    {
        return new PolicyEventCandidatePrediction
        {
            CandidateId = candidateId,
            Kind = "water_crop_tile",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "Farm",
            TileX = x,
            TileY = y,
            ExpectedEffect = "farm.crops[" + x + "," + y + "].needs_watering=false",
            EstimatedTicks = 60,
            EnergyCost = 2
        };
    }

    private static PolicyEventCandidatePrediction PlantCandidate(string candidateId, int x, int y, string seedId, int stack, double score)
    {
        return new PolicyEventCandidatePrediction
        {
            CandidateId = candidateId,
            Kind = "plant_seed_tile",
            Rank = 1,
            TimelineStatus = "ready_now",
            Score = score,
            LocationId = "Farm",
            TileX = x,
            TileY = y,
            ItemId = seedId,
            QualifiedItemId = "(O)" + seedId,
            SlotIndex = 0,
            Quantity = stack,
            ExpectedEffect = "current_location.planting_context[" + x + "," + y + "].has_crop=true;player.seed_inventory[" + seedId + "].stack_decreases;seed_id=" + seedId + ";adjusted_grow_days=4;days_remaining_in_season=20;expected_first_harvest_value=35",
            EstimatedTicks = 60
        };
    }

    private static PolicyEventCandidatePrediction MachineInputCandidate(string candidateId, int x, int y, int slotIndex, int stack, double score)
    {
        return new PolicyEventCandidatePrediction
        {
            CandidateId = candidateId,
            Kind = "load_machine_input_tile",
            Rank = 1,
            TimelineStatus = "ready_now",
            Score = score,
            LocationId = "Farm",
            TileX = x,
            TileY = y,
            ItemId = "262",
            QualifiedItemId = "(O)262",
            SlotIndex = slotIndex,
            Quantity = stack,
            ExpectedEffect = "move_to_adjacent=63,15;farm.machines[" + x + "," + y + "].minutes_until_ready>0_or_ready=true;input_slot_index=" + slotIndex + ";qualified_item_id=(O)262;item_id=262;input_stack_available=" + stack + ";input_sale_price=15;machine_input_opportunity_cost=15;machine_input_value_basis=transparent_input_sale_price_output_unknown;machine_output_rule_count=3;machine_has_output_rule=true;machine_output_prediction_status=machine_data_summary_not_input_specific;machine_input_probe_source=Object.performObjectDropInAction(probe:true);machine_input_executor_status=runtime_load",
            EstimatedTicks = 90,
            Available = true
        };
    }
    [Fact]
    public void CompileRecoveryCloseMenuEmitsCloseMenuPlanStep()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "recovery:close_blocking_menu",
            Kind = "recovery_close_menu",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "FarmHouse",
            EstimatedTicks = 10
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        var step = Assert.Single(plan.Steps);
        Assert.Equal("close_menu", step.Kind);
        Assert.Contains(step.SafetyConstraints, constraint => constraint == "recovery_menu_close");
        Assert.Equal("accepted", Assert.Single(plan.CandidateAudit).Decision);
    }

    [Fact]
    public void CompileRecoveryReturnHomeEmitsSleepPlanStep()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "recovery:return_home",
            Kind = "recovery_return_home",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "FarmHouse",
            TileX = 3,
            TileY = 9,
            EstimatedTicks = 240,
            Parameters = new[]
            {
                new SmallModelActionParameter { Name = "execution_option_id", Value = "executor.sleep" }
            }
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        var step = Assert.Single(plan.Steps);
        Assert.Equal("sleep", step.Kind);
        Assert.Contains(step.SafetyConstraints, constraint => constraint == "terminal_sleep_only_via_recovery_candidate");
        Assert.Equal("accepted", Assert.Single(plan.CandidateAudit).Decision);
    }

    [Fact]
    public void CompileRecoverySleepImmediatelyEmitsSleepPlanStep()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "recovery:sleep_immediately",
            Kind = "recovery_sleep_immediately",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "FarmHouse",
            TileX = 3,
            TileY = 9,
            EstimatedTicks = 120,
            Parameters = new[]
            {
                new SmallModelActionParameter { Name = "execution_option_id", Value = "executor.sleep" }
            }
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        Assert.Equal("sleep", Assert.Single(plan.Steps).Kind);
        Assert.Equal("accepted", Assert.Single(plan.CandidateAudit).Decision);
    }

    [Fact]
    public void CompileRecoveryResumeSleepPromptPreservesTypedMode()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "recovery:resume_sleep_prompt",
            Kind = "recovery_resume_sleep_prompt",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "FarmHouse",
            TileX = 9,
            TileY = 9,
            EstimatedTicks = 120,
            Parameters = new[]
            {
                new SmallModelActionParameter
                {
                    Name = "execution_option_id",
                    Value = "executor.sleep"
                },
                new SmallModelActionParameter
                {
                    Name = "sleep_resume_mode",
                    Value = "existing_exact_prompt"
                }
            }
        };

        var plan = new DailyPlanCompiler().Compile(
            new[] { candidate },
            "state.sleep.resume");

        var step = Assert.Single(plan.Steps);
        Assert.Equal("sleep", step.Kind);
        Assert.Contains(
            step.Parameters,
            parameter =>
                parameter.Name == "sleep_resume_mode" &&
                parameter.Value == "existing_exact_prompt");
    }

    [Fact]
    public void CompileRecoverySleepBeforeCollapseEmitsSleepPlanStep()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "recovery:sleep_before_collapse",
            Kind = "recovery_sleep_before_collapse",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "FarmHouse",
            EstimatedTicks = 120,
            Parameters = new[]
            {
                new SmallModelActionParameter { Name = "execution_option_id", Value = "executor.sleep" }
            }
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        Assert.Equal("sleep", Assert.Single(plan.Steps).Kind);
        Assert.Equal("accepted", Assert.Single(plan.CandidateAudit).Decision);
    }

    [Fact]
    public void CompileRecoveryOutsideHomeEmitsOneExactConnectorStep()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "recovery:return_home",
            Kind = "recovery_return_home",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "Town",
            TileX = 43,
            TileY = 23,
            EstimatedTicks = 180,
            Parameters = new[]
            {
                new SmallModelActionParameter { Name = "execution_option_id", Value = "executor.traverse_connector" },
                new SmallModelActionParameter { Name = "connector_kind", Value = "warp" },
                new SmallModelActionParameter { Name = "expected_target_location", Value = "Farm" },
                new SmallModelActionParameter { Name = "expected_arrival_tile_x", Value = "10" },
                new SmallModelActionParameter { Name = "expected_arrival_tile_y", Value = "11" },
                new SmallModelActionParameter { Name = "max_movement_tiles", Value = "3" },
                new SmallModelActionParameter { Name = "estimated_ticks", Value = "180" },
                new SmallModelActionParameter { Name = "estimated_minutes", Value = "3" },
                new SmallModelActionParameter { Name = "compiler_context.remaining_connector_count", Value = "2" }
            }
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        var step = Assert.Single(plan.Steps);
        Assert.Equal("traverse_connector", step.Kind);
        Assert.Equal("Town", step.TargetLocation);
        Assert.Equal(43, step.TargetTileX);
        Assert.Equal(23, step.TargetTileY);
        Assert.Equal(3, step.EstimatedMinutes);
        Assert.Contains(step.Parameters, parameter => parameter.Name == "connector_kind" && parameter.Value == "warp");
        Assert.Contains(step.Parameters, parameter => parameter.Name == "expected_target_location" && parameter.Value == "Farm");
        Assert.Contains(step.SafetyConstraints, constraint => constraint == "one_connector_per_recovery_replan");
    }

    [Fact]
    public void CompileRouteConnectorCandidatePreservesExactTransparentConnector()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "route:Farm:12,10:warp",
            Kind = "route_connector_tile",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "Farm",
            TileX = 12,
            TileY = 10,
            EstimatedTicks = 180,
            Parameters = new[]
            {
                new SmallModelActionParameter { Name = "execution_option_id", Value = "executor.traverse_connector" },
                new SmallModelActionParameter { Name = "connector_kind", Value = "warp" },
                new SmallModelActionParameter { Name = "expected_target_location", Value = "Town" },
                new SmallModelActionParameter { Name = "expected_arrival_tile_x", Value = "1" },
                new SmallModelActionParameter { Name = "expected_arrival_tile_y", Value = "2" },
                new SmallModelActionParameter { Name = "max_movement_tiles", Value = "3" },
                new SmallModelActionParameter { Name = "estimated_ticks", Value = "180" },
                new SmallModelActionParameter { Name = "estimated_minutes", Value = "3" }
            }
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        var step = Assert.Single(plan.Steps);
        Assert.Equal("traverse_connector", step.Kind);
        Assert.Equal("Farm", step.TargetLocation);
        Assert.Equal(12, step.TargetTileX);
        Assert.Equal(10, step.TargetTileY);
        Assert.Equal(3, step.EstimatedMinutes);
        Assert.Contains(step.Parameters, parameter => parameter.Name == "connector_kind" && parameter.Value == "warp");
        Assert.Contains(step.Parameters, parameter => parameter.Name == "expected_target_location" && parameter.Value == "Town");
        Assert.Contains(step.Parameters, parameter => parameter.Name == "expected_arrival_tile_x" && parameter.Value == "1");
        Assert.Contains(step.SafetyConstraints, constraint => constraint == "one_connector_per_replan");
    }

    [Fact]
    public void CompileRecoveryCandidateWithoutExecutionOptionFailsClosed()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "recovery:return_home",
            Kind = "recovery_return_home",
            Rank = 1,
            TimelineStatus = "ready_now",
            LocationId = "Town",
            EstimatedTicks = 180
        };

        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, "state.1");

        Assert.Empty(plan.Steps);
        Assert.Equal("skipped", Assert.Single(plan.CandidateAudit).Decision);
    }
}
