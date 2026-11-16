using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;
using StardewAI.GoalConditionedBootstrap;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaleStockPartitionsUseTheSameMainOwnerAndRecoverOnlyTheOriginalAcceptedPreparation(bool loseReply)
    {
        using var f = new SelectedSaleStockFixture(partitioned: true); var lost = false;
        await using var owner = await f.OwnerFiles.Open(writer: async (path, json) =>
        {
            if (loseReply && !lost && Path.GetFileName(path) == "marker-reply.json")
            { lost = true; throw new IOException("Declared partition preparation reply publication loss."); }
            await InvokeLive<Task>("WritePortfolioJournalFileAsync", path, json);
        });
        await f.RetainDecision(owner);
        Assert.Equal(2, f.Preview.Request.MaterialClaims.Length);
        if (loseReply) await Assert.ThrowsAsync<IOException>(() => f.PrepareMain(owner));
        else
        {
            var result = await f.PrepareMain(owner);
            var other = result.GetType().GetProperty("OtherGoalPreparation")!.GetValue(result)!;
            Assert.Same(owner.Pending, other.GetType().GetProperty("Operation")!.GetValue(other));
        }
        Assert.Equal(1, f.Transfer.Runtime.Handler.Commits);
        var beforeHead = owner.CurrentSnapshotPath;
        await owner.DisposeAsync();
        await using var reopened = await f.OwnerFiles.Open();
        Assert.NotNull(reopened.Pending);
        if (loseReply)
        {
            var path = await InvokeLive<Task<string>>("RecoverCropTeacherSelectedSaleStockWithOwnerAsync",
                f.Transfer.Runtime.Client, f.Options, reopened);
            Assert.True(JsonNode.Parse(File.ReadAllText(path))!["preparation_only"]!.GetValue<bool>());
            Assert.Equal(1, f.Transfer.Runtime.Handler.Lookups);
        }
        var claim = await reopened.ClaimSharedSourceExecutionAsync(reopened.Pending!, f.Transfer.Runtime.Client);
        var queue = JsonSerializer.Deserialize<ActionQueueEnvelope>(File.ReadAllText(claim.Transition.QueuePath), JsonOptions)!;
        Assert.Contains(queue.Items, row => row.OptionId == "executor.move_to_tile");
        var transfer = Assert.Single(queue.Items.Where(row => row.OptionId == "executor.transfer_material"));
        var ledger = f.Transfer.Runtime.Repository.Get(f.Transfer.Files.Before);
        Assert.Equal(2, ledger.PreparedMaterialTransfer!.MaterialRelocations.Length);
        Assert.True(CropSaleStockMaterialTransferBinding.IsBound(transfer, ledger));
        Assert.Equal(beforeHead, reopened.CurrentSnapshotPath);
        Assert.Equal(1, f.Transfer.Runtime.Handler.Commits); Assert.Equal(0, f.Transfer.Runtime.Handler.Settlements);
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("purpose")]
    [InlineData("relocation")]
    [InlineData("release")]
    public void SaleStockPartitionsRequireTheirCanonicalAtomicRequestAndKeepTheOldSupportStrict(string fault)
    {
        using var f = new SelectedSaleStockFixture(partitioned: true);
        var files = f.Transfer.Files;
        CropTeacherSharedTransferSupport Open(ReservationPortfolioCommitRequest request, bool partitioned) => partitioned
            ? CropTeacherSharedTransferSupport.OpenPartitionedSaleStock(files.RequestPath, files.SourcePath, files.LedgerPath,
                [Id], "declared.partition.source", request, new Dictionary<string, string>())
            : CropTeacherSharedTransferSupport.Open(files.RequestPath, files.SourcePath, files.LedgerPath,
                [Id], "declared.partition.source", request, new Dictionary<string, string>());
        _ = Open(f.Preview.Request, true);
        Assert.Throws<InvalidDataException>(() => Open(f.Preview.Request, false));
        var changed = JsonSerializer.Deserialize<ReservationPortfolioCommitRequest>(JsonSerializer.Serialize(f.Preview.Request, JsonOptions), JsonOptions)!;
        if (fault == "quantity") changed.MaterialClaims[0].Quantity++;
        if (fault == "purpose") changed.MaterialClaims[1].Purpose = "not-original-stock";
        if (fault == "relocation") changed.MaterialTransferPreparation!.MaterialRelocations[1].DestinationSlotIndex = 15;
        if (fault == "release") changed.ReleaseReservationIds = [changed.MaterialClaims[0].ReservationId];
        Assert.Throws<InvalidDataException>(() => Open(changed, true));
        Assert.Equal(0, f.Transfer.Runtime.Handler.Commits);
    }
}
