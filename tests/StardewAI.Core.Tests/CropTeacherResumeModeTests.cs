using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class CropTeacherResumeModeTests
{
    [Fact]
    public void ParsesOnlyAnExplicitNativeProductResumeMode()
    {
        var ledgerPath = Path.GetTempFileName();
        try
        {
            var options = LiveTrainingOptions.Parse(
            [
                "--use-daily-plan",
                "--use-product-executor",
                "--skip-training",
                "--resume-crop-teacher-transition-ledger",
                ledgerPath
            ]);

            Assert.Equal(
                Path.GetFullPath(ledgerPath),
                options.ResumeCropTeacherTransitionLedgerPath);
        }
        finally
        {
            File.Delete(ledgerPath);
        }
    }

    [Fact]
    public void RejectsResumeWithoutTheNativeProductBoundary()
    {
        var ledgerPath = Path.GetTempFileName();
        try
        {
            var error = Assert.Throws<ArgumentException>(() =>
                LiveTrainingOptions.Parse(
                [
                    "--use-daily-plan",
                    "--skip-training",
                    "--resume-crop-teacher-transition-ledger",
                    ledgerPath
                ]));

            Assert.Contains(
                "--use-product-executor",
                error.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(ledgerPath);
        }
    }

    [Fact]
    public void ResumeLoaderRebuildsTheBoundAuthoritativeRunPlan()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "StardewAI.LiveTrainingLoop",
            "CropTeacherResumeLoader.cs"));

        Assert.Contains(
            "CropTeacherTransitionLedger.OpenExisting",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "MoneyRevenueCropNativeProductionRunPlanBuilder.Build",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "JsonNode.DeepEquals",
            source,
            StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "tools")) &&
                Directory.Exists(Path.Combine(current.FullName, "tests")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
