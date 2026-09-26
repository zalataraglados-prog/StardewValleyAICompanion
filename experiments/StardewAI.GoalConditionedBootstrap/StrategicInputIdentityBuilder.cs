namespace StardewAI.GoalConditionedBootstrap;

internal static class StrategicInputIdentityBuilder
{
    private const string SchemaVersion = "strategic_input_identity.v1";

    public static string Build(
        StrategicPolicySelectionRequest request,
        AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .AcquisitionRoutePortfolioTeacherScoringSet set)
    {
        var preference = set.Preference;
        var denominator = set.Candidates
            .OrderBy(candidate => candidate.Proposal.ProposalId,
                StringComparer.Ordinal)
            .Select(candidate => new
            {
                proposal_id = candidate.Proposal.ProposalId,
                proposal_sha256 = candidate.ProposalSha256,
                admission_sha256 =
                    AcquisitionRoutePortfolioTeacherPreferenceBuilder
                        .ArtifactSha256(candidate.Admission)
            })
            .ToArray();
        var modelRelevant = preference.SelectionDisposition ==
            AcquisitionRoutePortfolioSelectionDisposition
                .IncomparableFrontier;
        var identity = new
        {
            schema_version = SchemaVersion,
            goal_id = preference.GoalId,
            snapshot_state_hash = preference.SnapshotStateHash,
            strategy_ledger_revision = preference.ExpectedLedgerRevision,
            selection_disposition = preference.SelectionDisposition,
            teacher_preference_sha256 =
                AcquisitionRoutePortfolioTeacherPreferenceBuilder
                    .ArtifactSha256(preference),
            preference_request_sha256 = preference.PreferenceRequestSha256,
            requirement_inventory_sha256 =
                preference.RequirementInventorySha256,
            opportunity_cost_sha256 = preference.OpportunityCostSha256,
            strategy_ledger_sha256 = preference.StrategyLedgerSha256,
            snapshot_sha256 = preference.SnapshotSha256,
            prior_supporting_transition_replan_sha256 =
                preference.PriorSupportingTransitionReplanSha256,
            candidate_denominator_sha256 =
                AcquisitionRoutePortfolioTeacherPreferenceBuilder
                    .ArtifactSha256(denominator),
            prior_rollout_proof = ArtifactIdentity(
                request.PriorRolloutProofManifestPath,
                relevant: !string.IsNullOrWhiteSpace(
                    request.PriorRolloutProofManifestPath)),
            model_checkpoint = ArtifactIdentity(
                request.CheckpointPath,
                modelRelevant),
            model_corpus_manifest = ArtifactIdentity(
                request.CorpusManifestPath,
                modelRelevant)
        };
        return AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .ArtifactSha256(identity);
    }

    private static StrategicArtifactIdentity ArtifactIdentity(
        string path,
        bool relevant)
    {
        if (!relevant)
            return new StrategicArtifactIdentity("not_applicable", string.Empty);
        if (string.IsNullOrWhiteSpace(path))
            return new StrategicArtifactIdentity("missing_path", string.Empty);
        try
        {
            var fullPath = Path.GetFullPath(path);
            return File.Exists(fullPath)
                ? new StrategicArtifactIdentity(
                    "available",
                    CurrentTeacherFrontierSupport.HashFile(fullPath))
                : new StrategicArtifactIdentity("missing_file", string.Empty);
        }
        catch (Exception exception) when (
            exception is ArgumentException or IOException or
                UnauthorizedAccessException)
        {
            return new StrategicArtifactIdentity(
                "unreadable:" + exception.GetType().Name,
                string.Empty);
        }
    }

    private sealed record StrategicArtifactIdentity(
        string Status,
        string Sha256);
}
