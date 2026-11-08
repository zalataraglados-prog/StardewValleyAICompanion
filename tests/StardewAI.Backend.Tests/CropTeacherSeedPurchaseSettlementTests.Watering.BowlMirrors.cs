namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(20, 100f, 2)]
    [InlineData(0, 100f, 2)]
    [InlineData(1, 100f, 1)]
    [InlineData(20, 3f, 1)]
    public async Task WateringMaintenanceAccountsPassivePetBowlResourceEchoWithoutIgnoringBowlState(int water, float energy, int count)
    {
        using var f = new WateringFixture(water: water, bowlMirrors: true, energy: energy);
        await f.Emit(applied: count, executableTileCount: count);
        var scope = f.Open();
        Assert.Equal(count, scope.AppliedTileCount);
        var reply = new StardewAI.Core.Strategy.ReservationPortfolioLedgerService().Commit(
            f.Shared.Source.Ledger, f.After, scope.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted);
        scope.VerifyRebindReply(reply);
    }

    [Theory]
    [InlineData("watered")]
    [InlineData("friendship")]
    [InlineData("water_mirror")]
    [InlineData("status")]
    [InlineData("unknown")]
    [InlineData("mail")]
    public async Task WateringMaintenanceRejectsPetBowlMutationOrStaleResourceEcho(string changed)
    {
        using var f = new WateringFixture(water: 1, bowlMirrors: true);
        await f.Emit(applied: 1, executableTileCount: 1, change: after => ChangeState(after, "farm", farm =>
        {
            var row = farm["pet_bowls"]!["value"]![0]!;
            if (changed == "watered") row["watered"] = true;
            if (changed == "friendship") row["friendship_before_next_day"] = 11;
            if (changed == "water_mirror") row["watering_can_water_left"] = 1;
            if (changed == "status") row["action_status"] = "ready";
            if (changed == "unknown") row["unknown_native_bowl_detail"] = "changed";
            if (changed == "mail") row["pet_love_mail_before"] = true;
        }));
        Assert.Throws<InvalidDataException>(() => f.Open());
    }

    [Fact]
    public async Task WateringMaintenanceRejectsAnAlreadyInconsistentBeforePetBowlMirror()
    {
        using var f = new WateringFixture(bowlMirrors: true, inconsistentBeforeMirror: true);
        await f.Emit();
        var error = Assert.Throws<InvalidDataException>(() => f.Open());
        Assert.Contains("before resource projection is inconsistent", error.Message);
    }

    [Fact]
    public async Task WateringMaintenancePreservesNativeFloatEnergyAndDoubleProjectedPetBowlCost()
    {
        using var f = new WateringFixture(bowlMirrors: true, farmingLevel: 9);
        await f.Emit();
        var scope = f.Open();
        Assert.Equal(2, scope.AppliedTileCount);
    }
}
