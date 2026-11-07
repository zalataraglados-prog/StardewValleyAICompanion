using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class CropTeacherSeedPlantingPrefixTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    internal static void CompleteGrowthContext(CropTeacherSeedPlantingTests.Fixture f)
    {
        var grow = f.Ledger.CropPlantingCommitments.Single().BaseGrowDays;
        f.RefreshSource(snapshot =>
        {
            var location = JsonNode.Parse(snapshot.State["current_location"].GetRawText())!;
            var context = location["planting_context"]!["value"]!;
            context["location_id"] = "Farm";
            foreach (var tile in context["hoe_dirt_tiles"]!.AsArray())
                tile!["seed_results"] = JsonSerializer.SerializeToNode(Enumerable.Range(0, 2).Select(index => new
                {
                    slot_index = index, seed_id = "745", hard_rule_allows_planting = true,
                    base_grow_days = grow, adjusted_grow_days_with_paddy_if_eligible = grow,
                    speed_boost_without_paddy = 0d, speed_boost_with_paddy = 0d
                }), JsonOptions);
            snapshot.State["current_location"] = JsonSerializer.SerializeToElement(location, JsonOptions);
        });
    }

    [Fact]
    public void EightRealShapedStepsKeepOneOriginalAuthorityAndUseEachVerifiedFreshBoundary()
    {
        using var f = new CropTeacherSeedPlantingTests.Fixture();
        CompleteGrowthContext(f);
        var scope = f.Open();
        Assert.Equal(8, scope.ValidateExecutionPreflight(f.Batch(), f.OwnedSlotOne).Tiles.Length);
        var marker = f.Mark(scope);
        var queue = f.Compile(marker, marker.BuildActions(f.Batch(), f.OwnedSlotOne));
        var transition = f.NativeTransition(marker, compiledQueue: queue);
        var prefix = marker.OpenPrefix(f.QueuePath, marker.ValidateQueue(f.QueuePath));
        var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(transition.ExecutionReceiptPath), JsonOptions)!;
        var current = f.Before;
        for (var index = 0; index < 8; index++)
        {
            prefix.ValidateBeforeDispatch(index, queue.Items[index], current, marker.Ledger);
            prefix.ObserveNativeStep(receipt.StepResults[index], receipt.StepResults[index].AfterSnapshotPath);
            current = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(receipt.StepResults[index].AfterSnapshotPath), JsonOptions)!;
            Assert.Equal(index + 1, prefix.VerifiedCount);
            Assert.Equal(current.StateHash, prefix.CurrentStateHash);
        }
        Assert.NotEqual(f.Before.StateHash, prefix.CurrentStateHash);
        Assert.Equal(f.Before.StateHash, marker.Ledger.SourceStateHash);
    }

    [Theory]
    [InlineData("fertilizer")]
    [InlineData("paddy")]
    [InlineData("adjustment")]
    [InlineData("string_zero")]
    [InlineData("missing")]
    [InlineData("slot")]
    public void UnsupportedGrowthIsRejectedBeforeRealMarkerMutation(string fault)
    {
        using var f = new CropTeacherSeedPlantingTests.Fixture();
        CompleteGrowthContext(f);
        f.RefreshSource(snapshot =>
        {
            var location = JsonNode.Parse(snapshot.State["current_location"].GetRawText())!;
            var seed = location["planting_context"]!["value"]!["hoe_dirt_tiles"]![0]!["seed_results"]![1]!.AsObject();
            switch (fault)
            {
                case "fertilizer": seed["speed_boost_without_paddy"] = 0.1; break;
                case "paddy": seed["speed_boost_with_paddy"] = 0.25; break;
                case "adjustment": seed["adjusted_grow_days_with_paddy_if_eligible"] = 1; break;
                case "string_zero": seed["speed_boost_with_paddy"] = "0"; break;
                case "missing": seed.Remove("speed_boost_with_paddy"); break;
                case "slot": seed["slot_index"] = 9; break;
            }
            snapshot.State["current_location"] = JsonSerializer.SerializeToElement(location, JsonOptions);
        });
        var before = f.Repository.Get(f.Before).Revision;
        Assert.Throws<InvalidDataException>(() => f.Open().ValidateExecutionPreflight(f.Batch(), f.OwnedSlotOne));
        Assert.Equal(before, f.Repository.Get(f.Before).Revision);
    }

    [Fact]
    public void NextDispatchCannotSkipObservationReuseStaleNativeSourceOrAcceptAnotherLedger()
    {
        using var f = new CropTeacherSeedPlantingTests.Fixture();
        CompleteGrowthContext(f);
        var marker = f.Mark();
        var queue = f.Compile(marker, marker.BuildActions(f.Batch(), f.OwnedSlotOne));
        Assert.True(queue.Status == "pending", string.Join(",", queue.CompilerDiagnostics.Concat(queue.Items.SelectMany(row => row.BlockingReasons))));
        var transition = f.NativeTransition(marker, compiledQueue: queue);
        var prefix = marker.OpenPrefix(f.QueuePath, marker.ValidateQueue(f.QueuePath));
        prefix.ValidateBeforeDispatch(0, queue.Items[0], f.Before, marker.Ledger);
        Assert.Throws<InvalidDataException>(() => prefix.ValidateBeforeDispatch(1, queue.Items[1], f.Before, marker.Ledger));
        var receipt = JsonSerializer.Deserialize<QueueExecutionReceiptEnvelope>(File.ReadAllText(transition.ExecutionReceiptPath), JsonOptions)!;
        prefix.ObserveNativeStep(receipt.StepResults[0], receipt.StepResults[0].AfterSnapshotPath);
        Assert.Throws<InvalidDataException>(() => prefix.ValidateBeforeDispatch(1, queue.Items[1], f.Before, marker.Ledger));
        var current = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(receipt.StepResults[0].AfterSnapshotPath), JsonOptions)!;
        var corrupt = marker.Ledger;
        corrupt.MaterialReservations.Single(row => row.ReservationId == "outside").Quantity++;
        Assert.Throws<InvalidDataException>(() => prefix.ValidateBeforeDispatch(1, queue.Items[1], current, corrupt));
        prefix.ValidateBeforeDispatch(1, queue.Items[1], current, marker.Ledger);
    }
}
