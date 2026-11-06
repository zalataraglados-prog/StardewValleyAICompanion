using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static class MarriageTeacherRowBuilder
{
    public static PolicyDecisionTrajectoryEnvelope[] BuildAll(string nativeReceiptPath, string trajectoryPrefix)
    {
        Require(!string.IsNullOrWhiteSpace(trajectoryPrefix), "Marriage Teacher trajectory prefix is required.");
        var source = MarriageNativeJointReceiptBuilder.VerifySource(RequiredFile(nativeReceiptPath));
        return Enumerable.Range(0, source.Preparations.Length).Select(index =>
            Build(source, nativeReceiptPath, index, trajectoryPrefix + "." + index.ToString("D4", CultureInfo.InvariantCulture))).ToArray();
    }

    public static PolicyDecisionTrajectoryEnvelope Build(string nativeReceiptPath, int preparationIndex, string trajectoryId) =>
        Build(MarriageNativeJointReceiptBuilder.VerifySource(RequiredFile(nativeReceiptPath)), nativeReceiptPath, preparationIndex, trajectoryId);

    private static PolicyDecisionTrajectoryEnvelope Build(MarriageNativeJointSource source, string nativeReceiptPath,
        int index, string trajectoryId)
    {
        Require(index >= 0 && index < source.Preparations.Length, "Marriage Teacher preparation index is outside its original source.");
        var preference = source.Preparations[index];
        var execution = source.GiftExecutions[index];
        var step = source.Request.Steps[index];
        var inputs = new MarriagePolicyInputProjector(execution.Before, CurrentTeacherFrontierSupport.ReadArtifactText(preference.CalibrationPath));
        var candidates = inputs.ProjectCandidates(preference.DecisionCandidates.Select((candidate, rank) =>
        {
            var copy = CurrentStageOneCollectionTeacherPreferenceBuilder.NeutralizeLearnerSignals(candidate, MarriageTeacherSupervisionReplayVerifier.ProviderId);
            copy.Rank = rank + 1;
            return copy;
        }));
        var selected = preference.SelectedCandidate!;
        var row = new PolicyDecisionTrajectoryBuilder().Build(trajectoryId, source.Request.RunId,
            CurrentStageOneCollectionTeacherReceiptBuilder.BuildContext(execution.Before),
            new PolicyStateFeatureProjector().Project(new WorldModelProjector().Project(execution.Before,
                MarriagePolicyInputProjector.GoalId, "training_singleplayer"), execution.Before),
            new PolicyTrajectoryVersions
            {
                FeatureSchema = PolicyTrajectoryVersionPins.FeatureSchema,
                CandidateVocabulary = OptionCapabilityRegistrySource.SchemaVersion,
                CapabilityRegistry = OptionCapabilityRegistrySource.SchemaVersion,
                KnowledgeDictionary = PolicyTrajectoryVersionPins.KnowledgeDictionary,
                Compiler = PolicyTrajectoryVersionPins.Compiler, Executor = PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor
            }, execution.Before.StateHash, new AvailabilityAwarePolicyPredictionEnvelope { RankedEventCandidates = candidates },
            selected.CandidateId, CurrentStageOneCollectionTeacherReceiptBuilder.BuildAggregateReceipt(execution.Receipt));
        row.Context.DatasetPartition = PolicyTrajectoryDatasetBuilder.PartitionFor(row.Context.SplitKey);
        var projected = preference.Candidates.Single(value => value.CandidateId == selected.CandidateId);
        var route = selected.Kind == "route_connector_tile";
        row.Audit.TeacherSupervision = new PolicyTrajectoryTeacherSupervision
        {
            SchemaVersion = PolicyTeacherSupervisionSchemas.MarriagePreparation,
            TeacherId = MarriageTeacherSupervisionReplayVerifier.ProviderId, TeacherVersion = MarriageTeacherSupervisionReplayVerifier.ProviderVersion,
            SourceStateHash = row.SourceStateHash,
            CandidateDenominatorSha256 = PolicyPairwiseSupervisionAdmission.CandidateDenominatorSha256(row),
            DatasetPartition = row.Context.DatasetPartition,
            Sources = new PolicyTeacherEvidenceSources
            {
                MarriageNativeJointReceiptPath = Path.GetFullPath(nativeReceiptPath), MarriagePreparationIndex = index,
                BeforeSnapshotPath = preference.Intake.SnapshotPath, PreferencePath = step.PreferencePath!,
                ExecutionReceiptPath = step.ExecutionReceiptPath, AfterSnapshotPath = step.AfterSnapshotPath
            },
            PairwisePreferences = preference.PairwisePreferences, ProvenanceClass = "independent_deterministic_teacher_preference",
            SelectionPolicyId = preference.ComparisonPolicyId,
            PreferenceArtifactSha256 = CurrentTeacherFrontierSupport.HashFile(step.PreferencePath!),
            BeforeSnapshotSha256 = preference.Intake.SnapshotSha256,
            ExecutionReceiptSha256 = CurrentTeacherFrontierSupport.HashFile(step.ExecutionReceiptPath),
            AfterSnapshotSha256 = execution.SourceSha256["after"],
            MarriageNativeJointReceiptSha256 = CurrentTeacherFrontierSupport.HashFile(nativeReceiptPath),
            SelectedCandidateId = selected.CandidateId, SelectedQueueItemId = execution.Queue.Items[0].QueueItemId,
            SelectedQueueItemIds = execution.Queue.Items.Select(item => item.QueueItemId).ToArray(),
            PrimitiveOptionIds = execution.Queue.Items.Select(item => item.OptionId).ToArray(),
            RequirementTransitions = new[] { new PolicyTeacherRequirementTransition
            {
                RequirementSetId = "marriage_preparation", RequirementId = preference.BindingRole,
                BindingKind = "current_partner_gift_support_not_joint_criterion",
                TransitionKind = route ? "native_contact_connector_completed" : "native_proposal_points_prepared",
                BeforeValue = route ? Value(execution.Before, "player", "location_id").GetString()! : projected.FriendshipPointsBefore.ToString(CultureInfo.InvariantCulture),
                AfterValue = route ? Value(execution.After, "player", "location_id").GetString()! : projected.FriendshipPointsAfter.ToString(CultureInfo.InvariantCulture),
                Verified = true
            } }
        };
        var error = PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row);
        Require(error is null, "Original policy validator rejected Marriage Teacher row: " + error);
        return row;
    }

    public static PolicyDecisionTrajectoryEnvelope Verify(string rowPath) =>
        VerifyRow(rowPath, row => new MarriageTeacherSupervisionReplayVerifier().Rebuild(row));

    internal static PolicyDecisionTrajectoryEnvelope VerifyFromJointSource(string rowPath, string nativeReceiptPath,
        MarriageNativeJointSource source) => VerifyRow(rowPath, row =>
    {
        var claim = row.Audit?.TeacherSupervision;
        Require(claim?.SchemaVersion == PolicyTeacherSupervisionSchemas.MarriagePreparation &&
            claim.Sources?.MarriagePreparationIndex is >= 0 and < 64,
            "Marriage corpus row needs its original preparation identity.");
        return Build(source, nativeReceiptPath, claim!.Sources!.MarriagePreparationIndex!.Value, row.TrajectoryId);
    });

    private static PolicyDecisionTrajectoryEnvelope VerifyRow(string rowPath,
        Func<PolicyDecisionTrajectoryEnvelope, PolicyDecisionTrajectoryEnvelope> rebuild)
    {
        var raw = ReadTyped<JsonElement>(RequiredFile(rowPath), PolicyTrajectoryVersionPins.TrajectorySchema);
        var row = raw.Deserialize<PolicyDecisionTrajectoryEnvelope>(JsonDefaults.Options)!;
        var error = PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row);
        Require(error is null, "Marriage Teacher row replay rejected: " + error);
        var expected = rebuild(row);
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(expected, JsonDefaults.Options)),
            "Marriage Teacher row differs from its original preparation and complete native joint source.");
        return expected;
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
