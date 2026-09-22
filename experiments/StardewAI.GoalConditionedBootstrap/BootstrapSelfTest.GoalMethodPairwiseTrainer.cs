using System.Text.Json;
using System.Text.Json.Nodes;
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

        var trainSource = readyManifest.Sources.Single(source =>
            source.DatasetSha256 == comparisonRow.SourceDatasetSha256);
        var rolloutManifest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofManifest>(
            trainSource.ProofManifestPath,
            "Goal-method live shadow self-test rollout");
        var initialInputs = AcquisitionRoutePortfolioInputAdapter
            .FromExecutionBinding(
                rolloutManifest.InitialCheckpointProof.ExecutionInputs);
        var liveInitial = new GoalMethodPairwiseRanker().RankLiveShadow(
            checkpointPath,
            readyCorpusManifestPath,
            initialInputs,
            rolloutManifest.InitialCheckpointProof.ExecutionInputs
                .PortfolioPreferenceRequestPath);
        Require(liveInitial.Status ==
                    "ready_teacher_authoritative_live_shadow_scoring" &&
                liveInitial.TransitionIndex == 1 &&
                liveInitial.CandidateDenominatorVerified &&
                liveInitial.CandidateScores.Length == 3 &&
                liveInitial.ShadowSelectedProposal is not null &&
                liveInitial.ShadowSelectedAdmission is not null &&
                liveInitial.SelectionAuthority ==
                    "deterministic_unique_strict_pareto_teacher" &&
                liveInitial.ShadowSelectedProposal.ProposalId ==
                    liveInitial.TeacherSelectedProposalId &&
                !liveInitial.PortfolioCommitAuthorized &&
                !liveInitial.FormalProductTrainingAuthorized,
            "Fresh initial goal-method shadow scoring drifted.");

        var firstContinuation = rolloutManifest.ContinuationTransitions[0];
        var continuationInputs = AcquisitionRoutePortfolioInputAdapter
            .FromExecutionBinding(firstContinuation.ExecutionInputs);
        var initialProofManifestPath = Path.Combine(
            Path.GetDirectoryName(rolloutManifest.InitialCheckpointPath)!,
            "initial-rollout-proof-manifest.json");
        var liveContinuation = new GoalMethodPairwiseRanker().RankLiveShadow(
            checkpointPath,
            readyCorpusManifestPath,
            continuationInputs,
            firstContinuation.ContinuationRequestPath,
            initialProofManifestPath);
        Require(liveContinuation.Status ==
                    "ready_teacher_authoritative_live_shadow_scoring" &&
                liveContinuation.TransitionIndex == 2 &&
                liveContinuation.CandidateDenominatorVerified &&
                liveContinuation.ShadowSelectedProposal is not null &&
                liveContinuation.ShadowSelectedAdmission is not null &&
                liveContinuation.SelectionAuthority ==
                    "deterministic_unique_strict_pareto_teacher" &&
                !liveContinuation.PortfolioCommitAuthorized &&
                !liveContinuation.FormalProductTrainingAuthorized,
            "Fresh continuation goal-method shadow scoring drifted.");

        VerifyIncomparableGoalMethodLiveShadow(
            checkpointPath,
            readyCorpusManifestPath,
            rolloutManifest,
            outputRoot);

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

    private static void VerifyIncomparableGoalMethodLiveShadow(
        string checkpointPath,
        string readyCorpusManifestPath,
        AcquisitionRoutePortfolioRolloutProofManifest rolloutManifest,
        string outputRoot)
    {
        var fixtureRoot = Path.Combine(
            outputRoot,
            "incomparable-goal-method-live-shadow");
        Directory.CreateDirectory(fixtureRoot);
        var template = rolloutManifest.InitialCheckpointProof.ExecutionInputs;
        var snapshotRoot = JsonNode.Parse(
            File.ReadAllText(template.BeforeSnapshotPath))!.AsObject();
        const string stateHash = "incomparable-goal-method-live-state";
        const string saveId = "fixture-save-incomparable-shadow";
        snapshotRoot["state_hash"] = stateHash;
        snapshotRoot["save_id"]!["value"] = saveId;
        snapshotRoot["state"]!["identity"]!["save_id"]!["value"] =
            saveId;
        snapshotRoot["in_game_time"]!["value"] = 900;
        snapshotRoot["state"]!["time"]!["time"]!["value"] = 900;
        snapshotRoot["state"]!["world_progress"]!
            ["game_state_query_calendar_state"]!["value"]!
            ["time_of_day"] = 900;
        var collisionGrid = snapshotRoot["state"]!["locations"]!
            ["collision_grid"]!["value"]!.AsObject();
        collisionGrid["width"] = 100;
        collisionGrid["height"] = 100;
        var routeLocations = snapshotRoot["state"]!["locations"]!
            ["social_route_date_evidence"]!["value"]!["locations"]!
            .AsArray();
        var farmRouteLocation = routeLocations.Single(location =>
            location!["location_id"]!.GetValue<string>() == "Farm")!
            .AsObject();
        farmRouteLocation["map_width"] = 100;
        farmRouteLocation["map_height"] = 100;
        farmRouteLocation["static_walkable_tile_count"] = 10_000;
        var walkableRows = new JsonArray();
        for (var y = 0; y < 100; y++)
        {
            walkableRows.Add(new JsonObject
            {
                ["y"] = y,
                ["start_x"] = 0,
                ["end_x"] = 99
            });
        }
        farmRouteLocation["static_walkable_tile_ranges"] = walkableRows;
        var crops = snapshotRoot["state"]!["farm"]!["crops"]!["value"]!
            .AsArray();
        for (var index = 0; index < crops.Count; index++)
        {
            crops[index]!["tile_x"] = 96 + index;
            crops[index]!["tile_y"] = 96;
        }
        var snapshotPath = Path.Combine(fixtureRoot, "before-snapshot.json");
        File.WriteAllText(
            snapshotPath,
            snapshotRoot.ToJsonString(JsonDefaults.Options));
        var ledgerPath = Path.Combine(fixtureRoot, "strategy-ledger.json");
        WriteEmptyStrategyLedger(ledgerPath, stateHash, saveId);
        var proposalPath = Path.Combine(fixtureRoot, "proposal.json");
        var inputs = BuildTargetDatePortfolioInputs(
            template,
            snapshotPath,
            ledgerPath,
            proposalPath,
            fixtureRoot,
            targetTotalDay: 0);
        var originalRequest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioTeacherPreferenceRequest>(
            template.PortfolioPreferenceRequestPath,
            "Incomparable live shadow preference template");
        originalRequest.RequestId = "self-test-incomparable-shadow";
        originalRequest.SnapshotStateHash = stateHash;
        originalRequest.ExpectedLedgerRevision = 0;
        var requestPath = Path.Combine(fixtureRoot, "preference-request.json");
        Write(requestPath, originalRequest);

        var shadow = new GoalMethodPairwiseRanker().RankLiveShadow(
            checkpointPath,
            readyCorpusManifestPath,
            inputs,
            requestPath);
        Require(shadow.Status ==
                    "ready_checkpoint_ranked_incomparable_frontier_shadow" &&
                shadow.SelectionAuthority ==
                    "goal_method_checkpoint_shadow_only" &&
                shadow.TeacherPreferenceStatus ==
                    "blocked_portfolio_teacher_preference" &&
                shadow.TeacherSelectedProposalId.Length == 0 &&
                shadow.ModelAgreesWithTeacher is null &&
                shadow.CandidateDenominatorVerified &&
                shadow.CandidateScores.Length >= 2 &&
                shadow.CandidateScores.Count(candidate =>
                    candidate.OnDeterministicParetoFrontier) >= 2 &&
                shadow.ShadowSelectedProposal is not null &&
                shadow.ShadowSelectedAdmission is not null &&
                shadow.CandidateScores.Any(candidate =>
                    candidate.OnDeterministicParetoFrontier &&
                    candidate.ProposalId ==
                        shadow.ShadowSelectedProposal.ProposalId) &&
                !shadow.PortfolioCommitAuthorized &&
                !shadow.FormalProductTrainingAuthorized,
            "Incomparable Pareto-frontier shadow scoring drifted: status=" +
            shadow.Status + "; teacher_status=" +
            shadow.TeacherPreferenceStatus + "; frontier=" +
            shadow.CandidateScores.Count(candidate =>
                candidate.OnDeterministicParetoFrontier) + "; blocks=" +
            string.Join(",", shadow.BlockingReasons));
    }

    internal static void RunGoalMethodIncomparableLiveShadow(
        string checkpointPath,
        string readyCorpusManifestPath,
        string rolloutProofManifestPath,
        string outputRoot)
    {
        var rolloutManifest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofManifest>(
            rolloutProofManifestPath,
            "Incomparable live shadow self-test rollout");
        VerifyIncomparableGoalMethodLiveShadow(
            checkpointPath,
            readyCorpusManifestPath,
            rolloutManifest,
            Path.GetFullPath(outputRoot));
    }
}
