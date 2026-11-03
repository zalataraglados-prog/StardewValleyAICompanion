namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyRollingSupplyProposalBuilder
{
    private static void BuildAcquisitionStep(
        RustyKeyRollingSupplyProposal result, RustyKeyAcquisitionPortfolioBuilder.RustyKeySourceScope source,
        AcquisitionRoutePortfolioBuilder.AcquisitionRoutePortfolioBuildContext context,
        List<RustyKeyRollingCandidateEvaluation> evaluations)
    {
        if (context.Opportunity.TargetTotalDay != source.SourceTotalDay)
        {
            Block(result, new[] { "rusty_key_rolling_acquisition_target_date_is_not_current_day" });
            return;
        }
        var missing = source.Museum.Groups.Where(group => !source.Progress.Requirements[group.RequirementId].Completed)
            .ToDictionary(group => group.RequirementId, StringComparer.Ordinal);
        AcquisitionRoutePortfolioAdmission? selectedAdmission = null;
        AcquisitionRoutePortfolioProposal? selectedProposal = null;
        foreach (var route in context.Opportunity.Routes.OrderBy(route => route.RouteOccurrenceId, StringComparer.Ordinal))
        {
            var requirement = AcquisitionRoutePortfolioBuilder.RequirementRoute(route);
            if (requirement.RequirementSetId != "museum_collection" || !missing.ContainsKey(requirement.RequirementId))
                continue;
            var proposal = new AcquisitionRoutePortfolioProposal
            {
                ProposalId = "rusty-key-rolling:" + route.RouteOccurrenceId + ":" + result.SnapshotStateHash +
                    ":r" + result.StrategyLedgerRevision,
                GoalId = result.GoalId, SnapshotStateHash = result.SnapshotStateHash,
                ExpectedLedgerRevision = result.StrategyLedgerRevision,
                ScopedRequirements = new[] { new AcquisitionRoutePortfolioRequirementScope("museum_collection", requirement.RequirementId) },
                SelectedRouteOccurrenceIds = new[] { route.RouteOccurrenceId }
            };
            var admission = AcquisitionRoutePortfolioBuilder.Build(context, proposal,
                AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(proposal));
            evaluations.Add(new(route.RouteOccurrenceId, requirement.RequirementId, "acquisition_route",
                admission.PortfolioAdmissionReady, admission.BlockingReasons));
            if (!admission.PortfolioAdmissionReady || admission.AggregateCostVector is null) continue;
            // This is an explicit controller policy, not an independently justified preference label.
            if (selectedAdmission is null ||
                admission.AggregateCostVector.GuaranteedElapsedGameMinutes < selectedAdmission.AggregateCostVector!.GuaranteedElapsedGameMinutes ||
                admission.AggregateCostVector.GuaranteedElapsedGameMinutes == selectedAdmission.AggregateCostVector!.GuaranteedElapsedGameMinutes &&
                admission.AggregateCostVector.RequiredEnergy < selectedAdmission.AggregateCostVector.RequiredEnergy)
            {
                selectedAdmission = admission;
                selectedProposal = proposal;
            }
        }
        if (selectedAdmission is null || selectedProposal is null)
        {
            Block(result, new[] { "rusty_key_no_safe_current_rolling_supply_step" });
            return;
        }
        result.SelectedCandidateId = selectedProposal.SelectedRouteOccurrenceIds.Single();
        result.SelectedAcquisitionProposal = selectedProposal;
        result.SelectedAcquisitionAdmission = selectedAdmission;
        var selectedRequirement = selectedProposal.ScopedRequirements.Single().RequirementId;
        result.CompleteMuseumItemDenominator = result.CompleteMuseumItemDenominator
            .Select(item => item with { SelectedForAcquisition = item.RequirementId == selectedRequirement }).ToArray();
        result.RollingStepReady = true;
        result.Status = "ready_partial_acquisition_proposal_pending_atomic_commit_and_execution_binding";
    }
}
