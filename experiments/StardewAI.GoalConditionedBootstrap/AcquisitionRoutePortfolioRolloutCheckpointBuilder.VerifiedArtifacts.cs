namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioRolloutCheckpointBuilder
{
    internal static AcquisitionRoutePortfolioRolloutCheckpoint
        BuildInitialFromVerifiedArtifacts(
            AcquisitionRouteExecutionBindingInputs inputs,
            string executionBindingPath,
            string executionReceiptPath,
            string afterSnapshotPath,
            string freshTerminalReceiptPath,
            string runId,
            string executorVersion,
            string settlementRequestPath,
            string settlementResultPath,
            string settledLedgerPath,
            string settlementReceiptPath)
    {
        var settlement = AcquisitionRoutePortfolioSettlementBuilder
            .BuildReceiptFromVerifiedArtifacts(
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                freshTerminalReceiptPath,
                runId,
                executorVersion,
                settlementRequestPath,
                settlementResultPath,
                settledLedgerPath);
        var preference = ReadVerifiedPreference(inputs);
        return BuildInitialCore(
            inputs,
            executionBindingPath,
            executionReceiptPath,
            afterSnapshotPath,
            freshTerminalReceiptPath,
            runId,
            executorVersion,
            settlementRequestPath,
            settlementResultPath,
            settledLedgerPath,
            settlementReceiptPath,
            settlement,
            preference);
    }

    private static AcquisitionRoutePortfolioTeacherPreference
        ReadVerifiedPreference(AcquisitionRouteExecutionBindingInputs inputs)
    {
        var requestPath = Path.GetFullPath(
            inputs.PortfolioPreferenceRequestPath);
        var preferencePath = Path.GetFullPath(
            inputs.PortfolioTeacherPreferencePath);
        var request = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioTeacherPreferenceRequest>(
            requestPath,
            "Acquisition route portfolio Teacher preference request");
        var preference = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioTeacherPreference>(
            preferencePath,
            "Acquisition route portfolio Teacher preference");

        Require(
            preference.SchemaVersion ==
                "acquisition_route_portfolio_teacher_preference.v1" &&
            preference.RequestId == request.RequestId &&
            preference.GoalId == request.GoalId &&
            preference.SnapshotStateHash == request.SnapshotStateHash &&
            preference.ExpectedLedgerRevision ==
                request.ExpectedLedgerRevision &&
            preference.SelectionDisposition ==
                AcquisitionRoutePortfolioSelectionDisposition
                    .UniqueStrictPareto &&
            preference.CandidateDenominatorComplete &&
            preference.TeacherPreferenceLabelEligible &&
            preference.SelectedProposal is not null &&
            preference.SelectedAdmission is not null &&
            !preference.UsesLearnerRankOrScore &&
            !preference.FormalTrainingAuthorized &&
            preference.BlockingReasons.Length == 0 &&
            string.IsNullOrEmpty(
                preference.PriorSupportingTransitionReplanSha256),
            "Persisted initial portfolio Teacher preference is not verified.");

        Require(
            preference.PreferenceRequestSha256 == Hash(requestPath) &&
            preference.RequirementInventorySha256 == Hash(
                inputs.RequirementInventoryPath) &&
            preference.OpportunityCostSha256 == Hash(
                inputs.TargetDateOpportunityCostPath) &&
            preference.StrategyLedgerSha256 == Hash(
                inputs.StrategyLedgerPath) &&
            preference.SnapshotSha256 == Hash(inputs.BeforeSnapshotPath),
            "Persisted initial portfolio Teacher preference hashes drifted.");
        return preference;
    }

    private static string Hash(string path) =>
        CurrentTeacherFrontierSupport.HashFile(Path.GetFullPath(path));
}
