using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void NativePurchaseSettlesObservedPriceThenAugmentsOnlyOwnedDeltaAndReleasesSurplusCash()
    {
        using var f = new Fixture();
        var support = f.Open();
        Assert.Equal("support.step.1", support.MarkerRequest.SourceDecisionId);
        var marker = f.Mark(support);
        Assert.True(marker.Accepted);
        var transition = f.Transition();
        var context = f.Verify(support, marker, transition);
        Assert.Equal(75, context.Proof.ExpectedUnitPrice);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(context.ProofPath))).ToLowerInvariant(),
            context.SettlementRequest.SupportingTransitionReceiptSha256);
        Assert.Equal("teacher.crop.owner", context.SettlementRequest.RouteSourceDecisionId);
        Assert.Throws<InvalidDataException>(() => CropTeacherOwnedSeedSupply.Read(f.After, marker.Ledger!, new[] { Id }));
        var settlement = f.Settle(context, marker);
        var custody = context.VerifySettlementReply(settlement);
        var settled = custody.Ledger;
        Assert.Equal(5, settled.CurrencyReservations.Single(row => row.ReservationId == Id + ":seed-purchase:money").Amount);
        Assert.Equal(1, CropTeacherOwnedSeedSupply.Read(f.After, settled, new[] { Id }).PlayerQuantity(new[] { Id }));
        var acquisitionRequest = custody.AcquiredSeedRequest;
        Assert.Equal("owned-seed", Assert.Single(acquisitionRequest.MaterialClaims).ReservationId);
        Assert.Equal(2, acquisitionRequest.MaterialClaims[0].Quantity);
        Assert.Equal(Id + ":seed-purchase:money", Assert.Single(acquisitionRequest.ReleaseReservationIds));
        var final = custody.VerifyAcquiredSeedReply(f.Acquire(custody));
        Assert.Equal(2, CropTeacherOwnedSeedSupply.Read(f.After, final, new[] { Id }).PlayerQuantity(new[] { Id }));
        Assert.Equal(41, context.Proof.InventoryQuantityAfter);
        AssertOtherRootsUnchanged(f.Ledger, final);
    }

    [Fact]
    public void NeutralSupportRebindsOnlyOwnClaimsWithoutConsumingOrCreatingSeeds()
    {
        using var f = new Fixture();
        var support = f.Open();
        var marker = f.Mark(support);
        var context = f.Verify(support, marker, f.Transition(quantity: 0));
        Assert.Empty(context.SettlementRequest.CurrencyConsumptions);
        Assert.Equal(new[] { Id + ":seed-purchase:money", "owned-seed" }, context.SettlementRequest.RebindActiveReservationIds);
        var settlement = f.Settle(context, marker);
        var custody = context.VerifySettlementReply(settlement);
        var final = custody.Ledger;
        AssertOtherRootsUnchanged(f.Ledger, final);
        Assert.Throws<InvalidDataException>(() => custody.AcquiredSeedRequest);
    }

    [Fact]
    public void ExactBudgetCompletionDoesNotRebindCompletedCash()
    {
        using var f = new Fixture();
        var support = f.Open();
        var marker = f.Mark(support);
        var context = f.Verify(support, marker, f.Transition(price: 80));
        Assert.Equal(new[] { "owned-seed" }, context.SettlementRequest.RebindActiveReservationIds);
        var settlement = f.Settle(context, marker);
        var custody = context.VerifySettlementReply(settlement);
        Assert.Empty(custody.AcquiredSeedRequest.ReleaseReservationIds);
        var final = custody.VerifyAcquiredSeedReply(f.Acquire(custody));
        Assert.Equal(StrategyCommitmentStatuses.Completed, final.CurrencyReservations.Single(row => row.ReservationId == Id + ":seed-purchase:money").Status);
    }

    [Fact]
    public void ConsecutivePurchasesUseUniqueMarkersAndRetainOnlyRemainingBudget()
    {
        using var f = new Fixture(tiles: 3, budget: 160);
        for (var index = 0; index < 2; index++)
        {
            var support = f.Open("support.step." + (index + 1));
            var marker = f.Mark(support);
            var context = f.Verify(support, marker, f.Transition());
            var settlement = f.Settle(context, marker);
            var custody = context.VerifySettlementReply(settlement);
            var request = custody.AcquiredSeedRequest;
            Assert.Equal(index == 1, request.ReleaseReservationIds.Length == 1);
            f.Adopt(custody.VerifyAcquiredSeedReply(f.Acquire(custody)));
        }
        Assert.Equal(3, CropTeacherOwnedSeedSupply.Read(f.Before, f.Ledger, new[] { Id }).PlayerQuantity(new[] { Id }));
        Assert.Equal(2, f.Ledger.History.Count(row => row.Operation == "reservation_portfolio_supporting_transition_complete"));
    }

    [Fact]
    public void StockBundleReservesOnlyOutstandingTilesLeavingActualSurplusUnclaimed()
    {
        using var f = new Fixture();
        var support = f.Open();
        var marker = f.Mark(support);
        var context = f.Verify(support, marker, f.Transition(quantity: 30));
        var settlement = f.Settle(context, marker);
        var custody = context.VerifySettlementReply(settlement);
        var final = custody.VerifyAcquiredSeedReply(f.Acquire(custody));
        Assert.Equal(30, context.Proof.InventoryQuantityIncrease);
        Assert.Equal(2, CropTeacherOwnedSeedSupply.Read(f.After, final, new[] { Id }).PlayerQuantity(new[] { Id }));
        AssertOtherRootsUnchanged(f.Ledger, final);
    }

    [Fact]
    public void NewNativeSlotGetsFreshClaimWithoutReservingPreexistingUnownedSeeds()
    {
        using var f = new Fixture();
        var support = f.Open();
        var marker = f.Mark(support);
        var transition = f.Transition(afterChange: after =>
        {
            var player = JsonNode.Parse(after.State["player"].GetRawText())!;
            var inventory = player["inventory"]!["value"]!.AsArray();
            var added = inventory[0]!.DeepClone();
            inventory[0]!["stack"] = 40;
            added["slot_index"] = 1;
            added["stack"] = 1;
            inventory.Add(added);
            after.State["player"] = JsonSerializer.SerializeToElement(player, JsonOptions);
            var farm = JsonNode.Parse(after.State["farm"].GetRawText())!;
            var slots = farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["slots"]!.AsArray();
            var graphAdded = slots[0]!.DeepClone();
            slots[0]!["stack"] = 40;
            graphAdded["slot_index"] = 1;
            graphAdded["stack"] = 1;
            slots.Add(graphAdded);
            after.State["farm"] = JsonSerializer.SerializeToElement(farm, JsonOptions);
            after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        });
        var context = f.Verify(support, marker, transition);
        var settlement = f.Settle(context, marker);
        var custody = context.VerifySettlementReply(settlement);
        var claim = Assert.Single(custody.AcquiredSeedRequest.MaterialClaims);
        Assert.Equal(1, claim.SlotIndex);
        Assert.Equal(1, claim.Quantity);
        Assert.NotEqual("owned-seed", claim.ReservationId);
        custody.VerifyAcquiredSeedReply(f.Acquire(custody));
    }

    [Theory]
    [InlineData("shop", "Joja")]
    [InlineData("seed", "(O)472")]
    [InlineData("price", "74")]
    [InlineData("deadline", "102")]
    public void OriginalNativeSliceValidatorRejectsOutOfScopePurchase(string field, string value)
    {
        using var f = new Fixture();
        var transition = f.Transition();
        var error = Assert.Throws<InvalidDataException>(() => MoneyRevenueCropSeedAcquisitionProofBuilder.BuildSupportSlice(
            transition, field == "seed" ? value : "(O)745", field == "shop" ? value : "SeedShop",
            field == "price" ? int.Parse(value) : 80, field == "deadline" ? int.Parse(value) : 112));
        Assert.Contains(field == "deadline" ? "deadline" : field == "seed" ? "binding" : "scope", error.Message);
    }

    [Fact]
    public void MarkerAndReturnedRequestsAreDefensiveCopies()
    {
        using var f = new Fixture();
        var support = f.Open();
        support.MarkerRequest.SourceDecisionId = "teacher.crop.owner";
        support.BaseLedger.MaterialReservations[0].Quantity = 99;
        var marker = f.Mark(support);
        var context = f.Verify(support, marker, f.Transition());
        context.SettlementRequest.RouteSourceDecisionId = "other-root";
        context.Proof.OptionIds[0] = "executor.warp";
        Assert.Equal("teacher.crop.owner", context.SettlementRequest.RouteSourceDecisionId);
        context.VerifySettlementReply(f.Settle(context, marker));
    }

    [Theory]
    [InlineData("marker")]
    [InlineData("settlement")]
    [InlineData("acquisition")]
    public void EveryMutationReplyRequiresFullIndependentReplayIncludingOtherRoots(string stage)
    {
        using var f = new Fixture();
        var support = f.Open();
        var marker = f.Mark(support);
        var transition = f.Transition();
        if (stage == "marker")
        {
            marker.Ledger!.CurrencyReservations.Single(row => row.ReservationId == "other-money").Amount--;
            Assert.Contains("marker", Assert.Throws<InvalidDataException>(() => f.Verify(support, marker, transition)).Message);
            return;
        }
        var context = f.Verify(support, marker, transition);
        var settlement = f.Settle(context, marker);
        if (stage == "settlement")
        {
            settlement.Ledger!.MaterialReservations.Single(row => row.ReservationId == "other-seeds").SourceStateHash = f.After.StateHash;
            Assert.Contains("replay", Assert.Throws<InvalidDataException>(() => context.VerifySettlementReply(settlement)).Message);
            return;
        }
        var custody = context.VerifySettlementReply(settlement);
        var acquired = f.Acquire(custody);
        acquired.Ledger!.MaterialReservations.Single(row => row.ReservationId == "other-seeds").Quantity++;
        Assert.Contains("replay", Assert.Throws<InvalidDataException>(() => custody.VerifyAcquiredSeedReply(acquired)).Message);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("request")]
    [InlineData("proof")]
    public void ChangedInputsCannotBeUsedForLaterMutation(string target)
    {
        using var f = new Fixture();
        var support = f.Open();
        var marker = f.Mark(support);
        var transition = f.Transition();
        var context = f.Verify(support, marker, transition);
        if (target == "proof")
        {
            File.AppendAllText(f.ProofPath, " ");
            Assert.Contains("artifact changed", Assert.Throws<InvalidDataException>(() => context.SettlementRequest).Message);
            return;
        }
        File.AppendAllText(target == "source" ? f.SourcePath : f.RequestPath, " ");
        Assert.Contains("input changed", Assert.Throws<InvalidDataException>(() => support.ValidateInputsUnchanged()).Message);
    }

    [Fact]
    public void ReusedOrOwnerMarkerIdIsRejectedBeforeExecution()
    {
        using var f = new Fixture();
        Assert.Contains("marker", Assert.Throws<InvalidDataException>(() => f.Open("initial-crop-resources")).Message);
        Assert.Contains("marker", Assert.Throws<InvalidDataException>(() => f.Open("teacher.crop.owner")).Message);
    }

    private static void AssertOtherRootsUnchanged(StrategyCommitmentLedger before, StrategyCommitmentLedger after)
    {
        Assert.Equal(JsonSerializer.Serialize(before.MaterialReservations.Single(row => row.ReservationId == "other-seeds"), JsonOptions),
            JsonSerializer.Serialize(after.MaterialReservations.Single(row => row.ReservationId == "other-seeds"), JsonOptions));
        Assert.Equal(JsonSerializer.Serialize(before.CurrencyReservations.Single(row => row.ReservationId == "other-money"), JsonOptions),
            JsonSerializer.Serialize(after.CurrencyReservations.Single(row => row.ReservationId == "other-money"), JsonOptions));
    }
}
