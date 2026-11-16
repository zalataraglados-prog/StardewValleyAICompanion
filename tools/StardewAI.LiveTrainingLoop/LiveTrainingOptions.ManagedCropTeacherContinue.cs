public sealed partial class LiveTrainingOptions
{
    public void ValidateManagedCropTeacherContinueMode()
    {
        if (!UseManagedCropTeacher)
            throw new ArgumentException("Managed production continuation requires its explicit managed crop mode.");
        ValidateManagedCropTeacherResumeMode();
        if (string.IsNullOrWhiteSpace(ResumeManagedCropOwnerDirectory) || ManagedCropReloadProcessId <= 0 ||
            string.IsNullOrWhiteSpace(ManagedCropReloadExecutablePath) || string.IsNullOrWhiteSpace(ManagedCropReloadLogPath) ||
            !string.IsNullOrWhiteSpace(ManagedCropReloadObservationPath))
            throw new ArgumentException("Managed production continuation requires the original owner and a CURRENT live native process/executable/log, not a retained observation alone.");
    }
}
