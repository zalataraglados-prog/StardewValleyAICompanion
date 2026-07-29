namespace StardewAI.Core.Tests;

public sealed class QuestRuntimeValidationSourceGuardTests
{
    [Fact]
    public void SlayValidationAndFeedbackRequireExplicitSlayStep()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.QuestSlay.cs"));

        Assert.Equal(2, CountOccurrences(
            source,
            "!request.QuestSlayTargetStep"));
    }

    [Fact]
    public void SpecialOrderCollectSourceValidationRequiresExplicitSourceStep()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.QuestSpecialOrderCollect.cs"));
        var validatorStart = source.LastIndexOf(
            "private static bool ValidateSpecialOrderCollectSourceTarget(",
            StringComparison.Ordinal);
        Assert.True(validatorStart >= 0);
        var validatorEnd = source.IndexOf(
            "private static bool NativeCollectObjectiveMatches(",
            validatorStart,
            StringComparison.Ordinal);
        Assert.True(validatorEnd > validatorStart);
        var validator = source[validatorStart..validatorEnd];

        Assert.Contains(
            "!request.QuestAcquisitionSourceStep",
            validator,
            StringComparison.Ordinal);
    }

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ??
                throw new InvalidOperationException("Cannot find repository root."),
            Path.Combine(segments));
    }
}
