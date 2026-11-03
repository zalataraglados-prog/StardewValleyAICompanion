using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class RustyKeyRollingSupplyProposalBuilder
{
    public static RustyKeyRollingSupplyProposal BuildAfterSupportingTransition(
        AcquisitionRoutePortfolioInputs inputs, string proofPath)
    {
        var support = CurrentTeacherFrontierSupport.Read<AcquisitionRoutePortfolioSupportingTransitionInitialProof>(
            proofPath, "Rolling control supporting-transition proof");
        var expected = AcquisitionRouteSupportingTransitionReplanBuilder.Build(
            support.RequestInputs, support.SettlementProof, inputs);
        var actual = CurrentTeacherFrontierSupport.Read<AcquisitionRouteSupportingTransitionReplanAdmission>(
            support.ReplanAdmissionPath, "Rolling control fresh replan admission");
        if (JsonSerializer.Serialize(actual, JsonDefaults.Options) != JsonSerializer.Serialize(expected, JsonDefaults.Options) ||
            !expected.FreshTeacherRequestReady || !expected.PriorQueueInvalidated || !expected.AllTargetDateAxesRebuilt ||
            expected.FormalTrainingAuthorized || expected.BlockingReasons.Length != 0 || expected.RequirementSetId != "museum_collection")
            throw new InvalidDataException("Rolling control requires the independently rebuilt native support settlement and fresh replan.");
        // The existing replan request is not a preference label. Rebuild the controller's selection separately.
        var result = Build(inputs);
        if (!result.RollingStepReady || result.SelectedAcquisitionProposal is null ||
            result.SelectedCandidateId != expected.RouteOccurrenceId)
            throw new InvalidDataException("Fresh rolling acquisition selection no longer matches the verified support replan.");
        var proposal = result.SelectedAcquisitionProposal;
        proposal.PriorSupportingTransitionReplanSha256 = CurrentTeacherFrontierSupport.HashFile(support.ReplanAdmissionPath);
        var context = AcquisitionRoutePortfolioBuilder.Prepare(inputs);
        result.SelectedAcquisitionAdmission = AcquisitionRoutePortfolioBuilder.Build(context, proposal,
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(proposal), null, proposal.PriorSupportingTransitionReplanSha256);
        if (!result.SelectedAcquisitionAdmission.PortfolioAdmissionReady)
            throw new InvalidDataException("Support-rebound control acquisition is not admitted: " +
                string.Join(",", result.SelectedAcquisitionAdmission.BlockingReasons));
        return result;
    }
}
