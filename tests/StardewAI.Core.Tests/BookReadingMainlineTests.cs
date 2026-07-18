using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class BookReadingMainlineTests
{
    [Fact]
    public void SkillBookFlowsThroughNativeReadBookQueue()
    {
        var snapshot = Snapshot(StateJson(250));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "skills.read_books" },
            true);
        var candidate = Assert.Single(availability.Options.Single().EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("read_inventory_book", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "expected_skill_experience_deltas_json" && parameter.Value.Contains("\"SkillId\":\"mining\"", StringComparison.Ordinal));

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var read = Assert.Single(queue.Items.Where(item => item.OptionId == "executor.read_book"));

        Assert.Empty(read.BlockingReasons);
        Assert.Contains(read.NormalizedCommand.Parameters, parameter => parameter.Name == "book_native_branch" && parameter.Value == "skill_book");
        Assert.Equal("read_book", Assert.Single(read.NormalizedCommand.Steps).StepType);
        var settle = Assert.Single(queue.Items.Where(item => item.OptionId == "executor.wait_ticks"));
        Assert.Empty(settle.BlockingReasons);
        Assert.Contains(settle.NormalizedCommand.Parameters, parameter => parameter.Name == "wait_ticks" && parameter.Value == "75");
    }

    [Fact]
    public void CompilerBlocksBookExperienceDrift()
    {
        var initial = Snapshot(StateJson(250));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            initial,
            new[] { "skills.read_books" },
            true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            initial.StateHash);
        var drifted = Snapshot(StateJson(251));
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);
        var read = Assert.Single(queue.Items.Where(item => item.OptionId == "executor.read_book"));
        Assert.Contains(read.BlockingReasons, reason => reason.StartsWith("read_book_projection_drifted:", StringComparison.Ordinal));
    }

    private static string StateJson(int miningDelta)
    {
        var deltas = JsonSerializer.Serialize(new[]
        {
            new { SkillId = "mining", SkillIndex = 3, Delta = miningDelta }
        });
        var escapedDeltas = JsonSerializer.Serialize(deltas);
        var levelDeltas = JsonSerializer.Serialize(new[]
        {
            new { SkillId = "mining", SkillIndex = 3, LevelBefore = 0, LevelAfter = 1, NewLevelsQueued = new[] { 1 } }
        });
        var escapedLevelDeltas = JsonSerializer.Serialize(levelDeltas);
        var newLevelsBefore = JsonSerializer.Serialize(Array.Empty<object>());
        var escapedNewLevelsBefore = JsonSerializer.Serialize(newLevelsBefore);
        var newLevelsAfter = JsonSerializer.Serialize(new[] { new { SkillIndex = 3, Level = 1 } });
        var escapedNewLevelsAfter = JsonSerializer.Serialize(newLevelsAfter);
        var tags = JsonSerializer.Serialize(new[] { "book_item" });
        var escapedTags = JsonSerializer.Serialize(tags);
        var recipes = JsonSerializer.Serialize(Array.Empty<string>());
        var escapedRecipes = JsonSerializer.Serialize(recipes);
        return $$"""
        {
          "player": {
            "location_id":{"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[{"slot_index":0,"item_id":"SkillBook_3","qualified_item_id":"(O)SkillBook_3","stack":1,"category":-103}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "skills_detail":{"value":{"scoring_level":0,"skills":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "book_candidates":{"value":[{
              "slot_index":0,"item_id":"SkillBook_3","qualified_item_id":"(O)SkillBook_3","display_name":"Mining Monthly","runtime_type":"StardewValley.Object","category":-103,
              "stack_before":1,"stack_after":0,"temporarily_invisible":false,
              "context_tags_native_order":{{tags}},"context_tags_native_order_json":{{escapedTags}},"matched_book_experience_tag":"",
              "already_read_stat_key":"SkillBook_3","already_read_stat_before":0,
              "native_branch":"skill_book","native_branch_status":"exact","experience_calls":[{"SkillId":"mining","SkillIndex":3,"Amount":250}],
              "experience_deltas":{{deltas}},"experience_deltas_json":{{escapedDeltas}},"mastery_experience_delta":0,"experience_projection_status":"exact_native_gain_experience_order",
              "skill_level_deltas":{{levelDeltas}},"skill_level_deltas_json":{{escapedLevelDeltas}},
              "new_levels_before":{{newLevelsBefore}},"new_levels_before_json":{{escapedNewLevelsBefore}},
              "new_levels_after":{{newLevelsAfter}},"new_levels_after_json":{{escapedNewLevelsAfter}},
              "native_feedback_callbacks":"native_book_animation_1000ms;music_duck_4000ms;book_read_sound;skill_book_message_suppressed_for_new_level_menu",
              "book_stat_key":"","book_stat_before":null,"book_stat_after":null,
              "read_a_book_mail_before":false,"read_a_book_mail_after":false,
              "well_read_achievement_before":false,"well_read_achievement_after":false,"well_read_achievement_will_unlock":false,
              "well_read_achievement_definition_loaded":true,"well_read_achievement_game_mode_allows_unlock":true,
              "well_read_hatter_mail_before":false,"well_read_hatter_mail_after":false,
              "well_read_dialogue_event_seen_before":false,"well_read_dialogue_event_seen_after":false,"well_read_ui_sound_platform_callbacks":"not_triggered",
              "cooking_recipes_added":{{recipes}},"cooking_recipes_added_json":{{escapedRecipes}},"cooking_recipes_added_count":0,
              "player_can_move":true,"event_up":false,"festival_active":false,"fade_to_black":false,"swimming":false,"bathing_clothes":false,"on_bridge":false,"active_menu_clear":true,
              "available":true,"block_reasons":[]
            }],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
        }
        """;
    }

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-18T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
