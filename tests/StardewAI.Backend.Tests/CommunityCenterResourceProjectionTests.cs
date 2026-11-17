using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed class CommunityCenterResourceProjectionTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly Type Projector = typeof(CropTeacherSeedPurchaseSupport).Assembly.GetType(
        "StardewAI.GoalConditionedBootstrap.FullShipmentRecoverySettlementBoundaryProjector", true)!;

    [Theory]
    [InlineData("inventory_accepts_reward")]
    [InlineData("inventory_item_total_before")]
    [InlineData("inventory_item_total_after")]
    [InlineData("action_status")]
    public void RewardInventoryDuplicatesChangeOnlyTheExpectedResourceView(string field)
    {
        var before = Source();
        var after = Mutate(before, center =>
        {
            var reward = center["bundle_rows"]![0]!["reward"]!.AsObject();
            if (field == "inventory_accepts_reward") reward[field] = false;
            else if (field == "action_status") reward[field] = "community_center_bundle_reward_inventory_unavailable";
            else reward[field] = 8;
        });
        Assert.False(Matches(before, after, resource: false));
        Assert.True(Matches(before, after, resource: true));
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("bundle_complete")]
    [InlineData("reward_available")]
    [InlineData("item_id")]
    [InlineData("qualified_item_id")]
    [InlineData("stack")]
    [InlineData("quality")]
    [InlineData("projection_status")]
    [InlineData("claim_mode")]
    [InlineData("unknown_reward")]
    [InlineData("unknown_center")]
    [InlineData("route_state")]
    public void RealRewardProgressAndUnknownFieldsRemainPersistent(string change)
    {
        var before = Source();
        var after = Mutate(before, center =>
        {
            var row = center["bundle_rows"]![0]!.AsObject();
            var reward = row["reward"]!.AsObject();
            reward["inventory_accepts_reward"] = false;
            switch (change)
            {
                case "completed": center["completed"] = true; break;
                case "bundle_complete": row["complete"] = true; break;
                case "reward_available": row["reward_available"] = false; break;
                case "stack": reward["stack"] = 1; break;
                case "quality": reward["quality"] = 4; break;
                case "unknown_reward": reward["future_capacity_or_progress"] = 1; break;
                case "unknown_center": center["future_progress"] = 1; break;
                case "route_state": center["route_state"] = "joja_locked"; break;
                default: reward[change] = "changed"; break;
            }
        });
        Assert.False(Matches(before, after, resource: false));
        Assert.False(Matches(before, after, resource: true));
    }

    private static SnapshotEnvelope Source()
    {
        var snapshot = MoneyCropTeacherFixture.CreateSnapshot("SeedAcquisitionRuntimeBoundary",
            MoneyCropTeacherFixture.CreateSnapshot("MoneyCropProductionSnapshot"));
        CropTeacherNativeSupportFixture.CompleteResources(snapshot, 640);
        var world = JsonNode.Parse(snapshot.State["world_progress"].GetRawText())!.AsObject();
        world["community_center"] = JsonNode.Parse("""
            {"status":"available","value":{"completed":false,"route_state":"community_center",
            "bundle_rows":[{"bundle_id":0,"complete":false,"reward_available":true,"reward":{
            "projection_status":"exact","projection_failure":"","item_id":"465","qualified_item_id":"(O)465",
            "runtime_type":"StardewValley.Object","quality":0,"stack":20,"claim_mode":"junimo_note_present_button",
            "inventory_item_total_before":0,"inventory_item_total_after":20,"inventory_accepts_reward":true,"action_status":"ready"}}]}}
            """);
        snapshot.State["world_progress"] = JsonSerializer.SerializeToElement(world, Json);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static SnapshotEnvelope Mutate(SnapshotEnvelope source, Action<JsonObject> change)
    {
        var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(source, Json), Json)!;
        var world = JsonNode.Parse(snapshot.State["world_progress"].GetRawText())!.AsObject();
        change(world["community_center"]!["value"]!.AsObject());
        snapshot.State["world_progress"] = JsonSerializer.SerializeToElement(world, Json);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static bool Matches(SnapshotEnvelope before, SnapshotEnvelope after, bool resource)
    {
        var method = Projector.GetMethod(resource ? "TryProjectExpectedResourceMutationInvariant" : "TryProject",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        object?[] a = [before, null], b = [after, null];
        Assert.True((bool)method.Invoke(null, a)!);
        Assert.True((bool)method.Invoke(null, b)!);
        return a[1]!.Equals(b[1]);
    }
}
