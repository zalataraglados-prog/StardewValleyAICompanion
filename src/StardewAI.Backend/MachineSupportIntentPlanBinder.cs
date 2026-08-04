using System.Globalization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;

public static class MachineSupportIntentPlanBinder
{
    public static StrategyCommitmentMutationResult? Bind(
        SmallModelPlanEnvelope plan,
        SnapshotEnvelope snapshot,
        IStrategyCommitmentRepository repository)
    {
        var steps = plan.Steps.Where(IsSupportStep).ToArray();
        if (steps.Length == 0)
        {
            return null;
        }
        if (steps.Length != 1)
        {
            return new StrategyCommitmentMutationResult
            {
                Errors =
                [
                    "machine_support_plan_requires_single_intent_step"
                ]
            };
        }

        var step = steps[0];
        var ledger = repository.Get(snapshot);
        var request = string.Equals(
                step.Kind,
                "craft_machine_item",
                StringComparison.Ordinal)
            ? CraftRequest(step, snapshot, ledger)
            : PlacementRequest(step, snapshot, ledger);
        if (request is null)
        {
            return new StrategyCommitmentMutationResult
            {
                Errors =
                [
                    "machine_support_plan_intent_binding_unavailable"
                ],
                Ledger = ledger
            };
        }

        var result = repository.UpsertMachineSupport(
            snapshot,
            request);
        if (result.Accepted && result.Ledger is not null)
        {
            RebindPlanLedger(
                plan,
                step,
                result.Ledger,
                request.IntentId);
        }
        return result;
    }

    private static bool IsSupportStep(SmallModelPlanStep step)
    {
        if (string.Equals(
                step.Kind,
                "craft_machine_item",
                StringComparison.Ordinal))
        {
            return SupportedStatus(
                       Parameter(step, "goal_support_status")) &&
                   !string.IsNullOrWhiteSpace(
                       Parameter(
                           step,
                           "machine_support_intent_id"));
        }

        return string.Equals(
                   step.Kind,
                   "place_machine_item",
                   StringComparison.Ordinal) &&
               !string.IsNullOrWhiteSpace(
                   Parameter(step, "machine_support_intent_id")) &&
               (SupportedStatus(
                    Parameter(step, "goal_support_status")) ||
                string.Equals(
                    Parameter(
                        step,
                        "machine_support_continuation_status"),
                    "active",
                    StringComparison.Ordinal));
    }

    private static MachineSupportIntentUpsertRequest CraftRequest(
        SmallModelPlanStep step,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger) => new()
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = ledger.Revision,
            IntentId = Parameter(
                step,
                "machine_support_intent_id"),
            Stage = MachineSupportIntentStages.CraftSelected,
            SourceDecisionId = CandidateId(step),
            GoalId = Parameter(
                step,
                "goal_support_parent_goal_id"),
            QualifiedItemId = Parameter(
                step,
                "output_qualified_item_id"),
            ItemId = Parameter(step, "output_item_id"),
            DemandClass = Parameter(
                step,
                "machine_demand_class"),
            SupportKind = Parameter(step, "goal_support_kind"),
            EvidenceStatus = Parameter(
                step,
                "goal_support_evidence_status"),
            TaskSourcesJson = Parameter(
                step,
                "priority_task_sources_json"),
            GrossBenefit = IntParameter(
                step,
                "goal_support_gross_benefit"),
            OpportunityCost = IntParameter(
                step,
                "goal_support_opportunity_cost"),
            NetBenefit = IntParameter(
                step,
                "goal_support_net_benefit"),
            SupportScore = DoubleParameter(
                step,
                "goal_support_score"),
            RequiredAdditionalMachineCount = IntParameter(
                step,
                "required_additional_machine_count")
        };

    private static MachineSupportIntentUpsertRequest? PlacementRequest(
        SmallModelPlanStep step,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger)
    {
        var intentId = Parameter(
            step,
            "machine_support_intent_id");
        var existing = ledger.MachineSupportIntents.FirstOrDefault(row =>
            string.Equals(
                row.IntentId,
                intentId,
                StringComparison.Ordinal) &&
            string.Equals(
                row.Status,
                StrategyCommitmentStatuses.Active,
                StringComparison.Ordinal));
        if (existing is null)
        {
            if (!string.Equals(
                    Parameter(step, "goal_support_status"),
                    "supported_exact_active_collection_task",
                    StringComparison.Ordinal))
            {
                return null;
            }

            return new MachineSupportIntentUpsertRequest
            {
                StateHash = snapshot.StateHash,
                ExpectedLedgerRevision = ledger.Revision,
                IntentId = intentId,
                Stage = MachineSupportIntentStages.PlacementBound,
                SourceDecisionId = CandidateId(step),
                GoalId = Parameter(
                    step,
                    "goal_support_parent_goal_id"),
                QualifiedItemId = Parameter(
                    step,
                    "qualified_item_id"),
                ItemId = Parameter(step, "item_id"),
                DemandClass = Parameter(
                    step,
                    "machine_demand_class"),
                SupportKind = Parameter(
                    step,
                    "goal_support_kind"),
                EvidenceStatus = Parameter(
                    step,
                    "goal_support_evidence_status"),
                TaskSourcesJson = Parameter(
                    step,
                    "priority_task_sources_json"),
                GrossBenefit = IntParameter(
                    step,
                    "goal_support_gross_benefit"),
                OpportunityCost = IntParameter(
                    step,
                    "goal_support_opportunity_cost"),
                NetBenefit = IntParameter(
                    step,
                    "goal_support_net_benefit"),
                SupportScore = DoubleParameter(
                    step,
                    "goal_support_score"),
                RequiredAdditionalMachineCount = 1,
                TargetLocationId = step.TargetLocation,
                TargetTileX = step.TargetTileX,
                TargetTileY = step.TargetTileY
            };
        }

        return new MachineSupportIntentUpsertRequest
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = ledger.Revision,
            IntentId = intentId,
            Stage = MachineSupportIntentStages.PlacementBound,
            SourceDecisionId = CandidateId(step),
            GoalId = existing.GoalId,
            QualifiedItemId = Parameter(
                step,
                "qualified_item_id"),
            ItemId = Parameter(step, "item_id"),
            DemandClass = existing.DemandClass,
            SupportKind = existing.SupportKind,
            EvidenceStatus = existing.EvidenceStatus,
            TaskSourcesJson = existing.TaskSourcesJson,
            GrossBenefit = existing.GrossBenefit,
            OpportunityCost = existing.OpportunityCost,
            NetBenefit = existing.NetBenefit,
            SupportScore = existing.SupportScore,
            RequiredAdditionalMachineCount =
                existing.RequiredAdditionalMachineCount,
            TargetLocationId = step.TargetLocation,
            TargetTileX = step.TargetTileX,
            TargetTileY = step.TargetTileY
        };
    }

    private static void RebindPlanLedger(
        SmallModelPlanEnvelope plan,
        SmallModelPlanStep supportStep,
        StrategyCommitmentLedger ledger,
        string intentId)
    {
        foreach (var step in plan.Steps)
        {
            SetIfPresent(
                step,
                "commitment_ledger_id",
                ledger.LedgerId);
            SetIfPresent(
                step,
                "commitment_ledger_revision",
                ledger.Revision.ToString(
                    CultureInfo.InvariantCulture));
            SetIfPresent(
                step,
                "material_reservation_ledger_id",
                ledger.LedgerId);
            SetIfPresent(
                step,
                "material_reservation_ledger_revision",
                ledger.Revision.ToString(
                    CultureInfo.InvariantCulture));
        }

        var intent = ledger.MachineSupportIntents.Single(row =>
            string.Equals(
                row.IntentId,
                intentId,
                StringComparison.Ordinal));
        Set(
            supportStep,
            "machine_support_intent_revision",
            intent.Revision.ToString(CultureInfo.InvariantCulture));
        Set(
            supportStep,
            "machine_support_intent_stage",
            intent.Stage);
        Set(
            supportStep,
            "machine_support_intent_source_state_hash",
            intent.SourceStateHash);
        if (string.Equals(
                supportStep.Kind,
                "place_machine_item",
                StringComparison.Ordinal))
        {
            Set(
                supportStep,
                "machine_support_continuation_status",
                "active");
            Set(
                supportStep,
                "machine_support_continuation_kind",
                "place_supported_machine");
            Set(
                supportStep,
                "machine_support_goal_id",
                intent.GoalId);
            Set(
                supportStep,
                "machine_support_demand_class",
                intent.DemandClass);
            Set(
                supportStep,
                "machine_support_original_net_benefit",
                intent.NetBenefit.ToString(
                    CultureInfo.InvariantCulture));
            Set(
                supportStep,
                "machine_support_current_input_net_benefit",
                "0");
            Set(
                supportStep,
                "machine_support_continuation_score",
                intent.SupportScore.ToString(
                    "0.####",
                    CultureInfo.InvariantCulture));
            Set(
                supportStep,
                "machine_support_continuation_reason",
                string.Equals(
                    intent.DemandClass,
                    "priority_task_requirement",
                    StringComparison.Ordinal)
                        ? "continue_committed_task_machine_capacity"
                        : "continue_committed_positive_machine_capacity");
        }
    }

    private static void SetIfPresent(
        SmallModelPlanStep step,
        string name,
        string value)
    {
        if (step.Parameters.Any(parameter => string.Equals(
                parameter.Name,
                name,
                StringComparison.Ordinal)))
        {
            Set(step, name, value);
        }
    }

    private static void Set(
        SmallModelPlanStep step,
        string name,
        string value)
    {
        var existing = step.Parameters.FirstOrDefault(parameter =>
            string.Equals(
                parameter.Name,
                name,
                StringComparison.Ordinal));
        if (existing is not null)
        {
            existing.Value = value;
            return;
        }

        step.Parameters = step.Parameters.Append(
            new SmallModelActionParameter
            {
                Name = name,
                Value = value
            }).ToArray();
    }

    private static string CandidateId(SmallModelPlanStep step)
    {
        const string prefix = "candidate_id:";
        return step.Preconditions.FirstOrDefault(value =>
            value.StartsWith(prefix, StringComparison.Ordinal))
            ?[prefix.Length..] ?? step.StepId;
    }

    private static int IntParameter(
        SmallModelPlanStep step,
        string name) =>
        int.TryParse(
            Parameter(step, name),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
                ? value
                : int.MinValue;

    private static double DoubleParameter(
        SmallModelPlanStep step,
        string name) =>
        double.TryParse(
            Parameter(step, name),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
                ? value
                : double.NaN;

    private static string Parameter(
        SmallModelPlanStep step,
        string name) =>
        step.Parameters.FirstOrDefault(parameter => string.Equals(
            parameter.Name,
            name,
            StringComparison.Ordinal))?.Value ?? string.Empty;

    private static bool SupportedStatus(string status) =>
        string.Equals(
            status,
            "supported_bounded_positive_net_benefit",
            StringComparison.Ordinal) ||
        string.Equals(
            status,
            "supported_exact_active_collection_task",
            StringComparison.Ordinal);
}
