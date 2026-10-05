namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteExecutionBindingBuilder
{
    internal static AcquisitionRouteExecutionBinding ReadVerifiedArtifact(
        AcquisitionRouteExecutionBindingInputs inputs,
        string executionBindingPath)
    {
        var bindingPath = Path.GetFullPath(executionBindingPath);
        var binding = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteExecutionBinding>(
            bindingPath,
            "Acquisition route execution binding");

        Require(
            binding.SchemaVersion ==
                "acquisition_route_execution_binding.v1" &&
            binding.Status == "ready_for_exact_route_dispatch" &&
            binding.RouteOccurrenceId == inputs.RouteOccurrenceId &&
            binding.SelectedCandidateId ==
                SelectedCandidateId(inputs.RouteOccurrenceId) &&
            binding.SelectedFromCompleteParetoFrontier &&
            binding.QueueOptionsBoundToRoute &&
            binding.PortfolioReservationCommitVerified &&
            binding.PortfolioTeacherPreferenceVerified &&
            binding.DispatchBindingReady &&
            !binding.FormalTrainingAuthorized &&
            binding.BlockingReasons.Length == 0,
            "Persisted route execution binding is not dispatch-ready.");

        Require(
            binding.OpportunityCostSha256 == Hash(
                inputs.TargetDateOpportunityCostPath) &&
            binding.PortfolioCommitReceiptSha256 == Hash(
                inputs.PortfolioCommitReceiptPath) &&
            binding.PortfolioTeacherPreferenceSha256 == Hash(
                inputs.PortfolioTeacherPreferencePath) &&
            binding.CommittedStrategyLedgerSha256 == Hash(
                inputs.CommittedStrategyLedgerPath) &&
            binding.AcquisitionLoweringSha256 == Hash(
                inputs.AcquisitionLoweringPath) &&
            binding.BeforeSnapshotSha256 == Hash(
                inputs.BeforeSnapshotPath) &&
            binding.ActionQueueSha256 == Hash(inputs.ActionQueuePath),
            "Persisted route execution binding source hashes drifted.");

        return binding;
    }

    private static string Hash(string path) =>
        CurrentTeacherFrontierSupport.HashFile(Path.GetFullPath(path));
}
