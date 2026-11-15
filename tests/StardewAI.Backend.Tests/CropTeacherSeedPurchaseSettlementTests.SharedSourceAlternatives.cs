using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("different_queue")]
    [InlineData("two_selected")]
    [InlineData("different_item")]
    [InlineData("different_shop")]
    [InlineData("duplicate_id")]
    public async Task SharedSelectionAllowsAlternativeCounterEndpointsButOnlyOneBoundCompiledChoice(string fault)
    {
        var f = new SharedYieldFixture();
        using var host = f.Native.Files.F.Runtime;
        var source = f.Native.Files.F.Source;
        var availability = new OptionAvailabilityEnvelope
        {
            StateHash = source.Before.StateHash,
            Options = [new OptionAvailability { OptionId = "economy.buy_supplies", Available = true, ReadEligible = true,
                EventCandidates = [Counter(5), Counter(4)] }]
        };
        var rows = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, "daily.closed_loop");
        Assert.Equal(2, rows.Length);
        SmallModelPlanEnvelope Compile(PolicyEventCandidatePrediction[] candidates) => new DailyPlanCompiler().Compile(
            candidates, source.Before.StateHash, maxCandidates: 1, availableMinutes: 600, energyBudget: 270);
        var original = Compile(rows.Take(1).ToArray());
        var current = Compile(rows);
        Assert.NotEmpty(current.Steps);
        var ranking = new JsonObject { ["ranked_event_candidates"] = JsonSerializer.SerializeToNode(rows.Take(1), JsonOptions) };
        var selected = Assert.Single(SelectedQueueDecisionLease.CreateFromPlan(original, ranking).Candidates);
        Assert.NotNull(selected.ObjectiveContinuation);

        // Selection-reader input only: mirror its existing annotation adapter, not an executable/native queue.
        JsonObject Annotations(SmallModelPlanEnvelope plan) => new()
        {
            ["items"] = new JsonArray(plan.Steps.Select(step => (JsonNode)new JsonObject
            {
                ["normalized_command"] = new JsonObject
                {
                    ["parameters"] = JsonSerializer.SerializeToNode(step.Parameters.Concat(step.Preconditions.Select(value =>
                        new SmallModelActionParameter { Name = "precondition", Value = value })), JsonOptions)
                }
            }).ToArray())
        };
        var queue = Annotations(fault == "different_queue" ? Compile(rows.Skip(1).ToArray()) : current);
        if (fault == "two_selected")
        {
            foreach (var step in Compile(rows.Skip(1).ToArray()).Steps)
            {
                foreach (var parameter in step.Parameters.Where(p => p.Name == "budget.accepted_candidate_index")) parameter.Value = "1";
                current.Steps = current.Steps.Append(step).ToArray();
            }
            queue = Annotations(current);
        }
        availability = JsonSerializer.Deserialize<OptionAvailabilityEnvelope>(JsonSerializer.Serialize(availability, JsonOptions), JsonOptions)!;
        if (fault is "different_item" or "different_shop")
            foreach (var candidate in availability.Options[0].EventCandidates)
            {
                foreach (var parameter in candidate.Parameters.Where(p => p.Name == (fault == "different_item" ? "continuation.qualified_item_id" : "continuation.shop_id")))
                    parameter.Value = "different-original-objective";
                if (fault == "different_shop") candidate.ShopId = "different-original-objective";
            }
        if (fault == "duplicate_id") availability.Options[0].EventCandidates[1].CandidateId = availability.Options[0].EventCandidates[0].CandidateId;
        string Save(string name, object value) { var path = Path.Combine(source.RuntimePath, name); Fixture.Save(path, value); return path; }
        var selection = new CropTeacherSharedSourceSelection(source.SourcePath,
            Save("counter-original-plan.json", original), Save("counter-original-ranking.json", ranking),
            Save("counter-fresh-plan.json", current), Save("counter-selection-annotations.json", queue),
            Save("counter-refresh.json", new JsonObject
            {
                ["schema_version"] = "selected_queue_candidate_refresh.v1", ["state_hash"] = source.Before.StateHash,
                ["goal_id"] = current.GoalId, ["policy_model_invoked"] = false, ["selection_locked"] = true,
                ["selected_queue_index"] = selected.QueueIndex, ["selected_candidate_id"] = selected.CandidateId,
                ["selected_option_id"] = selected.OptionId, ["availability"] = JsonSerializer.SerializeToNode(availability, JsonOptions)
            }), selected.QueueIndex, []);
        await using var owner = await f.Native.Files.F.Files.Open();
        var method = typeof(CropTeacherManagedSeedOwner).GetMethod("ReadSharedSourceSelection", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var record = new JsonObject { ["shared_source_selection"] = JsonSerializer.SerializeToNode(selection, JsonOptions) };
        if (fault == "valid")
            Assert.Equal(selected.CandidateId, Assert.IsType<SelectedQueueCandidateLock>(method.Invoke(owner, [record, null])).CandidateId);
        else
            Assert.True(Assert.Throws<TargetInvocationException>(() => method.Invoke(owner, [record, null])).InnerException is
                InvalidDataException or InvalidOperationException);
        Assert.Null(owner.Pending);
        Assert.Equal(0, f.Native.Files.F.Runtime.Handler.Commits);
        Assert.False(File.Exists(f.Native.ReceiptPath));
    }

    private static EventCandidate Counter(int x) => new()
    {
        CandidateId = "purchase:SeedShop:(O)427:(O)427:interact:SeedShop:" + x + ",18",
        Kind = "interact_endpoint", Available = true, LocationId = "SeedShop", ShopId = "SeedShop", TileX = x, TileY = 18,
        EstimatedTicks = x == 5 ? 60 : 120, ExpectedEffect = "move_to_adjacent=" + x + ",19;preview_interact=Buy",
        Parameters = new Dictionary<string, string>
        {
            ["shop_id"] = "SeedShop", ["target_location"] = "SeedShop", ["item_id"] = "427",
            ["qualified_item_id"] = "(O)427", ["stock_id"] = "(O)427", ["output_stack_per_purchase"] = "1",
            ["output_quality"] = "0", ["max_unit_price"] = "20", ["quantity"] = "1"
        }.Append(new KeyValuePair<string, string>("option_id", "economy.buy_supplies"))
            .Select(pair => new SmallModelActionParameter { Name = "continuation." + pair.Key, Value = pair.Value }).ToArray()
    };
}
