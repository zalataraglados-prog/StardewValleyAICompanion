using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    public static void RunAcquisitionRouteDispatch()
    {
        var snapshot = AcquisitionDispatchSnapshot();
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(
                snapshot,
                new[] { "foraging.harvest_bushes" },
                true));
        var requirement = BushRequirement();
        var ledger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger.dispatch.self-test",
            Revision = 1,
            SourceStateHash = snapshot.StateHash
        };
        var rebuilt = AcquisitionRouteDispatchCompilationBuilder
            .RebuildVerifiedCurrentCandidates(
                snapshot,
                ledger,
                "grandpa.stage1.21_points",
                requirement,
                new[] { "foraging.harvest_bushes" },
                ranked,
                out var rebuildReasons);
        Require(rebuilt.Length == ranked.Length && rebuildReasons.Length == 0,
            "Untampered live ranking did not reproduce from the transparent snapshot.");
        var driftedRanking = ranked.Select(CloneCandidate).ToArray();
        driftedRanking[0].Parameters = driftedRanking[0].Parameters
            .Select(parameter => parameter.Name == "target_tile_x"
                ? new SmallModelActionParameter
                {
                    Name = parameter.Name,
                    Value = "999"
                }
                : parameter)
            .ToArray();
        var rejected = AcquisitionRouteDispatchCompilationBuilder
            .RebuildVerifiedCurrentCandidates(
                snapshot,
                ledger,
                "grandpa.stage1.21_points",
                requirement,
                new[] { "foraging.harvest_bushes" },
                driftedRanking,
                out var driftReasons);
        Require(rejected.Length == 0 && driftReasons.Contains(
                "ranking_endpoint_candidate_evidence_mismatch",
                StringComparer.Ordinal),
            "A ranking with drifted transparent candidate evidence was admitted.");

        var source = ranked.Single(value => value.Kind == "harvest_bush");
        source.Rank = 999;
        source.Score = 999_999;
        source.ModelScore = 999_999;
        source.ExpectedReward = 999_999;
        source.EstimatedTicks = 120;
        var faster = CloneCandidate(source);
        faster.CandidateId += ":faster";
        faster.Rank = 10_000;
        faster.Score = -999_999;
        faster.ModelScore = -999_999;
        faster.ExpectedReward = -999_999;
        faster.EstimatedTicks = 60;

        var lowered = BushLowering();
        var matches = AcquisitionRouteDispatchCompilationBuilder
            .SelectCandidates(
                requirement,
                lowered,
                snapshot,
                new[] { source, faster });
        Require(matches.Length == 2 &&
                matches[0].Candidate.CandidateId == faster.CandidateId,
            "Acquisition dispatch selection used learner rank or score instead of deterministic live cost.");

        var compilation = AcquisitionRouteDispatchCompilationBuilder.Compile(
            "grandpa.stage1.21_points",
            requirement,
            lowered,
            matches[0],
            snapshot,
            ledger,
            "portfolio.dispatch.self-test",
            1,
            new string('a', 64));
        Require(compilation.DispatchReady &&
                compilation.ActionQueue is not null &&
                compilation.ActionQueue.Status == "pending" &&
                !compilation.UsesLearnerRankOrScore &&
                !compilation.FormalTrainingAuthorized,
            "Exact source-bound acquisition route did not compile to a pending native queue.");
        var queue = compilation.ActionQueue ?? throw new InvalidDataException(
            "Acquisition dispatch self-test queue is null.");
        var item = queue.Items.Single();
        Require(item.OptionId == "executor.harvest_bush" &&
                item.NormalizedCommand.CommandType == "compiled_action_steps" &&
                item.NormalizedCommand.Parameters.Any(value =>
                    value.Name == "acquisition_endpoint_option_id" &&
                    value.Value == "foraging.harvest_bushes") &&
                item.NormalizedCommand.Parameters.Any(value =>
                    value.Name == "acquisition_source_candidate_id" &&
                    value.Value == faster.CandidateId),
            "Expanded native queue lost its authoritative route lineage.");

        var invalidSource = requirement with
        {
            RouteKind = "native_geode_default_drop",
            SourceId = "Utility.getTreasureFromGeode"
        };
        Require(AcquisitionRouteDispatchCompilationBuilder.SelectCandidates(
                    invalidSource,
                    lowered,
                    snapshot,
                    new[] { faster }).Length == 0,
            "A same-item candidate from the wrong authoritative source was admitted.");

        var animalCandidate = CloneCandidate(faster);
        animalCandidate.OptionId = "farm.collect_animal_products";
        animalCandidate.Kind = "collect_animal_product";
        animalCandidate.QualifiedItemId = "(O)184";
        animalCandidate.Parameters = animalCandidate.Parameters.Concat(new[]
        {
            new SmallModelActionParameter
            {
                Name = "authoritative_route_sources_json",
                Value = "[{\"route_kind\":\"native_farm_animal_produce\",\"source_id\":\"farm_animal:White Cow:0\",\"qualified_item_id\":\"(O)184\"}]"
            }
        }).ToArray();
        var animalRequirement = requirement with
        {
            QualifiedItemId = "(O)184",
            RouteKind = "native_farm_animal_produce",
            SourceId = "farm_animal:White Cow:0"
        };
        var animalLowering = lowered with
        {
            RouteKind = animalRequirement.RouteKind,
            SourceId = animalRequirement.SourceId,
            EndpointOptionIds = new[] { "farm.collect_animal_products" }
        };
        Require(AcquisitionRouteDispatchCompilationBuilder.SelectCandidates(
                    animalRequirement,
                    animalLowering,
                    snapshot,
                    new[] { animalCandidate }).Length == 1,
            "A rebuilt candidate with exact typed route source was rejected.");
        Require(AcquisitionRouteDispatchCompilationBuilder.SelectCandidates(
                    animalRequirement with
                    {
                        RouteKind = "native_farm_animal_deluxe_produce"
                    },
                    animalLowering,
                    snapshot,
                    new[] { animalCandidate }).Length == 0,
            "A regular animal product source was accepted as deluxe produce.");
        var ambiguousAnimalCandidate = CloneCandidate(animalCandidate);
        ambiguousAnimalCandidate.Parameters = ambiguousAnimalCandidate.Parameters
            .Select(parameter =>
                parameter.Name == "authoritative_route_sources_json"
                    ? Parameter(
                        parameter.Name,
                        "[{\"route_kind\":\"native_farm_animal_produce\",\"source_id\":\"farm_animal:White Cow:0\",\"qualified_item_id\":\"(O)184\"},{\"route_kind\":\"native_farm_animal_produce\",\"source_id\":\"farm_animal:Brown Cow:0\",\"qualified_item_id\":\"(O)184\"}]")
                    : parameter)
            .ToArray();
        Require(AcquisitionRouteDispatchCompilationBuilder.SelectCandidates(
                    animalRequirement,
                    animalLowering,
                    snapshot,
                    new[] { ambiguousAnimalCandidate }).Length == 0,
            "A candidate with ambiguous same-item source rows was admitted.");

        var broadChopCandidate = CloneCandidate(faster);
        broadChopCandidate.OptionId = "foraging.chop_wild_tree";
        broadChopCandidate.Kind = "clear_obstacle_tile";
        broadChopCandidate.QualifiedItemId = string.Empty;
        broadChopCandidate.Parameters = broadChopCandidate.Parameters.Concat(
            new[]
            {
                Parameter(
                    "authoritative_route_sources_json",
                    "[{\"route_kind\":\"native_wild_tree_chop_drop\",\"source_id\":\"wild_tree:1:0\",\"qualified_item_id\":\"(O)92\"}]")
            }).ToArray();
        var chopRequirement = requirement with
        {
            QualifiedItemId = "(O)92",
            RouteKind = "native_wild_tree_chop_drop",
            SourceId = "wild_tree:1:0"
        };
        var chopLowering = lowered with
        {
            RouteKind = chopRequirement.RouteKind,
            SourceId = chopRequirement.SourceId,
            EndpointOptionIds = new[] { "foraging.chop_wild_tree" }
        };
        Require(AcquisitionRouteDispatchCompilationBuilder.SelectCandidates(
                    chopRequirement,
                    chopLowering,
                    snapshot,
                    new[] { broadChopCandidate }).Length == 1,
            "A broad native chop candidate did not inherit its unique exact output identity.");
        var contradictoryChopCandidate = CloneCandidate(broadChopCandidate);
        contradictoryChopCandidate.QualifiedItemId = "(O)388";
        Require(AcquisitionRouteDispatchCompilationBuilder.SelectCandidates(
                    chopRequirement,
                    chopLowering,
                    snapshot,
                    new[] { contradictoryChopCandidate }).Length == 0,
            "A broad native chop source overrode a contradictory candidate item identity.");

        var geodeCandidate = CloneCandidate(animalCandidate);
        geodeCandidate.OptionId = "processing.crack_geode";
        geodeCandidate.Kind = "crack_geode";
        geodeCandidate.QualifiedItemId = "(O)535";
        geodeCandidate.Parameters = geodeCandidate.Parameters
            .Where(parameter => parameter.Name !=
                "authoritative_route_sources_json")
            .Concat(new[]
            {
                Parameter("geode_expected_output_qid", "(O)538"),
                Parameter(
                    "authoritative_route_sources_json",
                    "[{\"route_kind\":\"native_geode_drop\",\"source_id\":\"geode:535:0:random:0\",\"qualified_item_id\":\"(O)538\"}]")
            }).ToArray();
        var geodeRequirement = requirement with
        {
            QualifiedItemId = "(O)538",
            RouteKind = "native_geode_drop",
            SourceId = "geode:535:0:random:0"
        };
        var geodeLowering = lowered with
        {
            RouteKind = geodeRequirement.RouteKind,
            SourceId = geodeRequirement.SourceId,
            EndpointOptionIds = new[] { "processing.crack_geode" }
        };
        Require(AcquisitionRouteDispatchCompilationBuilder.SelectCandidates(
                    geodeRequirement,
                    geodeLowering,
                    snapshot,
                    new[] { geodeCandidate }).Length == 1,
            "A geode input candidate did not bind its exact projected output source.");
        var wrongGeodeOutput = CloneCandidate(geodeCandidate);
        wrongGeodeOutput.Parameters = wrongGeodeOutput.Parameters
            .Select(parameter => parameter.Name ==
                "geode_expected_output_qid"
                ? Parameter(parameter.Name, "(O)542")
                : parameter)
            .ToArray();
        Require(AcquisitionRouteDispatchCompilationBuilder.SelectCandidates(
                    geodeRequirement,
                    geodeLowering,
                    snapshot,
                    new[] { wrongGeodeOutput }).Length == 0,
            "A geode candidate with the wrong projected output was admitted.");

        var machineSources = new[]
        {
            new
            {
                RouteKind = "machine_output",
                SourceId = "machine:(BC)13:rule:Default_CopperOre",
                QualifiedItemId = "(O)334"
            },
            new
            {
                RouteKind = "native_machine_item_query_output",
                SourceId = "machine:(BC)13:rule:0:output:0",
                QualifiedItemId = "(O)334"
            },
            new
            {
                RouteKind = "native_machine_flavored_output",
                SourceId = "machine:(BC)10:rule:0:output:0",
                QualifiedItemId = "(O)340"
            }
        };
        foreach (var machineSource in machineSources)
        {
            var machineCandidate = CloneCandidate(animalCandidate);
            machineCandidate.OptionId = "farm.collect_machine_outputs";
            machineCandidate.Kind = "collect_machine_output_tile";
            machineCandidate.QualifiedItemId =
                machineSource.QualifiedItemId;
            machineCandidate.Parameters = machineCandidate.Parameters
                .Where(parameter => parameter.Name !=
                    "authoritative_route_sources_json")
                .Concat(new[]
                {
                    Parameter(
                        "authoritative_route_sources_json",
                        "[{\"route_kind\":\"" +
                        machineSource.RouteKind +
                        "\",\"source_id\":\"" +
                        machineSource.SourceId +
                        "\",\"qualified_item_id\":\"" +
                        machineSource.QualifiedItemId + "\"}]")
                }).ToArray();
            var machineRequirement = requirement with
            {
                QualifiedItemId = machineSource.QualifiedItemId,
                RouteKind = machineSource.RouteKind,
                SourceId = machineSource.SourceId
            };
            var machineLowering = lowered with
            {
                RouteKind = machineRequirement.RouteKind,
                SourceId = machineRequirement.SourceId,
                EndpointOptionIds = new[]
                {
                    "farm.collect_machine_outputs"
                }
            };
            Require(AcquisitionRouteDispatchCompilationBuilder
                    .SelectCandidates(
                        machineRequirement,
                        machineLowering,
                        snapshot,
                        new[] { machineCandidate }).Length == 1,
                "An exact ordinary machine output source was rejected: " +
                machineSource.RouteKind);
        }

        var cookingSnapshot = AcquisitionDispatchCookingSnapshot();
        var cookingLedger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger.dispatch.cooking.self-test",
            Revision = 1,
            SourceStateHash = cookingSnapshot.StateHash
        };
        var cookingIntent = new OptionAvailabilityCandidate
        {
            OptionId = "crafting.cook_recipe",
            ExplicitConfirmationGranted = true,
            Parameters = new[]
            {
                Parameter("recipe_name", "Fried Egg"),
                Parameter("craft_count", "1"),
                Parameter("cooking_reason", "full_shipment")
            }
        };
        var cookingRanked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(
                cookingSnapshot,
                new[] { cookingIntent },
                includeExecutorCalibrationOptions: true,
                commitmentLedger: cookingLedger),
            "grandpa.stage1.21_points");
        var cookingRequirement = requirement with
        {
            QualifiedItemId = "(O)194",
            RouteKind = "recipe_output",
            SourceId = "cooking_recipe:Fried Egg"
        };
        var rebuiltCooking = AcquisitionRouteDispatchCompilationBuilder
            .RebuildVerifiedCurrentCandidates(
                cookingSnapshot,
                cookingLedger,
                "grandpa.stage1.21_points",
                cookingRequirement,
                new[] { "crafting.cook_recipe" },
                cookingRanked,
                out var cookingRebuildReasons);
        Require(rebuiltCooking.Length == cookingRanked.Length &&
                cookingRebuildReasons.Length == 0,
            "Exact recipe-output ranking did not reproduce from the transparent snapshot.");
        var driftedCooking = cookingRanked.Select(CloneCandidate).ToArray();
        driftedCooking[0].Parameters = driftedCooking[0].Parameters
            .Select(parameter => parameter.Name == "cooking_reason"
                ? Parameter("cooking_reason", "ranking_injected_reason")
                : parameter)
            .ToArray();
        var rejectedCooking = AcquisitionRouteDispatchCompilationBuilder
            .RebuildVerifiedCurrentCandidates(
                cookingSnapshot,
                cookingLedger,
                "grandpa.stage1.21_points",
                cookingRequirement,
                new[] { "crafting.cook_recipe" },
                driftedCooking,
                out var cookingDriftReasons);
        Require(rejectedCooking.Length == 0 && cookingDriftReasons.Contains(
                "ranking_endpoint_candidate_evidence_mismatch",
                StringComparer.Ordinal),
            "A ranking-injected cooking reason was admitted.");

        var spoofed = CloneCandidate(faster);
        spoofed.Parameters = spoofed.Parameters.Concat(new[]
        {
            new SmallModelActionParameter
            {
                Name = "acquisition_source_id",
                Value = requirement.SourceId
            }
        }).ToArray();
        Require(AcquisitionRouteDispatchCompilationBuilder.SelectCandidates(
                    requirement,
                    lowered,
                    snapshot,
                    new[] { spoofed }).Length == 0,
            "A live candidate was allowed to inject acquisition lineage.");

        var endpoint = item.NormalizedCommand.Parameters.Single(value =>
            value.Name == "acquisition_endpoint_option_id");
        endpoint.Value = "farm.maintain_crops";
        var tamperReasons = AcquisitionRouteExecutionBindingBuilder
            .ValidateQueue(
                queue,
                compilation.GoalId,
                snapshot.StateHash,
                compilation.SelectedCandidateId,
                requirement,
                lowered,
                "portfolio.dispatch.self-test",
                1)
            .ToArray();
        Require(tamperReasons.Contains(
                "route_queue_option_outside_authoritative_route",
                StringComparer.Ordinal),
            "Tampered expanded-route lineage was not rejected.");

        endpoint.Value = "foraging.harvest_bushes";
        item.NormalizedCommand.CommandType = "option_request";
        var shapeReasons = AcquisitionRouteExecutionBindingBuilder
            .ValidateQueue(
                queue,
                compilation.GoalId,
                snapshot.StateHash,
                compilation.SelectedCandidateId,
                requirement,
                lowered,
                "portfolio.dispatch.self-test",
                1)
            .ToArray();
        Require(shapeReasons.Contains(
                "route_queue_command_binding_invalid",
                StringComparer.Ordinal),
            "An expanded primitive queue was accepted as a legacy option request.");
    }

    private static PolicyEventCandidatePrediction CloneCandidate(
        PolicyEventCandidatePrediction source) =>
        JsonSerializer.Deserialize<PolicyEventCandidatePrediction>(
            JsonSerializer.Serialize(source, JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
            "Acquisition dispatch self-test candidate clone failed.");

    private static AcquisitionRouteTargetDateUnlock BushRequirement() => new(
        RouteOccurrenceId: "full_shipment:bush:salmonberry",
        RequirementSetId: "full_shipment",
        RequirementId: "ship_salmonberry",
        AlternativeIndex: 0,
        RouteIndex: 0,
        QualifiedItemId: "(O)296",
        MatchKind: "item_id",
        RequiredAmount: 1,
        MinimumQuality: 0,
        RouteKind: "native_bush_shake",
        UncertaintyMode: "deterministic_fresh_receipt",
        SourceId: "Bush.GetShakeOffItem",
        SourceResolutionStatus: "resolved",
        CalendarAxisStatus: "resolved",
        StaticWindowMatchesTargetDate: true,
        MatchingWindows: Array.Empty<AuthoritativeCalendarSourceWindow>(),
        UnlockAxisStatus: "resolved",
        UnlockAxisResolved: true,
        UnlockStateMatchesTargetDate: true,
        UnlockConditions: Array.Empty<AcquisitionUnlockConditionEvaluation>(),
        PendingCalendarConditions: Array.Empty<string>(),
        PendingStochasticConditions: Array.Empty<string>(),
        PendingResourceConditions: Array.Empty<string>(),
        PendingLocationConditions: Array.Empty<string>(),
        UnsupportedConditions: Array.Empty<string>(),
        BlockingReasons: Array.Empty<string>());

    private static AcquisitionRequirementRouteLowering BushLowering() => new(
        RouteKind: "native_bush_shake",
        SourceId: "Bush.GetShakeOffItem",
        SourceAsset: "decompiled native method",
        SourcePath: "Season.Spring => (O)296",
        SupervisionMode: "policy_option",
        UncertaintyMode: "deterministic_fresh_receipt",
        RequiredDownstreamDependencyAxes: Array.Empty<string>(),
        EndpointOptionIds: new[] { "foraging.harvest_bushes" },
        SupportingOptionIds: Array.Empty<string>(),
        RuntimeAdmissionReady: true,
        TeacherAdmissionReady: true);

    private static SnapshotEnvelope AcquisitionDispatchSnapshot()
    {
        const string json = """
        {
          "player": {
            "location_id":{"value":"Forest","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "skills_detail":{"value":{"foraging":{"level":8}},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "current_location":{
            "debris":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "large_terrain_features":{"value":[{"tile_x":12,"tile_y":10,"runtime_type":"StardewValley.TerrainFeatures.Bush","bounding_tile_width":2,"bounding_tile_height":1,
              "is_bush":true,"bush_size":1,"bush_kind":"ordinary_berry","ready_for_harvest":true,"in_bloom":true,"tile_sheet_offset_before":1,"tile_sheet_offset_expected_after":0,
              "bush_harvest_status":"ready","bush_projection_status":"exact_from_native_bush_shake","bush_output_qualified_item_id":"(O)296",
              "bush_output_quantity_min":1,"bush_output_quantity_max":1,"bush_output_quality":0,
              "bush_foraging_experience_on_success_min":7,"bush_foraging_experience_on_success_max":7,
              "bush_nut_key":"","bush_nut_collected_before":false,"bush_nut_collected_expected_after":false}],
              "status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations":{
            "collision_grid":{"value":{"location_id":"Forest","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            json,
            JsonDefaults.Options) ?? throw new InvalidDataException(
            "Acquisition dispatch self-test snapshot is null.");
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-09-25T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static SnapshotEnvelope AcquisitionDispatchCookingSnapshot()
    {
        const string ingredientRows =
            "[{\"requirement_id_or_category\":\"176\",\"required_count\":1,\"available_count_before_this_ingredient\":1,\"satisfied\":true,\"native_consumption_plan\":[{\"source_id\":\"kitchen-fridge:FarmHouse\",\"slot_index\":0,\"qualified_item_id\":\"(O)176\",\"amount\":1,\"unit_sale_price\":50,\"total_sale_value\":50}]}]";
        const string seasoningRows = "[]";
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"FarmHouse","status":"available"},
            "tile_x":{"value":4,"status":"available"},
            "tile_y":{"value":5,"status":"available"},
            "inventory":{"value":[],"status":"available"},
            "inventory_capacity":{"value":{"occupied_stacks":0,"empty_slots":12,"has_empty_slot":true},"status":"available"},
            "cooking":{"value":{
              "projection_status":"complete_learned_cooking_recipe_and_native_source_projection",
              "rows":[{
                "recipe_name":"Fried Egg","known_recipe":true,
                "cooking_source_id":"kitchen:FarmHouse:5,5","cooking_source_kind":"kitchen",
                "location_id":"FarmHouse","interaction_tile_x":5,"interaction_tile_y":5,
                "material_container_ids":["kitchen-fridge:FarmHouse"],
                "material_container_topology_json":"[\"kitchen-fridge:FarmHouse\"]",
                "output_item_id":"194","output_qualified_item_id":"(O)194","output_display_name":"Fried Egg",
                "output_count_per_craft":1,"output_quality":0,"output_order_data":"","recipes_cooked_before":0,
                "ingredient_rows":{{{ingredientRows}}},"ingredient_rows_json":{{{JsonSerializer.Serialize(ingredientRows)}}},
                "seasoning_rows":{{{seasoningRows}}},"seasoning_rows_json":{{{JsonSerializer.Serialize(seasoningRows)}}},
                "output_inventory_acceptance_after_material_consumption":true,
                "craft_candidate_status":"ready_for_native_cooking_page"
              }]
            },"status":"available"}
          },
          "locations":{
            "route_graph":{"value":{"edges":[]},"status":"available"},
            "collision_grid":{"value":{"location_id":"FarmHouse","width":40,"height":40,"notable_tiles":[]},"status":"available"}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            json) ?? throw new InvalidDataException(
            "Cooking dispatch self-test snapshot did not deserialize.");
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-09-25T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }
}
