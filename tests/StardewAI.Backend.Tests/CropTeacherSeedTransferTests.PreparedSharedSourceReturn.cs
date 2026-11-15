using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparedSharedTransferReturnKeepsTheExistingOwnerSourceThroughOriginalPreparationAndSettlement(bool product)
    {
        using var f = new SharedTransferReturnFixture();
        using var inputs = new OwnerInputs(f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath);
        using var files = new OwnerFiles(f.Runtime.Options, f.Runtime.Client, f.Files.SourcePath, f.Files.LedgerPath, inputs);
        await using var owner = await files.Open();
        var original = new[] { f.Files.SourcePath, f.Files.LedgerPath, f.Files.RequestPath }
            .ToDictionary(path => path, SharedTransferReturnFixture.Hash);
        await f.Build(product, includePreparation: true);
        Assert.All(original, pair => Assert.Equal(pair.Value, SharedTransferReturnFixture.Hash(pair.Key)));
        Assert.Null(JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(owner.CurrentLedgerPath), JsonOptions)!.PreparedMaterialTransfer);
        Assert.Throws<InvalidDataException>(() => CropTeacherSharedSourceReturn.OpenAfterSettledMaterialTransfer(
            f.Files.RequestPath, f.Files.SourcePath, f.Files.LedgerPath, [Id], f.Transition, "no.implicit.upgrade", f.Reference));
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.AdmitSettledSharedTransferReturnAsync(f.Transition, files.Native, f.Reference));
        Assert.Null(owner.Pending);
        var scope = f.Open();
        var transition = f.Transition with
        {
            NativeSharedSource = new(f.Files.RequestPath, f.Files.LedgerPath, [Id], "shared.return.transfer.fixture", scope.InputHashes)
                { SettledMaterialTransfer = f.Reference }
        };
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithSettledMaterialTransfers(transition));
        Assert.Equal(scope.AfterStateHash, MoneyRevenueCropNativeSharedSourceVerifier.VerifyWithPreparedMaterialTransfers(transition).AfterStateHash);
        var operation = await owner.AdmitPreparedSharedTransferReturnAsync(f.Transition, files.Native, f.Reference);
        await owner.RunSharedSourceReturnAsync(operation, f.Runtime.Client,
            (diagnostic, request) => InvokeLive<Task<JsonObject>>("PostJournaledPortfolioCommitAsync", f.Runtime.Client, f.Runtime.Options, diagnostic, request), files.Post);
        Assert.Null(owner.Pending); Assert.Equal(f.Transition.AfterSnapshotPath, owner.CurrentSnapshotPath);
        Assert.Equal(f.Reference, Assert.Single((await owner.ReadJointProductionTraceAsync()).SharedSourceReturns).SourceReference!.SettledMaterialTransfer);
        Assert.Equal(2, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
        await owner.DisposeAsync();
        await using var reopened = await files.Open();
        await reopened.RestoreHeadAsync(f.Runtime.Client, files.Recovery, files.Post);
        Assert.Null(reopened.Pending); Assert.Equal(f.Transition.AfterSnapshotPath, reopened.CurrentSnapshotPath);
        Assert.All(original, pair => Assert.Equal(pair.Value, SharedTransferReturnFixture.Hash(pair.Key)));
        Assert.Equal(2, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
    }

    [Theory]
    [InlineData("request")]
    [InlineData("reply")]
    [InlineData("binding")]
    [InlineData("journal")]
    [InlineData("backend")]
    [InlineData("alias")]
    [InlineData("return_marker")]
    public async Task PreparedSharedTransferReturnRejectsBrokenOriginalPreparation(string fault)
    {
        using var f = new SharedTransferReturnFixture(); await f.Build(includePreparation: true);
        var scope = f.Open(); var preparation = f.Reference.Preparation!;
        if (fault == "return_marker")
            Assert.Throws<InvalidDataException>(() => CropTeacherSharedSourceReturn.OpenAfterPreparedAndSettledMaterialTransfer(
                f.Files.RequestPath, f.Files.SourcePath, f.Files.LedgerPath, [Id], f.Transition, Step, f.Reference));
        else if (fault is "backend" or "alias")
        {
            var changed = fault == "backend" ? preparation with { BackendUrl = "http://other-backend" } :
                preparation with { ReplyPath = preparation.RequestPath };
            Assert.Throws<InvalidDataException>(() => CropTeacherSharedSourceReturn.OpenAfterPreparedAndSettledMaterialTransfer(
                f.Files.RequestPath, f.Files.SourcePath, f.Files.LedgerPath, [Id], f.Transition, "changed.preparation",
                f.Reference with { Preparation = changed }));
        }
        else
        {
            var path = fault == "request" ? preparation.RequestPath : fault == "journal" ? preparation.JournalPath : preparation.ReplyPath;
            var value = JsonNode.Parse(File.ReadAllText(path))!;
            if (fault == "request") value["expected_ledger_revision"] = 999;
            if (fault == "reply") value["ledger"]!["revision"] = 999;
            if (fault == "binding") value["ledger"]!["prepared_material_transfer"]!["binding_sha256"] = new string('a', 64);
            if (fault == "journal") value["operation"] = "settle-supporting-transition";
            Fixture.Save(path, value);
            Assert.Throws<InvalidDataException>(scope.ValidateInputsUnchanged);
            Assert.Throws<InvalidDataException>(() => f.Open());
        }
        Assert.Equal(1, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
    }
}
