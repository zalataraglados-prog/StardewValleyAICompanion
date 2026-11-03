using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static class RustyKeyRollingStepPreparationBuilder
{
    public static RustyKeyRollingStepPreparation Build(AcquisitionRoutePortfolioInputs inputs,
        string controlSelectionPath, string rankingPath = "", string supportingTransitionProofPath = "")
    {
        var control = string.IsNullOrWhiteSpace(supportingTransitionProofPath)
            ? RustyKeyRollingSupplyProposalBuilder.Build(inputs)
            : RustyKeyRollingSupplyProposalBuilder.BuildAfterSupportingTransition(inputs, supportingTransitionProofPath);
        AcquisitionRouteControlSelectionVerifier.VerifyArtifactIdentity(controlSelectionPath, control);
        var result = new RustyKeyRollingStepPreparation
        {
            GoalId = control.GoalId, SnapshotStateHash = control.SnapshotStateHash,
            StrategyLedgerRevision = control.StrategyLedgerRevision,
            ControlSelectionSha256 = CurrentTeacherFrontierSupport.HashFile(controlSelectionPath)
        };
        if (control.ThresholdSatisfied)
        {
            result.Kind = "native_key_handoff";
            return result;
        }
        if (!control.RollingStepReady)
            return Block(result, control.BlockingReasons);
        if (control.CompiledQueue is not null)
        {
            result.Kind = "held_donation";
            result.CompiledQueue = control.CompiledQueue;
            result.StepReady = true;
            return result;
        }
        if (control.SelectedAcquisitionProposal is null || control.SelectedAcquisitionAdmission?.AtomicCommitRequest is null)
            return Block(result, new[] { "rusty_key_selected_acquisition_request_missing" });
        result.RouteOccurrenceId = control.SelectedCandidateId;
        if (string.IsNullOrWhiteSpace(rankingPath))
            return Block(result, new[] { "rusty_key_current_execution_ranking_required_before_commit" });

        var context = AcquisitionRoutePortfolioBuilder.Prepare(inputs);
        var route = context.Opportunity.Routes.Single(value => value.RouteOccurrenceId == control.SelectedCandidateId);
        var requirement = AcquisitionRoutePortfolioBuilder.RequirementRoute(route);
        var lowering = CurrentTeacherFrontierSupport.Read<AcquisitionRouteOptionLoweringReport>(
            inputs.AcquisitionLoweringPath, "Rolling acquisition lowering");
        var lowered = AcquisitionRouteDispatchCompilationBuilder.LoweredRoute(lowering, requirement);
        var ranking = CurrentTeacherFrontierSupport.Read<AvailabilityAwarePolicyPredictionEnvelope>(rankingPath, "Rolling current execution ranking");
        result.RankingSha256 = CurrentTeacherFrontierSupport.HashFile(rankingPath);
        if (ranking.SchemaVersion != "availability_policy_prediction.v1" || ranking.Availability.StateHash != control.SnapshotStateHash)
            return Block(result, new[] { "rusty_key_execution_ranking_identity_mismatch" });
        var terminal = AcquisitionRouteDispatchCompilationBuilder.CompileCurrentEndpoint(control.GoalId, requirement,
            lowered, route, context.Snapshot, context.LedgerState.Ledger, ranking, result.RankingSha256,
            control.SelectedAcquisitionAdmission.AtomicCommitRequest.PortfolioId, control.StrategyLedgerRevision, usesControl: true);
        if (terminal.DispatchReady && terminal.TerminalReceiptEligible)
        {
            result.Kind = "terminal_acquisition";
            result.AtomicCommitRequest = control.SelectedAcquisitionAdmission.AtomicCommitRequest;
            result.StepReady = true;
            return result;
        }
        // A compiled connector/interaction is preparation, never terminal acquisition evidence.
        if (!(terminal.DispatchReady && terminal.SelectedRouteOptionRole == "supporting_transition") &&
            !terminal.BlockingReasons.SequenceEqual(new[] { "no_current_exact_source_bound_endpoint_candidate" }))
            return Block(result, terminal.BlockingReasons);
        result.SupportInputs = SupportInputs(inputs, rankingPath, control.SelectedCandidateId,
            Math.Min(control.SourceTotalDay + 1, RustyKeyAcquisitionPortfolioBuilder.StageOneDeadlineExclusive - 1));
        result.SupportRequest = AcquisitionRouteSupportingTransitionRequestBuilder.Build(result.SupportInputs);
        if (!result.SupportRequest.SupportRequestReady || result.SupportRequest.AtomicCommitRequest is null)
            return Block(result, result.SupportRequest.BlockingReasons);
        result.Kind = "supporting_transition";
        result.AtomicCommitRequest = result.SupportRequest.AtomicCommitRequest;
        result.StepReady = true;
        return result;
    }

    private static RustyKeyRollingStepPreparation Block(RustyKeyRollingStepPreparation result, IEnumerable<string> reasons)
    {
        result.BlockingReasons = reasons.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        return result;
    }

    private static AcquisitionRouteSupportingTransitionRequestInputs SupportInputs(AcquisitionRoutePortfolioInputs value,
        string rankingPath, string routeId, int deadline) => new()
    {
        RequirementInventoryPath = value.RequirementInventoryPath, AcquisitionLoweringPath = value.AcquisitionLoweringPath,
        MasterAnglerWindowsPath = value.MasterAnglerWindowsPath, CalendarResolutionPath = value.CalendarResolutionPath,
        TargetDateCalendarPath = value.TargetDateCalendarPath, TargetDateUnlockPath = value.TargetDateUnlockPath,
        TargetDateFestivalPath = value.TargetDateFestivalPath, TargetDateLocationPath = value.TargetDateLocationPath,
        TargetDateFacilityPath = value.TargetDateFacilityPath, TargetDateResourcePath = value.TargetDateResourcePath,
        TargetDateCurrencyPath = value.TargetDateCurrencyPath, TargetDateReservationPath = value.TargetDateReservationPath,
        TargetDateProcessingPath = value.TargetDateProcessingPath, StrategyLedgerPath = value.StrategyLedgerPath,
        SnapshotPath = value.SnapshotPath, RouteTimingCalibrationPath = value.RouteTimingCalibrationPath,
        RankingPath = rankingPath, RouteOccurrenceId = routeId, SupportDeadlineTotalDay = deadline, UseControlSelection = true
    };
}
