using System.Security.Cryptography;
using System.Text;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class NativeOutputObservationBoundaryTests
{
    [Theory]
    [InlineData(0, 16, true)]
    [InlineData(6, 16, true)]
    [InlineData(4, 12, false)]
    [InlineData(6, 10, true)]
    public void UsesActualInventoryDeltaNotReturnOrPostCallStack(int stackAfter, int inventoryAfter, bool returned)
    {
        var (request, receipt) = Fixture();
        var admission = receipt.Collections[0].InventoryAdmissions[0];
        admission.ItemStackAfterCall = stackAfter; admission.InventoryAfter = inventoryAfter; admission.NativeReturn = returned;
        receipt.InventoryAfter[0].Quantity = inventoryAfter;
        var result = Read(request, receipt);
        Assert.Equal(inventoryAfter, result.Inventory.Single().InventoryAfter);
        Assert.True(result.Collections.Single().DebrisPresentAtScopeStart);
        Assert.True(result.Collections.Single().ChunkPresentAtScopeStart);
        Assert.False(result.ResourceCreditAuthorized);
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
    }

    [Fact]
    public void NewReferenceIsNotAutomaticallyAProductionWitnessAndSummaryIsDetached()
    {
        var (request, receipt) = Fixture();
        receipt.BeforeDebris = [];
        var result = Read(request, receipt);
        receipt.InventoryAfter[0].Quantity = 999;
        receipt.Collections[0].DebrisId = Guid.NewGuid().ToString("N");
        Assert.False(result.Collections.Single().DebrisPresentAtScopeStart);
        Assert.False(result.Collections.Single().ChunkPresentAtScopeStart);
        Assert.Equal(16, result.Inventory.Single().InventoryAfter);
        Assert.False(result.ResourceCreditAuthorized);
    }

    [Fact]
    public void CollectionSuccessWithoutInventoryAdmissionDoesNotGrantQuantity()
    {
        var (request, receipt) = Fixture();
        receipt.Collections[0].InventoryAdmissions = [];
        receipt.InventoryAfter[0].Quantity = 10;
        Assert.Equal(0, Read(request, receipt).Inventory.Single().AdmissionCount);
        receipt.InventoryAfter[0].Quantity = 11;
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MultipleAdmissionsMustExhaustActualAggregateWithNoGapOrDoubleCount(bool zeroDelta)
    {
        var (request, receipt) = Fixture();
        var first = receipt.Collections[0].InventoryAdmissions[0];
        first.InventoryAfter = 12;
        var second = new ActionOriginInventoryAdmission { Item = first.Item, OfferedQuantity = 6, InventoryBefore = 12,
            InventoryAfter = zeroDelta ? 12 : 16, NativeReturn = !zeroDelta, ItemStackAfterCall = 6 };
        receipt.Collections[0].InventoryAdmissions = [first, second];
        receipt.InventoryAfter[0].Quantity = second.InventoryAfter;
        Assert.Equal(2, Read(request, receipt).Inventory.Single().AdmissionCount);
        second.InventoryBefore = 11;
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdmissionExecutionOrderCannotBeRepairedBySortingOrFutureEndpoints(bool futureZero)
    {
        var (request, receipt) = Fixture();
        var initial = receipt.Collections[0].InventoryAdmissions[0];
        var later = new ActionOriginInventoryAdmission { Item = initial.Item, OfferedQuantity = 6,
            InventoryBefore = futureZero ? 16 : 12, InventoryAfter = 16, NativeReturn = true };
        if (!futureZero) initial.InventoryAfter = 12;
        receipt.Collections[0].InventoryAdmissions = [later, initial];
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    [Theory]
    [InlineData("scope")]
    [InlineData("nonce")]
    [InlineData("queue-item")]
    [InlineData("before-hash")]
    [InlineData("location")]
    [InlineData("tick")]
    [InlineData("status")]
    [InlineData("diagnostic")]
    [InlineData("session")]
    [InlineData("duplicate-debris")]
    [InlineData("duplicate-chunk")]
    [InlineData("chunk-reference")]
    [InlineData("ordinal")]
    [InlineData("collection-return")]
    [InlineData("admission-return")]
    [InlineData("quantity")]
    [InlineData("unobserved-gain")]
    [InlineData("hash")]
    [InlineData("xml-identity")]
    [InlineData("xml-quality")]
    [InlineData("xml-stack")]
    [InlineData("duplicate-xml-field")]
    [InlineData("dtd")]
    [InlineData("item-type")]
    [InlineData("missing-held-payload")]
    [InlineData("inventory-identity")]
    [InlineData("foreign-admission-baseline")]
    [InlineData("zero-at-impossible-quantity")]
    public void RejectsUnboundAmbiguousOrCorruptObservation(string change)
    {
        var (request, receipt) = Fixture();
        var collection = receipt.Collections[0]; var admission = collection.InventoryAdmissions[0];
        switch (change)
        {
            case "scope": receipt.ScopeId += "changed"; break;
            case "nonce": receipt.RequestNonce += "changed"; break;
            case "queue-item": receipt.QueueItemId += "changed"; break;
            case "before-hash": receipt.BeforeStateHash += "changed"; break;
            case "location": receipt.LocationId = "Town"; break;
            case "tick": collection.AfterGameTick = 111; break;
            case "status": receipt.Status = "unavailable"; break;
            case "diagnostic": receipt.Diagnostics = ["truncated"]; break;
            case "session": receipt.CaptureSessionId = "missing"; break;
            case "duplicate-debris": receipt.BeforeDebris = [receipt.BeforeDebris[0], receipt.BeforeDebris[0]]; break;
            case "duplicate-chunk": collection.DebrisBefore!.ChunkIds = [collection.ChunkId!, collection.ChunkId!]; break;
            case "chunk-reference": collection.ChunkId = Guid.NewGuid().ToString("N"); break;
            case "ordinal": collection.Ordinal = 2; break;
            case "collection-return": collection.NativeReturn = null; break;
            case "admission-return": admission.NativeReturn = null; break;
            case "quantity": admission.InventoryAfter = 17; receipt.InventoryAfter[0].Quantity = 17; break;
            case "unobserved-gain": receipt.InventoryAfter[0].Quantity = 17; break;
            case "hash": admission.Item!.SerializedStateSha256 = new string('0', 64); break;
            case "xml-identity": Rewrite(admission.Item!, xml => xml.Replace("<itemId>388", "<itemId>390")); break;
            case "xml-quality": Rewrite(admission.Item!, xml => xml.Replace("<quality>0", "<quality>1")); break;
            case "xml-stack": Rewrite(admission.Item!, xml => xml.Replace("<stack>6", "<stack>5")); break;
            case "duplicate-xml-field": Rewrite(admission.Item!, xml => xml.Replace("</Object>", "<stack>6</stack></Object>")); break;
            case "dtd": Rewrite(admission.Item!, xml => "<!DOCTYPE Object [<!ENTITY x '388'>]>" + xml); break;
            case "item-type": admission.Item!.RuntimeType = "Custom.Object"; break;
            case "missing-held-payload": collection.DebrisBefore!.HeldItemStack = 6; break;
            case "inventory-identity": receipt.InventoryBefore = [receipt.InventoryBefore[0], receipt.InventoryBefore[0]]; break;
            case "foreign-admission-baseline": admission.InventoryBefore = 100; admission.InventoryAfter = 106; break;
            case "zero-at-impossible-quantity": admission.InventoryBefore = 12; admission.InventoryAfter = 12;
                receipt.InventoryAfter[0].Quantity = 10; break;
        }
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    private static NativeOutputObservationSummary Read(TrainingExecutionRequest request, ActionOriginObservationReceipt receipt) =>
        NativeOutputObservationBoundary.Read(request, receipt, 99, 111, "Farm");

    [Theory]
    [InlineData("debris-rows")]
    [InlineData("chunk-total")]
    [InlineData("collection-total")]
    [InlineData("admission-total")]
    [InlineData("xml-size")]
    [InlineData("inventory-rows")]
    [InlineData("total-payload")]
    public void RejectsNativeReceiptBudgetOverflowWithoutTruncating(string change)
    {
        var (request, receipt) = Fixture();
        var collection = receipt.Collections[0]; var admission = collection.InventoryAdmissions[0];
        switch (change)
        {
            case "debris-rows": receipt.BeforeDebris = Enumerable.Repeat(receipt.BeforeDebris[0], 513).ToArray(); break;
            case "chunk-total": receipt.BeforeDebris[0].ChunkIds = Enumerable.Range(0, 4097).Select(_ => Guid.NewGuid().ToString("N")).ToArray(); break;
            case "collection-total": receipt.Collections = Enumerable.Repeat(collection, 257).ToArray(); break;
            case "admission-total": collection.InventoryAdmissions = Enumerable.Repeat(admission, 257).ToArray(); break;
            case "xml-size": Rewrite(admission.Item!, _ => new string(' ', 65537)); break;
            case "inventory-rows": receipt.InventoryBefore = Enumerable.Repeat(receipt.InventoryBefore[0], 513).ToArray(); break;
            case "total-payload":
                Rewrite(admission.Item!, xml => xml.Replace("</Object>", "<!--" + new string('x', 60000) + "--></Object>"));
                collection.InventoryAdmissions = Enumerable.Repeat(admission, 18).ToArray(); break;
        }
        Assert.Throws<InvalidDataException>(() => Read(request, receipt));
    }

    internal static (TrainingExecutionRequest Request, ActionOriginObservationReceipt Receipt) Fixture()
    {
        var request = new TrainingExecutionRequest
        {
            RunId = "run", QueueId = "queue", QueueItemId = "item", RequestNonce = Guid.NewGuid().ToString("N"),
            OptionId = "executor.move_to_tile", SourcePlanningOptionId = "executor.move_to_tile", SaveIsolationPath = "isolated-saves",
            BeforeStateHash = new string('a', 64), ActionOriginObservationScopeId = "native-output.v1:fixture"
        };
        var item = new ActionOriginObservedItem { RuntimeType = "StardewValley.Object", QualifiedItemId = "(O)388" };
        Rewrite(item, _ => "<Object><itemId>388</itemId><quality>0</quality><stack>6</stack><bigCraftable>false</bigCraftable></Object>");
        var debris = new ActionOriginObservedDebris { DebrisId = Guid.NewGuid().ToString("N"), RuntimeType = "StardewValley.Debris",
            DebrisType = "RESOURCE", RawItemId = "(O)388", ChunkIds = [Guid.NewGuid().ToString("N")] };
        var receipt = new ActionOriginObservationReceipt
        {
            Status = "observed", ScopeId = request.ActionOriginObservationScopeId, CaptureSessionId = Guid.NewGuid().ToString("N"),
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId, RequestNonce = request.RequestNonce,
            BeforeStateHash = request.BeforeStateHash, LocationId = "Farm", BeforeGameTick = 100, AfterGameTick = 110,
            BeforeDebris = [debris], InventoryBefore = [new() { QualifiedItemId = "(O)388", Quantity = 10 }],
            InventoryAfter = [new() { QualifiedItemId = "(O)388", Quantity = 16 }], Collections = [new()
            {
                Ordinal = 1, DebrisId = debris.DebrisId, ChunkId = debris.ChunkIds[0], DebrisBefore = debris,
                BeforeGameTick = 105, AfterGameTick = 105, NativeReturn = true,
                InventoryAdmissions = [new() { Item = item, OfferedQuantity = 6, InventoryBefore = 10, InventoryAfter = 16, NativeReturn = true }]
            }]
        };
        return (request, receipt);
    }

    private static void Rewrite(ActionOriginObservedItem item, Func<string, string> change)
    {
        item.SerializedStateXml = change(item.SerializedStateXml);
        item.SerializedStateSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.SerializedStateXml))).ToLowerInvariant();
    }
}
