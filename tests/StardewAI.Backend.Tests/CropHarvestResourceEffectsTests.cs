using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class CropHarvestResourceEffectsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ComposesOriginalCropSkillStockAndPersistentChecksWithoutProductionCredit(bool newSlot)
    {
        var f = Fixture(newSlot);
        var original = JsonSerializer.Serialize(new[] { f.Before, f.After });
        var result = Read(f.Before, f.After, f.Request, "(O)388");
        Assert.True(result.GetProperty("SpecificPersistentResourceBoundaryVerified").GetBoolean());
        Assert.Equal(6, result.GetProperty("Stock").GetProperty("Inventory").GetProperty("QuantityIncrease").GetInt32());
        foreach (var name in new[] { "ProducerProvenanceVerified", "ResourceCreditAuthorized", "NativeOutcomeVerified", "FormalProductTrainingAuthorized" })
            Assert.False(result.GetProperty(name).GetBoolean());
        Assert.Equal(original, JsonSerializer.Serialize(new[] { f.Before, f.After }));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("foreign-save")]
    [InlineData("target-output")]
    [InlineData("unrelated-state")]
    [InlineData("day")]
    [InlineData("crop-retained")]
    [InlineData("skill")]
    public void RefusesCompositionWithWrongBindingOrUnexplainedState(string change)
    {
        var f = Fixture(false);
        if (change == "source") f.Request.BeforeStateHash = "foreign";
        if (change == "foreign-save") f.After.SaveId.Value = "foreign";
        if (change == "unrelated-state") Put(f.After, "farm", "future_persistent", JsonValue.Create("changed")!);
        if (change == "day") Put(f.After, "time", "total_days", JsonValue.Create(2)!);
        if (change == "crop-retained") Put(f.After, "farm", "crops", Field(f.Before, "farm", "crops").DeepClone());
        if (change == "skill")
        {
            var details = Field(f.After, "player", "skills_detail"); details["profession_ids"]!.AsArray().Add(12);
            Put(f.After, "player", "skills_detail", details);
        }
        f.After.StateHash = SnapshotHash.ComputeStateHash(f.After.State);
        Assert.Throws<InvalidDataException>(() => Read(f.Before, f.After, f.Request, change == "target-output" ? "(O)190" : "(O)388"));
    }

    private static JsonElement Read(SnapshotEnvelope before, SnapshotEnvelope after, TrainingExecutionRequest request, string output)
    {
        var type = typeof(CropTeacherWateringMaintenance).Assembly.GetType("StardewAI.GoalConditionedBootstrap.CropSeedResourceBoundary")!;
        try { return JsonSerializer.SerializeToElement(type.GetMethod("VerifyHarvestResources", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [before, after, request, output, 1])); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    private static (TrainingExecutionRequest Request, SnapshotEnvelope Before, SnapshotEnvelope After) Fixture(bool newSlot)
    {
        var f = NativeCropHarvestSkillBoundaryTests.Fixture(90, mirrors: false);
        var stock = NativePickupInventoryBoundaryTests.ResourceFixture(newSlot);
        foreach (var (target, source) in new[] { (f.Before, stock.Before), (f.After, stock.After) })
        {
            foreach (var name in new[] { "inventory", "inventory_capacity", "money", "shop_currency_balances", "multiplayer_wallet", "seed_inventory" })
                Put(target, "player", name, Field(source, "player", name));
            Put(target, "farm", "material_inventory_graph", Field(source, "farm", "material_inventory_graph"));
            Put(target, "world_progress", "marriage_house", Field(source, "world_progress", "marriage_house"));
            Put(target, "farm", "crop_catalog", new JsonArray(new JsonObject { ["seed_id"] = "474", ["needs_watering"] = true, ["regrow_days"] = null }));
        }
        var crops = Field(f.Before, "farm", "crops"); var row = crops[0]!;
        row["harvest_source_seed_id"] = "474"; row["harvest_item_qualified_id"] = "(O)388";
        row["phase_days"] = new JsonArray(12, 99999); row["current_phase"] = 1; row["phase_count"] = 2;
        row["day_of_current_phase"] = 0; row["fully_grown"] = false; row["days_until_next_harvest_if_watered"] = 0;
        row["regrow_days"] = null; row["watered"] = true;
        Put(f.Before, "farm", "crops", crops); Put(f.After, "farm", "crops", new JsonArray());
        f.Before.StateHash = SnapshotHash.ComputeStateHash(f.Before.State); f.After.StateHash = SnapshotHash.ComputeStateHash(f.After.State);
        f.Request.BeforeStateHash = f.Before.StateHash;
        return f;
    }

    private static JsonNode Field(SnapshotEnvelope source, string domain, string field) =>
        JsonNode.Parse(source.State[domain].GetProperty(field).GetProperty("value").GetRawText())!;

    private static void Put(SnapshotEnvelope target, string domain, string field, JsonNode value)
    {
        var fields = JsonNode.Parse(target.State[domain].GetRawText())!;
        fields[field] = new JsonObject { ["status"] = "available", ["value"] = value };
        target.State[domain] = JsonSerializer.SerializeToElement(fields);
    }
}
