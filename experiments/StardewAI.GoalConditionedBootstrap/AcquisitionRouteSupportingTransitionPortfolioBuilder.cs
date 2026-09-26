using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static class AcquisitionRouteSupportingTransitionPortfolioBuilder
{
    public static AcquisitionRoutePortfolioTeacherPreference
        BuildTeacherPreference(
            AcquisitionRouteSupportingTransitionRequestInputs priorInputs,
            AcquisitionRouteSupportingTransitionSettlementProof proof,
            AcquisitionRoutePortfolioInputs freshInputs,
            string replanAdmissionPath,
            string preferenceRequestPath)
    {
        var verified = VerifyReplan(
            priorInputs,
            proof,
            freshInputs,
            replanAdmissionPath,
            preferenceRequestPath);
        var preference = AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .BuildAfterSupportingTransition(
                freshInputs,
                preferenceRequestPath,
                verified.ReplanSha256);
        Require(preference.PriorSupportingTransitionReplanSha256 ==
                verified.ReplanSha256 &&
                (preference.SelectedProposal is null ||
                 preference.SelectedProposal
                     .PriorSupportingTransitionReplanSha256 ==
                 verified.ReplanSha256) &&
                (preference.SelectedAdmission is null ||
                 preference.SelectedAdmission
                     .PriorSupportingTransitionReplanSha256 ==
                 verified.ReplanSha256),
            "Acquisition support-replan lineage did not reach Teacher output.");
        return preference;
    }

    public static AcquisitionRoutePortfolioCommitReceipt BuildCommitReceipt(
        AcquisitionRouteSupportingTransitionRequestInputs priorInputs,
        AcquisitionRouteSupportingTransitionSettlementProof proof,
        AcquisitionRoutePortfolioInputs freshInputs,
        string replanAdmissionPath,
        string preferenceRequestPath,
        string preferencePath,
        string admissionPath,
        string committedLedgerPath,
        string? commitResultPath)
    {
        var expectedPreference = BuildTeacherPreference(
            priorInputs,
            proof,
            freshInputs,
            replanAdmissionPath,
            preferenceRequestPath);
        var preference = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioTeacherPreference>(
            Path.GetFullPath(preferencePath),
            "Acquisition support-replan Teacher preference");
        Require(EqualJson(preference, expectedPreference) &&
                preference.TeacherPreferenceLabelEligible &&
                preference.SelectedProposal is not null &&
                preference.SelectedAdmission is not null,
            "Acquisition support-replan Teacher preference is not verified.");
        var proposal = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioProposal>(
            Path.GetFullPath(freshInputs.ProposalPath),
            "Acquisition support-replan Teacher-selected proposal");
        Require(EqualJson(proposal, preference.SelectedProposal),
            "Acquisition support-replan proposal drifted from Teacher selection.");
        var admission = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioAdmission>(
            Path.GetFullPath(admissionPath),
            "Acquisition support-replan Teacher-selected admission");
        Require(EqualJson(admission, preference.SelectedAdmission),
            "Acquisition support-replan admission drifted from Teacher selection.");

        var replanSha256 = CurrentTeacherFrontierSupport.HashFile(
            Path.GetFullPath(replanAdmissionPath));
        var receipt = AcquisitionRoutePortfolioCommitReceiptBuilder
            .BuildAfterSupportingTransition(
                freshInputs,
                replanSha256,
                admissionPath,
                committedLedgerPath,
                commitResultPath);
        Require(receipt.PriorSupportingTransitionReplanSha256 ==
                replanSha256,
            "Acquisition support-replan lineage did not reach commit receipt.");
        return receipt;
    }

    private static VerifiedReplan VerifyReplan(
        AcquisitionRouteSupportingTransitionRequestInputs priorInputs,
        AcquisitionRouteSupportingTransitionSettlementProof proof,
        AcquisitionRoutePortfolioInputs freshInputs,
        string replanAdmissionPath,
        string preferenceRequestPath)
    {
        var admissionPath = Path.GetFullPath(replanAdmissionPath);
        var requestPath = Path.GetFullPath(preferenceRequestPath);
        var expected = AcquisitionRouteSupportingTransitionReplanBuilder.Build(
            priorInputs,
            proof,
            freshInputs);
        var admission = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteSupportingTransitionReplanAdmission>(
            admissionPath,
            "Acquisition support-replan admission");
        Require(EqualJson(admission, expected) &&
                admission.FreshTeacherRequestReady &&
                admission.NextTeacherPreferenceRequest is not null,
            "Acquisition support-replan admission is not verified.");
        var request = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioTeacherPreferenceRequest>(
            requestPath,
            "Acquisition support-replan Teacher request");
        Require(EqualJson(request, admission.NextTeacherPreferenceRequest),
            "Acquisition support-replan Teacher request drifted.");
        return new VerifiedReplan(
            CurrentTeacherFrontierSupport.HashFile(admissionPath));
    }

    private static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Options),
        JsonSerializer.Serialize(right, JsonDefaults.Options),
        StringComparison.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record VerifiedReplan(string ReplanSha256);
}
