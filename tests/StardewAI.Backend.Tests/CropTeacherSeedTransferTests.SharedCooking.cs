using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedCookingReplaysOriginalNativeWireAndReturnsProtectedForeignPositionsWithoutTransferCredit(bool product)
    {
        using var f = new SharedCookingFixture(); await f.Build(product);
        Assert.True(f.Scope.RequiresConsumptionSettlement); Assert.False(f.Scope.RequiresTransferSettlement);
        var returned = Assert.IsType<CropTeacherSharedSourceReturn>(f.Scope.VerifyTransition(f.Transition));
        Assert.Null(returned.TransferSettlement); Assert.Equal(f.Settlement, returned.ConsumptionSettlement);
        Assert.Equal(f.After.StateHash, returned.AfterStateHash); Assert.Equal(1, returned.VerifiedNativeStepCount);
        var original = JsonSerializer.Serialize(f.BeforeLedger);
        Assert.Equal(f.BeforeLedger.Revision + 2, returned.BaseLedger.Revision);
        Assert.Null(returned.BaseLedger.PreparedInventoryConsumption);
        foreach (var prior in f.BeforeLedger.MaterialReservations)
        {
            var current = returned.BaseLedger.MaterialReservations.Single(row => row.ReservationId == prior.ReservationId);
            Assert.Equal(prior.Quantity, current.Quantity); Assert.Equal(prior.SourceDecisionId, current.SourceDecisionId);
            Assert.Equal(prior.GoalId, current.GoalId); Assert.Equal(prior.Purpose, current.Purpose);
        }
        Assert.Equal(0, returned.BaseLedger.MaterialReservations.Single(row => row.ReservationId == "cooking-wood").SlotIndex);
        Assert.Equal(1, returned.BaseLedger.MaterialReservations.Single(row => row.ReservationId == "cooking-stone").SlotIndex);
        Assert.Equal(original, JsonSerializer.Serialize(f.BeforeLedger));
        Assert.Throws<InvalidDataException>(() => CropTeacherSharedSourceReturn.Open(f.Files.RequestPath, f.Files.SourcePath,
            f.Files.LedgerPath, [Id], f.Transition, "cannot.downgrade.cooking"));
        var marker = f.Runtime.Repository.CommitReservationPortfolio(f.After, returned.RebindRequest);
        Assert.True(marker.Accepted, string.Join(",", marker.Errors));
        var final = returned.VerifyRebindReply(marker);
        Assert.Equal(f.BeforeLedger.Revision + 3, final.Revision);
        Assert.Equal(1, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
        Assert.Throws<InvalidDataException>(() => f.Scope.ReadTransferSettlementRequest());
    }

    [Theory]
    [InlineData("preparation")]
    [InlineData("settlement")]
    [InlineData("reference")]
    [InlineData("native_result")]
    [InlineData("request_blob")]
    public async Task SharedCookingRetainsOriginalPreparationSettlementAndNativeInputs(string fault)
    {
        using var f = new SharedCookingFixture(); await f.Build(product: true);
        var returned = Assert.IsType<CropTeacherSharedSourceReturn>(f.Scope.VerifyTransition(f.Transition));
        var path = fault switch
        {
            "preparation" => f.Preparation.ReplyPath,
            "settlement" => f.Settlement.ReplyPath,
            "reference" => f.ReferencePath,
            "request_blob" => returned.InputHashes.Keys.Single(path => path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)),
            _ => f.NativeResultPath
        };
        if (fault == "request_blob")
        {
            var bytes = File.ReadAllBytes(path); bytes[0] ^= 1;
            File.WriteAllBytes(path, bytes);
        }
        else
        {
            var changed = JsonNode.Parse(File.ReadAllText(path))!;
            if (fault is "preparation" or "settlement") changed["ledger"]!["revision"] = 999;
            if (fault == "reference") changed["backend_url"] = "http://another-backend";
            if (fault == "native_result") changed["execution_request_nonce"] = "another-invocation";
            Fixture.Save(path, changed);
        }
        Assert.Throws<InvalidDataException>(returned.ValidateInputsUnchanged);
        Assert.Throws<InvalidDataException>(() => f.Scope.VerifyTransition(f.Transition));
        Assert.Equal(1, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
    }

    [Fact]
    public async Task SharedCookingRequiresTheOriginalObservedReceiptAndCannotBorrowTransferOrWrongReply()
    {
        using var f = new SharedCookingFixture(); await f.Build(product: true);
        var wrong = JsonSerializer.Deserialize<ReservationPortfolioSupportingTransitionSettlementRequest>(JsonSerializer.Serialize(f.SettlementRequest))!;
        wrong.SupportingTransitionReceiptSha256 = new string('b', 64);
        Assert.Throws<InvalidDataException>(() => f.Scope.VerifyConsumptionSettlementReply(wrong, f.SettlementReply));
        var forged = JsonSerializer.Deserialize<ReservationPortfolioSupportingTransitionSettlementResult>(JsonSerializer.Serialize(f.SettlementReply))!;
        forged.Ledger!.MaterialReservations.Single(row => row.ReservationId == "cooking-stone").GoalId = "stolen";
        Assert.Throws<InvalidDataException>(() => f.Scope.VerifyConsumptionSettlementReply(f.SettlementRequest, forged));
        var original = JsonNode.Parse(File.ReadAllText(f.NativeResultPath))!;
        original["execution_request_nonce"] = "another-invocation"; Fixture.Save(f.NativeResultPath, original);
        Assert.Throws<InvalidDataException>(() => f.Scope.ReadConsumptionSettlementRequest());
        Assert.Equal(1, f.Runtime.Handler.Commits); Assert.Equal(1, f.Runtime.Handler.Settlements);
    }
}
