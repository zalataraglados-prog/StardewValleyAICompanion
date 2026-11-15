using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SettledSharedTransferReturnReplaysTheOriginalSettlementBeforeItsMarker(bool product)
    {
        using var f = new SharedTransferReturnFixture(); await f.Build(product);
        Assert.Throws<InvalidDataException>(() => CropTeacherSharedSourceReturn.Open(f.Files.RequestPath, f.Files.SourcePath,
            f.Files.LedgerPath, [Id], f.Transition, "old.marker"));
        var scope = f.Open();
        Assert.Equal(JsonSerializer.Serialize(f.Reply.Ledger), JsonSerializer.Serialize(scope.BaseLedger));
        Assert.Equal(f.Reply.Ledger!.Revision, scope.RebindRequest.ExpectedLedgerRevision);
        Assert.False(scope.CropProductionCreditAuthorized); Assert.False(scope.FormalProductTrainingAuthorized);
        Assert.Equal(1, f.Runtime.Handler.Settlements);
        var reply = new ReservationPortfolioLedgerService().Commit(scope.BaseLedger, f.Files.After, scope.RebindRequest, Timestamp);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        var result = scope.VerifyRebindReply(reply);
        Assert.Equal(f.Reply.Ledger.Revision + 1, result.Revision);
        Assert.Equal("player:123", result.MaterialReservations.Single(row => row.ReservationId == "owned-seeds").NodeId);
        Assert.All(new[] { f.Reference.RequestPath, f.Reference.ReplyPath, f.Reference.JournalPath }, path =>
            Assert.Equal(SharedTransferReturnFixture.Hash(path), scope.InputHashes[path]));
        var transition = f.Transition with
        {
            NativeSharedSource = new(f.Files.RequestPath, f.Files.LedgerPath, [Id], "shared.return.transfer.fixture", scope.InputHashes)
                { SettledMaterialTransfer = f.Reference }
        };
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.Verify(transition));
        Assert.Equal(scope.AfterStateHash, MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithSettledMaterialTransfers(transition).AfterStateHash);
    }

    [Theory]
    [InlineData("receipt")]
    [InlineData("binding")]
    [InlineData("reply")]
    [InlineData("journal")]
    [InlineData("actor")]
    public async Task SettledSharedTransferReturnRejectsChangedOriginalSettlementEvidence(string fault)
    {
        using var f = new SharedTransferReturnFixture(); await f.Build();
        var scope = f.Open();
        var path = fault is "reply" or "actor" ? f.Reference.ReplyPath : fault == "journal" ? f.Reference.JournalPath : f.Reference.RequestPath;
        var value = JsonNode.Parse(File.ReadAllText(path))!;
        if (fault == "receipt") value["supporting_transition_receipt_sha256"] = new string('a', 64);
        if (fault == "binding") value["prepared_material_transfer_sha256"] = new string('a', 64);
        if (fault == "reply") value["ledger"]!["revision"] = 999;
        if (fault == "actor") value["ledger"]!["player_id"] = "other";
        if (fault == "journal") value["operation"] = "commit";
        Fixture.Save(path, value);
        Assert.Throws<InvalidDataException>(scope.ValidateInputsUnchanged);
        Assert.Throws<InvalidDataException>(() => f.Open());
        Assert.Equal(1, f.Runtime.Handler.Settlements);
    }

    [Fact]
    public async Task SettledSharedTransferReturnAdvancesAndRestoresTheSameOwnerWithoutRepeatingSettlement()
    {
        using var f = new SharedTransferReturnFixture(); await f.Build();
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs);
        await using var owner = await files.Open();
        var operation = await owner.AdmitSettledSharedTransferReturnAsync(f.Transition, files.Native, f.Reference);
        await owner.RunSharedSourceReturnAsync(operation, f.Runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, request), files.Post);
        Assert.Null(owner.Pending); Assert.Equal(f.Transition.AfterSnapshotPath, owner.CurrentSnapshotPath);
        var trace = await owner.ReadJointProductionTraceAsync();
        Assert.Equal(f.Reference, Assert.Single(trace.SharedSourceReturns).SourceReference!.SettledMaterialTransfer);
        Assert.Equal(1, f.Runtime.Handler.Settlements); Assert.Equal(2, f.Runtime.Handler.Commits);
        await owner.DisposeAsync();
        await using var reopened = await files.Open();
        await reopened.RestoreHeadAsync(f.Runtime.Client, files.Recovery, files.Post);
        Assert.Null(reopened.Pending); Assert.Equal(f.Transition.AfterSnapshotPath, reopened.CurrentSnapshotPath);
        Assert.Single((await reopened.ReadJointProductionTraceAsync()).SharedSourceReturns);
        Assert.Equal(1, f.Runtime.Handler.Settlements); Assert.Equal(2, f.Runtime.Handler.Commits);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task SettledSharedTransferReturnRecoversOnlyItsOriginalMarkerAfterLostReply(bool retained, bool prepared)
    {
        using var f = new SharedTransferReturnFixture(); await f.Build(includePreparation: prepared);
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs);
        var failed = false;
        await using var owner = await files.Open(writer: async (path, json) =>
        {
            if (!failed && Path.GetFileName(path) == "marker-reply.json")
            {
                failed = true;
                if (retained) await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
                throw new IOException("Declared transfer-return marker acknowledgement lost.");
            }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        var scope = f.Open(); var hashes = scope.InputHashes;
        var operation = prepared
            ? await owner.AdmitPreparedSharedTransferReturnAsync(f.Transition, files.Native, f.Reference)
            : await owner.AdmitSettledSharedTransferReturnAsync(f.Transition, files.Native, f.Reference);
        await Assert.ThrowsAsync<IOException>(() => owner.RunSharedSourceReturnAsync(operation, f.Runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, request), files.Post));
        Assert.True(failed); await owner.DisposeAsync();
        await using var reopened = await files.Open();
        await reopened.RecoverPendingAsync(f.Runtime.Client, files.Recovery, files.Post);
        Assert.Null(reopened.Pending); Assert.Equal(f.Transition.AfterSnapshotPath, reopened.CurrentSnapshotPath);
        Assert.Equal(2, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
        Assert.Equal(retained ? 0 : 1, f.Runtime.Handler.Lookups);
        Assert.All(hashes, pair => Assert.Equal(pair.Value, SharedTransferReturnFixture.Hash(pair.Key)));
        Assert.False(File.Exists(Path.Combine(operation.RuntimeDirectory, "shared-source-native-claim.json")));
    }

    [Fact]
    public async Task SettledSharedTransferReturnCannotChangeBackendOrAliasOriginalRoles()
    {
        using var f = new SharedTransferReturnFixture(); await f.Build();
        Assert.Throws<InvalidDataException>(() => CropTeacherSharedSourceReturn.OpenAfterSettledMaterialTransfer(
            f.Files.RequestPath, f.Files.SourcePath, f.Files.LedgerPath, [Id], f.Transition, "shared.bad.alias",
            f.Reference with { ReplyPath = f.Reference.RequestPath }));
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs);
        await using var owner = await files.Open();
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.AdmitSettledSharedTransferReturnAsync(f.Transition,
            files.Native, f.Reference with { BackendUrl = "http://other-backend" }));
        Assert.Null(owner.Pending); Assert.Equal(1, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
    }
}
