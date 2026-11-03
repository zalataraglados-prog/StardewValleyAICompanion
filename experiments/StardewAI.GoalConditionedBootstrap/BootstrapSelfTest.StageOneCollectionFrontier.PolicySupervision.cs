using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private sealed partial class StageOneCollectionFrontierFixture
    {
        private CurrentStageOneCollectionTeacherReceiptAdmission BuildSupervisedComparisonReceiptFixture(
            string runId, string afterStateHash, CurrentCommunityCenterDenominatorReport denominator)
        {
            var ranking = JsonSerializer.Deserialize<AvailabilityAwarePolicyPredictionEnvelope>(
                File.ReadAllText(rankingPath), JsonDefaults.Options)!;
            var alternate = JsonSerializer.Deserialize<PolicyEventCandidatePrediction>(
                JsonSerializer.Serialize(ranking.RankedEventCandidates.Single(candidate =>
                    candidate.CandidateId == "master-angler-route"), JsonDefaults.Options), JsonDefaults.Options)!;
            alternate.CandidateId = "master-angler-comparison-alternative";
            alternate.Rank = ranking.RankedEventCandidates.Length + 1;
            alternate.EstimatedTicks += 120;
            alternate.EnergyCost++;
            ranking.RankedEventCandidates = ranking.RankedEventCandidates.Append(alternate).ToArray();
            var comparisonRankingPath = Path.Combine(root, "pairwise-ranking.json");
            var comparisonPreferencePath = Path.Combine(root, "pairwise-preference.json");
            var comparisonReceiptPath = Path.Combine(root, "pairwise-execution-receipt.json");
            Write(comparisonRankingPath, ranking);
            var preference = CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                inventoryPath, loweringPath, comparisonRankingPath, snapshotPath, intentsPath, denominator);
            Require(preference.Status == "ready" && preference.PairwisePreferences.Any(pair =>
                    pair.PreferredCandidateId == "master-angler-route" &&
                    pair.AlternativeCandidateId == alternate.CandidateId),
                "A strict comparison between two admitted fixture candidates was not generated.");
            Write(comparisonPreferencePath, preference);
            Write(comparisonReceiptPath, StageOneRouteReceipt(
                preference, runId, afterStateHash, 2, snapshotPath, afterSnapshotPath));
            var admission = CurrentStageOneCollectionTeacherReceiptBuilder.Build(
                inventoryPath, loweringPath, comparisonRankingPath, snapshotPath, intentsPath,
                comparisonPreferencePath, comparisonReceiptPath, afterSnapshotPath, "fixture-teacher-trajectory", runId,
                PolicyTrajectoryVersionPins.KnowledgeDictionary,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, denominator);
            Require(admission.Status == "ready" && admission.TeacherTrainingRowEligible &&
                    admission.TrainingRow!.Audit.TeacherSupervision!.PairwisePreferences.Length == 1,
                "A freshly replayed native receipt failed explicit admitted-pair supervision.");
            return admission;
        }
    }
}
