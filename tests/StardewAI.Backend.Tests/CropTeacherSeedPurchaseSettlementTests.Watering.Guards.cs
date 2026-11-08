using System.Text.Json.Nodes;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("money")]
    [InlineData("inventory")]
    [InlineData("other_crop")]
    [InlineData("growth")]
    [InlineData("unknown_crop")]
    [InlineData("stale_watering_projection")]
    [InlineData("water")]
    [InlineData("energy")]
    [InlineData("capacity")]
    [InlineData("persistent")]
    [InlineData("location")]
    public async Task WateringMaintenanceRejectsUnattributedResourceCropToolOrUnknownPersistentChanges(string changed)
    {
        using var f = new WateringFixture();
        await f.Emit(change: after =>
        {
            if (changed is "money" or "inventory" or "water" or "energy" or "capacity" or "location")
                ChangeState(after, "player", node =>
                {
                    if (changed == "money") node["money"]!["value"] = node["money"]!["value"]!.GetValue<int>() + 1;
                    if (changed == "inventory") node["inventory"]!["value"]![0]!["stack"] = 39;
                    if (changed == "water") node["watering_can_state"]!["value"]!["water_left"] = 19;
                    if (changed == "energy") node["energy"]!["value"] = node["energy"]!["value"]!.GetValue<float>() + 0.0001f;
                    if (changed == "capacity") node["watering_can_state"]!["value"]!["capacity"] = 41;
                    if (changed == "location") node["location_id"]!["value"] = "Town";
                });
            if (changed is "other_crop" or "growth" or "unknown_crop" or "stale_watering_projection")
                ChangeState(after, "farm", node =>
                {
                    if (changed == "other_crop") node["crops"]!["value"]![2]!["watered"] = true;
                    if (changed == "growth") node["crops"]!["value"]![1]!["day_of_current_phase"] = 1;
                    if (changed == "unknown_crop") node["crops"]!["value"]![1]!["unknown_native_crop_detail"] = "changed";
                    if (changed == "stale_watering_projection") node["crops"]!["value"]![1]!["needs_watering"] = true;
                });
            if (changed == "persistent") ChangeState(after, "world_progress", node =>
                node["unrelated_observed_native_progress"] = new JsonObject { ["status"] = "available", ["value"] = 1 });
        });
        Assert.Throws<InvalidDataException>(() => f.Open());
    }

    [Theory]
    [InlineData("actor")]
    [InlineData("source")]
    [InlineData("primitive")]
    [InlineData("path")]
    [InlineData("prefix")]
    public async Task WateringMaintenanceRejectsChangedNativeReceiptIdentityOrPrefix(string changed)
    {
        using var f = new WateringFixture();
        await f.Emit(receiptChange: receipt =>
        {
            if (changed == "actor") receipt.RunId = "different-run";
            if (changed == "source") receipt.SourceStateHash = "different-source";
            if (changed == "primitive") receipt.StepResults[0].PrimitiveKind = "plant_seed";
            if (changed == "path") receipt.StepResults[0].AfterSnapshotPath = f.Shared.Source.SourcePath;
            if (changed == "prefix") receipt.Success = receipt.SelectedCandidateCompleted = false;
        });
        Assert.Throws<InvalidDataException>(() => f.Open());
    }

    [Fact]
    public async Task WateringMaintenancePinsEveryRetainedStepAndIndependentlyReplaysMarkerReply()
    {
        using var f = new WateringFixture();
        await f.Emit();
        var scope = f.Open();
        var reply = new StardewAI.Core.Strategy.ReservationPortfolioLedgerService().Commit(
            f.Shared.Source.Ledger, f.After, scope.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted);
        reply.Ledger!.CurrencyReservations.Single(row => row.ReservationId == "other-money").Amount--;
        Assert.Throws<InvalidDataException>(() => scope.VerifyRebindReply(reply));
        var path = Path.Combine(f.Shared.Runtime.LiveOptions.SnapshotDir, "water-step-0.json");
        var retained = JsonNode.Parse(File.ReadAllText(path))!;
        retained["game_tick"] = retained["game_tick"]!.GetValue<long>() + 1;
        Fixture.Save(path, retained);
        Assert.Throws<InvalidDataException>(() => scope.ValidateInputsUnchanged());
    }
}
