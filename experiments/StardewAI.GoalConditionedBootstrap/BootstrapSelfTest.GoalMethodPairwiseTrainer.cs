using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyGoalMethodPairwiseTrainer(
        string readyCorpusManifestPath,
        string blockedCorpusManifestPath,
        string outputRoot)
    {
        var checkpointPath = Path.Combine(
            outputRoot,
            "goal-method-pairwise-checkpoint.json");
        var result = new GoalMethodPairwiseTrainer().Train(
            readyCorpusManifestPath,
            checkpointPath);
        var checkpoint = result.Checkpoint;
        Require(File.Exists(result.CheckpointPath) &&
                CurrentTeacherFrontierSupport.HashFile(
                    result.CheckpointPath) == result.CheckpointSha256 &&
                checkpoint.SchemaVersion ==
                    GoalMethodPairwiseVersionPins.CheckpointSchema &&
                checkpoint.ModelKind ==
                    GoalMethodPairwiseVersionPins.ModelKind &&
                checkpoint.TeacherLabelSource ==
                    "explicit_teacher_pairwise_preferences" &&
                !checkpoint.UsesCandidateSelectedFlag &&
                !checkpoint.FormalProductTrainingAuthorized &&
                checkpoint.Training.TrainRows == 3 &&
                checkpoint.Training.TrainPairs == 2 &&
                checkpoint.Training.TrainPairAccuracy == 1 &&
                checkpoint.Training.ValidationRows == 3 &&
                checkpoint.Training.ValidationPairs == 2 &&
                checkpoint.Training.ValidationPairAccuracy == 1 &&
                checkpoint.Training.TestRows == 3 &&
                checkpoint.Training.TestPairs == 2 &&
                checkpoint.Training.TestPairAccuracy == 1 &&
                checkpoint.Training.FeatureCount ==
                    checkpoint.Model.FeatureNames.Length &&
                checkpoint.Model.FeatureNames.Any(name =>
                    name == "candidate.numeric:elapsed_game_minutes") &&
                checkpoint.Model.FeatureNames.Any(name =>
                    name.StartsWith(
                        "candidate.route=",
                        StringComparison.Ordinal)) &&
                checkpoint.Model.FeatureNames.All(name =>
                    !name.Contains("save_id", StringComparison.Ordinal) &&
                    !name.Contains("proposal_id", StringComparison.Ordinal) &&
                    !name.Contains("learner", StringComparison.Ordinal) &&
                    !name.Contains("dominated_by", StringComparison.Ordinal)),
            "Dedicated goal-method pairwise checkpoint drifted.");
        var loaded = new GoalMethodPairwiseCheckpointStore().Load(
            checkpointPath);
        Require(loaded.CheckpointId == checkpoint.CheckpointId,
            "Saved goal-method checkpoint did not round-trip.");

        var readyManifest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioSupervisionCorpusManifest>(
            readyCorpusManifestPath,
            "Goal-method scoring self-test corpus");
        var trainPath = readyManifest.Partitions.Single(partition =>
            partition.Partition ==
                PolicyDatasetPartitions.Train).Path;
        var comparisonRow = File.ReadLines(trainPath)
            .Select(line => JsonSerializer.Deserialize<
                AcquisitionRoutePortfolioSupervisionCorpusRow>(
                line,
                JsonDefaults.Compact)!)
            .Single(row => row.SupervisionRow.Payload.TeacherPreference
                .PairwisePreferences.Length == 2);
        var scoring = new GoalMethodPairwiseRanker()
            .RankVerifiedCorpusRow(
                checkpointPath,
                readyCorpusManifestPath,
                comparisonRow.SupervisionRow.RowId);
        Require(scoring.Status ==
                    "ready_verified_goal_method_shadow_ranking" &&
                scoring.CheckpointId == checkpoint.CheckpointId &&
                scoring.CandidateDenominatorVerified &&
                scoring.CandidateScores.Length == 3 &&
                scoring.CandidateScores.Count(candidate =>
                    candidate.TeacherSelected) == 1 &&
                scoring.CandidateScores.Single(candidate =>
                    candidate.TeacherSelected).Rank == 1 &&
                scoring.ModelTopProposalId ==
                    scoring.TeacherSelectedProposalId &&
                scoring.ModelAgreesWithTeacher &&
                !scoring.PortfolioCommitAuthorized &&
                !scoring.FormalProductTrainingAuthorized,
            "Read-only goal-method checkpoint scoring drifted.");

        var blockedCorpusRejected = false;
        try
        {
            new GoalMethodPairwiseTrainer().Train(
                blockedCorpusManifestPath,
                Path.Combine(outputRoot, "blocked-goal-method-checkpoint.json"));
        }
        catch (InvalidDataException)
        {
            blockedCorpusRejected = true;
        }
        Require(blockedCorpusRejected,
            "Partition-incomplete corpus entered the goal-method trainer.");

        var forgedManifest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioSupervisionCorpusManifest>(
            readyCorpusManifestPath,
            "Goal-method trainer source-digest negative fixture");
        forgedManifest.Sources[0].ProofReceiptSha256 = new string('0', 64);
        var forgedManifestPath = Path.Combine(
            outputRoot,
            "forged-source-goal-method-corpus-manifest.json");
        Write(forgedManifestPath, forgedManifest);
        var forgedSourceRejected = false;
        try
        {
            new GoalMethodPairwiseTrainer().Train(
                forgedManifestPath,
                Path.Combine(outputRoot, "forged-goal-method-checkpoint.json"));
        }
        catch (InvalidDataException)
        {
            forgedSourceRejected = true;
        }
        Require(forgedSourceRejected,
            "Forged source digest entered the goal-method trainer.");

        checkpoint.UsesCandidateSelectedFlag = true;
        var tamperedCheckpointPath = Path.Combine(
            outputRoot,
            "tampered-goal-method-checkpoint.json");
        File.WriteAllText(
            tamperedCheckpointPath,
            JsonSerializer.Serialize(checkpoint, JsonDefaults.Options));
        var tamperedCheckpointRejected = false;
        try
        {
            new GoalMethodPairwiseCheckpointStore().Load(
                tamperedCheckpointPath);
        }
        catch (InvalidDataException)
        {
            tamperedCheckpointRejected = true;
        }
        Require(tamperedCheckpointRejected,
            "Checkpoint claiming candidate.Selected labels was accepted.");

        loaded.CheckpointId = "goal-method-forged";
        var forgedCheckpointIdPath = Path.Combine(
            outputRoot,
            "forged-id-goal-method-checkpoint.json");
        File.WriteAllText(
            forgedCheckpointIdPath,
            JsonSerializer.Serialize(loaded, JsonDefaults.Options));
        var forgedCheckpointIdRejected = false;
        try
        {
            new GoalMethodPairwiseCheckpointStore().Load(
                forgedCheckpointIdPath);
        }
        catch (InvalidDataException)
        {
            forgedCheckpointIdRejected = true;
        }
        Require(forgedCheckpointIdRejected,
            "Checkpoint with a forged identity was accepted.");
    }
}
