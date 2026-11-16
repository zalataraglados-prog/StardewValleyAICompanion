using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("wrong_npc")]
    [InlineData("wrong_queue_ticks")]
    [InlineData("wrong_model_ticks")]
    [InlineData("wrong_kind")]
    [InlineData("foreign_primitive")]
    [InlineData("extra_step")]
    [InlineData("conflicting_candidate")]
    [InlineData("wrong_retry")]
    [InlineData("unavailable")]
    [InlineData("wrong_queue_continuation")]
    [InlineData("extra_queue_parameter")]
    [InlineData("missing_queue_step")]
    [InlineData("wrong_source_action")]
    [InlineData("legacy_mode")]
    [InlineData("omitted_optional_plans")]
    [InlineData("non_null_social_plan")]
    [InlineData("non_null_quest_plan")]
    [InlineData("unknown_command_field")]
    public async Task SharedSelectionReusesOriginalBoundedSocialRetryWaitWithoutChangingItsArtifacts(string fault)
    {
        var f = new SharedYieldFixture();
        using var host = f.Native.Files.F.Runtime;
        var source = f.Native.Files.F.Source;
        SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };
        var parameters = new[]
        {
            P("npc_name", "Abigail"), P("stand_tile_x", "1"), P("stand_tile_y", "1"),
            P("npc_tile_x", "1"), P("npc_tile_y", "0"), P("continuation.option_id", "social.talk_npc"),
            P("continuation.npc_name", "Abigail"), P("continuation.target_location", "Farm")
        };
        var originalRow = new PolicyEventCandidatePrediction
        {
            CandidateId = "social:talk:Abigail", Kind = "social_talk_current", OptionId = "social.talk_npc",
            Available = true, LocationId = "Farm", Parameters = parameters, EstimatedTicks = 60
        };
        var original = new DailyPlanCompiler().Compile([originalRow], source.Before.StateHash, maxCandidates: 1);
        var ranking = new JsonObject { ["ranked_event_candidates"] = JsonSerializer.SerializeToNode(new[] { originalRow }, JsonOptions) };
        var selected = Assert.Single(SelectedQueueDecisionLease.CreateFromPlan(original, ranking).Candidates);
        var retry = new EventCandidate
        {
            CandidateId = originalRow.CandidateId + ":continuation-retry-wait", Kind = "social_continuation_retry_wait",
            Available = true, LocationId = "Farm", EstimatedTicks = 600,
            Parameters = parameters.Concat([P("retry_wait_ticks", "600"), P("continuation.retry_count", "1"),
                P("continuation.retry_game_time", "600")]).ToArray()
        };
        var availability = new OptionAvailabilityEnvelope
        {
            StateHash = source.Before.StateHash,
            Options = [new OptionAvailability { OptionId = "social.talk_npc", Available = true, ReadEligible = true, SocialCandidates = [retry] }]
        };
        var rows = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, original.GoalId);
        var current = new DailyPlanCompiler().Compile(rows, source.Before.StateHash, maxCandidates: 1, availableMinutes: 600, energyBudget: 270);
        Assert.Equal("wait_ticks", Assert.Single(current.Steps).Kind);
        Assert.Throws<InvalidDataException>(() => SelectedQueueDecisionLease.CreateFromPlan(current,
            new JsonObject { ["ranked_event_candidates"] = JsonSerializer.SerializeToNode(rows, JsonOptions) }));
        var queue = new ActionQueueCompiler().Compile(current, source.Before, host.Repository.Get(source.Before));
        Assert.True(queue.Status == "pending", JsonSerializer.Serialize(queue, JsonOptions));
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.wait_ticks", item.OptionId);
        availability = JsonSerializer.Deserialize<OptionAvailabilityEnvelope>(JsonSerializer.Serialize(availability, JsonOptions), JsonOptions)!;
        retry = availability.Options[0].SocialCandidates[0];
        if (fault == "wrong_npc") retry.Parameters.Single(p => p.Name == "continuation.npc_name").Value = "Leah";
        if (fault == "wrong_queue_ticks") item.NormalizedCommand.Parameters.Single(p => p.Name == "wait_ticks").Value = "599";
        if (fault == "wrong_model_ticks") current.Steps[0].WaitTicks = 599;
        if (fault == "wrong_kind") current.CandidateAudit[0].Kind = "ordinary_wait";
        if (fault == "foreign_primitive") item.OptionId = "executor.move_to_tile";
        if (fault == "extra_step") current.Steps = current.Steps.Concat(current.Steps).ToArray();
        if (fault == "conflicting_candidate") current.Steps[0].Preconditions = current.Steps[0].Preconditions.Append("candidate_id:foreign").ToArray();
        if (fault == "wrong_retry") current.Steps[0].Parameters.Single(p => p.Name == "continuation.retry_count").Value = "2";
        if (fault == "unavailable") retry.Available = false;
        if (fault == "wrong_queue_continuation") item.NormalizedCommand.Parameters.Single(p => p.Name == "continuation.retry_count").Value = "2";
        if (fault == "extra_queue_parameter") item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters.Append(P("max_slices", "20")).ToArray();
        if (fault == "missing_queue_step") item.NormalizedCommand.Steps = [];
        if (fault == "wrong_source_action") item.SourceActionId = "another-action";
        var queueNode = JsonSerializer.SerializeToNode(queue, JsonOptions)!.AsObject();
        var command = queueNode["items"]![0]!["normalized_command"]!.AsObject();
        if (fault == "omitted_optional_plans") { command.Remove("social_plan"); command.Remove("quest_plan"); }
        if (fault == "non_null_social_plan") command["social_plan"] = new JsonObject();
        if (fault == "non_null_quest_plan") command["quest_plan"] = new JsonObject();
        if (fault == "unknown_command_field") command["unknown_command_field"] = null;
        string Save(string name, object value) { var path = Path.Combine(source.RuntimePath, name); Fixture.Save(path, value); return path; }
        var selection = new CropTeacherSharedSourceSelection(source.SourcePath,
            Save("retry-original-plan.json", original), Save("retry-original-ranking.json", ranking),
            Save("retry-fresh-plan.json", current), Save("retry-compiled-queue.json", queueNode),
            Save("retry-refresh.json", new JsonObject
            {
                ["schema_version"] = "selected_queue_candidate_refresh.v1", ["state_hash"] = source.Before.StateHash,
                ["goal_id"] = current.GoalId, ["policy_model_invoked"] = false, ["selection_locked"] = true,
                ["selected_queue_index"] = selected.QueueIndex, ["selected_candidate_id"] = selected.CandidateId,
                ["selected_option_id"] = selected.OptionId, ["availability"] = JsonSerializer.SerializeToNode(availability, JsonOptions)
            }), selected.QueueIndex, []);
        var before = new[] { selection.ModelPlanPath, selection.QueuePath }.Select(File.ReadAllBytes).ToArray();
        await using var owner = await f.Native.Files.F.Files.Open();
        var method = typeof(CropTeacherManagedSeedOwner).GetMethod("ReadSharedSourceSelection", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var record = new JsonObject { ["shared_source_selection"] = JsonSerializer.SerializeToNode(selection, JsonOptions),
            ["shared_source_execution_protocol"] = fault == "legacy_mode" ? "selected_work_yield.v1" : "selected_native_work_yield.v1" };
        if (fault is "valid" or "omitted_optional_plans")
        {
            var bound = Assert.IsType<SelectedQueueCandidateLock>(method.Invoke(owner, [record, null]));
            Assert.Equal(retry.CandidateId, bound.CandidateId);
            Assert.Equal("social", bound.ObjectiveContinuation!["kind"]!.GetValue<string>());
            Assert.Equal("1", bound.ObjectiveContinuation["retry_count"]!.GetValue<string>());
        }
        else Assert.True(Assert.Throws<TargetInvocationException>(() => method.Invoke(owner, [record, null])).InnerException is
            InvalidDataException or InvalidOperationException);
        Assert.Equal(before[0], File.ReadAllBytes(selection.ModelPlanPath));
        Assert.Equal(before[1], File.ReadAllBytes(selection.QueuePath));
        Assert.Null(owner.Pending);
        Assert.Equal(0, host.Handler.Commits);
    }
}
