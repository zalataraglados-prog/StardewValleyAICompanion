using System.Security.Cryptography;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingTests
{
    [Theory]
    [InlineData("receipt")]
    [InlineData("step")]
    [InlineData("proof")]
    [InlineData("source")]
    public void CreatedConsumptionContextRetainsAllPhysicalEvidence(string artifact)
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var transition = f.NativeTransition(marker);
        var context = f.Verify(marker, transition);
        File.AppendAllText(artifact switch
        {
            "receipt" => transition.ExecutionReceiptPath,
            "step" => context.Proof.ArtifactHashes.Keys.Single(path => Path.GetFileName(path) == "step-0.json"),
            "proof" => f.ProofPath,
            _ => f.SourcePath
        }, " ");
        Assert.Throws<InvalidDataException>(() => context.SettlementRequest);
    }

    [Fact]
    public void ReadOnlyResumeRebuildsProofAndOriginalSettlementReplyRemainsFullyBound()
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var transition = f.NativeTransition(marker, 3, failedSuffix: true);
        var context = f.Verify(marker, transition);
        var bytes = File.ReadAllBytes(f.ProofPath);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var restored = marker.ResumeTransition(transition, f.ProofPath, marker.ValidateQueue(f.QueuePath), hash);
        Assert.True(bytes.SequenceEqual(File.ReadAllBytes(f.ProofPath)));
        Assert.Equal(context.SettlementRequest.SupportingTransitionReceiptSha256, restored.SettlementRequest.SupportingTransitionReceiptSha256);
        var result = f.Repository.SettleReservationPortfolioSupportingTransition(f.After, restored.SettlementRequest);
        restored.VerifySettlementReply(result);
        result.Ledger!.CropPlantingCommitments[0].BaseGrowDays++;
        Assert.Throws<InvalidDataException>(() => restored.VerifySettlementReply(result));
        Assert.Throws<InvalidDataException>(() => marker.ResumeTransition(transition, f.ProofPath,
            marker.ValidateQueue(f.QueuePath), new string('a', 64)));
    }

    [Fact]
    public void WholeClaimCompletionDoesNotCancelCropOrOtherPlayerSeedClaims()
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var transition = f.NativeTransition(marker, 1, firstSlot: true);
        var context = f.Verify(marker, transition);
        var ledger = context.VerifySettlementReply(f.Repository.SettleReservationPortfolioSupportingTransition(f.After, context.SettlementRequest));
        Assert.Equal(9, ledger.MaterialReservations.Single(row => row.ReservationId == f.OwnedSlotOne).Quantity);
        Assert.Equal("active", ledger.CropPlantingCommitments.Single().Status);
        Assert.Equal("active", ledger.MaterialReservations.Single(row => row.ReservationId == f.OwnedSlotOne).Status);
        Assert.Equal(9, CropTeacherOwnedSeedSupply.Read(f.After, ledger, new[] { f.Id }).Commitments.Single().PlayerQuantity);
    }
}
