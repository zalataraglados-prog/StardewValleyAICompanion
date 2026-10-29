using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed class QueueExecutionReceiptClassifierTests
{
    [Fact]
    public void VerifiedPartialProgressRemainsAppliedWithoutCandidateSuccess()
    {
        var result = QueueExecutionReceiptClassifier.Classify(new[]
        {
            Step(candidateCompleted: false)
        });

        Assert.Equal("applied", result.Status);
        Assert.True(result.ExecutionProgressVerified);
        Assert.False(result.SelectedCandidateCompleted);
        Assert.False(result.Success);
        Assert.True(result.AllSnapshotsFresh);
    }

    [Fact]
    public void ExactCompletedCandidateSucceeds()
    {
        var result = QueueExecutionReceiptClassifier.Classify(new[]
        {
            Step(candidateCompleted: false),
            Step(candidateCompleted: true)
        });

        Assert.Equal("applied", result.Status);
        Assert.True(result.ExecutionProgressVerified);
        Assert.True(result.SelectedCandidateCompleted);
        Assert.True(result.Success);
    }

    [Fact]
    public void StaleSnapshotBlocksOtherwiseVerifiedProgress()
    {
        var result = QueueExecutionReceiptClassifier.Classify(new[]
        {
            Step(candidateCompleted: false, snapshotFresh: false)
        });

        Assert.Equal("blocked", result.Status);
        Assert.False(result.ExecutionProgressVerified);
        Assert.False(result.Success);
        Assert.False(result.AllSnapshotsFresh);
    }

    [Fact]
    public void UnverifiedStepBlocksReceipt()
    {
        var result = QueueExecutionReceiptClassifier.Classify(new[]
        {
            Step(candidateCompleted: false),
            Step(candidateCompleted: false, verificationStatus: "blocked")
        });

        Assert.Equal("blocked", result.Status);
        Assert.False(result.ExecutionProgressVerified);
        Assert.False(result.Success);
    }

    private static JsonObject Step(
        bool candidateCompleted,
        bool snapshotFresh = true,
        string verificationStatus = "verified")
    {
        return new JsonObject
        {
            ["status"] = "applied",
            ["primitive_verification_status"] = verificationStatus,
            ["after_snapshot_fresh"] = snapshotFresh,
            ["selected_queue_candidate_completed"] = candidateCompleted
        };
    }
}
