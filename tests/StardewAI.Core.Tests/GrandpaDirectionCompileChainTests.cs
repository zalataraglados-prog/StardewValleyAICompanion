using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Goals;
using StardewAI.Core.MockModel;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;

namespace StardewAI.Core.Tests;

public sealed class GrandpaDirectionCompileChainTests
{
    [Fact]
    public void ClassifierDoesNotOutputAutoSelectBestDirection()
    {
        var classifier = new TaskIntentClassifier();
        var grandpa = classifier.Classify("grandpa_max_score_year3");

        Assert.Equal("strategy.grandpa_progress", grandpa.OptionId);
        Assert.DoesNotContain(grandpa.Parameters, param => param.Name == "direction_id");
        Assert.DoesNotContain(grandpa.Parameters, param => param.Name == "required_minutes");
        Assert.Contains(grandpa.Parameters, param =>
            param.Name == "requires_direction_selection" && param.Value == "true");
        Assert.Contains(grandpa.Parameters, param =>
            param.Name == "classifier_note" && param.Value == "direction_deferred_to_snapshot_aware_policy");
    }

    [Fact]
    public void PolicySelectsKnownUnblockedPositivePotentialDirection()
    {
        var snapshot = GrandpaSnapshot();
        var output = new MockSmallModelPolicy().Generate(snapshot, "grandpa_max_score_year3", "training_singleplayer");

        Assert.Equal("strategy.grandpa_progress", output.Actions[0].OptionId);
        var directionId = output.Actions[0].Parameters.First(param => param.Name == "direction_id").Value;
        Assert.NotEmpty(directionId);
        Assert.NotEqual("auto_select_best_direction", directionId);

        var potential = int.Parse(output.Actions[0].Parameters.First(param => param.Name == "potential_points").Value);
        Assert.True(potential > 0);

        var requiredMinutes = int.Parse(output.Actions[0].Parameters.First(param => param.Name == "required_minutes").Value);
        Assert.True(requiredMinutes > 0);
    }

    [Fact]
    public void PolicyFailsClosedWhenTargetAlreadyComplete()
    {
        var snapshot = TargetCompleteSnapshot();
        var output = new MockSmallModelPolicy().Generate(snapshot, "grandpa_max_score_year3", "training_singleplayer");

        var directionId = output.Actions[0].Parameters.First(param => param.Name == "direction_id").Value;
        Assert.Empty(directionId);

        var blockReason = output.Actions[0].Parameters.First(param => param.Name == "block_reason").Value;
        Assert.Contains("target_already_met", blockReason);

        var targetComplete = output.Actions[0].Parameters.First(param => param.Name == "target_complete").Value;
        Assert.Equal("true", targetComplete);
    }

    [Fact]
    public void CompilerRejectsAutoSelectBestDirection()
    {
        var snapshot = GrandpaSnapshot();
        var request = new SmallModelActionEnvelope
        {
            SchemaVersion = "small_model_action.v1",
            StateHash = snapshot.StateHash,
            GoalId = "test.goal",
            ExecutionMode = "training_singleplayer",
            Actor = Actor(),
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "test.action",
                    OptionId = "strategy.grandpa_progress",
                    Parameters = new[]
                    {
                        Parameter("strategic_goal", "grandpa_max_score_year3"),
                        Parameter("direction_id", "auto_select_best_direction")
                    }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].BlockingReasons, reason =>
            reason.Contains("strategy_auto_select_best_direction_rejected"));
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void CompilerRejectsEmptyDirectionId()
    {
        var snapshot = GrandpaSnapshot();
        var request = new SmallModelActionEnvelope
        {
            SchemaVersion = "small_model_action.v1",
            StateHash = snapshot.StateHash,
            GoalId = "test.goal",
            ExecutionMode = "training_singleplayer",
            Actor = Actor(),
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "test.action",
                    OptionId = "strategy.grandpa_progress",
                    Parameters = new[]
                    {
                        Parameter("strategic_goal", "grandpa_max_score_year3"),
                        Parameter("direction_id", string.Empty),
                        Parameter("requires_direction_selection", "failed_no_eligible_candidate"),
                        Parameter("block_reason", "target_already_met")
                    }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].BlockingReasons, reason =>
            reason.Contains("strategy_direction_failed_closed"));
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void CompilerRejectsStaleDirectionFromOldSnapshot()
    {
        var snapshot = GrandpaSnapshot();
        var staleHash = "stale-hash-that-does-not-match-" + snapshot.StateHash;
        var request = new SmallModelActionEnvelope
        {
            SchemaVersion = "small_model_action.v1",
            StateHash = staleHash,
            GoalId = "test.goal",
            ExecutionMode = "training_singleplayer",
            Actor = Actor(),
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "test.action",
                    OptionId = "strategy.grandpa_progress",
                    Parameters = new[]
                    {
                        Parameter("strategic_goal", "grandpa_max_score_year3"),
                        Parameter("direction_id", "earn_money"),
                        Parameter("direction_domain", "economy"),
                        Parameter("potential_points", "7"),
                        Parameter("priority_score", "7.7"),
                        Parameter("feedback_key", "grandpa.money"),
                        Parameter("required_minutes", "240"),
                        Parameter("optional_minutes", "0"),
                        Parameter("requires_direction_selection", "false")
                    }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.CompilerDiagnostics, diagnostic =>
            diagnostic == "state_hash_mismatch");
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void CompilerRejectsDirectionAbsentFromSnapshotCandidateSet()
    {
        var snapshot = TargetCompleteSnapshot();
        var request = new SmallModelActionEnvelope
        {
            SchemaVersion = "small_model_action.v1",
            StateHash = snapshot.StateHash,
            GoalId = "test.goal",
            ExecutionMode = "training_singleplayer",
            Actor = Actor(),
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "test.action",
                    OptionId = "strategy.grandpa_progress",
                    Parameters = new[]
                    {
                        Parameter("strategic_goal", "grandpa_max_score_year3"),
                        Parameter("direction_id", "earn_money"),
                        Parameter("direction_domain", "economy"),
                        Parameter("potential_points", "7"),
                        Parameter("priority_score", "7.7"),
                        Parameter("feedback_key", "grandpa.money"),
                        Parameter("required_minutes", "240"),
                        Parameter("optional_minutes", "0"),
                        Parameter("requires_direction_selection", "false")
                    }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].BlockingReasons, reason =>
            reason.StartsWith("strategy_direction_absent:"));
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void ValidPolicyOutputCompilesToStepMatchingRecomputedCandidate()
    {
        var snapshot = GrandpaSnapshot();
        var output = new MockSmallModelPolicy().Generate(snapshot, "grandpa_max_score_year3", "training_singleplayer");
        var queue = new ActionQueueCompiler().Compile(output, snapshot);

        Assert.Equal("pending", queue.Status);
        var plan = Assert.Single(queue.Items[0].NormalizedCommand.StrategyPlan);
        Assert.NotEmpty(plan.DirectionId);

        var worldModel = new WorldModelProjector().Project(snapshot, "grandpa_max_score_year3", "training_singleplayer");
        var report = new GrandpaEvaluationGoalEvaluator().Evaluate(worldModel);
        var sample = new GrandpaTrainingSampleAdapter().Build(worldModel, report);
        var candidate = Assert.Single(sample.CandidateDirections
            .Where(c => c.DirectionId == plan.DirectionId
                && c.Known && !c.Blocked && c.PotentialPoints > 0
                && c.PriorityScore > 0));

        Assert.Equal(candidate.DirectionId, plan.DirectionId);
        Assert.Equal(candidate.Domain, plan.Domain);
        Assert.Equal(candidate.PotentialPoints, plan.PotentialPoints);
        Assert.Equal(candidate.PriorityScore, plan.PriorityScore);
        Assert.Equal(candidate.FeedbackKey, plan.FeedbackKey);
        Assert.Equal(GrandpaStrategyFeatureRowBuilder.EstimateRequiredMinutes(candidate), plan.RequiredMinutes);
        Assert.Equal(0, plan.OptionalMinutes);
    }

    [Fact]
    public void CompilerRejectsDomainMismatch()
    {
        var snapshot = GrandpaSnapshot();
        var request = BuildEarnMoneyRequest(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Where(p => p.Name != "direction_domain")
            .Append(Parameter("direction_domain", "exploration"))
            .ToArray();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].BlockingReasons, reason =>
            reason.Contains("strategy_direction_domain_mismatch"));
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void CompilerRejectsPotentialPointsMismatch()
    {
        var snapshot = GrandpaSnapshot();
        var request = BuildEarnMoneyRequest(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Where(p => p.Name != "potential_points")
            .Append(Parameter("potential_points", "999"))
            .ToArray();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].BlockingReasons, reason =>
            reason.Contains("strategy_potential_points_mismatch"));
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void CompilerRejectsPriorityScoreMismatch()
    {
        var snapshot = GrandpaSnapshot();
        var request = BuildEarnMoneyRequest(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Where(p => p.Name != "priority_score")
            .Append(Parameter("priority_score", "99.99"))
            .ToArray();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].BlockingReasons, reason =>
            reason.Contains("strategy_priority_score_mismatch"));
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void CompilerRejectsFeedbackKeyMismatch()
    {
        var snapshot = GrandpaSnapshot();
        var request = BuildEarnMoneyRequest(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Where(p => p.Name != "feedback_key")
            .Append(Parameter("feedback_key", "grandpa.tampered"))
            .ToArray();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].BlockingReasons, reason =>
            reason.Contains("strategy_feedback_key_mismatch"));
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void CompilerRejectsRequiredMinutesMismatch()
    {
        var snapshot = GrandpaSnapshot();
        var request = BuildEarnMoneyRequest(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Where(p => p.Name != "required_minutes")
            .Append(Parameter("required_minutes", "999"))
            .ToArray();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].BlockingReasons, reason =>
            reason.Contains("strategy_required_minutes_mismatch"));
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void CompilerRejectsOptionalMinutesNonzero()
    {
        var snapshot = GrandpaSnapshot();
        var request = BuildEarnMoneyRequest(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Where(p => p.Name != "optional_minutes")
            .Append(Parameter("optional_minutes", "30"))
            .ToArray();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].BlockingReasons, reason =>
            reason.Contains("strategy_optional_minutes_must_be_zero"));
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void CompilerRejectsMissingStrategicGoal()
    {
        var snapshot = GrandpaSnapshot();
        var request = new SmallModelActionEnvelope
        {
            SchemaVersion = "small_model_action.v1",
            StateHash = snapshot.StateHash,
            GoalId = "test.goal",
            ExecutionMode = "training_singleplayer",
            Actor = Actor(),
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "test.action",
                    OptionId = "strategy.grandpa_progress",
                    Parameters = new[]
                    {
                        Parameter("direction_id", "earn_money"),
                        Parameter("direction_domain", "economy"),
                        Parameter("potential_points", "7"),
                        Parameter("priority_score", "7.7"),
                        Parameter("feedback_key", "grandpa.money"),
                        Parameter("required_minutes", "240"),
                        Parameter("optional_minutes", "0"),
                        Parameter("requires_direction_selection", "false")
                    }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].BlockingReasons, reason =>
            reason.StartsWith("strategy_strategic_goal_missing:"));
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void CompilerRejectsWrongStrategicGoal()
    {
        var snapshot = GrandpaSnapshot();
        var request = new SmallModelActionEnvelope
        {
            SchemaVersion = "small_model_action.v1",
            StateHash = snapshot.StateHash,
            GoalId = "test.goal",
            ExecutionMode = "training_singleplayer",
            Actor = Actor(),
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "test.action",
                    OptionId = "strategy.grandpa_progress",
                    Parameters = new[]
                    {
                        Parameter("strategic_goal", "some_other_goal"),
                        Parameter("direction_id", "earn_money"),
                        Parameter("direction_domain", "economy"),
                        Parameter("potential_points", "7"),
                        Parameter("priority_score", "7.7"),
                        Parameter("feedback_key", "grandpa.money"),
                        Parameter("required_minutes", "240"),
                        Parameter("optional_minutes", "0"),
                        Parameter("requires_direction_selection", "false")
                    }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].BlockingReasons, reason =>
            reason.StartsWith("strategy_strategic_goal_invalid:"));
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void CompilerRejectsMissingOptionalMinutes()
    {
        var snapshot = GrandpaSnapshot();
        var request = new SmallModelActionEnvelope
        {
            SchemaVersion = "small_model_action.v1",
            StateHash = snapshot.StateHash,
            GoalId = "test.goal",
            ExecutionMode = "training_singleplayer",
            Actor = Actor(),
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "test.action",
                    OptionId = "strategy.grandpa_progress",
                    Parameters = new[]
                    {
                        Parameter("strategic_goal", "grandpa_max_score_year3"),
                        Parameter("direction_id", "earn_money"),
                        Parameter("direction_domain", "economy"),
                        Parameter("potential_points", "7"),
                        Parameter("priority_score", "7.7"),
                        Parameter("feedback_key", "grandpa.money"),
                        Parameter("required_minutes", "240"),
                        Parameter("requires_direction_selection", "false")
                    }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].BlockingReasons, reason =>
            reason.StartsWith("strategy_optional_minutes_missing:"));
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void CompilerRejectsHardPreconditionsValue()
    {
        var snapshot = GrandpaSnapshot();
        var request = BuildEarnMoneyRequest(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Append(Parameter("hard_preconditions", "daytime_social_window=True"))
            .ToArray();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].BlockingReasons, reason =>
            reason.Contains("strategy_hard_preconditions_not_verifiable"));
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void CompilerRejectsResourceBudgetValue()
    {
        var snapshot = GrandpaSnapshot();
        var request = BuildEarnMoneyRequest(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Append(Parameter("resource_budget", "gift_items"))
            .ToArray();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].BlockingReasons, reason =>
            reason.Contains("strategy_resource_budget_not_verifiable"));
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void CompilerRejectsExecutorHandoffValue()
    {
        var snapshot = GrandpaSnapshot();
        var request = BuildEarnMoneyRequest(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Append(Parameter("executor_handoff_option", "social.talk_npc"))
            .ToArray();

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Contains(queue.Items[0].BlockingReasons, reason =>
            reason.Contains("strategy_executor_handoff_not_verifiable"));
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void All11DirectionIdsAreCoveredByAdapter()
    {
        var expectedDirectionIds = new[]
        {
            "complete_community_center",
            "raise_friendships",
            "complete_full_shipment",
            "raise_skill_levels",
            "marriage_and_house_upgrade",
            "complete_master_angler",
            "complete_museum_collection",
            "obtain_rusty_key",
            "obtain_skull_key",
            "earn_money",
            "earn_pet_love"
        };

        Assert.Equal(11, expectedDirectionIds.Length);

        var snapshot = GrandpaSnapshot();
        var worldModel = new WorldModelProjector().Project(snapshot, "grandpa_max_score_year3", "training_singleplayer");
        var report = new GrandpaEvaluationGoalEvaluator().Evaluate(worldModel);
        var sample = new GrandpaTrainingSampleAdapter().Build(worldModel, report);

        foreach (var directionId in expectedDirectionIds)
        {
            var direction = sample.CandidateDirections
                .FirstOrDefault(c => c.DirectionId == directionId);
            Assert.NotNull(direction);
            Assert.Equal(directionId, direction.DirectionId);
        }
    }

    [Fact]
    public void DirectionWithUnknownFactorIsNotSelected()
    {
        var classifier = new TaskIntentClassifier();
        var grandpa = classifier.Classify("grandpa_max_score_year3");

        Assert.Equal("strategy.grandpa_progress", grandpa.OptionId);
        Assert.Contains(grandpa.Parameters, param =>
            param.Name == "requires_direction_selection" && param.Value == "true");
    }

    [Fact]
    public void BlockedItemHasEmptyStrategyPlan()
    {
        var snapshot = TargetCompleteSnapshot();
        var request = new SmallModelActionEnvelope
        {
            SchemaVersion = "small_model_action.v1",
            StateHash = snapshot.StateHash,
            GoalId = "test.goal",
            ExecutionMode = "training_singleplayer",
            Actor = Actor(),
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "test.action",
                    OptionId = "strategy.grandpa_progress",
                    Parameters = new[]
                    {
                        Parameter("strategic_goal", "grandpa_max_score_year3"),
                        Parameter("direction_id", string.Empty),
                        Parameter("requires_direction_selection", "failed_no_eligible_candidate"),
                        Parameter("block_reason", "target_already_met")
                    }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Items[0].Status);
        Assert.Empty(queue.Items[0].NormalizedCommand.StrategyPlan);
    }

    [Fact]
    public void AllDirectionsPresentInValidSnapshotAreCoveredByAdapter()
    {
        var snapshot = GrandpaSnapshot();
        var worldModel = new WorldModelProjector().Project(snapshot, "grandpa_max_score_year3", "training_singleplayer");
        var report = new GrandpaEvaluationGoalEvaluator().Evaluate(worldModel);
        var sample = new GrandpaTrainingSampleAdapter().Build(worldModel, report);

        Assert.False(sample.Target.Complete);
        Assert.Equal(11, sample.CandidateDirections.Length);

        var candidateDirectionIds = sample.CandidateDirections
            .Select(c => c.DirectionId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();

        var expectedDirectionIds = new[]
        {
            "complete_community_center",
            "complete_full_shipment",
            "complete_master_angler",
            "complete_museum_collection",
            "earn_money",
            "earn_pet_love",
            "marriage_and_house_upgrade",
            "obtain_rusty_key",
            "obtain_skull_key",
            "raise_friendships",
            "raise_skill_levels"
        };

        Assert.Equal(expectedDirectionIds, candidateDirectionIds);
    }

    [Fact]
    public void NonStrategyOptionDoesNotRebuildCandidateSet()
    {
        var snapshot = GrandpaSnapshot();
        var request = new SmallModelActionEnvelope
        {
            SchemaVersion = "small_model_action.v1",
            StateHash = snapshot.StateHash,
            GoalId = "test.goal",
            ExecutionMode = "training_singleplayer",
            Actor = Actor(),
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "test.action",
                    OptionId = "recovery.stabilize_day",
                    Parameters = new[]
                    {
                        Parameter("strategic_goal", "grandpa_max_score_year3")
                    }
                }
            }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.DoesNotContain(queue.Items[0].BlockingReasons, reason =>
            reason.Contains("strategy_direction_absent"));
    }

    private static SmallModelActionEnvelope BuildEarnMoneyRequest(SnapshotEnvelope snapshot)
    {
        var worldModel = new WorldModelProjector().Project(snapshot, "grandpa_max_score_year3", "training_singleplayer");
        var report = new GrandpaEvaluationGoalEvaluator().Evaluate(worldModel);
        var sample = new GrandpaTrainingSampleAdapter().Build(worldModel, report);
        var candidate = sample.CandidateDirections.First(c => c.DirectionId == "earn_money");
        var requiredMinutes = GrandpaStrategyFeatureRowBuilder.EstimateRequiredMinutes(candidate);

        return new SmallModelActionEnvelope
        {
            SchemaVersion = "small_model_action.v1",
            StateHash = snapshot.StateHash,
            GoalId = "test.goal",
            ExecutionMode = "training_singleplayer",
            Actor = Actor(),
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "test.action",
                    OptionId = "strategy.grandpa_progress",
                    Parameters = new[]
                    {
                        Parameter("strategic_goal", "grandpa_max_score_year3"),
                        Parameter("direction_id", candidate.DirectionId),
                        Parameter("direction_domain", candidate.Domain),
                        Parameter("potential_points", candidate.PotentialPoints.ToString()),
                        Parameter("priority_score", candidate.PriorityScore.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        Parameter("feedback_key", candidate.FeedbackKey),
                        Parameter("required_minutes", requiredMinutes.ToString()),
                        Parameter("optional_minutes", "0"),
                        Parameter("requires_direction_selection", "false")
                    }
                }
            }
        };
    }

    private static SnapshotEnvelope GrandpaSnapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {
          "identity": {
            "save_id": {"value":"test-save","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "player_id": {"value":"test-player","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "year": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "day": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sunny","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "total_money_earned": {"value":10000,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "level": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_skull_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_rusty_key": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "married_or_roommate": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "farmhouse_upgrade_level": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "grandpa_score": {"value":0,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "achievements": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "community_center": {"value":{"location_accessible":false,"completed":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "joja_membership": {"value":false,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": {
            "friendships": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "quests": {
            "mail_received": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":null,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "transport": {
            "event_stream_websocket": {"value":"ws://localhost/test","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """, JsonOptions)!;
        return SnapshotFromState(state);
    }

    private static SnapshotEnvelope TargetCompleteSnapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
        {
          "identity": {
            "save_id": {"value":"test-save","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "player_id": {"value":"test-player","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "year": {"value":3,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "day": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "time": {"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "weather": {"value":"sunny","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "total_money_earned": {"value":1200000,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "level": {"value":25,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_skull_key": {"value":true,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "has_rusty_key": {"value":true,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "married_or_roommate": {"value":true,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "farmhouse_upgrade_level": {"value":2,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crops": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "grandpa_score": {"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "achievements": {"value":[5,26,34],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "community_center": {"value":{"location_accessible":true,"completed":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "joja_membership": {"value":true,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": {
            "friendships": {"value":[{"npc":"A","points":2500},{"npc":"B","points":2500},{"npc":"C","points":2500},{"npc":"D","points":2500},{"npc":"E","points":2500},{"npc":"F","points":2500},{"npc":"G","points":2500},{"npc":"H","points":2500},{"npc":"I","points":2500},{"npc":"J","points":2500}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "quests": {
            "mail_received": {"value":["petLoveMessage"],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":null,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "transport": {
            "event_stream_websocket": {"value":"ws://localhost/test","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """, JsonOptions)!;
        return SnapshotFromState(state);
    }

    private static SnapshotEnvelope SnapshotFromState(Dictionary<string, JsonElement> state)
    {
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-14T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static ActionActorRef Actor()
    {
        return new ActionActorRef
        {
            ActorId = "training_farmer.main",
            ActorType = "training_farmer",
            ControlSurface = "training_sandbox"
        };
    }

    private static SmallModelActionParameter Parameter(string name, string value)
    {
        return new SmallModelActionParameter
        {
            Name = name,
            Value = value
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
