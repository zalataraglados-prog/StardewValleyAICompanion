using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class SignPlacementMainlineTests
{
    private const string NativeContract =
        "Utility.playerCanPlaceItemHere->Utility.tryToPlaceItem->Object.placementAction(sign_item_or_TextSign)->location.objects";
    private const string LayoutBasis =
        "native_legal_range+collision_grid_virtual_occupancy_bfs+protected_endpoint_and_storage_access";

    [Theory]
    [InlineData("37", "(BC)37", "display_item_sign", "StardewValley.Objects.Sign", false)]
    [InlineData("TextSign", "(BC)TextSign", "text_sign", "StardewValley.Object", true)]
    public void BothNativeSignBranchesCompileToOneSharedPlacement(
        string itemId, string qid, string kind, string targetType, bool showNext)
    {
        var snapshot = Snapshot(itemId, qid, kind, targetType, showNext);
        var queue = new ActionQueueCompiler().Compile(Request(snapshot, itemId, qid, kind, targetType, showNext), snapshot);

        Assert.True(queue.Status == "pending", string.Join(",", queue.Items.SelectMany(row => row.BlockingReasons)));
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("place_sign", step.StepType);
        Assert.Equal("Farm(12,10):slot2:" + qid, step.Target);
        Assert.Contains("sign_state.placement_kind=" + kind, step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("placement_projection_fingerprint", "stale", "place_sign_projection_fingerprint_drifted")]
    [InlineData("inventory_stack_before", "1", "place_sign_inventory_data_or_branch_identity_drifted")]
    [InlineData("placement_kind", "text_sign", "place_sign_exact_empty_sign_identity_required")]
    [InlineData("reachable_tile_count_after_placement", "398", "place_sign_route_or_access_layout_drifted")]
    public void StaleInventoryBranchAndLayoutBindingsFailClosed(string parameter, string value, string reason)
    {
        var snapshot = Snapshot("37", "(BC)37", "display_item_sign", "StardewValley.Objects.Sign", false);
        var request = Request(snapshot, "37", "(BC)37", "display_item_sign", "StardewValley.Objects.Sign", false);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == parameter)).Value = value;

        Assert.Contains(reason, Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items).BlockingReasons);
    }

    [Fact]
    public void PayloadAndPurposeCannotBeHiddenInsidePlacement()
    {
        var snapshot = Snapshot("TextSign", "(BC)TextSign", "text_sign", "StardewValley.Object", true);
        var request = Request(snapshot, "TextSign", "(BC)TextSign", "text_sign", "StardewValley.Object", true);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == "expected_sign_text")).Value = "hidden text";
        request.Actions[0].Parameters = request.Actions[0].Parameters.Where(row => row.Name != "sign_layout_reason").ToArray();

        var reasons = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items).BlockingReasons;
        Assert.Contains("place_sign_exact_empty_sign_identity_required", reasons);
        Assert.Contains("place_sign_layout_reason_required", reasons);
    }

    [Fact]
    public void PlacementUsesOneSharedRuntimeAndDenominatorMapsBothBranches()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.place_sign");
        Assert.True(capability.HarnessDispatchSupported);
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.False(PendingSemanticActionCatalog.TryGet("executor.place_sign", out _));
        Assert.True(PendingSemanticActionCatalog.TryGet("executor.set_sign_display_item", out _));
        Assert.True(PendingSemanticActionCatalog.TryGet("executor.edit_text_sign", out _));
        Assert.Equal(ImplementationEngineIds.PlacementLayout,
            OptionImplementationCatalog.GetRequired("executor.place_sign").PrimaryEngineId);

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.SignPlacement.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.SignPlacement.cs"));
        var denominator = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.KnowledgeCompiler", "NativeActionBranchCatalogBuilder.cs"));
        Assert.Contains("PlaceInventoryObjectNative", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("location.objects.Add", runtime, StringComparison.Ordinal);
        Assert.Contains("sign_item_or_TextSign", runtime, StringComparison.Ordinal);
        Assert.Contains("live Data/BigCraftables", projection, StringComparison.Ordinal);
        Assert.Contains("payload_policy", projection, StringComparison.Ordinal);
        Assert.Contains("literals.Contains(\"sign_item\"", denominator, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(
        SnapshotEnvelope snapshot, string itemId, string qid, string kind, string targetType, bool showNext) => new()
    {
        ModelOutputId = "sign-placement-test",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "test",
        ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef { ActorId = "training_farmer.test", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
        Actions = new[]
        {
            new SmallModelAction
            {
                ActionId = "place-sign",
                OptionId = "executor.place_sign",
                Rationale = "purpose-bound empty sign layout",
                Parameters = new[]
                {
                    P("target_location", "Farm"), P("target_tile_x", "12"), P("target_tile_y", "10"),
                    P("stand_tile_x", "11"), P("stand_tile_y", "10"), P("inventory_slot_index", "2"),
                    P("inventory_stack_before", "2"), P("item_id", itemId), P("qualified_item_id", qid),
                    P("inventory_runtime_type", "StardewValley.Object"), P("target_runtime_type", targetType),
                    P("placement_kind", kind), P("expected_passable", "false"),
                    P("expected_display_item_empty", "true"), P("expected_display_type", "0"),
                    P("expected_sign_text", string.Empty), P("expected_show_next_index", showNext.ToString().ToLowerInvariant()),
                    P("placement_projection_fingerprint", "sign-fingerprint"),
                    P("baseline_reachable_tile_count", "400"), P("reachable_tile_count_after_placement", "399"),
                    P("protected_access_group_count", "0"), P("route_distance_tiles", "1"),
                    P("layout_projection_basis", LayoutBasis), P("sign_layout_reason", "label_machine_or_storage_area"),
                    P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(
        string itemId, string qid, string kind, string targetType, bool showNext)
    {
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"Farm","status":"available"},
            "tile_x":{"value":10,"status":"available"},"tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[{"slot_index":2,"qualified_item_id":"{{{qid}}}","stack":2}],"status":"available"},
            "sign_placement":{"value":{"static_projection_fingerprint":"sign-fingerprint","rows":[{
              "inventory_slot_index":2,"item_id":"{{{itemId}}}","qualified_item_id":"{{{qid}}}","stack":2,
              "inventory_runtime_type":"StardewValley.Object","expected_placed_runtime_type":"{{{targetType}}}",
              "placement_kind":"{{{kind}}}","expected_passable":false,"expected_display_item_empty":true,
              "expected_display_type":0,"expected_sign_text":"","expected_show_next_index":{{{showNext.ToString().ToLowerInvariant()}}},
              "locations":[{"location_id":"Farm","placement_probe_status":"native_legal_tiles_available",
                "static_legal_tile_ranges":[{"y":10,"start_x":12,"end_x":12}]}]
            }]},"status":"available"}
          },
          "current_location":{"objects":{"value":[],"status":"available"},
            "chests":{"value":{"schema_version":"storage_infrastructure.v1","status":"available","scope_location_id":"Farm","access_points":[]},"status":"available"}},
          "locations":{"collision_grid":{"value":{"location_id":"Farm","width":20,"height":20,"notable_tiles":[]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1", StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1,
            RealTimestamp = "2026-08-25T00:00:00Z", Completeness = "complete", State = state
        };
    }

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root not found.");
    }
}
