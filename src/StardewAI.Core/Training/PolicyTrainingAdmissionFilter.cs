using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Training
{
    public sealed class PolicyTrainingAdmissionDecision
    {
        public bool Included { get; init; }
        public bool CalibrationExcluded { get; init; }
        public string[] OptionIds { get; init; } = Array.Empty<string>();
        public string[] Reasons { get; init; } = Array.Empty<string>();
    }

    public sealed class PolicyTrainingAdmissionFilter
    {
        public const string CalibrationExcludedReason = "exclude_from_policy_training";
        public const string MissingOptionReason = "policy_training_option_missing";
        public const string MultipleOptionsReason = "policy_training_requires_single_model_option";
        public const string OptionNotAdmittedReason = "policy_training_option_not_admitted";

        private readonly HashSet<string> allowlist;
        private readonly bool useExecutionContext;
        private readonly bool singlePlayerVariant;
        private static readonly Lazy<PolicyTrainingAdmissionFilter> ConservativeFilter = new(() => new PolicyTrainingAdmissionFilter());
        private static readonly Lazy<PolicyTrainingAdmissionFilter> SinglePlayerFilter = new(() =>
            new PolicyTrainingAdmissionFilter(OptionCapabilityRegistrySource.All
                .Where(declaration => declaration.PolicyTrainingCandidate &&
                    declaration.TrainingExclusionReasons.All(reason =>
                        reason == TrainingAdmissionExclusionReason.ExplicitPlayerConfirmationRequired))
                .Select(declaration => declaration.OptionId), true));

        public PolicyTrainingAdmissionFilter()
            : this(OptionCapabilityRegistrySource.TrainingAllowlist)
        {
            useExecutionContext = true;
        }

        internal PolicyTrainingAdmissionFilter(IEnumerable<string> optionIds)
        {
            var values = optionIds
                .Where(optionId => !string.IsNullOrWhiteSpace(optionId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(optionId => optionId, StringComparer.Ordinal)
                .ToArray();
            allowlist = new HashSet<string>(values, StringComparer.Ordinal);
            Allowlist = new ReadOnlyCollection<string>(values);
        }

        private PolicyTrainingAdmissionFilter(IEnumerable<string> optionIds, bool singlePlayer)
            : this(optionIds)
        {
            useExecutionContext = true;
            singlePlayerVariant = singlePlayer;
        }

        public IReadOnlyCollection<string> Allowlist { get; }

        public PolicyTrainingAdmissionFilter ForState(FeatureVector? features) =>
            WithSinglePlayerScope(SinglePlayerScope.IsSinglePlayerTraining(features));

        public PolicyTrainingAdmissionFilter ForSnapshot(SnapshotEnvelope? snapshot) =>
            WithSinglePlayerScope(SinglePlayerScope.IsKnownSinglePlayer(snapshot));

        private PolicyTrainingAdmissionFilter WithSinglePlayerScope(bool singlePlayer)
        {
            if (!useExecutionContext) return this;
            if (singlePlayer) return SinglePlayerFilter.Value;
            return singlePlayerVariant ? ConservativeFilter.Value : this;
        }

        public string[] FilterOptionIds(IEnumerable<string> optionIds)
        {
            return optionIds
                .Where(optionId => !string.IsNullOrWhiteSpace(optionId) && allowlist.Contains(optionId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(optionId => optionId, StringComparer.Ordinal)
                .ToArray();
        }

        public PolicyTrainingAdmissionDecision Evaluate(TrainingFeatureRowEnvelope row)
        {
            var optionIds = (row.ActionFeatures.OptionIds ?? Array.Empty<string>())
                .Where(optionId => !string.IsNullOrWhiteSpace(optionId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(optionId => optionId, StringComparer.Ordinal)
                .ToArray();

            if (IsCalibrationRow(row))
            {
                return Excluded(optionIds, calibration: true, CalibrationExcludedReason);
            }

            if (optionIds.Length == 0)
            {
                return Excluded(optionIds, calibration: false, MissingOptionReason);
            }

            if (optionIds.Length != 1)
            {
                return Excluded(optionIds, calibration: false, MultipleOptionsReason);
            }

            if (!ForState(row.StateFeatures).allowlist.Contains(optionIds[0]))
            {
                return Excluded(optionIds, calibration: false, OptionNotAdmittedReason);
            }

            return new PolicyTrainingAdmissionDecision
            {
                Included = true,
                OptionIds = optionIds
            };
        }

        private static bool IsCalibrationRow(TrainingFeatureRowEnvelope row)
        {
            if (row.ActionFeatures.ExcludeFromPolicyTraining ||
                string.Equals(row.ActionFeatures.TrainingRole, "executor_calibration", StringComparison.Ordinal) ||
                string.Equals(row.ActionFeatures.LearningScope, "calibration_only", StringComparison.Ordinal))
            {
                return true;
            }

            return row.ActionFeatures.Features.Categorical.Any(item =>
                string.Equals(item.Name, "action.training_role", StringComparison.Ordinal) &&
                string.Equals(item.Value, "executor_calibration", StringComparison.Ordinal));
        }

        private static PolicyTrainingAdmissionDecision Excluded(
            string[] optionIds,
            bool calibration,
            string reason)
        {
            return new PolicyTrainingAdmissionDecision
            {
                Included = false,
                CalibrationExcluded = calibration,
                OptionIds = optionIds,
                Reasons = new[] { reason }
            };
        }
    }
}
