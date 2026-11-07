using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedTransferTests
{
    [Theory]
    [InlineData("source-stack")]
    [InlineData("destination-stack")]
    [InlineData("outside-inventory")]
    [InlineData("graph-unknown-field")]
    [InlineData("aggregate-slots")]
    [InlineData("chest-occupancy")]
    [InlineData("storage-echo")]
    [InlineData("inventory-metadata")]
    [InlineData("new-item-metadata")]
    [InlineData("capacity")]
    [InlineData("seed-echo-missing")]
    [InlineData("seed-catalog")]
    [InlineData("money")]
    [InlineData("currency")]
    [InlineData("persistent-progress")]
    [InlineData("clock-rewind")]
    [InlineData("next-day")]
    public void CoherentNativeReceiptsCannotHideUnexpectedResourceOrPersistentChanges(string mutation)
    {
        using var fixture = new Fixture(nativeEvidence: true);
        var scope = fixture.Open();
        var marker = scope.VerifyPreparationReply(fixture.Prepare(scope));
        var transition = fixture.NativeTransition(marker, after =>
        {
            void Change(string domain, Action<JsonObject> change)
            {
                var node = JsonNode.Parse(after.State[domain].GetRawText())!.AsObject();
                change(node);
                after.State[domain] = JsonSerializer.SerializeToElement(node, JsonOptions);
            }
            switch (mutation)
            {
                case "source-stack": Change("farm", farm => farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![1]!["slots"]![0]!["stack"] = 1); break;
                case "destination-stack": Change("farm", farm => farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["slots"]![0]!["stack"] = 998); break;
                case "outside-inventory": Change("farm", farm => farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![2]!["slots"]![0]!["stack"] = 8); break;
                case "graph-unknown-field": Change("farm", farm => farm["material_inventory_graph"]!["value"]!["unexpected_progress"] = true); break;
                case "aggregate-slots": Change("farm", farm => farm["material_inventory_graph"]!["value"]!["quantity_rows"]![0]!["source_slot_count"] = 99); break;
                case "chest-occupancy": Change("farm", farm => farm["material_inventory_graph"]!["value"]!["access_points"]![0]!["occupied_slot_count"] = 0); break;
                case "storage-echo": Change("farm", farm => farm["chests"]!["value"]!["unexpected_progress"] = true); break;
                case "inventory-metadata": Change("player", player => player["inventory"]!["value"]![0]!["unexpected_item_state"] = true); break;
                case "new-item-metadata": Change("player", player => player["inventory"]!["value"]![1]!["runtime_type"] = "Other.Type"); break;
                case "capacity": Change("player", player => player["inventory_capacity"]!["value"]!["empty_slots"] = 0); break;
                case "seed-echo-missing": Change("player", player => player["seed_inventory"]!["value"]!.AsArray().RemoveAt(1)); break;
                case "seed-catalog": Change("player", player => player["seed_inventory"]!["value"]![1]!["seed_id"] = "472"); break;
                case "money": Change("player", player => player["money"]!["value"] = 499); break;
                case "currency": Change("player", player => player["shop_currency_balances"]!["value"]!["rows"]![1]!["balance"] = 1); break;
                case "persistent-progress": Change("player", player => player["unexpected_persistent_progress"] = new JsonObject { ["value"] = true, ["status"] = "available" }); break;
                case "clock-rewind": Change("time", time => time["time"]!["value"] = 550); after.InGameTime.Value = 550; break;
                case "next-day": Change("time", time => time["total_days"]!["value"] = 104); break;
            }
        });
        Assert.Throws<InvalidDataException>(() => fixture.VerifyNative(marker, transition));
        Assert.False(File.Exists(fixture.NativeProofPath));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("outside")]
    [InlineData("relocation")]
    [InlineData("count")]
    public void OnlyAnEntireActualSettlementReplyCanEstablishTransferredOwnership(string mutation)
    {
        using var fixture = new Fixture(nativeEvidence: true);
        var scope = fixture.Open();
        var marker = scope.VerifyPreparationReply(fixture.Prepare(scope));
        var context = fixture.VerifyNative(marker, fixture.NativeTransition(marker));
        var result = new ReservationPortfolioLedgerService().SettleSupportingTransition(marker.Ledger,
            fixture.After, context.SettlementRequest, Timestamp);
        Assert.True(result.Accepted);
        switch (mutation)
        {
            case "owner": result.Ledger!.MaterialReservations.Single(row => row.ReservationId == "owned-seeds").SourceDecisionId = "other-root"; break;
            case "outside": result.Ledger!.MaterialReservations.Single(row => row.ReservationId == "outside").Quantity++; break;
            case "relocation": result.MaterialRelocations[0].DestinationSlotIndex++; break;
            case "count": result.ConsumedQuantity++; break;
        }
        Assert.Throws<InvalidDataException>(() => context.VerifySettlementReply(result));
    }

    [Theory]
    [InlineData("candidate")]
    [InlineData("run")]
    [InlineData("step-path")]
    [InlineData("primitive")]
    public void OriginalNativeReceiptAndStepFileGatesAreRetained(string mutation)
    {
        using var fixture = new Fixture(nativeEvidence: true);
        var scope = fixture.Open();
        var marker = scope.VerifyPreparationReply(fixture.Prepare(scope));
        var transition = fixture.NativeTransition(marker, receiptChange: receipt =>
        {
            switch (mutation)
            {
                case "candidate": receipt.SelectedCandidateId = "wrong"; break;
                case "run": receipt.RunId = "wrong"; break;
                case "step-path": receipt.StepResults[0].AfterSnapshotPath = string.Empty; break;
                case "primitive": receipt.StepResults[0].PrimitiveVerificationStatus = "unknown"; break;
            }
        });
        Assert.Throws<InvalidDataException>(() => fixture.VerifyNative(marker, transition));
        Assert.False(File.Exists(fixture.NativeProofPath));
    }
}
