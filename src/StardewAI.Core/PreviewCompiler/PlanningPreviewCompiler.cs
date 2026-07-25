using System;
using StardewAI.Contracts.Goals;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Plans;
using StardewAI.Contracts.Previews;
using StardewAI.Contracts.State;
using StardewAI.Core.GoalCompiler;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Verifier;

namespace StardewAI.Core.PreviewCompiler
{
    public sealed class PlanningPreviewCompiler
    {
        private readonly GoalCompiler.GoalCompiler goalCompiler;
        private readonly OptionRegistry.OptionRegistry optionRegistry;
        private readonly Verifier.Verifier verifier;

        public PlanningPreviewCompiler()
            : this(new GoalCompiler.GoalCompiler(), new OptionRegistry.OptionRegistry(), new Verifier.Verifier())
        {
        }

        public PlanningPreviewCompiler(
            GoalCompiler.GoalCompiler goalCompiler,
            OptionRegistry.OptionRegistry optionRegistry,
            Verifier.Verifier verifier)
        {
            this.goalCompiler = goalCompiler;
            this.optionRegistry = optionRegistry;
            this.verifier = verifier;
        }

        public CommandPreview Compile(SnapshotEnvelope snapshot, string naturalLanguageGoal, string mode)
        {
            GoalSpec goal = goalCompiler.Compile(naturalLanguageGoal, mode);
            OptionSpec spec = optionRegistry.GetRequired(goal.Intent);
            var optionInstance = new OptionInstance
            {
                InstanceId = "option_instance." + Guid.NewGuid().ToString("N"),
                OptionId = spec.OptionId,
                BoundGoalId = goal.GoalId,
                BoundParameters = goal.ExtractedParameters
            };
            var plan = new Plan
            {
                PlanId = "plan." + Guid.NewGuid().ToString("N"),
                Options = new[] { optionInstance }
            };
            SafetyResult safety = verifier.Verify(snapshot, spec);
            var safetyPolicy = SafetyPolicyGate.Evaluate(spec, new OptionAvailabilityCandidate());
            var wouldRequireConfirmation =
                safetyPolicy.ExecutionAuthorization == "confirmation_required";

            return new CommandPreview
            {
                CommandId = "preview." + Guid.NewGuid().ToString("N"),
                Goal = goal,
                SelectedOption = spec,
                OptionInstance = optionInstance,
                Plan = plan,
                Feasibility = safety.Feasibility,
                PreviewOnly = true,
                ExecutionPermission = "disabled",
                WouldBeExecutable = false,
                WouldBeReadEligible = safety.ReadEligible,
                WouldBind = true,
                WouldCompile = false,
                WouldRequireConfirmation = wouldRequireConfirmation,
                WouldBeExecutionAuthorized =
                    safetyPolicy.ExecutionAuthorization == "authorized",
                RequiredStateFactors = spec.RequiredStateFactors,
                MissingStateFactors = safety.MissingStateFactors,
                PreconditionResults = safety.PreconditionResults,
                ExpectedEffects = spec.EstimatedEffects,
                IrreversibleEffects = spec.IrreversibleEffects,
                RiskLevel = safety.Feasibility == "feasible" ? spec.RiskLevel : "unknown",
                Recoverability = safety.Feasibility == "feasible" ? spec.Recoverability : "unknown",
                BlockingReasons = safety.BlockingReasons
            };
        }
    }
}
