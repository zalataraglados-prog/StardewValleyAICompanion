using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;

public static class MachineRelocationIntentPlanBinder
{
    public static StrategyCommitmentMutationResult? Bind(
        SmallModelPlanEnvelope plan,
        SnapshotEnvelope snapshot,
        IStrategyCommitmentRepository repository)
    {
        var steps = plan.Steps
            .Where(step => string.Equals(
                step.Kind,
                "remove_machine_item",
                StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(
                    Parameter(step, "relocation_intent_id")))
            .ToArray();
        if (steps.Length == 0)
        {
            return null;
        }
        if (steps.Length != 1)
        {
            return new StrategyCommitmentMutationResult
            {
                Errors = new[]
                {
                    "machine_relocation_plan_requires_single_intent"
                }
            };
        }

        var step = steps[0];
        var ledger = repository.Get(snapshot);
        var request = new MachineRelocationIntentUpsertRequest
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = ledger.Revision,
            IntentId = Parameter(step, "relocation_intent_id"),
            SourceDecisionId = CandidateId(step),
            QualifiedItemId = Parameter(step, "qualified_item_id"),
            ItemId = Parameter(step, "item_id"),
            SourceLocationId = step.TargetLocation,
            SourceTileX = step.TargetTileX ?? int.MinValue,
            SourceTileY = step.TargetTileY ?? int.MinValue,
            TargetLocationId = Parameter(
                step,
                "relocation_target_location_id"),
            TargetTileX = IntParameter(
                step,
                "relocation_target_tile_x"),
            TargetTileY = IntParameter(
                step,
                "relocation_target_tile_y"),
            MachinePlacementProjectionFingerprint = Parameter(
                step,
                "machine_placement_projection_fingerprint"),
            LayoutNetBenefitTicks = IntParameter(
                step,
                "layout_net_benefit_ticks")
        };
        return repository.UpsertMachineRelocation(snapshot, request);
    }

    private static string CandidateId(SmallModelPlanStep step)
    {
        const string prefix = "candidate_id:";
        return step.Preconditions
            .FirstOrDefault(value => value.StartsWith(
                prefix,
                StringComparison.Ordinal))
            ?[prefix.Length..] ?? step.StepId;
    }

    private static int IntParameter(
        SmallModelPlanStep step,
        string name)
    {
        return int.TryParse(Parameter(step, name), out var value)
            ? value
            : int.MinValue;
    }

    private static string Parameter(
        SmallModelPlanStep step,
        string name)
    {
        return step.Parameters.FirstOrDefault(parameter =>
            string.Equals(
                parameter.Name,
                name,
                StringComparison.Ordinal))?.Value ?? string.Empty;
    }
}
