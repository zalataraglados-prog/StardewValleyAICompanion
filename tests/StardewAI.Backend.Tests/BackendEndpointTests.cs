using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using Xunit;

namespace StardewAI.Backend.Tests
{
    public sealed class BackendEndpointTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> factory;

        public BackendEndpointTests(WebApplicationFactory<Program> factory)
        {
            this.factory = factory;
        }

        [Fact]
        public async Task SnapshotAndCompilerEndpointsReturnTypedPreview()
        {
            using var client = factory.CreateClient();

            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);

            var previewResponse = await client.PostAsJsonAsync("/api/v1/action-compiler/compile", new
            {
                goal = "water crops today",
                mode = "efficiency"
            });
            Assert.Equal(HttpStatusCode.OK, previewResponse.StatusCode);

            using var previewJson = JsonDocument.Parse(await previewResponse.Content.ReadAsStringAsync());
            var root = previewJson.RootElement;
            Assert.Equal("feasible", root.GetProperty("feasibility").GetString());
            Assert.True(root.GetProperty("preview_only").GetBoolean());
            Assert.Equal("disabled", root.GetProperty("execution_permission").GetString());
            Assert.True(root.GetProperty("would_be_read_eligible").GetBoolean());
            Assert.True(root.GetProperty("would_bind").GetBoolean());
            Assert.False(root.GetProperty("would_compile").GetBoolean());
            Assert.False(root.GetProperty("would_be_executable").GetBoolean());
            Assert.Equal("farm.maintain_crops", root.GetProperty("selected_option").GetProperty("option_id").GetString());
        }

        [Fact]
        public async Task ActionCompilerCheckDistinguishesFeasibilityFromExecutionPermission()
        {
            using var client = factory.CreateClient();
            await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());

            var response = await client.GetAsync("/api/v1/action-compiler/check");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
            Assert.Equal("disabled", json.RootElement.GetProperty("execution_permission").GetString());
            Assert.True(json.RootElement.GetProperty("preview_only").GetBoolean());
        }

        [Fact]
        public async Task StardewInputLatestReturnsTypedWorldModel()
        {
            using var client = factory.CreateClient();
            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);

            var response = await client.GetAsync("/api/v1/stardew/input/latest?goal=water%20crops&mode=efficiency");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            Assert.Equal("world_model.v1", root.GetProperty("schema_version").GetString());
            Assert.Equal("efficiency", root.GetProperty("mode").GetString());
            Assert.True(root.GetProperty("completeness").GetProperty("all_required_facts_readable").GetBoolean());
            Assert.False(root.GetProperty("planner_inputs").GetProperty("blocked").GetBoolean());
            Assert.Equal("Farm", root.GetProperty("facts").GetProperty("player").GetProperty("location_id").GetString());
            Assert.Equal(610, root.GetProperty("facts").GetProperty("game").GetProperty("time").GetInt32());
        }

        [Fact]
        public async Task GrandpaEvaluationEndpointReturnsMaximumScoreGoalReport()
        {
            using var client = factory.CreateClient();
            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);

            var response = await client.GetAsync("/api/v1/goals/grandpa-evaluation/latest");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            Assert.Equal("grandpa_evaluation_goal.v2", root.GetProperty("schema_version").GetString());
            Assert.Equal(21, root.GetProperty("target_score").GetInt32());
            Assert.Equal(12, root.GetProperty("four_candle_score_threshold").GetInt32());
            Assert.Equal(4, root.GetProperty("current_candles").GetInt32());
            Assert.True(root.GetProperty("four_candle_milestone_met").GetBoolean());
            Assert.True(root.GetProperty("target_met").GetBoolean());
            Assert.Empty(root.GetProperty("missing_fact_paths").EnumerateArray());
        }

        [Fact]
        public async Task GrandpaTrainingEndpointReturnsDeterministicSample()
        {
            using var client = factory.CreateClient();
            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);

            var response = await client.GetAsync("/api/v1/training/grandpa-evaluation/latest");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            Assert.Equal("training_sample.v1", root.GetProperty("schema_version").GetString());
            Assert.Equal("grandpa_score", root.GetProperty("target").GetProperty("metric").GetString());
            Assert.True(root.GetProperty("target").GetProperty("complete").GetBoolean());
            Assert.False(root.GetProperty("feedback").GetProperty("executor_required").GetBoolean());
            Assert.False(root.GetProperty("feedback").GetProperty("available_now").GetBoolean());
            Assert.Empty(root.GetProperty("candidate_directions").EnumerateArray());
        }

        [Fact]
        public async Task OptionAvailabilityEndpointReturnsMissingFieldsAndExecutorBlocks()
        {
            using var client = factory.CreateClient();
            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);

            var response = await client.PostAsJsonAsync("/api/v1/planner/options/availability", new
            {
                candidate_option_ids = new[] { "economy.buy_supplies" }
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            Assert.Equal("option_availability.v1", root.GetProperty("schema_version").GetString());
            var option = root.GetProperty("options")[0];
            Assert.Equal("economy.buy_supplies", option.GetProperty("option_id").GetString());
            Assert.False(option.GetProperty("available").GetBoolean());
            Assert.Equal("blocked", option.GetProperty("status").GetString());
            Assert.Contains(option.GetProperty("missing_state_factors").EnumerateArray(), item => item.GetString() == "player.seed_inventory");
            Assert.Contains(option.GetProperty("missing_state_factors").EnumerateArray(), item => item.GetString() == "farm.crop_catalog");
            Assert.Contains(option.GetProperty("blocking_reasons").EnumerateArray(), item => item.GetString() == "missing_required_state");
        }

        [Fact]
        public async Task StrategyCommitmentEndpointsEnforceRevisionAndPreserveCancellation()
        {
            var repository = new InMemoryStrategyCommitmentRepository();
            using var isolatedFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStrategyCommitmentRepository>();
                services.AddSingleton<IStrategyCommitmentRepository>(repository);
            }));
            using var client = isolatedFactory.CreateClient();
            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent(includeStrategyCatalog: true));
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
            using var snapshotJson = JsonDocument.Parse(await snapshotResponse.Content.ReadAsStringAsync());
            var stateHash = snapshotJson.RootElement.GetProperty("state_hash").GetString();

            var create = await client.PostAsJsonAsync("/api/v1/strategy/commitments/crops/upsert", new
            {
                state_hash = stateHash,
                expected_ledger_revision = 0,
                commitment_id = "year3-spring-strawberry",
                source_decision_id = "strategy.test",
                seed_id = "745",
                tile_count = 40,
                planting_year = 3,
                planting_season = "spring",
                planting_day_of_month = 1,
                location_context = "outdoor_seasonal"
            });
            Assert.Equal(HttpStatusCode.OK, create.StatusCode);
            using var createJson = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
            Assert.Equal(1, createJson.RootElement.GetProperty("ledger").GetProperty("revision").GetInt32());

            var stale = await client.PostAsJsonAsync("/api/v1/strategy/commitments/crops/upsert", new
            {
                state_hash = stateHash,
                expected_ledger_revision = 0,
                commitment_id = "year3-spring-strawberry",
                source_decision_id = "strategy.test.stale",
                seed_id = "745",
                tile_count = 60,
                planting_year = 3,
                planting_season = "spring",
                planting_day_of_month = 1,
                location_context = "outdoor_seasonal"
            });
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

            var cancel = await client.PostAsJsonAsync("/api/v1/strategy/commitments/crops/year3-spring-strawberry/cancel", new
            {
                state_hash = stateHash,
                expected_ledger_revision = 1,
                reason = "test_replan"
            });
            Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

            var read = await client.GetAsync("/api/v1/strategy/commitments/latest?stateHash=" + stateHash);
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            using var readJson = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
            var commitment = Assert.Single(readJson.RootElement.GetProperty("crop_planting_commitments").EnumerateArray());
            Assert.Equal("cancelled", commitment.GetProperty("status").GetString());
            Assert.Equal(
                HttpStatusCode.NotFound,
                (await client.GetAsync("/api/v1/strategy/commitments/latest?stateHash=unknown")).StatusCode);
            Assert.Equal("test_replan", commitment.GetProperty("cancel_reason").GetString());
        }

        [Fact]
        public async Task MaterialReservationEndpointsPersistExactSlotAndEnforceRevision()
        {
            var repository = new InMemoryStrategyCommitmentRepository();
            using var isolatedFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStrategyCommitmentRepository>();
                services.AddSingleton<IStrategyCommitmentRepository>(repository);
            }));
            using var client = isolatedFactory.CreateClient();
            var snapshotResponse = await client.PostAsync(
                "/api/v1/snapshots",
                SampleSnapshotContent(includeMaterialGraph: true));
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
            using var snapshotJson = JsonDocument.Parse(await snapshotResponse.Content.ReadAsStringAsync());
            var stateHash = snapshotJson.RootElement.GetProperty("state_hash").GetString();

            var create = await client.PostAsJsonAsync(
                "/api/v1/strategy/commitments/materials/upsert",
                new
                {
                    state_hash = stateHash,
                    expected_ledger_revision = 0,
                    reservation_id = "keg-wood",
                    source_decision_id = "strategy.keg",
                    goal_id = "goal.keg",
                    node_id = "player:123",
                    slot_index = 1,
                    qualified_item_id = "(O)388",
                    quantity = 20,
                    purpose = "reserve wood for keg"
                });
            Assert.Equal(HttpStatusCode.OK, create.StatusCode);

            var stale = await client.PostAsJsonAsync(
                "/api/v1/strategy/commitments/materials/upsert",
                new
                {
                    state_hash = stateHash,
                    expected_ledger_revision = 0,
                    reservation_id = "keg-wood",
                    source_decision_id = "strategy.keg.stale",
                    goal_id = "goal.keg",
                    node_id = "player:123",
                    slot_index = 1,
                    qualified_item_id = "(O)388",
                    quantity = 10,
                    purpose = "stale update"
                });
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

            var cancel = await client.PostAsJsonAsync(
                "/api/v1/strategy/commitments/materials/keg-wood/cancel",
                new
                {
                    state_hash = stateHash,
                    expected_ledger_revision = 1,
                    reason = "goal_replanned"
                });
            Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

            var read = await client.GetAsync(
                "/api/v1/strategy/commitments/latest?stateHash=" + stateHash);
            using var readJson = JsonDocument.Parse(await read.Content.ReadAsStringAsync());
            var reservation = Assert.Single(
                readJson.RootElement.GetProperty("material_reservations").EnumerateArray());
            Assert.Equal("cancelled", reservation.GetProperty("status").GetString());
            Assert.Equal("goal_replanned", reservation.GetProperty("cancel_reason").GetString());
        }

        [Fact]
        public async Task DispatchReadinessRejectsQueueAfterMaterialLedgerChanges()
        {
            var repository = new InMemoryStrategyCommitmentRepository();
            using var isolatedFactory = factory.WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IStrategyCommitmentRepository>();
                    services.AddSingleton<IStrategyCommitmentRepository>(repository);
                }));
            using var client = isolatedFactory.CreateClient();
            var snapshotResponse = await client.PostAsync(
                "/api/v1/snapshots",
                SampleSnapshotContent(includeMaterialGraph: true));
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
            using var snapshotJson = JsonDocument.Parse(
                await snapshotResponse.Content.ReadAsStringAsync());
            var stateHash = snapshotJson.RootElement.GetProperty("state_hash").GetString()!;

            var create = await client.PostAsJsonAsync(
                "/api/v1/strategy/commitments/materials/upsert",
                new
                {
                    state_hash = stateHash,
                    expected_ledger_revision = 0,
                    reservation_id = "keg-wood",
                    source_decision_id = "strategy.keg",
                    goal_id = "goal.keg",
                    node_id = "player:123",
                    slot_index = 1,
                    qualified_item_id = "(O)388",
                    quantity = 20,
                    purpose = "reserve wood for keg"
                });
            Assert.Equal(HttpStatusCode.OK, create.StatusCode);

            const string queueId = "dispatch-readiness-test";
            const string queueItemId = "dispatch-readiness-item";
            var item = new ActionQueueItem
            {
                QueueItemId = queueItemId,
                OptionId = "executor.craft_machine_item",
                Status = "pending",
                NormalizedCommand = new NormalizedCommand
                {
                    Parameters = new[]
                    {
                        Parameter("material_reservation_guard_status", "ready"),
                        Parameter("material_reservation_ledger_id", "test-ledger"),
                        Parameter("material_reservation_ledger_revision", "1"),
                        Parameter("material_reservation_ids_json", "[\"keg-wood\"]"),
                        Parameter("commitment_ledger_id", "test-ledger"),
                        Parameter("commitment_ledger_revision", "1")
                    }
                }
            };
            isolatedFactory.Services.GetRequiredService<StateStore>().ActionQueues[queueId] =
                new ActionQueueEnvelope
                {
                    QueueId = queueId,
                    StateHash = stateHash,
                    Items = new[] { item }
                };

            var readinessUrl =
                "/api/v1/action-queues/" + queueId + "/items/" + queueItemId +
                "/dispatch-readiness?stateHash=" + stateHash;
            var ready = await client.GetAsync(readinessUrl);
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            using (var readyJson = JsonDocument.Parse(
                       await ready.Content.ReadAsStringAsync()))
            {
                Assert.True(readyJson.RootElement.GetProperty("ready").GetBoolean());
            }

            var cancel = await client.PostAsJsonAsync(
                "/api/v1/strategy/commitments/materials/keg-wood/cancel",
                new
                {
                    state_hash = stateHash,
                    expected_ledger_revision = 1,
                    reason = "goal_replanned"
                });
            Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

            var blocked = await client.GetAsync(readinessUrl);
            Assert.Equal(HttpStatusCode.OK, blocked.StatusCode);
            using var blockedJson = JsonDocument.Parse(
                await blocked.Content.ReadAsStringAsync());
            Assert.False(blockedJson.RootElement.GetProperty("ready").GetBoolean());
            Assert.Contains(
                blockedJson.RootElement.GetProperty("blocking_reasons").EnumerateArray(),
                row => row.GetString() == "dispatch_strategy_ledger_revision_drifted");
        }

        [Fact]
        public async Task RankOptionsWithStateHashFiltersUnavailableCandidatesBeforeScoring()
        {
            using var client = factory.CreateClient();
            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
            using var snapshotJson = JsonDocument.Parse(await snapshotResponse.Content.ReadAsStringAsync());
            var stateHash = snapshotJson.RootElement.GetProperty("state_hash").GetString();

            var response = await client.PostAsJsonAsync("/api/v1/planner/baseline/rank-options", new
            {
                state_hash = stateHash,
                candidate_option_ids = new[] { "economy.buy_supplies", "strategy.grandpa_progress" },
                training_report = new
                {
                    schema_version = "baseline_training_report.v1",
                    dataset_path = "test.jsonl",
                    row_count = 2,
                    included_row_count = 2,
                    excluded_calibration_row_count = 0,
                    option_scores = new[]
                    {
                        new
                        {
                            option_id = "economy.buy_supplies",
                            example_count = 10,
                            average_goal_progress_delta = 10.0,
                            average_total_reward = 10.0,
                            hard_block_rate = 0.0
                        },
                        new
                        {
                            option_id = "strategy.grandpa_progress",
                            example_count = 1,
                            average_goal_progress_delta = 0.1,
                            average_total_reward = 0.1,
                            hard_block_rate = 0.0
                        }
                    }
                }
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            Assert.Equal("availability_policy_prediction.v1", root.GetProperty("schema_version").GetString());
            var goalResolution = root.GetProperty("goal_resolution");
            Assert.Equal("not_applicable", goalResolution.GetProperty("status").GetString());
            Assert.Equal(string.Empty, goalResolution.GetProperty("effective_goal_id").GetString());
            var ranked = root.GetProperty("prediction").GetProperty("ranked_options").EnumerateArray().ToArray();
            Assert.Empty(ranked);
            var availability = root.GetProperty("availability").GetProperty("options").EnumerateArray().ToArray();
            Assert.Contains(availability, item => item.GetProperty("option_id").GetString() == "economy.buy_supplies" && !item.GetProperty("available").GetBoolean());
            Assert.Contains(availability, item => item.GetProperty("option_id").GetString() == "strategy.grandpa_progress" && !item.GetProperty("available").GetBoolean());
        }

        [Fact]
        public async Task RankOptionsFailsClosedWhenStructuredPolicyIsRequiredWithoutCheckpoint()
        {
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync(
                "/api/v1/planner/baseline/rank-options",
                new { require_structured_policy = true });

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(
                "policy_checkpoint_path",
                await response.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        [Fact]
        public async Task DailyPlanCompileEndpointReturnsSmallModelPlanFromRankedCandidates()
        {
            using var client = factory.CreateClient();

            var response = await client.PostAsJsonAsync("/api/v1/planner/daily-plan/compile", new
            {
                state_hash = "state.test",
                goal_id = "daily.shop",
                ranked_event_candidates = new[]
                {
                    new
                    {
                        candidate_id = "interact:Town:11,10:OpenShop:SeedShop",
                        kind = "interact_endpoint",
                        rank = 1,
                        timeline_status = "deferred",
                        scheduled_wait_cost = 1200,
                        location_id = "Town",
                        tile_x = 11,
                        tile_y = 10,
                        expected_effect = "move_to_adjacent=10,10;preview_interact=OpenShop",
                        estimated_ticks = 90
                    }
                }
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            Assert.Equal("daily_plan_compile_response.v1", root.GetProperty("schema_version").GetString());
            var plan = root.GetProperty("plan");
            Assert.Equal("small_model_plan.v1", plan.GetProperty("schema_version").GetString());
            Assert.Equal("daily_candidate_plan", plan.GetProperty("plan_type").GetString());
            var steps = plan.GetProperty("steps").EnumerateArray().ToArray();
            var wait = Assert.Single(steps);
            Assert.Equal("wait_ticks", wait.GetProperty("kind").GetString());
            Assert.Equal(600, wait.GetProperty("wait_ticks").GetInt32());
            Assert.Contains("fresh_snapshot_replan_required=true", wait.GetProperty("expected_effects").EnumerateArray().Select(value => value.GetString()));
        }

        [Fact]
        public async Task DailyPlanCompileActionQueuePreservesCandidateAuditThroughTrainingFeatureRow()
        {
            using var client = factory.CreateClient();
            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
            using var snapshotJson = JsonDocument.Parse(await snapshotResponse.Content.ReadAsStringAsync());
            var stateHash = snapshotJson.RootElement.GetProperty("state_hash").GetString();

            var response = await client.PostAsJsonAsync("/api/v1/planner/daily-plan/compile", new
            {
                state_hash = stateHash,
                goal_id = "daily.audit",
                compile_action_queue = true,
                max_candidates = 4,
                ranked_event_candidates = new[]
                {
                    new
                    {
                        candidate_id = "water:Farm:1,2",
                        kind = "water_crop_tile",
                        rank = 1,
                        timeline_status = "ready_now",
                        location_id = "Farm",
                        tile_x = 1,
                        tile_y = 2,
                        expected_effect = "farm.crops[1,2].needs_watering=false",
                        estimated_ticks = 60,
                        energy_cost = 2
                    },
                    new
                    {
                        candidate_id = "water:Farm:3,4",
                        kind = "water_crop_tile",
                        rank = 2,
                        timeline_status = "ready_now",
                        location_id = "Farm",
                        tile_x = 3,
                        tile_y = 4,
                        expected_effect = "farm.crops[3,4].needs_watering=false",
                        estimated_ticks = 100000,
                        energy_cost = 2
                    }
                }
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = json.RootElement;
            var plan = root.GetProperty("plan");
            var audit = plan.GetProperty("candidate_audit").EnumerateArray().ToArray();
            Assert.Equal(2, audit.Length);
            Assert.Equal("accepted", audit[0].GetProperty("decision").GetString());
            Assert.Equal("skipped", audit[1].GetProperty("decision").GetString());
            Assert.Contains(audit[1].GetProperty("reasons").EnumerateArray(), item =>
                item.GetString() == "aggregate_time_budget_exceeded");

            var queue = root.GetProperty("action_queue");
            Assert.Equal("action_queue.v1", queue.GetProperty("schema_version").GetString());
            var queueAudit = queue.GetProperty("candidate_audit").EnumerateArray().ToArray();
            Assert.Equal(2, queueAudit.Length);
            Assert.Equal("aggregate_time_budget_exceeded", queueAudit[1].GetProperty("reasons")[0].GetString());
            var queueId = queue.GetProperty("queue_id").GetString();

            var episodeResponse = await client.GetAsync($"/api/v1/action-queues/{queueId}/training-episode");
            Assert.Equal(HttpStatusCode.OK, episodeResponse.StatusCode);
            using var episodeJson = JsonDocument.Parse(await episodeResponse.Content.ReadAsStringAsync());
            var episodeAudit = episodeJson.RootElement.GetProperty("candidate_audit").EnumerateArray().ToArray();
            Assert.Equal(2, episodeAudit.Length);
            Assert.Equal("skipped", episodeAudit[1].GetProperty("decision").GetString());

            var featureResponse = await client.GetAsync($"/api/v1/action-queues/{queueId}/training-feature-row");
            Assert.Equal(HttpStatusCode.OK, featureResponse.StatusCode);
            using var featureJson = JsonDocument.Parse(await featureResponse.Content.ReadAsStringAsync());
            var actionFeatureVector = featureJson.RootElement
                .GetProperty("action_features")
                .GetProperty("features");
            Assert.Contains(actionFeatureVector.GetProperty("numeric").EnumerateArray(), item =>
                item.GetProperty("name").GetString() == "candidate_audit.accepted_count" &&
                item.GetProperty("value").GetDouble() == 1);
            Assert.Contains(actionFeatureVector.GetProperty("numeric").EnumerateArray(), item =>
                item.GetProperty("name").GetString() == "candidate_audit.skipped_time_budget_count" &&
                item.GetProperty("value").GetDouble() == 1);
            Assert.Contains(actionFeatureVector.GetProperty("categorical").EnumerateArray(), item =>
                item.GetProperty("name").GetString() == "candidate_audit.primary_skip_reason" &&
                item.GetProperty("value").GetString() == "aggregate_time_budget_exceeded");
            Assert.Contains(actionFeatureVector.GetProperty("boolean").EnumerateArray(), item =>
                item.GetProperty("name").GetString() == "candidate_audit.has_time_budget_skip" &&
                item.GetProperty("value").GetBoolean());
        }

        [Fact]
        public async Task OptionAvailabilityEndpointAcceptsBoundCandidatesAndReturnsCompilerBlockReasons()
        {
            using var client = factory.CreateClient();
            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);

            var response = await client.PostAsJsonAsync("/api/v1/planner/options/availability", new
            {
                candidates = new[]
                {
                    new
                    {
                        option_id = "executor.wait_ticks",
                        parameters = new[]
                        {
                            new { name = "wait_ticks", value = "0" }
                        }
                    }
                }
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var option = json.RootElement.GetProperty("options")[0];
            Assert.Equal("executor.wait_ticks", option.GetProperty("option_id").GetString());
            Assert.False(option.GetProperty("available").GetBoolean());
            Assert.Equal("blocked", option.GetProperty("status").GetString());
            Assert.Equal("bound", option.GetProperty("binding_status").GetString());
            Assert.Equal("blocked", option.GetProperty("compile_status").GetString());
            Assert.Contains(option.GetProperty("blocking_reasons").EnumerateArray(), item => item.GetString() == "wait_ticks_1_600_required");
            Assert.DoesNotContain(option.GetProperty("blocking_reasons").EnumerateArray(), item => item.GetString() == "queue_global_compiler_block");
            Assert.Equal(1, option.GetProperty("parameters").GetArrayLength());
        }

        [Fact]
        public async Task SmallModelActionQueueCompilesAndDryRunExecutes()
        {
            using var client = factory.CreateClient();
            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
            using var snapshotJson = JsonDocument.Parse(await snapshotResponse.Content.ReadAsStringAsync());
            var stateHash = snapshotJson.RootElement.GetProperty("state_hash").GetString();

            var compileResponse = await client.PostAsJsonAsync("/api/v1/small-model/action-queue/compile", new
            {
                schema_version = "small_model_action.v1",
                model_output_id = "model-output.test",
                source_model = "small-model.test",
                state_hash = stateHash,
                goal_id = "goal.test",
                execution_mode = "training_singleplayer",
                actor = new
                {
                    actor_id = "training_farmer.main",
                    actor_type = "training_farmer",
                    control_surface = "training_sandbox"
                },
                actions = new[]
                {
                    new
                    {
                        action_id = "action.test",
                        option_id = "executor.water_crop",
                        rationale = "water the exact candidate selected by the daily planner",
                        parameters = new[]
                        {
                            new { name = "target_location", value = "Farm" },
                            new { name = "target_tile_x", value = "1" },
                            new { name = "target_tile_y", value = "2" }
                        }
                    }
                }
            });

            Assert.Equal(HttpStatusCode.OK, compileResponse.StatusCode);
            using var queueJson = JsonDocument.Parse(await compileResponse.Content.ReadAsStringAsync());
            var queueRoot = queueJson.RootElement;
            Assert.Equal("action_queue.v1", queueRoot.GetProperty("schema_version").GetString());
            Assert.Equal("pending", queueRoot.GetProperty("status").GetString());
            Assert.Equal("training_singleplayer", queueRoot.GetProperty("execution_mode").GetString());
            Assert.Equal("training_farmer.main", queueRoot.GetProperty("actor").GetProperty("actor_id").GetString());
            var queueId = queueRoot.GetProperty("queue_id").GetString();

            var executeResponse = await client.PostAsync($"/api/v1/action-queues/{queueId}/execute", null);

            Assert.Equal(HttpStatusCode.OK, executeResponse.StatusCode);
            using var resultJson = JsonDocument.Parse(await executeResponse.Content.ReadAsStringAsync());
            var resultRoot = resultJson.RootElement;
            Assert.Equal("execution_batch_result.v1", resultRoot.GetProperty("schema_version").GetString());
            Assert.Equal("dry_run", resultRoot.GetProperty("executor_mode").GetString());
            Assert.Equal("dry_run_ready", resultRoot.GetProperty("status").GetString());
            Assert.Equal("training_farmer.main", resultRoot.GetProperty("actor").GetProperty("actor_id").GetString());

            var sandboxResponse = await client.PostAsync($"/api/v1/action-queues/{queueId}/execute-training-sandbox", null);

            Assert.Equal(HttpStatusCode.OK, sandboxResponse.StatusCode);
            using var sandboxJson = JsonDocument.Parse(await sandboxResponse.Content.ReadAsStringAsync());
            var sandboxRoot = sandboxJson.RootElement;
            Assert.Equal("training_sandbox", sandboxRoot.GetProperty("executor_mode").GetString());
            Assert.Equal("applied", sandboxRoot.GetProperty("status").GetString());
            Assert.True(sandboxRoot.GetProperty("feedback_available").GetBoolean());
            Assert.NotEqual(string.Empty, sandboxRoot.GetProperty("after_state_hash").GetString());

            var transitionResponse = await client.PostAsync($"/api/v1/action-queues/{queueId}/simulate-training-transition", null);

            Assert.Equal(HttpStatusCode.OK, transitionResponse.StatusCode);
            using var transitionJson = JsonDocument.Parse(await transitionResponse.Content.ReadAsStringAsync());
            var transitionRoot = transitionJson.RootElement;
            Assert.Equal("simulated_transition.v1", transitionRoot.GetProperty("schema_version").GetString());
            Assert.False(transitionRoot.GetProperty("blocked").GetBoolean());
            Assert.StartsWith("sim.", transitionRoot.GetProperty("after_state_hash").GetString());
            Assert.Contains(transitionRoot.GetProperty("changed_facts").EnumerateArray(), item =>
                item.GetProperty("path").GetString() == "current_location.crops[1,2].needs_watering" &&
                item.GetProperty("after").GetString() == "false");
        }

        [Fact]
        public async Task MockModelOutputCompilesAndSimulatesTrainingTransition()
        {
            using var client = factory.CreateClient();
            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
            using var snapshotJson = JsonDocument.Parse(await snapshotResponse.Content.ReadAsStringAsync());
            var stateHash = snapshotJson.RootElement.GetProperty("state_hash").GetString();

            var mockResponse = await client.PostAsJsonAsync("/api/v1/mock-model/small-model-action", new
            {
                goal = "water crops",
                state_hash = stateHash,
                execution_mode = "training_singleplayer"
            });

            Assert.Equal(HttpStatusCode.OK, mockResponse.StatusCode);
            var mockPayload = await mockResponse.Content.ReadAsStringAsync();
            using var mockJson = JsonDocument.Parse(mockPayload);
            Assert.Equal("small_model_action.v1", mockJson.RootElement.GetProperty("schema_version").GetString());
            Assert.Equal("mock-small-model.rule.v1", mockJson.RootElement.GetProperty("source_model").GetString());
            Assert.Equal("farm.maintain_crops", mockJson.RootElement.GetProperty("actions")[0].GetProperty("option_id").GetString());
            Assert.Contains(mockJson.RootElement.GetProperty("actions")[0].GetProperty("parameters").EnumerateArray(), item =>
                item.GetProperty("name").GetString() == "intent_category" &&
                item.GetProperty("value").GetString() == "mechanical");

            var compileResponse = await client.PostAsync(
                "/api/v1/small-model/action-queue/compile",
                new StringContent(mockPayload, Encoding.UTF8, "application/json"));

            Assert.Equal(HttpStatusCode.OK, compileResponse.StatusCode);
            using var directQueueJson = JsonDocument.Parse(await compileResponse.Content.ReadAsStringAsync());
            Assert.Equal("blocked", directQueueJson.RootElement.GetProperty("status").GetString());
            Assert.Contains(directQueueJson.RootElement.GetProperty("items")[0].GetProperty("blocking_reasons").EnumerateArray(), item =>
                item.GetString() == "full_action_step_compilation_empty");

            var dailyPlanResponse = await client.PostAsJsonAsync("/api/v1/planner/daily-plan/compile", new
            {
                state_hash = stateHash,
                goal_id = "goal.mock.crop-maintenance",
                compile_action_queue = true,
                ranked_event_candidates = new[]
                {
                    new
                    {
                        candidate_id = "water:Farm:1,2",
                        option_id = "farm.maintain_crops",
                        kind = "water_crop_tile",
                        rank = 1,
                        timeline_status = "ready_now",
                        available = true,
                        location_id = "Farm",
                        tile_x = 1,
                        tile_y = 2,
                        expected_effect = "current_location.crops[1,2].needs_watering=false",
                        estimated_ticks = 60,
                        energy_cost = 2
                    }
                }
            });

            Assert.Equal(HttpStatusCode.OK, dailyPlanResponse.StatusCode);
            using var dailyPlanJson = JsonDocument.Parse(await dailyPlanResponse.Content.ReadAsStringAsync());
            var queueRoot = dailyPlanJson.RootElement.GetProperty("action_queue");
            var queueId = queueRoot.GetProperty("queue_id").GetString();
            Assert.Equal("pending", queueRoot.GetProperty("status").GetString());
            Assert.Equal("executor.water_crop", queueRoot.GetProperty("items")[0].GetProperty("option_id").GetString());

            var timeBudgetResponse = await client.GetAsync($"/api/v1/action-queues/{queueId}/time-budget");

            Assert.Equal(HttpStatusCode.OK, timeBudgetResponse.StatusCode);
            using var timeBudgetJson = JsonDocument.Parse(await timeBudgetResponse.Content.ReadAsStringAsync());
            Assert.Equal("time_budget.v1", timeBudgetJson.RootElement.GetProperty("schema_version").GetString());
            Assert.Equal("perfect_human_player", timeBudgetJson.RootElement.GetProperty("execution_profile").GetString());
            Assert.True(timeBudgetJson.RootElement.GetProperty("fits_required").GetBoolean());

            var transitionResponse = await client.PostAsync($"/api/v1/action-queues/{queueId}/simulate-training-transition", null);

            Assert.Equal(HttpStatusCode.OK, transitionResponse.StatusCode);
            using var transitionJson = JsonDocument.Parse(await transitionResponse.Content.ReadAsStringAsync());
            Assert.Equal("simulated_transition.v1", transitionJson.RootElement.GetProperty("schema_version").GetString());
            Assert.False(transitionJson.RootElement.GetProperty("blocked").GetBoolean());
            Assert.Contains(transitionJson.RootElement.GetProperty("changed_facts").EnumerateArray(), item =>
                item.GetProperty("path").GetString() == "current_location.crops[1,2].needs_watering");

            var episodeResponse = await client.GetAsync($"/api/v1/action-queues/{queueId}/training-episode");

            Assert.Equal(HttpStatusCode.OK, episodeResponse.StatusCode);
            using var episodeJson = JsonDocument.Parse(await episodeResponse.Content.ReadAsStringAsync());
            var episodeRoot = episodeJson.RootElement;
            Assert.Equal("training_episode.v1", episodeRoot.GetProperty("schema_version").GetString());
            Assert.Equal(queueId, episodeRoot.GetProperty("queue_id").GetString());
            Assert.Equal("executor.water_crop", episodeRoot.GetProperty("action_summary").GetProperty("option_ids")[0].GetString());
            Assert.False(episodeRoot.GetProperty("hard_feasibility").GetProperty("blocked").GetBoolean());
            Assert.Equal(0.09, episodeRoot.GetProperty("strategy_value").GetProperty("goal_progress_delta").GetDouble());
            Assert.Contains(episodeRoot.GetProperty("strategy_value").GetProperty("reward_terms").EnumerateArray(), item =>
                item.GetProperty("name").GetString() == "crop_watered" &&
                item.GetProperty("source").GetString() == "simulated_transition.changed_facts");
            Assert.Equal("perfect_human_player", episodeRoot.GetProperty("executor_calibration").GetProperty("execution_profile").GetString());
            Assert.Contains(episodeRoot.GetProperty("executor_calibration").GetProperty("changed_facts").EnumerateArray(), item =>
                item.GetProperty("path").GetString() == "current_location.crops[1,2].needs_watering");

            var featureResponse = await client.GetAsync($"/api/v1/action-queues/{queueId}/training-feature-row");

            Assert.Equal(HttpStatusCode.OK, featureResponse.StatusCode);
            using var featureJson = JsonDocument.Parse(await featureResponse.Content.ReadAsStringAsync());
            var featureRoot = featureJson.RootElement;
            Assert.Equal("training_feature_row.v1", featureRoot.GetProperty("schema_version").GetString());
            Assert.Equal(queueId, featureRoot.GetProperty("queue_id").GetString());
            Assert.Equal("executor.water_crop", featureRoot.GetProperty("action_features").GetProperty("option_ids")[0].GetString());
            Assert.Equal(0.09, featureRoot.GetProperty("labels").GetProperty("goal_progress_delta").GetDouble());
            Assert.Contains(featureRoot.GetProperty("state_features").GetProperty("numeric").EnumerateArray(), item =>
                item.GetProperty("name").GetString() == "current_location.crops_needing_watering" &&
                item.GetProperty("value").GetDouble() == 1);
            Assert.Contains(featureRoot.GetProperty("action_features").GetProperty("features").GetProperty("categorical").EnumerateArray(), item =>
                item.GetProperty("name").GetString() == "action.intent_category" &&
                item.GetProperty("value").GetString() == "mechanical");
            Assert.Equal("executor_calibration", featureRoot.GetProperty("action_features").GetProperty("training_role").GetString());
            Assert.Equal("calibration_only", featureRoot.GetProperty("action_features").GetProperty("learning_scope").GetString());
            Assert.True(featureRoot.GetProperty("action_features").GetProperty("exclude_from_policy_training").GetBoolean());

            var datasetPath = Path.Combine(Path.GetTempPath(), "stardewai-backend-tests", Guid.NewGuid().ToString("N"), "rows.jsonl");
            var appendResponse = await client.PostAsJsonAsync($"/api/v1/action-queues/{queueId}/training-feature-row/append", new
            {
                dataset_path = datasetPath
            });

            Assert.Equal(HttpStatusCode.OK, appendResponse.StatusCode);
            using var appendJson = JsonDocument.Parse(await appendResponse.Content.ReadAsStringAsync());
            Assert.Equal("training_dataset_append.v1", appendJson.RootElement.GetProperty("schema_version").GetString());
            Assert.Equal(1, appendJson.RootElement.GetProperty("row_count").GetInt32());
            Assert.True(File.Exists(datasetPath));

            var trainResponse = await client.PostAsJsonAsync("/api/v1/training/baseline/train", new
            {
                dataset_path = datasetPath
            });

            Assert.Equal(HttpStatusCode.OK, trainResponse.StatusCode);
            using var trainJson = JsonDocument.Parse(await trainResponse.Content.ReadAsStringAsync());
            var trainRoot = trainJson.RootElement;
            Assert.Equal("baseline_training_report.v1", trainRoot.GetProperty("schema_version").GetString());
            Assert.Equal(1, trainRoot.GetProperty("row_count").GetInt32());
            Assert.Equal(0, trainRoot.GetProperty("included_row_count").GetInt32());
            Assert.Equal(1, trainRoot.GetProperty("excluded_calibration_row_count").GetInt32());
            Assert.Equal(0, trainRoot.GetProperty("excluded_admission_row_count").GetInt32());
            Assert.Equal(58, trainRoot.GetProperty("training_allowlist").GetArrayLength());
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "processing.crack_geode");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "animals.purchase");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "rewards.claim_prize_ticket");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "skills.claim_mastery");
            Assert.DoesNotContain(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "buildings.paint");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "crafting.cook_recipe");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "crafting.forge_item");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "minigame.play_darts");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "minigame.play_prairie_king");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "festival.manage_grange_display");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "festival.play_fishing_game");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "festival.play_slingshot_game");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "festival.play_strength_game");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "festival.spin_wheel");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "fishing.catch_fish");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "mining.choose_dwarf_statue_power");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "rewards.claim_pot_of_gold");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "rewards.claim_statue_blessing");
            Assert.DoesNotContain(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "world.rotate_house_plant");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "farming.collect_slime_ball");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "skills.choose_profession");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "mail.process_letter");
            Assert.Contains(
                trainRoot.GetProperty("training_allowlist").EnumerateArray(),
                item => item.GetString() == "minigame.play_calico_jack");
            Assert.Equal(0, trainRoot.GetProperty("option_scores").GetArrayLength());

            var predictResponse = await client.PostAsJsonAsync("/api/v1/training/baseline/predict", new
            {
                dataset_path = datasetPath,
                candidate_option_ids = new[] { "farm.maintain_crops", "social.gift_npc" }
            });

            Assert.Equal(HttpStatusCode.OK, predictResponse.StatusCode);
            using var predictJson = JsonDocument.Parse(await predictResponse.Content.ReadAsStringAsync());
            var predictRoot = predictJson.RootElement;
            Assert.Equal("policy_prediction.v1", predictRoot.GetProperty("schema_version").GetString());
            Assert.Equal(1, predictRoot.GetProperty("ranked_options").GetArrayLength());
            Assert.Equal("social.gift_npc", predictRoot.GetProperty("ranked_options")[0].GetProperty("option_id").GetString());
            Assert.Equal(1, predictRoot.GetProperty("ranked_options")[0].GetProperty("rank").GetInt32());
            Assert.Equal("unseen_option", predictRoot.GetProperty("ranked_options")[0].GetProperty("evidence").GetString());

            var rankResponse = await client.PostAsJsonAsync("/api/v1/planner/baseline/rank-options", new
            {
                dataset_path = datasetPath
            });

            Assert.Equal(HttpStatusCode.OK, rankResponse.StatusCode);
            using var rankJson = JsonDocument.Parse(await rankResponse.Content.ReadAsStringAsync());
            var rankRoot = rankJson.RootElement;
            Assert.Equal("policy_prediction.v1", rankRoot.GetProperty("schema_version").GetString());
            Assert.DoesNotContain(rankRoot.GetProperty("ranked_options").EnumerateArray(), item =>
                item.GetProperty("option_id").GetString() == "farm.maintain_crops");
            Assert.True(rankRoot.GetProperty("ranked_options").GetArrayLength() >= 4);
        }

        [Fact]
        public async Task SnapshotIngestRejectsMismatchedHash()
        {
            using var client = factory.CreateClient();

            var response = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent("bad-hash"));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains("state_hash mismatch", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task SnapshotIngestRejectsUnavailableDefaultValue()
        {
            using var client = factory.CreateClient();

            var response = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent(unavailableCarriesDefault: true));

            Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
            Assert.Contains("non-readable status must not carry a default value", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public void DatasetPathResolverDefaultsToETrainingIsolationWhenDriveExists()
        {
            var resolved = DatasetPathResolver.Resolve(null);

            if (Directory.Exists("E:\\"))
            {
                Assert.Equal(@"E:\StardewAITraining\datasets\training-feature-rows.jsonl", resolved);
            }
            else
            {
                Assert.EndsWith(Path.Combine("datasets", "training-feature-rows.jsonl"), resolved);
            }
        }

        [Fact]
        public async Task TrainingSessionLaunchBlocksRealGameWithoutExplicitLaunchPermission()
        {
            using var client = factory.CreateClient();
            var root = Path.Combine(Path.GetTempPath(), "stardewai-backend-tests", Guid.NewGuid().ToString("N"));
            var runtime = Path.Combine(root, "Stardew Valley");
            var smapi = Path.Combine(runtime, "StardewModdingAPI.exe");
            Directory.CreateDirectory(runtime);
            File.WriteAllText(smapi, string.Empty);

            var response = await client.PostAsJsonAsync("/api/v1/training/session/launch", new
            {
                mode = "stardew_windowed",
                root_path = root,
                game_executable_path = smapi,
                game_working_directory = runtime,
                allow_game_launch = false,
                sound_enabled = false,
                save_isolation_path = Path.Combine(root, "saves")
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var rootElement = json.RootElement;
            Assert.Equal("training_launch_result.v1", rootElement.GetProperty("schema_version").GetString());
            Assert.True(rootElement.GetProperty("blocked").GetBoolean());
            Assert.False(rootElement.GetProperty("started").GetBoolean());
            Assert.False(rootElement.GetProperty("launch_attempted").GetBoolean());
            Assert.Contains(rootElement.GetProperty("block_reasons").EnumerateArray(), item =>
                item.GetString() == "real_game_launch_requires_allow_game_launch_true");
            Assert.Equal("disabled", rootElement.GetProperty("manifest").GetProperty("game_launch").GetString());
            Assert.Equal("disabled", rootElement.GetProperty("manifest").GetProperty("sound").GetString());
            Assert.Equal("smapi", rootElement.GetProperty("manifest").GetProperty("executable_kind").GetString());
            Assert.True(File.Exists(rootElement.GetProperty("manifest").GetProperty("manifest_path").GetString()));
        }

        [Fact]
        public async Task TrainingSessionPrepareWritesOfflineManifest()
        {
            using var client = factory.CreateClient();
            var root = Path.Combine(Path.GetTempPath(), "stardewai-backend-tests", Guid.NewGuid().ToString("N"));

            var response = await client.PostAsJsonAsync("/api/v1/training/session/prepare", new
            {
                mode = "offline_smoke",
                root_path = root,
                allow_game_launch = true,
                sound_enabled = false
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var rootElement = json.RootElement;
            Assert.False(rootElement.GetProperty("blocked").GetBoolean());
            Assert.False(rootElement.GetProperty("started").GetBoolean());
            var manifest = rootElement.GetProperty("manifest");
            Assert.Equal("training_run_manifest.v1", manifest.GetProperty("schema_version").GetString());
            Assert.Equal("offline_smoke", manifest.GetProperty("mode").GetString());
            Assert.Equal(root, manifest.GetProperty("root_path").GetString());
            Assert.EndsWith(Path.Combine("datasets", "training-feature-rows.jsonl"), manifest.GetProperty("dataset_path").GetString());
            Assert.True(File.Exists(manifest.GetProperty("manifest_path").GetString()));
        }

        [Fact]
        public async Task TrainingSessionPrepareAcceptsIsolatedSmapiPathWithoutStartingGame()
        {
            using var client = factory.CreateClient();
            var root = Path.Combine(Path.GetTempPath(), "stardewai-backend-tests", Guid.NewGuid().ToString("N"));
            var runtime = Path.Combine(root, "Stardew Valley");
            var smapi = Path.Combine(runtime, "StardewModdingAPI.exe");
            Directory.CreateDirectory(runtime);
            File.WriteAllText(smapi, string.Empty);

            var response = await client.PostAsJsonAsync("/api/v1/training/session/prepare", new
            {
                mode = "stardew_windowed",
                root_path = root,
                game_executable_path = smapi,
                game_working_directory = runtime,
                allow_game_launch = true,
                sound_enabled = false,
                save_isolation_path = Path.Combine(root, "saves")
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var rootElement = json.RootElement;
            Assert.False(rootElement.GetProperty("blocked").GetBoolean());
            Assert.False(rootElement.GetProperty("started").GetBoolean());
            Assert.False(rootElement.GetProperty("launch_attempted").GetBoolean());
            var manifest = rootElement.GetProperty("manifest");
            Assert.Equal("requested", manifest.GetProperty("game_launch").GetString());
            Assert.Equal("disabled", manifest.GetProperty("sound").GetString());
            Assert.Equal("minimized", manifest.GetProperty("window_style").GetString());
            Assert.Equal("smapi", manifest.GetProperty("executable_kind").GetString());
            Assert.Contains(manifest.GetProperty("environment_overrides").EnumerateArray(), item =>
                item.GetProperty("name").GetString() == "SDL_AUDIODRIVER" &&
                item.GetProperty("value").GetString() == "dummy");
            Assert.True(Directory.Exists(Path.Combine(root, "saves")));
        }

        [Fact]
        public async Task TrainingSessionPrepareRejectsVanillaExecutableForTransparentTraining()
        {
            using var client = factory.CreateClient();
            var root = Path.Combine(Path.GetTempPath(), "stardewai-backend-tests", Guid.NewGuid().ToString("N"));
            var runtime = Path.Combine(root, "Stardew Valley");
            var vanilla = Path.Combine(runtime, "Stardew Valley.exe");
            Directory.CreateDirectory(runtime);
            File.WriteAllText(vanilla, string.Empty);

            var response = await client.PostAsJsonAsync("/api/v1/training/session/prepare", new
            {
                mode = "stardew_windowed",
                root_path = root,
                game_executable_path = vanilla,
                game_working_directory = runtime,
                allow_game_launch = false,
                sound_enabled = false,
                save_isolation_path = Path.Combine(root, "saves")
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.GetProperty("blocked").GetBoolean());
            Assert.Contains(json.RootElement.GetProperty("block_reasons").EnumerateArray(), item =>
                item.GetString() == "smapi_executable_required_for_transparent_bridge_training");
        }

        [Fact]
        public async Task TrainingSessionLaunchReturnsStructuredFailureWhenProcessCannotStart()
        {
            using var client = factory.CreateClient();
            var root = Path.Combine(Path.GetTempPath(), "stardewai-backend-tests", Guid.NewGuid().ToString("N"));
            var runtime = Path.Combine(root, "Stardew Valley");
            var smapi = Path.Combine(runtime, "StardewModdingAPI.exe");
            Directory.CreateDirectory(runtime);
            File.WriteAllText(smapi, string.Empty);

            var response = await client.PostAsJsonAsync("/api/v1/training/session/launch", new
            {
                mode = "stardew_windowed",
                root_path = root,
                game_executable_path = smapi,
                game_working_directory = runtime,
                allow_game_launch = true,
                sound_enabled = false,
                save_isolation_path = Path.Combine(root, "saves")
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var rootElement = json.RootElement;
            Assert.True(rootElement.GetProperty("blocked").GetBoolean());
            Assert.True(rootElement.GetProperty("launch_attempted").GetBoolean());
            Assert.False(rootElement.GetProperty("started").GetBoolean());
            Assert.Contains(rootElement.GetProperty("block_reasons").EnumerateArray(), item =>
                item.GetString()?.StartsWith("process_start_failed:", StringComparison.Ordinal) == true);
        }

        [Fact]
        public async Task TrainingReadyProbeRequiresTransparentSnapshot()
        {
            await using var isolatedFactory = factory.WithWebHostBuilder(_ => { });
            using var client = isolatedFactory.CreateClient();

            var blockedResponse = await client.GetAsync("/api/v1/training/session/ready-probe");

            Assert.Equal(HttpStatusCode.OK, blockedResponse.StatusCode);
            using var blockedJson = JsonDocument.Parse(await blockedResponse.Content.ReadAsStringAsync());
            Assert.False(blockedJson.RootElement.GetProperty("ready").GetBoolean());
            Assert.False(blockedJson.RootElement.GetProperty("latest_snapshot_available").GetBoolean());
            Assert.Contains(blockedJson.RootElement.GetProperty("block_reasons").EnumerateArray(), item =>
                item.GetString() == "no_transparent_snapshot_ingested");

            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent());
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);

            var readyResponse = await client.GetAsync("/api/v1/training/session/ready-probe");

            Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
            using var readyJson = JsonDocument.Parse(await readyResponse.Content.ReadAsStringAsync());
            Assert.True(readyJson.RootElement.GetProperty("ready").GetBoolean());
            Assert.True(readyJson.RootElement.GetProperty("backend_reachable").GetBoolean());
            Assert.True(readyJson.RootElement.GetProperty("bridge_reachable").GetBoolean());
            Assert.True(readyJson.RootElement.GetProperty("latest_snapshot_available").GetBoolean());
            Assert.NotEqual(string.Empty, readyJson.RootElement.GetProperty("latest_state_hash").GetString());
            Assert.Empty(readyJson.RootElement.GetProperty("block_reasons").EnumerateArray());
        }

        [Fact]
        public async Task TrainingReadyProbeCanBindSnapshotToPreparedManifest()
        {
            await using var isolatedFactory = factory.WithWebHostBuilder(_ => { });
            using var client = isolatedFactory.CreateClient();
            var root = Path.Combine(Path.GetTempPath(), "stardewai-backend-tests", Guid.NewGuid().ToString("N"));
            var runtime = Path.Combine(root, "Stardew Valley");
            var smapi = Path.Combine(runtime, "StardewModdingAPI.exe");
            Directory.CreateDirectory(runtime);
            File.WriteAllText(smapi, string.Empty);

            var prepareResponse = await client.PostAsJsonAsync("/api/v1/training/session/prepare", new
            {
                mode = "stardew_windowed",
                root_path = root,
                game_executable_path = smapi,
                game_working_directory = runtime,
                allow_game_launch = true,
                sound_enabled = false,
                save_isolation_path = Path.Combine(root, "saves")
            });
            Assert.Equal(HttpStatusCode.OK, prepareResponse.StatusCode);
            using var prepareJson = JsonDocument.Parse(await prepareResponse.Content.ReadAsStringAsync());
            var manifest = prepareJson.RootElement.GetProperty("manifest");
            var manifestPath = manifest.GetProperty("manifest_path").GetString();
            var runId = manifest.GetProperty("run_id").GetString();

            var blockedResponse = await client.GetAsync("/api/v1/training/session/ready-probe?manifest_path=" + Uri.EscapeDataString(manifestPath!));
            Assert.Equal(HttpStatusCode.OK, blockedResponse.StatusCode);
            using var blockedJson = JsonDocument.Parse(await blockedResponse.Content.ReadAsStringAsync());
            Assert.False(blockedJson.RootElement.GetProperty("ready").GetBoolean());
            Assert.True(blockedJson.RootElement.GetProperty("manifest_loaded").GetBoolean());
            Assert.Contains(blockedJson.RootElement.GetProperty("block_reasons").EnumerateArray(), item =>
                item.GetString() == "no_transparent_snapshot_ingested");

            var snapshotResponse = await client.PostAsync("/api/v1/snapshots", SampleSnapshotContent(trainingRunId: runId));
            Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);

            var readyResponse = await client.GetAsync("/api/v1/training/session/ready-probe?manifest_path=" + Uri.EscapeDataString(manifestPath!));
            Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);
            using var readyJson = JsonDocument.Parse(await readyResponse.Content.ReadAsStringAsync());
            var readyRoot = readyJson.RootElement;
            Assert.True(readyRoot.GetProperty("ready").GetBoolean());
            Assert.True(readyRoot.GetProperty("manifest_loaded").GetBoolean());
            Assert.Equal(runId, readyRoot.GetProperty("run_id").GetString());
            Assert.Equal(runId, readyRoot.GetProperty("snapshot_run_id").GetString());
            Assert.Empty(readyRoot.GetProperty("block_reasons").EnumerateArray());
        }

        private static StringContent SampleSnapshotContent(
            string? forcedHash = null,
            bool unavailableCarriesDefault = false,
            string? trainingRunId = null,
            bool includeStrategyCatalog = false,
            bool includeMaterialGraph = false)
        {
            var stateJson = $$"""
            {
              "environment": {
                "game_version": {{FieldJson("1.6.15")}},
                "smapi_version": {{FieldJson("4.5.2")}},
                "bridge_version": {{FieldJson("test")}},
                "training_mode": {{(trainingRunId is null ? UnavailableFieldJson("not_a_training_process") : FieldJson("1"))}},
                "training_run_id": {{(trainingRunId is null ? UnavailableFieldJson("not_a_training_process") : FieldJson(trainingRunId))}},
                "save_isolation_path": {{(trainingRunId is null ? UnavailableFieldJson("not_a_training_process") : FieldJson("E:\\StardewAITraining\\saves"))}},
                "installed_mods": {{FieldJson("[]", raw: true)}}
              },
              "identity": {
                "save_id": {{FieldJson("Farm")}},
                "player_id": {{FieldJson("123")}}
              },
              "time": {
                "year": {{FieldJson(3)}},
                "season": {{FieldJson("spring")}},
                "day": {{FieldJson(1)}},
                "total_days": {{FieldJson(224)}},
                "time": {{FieldJson(610)}},
                "weather": {{FieldJson("sun")}}
              },
              "player": {
                "location_id": {{FieldJson("Farm")}},
                "tile_x": {{FieldJson(64)}},
                "tile_y": {{FieldJson(15)}},
                "facing_direction": {{FieldJson(2)}},
                "money": {{FieldJson(500)}},
                "total_money_earned": {{FieldJson(1000000)}},
                "health": {{FieldJson(100)}},
                "max_health": {{FieldJson(100)}},
                "energy": {{FieldJson(270)}},
                "stamina": {{FieldJson(270)}},
                "max_energy": {{FieldJson(270)}},
                "level": {{FieldJson(25)}},
                "has_skull_key": {{FieldJson(true)}},
                "has_rusty_key": {{FieldJson(true)}},
                "married_or_roommate": {{FieldJson(true)}},
                "farmhouse_upgrade_level": {{FieldJson(2)}},
                "current_tool": {{FieldJson("(T)Axe")}},
                "current_item_qualified_id": {{FieldJson("(O)72")}},
                "active_object_qualified_id": {{FieldJson("(O)72")}},
                "active_menu": {{FieldJson("none", status: unavailableCarriesDefault ? "unavailable" : "available")}},
                "inventory": {{FieldJson("[{\"slot_index\":0,\"item_id\":\"Axe\",\"qualified_item_id\":\"(T)Axe\",\"display_name\":\"Axe\",\"stack\":1,\"quality\":0,\"is_empty\":false},{\"slot_index\":1,\"item_id\":\"WateringCan\",\"qualified_item_id\":\"(T)WateringCan\",\"display_name\":\"Watering Can\",\"stack\":1,\"quality\":0,\"is_empty\":false}]", raw: true)}}
              },
              "mods": {
                "installed_count": {{FieldJson(0)}},
                "installed_mods": {{FieldJson("[]", raw: true)}}
              },
              "game": {
                "current_location": {{FieldJson("Farm")}},
                "time_of_day": {{FieldJson(610)}}
              },
              "farm": {
                "grandpa_score": {{FieldJson(3)}},
                "crops": {{FieldJson("[{\"tile_x\":1,\"tile_y\":2,\"needs_watering\":true,\"watered\":false}]", raw: true)}},
                "crop_catalog": {{(includeStrategyCatalog
                    ? FieldJson("[{\"seed_id\":\"745\",\"seasons\":[\"spring\"],\"grow_days\":8,\"regrow_days\":4,\"harvest_item_id\":\"400\",\"harvest_item_qualified_id\":\"(O)400\",\"harvest_min_stack\":1}]", raw: true)
                    : UnavailableFieldJson("crop_catalog_not_in_general_backend_fixture"))}},
                "material_inventory_graph": {{(includeMaterialGraph
                    ? FieldJson("{\"schema_version\":\"material_inventory_graph.v1\",\"status\":\"available\",\"player_id\":123,\"inventory_nodes\":[{\"node_id\":\"player:123\",\"inventory_kind\":\"player_inventory\",\"supply_state\":\"available\",\"owner_player_id\":123,\"ownership_class\":\"actor_owned\",\"actor_use_authorized\":true,\"slots\":[{\"slot_index\":1,\"item_id\":\"388\",\"qualified_item_id\":\"(O)388\",\"stack\":30}]}]}", raw: true)
                    : UnavailableFieldJson("material_graph_not_in_general_backend_fixture"))}}
              },
              "current_location": {
                "identity": {{FieldJson("{\"name\":\"Farm\",\"name_or_unique_name\":\"Farm\",\"type\":\"StardewValley.Farm\"}", raw: true)}},
                "crops": {{FieldJson("[{\"location_id\":\"Farm\",\"tile_x\":1,\"tile_y\":2,\"needs_watering\":true,\"watered\":false}]", raw: true)}},
                "planting_context": {{FieldJson("{\"hoe_dirt_tiles\":[]}", raw: true)}}
              },
              "locations": {
                "collision_grid": {{FieldJson("{\"width\":80,\"height\":65,\"notable_tiles\":[]}", raw: true)}}
              },
              "npcs": {
                "positions": {{FieldJson("[]", raw: true)}},
                "friendships": {{FieldJson("[{\"npc_name\":\"A\",\"points\":2000},{\"npc_name\":\"B\",\"points\":2000},{\"npc_name\":\"C\",\"points\":2000},{\"npc_name\":\"D\",\"points\":2000},{\"npc_name\":\"E\",\"points\":2000},{\"npc_name\":\"F\",\"points\":2000},{\"npc_name\":\"G\",\"points\":2000},{\"npc_name\":\"H\",\"points\":2000},{\"npc_name\":\"I\",\"points\":2000},{\"npc_name\":\"J\",\"points\":2000}]", raw: true)}},
                "schedules": {{UnavailableFieldJson("npc_schedules_unavailable_without_complete_read_only_decompile_proof")}}
              },
              "quests": {
                "active_quests": {{FieldJson("[]", raw: true)}},
                "mail_received": {{FieldJson("[\"petLoveMessage\"]", raw: true)}},
                "completed_quests": {{UnavailableFieldJson("no_verified_global_completed_quest_collection_found")}}
              },
              "world_progress": {
                "community_center": {{FieldJson("{\"location_accessible\":true,\"completed\":true,\"bundles\":{},\"bundle_rewards\":{},\"completed_area_mail_flags\":[]}", raw: true)}},
                "joja_membership": {{FieldJson(false)}},
                "achievements": {{FieldJson("[5,26,34]", raw: true)}},
                "perfection": {{UnavailableFieldJson("perfection_fields_not_verified_in_this_slice")}},
                "golden_walnuts": {{UnavailableFieldJson("golden_walnut_progress_not_verified_in_this_slice")}}
              },
              "menus": {
                "active_menu": {{FieldJson("{\"is_open\":false,\"type\":\"none\",\"full_type\":null}", raw: true)}},
                "menu_specific_state": {{UnavailableFieldJson("no_active_clickable_menu")}}
              },
              "modded_state": {
                "installed_count": {{FieldJson(0)}},
                "installed": {{FieldJson("[]", raw: true)}},
                "content_pack_count": {{FieldJson(0)}},
                "content_packs": {{FieldJson("[]", raw: true)}},
                "private_mod_state": {{UnavailableFieldJson("arbitrary_mod_private_state_unavailable_without_mod_specific_read_only_api")}}
              },
              "transport": {
                "event_stream_websocket": {{FieldJson("{\"endpoint\":\"ws://127.0.0.1:8766/api/v1/events/ws\"}", raw: true)}}
              }
            }
            """;

            var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson)!;
            var hash = forcedHash ?? SnapshotHash.ComputeStateHash(state);
            var snapshotJson = $$"""
            {
              "schema_version": "snapshot.v1",
              "bridge_version": "test",
              "game_version": "1.6.15",
              "smapi_version": "4.5.2",
              "installed_mods": [],
              "save_id": {{FieldJson("Farm")}},
              "player_id": {{FieldJson("123")}},
              "game_tick": 100,
              "in_game_time": {{FieldJson(610)}},
              "real_timestamp": "2026-07-04T00:00:00Z",
              "state_hash": "{{hash}}",
              "completeness": "partial",
              "unavailable_fields": [],
              "state": {{stateJson}}
            }
            """;

            return new StringContent(snapshotJson, Encoding.UTF8, "application/json");
        }

        private static string FieldJson(object? value, string status = "available", bool raw = false)
        {
            var valueJson = value is null
                ? "null"
                : raw
                    ? value.ToString()
                    : JsonSerializer.Serialize(value);
            var reason = status == "available" || status == "derived" ? "" : @",""reason"":""value_unavailable""";
            return $$"""
            {
              "value": {{valueJson}},
              "status": "{{status}}",
              "source": { "kind": "{{(status == "available" ? "game_object" : "unavailable")}}", "path": "test" },
              "adapter": "test",
              "read_at_tick": 100,
              "confidence": {{(status == "available" ? "1.0" : "0.0")}}{{reason}}
            }
            """;
        }

        private static string FieldJson(int value)
        {
            return FieldJson((object)value);
        }

        private static string FieldJson(string? value)
        {
            return FieldJson((object?)value);
        }

        private static string UnavailableFieldJson(string reason)
        {
            return $$"""
            {
              "value": null,
              "status": "unavailable",
              "source": { "kind": "unavailable", "path": "test" },
              "adapter": "test",
              "read_at_tick": 100,
              "confidence": 0.0,
              "reason": "{{reason}}"
            }
            """;
        }

        private static SmallModelActionParameter Parameter(string name, string value) => new()
        {
            Name = name,
            Value = value
        };

        private sealed class InMemoryStrategyCommitmentRepository : IStrategyCommitmentRepository
        {
            private readonly CropCommitmentLedgerService service = new();
            private readonly MaterialReservationLedgerService materialService = new();
            private readonly MachineRelocationIntentLedgerService
                machineRelocationService = new();
            private readonly MachineSupportIntentLedgerService
                machineSupportService = new();
            private StrategyCommitmentLedger? ledger;

            public StrategyCommitmentLedger Get(SnapshotEnvelope snapshot)
            {
                ledger ??= new StrategyCommitmentLedger
                {
                    LedgerId = "test-ledger",
                    SaveId = snapshot.SaveId.Value ?? string.Empty,
                    PlayerId = snapshot.PlayerId.Value ?? string.Empty,
                    SourceStateHash = snapshot.StateHash
                };
                ledger = service.ReconcileCompleted(ledger, snapshot, "2026-07-19T00:00:00Z");
                ledger = machineRelocationService.ReconcileCompleted(
                    ledger,
                    snapshot,
                    "2026-07-19T00:00:00Z");
                ledger = machineSupportService.ReconcileCompleted(
                    ledger,
                    snapshot,
                    "2026-07-19T00:00:00Z");
                return ledger;
            }

            public StrategyCommitmentMutationResult Upsert(SnapshotEnvelope snapshot, CropPlantingCommitmentUpsertRequest request)
            {
                var result = service.Upsert(Get(snapshot), snapshot, request, "2026-07-19T00:00:00Z");
                if (result.Accepted)
                {
                    ledger = result.Ledger;
                }
                return result;
            }

            public StrategyCommitmentMutationResult Cancel(SnapshotEnvelope snapshot, string commitmentId, StrategyCommitmentCancelRequest request)
            {
                var result = service.Cancel(Get(snapshot), snapshot, commitmentId, request, "2026-07-19T00:00:00Z");
                if (result.Accepted)
                {
                    ledger = result.Ledger;
                }
                return result;
            }

            public StrategyCommitmentMutationResult UpsertMaterial(
                SnapshotEnvelope snapshot,
                MaterialReservationUpsertRequest request)
            {
                var result = materialService.Upsert(
                    Get(snapshot),
                    snapshot,
                    request,
                    "2026-07-19T00:00:00Z");
                if (result.Accepted)
                {
                    ledger = result.Ledger;
                }
                return result;
            }

            public StrategyCommitmentMutationResult CancelMaterial(
                SnapshotEnvelope snapshot,
                string reservationId,
                StrategyCommitmentCancelRequest request)
            {
                var result = materialService.Cancel(
                    Get(snapshot),
                    snapshot,
                    reservationId,
                    request,
                    "2026-07-19T00:00:00Z");
                if (result.Accepted)
                {
                    ledger = result.Ledger;
                }
                return result;
            }

            public StrategyCommitmentMutationResult
                UpsertMachineRelocation(
                    SnapshotEnvelope snapshot,
                    MachineRelocationIntentUpsertRequest request)
            {
                var result = machineRelocationService.Upsert(
                    Get(snapshot),
                    snapshot,
                    request,
                    "2026-07-19T00:00:00Z");
                if (result.Accepted)
                {
                    ledger = result.Ledger;
                }
                return result;
            }

            public StrategyCommitmentMutationResult
                UpsertMachineSupport(
                    SnapshotEnvelope snapshot,
                    MachineSupportIntentUpsertRequest request)
            {
                var result = machineSupportService.Upsert(
                    Get(snapshot),
                    snapshot,
                    request,
                    "2026-07-19T00:00:00Z");
                if (result.Accepted)
                {
                    ledger = result.Ledger;
                }
                return result;
            }
        }
    }
}
