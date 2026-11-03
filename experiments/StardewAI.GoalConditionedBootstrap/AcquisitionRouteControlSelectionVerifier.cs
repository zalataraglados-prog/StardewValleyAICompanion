using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionRouteControlSelectionVerifier
{
    internal const string ProvenanceClass = "deterministic_control.rusty_key_rolling_supply.v1";
    internal const string DispatchSource = "deterministic_control.acquisition_route_dispatch.v1";

    internal static bool UsesControl(AcquisitionRouteExecutionBindingInputs inputs) =>
        !string.IsNullOrWhiteSpace(inputs.PortfolioControlSelectionPath);

    internal static RustyKeyRollingSupplyProposal Read(AcquisitionRouteExecutionBindingInputs inputs)
    {
        if (!UsesControl(inputs) || !string.IsNullOrWhiteSpace(inputs.PortfolioTeacherPreferencePath) ||
            !string.IsNullOrWhiteSpace(inputs.PortfolioPreferenceRequestPath))
            throw new InvalidDataException("Control execution requires one control source and no Teacher source.");
        var expected = RustyKeyRollingSupplyProposalBuilder.Build(AcquisitionRouteExecutionBindingBuilder.PortfolioInputs(inputs));
        var proposal = CurrentTeacherFrontierSupport.Read<AcquisitionRoutePortfolioProposal>(inputs.PortfolioProposalPath, "Control-selected proposal");
        var admission = CurrentTeacherFrontierSupport.Read<AcquisitionRoutePortfolioAdmission>(inputs.PortfolioAdmissionPath, "Control-selected admission");
        return ValidateArtifact(inputs.PortfolioControlSelectionPath!, expected, proposal, admission,
            inputs.RouteOccurrenceId);
    }

    internal static RustyKeyRollingSupplyProposal ValidateArtifact(string path, RustyKeyRollingSupplyProposal expected,
        AcquisitionRoutePortfolioProposal proposal, AcquisitionRoutePortfolioAdmission admission, string routeOccurrenceId)
    {
        // Compare raw JSON too: get-only false claims must not hide forged true values while deserializing.
        var raw = JsonNode.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(Path.GetFullPath(path)));
        if (!JsonNode.DeepEquals(raw, JsonSerializer.SerializeToNode(expected, JsonDefaults.Options)))
            throw new InvalidDataException("Rolling control selection drifted from deterministic source rebuilding.");
        if (expected.SchemaVersion != "rusty_key_rolling_supply_proposal.v1" || !expected.RollingStepReady ||
            expected.Status != "ready_partial_acquisition_proposal_pending_atomic_commit_and_execution_binding" ||
            expected.SelectedAcquisitionProposal is null || expected.SelectedAcquisitionAdmission is null ||
            expected.CompiledQueue is not null || expected.TeacherPreferenceLabelEligible || expected.FormalTrainingAuthorized ||
            expected.BlockingReasons.Length != 0 || expected.SelectedCandidateId != routeOccurrenceId ||
            proposal.SelectedRouteOccurrenceIds is not { Length: 1 } || proposal.SelectedRouteOccurrenceIds[0] != routeOccurrenceId ||
            JsonSerializer.Serialize(expected.SelectedAcquisitionProposal, JsonDefaults.Options) != JsonSerializer.Serialize(proposal, JsonDefaults.Options) ||
            JsonSerializer.Serialize(expected.SelectedAcquisitionAdmission, JsonDefaults.Options) != JsonSerializer.Serialize(admission, JsonDefaults.Options))
            throw new InvalidDataException("Control execution requires the exact independently rebuilt rolling acquisition selection.");
        return expected;
    }

    internal static bool HasVerifiedExecutionSelection(AcquisitionRouteExecutionBinding binding) =>
        binding.PortfolioControlSelection is null
            ? binding.PortfolioTeacherPreferenceVerified
            : !binding.PortfolioTeacherPreferenceVerified && string.IsNullOrEmpty(binding.PortfolioTeacherPreferenceSha256) &&
              binding.PortfolioControlSelection.Verified && binding.PortfolioControlSelection.ProvenanceClass == ProvenanceClass &&
              IsLowerSha256(binding.PortfolioControlSelection.SourceSha256);

    private static bool IsLowerSha256(string value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
