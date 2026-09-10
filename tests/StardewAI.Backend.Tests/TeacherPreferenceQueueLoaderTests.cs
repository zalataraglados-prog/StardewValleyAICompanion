using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class TeacherPreferenceQueueLoaderTests
{
    [Fact]
    public async Task LoadsExactQueueFromReadyTeacherPreference()
    {
        var path = TemporaryPreferencePath(Preference());
        try
        {
            var queue = await TeacherPreferenceQueueLoader.LoadAsync(
                path,
                "hash.current",
                "training_singleplayer");

            Assert.Equal("queue.teacher.1", queue["queue_id"]!.GetValue<string>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AllowsHighLevelCandidateToCompileToItsExecutorPrimitive()
    {
        var preference = Preference();
        preference["selected_candidate"]!["option_id"] =
            "farm.collect_machine_outputs";
        var path = TemporaryPreferencePath(preference);
        try
        {
            var queue = await TeacherPreferenceQueueLoader.LoadAsync(
                path,
                "hash.current",
                "training_singleplayer");

            Assert.Equal(
                "executor.interact",
                queue["items"]![0]!["option_id"]!.GetValue<string>());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RejectsQueueThatDoesNotComeFromSelectedCompiledStep()
    {
        var preference = Preference();
        preference["compiled_queue"]!["items"]![0]!["source_action_id"] =
            "action.unrelated";
        var path = TemporaryPreferencePath(preference);
        try
        {
            var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
                TeacherPreferenceQueueLoader.LoadAsync(
                    path,
                    "hash.current",
                    "training_singleplayer"));

            Assert.Contains(
                "teacher_preference_queue_source_step_mismatch",
                error.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void RejectsStateDriftAndMoreThanOnePrimitive()
    {
        var queue = Queue();
        queue["state_hash"] = "hash.stale";
        queue["items"]![0]!["normalized_command"]!["steps"]!.AsArray().Add(
            Step("interact"));

        var error = Assert.Throws<InvalidDataException>(() =>
            TeacherPreferenceQueueLoader.Validate(
                queue,
                "hash.current",
                "training_singleplayer"));

        Assert.Contains("precompiled_queue_state_hash_mismatch", error.Message);
        Assert.Contains(
            "precompiled_queue_requires_exactly_one_primitive_step",
            error.Message);
    }

    [Fact]
    public void RejectsActorMismatchAndBlockedItem()
    {
        var queue = Queue();
        queue["actor"]!["actor_id"] = "ai_host.main";
        queue["items"]![0]!["blocking_reasons"]!.AsArray().Add("blocked");

        var error = Assert.Throws<InvalidDataException>(() =>
            TeacherPreferenceQueueLoader.Validate(
                queue,
                "hash.current",
                "training_singleplayer"));

        Assert.Contains("precompiled_queue_actor_mismatch", error.Message);
        Assert.Contains("precompiled_queue_item_blocked", error.Message);
    }

    [Fact]
    public void OptionsRequireBoundedProductEvidenceMode()
    {
        var valid = LiveTrainingOptions.Parse(new[]
        {
            "--teacher-preference", "teacher-preference.json",
            "--skip-training",
            "--use-product-executor",
            "--max-attempts", "1",
            "--required-verified-actions", "1"
        });
        Assert.True(valid.UseTeacherPreferenceQueue);

        var error = Assert.Throws<ArgumentException>(() =>
            LiveTrainingOptions.Parse(new[]
            {
                "--teacher-preference", "teacher-preference.json",
                "--skip-training",
                "--use-product-executor",
                "--use-daily-plan",
                "--max-attempts", "1",
                "--required-verified-actions", "1"
            }));
        Assert.Contains("cannot be combined", error.Message);
    }

    private static string TemporaryPreferencePath(JsonObject value)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "stardewai-teacher-preference-" + Guid.NewGuid().ToString("N") +
            ".json");
        File.WriteAllText(path, value.ToJsonString());
        return path;
    }

    private static JsonObject Preference() => new()
    {
        ["schema_version"] =
            "current_stage_one_collection_teacher_preference.v1",
        ["status"] = "ready",
        ["source_state_hash"] = "hash.current",
        ["teacher_preference_label_eligible"] = true,
        ["formal_training_authorized"] = false,
        ["uses_learner_rank_or_score"] = false,
        ["preference_provenance_class"] =
            "independent_deterministic_teacher_preference",
        ["selected_candidate"] = new JsonObject
        {
            ["candidate_id"] = "candidate.teacher.1",
            ["option_id"] = "farm.collect_machine_outputs"
        },
        ["compiled_plan"] = Plan(),
        ["compiled_queue"] = Queue()
    };

    private static JsonObject Plan() => new()
    {
        ["schema_version"] = "small_model_plan.v1",
        ["plan_id"] = "plan.teacher.1",
        ["state_hash"] = "hash.current",
        ["goal_id"] = "goal.stage_one_collection",
        ["candidate_audit"] = new JsonArray
        {
            new JsonObject
            {
                ["candidate_id"] = "candidate.teacher.1",
                ["decision"] = "accepted"
            }
        },
        ["steps"] = new JsonArray
        {
            new JsonObject
            {
                ["step_id"] = "action.teacher.1",
                ["kind"] = "interact"
            }
        }
    };

    private static JsonObject Queue() => new()
    {
        ["schema_version"] = "action_queue.v1",
        ["queue_id"] = "queue.teacher.1",
        ["source_model_output_id"] = "plan.teacher.1",
        ["source_model"] = "StardewAI.Core.Training.DailyPlanCompiler",
        ["state_hash"] = "hash.current",
        ["goal_id"] = "goal.stage_one_collection",
        ["execution_mode"] = "training_singleplayer",
        ["actor"] = Actor(),
        ["status"] = "pending",
        ["items"] = new JsonArray
        {
            new JsonObject
            {
                ["queue_item_id"] = "queue.teacher.1.item.1",
                ["source_action_id"] = "action.teacher.1",
                ["option_id"] = "executor.interact",
                ["status"] = "pending",
                ["missing_state_factors"] = new JsonArray(),
                ["blocking_reasons"] = new JsonArray(),
                ["normalized_command"] = new JsonObject
                {
                    ["option_id"] = "executor.interact",
                    ["state_hash"] = "hash.current",
                    ["execution_mode"] = "training_singleplayer",
                    ["actor"] = Actor(),
                    ["parameters"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "plan_step_kind",
                            ["value"] = "interact"
                        }
                    },
                    ["steps"] = new JsonArray { Step("interact") }
                }
            }
        }
    };

    private static JsonObject Actor() => new()
    {
        ["actor_id"] = "training_farmer.main",
        ["actor_type"] = "training_farmer",
        ["control_surface"] = "training_sandbox"
    };

    private static JsonObject Step(string type) => new()
    {
        ["step_id"] = "step.1",
        ["step_type"] = type,
        ["target"] = "Farm(1,1)",
        ["expected_effect"] = "state_changes",
        ["estimated_ticks"] = 1
    };
}
