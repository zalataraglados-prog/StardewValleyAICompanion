using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    private static CropTeacherSharedSourceSelection CompileOwnedCookingSelection(SharedCookingFixture f,
        StrategyCommitmentLedger ledger, string output, CropTeacherSharedPreparationDecision? decision = null)
    {
        var availability = f.CookingAvailability();
        var rows = new EventCandidateRanker().Rank(new(), availability, "earn_money");
        Assert.Single(rows);
        if (ledger.PreparedInventoryConsumption is not null)
            rows[0].Parameters = rows[0].Parameters.Concat(PreparedCookingConsumptionBinding.ToParameters(ledger)).ToArray();
        var model = new DailyPlanCompiler().Compile(rows, f.Files.Before.StateHash, "earn_money");
        var ranking = new JsonObject { ["ranked_event_candidates"] = JsonSerializer.SerializeToNode(rows, JsonOptions) };
        var selected = Assert.Single(SelectedQueueDecisionLease.CreateFromPlan(model, ranking).Candidates);
        var queue = new ActionQueueCompiler().Compile(model, f.Files.Before, ledger);
        Assert.Equal("pending", queue.Status); Assert.Single(queue.Items);
        var evidence = new JsonObject
        {
            ["schema_version"] = "selected_queue_candidate_refresh.v1", ["state_hash"] = f.Files.Before.StateHash,
            ["goal_id"] = model.GoalId, ["policy_model_invoked"] = false, ["selection_locked"] = true,
            ["selected_queue_index"] = selected.QueueIndex, ["selected_candidate_id"] = selected.CandidateId,
            ["selected_option_id"] = selected.OptionId, ["availability"] = JsonSerializer.SerializeToNode(availability, JsonOptions)
        };
        Directory.CreateDirectory(output);
        string Save<T>(string name, T value) { var path = Path.Combine(output, name); Fixture.Save(path, value); return path; }
        var modelPath = Save("cooking-fresh-plan.json", model);
        var queuePath = Save("cooking-fresh-queue.json", queue);
        var evidencePath = Save("cooking-fresh-evidence.json", evidence);
        return decision is null
            ? new(f.Files.SourcePath, modelPath, Save("cooking-ranking.json", ranking), modelPath, queuePath, evidencePath,
                selected.QueueIndex, [])
            : new(decision.SnapshotPath, decision.PlanPath, decision.RankingPath, modelPath, queuePath, evidencePath,
                decision.SelectedQueueIndex, decision.OwnedCandidateIds) { DecisionReference = decision.DecisionReference };
    }

    private static CropTeacherSharedPreparationDecision CookingDecision(SharedCookingFixture f)
    {
        var selected = CompileOwnedCookingSelection(f, f.BeforeLedger, f.Root + "-cooking-decision");
        return new(selected.DecisionSnapshotPath, selected.DecisionPlanPath, selected.DecisionRankingPath,
            selected.SelectedQueueIndex, selected.OwnedCandidateIds);
    }
}
