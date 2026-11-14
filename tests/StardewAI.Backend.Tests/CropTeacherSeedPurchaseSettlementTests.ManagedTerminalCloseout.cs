using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ManagedTerminalCloseoutSelectsOriginalHomeRouteOrNativeSleepWithoutCropProgress(bool home)
    {
        using var f = new TerminalOutcomeFixture();
        var source = JsonSerializer.SerializeToNode(f.Before, JsonOptions)!.AsObject();
        if (home) source["state"]!["player"]!["location_id"]!["value"] = "FarmHouse";
        var step = InvokeLive<CropTeacherRunStep>("SelectCropTeacherTerminalCloseoutStep", source,
            f.Target with { AcceptedDepositCount = f.Target.RequiredQuantity });
        Assert.Equal(home ? CropTeacherRunStageKinds.NativeSleep : CropTeacherRunStageKinds.RouteHome, step.Stage);
        Assert.Equal("FarmHouse", step.TargetLocationId); Assert.Equal(f.Target.TotalDay, step.TotalDay);
        Assert.Null(step.ActionBatch); Assert.Equal(string.Empty, step.CommitmentId);
        Assert.Equal("crop_teacher_terminal_settlement", step.Reason);
    }

    [Theory]
    [InlineData("pending_deposit")]
    [InlineData("wrong_day")]
    [InlineData("missing_home")]
    [InlineData("unreadable_home")]
    [InlineData("missing_location")]
    public void ManagedTerminalCloseoutRejectsUnfinishedCursorOrUnknownDayHomeLocation(string fault)
    {
        using var f = new TerminalOutcomeFixture();
        var source = JsonSerializer.SerializeToNode(f.Before, JsonOptions)!.AsObject();
        var target = f.Target with { AcceptedDepositCount = f.Target.RequiredQuantity };
        if (fault == "pending_deposit") target = target with { AcceptedDepositCount = 0 };
        if (fault == "wrong_day") source["state"]!["time"]!["total_days"]!["value"] = target.TotalDay + 1;
        if (fault == "missing_home") source["state"]!["current_location"]!["home_context"]!["value"]!["home_location_id"] = "";
        if (fault == "unreadable_home") source["state"]!["current_location"]!["home_context"]!["status"] = "unavailable";
        if (fault == "missing_location") source["state"]!["player"]!["location_id"]!["value"] = "";
        var error = Assert.Throws<TargetInvocationException>(() => InvokeLive<CropTeacherRunStep>(
            "SelectCropTeacherTerminalCloseoutStep", source, target));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Fact]
    public async Task ManagedTerminalCloseoutCannotCreditADeclaredCursorWithoutAcceptedOwnerDeposits()
    {
        using var f = new TerminalOutcomeFixture();
        await using var owner = await f.Shared.Files.Open();
        var output = Path.Combine(f.Shared.Files.OwnerRoot, "unaccepted-terminal-proof");
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.BuildTerminalSettlementEvidenceAsync(
            new("unused-manifest", [], [], [], []), f.Target with { AcceptedDepositCount = f.Target.RequiredQuantity }, output));
        Assert.False(Directory.Exists(output)); Assert.Equal(0, f.Shared.Runtime.Handler.Commits);
    }
}
