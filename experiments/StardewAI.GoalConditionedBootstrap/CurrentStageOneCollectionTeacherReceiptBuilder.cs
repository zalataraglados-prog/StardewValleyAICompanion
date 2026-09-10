using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentStageOneCollectionTeacherReceiptBuilder
{
    public static CurrentStageOneCollectionTeacherReceiptAdmission Build(
        string requirementInventoryPath,
        string acquisitionLoweringPath,
        string rankingPath,
        string beforeSnapshotPath,
        string masterAnglerTargetDateIntentsPath,
        string preferencePath,
        string executionReceiptPath,
        string afterSnapshotPath,
        string trajectoryId,
        string runId,
        string knowledgeDictionaryVersion,
        string executorVersion)
    {
        var inventoryFullPath = Path.GetFullPath(requirementInventoryPath);
        var loweringFullPath = Path.GetFullPath(acquisitionLoweringPath);
        var rankingFullPath = Path.GetFullPath(rankingPath);
        var beforeFullPath = Path.GetFullPath(beforeSnapshotPath);
        var intentsFullPath = Path.GetFullPath(masterAnglerTargetDateIntentsPath);
        var preferenceFullPath = Path.GetFullPath(preferencePath);
        var receiptFullPath = Path.GetFullPath(executionReceiptPath);
        var afterFullPath = Path.GetFullPath(afterSnapshotPath);
        var preference = CurrentTeacherFrontierSupport.Read<
            CurrentStageOneCollectionTeacherPreferenceLabel>(
            preferenceFullPath,
            "Teacher preference");
        var expectedPreference =
            CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                inventoryFullPath,
                loweringFullPath,
                rankingFullPath,
                beforeFullPath,
                intentsFullPath);
        var result = new CurrentStageOneCollectionTeacherReceiptAdmission
        {
            Preference = preference,
            PreferenceArtifactSha256 = CurrentTeacherFrontierSupport.HashFile(
                preferenceFullPath),
            ExecutionReceiptSha256 = CurrentTeacherFrontierSupport.HashFile(
                receiptFullPath),
            AfterSnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(
                afterFullPath)
        };
        var preferenceReasons = ValidatePreferenceArtifact(
            preference,
            expectedPreference,
            inventoryFullPath,
            loweringFullPath,
            rankingFullPath,
            beforeFullPath,
            intentsFullPath);
        if (preferenceReasons.Length > 0)
        {
            result.Status = "blocked_teacher_preference_not_ready";
            result.BlockingReasons = preferenceReasons;
            return result;
        }

        var ranking = CurrentTeacherFrontierSupport.Read<
            AvailabilityAwarePolicyPredictionEnvelope>(
            rankingFullPath,
            "Availability-aware ranking");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            beforeFullPath,
            "Before snapshot");
        var receipt = CurrentTeacherFrontierSupport.Read<
            PlanExecutionEpisodeEnvelope>(
            receiptFullPath,
            "Execution receipt");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterFullPath,
            "After snapshot");
        var receiptReasons = ValidateReceipt(
            preference,
            before,
            receipt,
            after,
            runId,
            executorVersion);
        if (receiptReasons.Length > 0)
        {
            result.Status = "blocked_execution_receipt_not_exact";
            result.BlockingReasons = receiptReasons;
            return result;
        }

        var selectedCandidate = preference.SelectedCandidate!;
        var compiledQueue = preference.CompiledQueue!;
        var transitions = VerifyTransitions(preference, before, after);
        var failedTransitions = transitions
            .Where(value => !value.Verified)
            .Select(value =>
                "requirement_transition_not_verified:" +
                value.RequirementSetId + ":" + value.RequirementId + ":" +
                value.BindingKind)
            .ToArray();
        if (failedTransitions.Length > 0)
        {
            result.Status = "blocked_exact_requirement_transition_missing";
            result.BlockingReasons = failedTransitions;
            return result;
        }

        var decision = BuildTeacherDecision(preference, ranking);
        var stateFeatures = new PolicyStateFeatureProjector().Project(
            new WorldModelProjector().Project(
                before,
                preference.GoalId,
                "training_singleplayer"));
        var row = new PolicyDecisionTrajectoryBuilder().Build(
            trajectoryId,
            runId,
            BuildContext(before),
            stateFeatures,
            new PolicyTrajectoryVersions
            {
                FeatureSchema = PolicyTrajectoryVersionPins.FeatureSchema,
                CandidateVocabulary = OptionCapabilityRegistrySource.SchemaVersion,
                CapabilityRegistry = OptionCapabilityRegistrySource.SchemaVersion,
                KnowledgeDictionary = knowledgeDictionaryVersion,
                Compiler = PolicyTrajectoryVersionPins.Compiler,
                Executor = executorVersion
            },
            before.StateHash,
            decision,
            selectedCandidate.CandidateId,
            receipt);
        row.Audit.TeacherSupervision = new PolicyTrajectoryTeacherSupervision
        {
            ProvenanceClass = preference.PreferenceProvenanceClass,
            SelectionPolicyId = preference.SelectionPolicyId,
            PreferenceArtifactSha256 = result.PreferenceArtifactSha256,
            RequirementInventorySha256 = preference.RequirementInventorySha256,
            AcquisitionLoweringSha256 = preference.AcquisitionLoweringSha256,
            SourceRankingSha256 = preference.RankingSha256,
            BeforeSnapshotSha256 = preference.SnapshotSha256,
            ExecutionReceiptSha256 = result.ExecutionReceiptSha256,
            AfterSnapshotSha256 = result.AfterSnapshotSha256,
            SelectedCandidateId = selectedCandidate.CandidateId,
            SelectedQueueItemId = compiledQueue.Items.Single()
                .QueueItemId,
            RequirementTransitions = transitions,
            UnavailableCandidateSemantics = preference.CandidateMembership
                .SelectionContract.UnavailableCandidateSemantics
        };
        result.Status = "ready";
        result.TeacherTrainingRowEligible = true;
        result.VerifiedRequirementTransitions = transitions;
        result.TrainingRow = row;
        return result;
    }

    private static AvailabilityAwarePolicyPredictionEnvelope BuildTeacherDecision(
        CurrentStageOneCollectionTeacherPreferenceLabel preference,
        AvailabilityAwarePolicyPredictionEnvelope source)
    {
        var clone = JsonSerializer.Deserialize<
            AvailabilityAwarePolicyPredictionEnvelope>(
            JsonSerializer.Serialize(source, JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
            "Teacher decision could not be cloned.");
        var sourceById = (source.RankedEventCandidates ??
                Array.Empty<PolicyEventCandidatePrediction>())
            .ToDictionary(value => value.CandidateId, StringComparer.Ordinal);
        clone.RankedEventCandidates = preference.OrderedCandidateEvaluations
            .OrderBy(value => value.SelectionOrder)
            .Select(value =>
            {
                var candidate = JsonSerializer.Deserialize<
                    PolicyEventCandidatePrediction>(
                    JsonSerializer.Serialize(
                        sourceById[value.CandidateId],
                        JsonDefaults.Options),
                    JsonDefaults.Options) ?? throw new InvalidDataException(
                    "Teacher candidate could not be cloned.");
                candidate.Rank = value.SelectionOrder;
                candidate.Score = 0;
                candidate.ModelScore = null;
                candidate.ExpectedReward = 0;
                candidate.PolicyModelSource =
                    "deterministic_teacher.current_stage_one_collection.v1";
                return candidate;
            })
            .ToArray();
        return clone;
    }

    private static PolicyTrajectoryContext BuildContext(SnapshotEnvelope snapshot)
    {
        var saveId = snapshot.SaveId.Value;
        return new PolicyTrajectoryContext
        {
            SaveId = !string.IsNullOrWhiteSpace(saveId)
                ? saveId
                : throw new InvalidDataException(
                    "Before snapshot save_id is unavailable."),
            Year = RequiredStateInt(snapshot, "time", "year"),
            Season = RequiredStateString(snapshot, "time", "season"),
            Day = RequiredStateInt(snapshot, "time", "day"),
            Time = RequiredStateInt(snapshot, "time", "time"),
            DatasetPartition = "unassigned"
        };
    }

}
