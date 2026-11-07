using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedProofCanOnlyResumeByExpectedHashAndExactNativeRebuild(bool alterProof)
    {
        using var f = new Fixture();
        var support = f.Open();
        var marker = f.Mark(support);
        var transition = f.Transition();
        var original = f.Verify(support, marker, transition);
        var request = original.SettlementRequest;
        var bytes = File.ReadAllBytes(f.ProofPath);
        if (alterProof)
        {
            File.AppendAllText(f.ProofPath, " ");
            Assert.Contains("retained proof", Assert.Throws<InvalidDataException>(() => support.ResumeTransition(
                marker, transition, f.ProofPath, request.SupportingTransitionReceiptSha256)).Message);
            return;
        }
        var resumed = support.ResumeTransition(marker, transition, f.ProofPath, request.SupportingTransitionReceiptSha256);
        Assert.Equal(bytes, File.ReadAllBytes(f.ProofPath));
        Assert.Equal(JsonSerializer.Serialize(request, JsonOptions), JsonSerializer.Serialize(resumed.SettlementRequest, JsonOptions));
        var custody = resumed.VerifySettlementReply(f.Settle(resumed, marker));
        custody.VerifyAcquiredSeedReply(f.Acquire(custody));
    }

    [Fact]
    public void FirstPurchaseCanConsumeAllCashWithoutAnyExistingOwnSeedClaim()
    {
        using var f = new Fixture(tiles: 1, ownedSeeds: 0);
        var support = f.Open();
        var marker = f.Mark(support);
        var context = f.Verify(support, marker, f.Transition(price: 80));
        Assert.Empty(context.SettlementRequest.RebindActiveReservationIds);
        var custody = context.VerifySettlementReply(f.Settle(context, marker));
        var claim = Assert.Single(custody.AcquiredSeedRequest.MaterialClaims);
        Assert.Equal(1, claim.Quantity);
        Assert.NotEqual("owned-seed", claim.ReservationId);
        custody.VerifyAcquiredSeedReply(f.Acquire(custody));
    }

    [Theory]
    [InlineData("layout")]
    [InlineData("seed")]
    [InlineData("budget")]
    [InlineData("missing_intent")]
    public void UnsupportedOriginalRunScopeIsRejectedBeforeMarkerCommit(string target)
    {
        using var f = new Fixture();
        var request = JsonSerializer.Deserialize<MoneyRevenueCropNativeProductionRunPlanRequest>(File.ReadAllText(f.RequestPath), JsonOptions)!;
        if (target == "layout") request.LayoutIntents[0] = request.LayoutIntents[0] with { Tiles = request.LayoutIntents[0].Tiles.Take(1).ToArray() };
        if (target == "seed") request.SeedAcquisitionIntents[0] = request.SeedAcquisitionIntents[0] with { SeedQualifiedItemId = "(O)472" };
        if (target == "budget") request.SeedAcquisitionIntents[0] = request.SeedAcquisitionIntents[0] with { MaxUnitPrice = 81 };
        if (target == "missing_intent") request.SeedAcquisitionIntents = Array.Empty<MoneyRevenueCropNativeSeedAcquisitionIntent>();
        Fixture.Save(f.RequestPath, request);
        var error = Assert.Throws<InvalidDataException>(() => f.Open());
        Assert.Contains(target is "layout" or "missing_intent" ? "scope" : "funded shortfall", error.Message);
    }

    [Fact]
    public void NativeRouteUsesOriginalBoundaryVerifierWithMatchingGraphLocation()
    {
        using var f = new Fixture();
        var support = f.Open();
        var marker = f.Mark(support);
        var transition = f.Transition(quantity: 0, supportingOptionId: "executor.traverse_connector", afterChange: after =>
        {
            var farm = JsonNode.Parse(after.State["farm"].GetRawText())!;
            farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["location_id"] = "Town";
            after.State["farm"] = JsonSerializer.SerializeToElement(farm, JsonOptions);
            after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        });
        var context = f.Verify(support, marker, transition);
        var custody = context.VerifySettlementReply(f.Settle(context, marker));
        Assert.Equal(f.After.StateHash, custody.Ledger.SourceStateHash);
        AssertOtherRootsUnchanged(f.Ledger, custody.Ledger);
    }

    [Fact]
    public void VerifiedCustodyCannotBeChangedThroughMutableRepliesOrCopies()
    {
        using var f = new Fixture();
        var support = f.Open();
        var marker = f.Mark(support);
        var context = f.Verify(support, marker, f.Transition());
        var settlement = f.Settle(context, marker);
        var custody = context.VerifySettlementReply(settlement);
        settlement.Ledger!.MaterialReservations[0].Quantity = 999;
        custody.Ledger.CurrencyReservations[0].Amount = 999;
        custody.AcquiredSeedRequest.MaterialClaims[0].Quantity = 999;
        custody.VerifyAcquiredSeedReply(f.Acquire(custody));
    }

    [Fact]
    public void ProofOutputNeverOverwritesAnExistingArtifact()
    {
        using var f = new Fixture();
        var support = f.Open();
        var marker = f.Mark(support);
        var transition = f.Transition();
        File.WriteAllText(f.ProofPath, "retained evidence");
        Assert.Throws<IOException>(() => f.Verify(support, marker, transition));
        Assert.Equal("retained evidence", File.ReadAllText(f.ProofPath));
    }
}
