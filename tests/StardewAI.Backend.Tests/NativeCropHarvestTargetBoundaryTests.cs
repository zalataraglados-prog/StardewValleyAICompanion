using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class NativeCropHarvestTargetBoundaryTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(4)]
    public void HarvestReusesOriginalOneShotOrRegrowTransitionAndPreservesAllOtherCrops(int? regrow)
    {
        var f = Fixture(regrow);
        var original = JsonSerializer.Serialize(new[] { f.Before, f.After });
        Read(f.Request, f.Before, f.After);
        Assert.Equal(original, JsonSerializer.Serialize(new[] { f.Before, f.After }));
    }

    [Theory]
    [InlineData("not-ready", null)]
    [InlineData("dead", null)]
    [InlineData("request-hash", null)]
    [InlineData("wrong-target", null)]
    [InlineData("one-shot-retained", null)]
    [InlineData("other-crop", null)]
    [InlineData("other-crop-removed", null)]
    [InlineData("envelope", null)]
    [InlineData("missing-catalog", null)]
    [InlineData("duplicate-catalog", null)]
    [InlineData("regrow-removed", 4)]
    [InlineData("wrong-seed", 4)]
    [InlineData("still-ready", 4)]
    [InlineData("countdown", 4)]
    [InlineData("derived-countdown", 4)]
    [InlineData("target-unknown", 4)]
    [InlineData("catalog-regrow", 4)]
    [InlineData("phase", 4)]
    public void HarvestRejectsWrongTargetOrUnexplainedCropMutation(string change, int? regrow)
    {
        var f = Fixture(regrow);
        switch (change)
        {
            case "not-ready": Edit(f.Before, "crops", rows => rows[0]!["ready_for_harvest"] = false); break;
            case "dead": Edit(f.Before, "crops", rows => rows[0]!["dead"] = true); break;
            case "request-hash": f.Request.BeforeStateHash = "foreign"; break;
            case "wrong-target": f.Request.TargetTileX = 20; break;
            case "one-shot-retained": Edit(f.After, "crops", rows => rows.AsArray().Insert(0, Crop(null, 10))); break;
            case "other-crop": Edit(f.After, "crops", rows => rows[0]!["future_persistent"] = "changed"); break;
            case "other-crop-removed": Edit(f.After, "crops", rows => rows.AsArray().Clear()); break;
            case "envelope":
                var farm = JsonNode.Parse(f.After.State["farm"].GetRawText())!;
                farm["crops"]!["future_semantic"] = "changed";
                f.After.State["farm"] = JsonSerializer.SerializeToElement(farm); break;
            case "missing-catalog": Edit(f.Before, "crop_catalog", rows => rows.AsArray().Clear()); break;
            case "duplicate-catalog": Edit(f.Before, "crop_catalog", rows => rows.AsArray().Add(rows[0]!.DeepClone())); break;
            case "regrow-removed": Edit(f.After, "crops", rows => rows.AsArray().RemoveAt(0)); break;
            case "wrong-seed": Edit(f.After, "crops", rows => rows[0]!["harvest_source_seed_id"] = "475"); break;
            case "still-ready": Edit(f.After, "crops", rows => rows[0]!["ready_for_harvest"] = true); break;
            case "countdown": Edit(f.After, "crops", rows => rows[0]!["day_of_current_phase"] = 3); break;
            case "derived-countdown": Edit(f.After, "crops", rows => rows[0]!["days_until_next_harvest_if_watered"] = 3); break;
            case "target-unknown": Edit(f.After, "crops", rows => rows[0]!["future_persistent"] = "changed"); break;
            case "catalog-regrow": Edit(f.Before, "crop_catalog", rows => rows[0]!["regrow_days"] = 3); break;
            case "phase": Edit(f.After, "crops", rows => rows[0]!["current_phase"] = 0); break;
        }
        f.Before.StateHash = SnapshotHash.ComputeStateHash(f.Before.State);
        f.After.StateHash = SnapshotHash.ComputeStateHash(f.After.State);
        if (change != "request-hash") f.Request.BeforeStateHash = f.Before.StateHash;
        Assert.Throws<InvalidDataException>(() => Read(f.Request, f.Before, f.After));
    }

    private static void Read(TrainingExecutionRequest request, SnapshotEnvelope before, SnapshotEnvelope after)
    {
        var type = typeof(CropTeacherWateringMaintenance).Assembly.GetType("StardewAI.GoalConditionedBootstrap.CropSeedResourceBoundary")!;
        try { _ = type.GetMethod("VerifyCropHarvestTarget", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [before, after, request]); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    private static (TrainingExecutionRequest Request, SnapshotEnvelope Before, SnapshotEnvelope After) Fixture(int? regrow)
    {
        var before = NativeWildTreeClearanceBoundaryTests.Fixture(0).Before;
        Put(before, "crops", new JsonArray(Crop(regrow, 10), Crop(regrow, 20)));
        Put(before, "crop_catalog", new JsonArray(new JsonObject { ["seed_id"] = "474", ["needs_watering"] = true, ["regrow_days"] = regrow }));
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before))!;
        after.GameTick += 2;
        Edit(after, "crops", rows =>
        {
            if (!regrow.HasValue) rows.AsArray().RemoveAt(0);
            else
            {
                rows[0]!["fully_grown"] = true; rows[0]!["day_of_current_phase"] = regrow.Value;
                rows[0]!["ready_for_harvest"] = false; rows[0]!["days_until_next_harvest_if_watered"] = regrow.Value;
            }
        });
        before.StateHash = SnapshotHash.ComputeStateHash(before.State); after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        return (new TrainingExecutionRequest { OptionId = "executor.harvest_crop", LocationId = "Farm", TargetTileX = 10,
            TargetTileY = 11, BeforeStateHash = before.StateHash }, before, after);
    }

    private static JsonObject Crop(int? regrow, int x) => new()
    {
        ["location_id"] = "Farm", ["tile_x"] = x, ["tile_y"] = 11, ["harvest_source_seed_id"] = "474", ["harvest_item_qualified_id"] = "(O)190",
        ["phase_days"] = new JsonArray(12, 99999), ["current_phase"] = 1, ["phase_count"] = 2, ["day_of_current_phase"] = 0,
        ["dead"] = false, ["fully_grown"] = false, ["ready_for_harvest"] = true, ["days_until_next_harvest_if_watered"] = 0,
        ["regrow_days"] = regrow, ["watered"] = true, ["future_persistent"] = "retained"
    };

    private static void Put(SnapshotEnvelope snapshot, string field, JsonNode value)
    {
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!;
        farm[field] = new JsonObject { ["status"] = "available", ["value"] = value };
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
    }

    private static void Edit(SnapshotEnvelope snapshot, string field, Action<JsonNode> edit)
    {
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!;
        edit(farm[field]!["value"]!); snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
    }
}
