using StardewAI.LiveTrainingLoop;

public sealed partial class LiveTrainingOptions
{
    public string PreparedCropTerminalProductionPath { get; set; } = string.Empty;
    public string PreparedCropTerminalProductionSha256 { get; set; } = string.Empty;

    public CropTeacherTerminalProductionReference? PreparedCropTerminalProduction =>
        string.IsNullOrWhiteSpace(PreparedCropTerminalProductionPath) ? null :
            new(PreparedCropTerminalProductionPath, PreparedCropTerminalProductionSha256);

    private void ValidatePreparedCropTerminalProduction()
    {
        if (string.IsNullOrWhiteSpace(PreparedCropTerminalProductionPath) &&
            string.IsNullOrWhiteSpace(PreparedCropTerminalProductionSha256)) return;
        if (!UseManagedCropTeacher || !UseJointDailyCalendar || !Path.IsPathFullyQualified(PreparedCropTerminalProductionPath) ||
            PreparedCropTerminalProductionSha256.Length != 64 || PreparedCropTerminalProductionSha256.Any(value =>
                value is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new ArgumentException("Prepared crop terminal production requires managed joint mode and an absolute controller-pinned path/SHA256 pair.");
    }
}
