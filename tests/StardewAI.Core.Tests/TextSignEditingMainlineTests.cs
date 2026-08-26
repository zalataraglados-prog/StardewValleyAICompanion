using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class TextSignEditingMainlineTests
{
    private const string NativeContract =
        "GameLocation.checkAction->Object.CheckForActionOnTextSign->TitleTextInputMenu(textLimit=60,minLength=0,paste=false)->NamingMenu.textBoxEnter(FilterDirtyWords)->signText=text.Trim()->TokenParser.ParseText+FilterDirtyWords->showNextIndex=IsNullOrEmpty(SignText)";

    [Theory]
    [InlineData("", "New label", false)]
    [InlineData("Old label", " replacement ", true)]
    [InlineData("Old label", "", true)]
    public void ExactTextSignCompilesToOneNativeMenuInteraction(string before, string requested, bool replace)
    {
        var snapshot = Snapshot(before);
        var queue = new ActionQueueCompiler().Compile(Request(snapshot, before, requested, replace), snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("edit_text_sign", step.StepType);
        Assert.Equal("Farm(12,10)", step.Target);
        Assert.Contains("native_menu_receipt", step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("target_state_sha256", "stale", "edit_text_sign_target_projection_drifted")]
    [InlineData("target_projection_fingerprint", "stale", "edit_text_sign_target_projection_drifted")]
    [InlineData("raw_sign_text_before", "stale", "edit_text_sign_previous_text_drifted")]
    [InlineData("target_runtime_type", "StardewValley.Objects.Sign", "edit_text_sign_exact_base_object_required")]
    [InlineData("stand_tile_x", "9", "edit_text_sign_adjacent_stand_geometry_invalid")]
    public void StaleOrInvalidBindingsFailClosed(string parameter, string value, string reason)
    {
        var snapshot = Snapshot("Old label");
        var request = Request(snapshot, "Old label", "New label", replace: true);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == parameter)).Value = value;

        Assert.Contains(reason, Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items).BlockingReasons);
    }

    [Fact]
    public void NativeKeyboardContractRejectsUnrepresentableOrOverLimitInput()
    {
        var snapshot = Snapshot(string.Empty);
        foreach (var text in new[] { new string('a', 61), "contains\"quote", "line\nbreak" })
        {
            var queue = new ActionQueueCompiler().Compile(Request(snapshot, string.Empty, text, replace: false), snapshot);
            Assert.Contains("edit_text_sign_native_keyboard_input_invalid", Assert.Single(queue.Items).BlockingReasons);
        }
    }

    [Fact]
    public void ExistingTextRequiresExplicitReplacementAuthorization()
    {
        var snapshot = Snapshot("Old label");
        var request = Request(snapshot, "Old label", "New label", replace: true);
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == "allow_replace_existing_text")).Value = "false";

        Assert.Contains("edit_text_sign_replacement_not_authorized",
            Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items).BlockingReasons);
    }

    [Fact]
    public void EditingUsesOneNativeRuntimeAndNoDirectProductionFieldWrites()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.edit_text_sign");
        Assert.True(capability.HarnessDispatchSupported);
        Assert.True(capability.AutonomousCandidateEnabled);
        Assert.False(PendingSemanticActionCatalog.TryGet("executor.edit_text_sign", out _));
        Assert.Equal(ImplementationEngineIds.InteractionMenu,
            OptionImplementationCatalog.GetRequired("executor.edit_text_sign").PrimaryEngineId);

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.TextSignEditing.cs"));
        Assert.Contains("location.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("menu.textBox.RecieveCommandInput('\\b')", runtime, StringComparison.Ordinal);
        Assert.Contains("menu.textBox.RecieveTextInput(character)", runtime, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("menu.textBox.Text =", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("sign.signText.Value =", runtime, StringComparison.Ordinal);
        Assert.DoesNotMatch("sign\\.showNextIndex\\.Value\\s*=(?!=)", runtime);
    }

    private static SmallModelActionEnvelope Request(
        SnapshotEnvelope snapshot, string before, string requested, bool replace) => new()
    {
        ModelOutputId = "text-sign-test",
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
                ActionId = "edit-text-sign",
                OptionId = "executor.edit_text_sign",
                Rationale = "purpose-bound text sign edit",
                Parameters = new[]
                {
                    P("target_location", "Farm"), P("target_tile_x", "12"), P("target_tile_y", "10"),
                    P("stand_tile_x", "11"), P("stand_tile_y", "10"), P("target_runtime_type", "StardewValley.Object"),
                    P("target_qualified_item_id", "(BC)TextSign"), P("target_state_sha256", "target-state-hash"),
                    P("target_projection_fingerprint", "target-fingerprint"),
                    P("raw_sign_text_before", before), P("display_sign_text_before", before),
                    P("expected_show_next_index_before", string.IsNullOrEmpty(before).ToString().ToLowerInvariant()),
                    P("replaces_existing_text", replace.ToString().ToLowerInvariant()),
                    P("allow_replace_existing_text", replace.ToString().ToLowerInvariant()),
                    P("requested_sign_text", requested), P("text_edit_reason", "label_exact_storage_group"),
                    P("native_contract", NativeContract)
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(string before)
    {
        var showNext = string.IsNullOrEmpty(before).ToString().ToLowerInvariant();
        var replace = (!string.IsNullOrEmpty(before)).ToString().ToLowerInvariant();
        var escaped = JsonSerializer.Serialize(before);
        var json = $$$"""
        {
          "player":{"location_id":{"value":"Farm","status":"available"}},
          "current_location":{"objects":{"value":[{
            "tile_x":12,"tile_y":10,"type":"StardewValley.Object",
            "sign_state":{"status":"available","placement_kind":"text_sign","text_editing":{
              "status":"ready","target_location":"Farm","target_tile_x":12,"target_tile_y":10,
              "target_runtime_type":"StardewValley.Object","target_qualified_item_id":"(BC)TextSign",
              "target_state_sha256":"target-state-hash","target_projection_fingerprint":"target-fingerprint",
              "raw_sign_text_before":{{{escaped}}},"display_sign_text_before":{{{escaped}}},
              "show_next_index_before":{{{showNext}}},"replaces_existing_text":{{{replace}}},
              "text_limit_utf16_code_units":60,"minimum_length":0,"paste_button_visible":false,
              "input_filter":"Utility.FilterDirtyWords","display_pipeline":"TokenParser.ParseText_then_Utility.FilterDirtyWords",
              "trim_pipeline":"System.String.Trim","show_next_index_rule":"string.IsNullOrEmpty(SignText)",
              "native_contract":"{{{NativeContract}}}"
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
            RealTimestamp = "2026-08-26T00:00:00Z",
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
