using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class NativeClearanceTerrainBoundaryTests
{
    [Theory]
    [InlineData("StardewValley.TerrainFeatures.Tree", false)]
    [InlineData("StardewValley.TerrainFeatures.Tree", true)]
    [InlineData("StardewValley.TerrainFeatures.Grass", false)]
    [InlineData("Custom.Terrain", true)]
    public void RemovesOnlyOriginalTargetWithoutMutatingInputs(string runtimeType, bool explicitType)
    {
        var (request, receipt, before, after) = Fixture();
        ChangeTerrain(before, field => field["value"]![1]!["type"] = runtimeType);
        if (explicitType) request.TargetRuntimeType = runtimeType;
        Bind(request, receipt, before);
        string originalBefore = JsonSerializer.Serialize(before), originalAfter = JsonSerializer.Serialize(after);
        string originalRequest = JsonSerializer.Serialize(request), originalReceipt = JsonSerializer.Serialize(receipt);
        var result = Read(request, receipt, before, after);
        Assert.Equal(10, result.TileX); Assert.Equal(11, result.TileY);
        Assert.Equal(runtimeType, result.RuntimeType);
        Assert.True(result.TerrainRemovalVerified);
        Assert.True(result.Consumption.StaminaContinuityVerified);
        Assert.Equal(1, result.Consumption.ToolCallCount);
        Assert.Equal(6L, result.Consumption.ResourceEvidence.ObservedPickupUnits);
        Assert.False(result.FullResourceVerified);
        Assert.False(result.ProducerProvenanceVerified);
        Assert.False(result.ResourceCreditAuthorized);
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
        Assert.Equal(originalBefore, JsonSerializer.Serialize(before));
        Assert.Equal(originalAfter, JsonSerializer.Serialize(after));
        Assert.Equal(originalRequest, JsonSerializer.Serialize(request));
        Assert.Equal(originalReceipt, JsonSerializer.Serialize(receipt));
        Assert.Equal(SnapshotHash.ComputeStateHash(before.State), before.StateHash);
        Assert.Equal(SnapshotHash.ComputeStateHash(after.State), after.StateHash);
        receipt.ClearanceToolCalls![0].StaminaAtEntry = 999f;
        ChangeTerrain(before, field => field["value"]![1]!["type"] = "Changed");
        Assert.Equal(runtimeType, result.RuntimeType);
        Assert.Equal(14f, result.Consumption.ResourceEvidence.ClearanceToolInputs!.Calls[0].StaminaAtEntry);
    }

    [Theory]
    [InlineData("absent-target")]
    [InlineData("duplicate-target")]
    [InlineData("duplicate-foreign-before")]
    [InlineData("duplicate-foreign-after")]
    [InlineData("wrong-x")]
    [InlineData("wrong-y")]
    [InlineData("missing-x")]
    [InlineData("missing-y")]
    [InlineData("negative-x")]
    [InlineData("negative-y")]
    [InlineData("wrong-option")]
    [InlineData("wrong-type")]
    [InlineData("blank-request-type")]
    [InlineData("missing-type")]
    [InlineData("empty-type")]
    [InlineData("wrong-type-kind")]
    [InlineData("no-removal")]
    [InlineData("replacement")]
    [InlineData("extra-removal")]
    [InlineData("foreign-metadata")]
    [InlineData("foreign-coordinate")]
    [InlineData("foreign-order")]
    [InlineData("envelope-metadata")]
    [InlineData("availability-change")]
    [InlineData("missing-before")]
    [InlineData("missing-after")]
    [InlineData("unreadable-before")]
    [InlineData("unreadable-after")]
    [InlineData("null-value")]
    [InlineData("wrong-array")]
    [InlineData("nonobject-row")]
    [InlineData("null-row")]
    [InlineData("missing-coordinate")]
    [InlineData("wrong-coordinate-kind")]
    [InlineData("energy-drift")]
    public void RefusesAnythingBeyondOneExactTargetRemoval(string change)
    {
        var (request, receipt, before, after) = Fixture();
        switch (change)
        {
            case "absent-target": ChangeTerrain(before, field => Rows(field).RemoveAt(1)); break;
            case "duplicate-target": ChangeTerrain(before, field => Rows(field).Add(Rows(field)[1]!.DeepClone())); break;
            case "duplicate-foreign-before": ChangeTerrain(before, field => Rows(field).Add(Rows(field)[0]!.DeepClone())); break;
            case "duplicate-foreign-after": ChangeTerrain(after, field => Rows(field).Add(Rows(field)[0]!.DeepClone())); break;
            case "wrong-x": request.TargetTileX = 99; break;
            case "wrong-y": request.TargetTileY = 99; break;
            case "missing-x": request.TargetTileX = null; break;
            case "missing-y": request.TargetTileY = null; break;
            case "negative-x": request.TargetTileX = -1; break;
            case "negative-y": request.TargetTileY = -1; break;
            case "wrong-option": request.OptionId = "executor.move_to_tile"; break;
            case "wrong-type": request.TargetRuntimeType = "Other.Tree"; break;
            case "blank-request-type": request.TargetRuntimeType = " "; break;
            case "missing-type": ChangeTerrain(before, field => Rows(field)[1]!.AsObject().Remove("type")); break;
            case "empty-type": ChangeTerrain(before, field => Rows(field)[1]!["type"] = " "); break;
            case "wrong-type-kind": ChangeTerrain(before, field => Rows(field)[1]!["type"] = 42); break;
            case "no-removal": ChangeTerrain(after, field => Rows(field).Add(Target())); break;
            case "replacement": ChangeTerrain(after, field => { var row = Target(); row["type"] = "Replacement"; Rows(field).Add(row); }); break;
            case "extra-removal": ChangeTerrain(after, field => Rows(field).RemoveAt(0)); break;
            case "foreign-metadata": ChangeTerrain(after, field => Rows(field)[0]!["unknown"]!["nested"] = 8); break;
            case "foreign-coordinate": ChangeTerrain(after, field => Rows(field)[0]!["tile_x"] = 99); break;
            case "foreign-order": ChangeTerrain(after, field => { var row = Rows(field)[0]!; Rows(field).RemoveAt(0); Rows(field).Add(row); }); break;
            case "envelope-metadata": ChangeTerrain(after, field => field["unknown_envelope"] = "changed"); break;
            case "availability-change": ChangeTerrain(after, field => field["status"] = "derived"); break;
            case "missing-before": before.State.Remove("farm"); break;
            case "missing-after": after.State.Remove("farm"); break;
            case "unreadable-before": ChangeTerrain(before, field => field["status"] = "unavailable"); break;
            case "unreadable-after": ChangeTerrain(after, field => field["status"] = "unavailable"); break;
            case "null-value": ChangeTerrain(after, field => field["value"] = null); break;
            case "wrong-array": ChangeTerrain(after, field => field["value"] = new JsonObject()); break;
            case "nonobject-row": ChangeTerrain(before, field => Rows(field).Add(42)); break;
            case "null-row": ChangeTerrain(after, field => Rows(field).Add((JsonNode?)null)); break;
            case "missing-coordinate": ChangeTerrain(before, field => Rows(field)[0]!.AsObject().Remove("tile_x")); break;
            case "wrong-coordinate-kind": ChangeTerrain(after, field => Rows(field)[0]!["tile_y"] = "1"); break;
            case "energy-drift":
                var player = JsonNode.Parse(after.State["player"].GetRawText())!;
                player["energy"]!["value"] = 13f;
                after.State["player"] = JsonSerializer.SerializeToElement(player); break;
        }
        Rehash(before); Rehash(after); Bind(request, receipt, before);
        Assert.Throws<InvalidDataException>(() => Read(request, receipt, before, after));
    }

    private static NativeClearanceTerrainSummary Read(TrainingExecutionRequest request, ActionOriginObservationReceipt receipt,
        SnapshotEnvelope before, SnapshotEnvelope after) => NativeOutputObservationBoundary.ReadClearanceTerrain(request, receipt, before, after, "Farm");
    private static (TrainingExecutionRequest Request, ActionOriginObservationReceipt Receipt, SnapshotEnvelope Before, SnapshotEnvelope After) Fixture()
    {
        var fixture = NativeClearanceConsumptionBoundaryTests.Fixture();
        fixture.Request.OptionId = "executor.clear_obstacle";
        fixture.Request.TargetTileX = 10; fixture.Request.TargetTileY = 11;
        SetTerrain(fixture.Before, new JsonArray(Foreign(1), Target(), Foreign(2)));
        SetTerrain(fixture.After, new JsonArray(Foreign(1), Foreign(2)));
        Bind(fixture.Request, fixture.Receipt, fixture.Before);
        return fixture;
    }
    private static JsonObject Target() => new() { ["tile_x"] = 10, ["tile_y"] = 11, ["type"] = "StardewValley.TerrainFeatures.Tree", ["unknown_target"] = "retained until removed" };
    private static JsonObject Foreign(int x) => new() { ["tile_x"] = x, ["tile_y"] = 3, ["type"] = "Foreign.Terrain", ["unknown"] = new JsonObject { ["nested"] = 7 } };
    private static JsonArray Rows(JsonObject field) => field["value"]!.AsArray();
    private static void SetTerrain(SnapshotEnvelope snapshot, JsonArray rows)
    {
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(new JsonObject
        {
            ["terrain_features"] = new JsonObject { ["status"] = "available", ["value"] = rows, ["unknown_envelope"] = "preserve" }
        });
        Rehash(snapshot);
    }
    private static void ChangeTerrain(SnapshotEnvelope snapshot, Action<JsonObject> change)
    {
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!;
        change(farm["terrain_features"]!.AsObject());
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
        Rehash(snapshot);
    }
    private static void Rehash(SnapshotEnvelope snapshot) => snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
    private static void Bind(TrainingExecutionRequest request, ActionOriginObservationReceipt receipt, SnapshotEnvelope before) =>
        request.BeforeStateHash = receipt.BeforeStateHash = before.StateHash;
}
