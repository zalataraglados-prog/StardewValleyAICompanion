namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioCommitReceiptBuilder
{
    public static AcquisitionRoutePortfolioCommitReceipt
        BuildInitialContinuation(
            AcquisitionRoutePortfolioInitialCheckpointProof proof,
            string checkpointPath,
            AcquisitionRoutePortfolioInputs currentInputs,
            string continuationRequestPath,
            string continuationPreferencePath,
            string admissionPath,
            string committedLedgerPath,
            string? commitResultPath) => BuildContinuation(
                AcquisitionRoutePortfolioRolloutProofBuilder.VerifyInitial(
                    proof,
                    checkpointPath),
                currentInputs,
                continuationRequestPath,
                continuationPreferencePath,
                admissionPath,
                committedLedgerPath,
                commitResultPath);

    public static AcquisitionRoutePortfolioCommitReceipt BuildContinuation(
        string rolloutProofManifestPath,
        AcquisitionRoutePortfolioInputs currentInputs,
        string continuationRequestPath,
        string continuationPreferencePath,
        string admissionPath,
        string committedLedgerPath,
        string? commitResultPath) => BuildContinuation(
            AcquisitionRoutePortfolioRolloutProofBuilder.Verify(
                rolloutProofManifestPath),
            currentInputs,
            continuationRequestPath,
            continuationPreferencePath,
            admissionPath,
            committedLedgerPath,
            commitResultPath);

    internal static AcquisitionRoutePortfolioCommitReceipt BuildContinuation(
        AcquisitionRoutePortfolioVerifiedCheckpoint verifiedPrior,
        AcquisitionRoutePortfolioInputs currentInputs,
        string continuationRequestPath,
        string continuationPreferencePath,
        string admissionPath,
        string committedLedgerPath,
        string? commitResultPath)
    {
        var preferenceFullPath = Path.GetFullPath(
            continuationPreferencePath);
        var preference = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioTeacherPreference>(
            preferenceFullPath,
            "Acquisition route portfolio continuation Teacher preference");
        var expectedPreference =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder
                .BuildContinuation(
                    verifiedPrior,
                    currentInputs,
                    continuationRequestPath);
        Require(EqualJson(preference, expectedPreference) &&
                preference.TeacherPreferenceLabelEligible &&
                preference.SelectedProposal is not null &&
                preference.SelectedAdmission is not null,
            "Continuation Teacher preference is not verified.");
        var selectedProposal = preference.SelectedProposal ??
            throw new InvalidDataException(
                "Continuation Teacher proposal is missing.");
        var selectedAdmission = preference.SelectedAdmission ??
            throw new InvalidDataException(
                "Continuation Teacher admission is missing.");
        var proposal = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioProposal>(
            Path.GetFullPath(currentInputs.ProposalPath),
            "Continuation Teacher-selected portfolio proposal");
        Require(EqualJson(proposal, selectedProposal),
            "Continuation proposal drifted from Teacher selection.");
        return BuildVerifiedAdmission(
            currentInputs,
            admissionPath,
            committedLedgerPath,
            commitResultPath,
            selectedAdmission);
    }
}
