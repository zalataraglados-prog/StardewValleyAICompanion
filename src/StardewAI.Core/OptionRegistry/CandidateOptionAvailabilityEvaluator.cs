using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Verifier;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private static readonly HashSet<string> ExplicitTargetCandidateIds =
            new(StringComparer.Ordinal)
            {
                "exploration.visit_location"
            };

        private readonly OptionRegistry optionRegistry;
        private readonly Verifier.Verifier verifier;
        private readonly ActionQueueCompiler compiler;
        private readonly IReadOnlyDictionary<string, Func<SnapshotEnvelope, SmallModelActionParameter[], EventCandidate[]>> eventCandidateProviders;

        public CandidateOptionAvailabilityEvaluator()
            : this(new OptionRegistry(), new Verifier.Verifier())
        {
        }

        public CandidateOptionAvailabilityEvaluator(OptionRegistry optionRegistry, Verifier.Verifier verifier)
        {
            this.optionRegistry = optionRegistry;
            this.verifier = verifier;
            compiler = new ActionQueueCompiler(optionRegistry, verifier);
            eventCandidateProviders = CreateEventCandidateProviders();
        }

        public OptionAvailabilityEnvelope Evaluate(
            SnapshotEnvelope snapshot,
            string[] candidateOptionIds,
            bool includeExecutorCalibrationOptions = false,
            StrategyCommitmentLedger? commitmentLedger = null)
        {
            var candidates = candidateOptionIds.Length > 0
                ? candidateOptionIds.Select(optionId => new OptionAvailabilityCandidate { OptionId = optionId }).ToArray()
                : DefaultCandidates(includeExecutorCalibrationOptions);

            return Evaluate(snapshot, candidates, includeExecutorCalibrationOptions, commitmentLedger);
        }

        public OptionAvailabilityEnvelope EvaluateForAutonomousRuntimePlanning(
            SnapshotEnvelope snapshot,
            string[] candidateOptionIds,
            StrategyCommitmentLedger? commitmentLedger = null)
        {
            if (RequiresExclusiveRecovery(snapshot))
            {
                return Evaluate(
                    snapshot,
                    new[] { "recovery.stabilize_day" },
                    commitmentLedger: commitmentLedger);
            }

            if (candidateOptionIds.Length > 0)
            {
                return Evaluate(snapshot, candidateOptionIds, commitmentLedger: commitmentLedger);
            }

            var candidates = DefaultCandidates(includeExecutorCalibrationOptions: false)
                .Append(new OptionAvailabilityCandidate { OptionId = "recovery.stabilize_day" })
                .GroupBy(candidate => candidate.OptionId, StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(candidate => candidate.OptionId, StringComparer.Ordinal)
                .ToArray();
            return Evaluate(snapshot, candidates, commitmentLedger: commitmentLedger);
        }

        public static bool RequiresExclusiveRecovery(SnapshotEnvelope snapshot)
        {
            var time = ReadStateFieldInt(snapshot, "time", "time");
            return GameClockBudgetPolicy.RecoveryWindowStarted(time) ||
                Infrastructure.SleepPromptResumeProjection.IsAvailable(snapshot);
        }

        public OptionAvailabilityEnvelope Evaluate(
            SnapshotEnvelope snapshot,
            OptionAvailabilityCandidate[] candidates,
            bool includeExecutorCalibrationOptions = false,
            StrategyCommitmentLedger? commitmentLedger = null)
        {
            var effectiveCandidates = candidates.Length > 0
                ? candidates
                : DefaultCandidates(includeExecutorCalibrationOptions);
            return new OptionAvailabilityEnvelope
            {
                StateHash = snapshot.StateHash,
                CurrentTime = ReadStateFieldInt(snapshot, "time", "time"),
                Options = effectiveCandidates.Select(candidate => EvaluateOne(snapshot, candidate, commitmentLedger)).ToArray()
            };
        }

        private OptionAvailabilityCandidate[] DefaultCandidates(bool includeExecutorCalibrationOptions)
        {
            return optionRegistry.All
                .Where(option => includeExecutorCalibrationOptions || option.TrainingRole != TrainingRoles.ExecutorCalibration)
                .Where(option => option.InvocationPolicy != OptionInvocationPolicy.PlayerCommandOnly)
                .Where(option => !ExplicitTargetCandidateIds.Contains(option.OptionId))
                .Select(option => new OptionAvailabilityCandidate { OptionId = option.OptionId })
                .OrderBy(candidate => candidate.OptionId, StringComparer.Ordinal)
                .ToArray();
        }

        private OptionAvailability EvaluateOne(
            SnapshotEnvelope snapshot,
            OptionAvailabilityCandidate candidate,
            StrategyCommitmentLedger? commitmentLedger)
        {
            OptionSpec option;
            try
            {
                option = optionRegistry.GetRequired(candidate.OptionId);
            }
            catch (KeyNotFoundException)
            {
                return new OptionAvailability
                {
                    OptionId = candidate.OptionId,
                    Parameters = candidate.Parameters,
                    Available = false,
                    Status = "blocked",
                    BlockingReasons = new[] { "unknown_option_id" },
                    HardBlockReasons = new[] { "unknown_option_id" },
                    AvailabilityNotes = new[] { "candidate_rejected_before_model_scoring" }
                };
            }

            var safety = verifier.Verify(snapshot, option);
            var reasons = new List<string>(safety.BlockingReasons);
            var notes = new List<string>();
            var compilerProbe = IsUnboundSocialCandidate(candidate) ||
                IsSocialContinuationCandidate(candidate) ||
                IsPurchaseContinuationCandidate(candidate) ||
                IsSaleContinuationCandidate(candidate)
                ? new CompilerProbeResult()
                : ProbeCompiler(snapshot, candidate, commitmentLedger);
            var compilerReasons = compilerProbe.BlockingReasons;
            var safetyPolicy = SafetyPolicyGate.Evaluate(option, candidate);
            var eventCandidates = EventCandidates(snapshot, option.OptionId, safety.MissingStateFactors, candidate.Parameters, commitmentLedger);
            var economicCandidates = EconomicCandidates(
                snapshot,
                option.OptionId,
                candidate.Parameters);
            var socialCandidates = SocialCandidates(snapshot, option.OptionId, safety.MissingStateFactors, candidate.Parameters);
            var valueReasons = safety.MissingStateFactors.Length == 0
                ? ValueGateBlockingReasons(
                    snapshot,
                    option.OptionId,
                    economicCandidates,
                    eventCandidates)
                : Array.Empty<string>();
            var eventCandidateReasons = option.OptionId == "mining.reach_depth" ||
                option.OptionId == "mining.obtain_skull_key" ||
                option.OptionId == "mining.acquire_golden_scythe" ||
                option.OptionId == "volcano.reach_caldera" ||
                safety.MissingStateFactors.Length == 0
                ? EventCandidateGateBlockingReasons(option.OptionId, eventCandidates, candidate.Parameters.Length > 0)
                : Array.Empty<string>();
            var socialCandidateReasons = safety.MissingStateFactors.Length == 0
                ? SocialCandidateGateBlockingReasons(option.OptionId, socialCandidates, candidate.Parameters.Length > 0)
                : Array.Empty<string>();
            reasons.AddRange(compilerReasons);
            reasons.AddRange(valueReasons);
            reasons.AddRange(eventCandidateReasons);
            reasons.AddRange(socialCandidateReasons);
            reasons.AddRange(safetyPolicy.BlockingReasons);
            var executorEnabled = IsExecutorEnabled(option.OptionId);
            var previewOnly = IsPreviewOnly(option.OptionId, option.TrainingRole, executorEnabled);

            if (!executorEnabled)
            {
                reasons.Add(ExecutorDisabledReason(option.OptionId));
            }

            if (previewOnly)
            {
                notes.Add("preview_only_candidate_not_runtime_executable");
            }

            if (option.HarnessDispatchSupported && !option.ProductExecutorSupported)
            {
                notes.Add("runtime_test_harness_dispatch_is_not_product_executor_support");
            }

            if (option.TrainingRole == TrainingRoles.ExecutorCalibration)
            {
                notes.Add("executor_calibration_option_excluded_from_default_policy_ranking");
            }

            if (option.InvocationPolicy == OptionInvocationPolicy.PlayerCommandOnly)
            {
                notes.Add("player_command_only_excluded_from_default_candidates_and_policy_training");
            }

            var hasMissingState = safety.MissingStateFactors.Length > 0;
            var hasParameterBlock = compilerReasons.Length > 0;
            var hasValueBlock = valueReasons.Length > 0;
            var hasEventCandidateBlock = eventCandidateReasons.Length > 0;
            var hasSocialCandidateBlock = socialCandidateReasons.Length > 0;
            var hasDomainBlock = hasValueBlock || hasEventCandidateBlock || hasSocialCandidateBlock;
            var compileReady = compilerProbe.CompileStatus == "ready";
            var executionAuthorized = safetyPolicy.ExecutionAuthorization == "authorized";
            var available = safety.ReadEligible &&
                compilerProbe.BindingStatus == "bound" &&
                compileReady &&
                executionAuthorized &&
                !hasDomainBlock &&
                !previewOnly &&
                executorEnabled;
            var status = !safety.ReadEligible ||
                hasParameterBlock ||
                hasDomainBlock ||
                safetyPolicy.ExecutionAuthorization == "denied"
                    ? "blocked"
                    : safetyPolicy.ExecutionAuthorization == "confirmation_required"
                        ? "confirmation_required"
                        : previewOnly
                            ? "preview_available"
                            : !executorEnabled
                                ? "product_not_integrated"
                                : compilerProbe.BindingStatus == "unbound"
                                    ? "unbound"
                                    : compilerProbe.CompileStatus == "blocked"
                                        ? "blocked"
                                        : "available";

            return new OptionAvailability
            {
                OptionId = option.OptionId,
                Available = available,
                ReadEligible = safety.ReadEligible,
                BindingStatus = compilerProbe.BindingStatus,
                CompileStatus = compilerProbe.CompileStatus,
                ExecutionAuthorization = safetyPolicy.ExecutionAuthorization,
                RuntimeEvidenceStatus = option.RuntimeStatus,
                TrainingEligibility = option.TrainingEligibility,
                InvocationPolicy = option.InvocationPolicy,
                ProductStatus = option.ProductStatus,
                ProductIntegrationStatus = option.ProductIntegrationStatus,
                HarnessDispatchSupported = option.HarnessDispatchSupported,
                ProductExecutorSupported = option.ProductExecutorSupported,
                Status = status,
                PreviewOnly = previewOnly,
                ExecutorEnabled = executorEnabled,
                TrainingRole = option.TrainingRole,
                BehaviorCategory = option.BehaviorCategory,
                CompilerResponsibility = option.CompilerResponsibility,
                RequiredStateFactors = option.RequiredStateFactors,
                Parameters = candidate.Parameters,
                MissingStateFactors = safety.MissingStateFactors,
                BlockingReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                HardBlockReasons = safety.BlockingReasons
                    .Concat(safetyPolicy.BlockingReasons)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray(),
                PreconditionResults = safety.PreconditionResults,
                AvailabilityNotes = notes.ToArray(),
                EconomicCandidates = economicCandidates,
                EventCandidates = eventCandidates,
                SocialCandidates = socialCandidates
            };
        }

    }
}
