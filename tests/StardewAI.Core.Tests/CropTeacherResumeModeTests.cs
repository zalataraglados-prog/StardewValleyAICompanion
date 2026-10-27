using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class CropTeacherResumeModeTests
{
    [Fact]
    public void ParsesOnlyAnExplicitNativeProductResumeMode()
    {
        var ledgerPath = Path.GetTempFileName();
        var authorityPaths = Enumerable.Range(0, 4)
            .Select(_ => Path.GetTempFileName())
            .ToArray();
        try
        {
            var options = LiveTrainingOptions.Parse(
            [
                "--use-daily-plan",
                "--use-product-executor",
                "--skip-training",
                "--continue-after-crop-teacher-run",
                "--resume-crop-teacher-transition-ledger",
                ledgerPath,
                "--stage-one-collection-requirement-inventory",
                authorityPaths[0],
                "--stage-one-collection-acquisition-lowering",
                authorityPaths[1],
                "--master-angler-stage-one-window-index",
                authorityPaths[2],
                "--route-timing-calibration",
                authorityPaths[3]
            ]);

            Assert.Equal(
                Path.GetFullPath(ledgerPath),
                options.ResumeCropTeacherTransitionLedgerPath);
            Assert.True(options.ContinueAfterCropTeacherRun);
            Assert.True(options.UseStageOneCollectionTeacher);
        }
        finally
        {
            File.Delete(ledgerPath);
            foreach (var path in authorityPaths)
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void RejectsContinuationWithoutAnExplicitResumeLedger()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            LiveTrainingOptions.Parse(
            [
                "--use-daily-plan",
                "--use-product-executor",
                "--skip-training",
                "--continue-after-crop-teacher-run"
            ]));

        Assert.Contains(
            "--resume-crop-teacher-transition-ledger",
            error.Message,
            StringComparison.Ordinal);
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
