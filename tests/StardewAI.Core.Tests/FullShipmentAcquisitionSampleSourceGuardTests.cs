namespace StardewAI.Core.Tests;

public sealed class FullShipmentAcquisitionSampleSourceGuardTests
{
    [Fact]
    public void RadioactiveSampleUsesBoundedTrainingMiningSnapshotProfile()
    {
        var script = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");
        var common = ReadRepositoryFile(
            "scripts",
            "lib",
            "RuntimeEvidenceCommon.ps1");
        var runtimeSurface = script + Environment.NewLine + common;
        var bridge = ReadRepositoryFile(
            "src",
            "StardewAI.TransparentBridge",
            "ModEntry.cs");

        Assert.Contains(
            "$Scenario -in @(",
            runtimeSurface,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"radioactive_ore_node_sample\",",
            runtimeSurface,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"monster_drop_sample\"))",
            runtimeSurface,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"training_mining\"",
            runtimeSurface,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--execution-snapshot-profile\", $Context.SnapshotProfile",
            runtimeSurface,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--after-snapshot-poll-ms\", \"250\"",
            runtimeSurface,
            StringComparison.Ordinal);
        Assert.Contains(
            "profile=$SnapshotProfile&fresh=1",
            runtimeSurface,
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
    public void RuntimeEvidenceOrchestrationUsesScenarioNeutralSharedHelpers()
    {
        var script = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");
        var common = ReadRepositoryFile(
            "scripts",
            "lib",
            "RuntimeEvidenceCommon.ps1");

        Assert.Contains(
            "lib\\RuntimeEvidenceCommon.ps1",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "function Invoke-RuntimePrecompiledQueue",
            common,
            StringComparison.Ordinal);
        Assert.Contains(
            "function Invoke-RuntimeTeacherPreferenceQueue",
            common,
            StringComparison.Ordinal);
        Assert.Contains(
            "function Invoke-RuntimeDailyPlanStep",
            common,
            StringComparison.Ordinal);
        Assert.Contains(
            "function Start-RuntimeEvidenceProcess",
            common,
            StringComparison.Ordinal);
        Assert.Contains(
            "$nativeOutput = @(& dotnet $BootstrapDll @Arguments 2>&1)",
            common,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "& dotnet $BootstrapDll @Arguments | Out-Null",
            common,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "function Invoke-JsonPost",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "--precompiled-queue",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "-c Release --no-restore",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "release-build-$projectName.log",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "transparent-bridge-deploy.log",
            script,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "full_shipment",
            common,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            "sap_prefix",
            common,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "function Assert-FullShipmentState",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSamplePlanCoversEveryRunnerScenarioExactlyOnce()
    {
        var script = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");
        var milestone = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentEvidenceMilestone.ps1");
        var planJson = ReadRepositoryFile(
            "catalogs",
            "vanilla-1.6.15",
            "full-shipment-runtime-sample-plan.json");
        using var document = System.Text.Json.JsonDocument.Parse(planJson);
        var root = document.RootElement;
        var entries = root.GetProperty("entries")
            .EnumerateArray()
            .ToArray();

        Assert.Equal(
            "full_shipment_runtime_sample_plan.v1",
            root.GetProperty("schema_version").GetString());
        Assert.Equal(26, root.GetProperty("expected_stratum_count").GetInt32());
        Assert.False(root.GetProperty("formal_training_authorized").GetBoolean());
        Assert.Equal(26, entries.Length);
        Assert.Equal(26, entries.Select(entry =>
                entry.GetProperty("scenario").GetString())
            .Distinct(StringComparer.Ordinal)
            .Count());
        Assert.Equal(26, entries.Select(entry =>
                entry.GetProperty("route_kind").GetString())
            .Distinct(StringComparer.Ordinal)
            .Count());
        Assert.Equal(7, entries.Count(entry =>
            entry.GetProperty("run_batch").GetString() == "high_risk"));
        Assert.Single(entries.Where(entry =>
            entry.GetProperty("shared_shipping_anchor").GetBoolean()));

        foreach (var entry in entries)
        {
            foreach (var property in new[]
            {
                "scenario",
                "requirement_id",
                "qualified_item_id",
                "route_kind",
                "endpoint_option_id"
            })
            {
                Assert.Contains(
                    entry.GetProperty(property).GetString()!,
                    script,
                    StringComparison.Ordinal);
            }
        }

        Assert.Contains("[switch] $PlanOnly", milestone, StringComparison.Ordinal);
        Assert.Contains(
            "[string] $RuntimeRoot = \"F:\\StardewAI-TestLab\\runtime\"",
            milestone,
            StringComparison.Ordinal);
        Assert.Contains(
            "F:\\StardewAI-TestLab\\inputs\\fresh-save\\ProofFarm_450250338",
            milestone,
            StringComparison.Ordinal);
        foreach (var inputBinding in new[]
        {
            "RequirementInventory = $RequirementInventory",
            "AcquisitionLowering = $AcquisitionLowering",
            "MasterAnglerWindows = $MasterAnglerWindows",
            "RouteTimingCalibration = $RouteTimingCalibration"
        })
        {
            Assert.Contains(inputBinding, milestone, StringComparison.Ordinal);
        }
        Assert.Contains(
            "Write-MilestoneState -Status \"failed\"",
            milestone,
            StringComparison.Ordinal);
        Assert.DoesNotContain("retry", milestone, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1",
            milestone,
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
    public void WildTreeTapperSampleUsesSameTileNativeTreeAndMachineChain()
    {
        var source = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.MachinesAndPickup.cs");

        Assert.Contains(
            "\"wild_tree_tapper_output_sample\" { \"full_shipment:item:725\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"wild_tree_tapper_output_sample\" { \"(O)725\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"wild_tree_tapper_output_sample\" { \"native_wild_tree_tapper_output\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "MachineItemId = \"105\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "TapperTreeType = \"1\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "fixture_machine_tapper_tree_type",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "new Tree(",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "fixtureTapperTree.tapped.Value = true",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "machine.IsTapper()",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "ReferenceEquals(feature, fixtureTapperTree)",
            fixture,
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
            "FixtureOptionId = \"debug.setup_radioactive_ore_node\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = [string]$miningFixture.FixtureOptionId",
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
    public void MonsterDropSampleReusesMiningCombatAndDeferredPickupChain()
    {
        var runner = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.QuestMonsterDropFixture.cs");

        Assert.Contains(
            "\"monster_drop_sample\" { \"full_shipment:item:766\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"monster_drop_sample\" { \"(O)766\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"monster_drop_sample\" { \"native_monster_drop_table\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "FixtureOptionId = \"debug.setup_quest_monster_drop_fixture\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "QuestId = \"stardewai.full-shipment.monster-drop\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "monsterTarget.objectsToDrop.Add(item.QualifiedItemId)",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "new GreenSlime(",
            fixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "new StardewValley.Tools.MeleeWeapon(\"9\")",
            fixture,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ArtifactSpotSampleSearchesTransparentNativeProjection()
    {
        var runner = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.ClearObstacleFixture.cs");

        Assert.Contains(
            "\"location_artifact_spot_sample\" { \"full_shipment:item:330\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"location_artifact_spot_sample\" { \"(O)330\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExpectedRouteKind = \"native_location_artifact_spot\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExpectedSourceId = \"location:Default:10\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "$projectedSpot.clear_authoritative_route_sources",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "$projectedSpot.clear_output_items",
            runner,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ItemRegistry.Create(\"(O)330\")",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "pair.Value.QualifiedItemId == \"(O)590\"",
            fixture,
            StringComparison.Ordinal);
    }

    [Fact]
    public void GeodeDropSampleSearchesNativeRngAndUsesSharedBlacksmithChain()
    {
        var runner = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.GeodeProcessingFixture.cs");

        Assert.Contains(
            "\"geode_drop_sample\" { \"full_shipment:item:386\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"geode_drop_sample\" { \"(O)386\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"geode_drop_sample\" { \"processing.crack_geode\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExpectedSourceId = \"geode:791:1:random:6\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"debug.setup_geode_processing\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "$projectedInput.authoritative_route_sources",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "foreach ($geodesCrackedBefore in 0..",
            runner,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ItemRegistry.Create(\"(O)386\")",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "Game1.currentLocation = blacksmith",
            fixture,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ShopPurchaseSampleBindsCarpenterAndUsesSharedPurchaseChain()
    {
        var runner = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");

        Assert.Contains(
            "\"shop_purchase_sample\" { \"full_shipment:item:388\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"shop_purchase_sample\" { \"(O)388\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"shop_purchase_sample\" { \"sells\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"shop_purchase_sample\" { \"economy.buy_supplies\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "name = \"continuation.shop_id\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "value = \"Carpenter\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"debug.advance_time_to\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "TargetTime = 900",
            runner,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CommunityCenterRewardSampleUsesExactPendingNativeReward()
    {
        var runner = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.CommunityCenterFixture.cs");

        Assert.Contains(
            "\"community_center_reward_sample\" { \"full_shipment:item:336\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"community_center_reward_sample\" { \"(O)336\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"community_center_reward_sample\" { \"creates_reward_item\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"community_center.donate_bundle_items\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExpectedSourceId = \"bundle:Bulletin Board/33:reward\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "community_center_fixture_case = \"pending_reward\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "$rewardBundle.reward.authoritative_route_sources",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "communityCenter.bundleRewards[target.BundleId] = true",
            fixture,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ItemRegistry.Create(\"(O)336\")",
            fixture,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LocationFishingSampleUsesNativeTownRuleAndSharedCatchChain()
    {
        var runner = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeFullShipmentSapPrefixSmoke.ps1");
        var fixture = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.LocationFishingFixture.cs");

        Assert.Contains(
            "\"location_fish_spawn_sample\" { \"full_shipment:item:388\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"location_fish_spawn_sample\" { \"(O)388\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"location_fish_spawn_sample\" { \"native_location_fish_spawn\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"location_fish_spawn_sample\" { \"fishing.catch_fish\" }",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExpectedSource = \"Data/Locations:Town\"",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExpectedSourceIndex = 3",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "profile=fishing_forecast&fresh=true&location_id=",
            runner,
            StringComparison.Ordinal);
        Assert.Contains(
            "new FishingRod(4)",
            fixture,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ItemRegistry.Create(\"(O)388\")",
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
            "\"wild_tree_tapper_output_sample\" { \"native_wild_tree_tapper_output\" }",
            "\"solar_panel_output_sample\" { \"native_solar_panel_output\" }",
            "\"tree_moss_harvest_sample\" { \"native_tree_moss_harvest\" }",
            "\"location_artifact_spot_sample\" { \"native_location_artifact_spot\" }",
            "\"geode_drop_sample\" { \"native_geode_drop\" }",
            "\"community_center_reward_sample\" { \"creates_reward_item\" }",
            "\"location_fish_spawn_sample\" { \"native_location_fish_spawn\" }",
            "\"shop_purchase_sample\" { \"sells\" }",
            "\"monster_drop_sample\" { \"native_monster_drop_table\" }",
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
    public void LocationFishingDispatchSeparatesNonFishOutcomesFromMasterAngler()
    {
        var selection = ReadRepositoryFile(
            "experiments",
            "StardewAI.GoalConditionedBootstrap",
            "AcquisitionRouteDispatchCompilationBuilder.Selection.cs");
        var verification = ReadRepositoryFile(
            "experiments",
            "StardewAI.GoalConditionedBootstrap",
            "AcquisitionRouteDispatchCompilationBuilder.Verification.cs");

        Assert.Contains(
            "TryMatchCompleteLocationFishingOutcome",
            selection,
            StringComparison.Ordinal);
        Assert.Contains(
            "outcome_distribution_complete",
            selection,
            StringComparison.Ordinal);
        Assert.Contains(
            "outcome_distribution_json",
            selection,
            StringComparison.Ordinal);
        Assert.Contains(
            "SnapshotDeclaresFishCollectionSpecies",
            selection,
            StringComparison.Ordinal);
        Assert.Contains(
            "route_dispatch_complete_location_fishing_outcome_missing",
            verification,
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
            "full_shipment:item:725|(O)725|native_wild_tree_tapper_output",
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
            "full_shipment:item:330|(O)330|native_location_artifact_spot",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:386|(O)386|native_geode_drop",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:336|(O)336|creates_reward_item",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:388|(O)388|native_location_fish_spawn",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:388|(O)388|sells",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "full_shipment:item:766|(O)766|native_monster_drop_table",
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
            "$scenario -in @(",
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
