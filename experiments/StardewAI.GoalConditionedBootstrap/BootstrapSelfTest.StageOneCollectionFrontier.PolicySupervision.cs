using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

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
            var ledgerPath = Path.Combine(root, "pairwise-strategy-ledger.json");
            var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(snapshotPath, "Teacher ledger snapshot");
            var ledger = new StrategyCommitmentLedger
            {
                LedgerId = "fixture-teacher-ledger", SaveId = before.SaveId.Value!, PlayerId = before.PlayerId.Value!,
                SourceStateHash = before.StateHash
            };
            Write(ledgerPath, ledger);
            Write(comparisonRankingPath, ranking);
            var preference = CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                inventoryPath, loweringPath, comparisonRankingPath, snapshotPath, intentsPath, denominator, ledgerPath);
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
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, denominator, ledgerPath);
            Require(admission.Status == "ready" && admission.TeacherTrainingRowEligible &&
                    admission.TrainingRow!.Audit.TeacherSupervision!.PairwisePreferences.Length == 1,
                "A freshly replayed native receipt failed explicit admitted-pair supervision.");
            var trainingRow = admission.TrainingRow ?? throw new InvalidDataException("Missing fixture Teacher row.");
            var sources = trainingRow.Audit.TeacherSupervision?.Sources
                ?? throw new InvalidDataException("Missing fixture Teacher sources.");
            Require(preference.StrategyLedgerSha256 == HashFile(ledgerPath) &&
                    sources.StrategyLedgerPath == ledgerPath,
                "Teacher source ledger was not carried into independent replay.");
            var gate = new PolicyPairwiseSupervisionAdmission(new CollectionTeacherSupervisionReplayVerifier(denominator));
            Require(gate.Validate(trainingRow, out _) is null, "Valid ledger-bound Teacher replay failed.");
            var original = File.ReadAllBytes(ledgerPath);
            ledger.Revision++;
            Write(ledgerPath, ledger);
            Require(gate.Validate(trainingRow, out _) is not null,
                "Edited retained ledger evidence bypassed independent replay.");
            File.WriteAllBytes(ledgerPath, original);
            sources.StrategyLedgerPath = string.Empty;
            Require(gate.Validate(trainingRow, out _) is not null,
                "Omitting retained ledger evidence bypassed independent replay.");
            sources.StrategyLedgerPath = ledgerPath;
            return admission;
        }
    }
}
