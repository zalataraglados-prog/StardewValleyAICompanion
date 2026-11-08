using System.Text.Json.Nodes;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RefillControlVerifiesNativeSideEffectWithoutWateringOrDispatchCredit(bool bottomless)
    {
        using var f = new WateringFixture(water: 0, bottomless: bottomless, bowlMirrors: true);
        await f.Emit(applied: 0, failedSuffix: true, refillOnlySuffix: true);
        Assert.Throws<InvalidDataException>(() => f.Open());
        var scope = OpenRefillControl(f);
        Assert.True(scope.RefillControlVerified);
        Assert.Equal(0, scope.AppliedTileCount);
        Assert.Empty(scope.AppliedTiles);
        Assert.False(scope.CompleteBatch);
        Assert.False(scope.NativeCropLifecycleVerified);
        Assert.False(scope.FormalProductTrainingAuthorized);
        var reply = new ReservationPortfolioLedgerService().Commit(f.Shared.Source.Ledger, f.After, scope.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted);
        AssertOtherRootsUnchanged(f.Shared.Source.Ledger, scope.VerifyRebindReply(reply));
    }

    private static CropTeacherWateringMaintenance OpenRefillControl(WateringFixture f) =>
        CropTeacherWateringMaintenance.OpenRefillControl(f.Shared.Inputs.Plan.RequestPath, f.Shared.Source.SourcePath,
            f.Shared.Source.LedgerPath, new[] { Id }, Id, f.Transition, "refill-control.1");

    [Theory]
    [InlineData("no_refill")]
    [InlineData("partial_refill")]
    [InlineData("nonempty_before")]
    [InlineData("enchanted")]
    [InlineData("crop")]
    [InlineData("energy")]
    [InlineData("capacity")]
    [InlineData("money")]
    [InlineData("unknown")]
    public async Task RefillControlRejectsMissingRefillOrUnattributedPhysicalEffects(string changed)
    {
        using var f = new WateringFixture(water: changed == "nonempty_before" ? 1 : 0,
            enchanted: changed == "enchanted", bowlMirrors: true);
        await f.Emit(applied: 0, failedSuffix: true, refillOnlySuffix: changed != "no_refill", change: after =>
        {
            ChangeState(after, "player", node =>
            {
                if (changed == "partial_refill") node["watering_can_state"]!["value"]!["water_left"] = 39;
                if (changed == "capacity") node["watering_can_state"]!["value"]!["capacity"] = 41;
                if (changed == "energy") node["energy"]!["value"] = 99f;
                if (changed == "money") node["money"]!["value"] = node["money"]!["value"]!.GetValue<int>() + 1;
            });
            if (changed is "crop" or "unknown") ChangeState(after, "farm", node =>
                node["crops"]!["value"]![0]![changed == "crop" ? "watered" : "unknown_native_crop_detail"] = changed == "crop" ? JsonValue.Create(true) : JsonValue.Create("changed"));
        });
        Assert.Throws<InvalidDataException>(() => OpenRefillControl(f));
    }
}
