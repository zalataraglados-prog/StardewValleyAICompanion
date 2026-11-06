using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class CurrentMuseumCollectionIntakeTests
{
    private sealed record NativeTransitionFixture(CurrentMuseumCollectionIntake Intake, GoalRequirementSet Set,
        SnapshotEnvelope Before, SnapshotEnvelope After);

    private static NativeTransitionFixture NativeTransition(int initialStack = 2)
    {
        var (files, _, _) = TerminalFixture();
        Edit(files.Snapshot, root =>
        {
            root["state"]!["player"]!["inventory"]!["value"]![0]!["stack"] = initialStack;
            root["state"]!["player"]!["inventory"]!["value"]![0]!["quality"] = 0;
        }, snapshot: true);
        var before = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(files.Snapshot), Json)!;
        var after = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(files.Snapshot), Json)!;
        var state = JsonSerializer.SerializeToNode(after.State, Json)!;
        var museum = state["world_progress"]!["museum"]!["value"]!;
        museum["donated_count"] = 95;
        museum["missing_item_count"] = 0;
        museum["missing_item_ids"] = new JsonArray();
        museum["collection_complete"] = true;
        museum["complete_collection_achievement_received"] = true;
        museum["donatable_items"]![94]!["donated"] = true;
        state["world_progress"]!["achievements"]!["value"] = new JsonArray(5, 28);
        var slot = state["player"]!["inventory"]!["value"]![0]!;
        slot["stack"] = initialStack - 1;
        if (initialStack == 1)
        {
            slot["is_empty"] = true;
            slot["qualified_item_id"] = string.Empty;
        }
        after.GameTick = before.GameTick + 30;
        after.State = state.Deserialize<Dictionary<string, JsonElement>>(Json)!;
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        var inventory = JsonSerializer.Deserialize<AuthoritativeRequirementInventoryReport>(File.ReadAllText(files.Inventory), Json)!;
        return new(Build(files), inventory.RequirementSets.Single(), before, after);
    }

    private static void EditAfter(NativeTransitionFixture fixture, Action<JsonNode> change)
    {
        var state = JsonSerializer.SerializeToNode(fixture.After.State, Json)!;
        change(state);
        fixture.After.State = state.Deserialize<Dictionary<string, JsonElement>>(Json)!;
        fixture.After.StateHash = SnapshotHash.ComputeStateHash(fixture.After.State);
    }

    private static JsonElement ValidateNativeTransition(NativeTransitionFixture fixture)
    {
        var method = typeof(MuseumTerminalNativeJoinBuilder).GetMethod("ValidateTransition", BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            var result = method.Invoke(null, new object[] { fixture.Intake, fixture.Set, fixture.Before, fixture.After, "(O)96", 0 });
            return JsonSerializer.SerializeToElement(result, Json);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
