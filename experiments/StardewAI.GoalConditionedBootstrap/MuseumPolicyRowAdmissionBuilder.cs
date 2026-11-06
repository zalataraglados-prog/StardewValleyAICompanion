using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Capabilities;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MuseumPolicyRowAdmissionBuilder
{
    public static MuseumPolicyRowAdmission Build(string joinPath)
    {
        joinPath = RequiredFile(joinPath);
        var source = MuseumTerminalNativeJoinBuilder.VerifySource(joinPath);
        var snapshot = source.Native.Before;
        var projector = new MuseumPolicyInputProjector(snapshot);
        var inputs = BuildCandidates(source, projector);
        var candidates = inputs.Select(candidate => new MuseumPolicyCandidateAdmission(candidate,
            candidate.Kind != MuseumPolicyInputProjector.DeferKind,
            source.Comparison.CandidateEvaluations.Single(row => row.CandidateId == candidate.CandidateId).Admitted,
            new PolicyTrainingAdmissionFilter().FilterOptionIds(new[] { candidate.OptionId }).Length == 1,
            OptionCapabilityRegistrySource.GetRequired(candidate.OptionId).TrainingExclusionReasons.Select(reason => reason.ToString()).ToArray())).ToArray();
        var blockers = AdmissionReasons(candidates, source.Receipt.PreferredCandidateId, source.Receipt.PairwisePreferences);
        // Native corpus and original policy datasets use different split contracts. Do not overwrite either one.
        var context = CurrentStageOneCollectionTeacherReceiptBuilder.BuildContext(snapshot);
        context.SplitKey = context.SaveId + ":" + context.Year + ":" + context.Season + ":" + context.Day;
        context.DatasetPartition = PolicyTrajectoryDatasetBuilder.PartitionFor(context.SplitKey);
        return new(joinPath, CurrentTeacherFrontierSupport.HashFile(joinPath), AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                new PolicyTrainingAdmissionFilter().Allowlist.Order(StringComparer.Ordinal).ToArray()),
            source.Receipt.RunId, snapshot.StateHash, source.Receipt.SplitKey, source.Receipt.DatasetPartition, context,
            projector.ProjectState(new PolicyStateFeatureProjector().Project(new WorldModelProjector().Project(snapshot,
                MuseumPolicyInputProjector.GoalId, "training_singleplayer"))),
            candidates, source.Receipt.PreferredCandidateId, source.Receipt.PairwisePreferences, blockers);
    }

    public static MuseumPolicyRowAdmission Verify(string path)
    {
        var text = CurrentTeacherFrontierSupport.ReadArtifactText(RequiredFile(path));
        var report = JsonSerializer.Deserialize<MuseumPolicyRowAdmission>(text, JsonDefaults.Options) ??
            throw new InvalidDataException("Museum policy admission is null.");
        var rebuilt = Build(report.JoinPath);
        Require(JsonNode.DeepEquals(JsonNode.Parse(text), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)),
            "Museum policy admission differs from the original native source and current policy rules.");
        return rebuilt;
    }

    private static string[] AdmissionReasons(MuseumPolicyCandidateAdmission[] candidates, string selectedId,
        CurrentCollectionTeacherPairwisePreference[] pairs)
    {
        var blockers = new List<string>();
        var selected = candidates.Single(row => row.Candidate.CandidateId == selectedId);
        if (!selected.PolicyOptionAdmitted)
            blockers.Add(PolicyTrainingAdmissionFilter.OptionNotAdmittedReason + ":" + selected.Candidate.OptionId);
        var trainable = candidates.Where(row => row.OriginalRuntimeCandidate && row.PolicyOptionAdmitted &&
            row.Candidate.Available && row.IndependentComparisonAdmitted).Select(row => row.Candidate.CandidateId).ToHashSet(StringComparer.Ordinal);
        if (!pairs.Any(pair => trainable.Contains(pair.PreferredCandidateId) && trainable.Contains(pair.AlternativeCandidateId)))
            blockers.Add("no_strict_pair_between_original_policy_admitted_candidates");
        return blockers.ToArray();
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
