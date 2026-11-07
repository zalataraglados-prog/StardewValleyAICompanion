using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPlantingTests
{
    [Theory]
    [InlineData("raw_stack")]
    [InlineData("raw_quality")]
    [InlineData("seed_echo")]
    [InlineData("aggregate")]
    [InlineData("capacity")]
    [InlineData("other_slot")]
    [InlineData("money")]
    [InlineData("crop_missing")]
    [InlineData("crop_identity")]
    [InlineData("crop_growth")]
    [InlineData("other_crop_metadata")]
    [InlineData("seeds_sown")]
    [InlineData("unknown_progress")]
    [InlineData("day")]
    [InlineData("clock")]
    public void NativeConsumptionRejectsIncorrectEffectsAndUnrelatedPersistentProgress(string fault)
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var transition = f.NativeTransition(marker, change: snapshot => Change(snapshot, fault));
        Assert.Throws<InvalidDataException>(() => f.Verify(marker, transition));
        Assert.False(File.Exists(f.ProofPath));
    }

    private static void Change(SnapshotEnvelope snapshot, string fault)
    {
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!;
        var actor = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        var progress = JsonNode.Parse(snapshot.State["world_progress"].GetRawText())!;
        var crops = farm["crops"]!["value"]!.AsArray();
        switch (fault)
        {
            case "raw_stack": actor["inventory"]!["value"]![1]!["stack"] = 2; break;
            case "raw_quality": actor["inventory"]!["value"]![1]!["quality"] = 4; break;
            case "seed_echo": actor["seed_inventory"]!["value"]![1]!["stack"] = 2; break;
            case "aggregate": farm["material_inventory_graph"]!["value"]!["quantity_rows"]![0]!["available_quantity"] = 99999; break;
            case "capacity": actor["inventory_capacity"]!["value"]!["empty_slots"] = 0; break;
            case "other_slot": actor["inventory"]!["value"]![0]!["display_name"] = "changed"; break;
            case "money": actor["money"]!["value"] = 123456; break;
            case "crop_missing": crops.RemoveAt(crops.Count - 1); break;
            case "crop_identity": crops[^1]!["harvest_source_seed_id"] = "472"; break;
            case "crop_growth": crops[^1]!["current_phase"] = 1; break;
            case "other_crop_metadata": crops[0]!["unknown_native_crop_detail"] = "changed"; break;
            case "seeds_sown": progress["game_state_query_unlock_state"]!["value"]!["players"]![0]!["stats"]!["seedsSown"] = 999; break;
            case "unknown_progress": progress["unknown_persistent_progress"] = Fixture.NativeField("unrelated", snapshot.GameTick); break;
            case "day":
            case "clock":
                var time = JsonNode.Parse(snapshot.State["time"].GetRawText())!;
                time[fault == "day" ? "total_days" : "time"]!["value"] = fault == "day" ? 113 : 500;
                snapshot.State["time"] = JsonSerializer.SerializeToElement(time, JsonOptions);
                break;
        }
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm, JsonOptions);
        snapshot.State["player"] = JsonSerializer.SerializeToElement(actor, JsonOptions);
        snapshot.State["world_progress"] = JsonSerializer.SerializeToElement(progress, JsonOptions);
    }

    [Theory]
    [InlineData("order")]
    [InlineData("source")]
    [InlineData("effective")]
    [InlineData("completion")]
    [InlineData("unverified")]
    [InlineData("count")]
    [InlineData("path_alias")]
    [InlineData("failed_consumed")]
    public void PrefixReceiptCannotPromoteUnverifiedOrDifferentNativeSteps(string fault)
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var transition = f.NativeTransition(marker, 3, failedSuffix: true, receiptChange: receipt =>
        {
            var step = receipt.StepResults[0];
            switch (fault)
            {
                case "order": step.QueueItemIndex = 1; break;
                case "source": step.SourceStateHash = new string('a', 64); break;
                case "effective": step.EffectiveQueueItem = JsonSerializer.SerializeToElement(new { invalid = true }); break;
                case "completion": step.SelectedQueueCandidateCompleted = true; break;
                case "unverified": step.PrimitiveVerificationStatus = "not_verified"; break;
                case "count": receipt.FinalPendingItemCount = 0; break;
                case "path_alias": step.AfterSnapshotPath = f.QueuePath; break;
                case "failed_consumed": receipt.StepResults[^1].PrimitiveVerificationStatus = "verified"; break;
            }
        });
        Assert.Throws<InvalidDataException>(() => f.Verify(marker, transition));
    }

    [Fact]
    public void ExistingVolatileStepCounterDoesNotBecomeAPlantingDependency()
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var transition = f.NativeTransition(marker, change: snapshot =>
        {
            var progress = JsonNode.Parse(snapshot.State["world_progress"].GetRawText())!;
            progress["game_state_query_unlock_state"]!["value"]!["players"]![0]!["stats"]!["stepsTaken"] = 500;
            snapshot.State["world_progress"] = JsonSerializer.SerializeToElement(progress, JsonOptions);
        });
        Assert.Equal(8, f.Verify(marker, transition).Proof.ConsumedQuantity);
    }

    [Theory]
    [InlineData("raw_stack")]
    [InlineData("seeds_sown")]
    [InlineData("unknown_progress")]
    public void FailedSuffixMustNotHideAnUnsettledNativeEffect(string fault)
    {
        using var f = new Fixture();
        var marker = f.Mark();
        var transition = f.NativeTransition(marker, 3, failedSuffix: true, suffixChange: snapshot => Change(snapshot, fault));
        Assert.Throws<InvalidDataException>(() => f.Verify(marker, transition));
    }

    [Fact]
    public void FirstSourceClockCannotDisagreeWithItsTransparentStateEvenIfAfterIsConsistent()
    {
        using var f = new Fixture(beforeClock: 700);
        var marker = f.Mark();
        var transition = f.NativeTransition(marker);
        var error = Assert.Throws<InvalidDataException>(() => f.Verify(marker, transition));
        Assert.Contains("step source/actor/tick/day/clock drifted", error.Message);
    }
}
