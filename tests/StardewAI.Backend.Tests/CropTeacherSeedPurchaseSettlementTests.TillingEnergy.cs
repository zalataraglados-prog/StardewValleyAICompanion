using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(0, false, false)]
    [InlineData(3, false, false)]
    [InlineData(10, false, false)]
    [InlineData(30, false, false)]
    [InlineData(0, true, false)]
    [InlineData(0, false, true)]
    [InlineData(30, false, true)]
    public void TillingEnergyBindsNativeInputsAndOnlyNormalizesVerifiedEnergy(int level, bool efficient, bool protection)
    {
        var (before, after, observation) = TillingEnergyFixture(level, efficient, protection);
        ChangeState(after, "player", player => player["money"]!["value"] = 123);
        var originals = new[] { before, after, observation }.Select(snapshot => JsonSerializer.Serialize(snapshot, JsonOptions)).ToArray();
        var view = ProjectTillingEnergy(before, after, observation);
        Assert.Equal(originals, new[] { before, after, observation }.Select(snapshot => JsonSerializer.Serialize(snapshot, JsonOptions)).ToArray());
        Assert.Equal(270f, view.State["player"].GetProperty("energy").GetProperty("value").GetSingle());
        Assert.Equal(123, view.State["player"].GetProperty("money").GetProperty("value").GetInt32());
        Assert.True(string.IsNullOrEmpty(view.StateHash));
    }

    [Theory]
    [InlineData("missing_efficiency")]
    [InlineData("mistyped_efficiency")]
    [InlineData("cost")]
    [InlineData("tool_slot")]
    [InlineData("tool_id")]
    [InlineData("first_hoe")]
    [InlineData("duplicate_slot")]
    [InlineData("negative_slot")]
    [InlineData("missing_farming")]
    [InlineData("duplicate_farming")]
    [InlineData("negative_level")]
    [InlineData("negative_maximum")]
    [InlineData("missing_buffs")]
    [InlineData("duplicate_buff")]
    [InlineData("protection_changed")]
    [InlineData("wrong_delta")]
    [InlineData("one_ulp")]
    [InlineData("observation_tick")]
    [InlineData("observation_actor")]
    [InlineData("observation_hash")]
    [InlineData("after_save")]
    public void TillingEnergyRejectsUnboundNativeInputsOrUnexpectedCost(string fault)
    {
        var (before, after, observation) = TillingEnergyFixture();
        void Context(Action<JsonObject> change) => ChangeState(observation, "current_location", node =>
            change(node["site_preparation_context"]!["value"]!.AsObject()));
        void Inventory(Action<JsonArray> change) => ChangeState(before, "player", node => change(node["inventory"]!["value"]!.AsArray()));
        void Skills(Action<JsonArray> change) => ChangeState(before, "player", node => change(node["skills_detail"]!["value"]!["skills"]!.AsArray()));
        switch (fault)
        {
            case "missing_efficiency": Context(node => node.Remove("hoe_is_efficient")); break;
            case "mistyped_efficiency": Context(node => node["hoe_is_efficient"] = "false"); break;
            case "cost": Context(node => node["hoe_energy_cost_per_single_tile_action"] = 1d); break;
            case "tool_slot": Context(node => node["hoe_tool_slot_index"] = 2); break;
            case "tool_id": Context(node => node["hoe_tool_qualified_item_id"] = "(T)Other"); break;
            case "first_hoe": Inventory(rows => { var hoe = rows[0]!.DeepClone(); hoe["slot_index"] = 0; rows.Add(hoe); }); break;
            case "duplicate_slot": Inventory(rows => rows.Add(rows[0]!.DeepClone())); break;
            case "negative_slot": Inventory(rows => rows.Add(new JsonObject { ["slot_index"] = -1 })); break;
            case "missing_farming": Skills(rows => rows.Clear()); break;
            case "duplicate_farming": Skills(rows => rows.Add(rows[0]!.DeepClone())); break;
            case "negative_level": Skills(rows => rows[0]!["effective_level"] = -1); break;
            case "negative_maximum": ChangeState(before, "player", node => node["max_energy"]!["value"] = -1); break;
            case "missing_buffs": ChangeState(before, "current_location", node => node.AsObject().Remove("statue_blessing")); break;
            case "duplicate_buff": ChangeState(before, "current_location", node => node["statue_blessing"]!["value"]!["active_blessing_buffs"] =
                new JsonArray(new JsonObject { ["buff_id"] = "statue_of_blessings_2" }, new JsonObject { ["buff_id"] = "statue_of_blessings_2" })); break;
            case "protection_changed": ChangeState(after, "current_location", node => node["statue_blessing"]!["value"]!["active_blessing_buffs"] =
                new JsonArray(new JsonObject { ["buff_id"] = "statue_of_blessings_2" })); break;
            case "wrong_delta": ChangeState(after, "player", node => node["energy"]!["value"] = 269f); break;
            case "one_ulp": ChangeState(after, "player", node => node["energy"]!["value"] = float.BitIncrement(268f)); break;
            case "observation_tick": observation.GameTick++; break;
            case "observation_actor": observation.PlayerId.Value = "foreign"; break;
            case "observation_hash": observation.StateHash = "invalid"; break;
            case "after_save": after.SaveId.Value = "foreign"; break;
        }
        if (fault != "observation_hash") observation.StateHash = SnapshotHash.ComputeStateHash(observation.State);
        before.StateHash = SnapshotHash.ComputeStateHash(before.State);
        Assert.Throws<InvalidDataException>(() => ProjectTillingEnergy(before, after, observation));
    }

    private static SnapshotEnvelope ProjectTillingEnergy(SnapshotEnvelope before, SnapshotEnvelope after, SnapshotEnvelope observation)
    {
        var method = typeof(CropTeacherSupportingRoute).Assembly.GetType("StardewAI.GoalConditionedBootstrap.CropSeedResourceBoundary")!
            .GetMethod("ProjectVerifiedTillingEnergy", BindingFlags.NonPublic | BindingFlags.Static)!;
        try { return (SnapshotEnvelope)method.Invoke(null, new object[] { before, after, observation, 1, "(T)Hoe" })!; }
        catch (TargetInvocationException error) when (error.InnerException is InvalidDataException invalid) { throw invalid; }
    }

    private static (SnapshotEnvelope Before, SnapshotEnvelope After, SnapshotEnvelope Observation) TillingEnergyFixture(
        int level = 0, bool efficient = false, bool protection = false)
    {
        var (before, after) = TillingGeometryFixture();
        foreach (var snapshot in new[] { before, after })
        {
            snapshot.BridgeVersion = "till.energy.fixture"; snapshot.GameTick = snapshot == before ? 10 : 20;
            ChangeState(snapshot, "player", player =>
            {
                player["energy"] = JsonSerializer.SerializeToNode(new { status = "available", value = snapshot == before ? 270f :
                    NativeHoeSingleTileEnergy.ExpectedAfter(270f, 270, level, efficient, protection) });
                player["max_energy"] = JsonSerializer.SerializeToNode(new { status = "available", value = 270 });
                player["inventory"] = JsonSerializer.SerializeToNode(new { status = "available", value = new[] {
                    new { slot_index = 1, runtime_type = "StardewValley.Tools.Hoe", qualified_item_id = "(T)Hoe" } } });
                player["skills_detail"] = JsonSerializer.SerializeToNode(new { status = "available", value = new { skills = new[] {
                    new { skill_id = "farming", skill_index = 0, effective_level = level } } } });
            });
            snapshot.State["current_location"] = JsonSerializer.SerializeToElement(new { statue_blessing = new { status = "available",
                value = new { active_blessing_buffs = protection ? new[] { new { buff_id = "statue_of_blessings_2" } } : Array.Empty<object>() } } });
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        }
        var observation = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(before, JsonOptions), JsonOptions)!;
        ChangeState(observation, "current_location", location => location["site_preparation_context"] = JsonSerializer.SerializeToNode(new
        {
            status = "available", value = new { schema_version = "farm_site_preparation_context.v2",
                projection_status = "complete_bounded_current_farm_region", location_id = "Farm", hoe_tool_slot_index = 1,
                hoe_tool_qualified_item_id = "(T)Hoe", hoe_is_efficient = efficient,
                hoe_energy_cost_per_single_tile_action = efficient ? 0d : Math.Max(0d, 2d - level * .1d) }
        }));
        observation.StateHash = SnapshotHash.ComputeStateHash(observation.State);
        return (before, after, observation);
    }
}
