using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.LiveTrainingLoop;

public sealed partial class CropTeacherManagedSeedOwner
{
    private static (SmallModelPlanEnvelope Plan, JsonObject Queue) SharedRetryWaitSelectionViews(
        SmallModelPlanEnvelope model, JsonObject queue, JsonArray materialized, SelectedQueueCandidateLock selected,
        SnapshotEnvelope source)
    {
        var accepted = model.CandidateAudit.Where(row => row.Decision == "accepted").ToArray();
        if (!accepted.Any(row => row.Kind == "social_continuation_retry_wait")) return (model, queue);
        var matches = SelectedQueueCandidateMatcher.FilterMaterializedCandidates(materialized, selected)
            .Deserialize<PolicyEventCandidatePrediction[]>(JsonOptions)!;
        if (accepted.Length != 1 || matches.Length != 1 || model.Steps.Length != 1 ||
            matches[0].Kind != "social_continuation_retry_wait" || !matches[0].Available ||
            matches[0].CandidateId != accepted[0].CandidateId || matches[0].OptionId != selected.OptionId ||
            selected.OptionId is not ("social.talk_npc" or "social.gift_npc" or "social.advance_partnership"))
            throw new InvalidDataException("Shared retry wait lacks its one available original social objective/audit.");
        var replay = new DailyPlanCompiler().Compile(matches, model.StateHash, model.GoalId, model.ExecutionMode, 1,
            accepted[0].RemainingMinutesBefore, accepted[0].RemainingEnergyBefore);
        var expected = replay.Steps.Single();
        var step = model.Steps[0];
        if (expected.Kind != "wait_ticks" || step.StepId != expected.StepId || step.Kind != expected.Kind ||
            step.WaitTicks != expected.WaitTicks || step.EstimatedMinutes != expected.EstimatedMinutes ||
            step.TargetLocation != expected.TargetLocation || step.TargetTileX != expected.TargetTileX || step.TargetTileY != expected.TargetTileY ||
            !step.Preconditions.SequenceEqual(expected.Preconditions) || !step.ExpectedEffects.SequenceEqual(expected.ExpectedEffects) ||
            !step.FailurePolicy.SequenceEqual(expected.FailurePolicy) ||
            expected.SafetyConstraints.Any(value => !step.SafetyConstraints.Contains(value, StringComparer.Ordinal)) ||
            !Parameters(step).SequenceEqual(Parameters(expected)))
            throw new InvalidDataException("Shared retry wait differs from the original bounded social compiler.");
        var original = new ActionQueueCompiler().Compile(model, source);
        if (original.Status != "pending" || original.Items.Length != 1 ||
            queue["items"] is not JsonArray items || items.Count != 1 || items[0] is not JsonObject item ||
            original.Items[0].OptionId != "executor.wait_ticks" || item["option_id"]?.GetValue<string>() != "executor.wait_ticks" ||
            queue["source_model_output_id"]?.GetValue<string>() != model.PlanId ||
            item["source_action_id"]?.GetValue<string>() != step.StepId ||
            !JsonNode.DeepEquals(CommandView(JsonSerializer.SerializeToNode(original.Items[0].NormalizedCommand, JsonOptions)!.AsObject()),
                CommandView(item["normalized_command"]!.AsObject())))
            throw new InvalidDataException("Shared retry wait queue differs from its original compiled model.");

        // Selection views only. The old compiler deliberately emits this control wait without candidate_id.
        var planView = JsonSerializer.Deserialize<SmallModelPlanEnvelope>(JsonSerializer.Serialize(model, JsonOptions), JsonOptions)!;
        var queueView = JsonNode.Parse(queue.ToJsonString())!.AsObject();
        var marker = "candidate_id:" + accepted[0].CandidateId;
        planView.Steps[0].Preconditions = planView.Steps[0].Preconditions.Append(marker).ToArray();
        queueView["items"]![0]!["normalized_command"]!["parameters"]!.AsArray().Add(new JsonObject
            { ["name"] = "precondition", ["value"] = marker });
        return (planView, queueView);

        static IEnumerable<(string, string)> Parameters(SmallModelPlanStep value) => value.Parameters
            .Where(parameter => !parameter.Name.StartsWith("budget.", StringComparison.Ordinal))
            .Select(parameter => (parameter.Name, parameter.Value));

        static JsonObject CommandView(JsonObject command)
        {
            var view = JsonNode.Parse(command.ToJsonString())!.AsObject();
            // The original wire omits null optional plans; preserve any real or unknown content.
            foreach (var name in new[] { "social_plan", "quest_plan" })
                if (!view.ContainsKey(name)) view[name] = null;
            // Native primitive IDs are generated per compilation; all command semantics remain exact.
            if (view["steps"] is JsonArray commands)
                foreach (var primitive in commands.OfType<JsonObject>()) primitive.Remove("step_id");
            return view;
        }
    }
}
