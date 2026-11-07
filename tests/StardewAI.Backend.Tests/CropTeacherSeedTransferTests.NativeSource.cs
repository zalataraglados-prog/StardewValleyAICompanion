using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ACorrectAfterEchoCannotRepairAnInconsistentSource(bool seedEcho)
    {
        using var fixture = new Fixture(nativeEvidence: true);
        var domain = seedEcho ? "player" : "farm";
        var fields = JsonNode.Parse(fixture.Before.State[domain].GetRawText())!;
        if (seedEcho) fields["seed_inventory"]!["value"]![0]!["stack"] = 42;
        else fields["chests"]!["value"]!["nonempty_shove_access_point_count"] = 0;
        fixture.Before.State[domain] = JsonSerializer.SerializeToElement(fields, JsonOptions);
        fixture.Before.StateHash = SnapshotHash.ComputeStateHash(fixture.Before.State);
        var anchored = new ReservationPortfolioLedgerService().Commit(fixture.Ledger, fixture.Before,
            new ReservationPortfolioCommitRequest
            {
                StateHash = fixture.Before.StateHash, ExpectedLedgerRevision = fixture.Ledger.Revision,
                PortfolioId = "fixture.source.echo", SourceDecisionId = "fixture.source.echo", GoalId = "earn_money"
            }, Timestamp);
        Assert.True(anchored.Accepted);
        fixture.AdoptLedger(anchored.Ledger!);
        Fixture.Save(fixture.SourcePath, fixture.Before);
        var scope = fixture.Open();
        var marker = scope.VerifyPreparationReply(fixture.Prepare(scope));
        var transition = fixture.NativeTransition(marker);
        Assert.Throws<InvalidDataException>(() => fixture.VerifyNative(marker, transition));
        Assert.False(File.Exists(fixture.NativeProofPath));
    }

    [Fact]
    public void ASeparateOriginalStepAfterArtifactIsRetainedAndBound()
    {
        using var fixture = new Fixture(nativeEvidence: true);
        var scope = fixture.Open();
        var marker = scope.VerifyPreparationReply(fixture.Prepare(scope));
        string stepPath = string.Empty;
        var transition = fixture.NativeTransition(marker, receiptChange: receipt =>
        {
            stepPath = Path.Combine(Path.GetDirectoryName(receipt.StepResults[0].AfterSnapshotPath)!, "separate-step-after.json");
            Fixture.Save(stepPath, fixture.After);
            receipt.StepResults[0].AfterSnapshotPath = stepPath;
        });
        var context = fixture.VerifyNative(marker, transition);
        Assert.Equal(5, context.Proof.ArtifactHashes.Count);
        Assert.Contains(stepPath, context.Proof.ArtifactHashes.Keys);
        File.AppendAllText(stepPath, " ");
        Assert.Throws<InvalidDataException>(() => context.SettlementRequest);
    }

    [Fact]
    public void StepFileAliasesFollowActualFilesystemCaseRules()
    {
        using var fixture = new Fixture(nativeEvidence: true);
        var scope = fixture.Open();
        var marker = scope.VerifyPreparationReply(fixture.Prepare(scope));
        var transition = fixture.NativeTransition(marker, receiptChange: receipt =>
        {
            var path = Path.Combine(Path.GetDirectoryName(receipt.StepResults[0].AfterSnapshotPath)!, "NATIVE-AFTER.JSON");
            if (!OperatingSystem.IsWindows()) Fixture.Save(path, fixture.Before);
            receipt.StepResults[0].AfterSnapshotPath = path;
        });
        if (OperatingSystem.IsWindows()) Assert.True(fixture.VerifyNative(marker, transition).Proof.NativeOutcomeVerified);
        else Assert.Throws<InvalidDataException>(() => fixture.VerifyNative(marker, transition));
    }

    [Theory]
    [InlineData("blocked-diagnostic")]
    [InlineData("missing-state")]
    [InlineData("actor")]
    public void CompilationDiagnosticsCannotBeStrippedByOnlyChangingPendingStatus(string mutation)
    {
        using var fixture = new Fixture(nativeEvidence: true);
        var scope = fixture.Open();
        var marker = scope.VerifyPreparationReply(fixture.Prepare(scope));
        var transition = fixture.NativeTransition(marker);
        var queue = JsonNode.Parse(File.ReadAllText(transition.QueuePath))!;
        if (mutation == "actor") queue["actor"]!["actor_id"] = "other";
        else queue["items"]![0]![mutation == "missing-state" ? "missing_state_factors" : "blocking_reasons"] =
            new JsonArray("unexpected");
        File.WriteAllText(transition.QueuePath, queue.ToJsonString(JsonOptions));
        Assert.Throws<InvalidDataException>(() => marker.ValidateQueue(transition.QueuePath));
    }
}
