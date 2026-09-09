using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed partial class MasterAnglerMainlineTests
{
    [Fact]
    public void AuthoritativeCurrentLocationIntentReusesExactRuntimeCatchAndNativeTerminal()
    {
        var fixture = CreateFixture("Beach:0");
        try
        {
            var availability = new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    fixture.Before,
                    new[]
                    {
                        new OptionAvailabilityCandidate
                        {
                            OptionId = "fishing.catch_fish",
                            Parameters = fixture.Parameters
                        }
                    });

            var source = Assert.Single(Assert.Single(availability.Options).EventCandidates);
            Assert.True(source.Available, string.Join(";", source.BlockReasons));
            Assert.Equal("catch_fish", source.Kind);
            Assert.Contains(source.Parameters, parameter =>
                parameter.Name == "continuation.master_angler_target_qualified_item_id" &&
                parameter.Value == "(O)145");

            var ranked = new EventCandidateRanker().Rank(
                new BaselineTrainingReport(),
                availability,
                "goal.fishing.complete_master_angler");
            var plan = new DailyPlanCompiler().Compile(
                ranked,
                fixture.Before.StateHash);
            var catchStep = Assert.Single(plan.Steps);
            Assert.Equal("catch_fish", catchStep.Kind);

            var queue = new ActionQueueCompiler().Compile(plan, fixture.Before);
            var catchItem = Assert.Single(queue.Items);
            Assert.Equal("executor.catch_fish", catchItem.OptionId);
            Assert.Empty(catchItem.BlockingReasons);
            var queueItem = JsonSerializer.SerializeToNode(
                catchItem,
                JsonOptions)!.AsObject();
            var continuation = QueueReplanFilter.ReadObjectiveContinuation(
                queueItem);
            Assert.NotNull(continuation);
            Assert.Equal(
                "master_angler",
                continuation!["kind"]!.GetValue<string>());

            Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(
                queueItem,
                continuation,
                "applied",
                SnapshotNode(fixture.Before),
                afterSnapshotFresh: true));
            Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(
                queueItem,
                continuation,
                "applied",
                SnapshotNode(fixture.After),
                afterSnapshotFresh: false));
            Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(
                queueItem,
                continuation,
                "applied",
                SnapshotNode(fixture.After),
                afterSnapshotFresh: true));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void AuthoritativeIntentRejectsCatchProjectedByDifferentLocationRule()
    {
        var fixture = CreateFixture("Beach:1");
        try
        {
            var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    fixture.Before,
                    new[]
                    {
                        new OptionAvailabilityCandidate
                        {
                            OptionId = "fishing.catch_fish",
                            Parameters = fixture.Parameters
                        }
                    })
                .Options);

            var blocked = Assert.Single(option.EventCandidates);
            Assert.False(blocked.Available);
            Assert.Contains(
                "master_angler_no_exact_runtime_source_attempt",
                blocked.BlockReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void AuthoritativeIntentFailsClosedWhenCurrentStepCannotFinishInWindow()
    {
        var fixture = CreateFixture("Beach:0", currentTime: 2550);
        try
        {
            var candidate = Assert.Single(Assert.Single(
                new CandidateOptionAvailabilityEvaluator().Evaluate(
                    fixture.Before,
                    new[]
                    {
                        new OptionAvailabilityCandidate
                        {
                            OptionId = "fishing.catch_fish",
                            Parameters = fixture.Parameters
                        }
                    }).Options).EventCandidates);

            Assert.False(candidate.Available);
            Assert.Contains(
                "master_angler_current_step_would_miss_window",
                candidate.BlockReasons);
            Assert.Contains(candidate.Parameters, parameter =>
                parameter.Name == "master_angler_time_budget_required_minutes" &&
                int.Parse(parameter.Value) > 10);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void AuthoritativeReadyCrabPotIntentReusesCollectAndNativeTerminal()
    {
        var fixture = CreateCrabPotFixture();
        try
        {
            var availability = new CandidateOptionAvailabilityEvaluator()
                .Evaluate(
                    fixture.Before,
                    new[]
                    {
                        new OptionAvailabilityCandidate
                        {
                            OptionId = "fishing.collect_crab_pots",
                            Parameters = fixture.Parameters
                        }
                    });

            var source = Assert.Single(
                Assert.Single(availability.Options).EventCandidates);
            Assert.True(source.Available, string.Join(";", source.BlockReasons));
            Assert.Equal("collect_crab_pot", source.Kind);
            Assert.Equal("(O)372", source.QualifiedItemId);
            Assert.Contains(source.Parameters, parameter =>
                parameter.Name ==
                    "continuation.master_angler_target_qualified_item_id" &&
                parameter.Value == "(O)372");

            var plan = new DailyPlanCompiler().Compile(
                new EventCandidateRanker().Rank(
                    new BaselineTrainingReport(),
                    availability,
                    "goal.fishing.complete_master_angler"),
                fixture.Before.StateHash);
            Assert.Equal("collect_crab_pot", Assert.Single(plan.Steps).Kind);

            var queue = new ActionQueueCompiler().Compile(plan, fixture.Before);
            var item = Assert.Single(queue.Items);
            Assert.Equal("executor.collect_crab_pot", item.OptionId);
            Assert.Empty(item.BlockingReasons);
            var queueItem = JsonSerializer.SerializeToNode(item, JsonOptions)!
                .AsObject();
            var continuation = QueueReplanFilter.ReadObjectiveContinuation(
                queueItem);
            Assert.NotNull(continuation);
            Assert.Equal(
                "fishing.collect_crab_pots",
                continuation!["option_id"]!.GetValue<string>());
            Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(
                queueItem,
                continuation,
                "applied",
                SnapshotNode(fixture.Before),
                afterSnapshotFresh: true));
            Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(
                queueItem,
                continuation,
                "applied",
                SnapshotNode(fixture.After),
                afterSnapshotFresh: true));
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Theory]
    [InlineData(true, true, null)]
    [InlineData(false, false, "master_angler_dynamic_location_rule_not_runtime_eligible")]
    public void DynamicLocationRuleRequiresExactLiveEligibility(
        bool runtimeEligible,
        bool expectedAvailable,
        string? expectedReason)
    {
        var fixture = CreateFixture(
            "Beach:0",
            dynamicCondition: true,
            runtimeRuleEligible: runtimeEligible);
        try
        {
            var candidate = Assert.Single(Assert.Single(
                new CandidateOptionAvailabilityEvaluator().Evaluate(
                    fixture.Before,
                    new[]
                    {
                        new OptionAvailabilityCandidate
                        {
                            OptionId = "fishing.catch_fish",
                            Parameters = fixture.Parameters
                        }
                    }).Options).EventCandidates);

            Assert.Equal(expectedAvailable, candidate.Available);
            if (expectedReason is not null)
                Assert.Contains(expectedReason, candidate.BlockReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    private static Fixture CreateFixture(
        string sourceKey,
        bool dynamicCondition = false,
        bool runtimeRuleEligible = true,
        int currentTime = 900)
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-master-angler-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var indexPath = Path.Combine(root, "windows.json");
        var timingPath = WriteTimingCalibration(root);
        var species = new JsonArray();
        for (var index = 0; index < 72; index++)
        {
            var target = index == 0;
            species.Add(new JsonObject
            {
                ["qualified_item_id"] = target
                    ? "(O)145"
                    : "(O)test_" + index,
                ["windows"] = target
                    ? new JsonArray
                    {
                        new JsonObject
                        {
                            ["source_kind"] = "location_rule",
                            ["source_key"] = sourceKey,
                            ["location_id"] = "Beach",
                            ["first_total_day"] = 0,
                            ["last_total_day"] = 27,
                            ["minimum_fishing_level"] = 0,
                            ["require_magic_bait"] = false,
                            ["training_rod_allowed"] = true,
                            ["dynamic_conditions"] = dynamicCondition
                                ? new JsonArray(
                                    "!PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY")
                                : new JsonArray(),
                            ["time_windows"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["start_time"] = 600,
                                    ["end_time"] = 2600
                                }
                            }
                        }
                    }
                    : new JsonArray()
            });
        }
        var artifact = new JsonObject
        {
            ["schema_version"] = "master_angler_stage_one_window_index.v1",
            ["status"] = "complete_static_windows_dynamic_execution_pending",
            ["game_version"] = "1.6.15",
            ["static_window_coverage_complete"] = true,
            ["training_label_eligible"] = false,
            ["native_denominator_count"] = 72,
            ["deadline_total_day_exclusive"] = 224,
            ["species"] = species
        };
        File.WriteAllText(
            indexPath,
            artifact.ToJsonString(JsonOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var hash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(indexPath)))
            .ToLowerInvariant();
        var timingHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(timingPath)))
            .ToLowerInvariant();

        var before = BuildSnapshot(
            targetCaught: false,
            runtimeRuleEligible: runtimeRuleEligible,
            currentTime: currentTime);
        var after = BuildSnapshot(targetCaught: true);
        return new Fixture(
            before,
            after,
            indexPath,
            timingPath,
            new[]
            {
                Parameter("master_angler_target_qualified_item_id", "(O)145"),
                Parameter("master_angler_target_location", "Beach"),
                Parameter("master_angler_source_kind", "location_rule"),
                Parameter("master_angler_source_key", sourceKey),
                Parameter("master_angler_window_first_total_day", "0"),
                Parameter("master_angler_window_last_total_day", "27"),
                Parameter("master_angler_target_total_day", "5"),
                Parameter("master_angler_effective_start_time", "900"),
                Parameter("master_angler_last_cast_time_exclusive", "2600"),
                Parameter("master_angler_stage_one_deadline_total_day_exclusive", "224"),
                Parameter("master_angler_window_index_path", indexPath),
                Parameter("master_angler_window_index_sha256", hash),
                Parameter("master_angler_validation_status", "authoritative_stage_one_window_match"),
                Parameter("master_angler_runtime_terminal_validation_required", "true"),
                Parameter(
                    MasterAnglerRouteTimingValidator.CalibrationPathParameter,
                    timingPath),
                Parameter(
                    MasterAnglerRouteTimingValidator.CalibrationSha256Parameter,
                    timingHash),
                Parameter(
                    MasterAnglerRouteTimingValidator.ValidationRequiredParameter,
                    "true")
            });
    }

    private static Fixture CreateCrabPotFixture()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-master-angler-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var indexPath = Path.Combine(root, "windows.json");
        var timingPath = WriteTimingCalibration(root);
        var species = new JsonArray();
        for (var index = 0; index < 72; index++)
        {
            species.Add(new JsonObject
            {
                ["qualified_item_id"] = index == 0
                    ? "(O)372"
                    : "(O)test_" + index,
                ["windows"] = index == 0
                    ? new JsonArray
                    {
                        new JsonObject
                        {
                            ["source_kind"] = "crab_pot",
                            ["source_key"] = "CrabPot.DayUpdate:ocean",
                            ["location_id"] = string.Empty,
                            ["first_total_day"] = 1,
                            ["last_total_day"] = 223,
                            ["minimum_fishing_level"] = 0,
                            ["require_magic_bait"] = false,
                            ["training_rod_allowed"] = null,
                            ["dynamic_conditions"] = new JsonArray(),
                            ["time_windows"] = new JsonArray
                            {
                                new JsonObject
                                {
                                    ["start_time"] = 600,
                                    ["end_time"] = 2600
                                }
                            }
                        }
                    }
                    : new JsonArray()
            });
        }
        var artifact = new JsonObject
        {
            ["schema_version"] =
                "master_angler_stage_one_window_index.v1",
            ["status"] =
                "complete_static_windows_dynamic_execution_pending",
            ["game_version"] = "1.6.15",
            ["static_window_coverage_complete"] = true,
            ["training_label_eligible"] = false,
            ["native_denominator_count"] = 72,
            ["deadline_total_day_exclusive"] = 224,
            ["species"] = species
        };
        File.WriteAllText(
            indexPath,
            artifact.ToJsonString(JsonOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        var hash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(indexPath)))
            .ToLowerInvariant();
        var timingHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(timingPath)))
            .ToLowerInvariant();

        return new Fixture(
            BuildSnapshot(
                targetCaught: false,
                targetQualifiedItemId: "(O)372",
                readyCrabPotQualifiedItemId: "(O)372"),
            BuildSnapshot(
                targetCaught: true,
                targetQualifiedItemId: "(O)372"),
            indexPath,
            timingPath,
            new[]
            {
                Parameter(
                    "master_angler_target_qualified_item_id",
                    "(O)372"),
                Parameter("master_angler_target_location", "Beach"),
                Parameter("master_angler_source_kind", "crab_pot"),
                Parameter(
                    "master_angler_source_key",
                    "CrabPot.DayUpdate:ocean"),
                Parameter("master_angler_window_first_total_day", "1"),
                Parameter("master_angler_window_last_total_day", "223"),
                Parameter("master_angler_target_total_day", "5"),
                Parameter("master_angler_effective_start_time", "900"),
                Parameter("master_angler_last_cast_time_exclusive", "2600"),
                Parameter(
                    "master_angler_stage_one_deadline_total_day_exclusive",
                    "224"),
                Parameter("master_angler_window_index_path", indexPath),
                Parameter("master_angler_window_index_sha256", hash),
                Parameter(
                    "master_angler_validation_status",
                    "authoritative_stage_one_window_match"),
                Parameter(
                    "master_angler_runtime_terminal_validation_required",
                    "true"),
                Parameter(
                    MasterAnglerRouteTimingValidator.CalibrationPathParameter,
                    timingPath),
                Parameter(
                    MasterAnglerRouteTimingValidator.CalibrationSha256Parameter,
                    timingHash),
                Parameter(
                    MasterAnglerRouteTimingValidator.ValidationRequiredParameter,
                    "true")
            });
    }

    private static SnapshotEnvelope BuildSnapshot(
        bool targetCaught,
        string currentLocation = "Beach",
        string targetQualifiedItemId = "(O)145",
        string? readyCrabPotQualifiedItemId = null,
        bool runtimeRuleEligible = true,
        int currentTime = 900,
        bool remoteViaLongTownRoute = false,
        bool includeFullRouteEvidence = true)
    {
        var state = JsonNode.Parse(FishingMainlineTests.BaseState())!.AsObject();
        state["time"]!["total_days"] = Field(5);
        state["time"]!["time"] = Field(currentTime);
        state["player"]!["location_id"]!["value"] = currentLocation;
        state["player"]!["tile_x"]!["value"] = 1;
        state["player"]!["tile_y"]!["value"] = 5;
        state["player"]!["movement_timing_context"] = Field(new JsonObject
        {
            ["projection_status"] =
                "exact_current_player_native_cardinal_movement_context",
            ["runtime_calibration_compatible"] = true,
            ["route_timing_ready_now"] = true,
            ["real_milliseconds_per_game_minute"] = 700,
            ["theoretical_upper_bound_game_minutes_per_tile"] = 0.7,
            ["scope"] =
                "ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay"
        });
        state["current_location"]!["map"]!["value"]!["location_id"] =
            currentLocation;
        state["locations"]!["collision_grid"]!["value"]!["location_id"] =
            currentLocation;
        state["player"]!["skills_detail"] = Field(new JsonObject
        {
            ["skills"] = new JsonArray
            {
                new JsonObject
                {
                    ["skill_id"] = "fishing",
                    ["effective_level"] = 0
                }
            }
        });
        state["fishing"]!["rod_contexts"]!["value"]![0]!["spawn_rules"]!["rules"]![0]!["condition_met"] =
            runtimeRuleEligible;
        state["fishing"]!["rod_contexts"]!["value"]![0]!["spawn_rules"]!["rules"]![0]!["eligible_before_random_rolls"] =
            runtimeRuleEligible;
        state["fishing"]!["rod_contexts"]!["value"]![0]!["spawn_rules"]!["rules"]![0]!["outputs"]![0]!["output_eligible_before_random_rolls"] =
            runtimeRuleEligible;
        var remote = !string.Equals(
            currentLocation,
            "Beach",
            StringComparison.OrdinalIgnoreCase);
        var routeEdges = new JsonArray();
        if (remote)
        {
            routeEdges.Add(new JsonObject
            {
                ["kind"] = "building_door",
                ["from_location"] = currentLocation,
                ["from_x"] = 2,
                ["from_y"] = 4,
                ["target_location"] = remoteViaLongTownRoute
                    ? "Town"
                    : "Beach",
                ["target_x"] = 1,
                ["target_y"] = 5,
                ["resolved"] = true
            });
            if (remoteViaLongTownRoute)
            {
                routeEdges.Add(new JsonObject
                {
                    ["kind"] = "building_door",
                    ["from_location"] = "Town",
                    ["from_x"] = 90,
                    ["from_y"] = 4,
                    ["target_location"] = "Beach",
                    ["target_x"] = 1,
                    ["target_y"] = 5,
                    ["resolved"] = true
                });
            }
        }
        state["locations"]!["route_graph"] = Field(new JsonObject
        {
            ["edges"] = routeEdges
        });
        var routeLocations = new JsonArray
        {
            RouteLocationEvidence(currentLocation, 10, 10)
        };
        if (remoteViaLongTownRoute)
            routeLocations.Add(RouteLocationEvidence("Town", 100, 10));
        if (remote)
            routeLocations.Add(RouteLocationEvidence("Beach", 10, 10));
        if (includeFullRouteEvidence)
        {
            state["locations"]!["social_route_date_evidence"] = Field(
                new JsonObject
                {
                    ["schema_version"] = "social_route_date_evidence.v2",
                    ["capture_total_days"] = 5,
                    ["all_location_static_walkability_complete"] = true,
                    ["projection_status"] =
                        "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
                    ["location_count"] = routeLocations.Count,
                    ["locations"] = routeLocations
                });
        }
        if (remote)
        {
            state["locations"]!["route_action_branch_coverage"] = Field(
                new JsonObject
                {
                    ["rows"] = new JsonArray()
                });
            state["locations"]!["route_connectors"] = Field(new JsonObject
            {
                ["location_id"] = currentLocation,
                ["connectors"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["tile_x"] = 2,
                        ["tile_y"] = 4,
                        ["kind"] = "building_door",
                        ["target_location"] = remoteViaLongTownRoute
                            ? "Town"
                            : "Beach",
                        ["target_x"] = 1,
                        ["target_y"] = 5,
                        ["resolved"] = true
                    }
                }
            });
        }

        var items = new JsonArray();
        var missingIds = new JsonArray();
        var targetItemId = targetQualifiedItemId.StartsWith(
                "(O)",
                StringComparison.Ordinal)
            ? targetQualifiedItemId[3..]
            : targetQualifiedItemId;
        for (var index = 0; index < 72; index++)
        {
            var target = index == 0;
            var caught = !target || targetCaught;
            var itemId = target ? targetItemId : "test_" + index;
            items.Add(new JsonObject
            {
                ["item_id"] = itemId,
                ["qualified_item_id"] = target
                    ? targetQualifiedItemId
                    : "(O)test_" + index,
                ["caught"] = caught
            });
            if (!caught)
            {
                missingIds.Add(itemId);
            }
        }
        state["world_progress"] = new JsonObject
        {
            ["fish_collection_progress"] = Field(new JsonObject
            {
                ["eligible_species_count"] = 72,
                ["caught_eligible_species_count"] = targetCaught ? 72 : 71,
                ["missing_species_count"] = targetCaught ? 0 : 1,
                ["missing_item_ids"] = missingIds,
                ["items"] = items
            })
        };
        if (!string.IsNullOrWhiteSpace(readyCrabPotQualifiedItemId))
        {
            const string outputHash =
                "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            var outputItems = JsonSerializer.Serialize(new[]
            {
                new
                {
                    RuntimeType = "StardewValley.Object",
                    QualifiedItemId = readyCrabPotQualifiedItemId,
                    Quality = 0,
                    UnitStateSha256 = outputHash,
                    Quantity = 1
                }
            });
            state["current_location"]!["objects"] = Field(new JsonArray
            {
                new JsonObject
                {
                    ["tile_x"] = 2,
                    ["tile_y"] = 5,
                    ["item_id"] = "710",
                    ["qualified_item_id"] = "(O)710",
                    ["type"] = "StardewValley.Objects.CrabPot",
                    ["crab_pot_collect_status"] = "ready",
                    ["crab_pot_tile_index"] = 714,
                    ["crab_pot_ready_for_harvest"] = true,
                    ["crab_pot_bait_qualified_item_id"] = "(O)685",
                    ["crab_pot_output_runtime_type"] =
                        "StardewValley.Object",
                    ["crab_pot_output_qualified_item_id"] =
                        readyCrabPotQualifiedItemId,
                    ["crab_pot_output_quality"] = 0,
                    ["crab_pot_output_unit_state_sha256"] = outputHash,
                    ["crab_pot_expected_output_items_json"] = outputItems,
                    ["crab_pot_output_state_context"] =
                        "post_inventory_receive",
                    ["crab_pot_output_stack_before"] = 1,
                    ["crab_pot_output_stack_on_collect"] = 1,
                    ["crab_pot_book_double_roll_succeeded"] = false,
                    ["crab_pot_book_crabbing_owned"] = false,
                    ["crab_pot_book_double_applied"] = false,
                    ["crab_pot_fishing_experience_on_success_min"] = 5,
                    ["crab_pot_fishing_experience_on_success_max"] = 5,
                    ["crab_pot_experience_projection_status"] = "exact",
                    ["crab_pot_fish_collection_eligible"] = true,
                    ["crab_pot_fish_caught_count_before"] = 0,
                    ["crab_pot_fish_caught_count_after"] = 1,
                    ["crab_pot_fish_caught_max_size_before"] = 0,
                    ["crab_pot_catch_size_min"] = 1,
                    ["crab_pot_catch_size_max"] = 10,
                    ["crab_pot_catch_size_projection_status"] =
                        "runtime_rng_observed"
                }
            });
        }

        var serialized = state.ToJsonString(JsonOptions);
        var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            serialized,
            JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            GameVersion = "1.6.15",
            StateHash = SnapshotHash.ComputeStateHash(fields),
            GameTick = targetCaught ? 2 : 1,
            RealTimestamp = "2026-09-07T00:00:00Z",
            Completeness = "complete",
            State = fields
        };
    }

    private static string WriteTimingCalibration(string root)
    {
        var path = Path.Combine(root, "route-timing.json");
        File.WriteAllText(
            path,
            FutureRouteDateEvidenceProducerTests.CalibrationArtifactJson(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private static JsonObject RouteLocationEvidence(
        string locationId,
        int width,
        int height) => new()
        {
            ["location_id"] = locationId,
            ["map_width"] = width,
            ["map_height"] = height,
            ["projection_status"] =
                "exact_current_date_static_native_walkability",
            ["static_walkable_tile_count"] = width * height,
            ["static_walkable_tile_ranges"] = new JsonArray(
                Enumerable.Range(0, height)
                    .Select(y => (JsonNode)new JsonObject
                    {
                        ["y"] = y,
                        ["start_x"] = 0,
                        ["end_x"] = width - 1
                    })
                    .ToArray()),
            ["build_conditions"] = null,
            ["build_conditions_met"] = null,
            ["unsupported_route_action_record_count"] = 0,
            ["unsupported_route_action_tile_count"] = 0,
            ["unsupported_route_action_tiles"] = new JsonArray(),
            ["action_gates"] = new JsonArray()
        };

    private static JsonObject Field(JsonNode value) => new()
    {
        ["value"] = value,
        ["status"] = "available",
        ["source"] = new JsonObject
        {
            ["kind"] = "game_object",
            ["path"] = "test"
        },
        ["adapter"] = "test",
        ["read_at_tick"] = 1,
        ["confidence"] = 1
    };

    private static JsonObject SnapshotNode(SnapshotEnvelope snapshot) =>
        JsonSerializer.SerializeToNode(snapshot, JsonOptions)!.AsObject();

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new()
    {
        Name = name,
        Value = value
    };

    private sealed record Fixture(
        SnapshotEnvelope Before,
        SnapshotEnvelope After,
        string WindowIndexPath,
        string TimingCalibrationPath,
        SmallModelActionParameter[] Parameters) : IDisposable
    {
        public void Dispose()
        {
            File.Delete(WindowIndexPath);
            File.Delete(TimingCalibrationPath);
            Directory.Delete(Path.GetDirectoryName(WindowIndexPath)!);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };
}
