using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalAtomicPreparationConservesOwnershipAcrossAllDestinationSlots(bool multipleDestinations)
    {
        using var fixture = new Fixture(multipleDestinations);
        var original = JsonSerializer.Serialize(fixture.Ledger);
        var scope = fixture.Open();
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Ledger));
        Assert.Equal(10, scope.PreparationRequest.MaterialTransferPreparation!.MaterialRelocations.Sum(row => row.Quantity));
        var result = fixture.Prepare(scope);
        Assert.True(result.Accepted, string.Join(",", result.Errors));
        var marker = scope.VerifyPreparationReply(result);
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Ledger));
        Assert.Equal(fixture.Ledger.Revision + 1, marker.Ledger.Revision);
        var partitions = marker.Ledger.MaterialReservations.Where(row => row.Purpose == "crop_seed_supply:" + Id).ToArray();
        Assert.Equal(multipleDestinations ? 2 : 1, partitions.Length);
        Assert.Equal(10, partitions.Sum(row => row.Quantity));
        Assert.All(partitions, row =>
        {
            Assert.Equal(Owner, row.SourceDecisionId);
            Assert.Equal("chest:Farm:4,5", row.NodeId);
            Assert.Equal(5, row.SlotIndex);
        });
        foreach (var other in fixture.Ledger.MaterialReservations.Where(row => row.SourceDecisionId != Owner))
            Assert.Equal(JsonSerializer.Serialize(other), JsonSerializer.Serialize(marker.Ledger.MaterialReservations.Single(row => row.ReservationId == other.ReservationId)));
        var projection = marker.Ledger.PreparedMaterialTransfer!.ReservationProjection;
        Assert.Equal(0, projection.ReservationTransitions.Single(row => row.ReservationId == "other-seeds").DestinationSlotIndex);
        Assert.Equal(1, projection.ReservationTransitions.Single(row => row.ReservationId == "other-stone").DestinationSlotIndex);
        Assert.DoesNotContain(projection.ReservationTransitions, row => row.ReservationId == "outside");
        var queue = fixture.Compile(marker);
        Assert.True(queue.Status == "pending", string.Join(",", queue.CompilerDiagnostics.Concat(queue.Items
            .SelectMany(row => row.BlockingReasons.Concat(row.PreconditionResults.Select(result => result.Message))))));
        var item = Assert.Single(queue.Items);
        Assert.True(CropSeedMaterialTransferBinding.IsBound(item, marker.Ledger));
        Assert.DoesNotContain(item.NormalizedCommand.Parameters, row => row.Name.StartsWith("acquisition_", StringComparison.Ordinal));
        var readiness = new ActionQueueDispatchReadinessService().Evaluate(queue, item, marker.Ledger, fixture.Before.StateHash);
        Assert.True(readiness.Ready, string.Join(",", readiness.BlockingReasons));
    }

    [Theory]
    [InlineData("other-seeds")]
    [InlineData("outside")]
    [InlineData("missing")]
    public void OtherOwnersAndUnknownClaimsCannotBecomeCropSeedTransfers(string reservationId)
    {
        using var fixture = new Fixture();
        Assert.Throws<InvalidDataException>(() => fixture.Open(reservationId));
    }

    [Theory]
    [InlineData("locked")]
    [InlineData("fridge")]
    [InlineData("remote")]
    [InlineData("full")]
    public void OriginalNativeTransferConstraintsRejectBeforeAnyMutation(string blocker)
    {
        using var fixture = new Fixture(change: graph =>
        {
            switch (blocker)
            {
                case "locked": graph.AccessPoints[0].LockedByOtherPlayer = true; break;
                case "fridge": graph.AccessPoints[0].SpecialChestType = "MiniFridge"; break;
                case "remote": graph.InventoryNodes[1].LocationId = graph.AccessPoints[0].LocationId = "FarmHouse"; break;
                case "full": graph.InventoryNodes[0].Capacity = 1; break;
            }
        });
        var original = JsonSerializer.Serialize(fixture.Ledger);
        Assert.Throws<InvalidDataException>(() => fixture.Open());
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Ledger));
    }

    [Theory]
    [InlineData("missing-preparation")]
    [InlineData("foreign-claim")]
    [InlineData("wrong-partition")]
    [InlineData("reply-count")]
    public void FullOriginalPreparationReplayRejectsCorruptedReplies(string mutation)
    {
        using var fixture = new Fixture();
        var scope = fixture.Open();
        var result = fixture.Prepare(scope);
        switch (mutation)
        {
            case "missing-preparation": result.Ledger!.PreparedMaterialTransfer = null; break;
            case "foreign-claim": result.Ledger!.MaterialReservations.Single(row => row.ReservationId == "other-seeds").SourceDecisionId = Owner; break;
            case "wrong-partition": result.Ledger!.MaterialReservations.Single(row => row.ReservationId == "owned-seeds").Quantity++; break;
            case "reply-count": result.MaterialClaimCount++; break;
        }
        Assert.Throws<InvalidDataException>(() => scope.VerifyPreparationReply(result));
    }

    [Fact]
    public void VerifiedMarkerAndPreparationRequestsDoNotShareMutableCallerObjects()
    {
        using var fixture = new Fixture();
        var scope = fixture.Open();
        scope.PreparationRequest.MaterialTransferPreparation!.Intent.Quantity = 999;
        Assert.Equal(10, scope.PreparationRequest.MaterialTransferPreparation!.Intent.Quantity);
        var result = fixture.Prepare(scope);
        var marker = scope.VerifyPreparationReply(result);
        result.Ledger!.PreparedMaterialTransfer!.Intent.Quantity = 999;
        marker.Ledger.MaterialReservations[0].Quantity = 999;
        Assert.Equal(10, marker.Ledger.PreparedMaterialTransfer!.Intent.Quantity);
        Assert.True(CropSeedMaterialTransferBinding.IsBound(Assert.Single(fixture.Compile(marker).Items), marker.Ledger));
    }

    [Fact]
    public void ChangedPhysicalInputCannotProduceAnActionFromAnOldMarker()
    {
        using var fixture = new Fixture();
        var scope = fixture.Open();
        var marker = scope.VerifyPreparationReply(fixture.Prepare(scope));
        File.AppendAllText(fixture.RequestPath, " ");
        Assert.Throws<InvalidDataException>(() => marker.BuildAction(4, 6));
    }
}
