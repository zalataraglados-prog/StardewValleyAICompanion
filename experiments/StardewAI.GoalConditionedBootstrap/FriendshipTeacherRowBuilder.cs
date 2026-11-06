using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static class FriendshipTeacherRowBuilder
{
    public static PolicyDecisionTrajectoryEnvelope Build(string nativeReceiptPath, string trajectoryId)
    {
        nativeReceiptPath = RequiredFile(nativeReceiptPath);
        var source = FriendshipNativeThresholdReceiptBuilder.VerifySource(nativeReceiptPath);
        var execution = source.Execution;
        var preference = source.Preference;
        var inputs = new FriendshipPolicyInputProjector(execution.Before, File.ReadAllText(preference.CalibrationPath));
        var candidates = preference.DecisionCandidates.Select((candidate, index) =>
        {
            var clone = CurrentStageOneCollectionTeacherPreferenceBuilder.NeutralizeLearnerSignals(candidate,
                FriendshipTeacherSupervisionReplayVerifier.ProviderId);
            clone.Rank = index + 1;
            return clone;
        }).ToArray();
        candidates = inputs.ProjectCandidates(candidates);
        var selected = preference.SelectedCandidate!;
        var row = new PolicyDecisionTrajectoryBuilder().Build(trajectoryId, source.Request.RunId,
            CurrentStageOneCollectionTeacherReceiptBuilder.BuildContext(execution.Before),
            new PolicyStateFeatureProjector().Project(new WorldModelProjector().Project(execution.Before,
                FriendshipPolicyInputProjector.GoalId, "training_singleplayer"), execution.Before),
            new PolicyTrajectoryVersions
            {
                FeatureSchema = PolicyTrajectoryVersionPins.FeatureSchema,
                CandidateVocabulary = OptionCapabilityRegistrySource.SchemaVersion,
                CapabilityRegistry = OptionCapabilityRegistrySource.SchemaVersion,
                KnowledgeDictionary = PolicyTrajectoryVersionPins.KnowledgeDictionary,
                Compiler = PolicyTrajectoryVersionPins.Compiler,
                Executor = PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor
            }, execution.Before.StateHash,
            new AvailabilityAwarePolicyPredictionEnvelope { RankedEventCandidates = candidates },
            selected.CandidateId, CurrentStageOneCollectionTeacherReceiptBuilder.BuildAggregateReceipt(execution.Receipt));
        row.Context.DatasetPartition = PolicyTrajectoryDatasetBuilder.PartitionFor(row.Context.SplitKey);
        row.Audit.TeacherSupervision = new PolicyTrajectoryTeacherSupervision
        {
            SchemaVersion = PolicyTeacherSupervisionSchemas.Friendships,
            TeacherId = FriendshipTeacherSupervisionReplayVerifier.ProviderId,
            TeacherVersion = FriendshipTeacherSupervisionReplayVerifier.ProviderVersion,
            SourceStateHash = row.SourceStateHash,
            CandidateDenominatorSha256 = PolicyPairwiseSupervisionAdmission.CandidateDenominatorSha256(row),
            DatasetPartition = row.Context.DatasetPartition,
            Sources = new PolicyTeacherEvidenceSources
            {
                FriendshipNativeThresholdReceiptPath = nativeReceiptPath,
                BeforeSnapshotPath = preference.SnapshotPath,
                PreferencePath = source.Request.PreferencePath,
                ExecutionReceiptPath = source.Request.ExecutionReceiptPath,
                AfterSnapshotPath = source.Request.AfterSnapshotPath
            },
            PairwisePreferences = preference.PairwisePreferences,
            ProvenanceClass = "independent_deterministic_teacher_preference",
            SelectionPolicyId = preference.ComparisonPolicyId,
            PreferenceArtifactSha256 = CurrentTeacherFrontierSupport.HashFile(source.Request.PreferencePath),
            BeforeSnapshotSha256 = preference.SnapshotSha256,
            ExecutionReceiptSha256 = CurrentTeacherFrontierSupport.HashFile(source.Request.ExecutionReceiptPath),
            AfterSnapshotSha256 = execution.SourceSha256["after"],
            FriendshipNativeThresholdReceiptSha256 = CurrentTeacherFrontierSupport.HashFile(nativeReceiptPath),
            SelectedCandidateId = selected.CandidateId,
            SelectedQueueItemId = execution.Queue.Items[0].QueueItemId,
            SelectedQueueItemIds = execution.Queue.Items.Select(item => item.QueueItemId).ToArray(),
            PrimitiveOptionIds = execution.Queue.Items.Select(item => item.OptionId).ToArray(),
            RequirementTransitions = new[] { new PolicyTeacherRequirementTransition
            {
                RequirementSetId = "friendships",
                RequirementId = source.Request.TargetQualifyingCount == 5 ? "friendships_5" : "friendships_10",
                BindingKind = "native_friendship_qualifying_count", TransitionKind = "native_qualifying_count_threshold_crossed",
                BeforeValue = (source.Request.TargetQualifyingCount - 1).ToString(CultureInfo.InvariantCulture),
                AfterValue = source.Request.TargetQualifyingCount.ToString(CultureInfo.InvariantCulture), Verified = true
            } }
        };
        var error = PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row);
        Require(error is null, "Original policy envelope validator rejected Friendship Teacher row: " + error);
        return row;
    }

    public static PolicyDecisionTrajectoryEnvelope Verify(string rowPath)
    {
        var raw = ReadTyped<JsonElement>(RequiredFile(rowPath), PolicyTrajectoryVersionPins.TrajectorySchema);
        var row = raw.Deserialize<PolicyDecisionTrajectoryEnvelope>(JsonDefaults.Options)!;
        var error = PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row);
        Require(error is null, "Friendship Teacher row replay rejected: " + error);
        var expected = new FriendshipTeacherSupervisionReplayVerifier().Rebuild(row);
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(expected, JsonDefaults.Options)),
            "Friendship Teacher row differs from its complete original replay.");
        return expected;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
