using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class SignDisplayItemMainlineTests
{
    private const string NativeContract =
        "GameLocation.checkAction->Sign.checkForAction(CurrentItem.getOne,no_inventory_consumption)->displayItem/displayType";

    [Theory]
    [InlineData("StardewValley.Object", "(O)388", 1)]
    [InlineData("StardewValley.Object", "(BC)37", 3)]
    [InlineData("StardewValley.Objects.Hat", "(H)0", 2)]
    [InlineData("StardewValley.Objects.Ring", "(O)516", 4)]
    [InlineData("StardewValley.Objects.Furniture", "(F)1308", 5)]
    [InlineData("StardewValley.Tools.Axe", "(T)Axe", 1)]
    public void EveryNativeDisplayTypeBranchCompilesToOneSharedInteraction(string sourceType, string qid, int displayType)
    {
        var snapshot = Snapshot(sourceType, qid, displayType, replace: false);
        var queue = new ActionQueueCompiler().Compile(Request(snapshot, sourceType, qid, displayType, replace: false), snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("set_sign_display_item", step.StepType);
        Assert.Equal("Farm(12,10):slot2:" + qid, step.Target);
        Assert.Contains("stack_and_state=unchanged", step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("source_state_sha256", "stale", "set_sign_display_item_source_projection_drifted")]
    [InlineData("target_state_sha256", "stale", "set_sign_display_item_target_projection_drifted")]
    [InlineData("target_projection_fingerprint", "stale", "set_sign_display_item_target_projection_drifted")]
    [InlineData("previous_display_item_state_sha256", "stale", "set_sign_display_item_previous_payload_drifted")]
    [InlineData("target_runtime_type", "StardewValley.Object", "set_sign_display_item_exact_base_sign_required")]
    [InlineData("stand_tile_x", "9", "set_sign_display_item_adjacent_stand_geometry_invalid")]
    public void StaleOrInvalidBindingsFailClosed(string parameter, string value, string reason)
    {
        var snapshot = Snapshot("StardewValley.Objects.Ring", "(O)516", 4, replace: true);
        var request = Request(snapshot, "StardewValley.Objects.Ring", "(O)516", 4, replace: true);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == parameter)).Value = value;

        Assert.Contains(reason, Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items).BlockingReasons);
    }

    [Fact]
    public void ExistingPayloadRequiresExactReplacementAuthorization()
    {
        var snapshot = Snapshot("StardewValley.Object", "(O)388", 1, replace: true);
        var request = Request(snapshot, "StardewValley.Object", "(O)388", 1, replace: true);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == "allow_replace_existing_display")).Value = "false";

        Assert.Contains("set_sign_display_item_replacement_not_authorized",
            Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items).BlockingReasons);
    }

    [Fact]
    public void DisplayAssignmentHasOneNativeRuntimeAndLeavesTextEditingPending()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.set_sign_display_item");
        Assert.True(capability.HarnessDispatchSupported);
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.False(PendingSemanticActionCatalog.TryGet("executor.set_sign_display_item", out _));
        Assert.False(PendingSemanticActionCatalog.TryGet("executor.edit_text_sign", out _));
        Assert.Equal(ImplementationEngineIds.InventoryTransfer,
            OptionImplementationCatalog.GetRequired("executor.set_sign_display_item").PrimaryEngineId);

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.SignDisplayItem.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "CurrentLocationReadAdapter.Signs.cs"));
        Assert.Contains("location.checkAction", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("sign.displayItem.Value =", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("sign.displayType.Value = request", runtime, StringComparison.Ordinal);
        Assert.Contains("SaveSerializer.GetSerializer(item.GetType()).Serialize(stream, item)", projection, StringComparison.Ordinal);
        Assert.DoesNotContain("item.getOne()", projection, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(
        SnapshotEnvelope snapshot, string sourceType, string qid, int displayType, bool replace) => new()
    {
        ModelOutputId = "sign-display-test",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "test",
        ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef
        {
            ActorId = "training_farmer.test",
            ActorType = "training_farmer",
            ControlSurface = "training_sandbox"
        },
        Actions = new[]
        {
            new SmallModelAction
            {
                ActionId = "set-sign-display",
                OptionId = "executor.set_sign_display_item",
                Rationale = "purpose-bound sign display",
                Parameters = new[]
                {
                    P("target_location", "Farm"), P("target_tile_x", "12"), P("target_tile_y", "10"),
                    P("stand_tile_x", "11"), P("stand_tile_y", "10"), P("target_runtime_type", "StardewValley.Objects.Sign"),
                    P("target_qualified_item_id", "(BC)37"), P("target_state_sha256", "target-state-hash"),
                    P("inventory_slot_index", "2"), P("item_id", qid[(qid.IndexOf(')') + 1)..]), P("qualified_item_id", qid),
                    P("expected_stack_before", "3"), P("source_quality", "0"), P("source_runtime_type", sourceType),
                    P("source_state_sha256", "source-hash"), P("expected_display_type", displayType.ToString()),
                    P("target_projection_fingerprint", "target-fingerprint"),
                    P("previous_display_item_qualified_item_id", replace ? "(O)390" : string.Empty),
                    P("previous_display_item_runtime_type", replace ? "StardewValley.Object" : string.Empty),
                    P("previous_display_item_state_sha256", replace ? "previous-hash" : string.Empty),
                    P("previous_display_type", replace ? "1" : "0"),
                    P("replace_existing_display", replace.ToString().ToLowerInvariant()),
                    P("allow_replace_existing_display", replace.ToString().ToLowerInvariant()),
                    P("sign_display_reason", "label_exact_storage_or_machine_group"), P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(string sourceType, string qid, int displayType, bool replace)
    {
        var previousQid = replace ? "(O)390" : string.Empty;
        var previousType = replace ? "StardewValley.Object" : string.Empty;
        var previousHash = replace ? "previous-hash" : string.Empty;
        var previousDisplayType = replace ? 1 : 0;
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"Farm","status":"available"},
            "inventory":{"value":[{"slot_index":2,"qualified_item_id":"{{{qid}}}","stack":3}],"status":"available"}
          },
          "current_location":{"objects":{"value":[{
            "tile_x":12,"tile_y":10,"type":"StardewValley.Objects.Sign",
            "sign_state":{"status":"available","placement_kind":"display_item_sign","display_assignment":{
              "status":"ready","target_location":"Farm","target_tile_x":12,"target_tile_y":10,
              "target_runtime_type":"StardewValley.Objects.Sign","target_projection_fingerprint":"target-fingerprint",
              "target_qualified_item_id":"(BC)37","target_state_sha256":"target-state-hash",
              "previous_display_item_qualified_item_id":"{{{previousQid}}}",
              "previous_display_item_runtime_type":"{{{previousType}}}",
              "previous_display_item_state_sha256":"{{{previousHash}}}","previous_display_type":{{{previousDisplayType}}},
              "replace_existing_display":{{{replace.ToString().ToLowerInvariant()}}},"native_contract":"{{{NativeContract}}}",
              "inventory_rows":[{"inventory_slot_index":2,"item_id":"{{{qid[(qid.IndexOf(')') + 1)..]}}}",
                "qualified_item_id":"{{{qid}}}","stack":3,"quality":0,"source_runtime_type":"{{{sourceType}}}",
                "source_state_status":"exact_live_direct_serialization","source_state_sha256":"source-hash","expected_display_type":{{{displayType}}},
                "expected_source_stack_after":3,"expected_display_stack":1}]
            }}
          }],"status":"available"}},
          "locations":{"collision_grid":{"value":{"location_id":"Farm","width":20,"height":20,"notable_tiles":[]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-25T00:00:00Z",
            Completeness = "complete",
            State = state
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
