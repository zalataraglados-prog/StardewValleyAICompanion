namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData("proof")]
    [InlineData("receipt")]
    [InlineData("after")]
    [InlineData("request")]
    public void NativeArtifactsRemainPhysicallyBoundAfterContextCreation(string artifact)
    {
        using var fixture = new Fixture(nativeEvidence: true);
        var scope = fixture.Open();
        var marker = scope.VerifyPreparationReply(fixture.Prepare(scope));
        var transition = fixture.NativeTransition(marker);
        var context = fixture.VerifyNative(marker, transition);
        File.AppendAllText(artifact switch
        {
            "proof" => fixture.NativeProofPath,
            "receipt" => transition.ExecutionReceiptPath,
            "request" => fixture.RequestPath,
            _ => transition.AfterSnapshotPath
        }, " ");
        Assert.Throws<InvalidDataException>(() => context.SettlementRequest);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeQueueAndRetainedProofRequireTheExpectedPhysicalHash(bool resume)
    {
        using var fixture = new Fixture(nativeEvidence: true);
        var scope = fixture.Open();
        var marker = scope.VerifyPreparationReply(fixture.Prepare(scope));
        var transition = fixture.NativeTransition(marker);
        var hash = marker.ValidateQueue(transition.QueuePath);
        if (resume)
        {
            fixture.VerifyNative(marker, transition);
            Assert.Throws<InvalidDataException>(() => marker.ResumeTransition(transition, fixture.NativeProofPath, hash, new string('a', 64)));
        }
        else Assert.Throws<InvalidDataException>(() => marker.VerifyTransition(transition, fixture.NativeProofPath, new string('a', 64)));
    }
}
