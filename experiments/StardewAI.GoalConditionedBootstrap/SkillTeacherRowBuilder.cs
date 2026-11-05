using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static class SkillTeacherRowBuilder
{
    public static PolicyDecisionTrajectoryEnvelope Build(string nativeReceiptPath, string executionReceiptPath,
        string trajectoryId)
    {
        nativeReceiptPath = RequiredFile(nativeReceiptPath);
        executionReceiptPath = RequiredFile(executionReceiptPath);
        var source = SkillNativeThresholdReceiptBuilder.VerifySource(nativeReceiptPath);
        var action = source.Action;
        var preference = source.Dispatch.Preference;
        var selected = preference.SelectedCandidate!;
        var receipt = ReadTyped<QueueExecutionReceiptEnvelope>(executionReceiptPath, "queue_execution_receipt.v1");
        var reasons = QueueExecutionReceiptValidator.Validate(action.Queue, action.Before, receipt, action.After,
            source.Request.RunId, PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, selected.CandidateId,
            action.Before.StateHash, requireTeacherPreferenceStateRebound: false);
        Require(reasons.Length == 0 && action.Queue.Items.Length == 1,
            "Skills Teacher requires the complete original single native action queue: " + string.Join(",", reasons));
        var raw = CurrentTeacherFrontierSupport.Read<JsonElement>(executionReceiptPath, "Skills aggregate execution");
        var step = raw.GetProperty("step_results").EnumerateArray().Single();
        var recordedAction = RequiredFile(CurrentTeacherFrontierSupport.RequiredString(step, "execution_path"));
        Require(recordedAction.Equals(RequiredFile(source.Request.ExecutionReceiptPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
            CurrentTeacherFrontierSupport.RequiredString(step, "execution_request_nonce") == action.Request.RequestNonce &&
            JsonNode.DeepEquals(JsonNode.Parse(receipt.StepResults[0].ChangedFacts.GetRawText()),
                JsonSerializer.SerializeToNode(action.Result.ChangedFacts, JsonDefaults.Options)),
            "Skills aggregate receipt does not bind the original native action, nonce and observed facts.");
        var candidates = source.Dispatch.DecisionCandidates.Select((candidate, index) =>
        {
            var clone = CurrentStageOneCollectionTeacherPreferenceBuilder.NeutralizeLearnerSignals(candidate,
                SkillTeacherSupervisionReplayVerifier.ProviderId);
            clone.Rank = index + 1;
            return clone;
        }).ToArray();
        var row = new PolicyDecisionTrajectoryBuilder().Build(trajectoryId, source.Request.RunId,
            CurrentStageOneCollectionTeacherReceiptBuilder.BuildContext(action.Before),
            new PolicyStateFeatureProjector().Project(new WorldModelProjector().Project(action.Before,
                source.Dispatch.Queue.GoalId, "training_singleplayer"), action.Before),
            new PolicyTrajectoryVersions
            {
                FeatureSchema = PolicyTrajectoryVersionPins.FeatureSchema,
                CandidateVocabulary = OptionCapabilityRegistrySource.SchemaVersion,
                CapabilityRegistry = OptionCapabilityRegistrySource.SchemaVersion,
                KnowledgeDictionary = PolicyTrajectoryVersionPins.KnowledgeDictionary,
                Compiler = PolicyTrajectoryVersionPins.Compiler,
                Executor = PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor
            }, action.Before.StateHash,
            new AvailabilityAwarePolicyPredictionEnvelope { RankedEventCandidates = candidates },
            selected.CandidateId, CurrentStageOneCollectionTeacherReceiptBuilder.BuildAggregateReceipt(receipt));
        var before = preference.Deficit;
        var after = new CurrentSkillExperienceDeficitBuilder().Build(action.After);
        row.Context.DatasetPartition = PolicyTrajectoryDatasetBuilder.PartitionFor(row.Context.SplitKey);
        row.Audit.TeacherSupervision = new PolicyTrajectoryTeacherSupervision
        {
            SchemaVersion = PolicyTeacherSupervisionSchemas.Skills,
            TeacherId = SkillTeacherSupervisionReplayVerifier.ProviderId,
            TeacherVersion = SkillTeacherSupervisionReplayVerifier.ProviderVersion,
            SourceStateHash = row.SourceStateHash,
            CandidateDenominatorSha256 = PolicyPairwiseSupervisionAdmission.CandidateDenominatorSha256(row),
            DatasetPartition = row.Context.DatasetPartition,
            Sources = new PolicyTeacherEvidenceSources
            {
                SkillNativeThresholdReceiptPath = nativeReceiptPath,
                StrategyLedgerPath = preference.StrategyLedgerPath,
                BeforeSnapshotPath = preference.SnapshotPath,
                PreferencePath = source.Request.PreferencePath,
                ExecutionReceiptPath = executionReceiptPath,
                AfterSnapshotPath = source.Request.AfterSnapshotPath
            },
            PairwisePreferences = preference.PairwisePreferences,
            ProvenanceClass = "independent_deterministic_teacher_preference",
            SelectionPolicyId = preference.SelectionPolicyId,
            PreferenceArtifactSha256 = CurrentTeacherFrontierSupport.HashFile(source.Request.PreferencePath),
            BeforeSnapshotSha256 = preference.SnapshotSha256,
            ExecutionReceiptSha256 = CurrentTeacherFrontierSupport.HashFile(executionReceiptPath),
            AfterSnapshotSha256 = action.SourceSha256["after"],
            SkillNativeThresholdReceiptSha256 = CurrentTeacherFrontierSupport.HashFile(nativeReceiptPath),
            SelectedCandidateId = selected.CandidateId,
            SelectedQueueItemId = action.Item.QueueItemId,
            SelectedQueueItemIds = action.Queue.Items.Select(item => item.QueueItemId).ToArray(),
            PrimitiveOptionIds = action.Queue.Items.Select(item => item.OptionId).ToArray(),
            RequirementTransitions = new[] { new PolicyTeacherRequirementTransition
            {
                RequirementSetId = "skills",
                RequirementId = source.Request.TargetScoringLevel == 15 ? "player_level_15" : "player_level_25",
                BindingKind = "native_base_skill_experience", TransitionKind = "native_scoring_level_threshold_crossed",
                BeforeValue = before.ScoringLevel!.Value.ToString(CultureInfo.InvariantCulture),
                AfterValue = after.ScoringLevel!.Value.ToString(CultureInfo.InvariantCulture), Verified = true
            } }
        };
        Require(PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row) is null,
            "Original policy envelope validator rejected Skills Teacher row: " + PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row));
        return row;
    }

    public static PolicyDecisionTrajectoryEnvelope Verify(string rowPath)
    {
        var raw = ReadTyped<JsonElement>(RequiredFile(rowPath), PolicyTrajectoryVersionPins.TrajectorySchema);
        var row = raw.Deserialize<PolicyDecisionTrajectoryEnvelope>(JsonDefaults.Options)!;
        var error = PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row);
        Require(error is null, "Skills Teacher row replay rejected: " + error);
        var expected = new SkillTeacherSupervisionReplayVerifier().Rebuild(row);
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(expected, JsonDefaults.Options)),
            "Skills Teacher row differs from its complete original replay.");
        return expected;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
