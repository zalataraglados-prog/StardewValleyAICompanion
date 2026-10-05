namespace StardewAI.Core.Tests;

public sealed class FullShipmentAcquisitionSampleSourceGuardTests
{
    [Fact]
    public void RadioactiveSampleUsesBoundedTrainingMiningSnapshotProfile()
    {
        var script = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");
        var bridge = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "ModEntry.cs");

        Assert.Contains(
            "$Scenario -eq \"radioactive_ore_node_sample\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"training_mining\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--execution-snapshot-profile\", $SnapshotProfile",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--after-snapshot-poll-ms\", \"250\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "profile=$SnapshotProfile&fresh=1",
            script,
            StringComparison.Ordinal);

        Assert.Contains(
            "profile is \"training_mining\"",
            bridge,
            StringComparison.Ordinal);
        foreach (var domain in new[]
        {
            "farm",
            "current_location",
            "locations",
            "mining",
            "world_progress"
        })
        {
            Assert.Contains(
                $"domains.Add(\"{domain}\")",
                bridge,
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BerryBushSampleReusesNativeBushAcquisitionChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"berry_bush_harvest_sample\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"berry_bush_harvest_sample\" { \"full_shipment:item:296\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"berry_bush_harvest_sample\" { \"(O)296\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"berry_bush_harvest_sample\" { \"foraging.harvest_bushes\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"debug.setup_forage_source_fixture\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Slug = \"berry-bush\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "BushProfile = \"berry_standard\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"berry_standard\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GingerSampleReusesNativeGingerAcquisitionChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"ginger_harvest_sample\" { \"full_shipment:item:829\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"ginger_harvest_sample\" { \"(O)829\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"ginger_harvest_sample\" { \"foraging.harvest_ginger\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RuleKey = \"ginger\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "GingerProfile = \"dry_standard\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TeaBushSampleReusesNativeBushAcquisitionChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"tea_bush_harvest_sample\" { \"full_shipment:item:815\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"tea_bush_harvest_sample\" { \"(O)815\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"tea_bush_harvest_sample\" { \"foraging.harvest_bushes\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Slug = \"tea-bush\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "BushProfile = \"tea_leaf\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void WildTreeSeedDropSampleReusesNativeTreeProductChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"wild_tree_seed_drop_sample\" { \"full_shipment:item:408\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"wild_tree_seed_drop_sample\" { \"(O)408\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"wild_tree_seed_drop_sample\" { \"foraging.harvest_tree_product\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Slug = \"wild-tree-seed-drop\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RuleKey = \"wild_tree\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "WildTreeProfile = \"fall_hazelnut\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void IslandWildTreeSeedSampleUsesExactIslandRoot()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"wild_tree_seed_sample\" { \"full_shipment:item:88\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"wild_tree_seed_sample\" { \"(O)88\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"wild_tree_seed_sample\" { \"foraging.harvest_tree_product\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "WildTreeProfile = \"island_palm\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "LocationId = \"IslandSouth\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "$acquisitionRootLocationId = if ($null -ne $forageFixture)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (-not $sampleProofOnly -and $initialTotalDay -ne 0)",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SpringOnionSampleReusesNativeCropHarvestChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"spring_onion_harvest_sample\" { \"full_shipment:item:399\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"spring_onion_harvest_sample\" { \"(O)399\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"spring_onion_harvest_sample\" { \"foraging.harvest_spring_onions\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Slug = \"spring-onion\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RuleKey = \"spring_onion\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"debug.setup_harvest_crop_target\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LocationForageSampleReusesNativeSpawnedObjectChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"location_forage_spawn_sample\" { \"full_shipment:item:16\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"location_forage_spawn_sample\" { \"(O)16\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"location_forage_spawn_sample\" { \"foraging.collect_spawned_objects\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RuleKey = \"spawned_object\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "SpawnedObjectProfile = \"ordinary\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "LocationId = \"Forest\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FruitTreeSampleReusesNativeFruitTreeChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"fruit_tree_harvest_sample\" { \"full_shipment:item:638\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"fruit_tree_harvest_sample\" { \"(O)638\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"fruit_tree_harvest_sample\" { \"foraging.harvest_fruit_tree\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RuleKey = \"fruit_tree\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "FruitTreeProfile = \"single_normal\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FarmAnimalProductSampleReusesNativeMilkPailChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"farm_animal_product_sample\" { \"full_shipment:item:184\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"farm_animal_product_sample\" { \"(O)184\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"farm_animal_product_sample\" { \"farm.collect_animal_products\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"debug.setup_animal_product_target\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RequiredToolKind = \"Milk Pail\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "QualifiedItemId = \"(O)184\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExpectedAnimalCrackerMultiplier = 1",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DeluxeFarmAnimalProductSampleReusesNativeMilkPailChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"farm_animal_deluxe_product_sample\" { \"full_shipment:item:186\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"farm_animal_deluxe_product_sample\" { \"(O)186\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"farm_animal_deluxe_product_sample\" { \"farm.collect_animal_products\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Slug = \"farm-animal-deluxe-product\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "QualifiedItemId = \"(O)186\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FishPondOutputSampleReusesNativePondCollectionChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"fish_pond_output_sample\" { \"full_shipment:item:812\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"fish_pond_output_sample\" { \"(O)812\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"fish_pond_output_sample\" { \"fishing.service_fish_ponds\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"debug.setup_fish_pond_output\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "FishTypeItemId = \"(O)698\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "QualifiedItemId = \"(O)812\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MachineOutputSampleBindsMushroomLogSource()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"machine_output_sample\" { \"full_shipment:item:257\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"machine_output_sample\" { \"(O)257\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"machine_output_sample\" { \"farm.collect_machine_outputs\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"debug.setup_machine_output_target\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "MachineItemId = \"128\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "QualifiedItemId = \"(O)257\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SolarPanelOutputSampleBindsNativeSolarPanelSource()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"solar_panel_output_sample\" { \"full_shipment:item:787\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"solar_panel_output_sample\" { \"(O)787\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"solar_panel_output_sample\" { \"farm.collect_machine_outputs\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Slug = \"solar-panel-output\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "MachineItemId = \"231\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "QualifiedItemId = \"(O)787\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MachineQuerySamplesBindDistinctNativeSourceLayers()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MachinesAndPickup.cs");
        var contracts = ReadRepositoryFile(
            "src",
            "StardewAI.Contracts",
            "Training",
            "TrainingExecutionRequest.MachineLifecycle.cs");

        Assert.Contains(
            "\"machine_flavored_output_sample\" { \"full_shipment:item:340\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"machine_flavored_output_sample\" { \"native_machine_flavored_output\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Slug = \"machine-flavored-output\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "MachineItemId = \"10\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"machine_item_query_output_sample\" { \"full_shipment:item:257\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"machine_item_query_output_sample\" { \"native_machine_item_query_output\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Slug = \"machine-item-query-output\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "LastOutputRuleId = \"Default\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "fixture_machine_last_output_rule_id",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "FixtureMachineLastOutputRuleId",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "machine.lastOutputRuleId.Value = fixtureOutputRuleId",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "fixture_machine_last_output_rule_id",
            contracts,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TreeMossSampleReusesNativeScytheHarvestChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"tree_moss_harvest_sample\" { \"full_shipment:item:Moss\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"tree_moss_harvest_sample\" { \"(O)Moss\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"tree_moss_harvest_sample\" { \"foraging.harvest_tree_moss\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"debug.setup_clear_obstacle\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Slug = \"tree-moss-harvest\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "RuleKey = \"tree_moss\"",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RadioactiveOreSampleUsesNativeNodeAndMiningChain()
    {
        var runner = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.RadioactiveOreNodeFixture.cs");

        Assert.Contains(
            "\"radioactive_ore_node_sample\" { \"full_shipment:item:909\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"radioactive_ore_node_sample\" { \"(O)909\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"radioactive_ore_node_sample\" { \"mining.reach_depth\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"debug.setup_mining_floor\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"debug.setup_radioactive_ore_node\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "name = \"target_depth\"; value = \"100\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "-Parameters $sampleRankingParameters",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "ItemRegistry.Create<StardewValley.Object>(\"(O)95\")",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "mine.objects.Clear()",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "mine.resourceClumps.Clear()",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "mine.objects[tile] = oreNode",
            fixture,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ItemRegistry.Create<StardewValley.Object>(\"(O)909\")",
            fixture,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RadioactiveOreSampleUsesTheSharedPlanningAxes()
    {
        var calendar = ReadRepositoryFile(
            "experiments",
            "StardewAI.GoalConditionedBootstrap",
            "AcquisitionRouteCalendarResolutionBuilder.RadioactiveOre.cs");
        var location = ReadRepositoryFile(
            "experiments",
            "StardewAI.GoalConditionedBootstrap",
            "AcquisitionLocationRouteTargetResolver.cs");
        var candidates = ReadRepositoryFile(
            "experiments",
            "StardewAI.GoalConditionedBootstrap",
            "AcquisitionCurrentRouteCandidateIndex.cs");
        var retry = ReadRepositoryFile(
            "experiments",
            "StardewAI.GoalConditionedBootstrap",
            "AcquisitionRouteTargetDateStochasticRetryBuilder.Evaluation.cs");
        var dailyBudget = ReadRepositoryFile(
            "experiments",
            "StardewAI.GoalConditionedBootstrap",
            "AcquisitionRouteTargetDateDailyTimeEnergyBuilder.Evaluation.cs");
        var dispatchVerification = ReadRepositoryFile(
            "experiments",
            "StardewAI.GoalConditionedBootstrap",
            "AcquisitionRouteDispatchCompilationBuilder.Verification.cs");

        Assert.Contains(
            "route.SourceId != \"GameLocation.breakStone\"",
            calendar,
            StringComparison.Ordinal);
        Assert.Contains(
            "route.SourcePath != \"stone 95 => (O)909\"",
            calendar,
            StringComparison.Ordinal);
        Assert.Contains(
            "RequiresExistingLiveCandidateMatch = true",
            calendar,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"native_radioactive_ore_node\" or",
            location,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (routeKind == \"native_radioactive_ore_node\")",
            candidates,
            StringComparison.Ordinal);
        Assert.Contains(
            "current_mine_object_terminal_evidence_incomplete",
            candidates,
            StringComparison.Ordinal);
        Assert.Contains(
            "BuildRollingMiningCandidates(snapshot)",
            candidates,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (staticRoute.RouteKind == \"native_radioactive_ore_node\")",
            retry,
            StringComparison.Ordinal);
        Assert.Contains(
            "matching_guaranteed_radioactive_ore_node_candidate_not_present",
            retry,
            StringComparison.Ordinal);
        Assert.Contains(
            "state.mining.objects.value[].guaranteed_drop_qualified_item_ids",
            retry,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"native_radioactive_ore_node\" => EvaluateRadioactiveOreNode(",
            dailyBudget,
            StringComparison.Ordinal);
        Assert.Contains(
            "candidate.parameters[max_movement_tiles]",
            dailyBudget,
            StringComparison.Ordinal);
        Assert.Contains(
            "candidate.parameters[max_tool_swings]",
            dailyBudget,
            StringComparison.Ordinal);
        Assert.Contains(
            "BuildRollingMiningParameters(snapshot)",
            dispatchVerification,
            StringComparison.Ordinal);
        Assert.Contains(
            "route_dispatch_current_mine_intent_unavailable",
            dispatchVerification,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSamplesRejectBindingsForTheWrongAcquisitionLayer()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        var expectedRouteMappings = new[]
        {
            "\"parsnip_harvest_sample\" { \"harvests_as\" }",
            "\"berry_bush_harvest_sample\" { \"native_bush_shake\" }",
            "\"ginger_harvest_sample\" { \"native_ginger_harvest\" }",
            "\"tea_bush_harvest_sample\" { \"native_tea_bush_harvest\" }",
            "\"wild_tree_seed_drop_sample\" { \"native_wild_tree_seed_drop\" }",
            "\"wild_tree_seed_sample\" { \"native_wild_tree_seed\" }",
            "\"spring_onion_harvest_sample\" { \"native_spring_onion_harvest\" }",
            "\"location_forage_spawn_sample\" { \"native_location_forage_spawn\" }",
            "\"fruit_tree_harvest_sample\" { \"native_fruit_tree_produce\" }",
            "\"farm_animal_product_sample\" { \"native_farm_animal_produce\" }",
            "\"farm_animal_deluxe_product_sample\" { \"native_farm_animal_deluxe_produce\" }",
            "\"fish_pond_output_sample\" { \"native_fish_pond_output\" }",
            "\"machine_output_sample\" { \"machine_output\" }",
            "\"machine_flavored_output_sample\" { \"native_machine_flavored_output\" }",
            "\"machine_item_query_output_sample\" { \"native_machine_item_query_output\" }",
            "\"solar_panel_output_sample\" { \"native_solar_panel_output\" }",
            "\"tree_moss_harvest_sample\" { \"native_tree_moss_harvest\" }",
            "\"radioactive_ore_node_sample\" { \"native_radioactive_ore_node\" }",
            "default { \"native_wild_tree_chop_drop\" }",
        };
        foreach (var mapping in expectedRouteMappings)
        {
            Assert.Contains(
                mapping,
                source,
                StringComparison.Ordinal);
        }

        Assert.Contains(
            "$actualBindingTuple = @(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "[string]$executionBinding.requirement_id",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "[string]$executionBinding.qualified_item_id",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "[string]$executionBinding.route_kind",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "$sampleExpectedRouteKind",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Execution binding selected the wrong authoritative route.",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SampleDateDoesNotRelaxNativeSapRecurrenceRoot()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "$sampleProofOnly = $Scenario -ne \"sap_prefix\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (-not $sampleProofOnly -and $initialTotalDay -ne 0)",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--target-total-day\", ([string]$initialTotalDay)",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ResumeProofTakesSampleIdentityFromVerifiedBinding()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Resume-RuntimeFullShipmentAcquisitionProof.ps1");

        Assert.Contains(
            "$requirementId = [string]$binding.requirement_id",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "$qualifiedItemId = [string]$binding.qualified_item_id",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:296|(O)296|native_bush_shake",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:829|(O)829|native_ginger_harvest",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:815|(O)815|native_tea_bush_harvest",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:408|(O)408|native_wild_tree_seed_drop",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:88|(O)88|native_wild_tree_seed",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:399|(O)399|native_spring_onion_harvest",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:16|(O)16|native_location_forage_spawn",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:638|(O)638|native_fruit_tree_produce",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:184|(O)184|native_farm_animal_produce",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:186|(O)186|native_farm_animal_deluxe_produce",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:812|(O)812|native_fish_pond_output",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:257|(O)257|machine_output",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:340|(O)340|native_machine_flavored_output",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:257|(O)257|native_machine_item_query_output",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:787|(O)787|native_solar_panel_output",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:Moss|(O)Moss|native_tree_moss_harvest",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:909|(O)909|native_radioactive_ore_node",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "scenario = $scenario",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "$scenario -eq \"radioactive_ore_node_sample\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"training_mining\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "snapshots?profile=$SnapshotProfile",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "snapshots?profile=full",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "requirement_id = $requirementId",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "qualified_item_id = $qualifiedItemId",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "requirement_id = \"full_shipment:item:24\"",
            source,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(
                directory.FullName,
                "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException(
                "Cannot find repository root."),
            Path.Combine(segments)));
    }
}
