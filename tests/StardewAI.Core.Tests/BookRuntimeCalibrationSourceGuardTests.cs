namespace StardewAI.Core.Tests;

public sealed class BookRuntimeCalibrationSourceGuardTests
{
    [Fact]
    public void RuntimeValidationAdmitsTheNativeBookExecutor()
    {
        var validation = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Shipping.Utilities.cs");
        var dispatch = ReadRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs");

        Assert.Contains(
            "request.OptionId != \"executor.read_book\"",
            validation,
            StringComparison.Ordinal);
        Assert.Contains(
            "pending.Request.OptionId == \"executor.read_book\"",
            dispatch,
            StringComparison.Ordinal);
        Assert.Contains(
            "ExecuteReadBook(pending.Request)",
            dispatch,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeCoversEveryVanillaBookBranchAndWellRead()
    {
        var smoke = ReadRepositoryFile(
            "scripts",
            "Invoke-RuntimeBookReadingSmoke.ps1");

        foreach (var branch in new[]
        {
            "skill_book",
            "power_book_repeated_skill",
            "power_book_repeated_all_skills",
            "purple_book",
            "power_book_first_read",
            "queen_of_sauce_first_read"
        })
        {
            Assert.Contains(
                "expected_branch = \"" + branch + "\"",
                smoke,
                StringComparison.Ordinal);
        }
        Assert.Contains(
            "fixture_branch = \"power_book_first_read_well_read\"",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "option_id = \"executor.read_book\"",
            smoke,
            StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", smoke);
        Assert.Contains(
            "$env:SDL_AUDIODRIVER = \"dummy\"",
            smoke,
            StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(
            AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(
                Path.Combine(
                    directory.FullName,
                    "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }

        return File.ReadAllText(
            Path.Combine(
                directory?.FullName ??
                    throw new InvalidOperationException(
                        "Cannot find repository root."),
                Path.Combine(segments)));
    }
}
