using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Goals;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.Core.Verifier;
using StardewAI.Core.WorldModel;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private const int DefaultMaxMoveRouteRepairClears = 2;
        private const int HardMaxMoveRouteRepairClears = 4;
        private const int DefaultMoveRouteRepairMinutesPerClear = 2;
        private readonly OptionRegistry.OptionRegistry optionRegistry;
        private readonly Verifier.Verifier verifier;

        public ActionQueueCompiler()
            : this(new OptionRegistry.OptionRegistry(), new Verifier.Verifier())
        {
        }

        public ActionQueueCompiler(OptionRegistry.OptionRegistry optionRegistry, Verifier.Verifier verifier)
        {
            this.optionRegistry = optionRegistry;
            this.verifier = verifier;
        }

        public ActionQueueEnvelope Compile(
            SmallModelActionEnvelope modelOutput,
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger? commitmentLedger = null)
        {
            var diagnostics = new List<string>();
            if (modelOutput.SchemaVersion != "small_model_action.v1")
            {
                diagnostics.Add("unsupported_small_model_action_schema:" + modelOutput.SchemaVersion);
            }

            if (!string.Equals(modelOutput.StateHash, snapshot.StateHash, StringComparison.Ordinal))
            {
                diagnostics.Add("state_hash_mismatch");
            }

            if (modelOutput.Actions.Length == 0)
            {
                diagnostics.Add("empty_action_list");
            }

            diagnostics.AddRange(ValidateExecutionTarget(modelOutput.ExecutionMode, modelOutput.Actor));

            var items = modelOutput.Actions
                .Select(action => CompileAction(
                    action,
                    snapshot,
                    modelOutput.GoalId,
                    modelOutput.ExecutionMode,
                    modelOutput.Actor,
                    diagnostics.Count > 0,
                    commitmentLedger))
                .ToArray();
            var blocked = diagnostics.Count > 0 || items.Any(item => item.Status == "blocked");

            return new ActionQueueEnvelope
            {
                QueueId = "queue." + Guid.NewGuid().ToString("N"),
                SourceModelOutputId = modelOutput.ModelOutputId,
                SourceModel = modelOutput.SourceModel,
                StateHash = snapshot.StateHash,
                GoalId = modelOutput.GoalId,
                ExecutionMode = modelOutput.ExecutionMode,
                Actor = modelOutput.Actor,
                Status = blocked ? "blocked" : "pending",
                Items = items,
                CompilerDiagnostics = diagnostics.ToArray()
            };
        }

        public ActionQueueEnvelope Compile(
            SmallModelPlanEnvelope planOutput,
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger? commitmentLedger = null)
        {
            var actions = new List<SmallModelAction>();
            var activeMenuOpenBeforeStep = ActiveMenuOpen(snapshot);
            var activeMenuTypeBeforeStep = activeMenuOpenBeforeStep ? ActiveMenuType(snapshot) : string.Empty;
            var routedSteps = ExpandCrossLocationMovePrefix(planOutput.Steps, snapshot);
            var expandedSteps = ExpandMoveRouteRepairs(routedSteps, snapshot);
            for (var index = 0; index < expandedSteps.Length; index++)
            {
                var step = expandedSteps[index];
                actions.Add(PlanStepToAction(step, index, expandedSteps.Length, activeMenuOpenBeforeStep, activeMenuTypeBeforeStep));
                if (string.Equals(step.Kind, "close_menu", StringComparison.Ordinal) ||
                    StepClosesMenu(step))
                {
                    activeMenuOpenBeforeStep = false;
                    activeMenuTypeBeforeStep = string.Empty;
                }
                else if (StepOpensMenu(step))
                {
                    activeMenuOpenBeforeStep = true;
                    activeMenuTypeBeforeStep = InferredOpenedMenuType(step);
                }
            }

            var actionEnvelope = new SmallModelActionEnvelope
            {
                ModelOutputId = string.IsNullOrWhiteSpace(planOutput.PlanId)
                    ? "plan." + Guid.NewGuid().ToString("N")
                    : planOutput.PlanId,
                SourceModel = planOutput.SourceModel,
                StateHash = planOutput.StateHash,
                GoalId = planOutput.GoalId,
                ExecutionMode = planOutput.ExecutionMode,
                Actor = planOutput.Actor,
                Actions = actions.ToArray()
            };

            if (planOutput.SchemaVersion != "small_model_plan.v1")
            {
                actionEnvelope.SchemaVersion = "unsupported_plan_schema:" + planOutput.SchemaVersion;
            }

            var queue = Compile(actionEnvelope, snapshot, commitmentLedger);
            queue.CandidateAudit = planOutput.CandidateAudit;
            return queue;
        }

    }
}
