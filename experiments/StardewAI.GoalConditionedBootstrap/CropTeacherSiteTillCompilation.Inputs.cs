namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class CropTeacherSiteTillCompilation
{
    private static T Read<T>(string path) => CurrentTeacherFrontierSupport.Read<T>(path, "selected original till input");
    private static bool SamePath(string left, string right) => CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(
        Path.GetFullPath(left), Path.GetFullPath(right));
    private static void Check(bool valid, string message)
    {
        if (!valid) throw new InvalidDataException(message);
    }
}
