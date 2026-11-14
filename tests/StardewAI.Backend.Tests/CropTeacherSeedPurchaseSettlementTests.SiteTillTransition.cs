using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void SiteTillTransitionPreparationReusesSupportingInterfaceAndCopiesItsLedger()
    {
        var f = new SiteTillFiles(); using var host = f.F.Runtime;
        ICropTeacherSupportingPreparation start = f.Open();
        var original = JsonSerializer.Serialize(start.BaseLedger, JsonOptions);
        var copy = start.BaseLedger; copy.Revision++;
        Assert.Equal(original, JsonSerializer.Serialize(start.BaseLedger, JsonOptions));
        Assert.Equal(f.Queue, start.QueuePath); Assert.Equal(f.F.Source.SourcePath, start.SourcePath);
        Assert.Equal(f.SitePlan.Steps[0].CandidateId, start.CandidateId);
        start.ValidateInputsUnchanged();
        Assert.Equal(0, f.F.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData("kind")]
    [InlineData("day")]
    [InlineData("candidate")]
    [InlineData("queue")]
    [InlineData("before")]
    [InlineData("relative")]
    [InlineData("input-alias")]
    [InlineData("output-alias")]
    [InlineData("normalized-output-alias")]
    public void SiteTillTransitionRejectsChangedPreparationRolesBeforeReadingOrCreatingOutputs(string fault)
    {
        var f = new SiteTillFiles(); using var host = f.F.Runtime;
        var start = f.Open();
        var receipt = Path.Combine(f.F.Source.RuntimePath, "uncreated-till-receipt.json");
        var after = Path.Combine(f.F.Source.RuntimePath, "uncreated-till-after.json");
        var transition = new MoneyRevenueCropNativeLifecycleTransitionRequest("crop_actions", start.TotalDay,
            start.QueuePath, start.SourcePath, receipt, after, "till-transition-test",
            PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor, start.CandidateId);
        transition = fault switch
        {
            "kind" => transition with { TransitionKind = "supporting_route_to_crop" },
            "day" => transition with { TotalDay = transition.TotalDay + 1 },
            "candidate" => transition with { SelectedCandidateId = "other" },
            "queue" => transition with { QueuePath = f.Narrow },
            "before" => transition with { BeforeSnapshotPath = f.Observation },
            "relative" => transition with { ExecutionReceiptPath = "relative.json" },
            "input-alias" => transition with { ExecutionReceiptPath = f.Plan },
            "output-alias" => transition with { AfterSnapshotPath = receipt },
            "normalized-output-alias" => transition with { AfterSnapshotPath = Path.Combine(f.F.Source.RuntimePath,
                "unused", "..", Path.GetFileName(receipt)) },
            _ => throw new InvalidDataException("Unknown test case.")
        };
        var error = Assert.Throws<InvalidDataException>(() => start.VerifyTransition(transition));
        Assert.Contains(fault.Contains("alias", StringComparison.Ordinal) ? "outputs alias" : "pinned original", error.Message);
        Assert.False(File.Exists(receipt)); Assert.False(File.Exists(after));
        start.ValidateInputsUnchanged(); Assert.Equal(0, f.F.Runtime.Handler.Commits);
    }
}
