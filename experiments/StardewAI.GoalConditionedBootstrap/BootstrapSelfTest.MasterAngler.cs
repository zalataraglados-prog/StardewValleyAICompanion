using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    public static void RunMasterAnglerFreshSavePrerequisiteAudit(
        string catalogPath,
        string windowIndexPath,
        string dependencyExpansionPath)
    {
        var report = MasterAnglerFreshSavePrerequisiteAuditBuilder.Build(
            catalogPath,
            windowIndexPath,
            dependencyExpansionPath);
        Require(
            report.Status == "partial_fresh_save_prerequisite_inventory" &&
            !report.TrainingLabelEligible &&
            report.NativeDenominatorCount == 72 &&
            report.RodLocationSpeciesCount == 60 &&
            report.MineOverrideOnlySpeciesCount == 2 &&
            report.TrapSpeciesCount == 10 &&
            report.SpeciesWithTwoOrMoreStaticOpportunityDays == 72 &&
            report.RainOrStormRequiredSpeciesCount == 4 &&
            report.MaximumRequiredFishingLevel == 10 &&
            report.StaticOpportunityInventoryComplete &&
            report.PrerequisiteFamilyInventoryComplete &&
            report.UnknownLocationIds.Length == 0,
            "Master Angler fresh-save prerequisite audit summary drifted.");

        var byId = report.Species.ToDictionary(
            value => value.QualifiedItemId,
            StringComparer.Ordinal);
        Require(
            byId["(O)162"].RoutePrerequisiteExpression ==
                "mine_depth_or_ginger_island_caldera" &&
            byId["(O)734"].RoutePrerequisiteExpression ==
                "secret_woods_access_or_forest_farm_layout" &&
            byId["(O)796"].RoutePrerequisiteExpression ==
                "mutant_bug_lair_story_access" &&
            byId["(O)715"].TrapRequiredIndependentPotDaysAt95Percent == 59 &&
            byId["(O)715"].TrapTheoreticalMinimumParallelPotCapacityFromDayOne == 1,
            "Master Angler fresh-save location or stochastic prerequisite classification drifted.");
        Require(
            report.PrerequisiteFamilies.Single(value =>
                    value.FamilyId == "crab_pot_lifecycle")
                .ProducerStatus == "implemented_and_bound" &&
            report.PrerequisiteFamilies.Single(value =>
                    value.FamilyId == "fishing_skill_progression")
                .ProducerStatus == "implemented_and_bound" &&
            !report.MissingOrUnboundProducerFamilies.Contains(
                "fishing_skill_progression",
                StringComparer.Ordinal) &&
            !report.MissingOrUnboundProducerFamilies.Contains(
                "mine_access_and_depth",
                StringComparer.Ordinal) &&
            !report.MissingOrUnboundProducerFamilies.Contains(
                "mine_depth_or_ginger_island_caldera",
                StringComparer.Ordinal) &&
            !report.MissingOrUnboundProducerFamilies.Contains(
                "one_of_advanced_location_access",
                StringComparer.Ordinal) &&
            report.PrerequisiteFamilies.Single(value =>
                    value.FamilyId == "mine_access_and_depth")
                .ProducerStatus == "implemented_and_bound" &&
            report.PrerequisiteFamilies.Single(value =>
                    value.FamilyId == "mine_depth_or_ginger_island_caldera")
                .ProducerStatus == "implemented_and_bound" &&
            report.PrerequisiteFamilies.Single(value =>
                    value.FamilyId == "one_of_advanced_location_access")
                .ProducerStatus == "implemented_and_bound" &&
            report.PrerequisiteFamilies.Single(value =>
                    value.FamilyId == "rod_and_fishing_equipment_acquisition")
                .ProducerStatus == "implemented_and_bound" &&
            !report.MissingOrUnboundProducerFamilies.Contains(
                "rod_and_fishing_equipment_acquisition",
                StringComparer.Ordinal) &&
            report.PrerequisiteFamilies.Single(value =>
                    value.FamilyId == "desert_bus_access")
                .ProducerStatus == "implemented_and_bound" &&
            !report.MissingOrUnboundProducerFamilies.Contains(
                "desert_bus_access",
                StringComparer.Ordinal) &&
            report.PrerequisiteFamilies.Single(value =>
                    value.FamilyId == "sewer_rusty_key_access")
                .ProducerStatus == "implemented_and_bound" &&
            !report.MissingOrUnboundProducerFamilies.Contains(
                "sewer_rusty_key_access",
                StringComparer.Ordinal) &&
            report.PrerequisiteFamilies.Single(value =>
                    value.FamilyId ==
                        "crab_pot_recipe_material_and_bait_supply")
                .ProducerStatus == "implemented_and_bound" &&
            !report.MissingOrUnboundProducerFamilies.Contains(
                "crab_pot_recipe_material_and_bait_supply",
                StringComparer.Ordinal) &&
            report.PrerequisiteFamilies.Single(value =>
                    value.FamilyId == "weather_reserve_or_control")
                .ProducerStatus ==
                    "runtime_consumer_crafting_read_and_scheduler_bound_material_supply_and_native_crafting_receipt_missing" &&
            report.MissingOrUnboundProducerFamilies.Contains(
                "weather_reserve_or_control",
                StringComparer.Ordinal) &&
            report.MissingOrUnboundProducerFamilies.Contains(
                "ginger_island_access",
                StringComparer.Ordinal),
            "Master Angler fresh-save producer coverage was overstated.");

        VerifyMasterAnglerFishingSkillBindingFailsClosed(
            catalogPath,
            windowIndexPath,
            dependencyExpansionPath);
        VerifyMasterAnglerMineAccessBindingFailsClosed(
            catalogPath,
            windowIndexPath,
            dependencyExpansionPath);
        VerifyMasterAnglerRodEquipmentBindingFailsClosed(
            catalogPath,
            windowIndexPath,
            dependencyExpansionPath);
        VerifyMasterAnglerDesertBusBindingFailsClosed(
            catalogPath,
            windowIndexPath,
            dependencyExpansionPath);
        VerifyMasterAnglerSewerAccessBindingFailsClosed(
            catalogPath,
            windowIndexPath,
            dependencyExpansionPath);
        VerifyMasterAnglerTrapSupplyBindingFailsClosed(
            catalogPath,
            windowIndexPath,
            dependencyExpansionPath);
        VerifyMasterAnglerOwnedRainTotemBindingFailsClosed(
            catalogPath,
            windowIndexPath,
            dependencyExpansionPath);
        VerifyMasterAnglerRainTotemCraftingReadFailsClosed(
            catalogPath,
            windowIndexPath,
            dependencyExpansionPath);
        VerifyMasterAnglerWeatherSupplySchedulerFailsClosed(
            catalogPath,
            windowIndexPath,
            dependencyExpansionPath);
    }

    private static void VerifyMasterAnglerWeatherSupplySchedulerFailsClosed(
        string catalogPath,
        string windowIndexPath,
        string dependencyExpansionPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(dependencyExpansionPath))?.AsObject() ??
                   throw new InvalidDataException("Master Angler dependency fixture is null.");
        var direction = root["directions"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["direction_id"]!.GetValue<string>() ==
                             "complete_master_angler");
        var scheduler = direction["nodes"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["id"]!.GetValue<string>() ==
                             "dependency.complete_master_angler.weather_supply_scheduler");
        scheduler["rule"]!["target_horizon_days"] = 2;

        var tamperedPath = Path.Combine(
            Path.GetTempPath(),
            "stardewai-master-angler-weather-scheduler-" +
            Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(
                tamperedPath,
                root.ToJsonString(JsonDefaults.Options));
            var tampered = MasterAnglerFreshSavePrerequisiteAuditBuilder.Build(
                catalogPath,
                windowIndexPath,
                tamperedPath);
            Require(
                tampered.PrerequisiteFamilies.Single(value =>
                        value.FamilyId == "weather_reserve_or_control")
                    .ProducerStatus ==
                        "runtime_consumer_and_crafting_read_bound_executor_supply_scheduler_missing",
                "Master Angler weather supply accepted a drifted tomorrow scheduler.");
        }
        finally
        {
            if (File.Exists(tamperedPath))
                File.Delete(tamperedPath);
        }
    }

    private static void VerifyMasterAnglerRainTotemCraftingReadFailsClosed(
        string catalogPath,
        string windowIndexPath,
        string dependencyExpansionPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(dependencyExpansionPath))?.AsObject() ??
                   throw new InvalidDataException("Master Angler dependency fixture is null.");
        var direction = root["directions"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["direction_id"]!.GetValue<string>() ==
                             "complete_master_angler");
        var readNode = direction["nodes"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["id"]!.GetValue<string>() ==
                             "dependency.complete_master_angler.rain_totem_crafting_read_authority");
        readNode["rule"]!["schema_version"] = "rain_totem_crafting.v0";

        var tamperedPath = Path.Combine(
            Path.GetTempPath(),
            "stardewai-master-angler-rain-totem-crafting-read-" +
            Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(
                tamperedPath,
                root.ToJsonString(JsonDefaults.Options));
            var tampered = MasterAnglerFreshSavePrerequisiteAuditBuilder.Build(
                catalogPath,
                windowIndexPath,
                tamperedPath);
            Require(
                tampered.PrerequisiteFamilies.Single(value =>
                        value.FamilyId == "weather_reserve_or_control")
                    .ProducerStatus ==
                        "runtime_consumer_bound_crafting_read_supply_and_scheduler_missing" &&
                tampered.MissingOrUnboundProducerFamilies.Contains(
                    "weather_reserve_or_control",
                    StringComparer.Ordinal),
                "Master Angler weather supply accepted a drifted Rain Totem crafting schema.");
        }
        finally
        {
            if (File.Exists(tamperedPath))
                File.Delete(tamperedPath);
        }
    }

    private static void VerifyMasterAnglerOwnedRainTotemBindingFailsClosed(
        string catalogPath,
        string windowIndexPath,
        string dependencyExpansionPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(dependencyExpansionPath))?.AsObject() ??
                   throw new InvalidDataException("Master Angler dependency fixture is null.");
        var direction = root["directions"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["direction_id"]!.GetValue<string>() ==
                             "complete_master_angler");
        var consumer = direction["nodes"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["id"]!.GetValue<string>() ==
                             "dependency.complete_master_angler.consume_owned_rain_totem");
        consumer["rule"]!["effective_tomorrow_weather"] = "Sun";

        var tamperedPath = Path.Combine(
            Path.GetTempPath(),
            "stardewai-master-angler-owned-rain-totem-binding-" +
            Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(
                tamperedPath,
                root.ToJsonString(JsonDefaults.Options));
            var tampered = MasterAnglerFreshSavePrerequisiteAuditBuilder.Build(
                catalogPath,
                windowIndexPath,
                tamperedPath);
            Require(
                tampered.PrerequisiteFamilies.Single(value =>
                        value.FamilyId == "weather_reserve_or_control")
                    .ProducerStatus ==
                        "current_state_gate_present_future_producer_missing" &&
                tampered.MissingOrUnboundProducerFamilies.Contains(
                    "weather_reserve_or_control",
                    StringComparer.Ordinal),
                "Master Angler weather consumer accepted a drifted effective tomorrow weather.");
        }
        finally
        {
            if (File.Exists(tamperedPath))
                File.Delete(tamperedPath);
        }
    }

    private static void VerifyMasterAnglerTrapSupplyBindingFailsClosed(
        string catalogPath,
        string windowIndexPath,
        string dependencyExpansionPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(dependencyExpansionPath))?.AsObject() ??
                   throw new InvalidDataException("Master Angler dependency fixture is null.");
        var direction = root["directions"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["direction_id"]!.GetValue<string>() ==
                             "complete_master_angler");
        var producer = direction["nodes"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["id"]!.GetValue<string>() ==
                             "dependency.complete_master_angler.trap_supply_acquisition");
        producer["rule"]!["crab_pot_maximum_unit_price"] = 1499;

        var tamperedPath = Path.Combine(
            Path.GetTempPath(),
            "stardewai-master-angler-trap-supply-binding-" +
            Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(
                tamperedPath,
                root.ToJsonString(JsonDefaults.Options));
            var tampered = MasterAnglerFreshSavePrerequisiteAuditBuilder.Build(
                catalogPath,
                windowIndexPath,
                tamperedPath);
            Require(
                tampered.PrerequisiteFamilies.Single(value =>
                        value.FamilyId ==
                            "crab_pot_recipe_material_and_bait_supply")
                    .ProducerStatus != "implemented_and_bound" &&
                tampered.MissingOrUnboundProducerFamilies.Contains(
                    "crab_pot_recipe_material_and_bait_supply",
                    StringComparer.Ordinal),
                "Master Angler trap supply accepted a drifted Crab Pot price bound.");
        }
        finally
        {
            if (File.Exists(tamperedPath))
                File.Delete(tamperedPath);
        }
    }

    private static void VerifyMasterAnglerSewerAccessBindingFailsClosed(
        string catalogPath,
        string windowIndexPath,
        string dependencyExpansionPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(dependencyExpansionPath))?.AsObject() ??
                   throw new InvalidDataException("Master Angler dependency fixture is null.");
        var direction = root["directions"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["direction_id"]!.GetValue<string>() ==
                             "complete_master_angler");
        var edges = direction["edges"]!.AsArray();
        var requiredEdge = edges.Single(value =>
        {
            var edge = value!.AsObject();
            return edge["from"]!.GetValue<string>() ==
                       "dependency.complete_master_angler.sewer_rusty_key_access" &&
                   edge["to"]!.GetValue<string>() ==
                       "dependency.complete_master_angler.target_date_opportunity_producer";
        });
        edges.Remove(requiredEdge);

        var tamperedPath = Path.Combine(
            Path.GetTempPath(),
            "stardewai-master-angler-sewer-binding-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(tamperedPath, root.ToJsonString(JsonDefaults.Options));
            var tampered = MasterAnglerFreshSavePrerequisiteAuditBuilder.Build(
                catalogPath,
                windowIndexPath,
                tamperedPath);
            Require(
                tampered.PrerequisiteFamilies.Single(value =>
                        value.FamilyId == "sewer_rusty_key_access")
                    .ProducerStatus != "implemented_and_bound" &&
                tampered.MissingOrUnboundProducerFamilies.Contains(
                    "sewer_rusty_key_access",
                    StringComparer.Ordinal),
                "Master Angler sewer producer accepted a missing target-date edge.");
        }
        finally
        {
            if (File.Exists(tamperedPath))
                File.Delete(tamperedPath);
        }
    }

    private static void VerifyMasterAnglerDesertBusBindingFailsClosed(
        string catalogPath,
        string windowIndexPath,
        string dependencyExpansionPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(dependencyExpansionPath))?.AsObject() ??
                   throw new InvalidDataException("Master Angler dependency fixture is null.");
        var direction = root["directions"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["direction_id"]!.GetValue<string>() ==
                             "complete_master_angler");
        var producer = direction["nodes"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["id"]!.GetValue<string>() ==
                             "dependency.complete_master_angler.desert_bus_access");
        producer["rule"]!["connector_kind"] = "warp";

        var tamperedPath = Path.Combine(
            Path.GetTempPath(),
            "stardewai-master-angler-bus-binding-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(tamperedPath, root.ToJsonString(JsonDefaults.Options));
            var tampered = MasterAnglerFreshSavePrerequisiteAuditBuilder.Build(
                catalogPath,
                windowIndexPath,
                tamperedPath);
            Require(
                tampered.PrerequisiteFamilies.Single(value =>
                        value.FamilyId == "desert_bus_access")
                    .ProducerStatus != "implemented_and_bound" &&
                tampered.MissingOrUnboundProducerFamilies.Contains(
                    "desert_bus_access",
                    StringComparer.Ordinal),
                "Master Angler desert-bus producer accepted a drifted connector kind.");
        }
        finally
        {
            if (File.Exists(tamperedPath))
                File.Delete(tamperedPath);
        }
    }

    private static void VerifyMasterAnglerRodEquipmentBindingFailsClosed(
        string catalogPath,
        string windowIndexPath,
        string dependencyExpansionPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(dependencyExpansionPath))?.AsObject() ??
                   throw new InvalidDataException("Master Angler dependency fixture is null.");
        var direction = root["directions"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["direction_id"]!.GetValue<string>() ==
                             "complete_master_angler");
        var producer = direction["nodes"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["id"]!.GetValue<string>() ==
                             "dependency.complete_master_angler.rod_equipment_acquisition");
        producer["rule"]!["required_qualified_item_id"] = "(T)IridiumRod";

        var tamperedPath = Path.Combine(
            Path.GetTempPath(),
            "stardewai-master-angler-rod-binding-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(tamperedPath, root.ToJsonString(JsonDefaults.Options));
            var tampered = MasterAnglerFreshSavePrerequisiteAuditBuilder.Build(
                catalogPath,
                windowIndexPath,
                tamperedPath);
            Require(
                tampered.PrerequisiteFamilies.Single(value =>
                        value.FamilyId == "rod_and_fishing_equipment_acquisition")
                    .ProducerStatus != "implemented_and_bound" &&
                tampered.MissingOrUnboundProducerFamilies.Contains(
                    "rod_and_fishing_equipment_acquisition",
                    StringComparer.Ordinal),
                "Master Angler rod-equipment producer accepted a drifted required rod.");
        }
        finally
        {
            if (File.Exists(tamperedPath))
                File.Delete(tamperedPath);
        }
    }

    private static void VerifyMasterAnglerMineAccessBindingFailsClosed(
        string catalogPath,
        string windowIndexPath,
        string dependencyExpansionPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(dependencyExpansionPath))?.AsObject() ??
                   throw new InvalidDataException("Master Angler dependency fixture is null.");
        var direction = root["directions"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["direction_id"]!.GetValue<string>() ==
                             "complete_master_angler");
        var edges = direction["edges"]!.AsArray();
        var requiredEdge = edges
            .Single(value =>
            {
                var edge = value!.AsObject();
                return edge["from"]!.GetValue<string>() ==
                           "dependency.complete_master_angler.ordinary_mine_fishing_access" &&
                       edge["to"]!.GetValue<string>() ==
                           "dependency.complete_master_angler.target_date_opportunity_producer";
            });
        edges.Remove(requiredEdge);

        var tamperedPath = Path.Combine(
            Path.GetTempPath(),
            "stardewai-master-angler-mine-binding-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(tamperedPath, root.ToJsonString(JsonDefaults.Options));
            var tampered = MasterAnglerFreshSavePrerequisiteAuditBuilder.Build(
                catalogPath,
                windowIndexPath,
                tamperedPath);
            var affected = new[]
            {
                "mine_access_and_depth",
                "mine_depth_or_ginger_island_caldera",
                "one_of_advanced_location_access"
            };
            Require(
                affected.All(family =>
                    tampered.PrerequisiteFamilies.Single(value =>
                            value.FamilyId == family)
                        .ProducerStatus != "implemented_and_bound") &&
                affected.All(family =>
                    tampered.MissingOrUnboundProducerFamilies.Contains(
                        family,
                        StringComparer.Ordinal)),
                "Master Angler mine-access producer accepted a missing recurrence edge.");
        }
        finally
        {
            if (File.Exists(tamperedPath))
                File.Delete(tamperedPath);
        }
    }

    private static void VerifyMasterAnglerFishingSkillBindingFailsClosed(
        string catalogPath,
        string windowIndexPath,
        string dependencyExpansionPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(dependencyExpansionPath))?.AsObject() ??
                   throw new InvalidDataException("Master Angler dependency fixture is null.");
        var direction = root["directions"]!.AsArray()
            .Select(value => value!.AsObject())
            .Single(value => value["direction_id"]!.GetValue<string>() ==
                             "complete_master_angler");
        var edges = direction["edges"]!.AsArray();
        var requiredEdge = edges
            .Single(value =>
            {
                var edge = value!.AsObject();
                return edge["from"]!.GetValue<string>() ==
                           "dependency.complete_master_angler.fishing_skill_progression" &&
                       edge["to"]!.GetValue<string>() ==
                           "dependency.complete_master_angler.acquisition_recurrence";
            });
        edges.Remove(requiredEdge);

        var tamperedPath = Path.Combine(
            Path.GetTempPath(),
            "stardewai-master-angler-skill-binding-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(tamperedPath, root.ToJsonString(JsonDefaults.Options));
            var tampered = MasterAnglerFreshSavePrerequisiteAuditBuilder.Build(
                catalogPath,
                windowIndexPath,
                tamperedPath);
            Require(
                tampered.PrerequisiteFamilies.Single(value =>
                        value.FamilyId == "fishing_skill_progression")
                    .ProducerStatus == "reusable_but_unbound" &&
                tampered.MissingOrUnboundProducerFamilies.Contains(
                    "fishing_skill_progression",
                    StringComparer.Ordinal),
                "Master Angler fishing-skill producer accepted a missing recurrence edge.");
        }
        finally
        {
            if (File.Exists(tamperedPath))
                File.Delete(tamperedPath);
        }
    }

    private static void VerifyMasterAnglerChanceModifierParsing()
    {
        using var document = JsonDocument.Parse("""
            {
              "ChanceModifiers": [
                {
                  "Id": "fixture",
                  "Condition": "PLAYER_HAS_MAIL Current fixture",
                  "Modification": 2,
                  "Amount": 1.5,
                  "RandomAmount": [0.25, 0.75]
                }
              ]
            }
            """);
        var modifiers = MasterAnglerOpportunityCatalogBuilder.ParseChanceModifiers(
            document.RootElement);
        Require(
            modifiers.Length == 1 &&
            modifiers[0].Id == "fixture" &&
            modifiers[0].Condition == "PLAYER_HAS_MAIL Current fixture" &&
            modifiers[0].Modification == 2 &&
            Math.Abs(modifiers[0].Amount - 1.5) < 0.000001 &&
            modifiers[0].RandomAmount.SequenceEqual(new[] { 0.25, 0.75 }),
            "Master Angler chance modifier inputs were not preserved losslessly.");
    }

    private static void VerifyMasterAnglerFullRouteIntent(string outputRoot)
    {
        var root = Path.Combine(outputRoot, "master-angler-route-fixture");
        Directory.CreateDirectory(root);
        var windowsPath = Path.Combine(root, "windows.json");
        var snapshotPath = Path.Combine(root, "snapshot.json");
        var calibrationPath = Path.Combine(root, "route-timing.json");

        var species = Enumerable.Range(0, 72)
            .Select(index => new
            {
                qualified_item_id = index == 0
                    ? "(O)145"
                    : "(O)test_" + index,
                windows = index == 0
                    ? new object[]
                    {
                        new
                        {
                            source_kind = "location_rule",
                            source_key = "Beach:0",
                            location_id = "Beach",
                            first_total_day = 0,
                            last_total_day = 27,
                            minimum_fishing_level = 0,
                            require_magic_bait = false,
                            training_rod_allowed = true,
                            dynamic_conditions = Array.Empty<string>(),
                            weather_modes = new[] { "rain", "storm" },
                            time_windows = new[]
                            {
                                new { start_time = 600, end_time = 2600 }
                            }
                        }
                    }
                    : Array.Empty<object>()
            })
            .ToArray();
        Write(windowsPath, new
        {
            schema_version = "master_angler_stage_one_window_index.v1",
            status = "complete_static_windows_dynamic_execution_pending",
            game_version = "1.6.15",
            static_window_coverage_complete = true,
            training_label_eligible = false,
            native_denominator_count = 72,
            deadline_total_day_exclusive = 224,
            species
        });
        Write(calibrationPath, new
        {
            schema_version = "stardewai.runtime_movement_timing_calibration.v1",
            status = "passed",
            game_version = "1.6.15",
            capture_total_days = 223,
            sample_count = 3,
            total_manhattan_tiles = 66,
            maximum_observed_game_minutes_per_tile = 0.29,
            native_milliseconds_per_game_minute = 700,
            conservative_game_minute_numerator_per_tile = 1,
            conservative_game_minute_denominator_per_tile = 1,
            connector_sample_count = 2,
            connector_kinds = new[] { "building_door", "warp" },
            maximum_observed_connector_game_minutes = 1.04,
            conservative_connector_transition_game_minutes = 2,
            calibration_evidence_kind = "conservative_upper_bound",
            connector_transition_game_minutes_status =
                "runtime_proven_building_door_and_step_warp",
            movement_context_projection_status =
                "exact_current_player_native_cardinal_movement_context",
            movement_context_scope =
                "ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay",
            source_save_fingerprint_before = "ABC",
            source_save_fingerprint_after = "ABC",
            source_save_untouched = true
        });

        var fishRows = Enumerable.Range(0, 72)
            .Select(index => new
            {
                item_id = index == 0 ? "145" : "test_" + index,
                qualified_item_id = index == 0
                    ? "(O)145"
                    : "(O)test_" + index,
                caught = index != 0
            })
            .ToArray();
        var routeEdges = new object[]
        {
            Edge("Farm", 2, 4, "Town", 1, 5),
            Edge("Town", 3, 4, "Beach", 1, 5)
        };
        Write(snapshotPath, new
        {
            schema_version = "snapshot.v1",
            game_version = "1.6.15",
            state_hash = "master-angler-full-route-self-test",
            state = new
            {
                time = new
                {
                    total_days = Field(5),
                    time = Field(800)
                },
                player = new
                {
                    location_id = Field("Farm"),
                    tile_x = Field(1),
                    tile_y = Field(5),
                    skills_detail = Field(new
                    {
                        skills = new[]
                        {
                            new { skill_id = "fishing", effective_level = 0 }
                        }
                    }),
                    rain_totem = Field(new
                    {
                        projection_status =
                            "complete_current_native_rain_totem_context",
                        context_routing = new
                        {
                            affected_location_context_id = "Default",
                            allow_rain_totem = true
                        },
                        weather_transition = new
                        {
                            tomorrow_is_default_festival = false,
                            affected_weather_before = "Sun",
                            affected_weather_after = "Rain",
                            tomorrow_total_days = 6,
                            effective_tomorrow_weather = "Rain",
                            rain_will_take_effect_tomorrow = true
                        }
                    }),
                    movement_timing_context = Field(new
                    {
                        projection_status =
                            "exact_current_player_native_cardinal_movement_context",
                        runtime_calibration_compatible = true,
                        route_timing_ready_now = true,
                        real_milliseconds_per_game_minute = 700,
                        theoretical_upper_bound_game_minutes_per_tile = 0.7,
                        scope =
                            "ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay"
                    })
                },
                locations = new
                {
                    route_graph = Field(new { edges = routeEdges }),
                    social_route_date_evidence = Field(new
                    {
                        schema_version = "social_route_date_evidence.v2",
                        capture_total_days = 5,
                        all_location_static_walkability_complete = true,
                        projection_status =
                            "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
                        location_count = 3,
                        locations = new[]
                        {
                            Location("Farm"),
                            Location("Town"),
                            Location("Beach")
                        }
                    })
                },
                fishing = new
                {
                    rod_contexts = Field(new[]
                    {
                        new
                        {
                            has_magic_bait = false,
                            uses_training_rod = false
                        }
                    })
                },
                current_location = new
                {
                    objects = Field(Array.Empty<object>())
                },
                world_progress = new
                {
                    fish_collection_progress = Field(new
                    {
                        eligible_species_count = 72,
                        caught_eligible_species_count = 71,
                        missing_species_count = 1,
                        items = fishRows
                    })
                }
            }
        });

        var result = MasterAnglerTargetDateIntentBuilder.Build(
            windowsPath,
            snapshotPath,
            calibrationPath);
        var candidate = result.Candidates.Single();
        Require(
            result.Status ==
                "validated_current_date_full_route_intents_runtime_terminal_pending" &&
            !result.TrainingLabelEligible &&
            result.WeatherSupplyCandidateCount == 1 &&
            result.WeatherSupplyCandidates.Single().TargetQualifiedItemIds
                .SequenceEqual(new[] { "(O)145" }, StringComparer.Ordinal) &&
            candidate.RouteEdgeCount == 2 &&
            candidate.RouteGuaranteedArrivalTime == 807 &&
            candidate.Parameters.Any(parameter =>
                parameter.Name ==
                    MasterAnglerRouteTimingValidator.CalibrationSha256Parameter &&
                parameter.Value == result.RouteTimingCalibrationSha256),
            "Master Angler teacher intent did not retain its complete route timing proof.");

        var constrainedWindows = JsonNode.Parse(
            File.ReadAllText(windowsPath))!;
        constrainedWindows["species"]![0]!["windows"]![0]!
            ["time_windows"]![0]!["end_time"] = 820;
        Write(windowsPath, constrainedWindows);
        var noRouteSlack = MasterAnglerTargetDateIntentBuilder.Build(
            windowsPath,
            snapshotPath,
            calibrationPath);
        Require(
            noRouteSlack.CandidateCount == 0,
            "Master Angler teacher admitted a window without post-arrival catch reserve.");
    }

    private static object Field(object value) => new
    {
        value,
        status = "available",
        source = new { kind = "test", path = "self-test" },
        adapter = "self-test",
        read_at_tick = 1,
        confidence = 1
    };

    private static object Edge(
        string from,
        int fromX,
        int fromY,
        string target,
        int targetX,
        int targetY,
        string kind = "building_door") => new
    {
        kind,
        from_location = from,
        from_x = fromX,
        from_y = fromY,
        target_location = target,
        target_x = targetX,
        target_y = targetY,
        resolved = true
    };

    private static object Location(
        string id,
        bool seedsIgnoreSeasonsHere = false,
        object[]? actionGates = null,
        int openPreparedSoilSlots = 0,
        int occupiedPreparedSoilSlots = 0,
        object[]? occupiedHarvestItems = null,
        int mapWidth = 5,
        int mapHeight = 10) => new
    {
        location_id = id,
        location_context_id = "Default",
        seeds_ignore_seasons_here = seedsIgnoreSeasonsHere,
        map_width = mapWidth,
        map_height = mapHeight,
        projection_status = "exact_current_date_static_native_walkability",
        static_walkable_tile_count = mapWidth * mapHeight,
        static_walkable_tile_ranges = Enumerable.Range(0, mapHeight)
            .Select(y => new { y, start_x = 0, end_x = mapWidth - 1 })
            .ToArray(),
        build_conditions = (string?)null,
        build_conditions_met = (bool?)null,
        unsupported_route_action_record_count = 0,
        unsupported_route_action_tile_count = 0,
        unsupported_route_action_tiles = Array.Empty<object>(),
        cultivation_capacity = new
        {
            schema_version = "prepared_cultivation_capacity.v1",
            projection_status =
                "exact_current_snapshot_prepared_soil_slots",
            total_prepared_soil_slot_count =
                openPreparedSoilSlots + occupiedPreparedSoilSlots,
            open_prepared_soil_slot_count = openPreparedSoilSlots,
            occupied_crop_slot_count = occupiedPreparedSoilSlots,
            unresolved_harvest_item_slot_count = 0,
            garden_pot_slot_count = 0,
            occupied_harvest_items =
                occupiedHarvestItems ?? Array.Empty<object>()
        },
        action_gates = actionGates ?? Array.Empty<object>()
    };
}
