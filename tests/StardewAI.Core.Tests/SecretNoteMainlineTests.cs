using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class SecretNoteMainlineTests
{
    private const string NativeContract =
        "Object.performUseAction((O)79|(O)842)->Utility.GetUnseenSecretNotes->Farmer.secretNotesSeen.Add->LetterViewerMenu;on_true->Farmer.reduceActiveItemByOne";

    [Theory]
    [InlineData("79", "(O)79", false, "seeded_choose_from_unseen_native_order", 10, "30")]
    [InlineData("842", "(O)842", true, "minimum_unseen_id", 1001, "")]
    [InlineData("79", "(O)79", false, "seeded_choose_from_unseen_native_order", 23, "29")]
    public void NativeSecretNoteBranchesCompileToOneExactUse(
        string itemId,
        string qualifiedItemId,
        bool journal,
        string selectionKind,
        int selectedNoteId,
        string questId)
    {
        var snapshot = Snapshot(itemId, qualifiedItemId, journal, selectionKind, selectedNoteId, questId);
        var queue = new ActionQueueCompiler().Compile(
            Request(snapshot, itemId, qualifiedItemId, journal, selectionKind, selectedNoteId, questId),
            snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("read_secret_note", step.StepType);
        Assert.Equal($"player.inventory[2]:{qualifiedItemId}:native_performUseAction", step.Target);
        Assert.Contains("secret_note_id=" + selectedNoteId, step.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("quest_id=" + questId, step.ExpectedEffect, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("secret_note_projection_fingerprint", "drifted")]
    [InlineData("secret_note_selected_id", "22")]
    [InlineData("secret_note_unseen_ids_native_order_json", "[23]")]
    [InlineData("secret_note_stack_before", "2")]
    [InlineData("secret_note_expected_quest_id", "29")]
    public void StaleSelectionInventoryAndSideEffectsFailClosed(string parameterName, string value)
    {
        var snapshot = Snapshot("79", "(O)79", false, "seeded_choose_from_unseen_native_order", 10, "30");
        var request = Request(snapshot, "79", "(O)79", false, "seeded_choose_from_unseen_native_order", 10, "30");
        Assert.Single(request.Actions[0].Parameters.Where(row => row.Name == parameterName)).Value = value;

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);
        Assert.Contains(item.BlockingReasons, reason =>
            reason.StartsWith("read_secret_note_projection_drifted:", StringComparison.Ordinal));
    }

    [Fact]
    public void SecretNoteExecutorOwnsFiveGatesAndSharesNativeInventoryUse()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.read_secret_note");
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, capability.CompilerStatus);
        Assert.True(capability.HarnessDispatchSupported);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, capability.ReadTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, capability.CandidateTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, capability.CompilerTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, capability.RuntimeTrainingGate);
        Assert.Equal(TrainingEvidenceGateStatus.RuntimeVerified, capability.OutputTrainingGate);
        Assert.Equal(CapabilityCandidateStatus.NotApplicable, capability.CandidateStatus);
        Assert.Equal(
            ImplementationEngineIds.InteractionMenu,
            OptionImplementationCatalog.GetRequired("executor.read_secret_note").PrimaryEngineId);
        Assert.False(PendingSemanticActionCatalog.TryGet("executor.read_secret_note", out _));

        var root = FindRepositoryRoot();
        var noteRuntime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.SecretNotes.cs"));
        var bookRuntime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Books.cs"));
        var sharedRuntime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.NativeInventoryObjectUse.cs"));
        var projection = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.SecretNotes.cs"));

        Assert.Contains("UseInventoryObjectNative", noteRuntime, StringComparison.Ordinal);
        Assert.Contains("UseInventoryObjectNative", bookRuntime, StringComparison.Ordinal);
        Assert.Contains("performUseAction", sharedRuntime, StringComparison.Ordinal);
        Assert.Contains("reduceActiveItemByOne", sharedRuntime, StringComparison.Ordinal);
        Assert.Contains("DataLoader.SecretNotes", projection, StringComparison.Ordinal);
        Assert.Contains("Utility.GetUnseenSecretNotes", projection, StringComparison.Ordinal);
        Assert.DoesNotContain(".secretNotesSeen.Add(", noteRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain("new LetterViewerMenu", noteRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain(".reduceActiveItemByOne(", noteRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain("note.performUseAction(", noteRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain(".reduceActiveItemByOne(", bookRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain("book.performUseAction(", bookRuntime, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeSmokeCoversBothQuestBranchesAndJournalScrapsSilently()
    {
        var smoke = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "Invoke-RuntimeSecretNoteSmoke.ps1"));
        Assert.Contains("selected_note_id = 10", smoke, StringComparison.Ordinal);
        Assert.Contains("expected_quest_id = \"30\"", smoke, StringComparison.Ordinal);
        Assert.Contains("selected_note_id = 23", smoke, StringComparison.Ordinal);
        Assert.Contains("expected_quest_id = \"29\"", smoke, StringComparison.Ordinal);
        Assert.Contains("qualified_item_id = \"(O)842\"", smoke, StringComparison.Ordinal);
        Assert.Contains("-WindowStyle Hidden", smoke, StringComparison.Ordinal);
        Assert.Contains("$env:SDL_AUDIODRIVER = \"dummy\"", smoke, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(
        SnapshotEnvelope snapshot,
        string itemId,
        string qualifiedItemId,
        bool journal,
        string selectionKind,
        int selectedNoteId,
        string questId) => new()
    {
        ModelOutputId = "secret-note-test",
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
                ActionId = "read-secret-note",
                OptionId = "executor.read_secret_note",
                Rationale = "read one exactly projected note",
                Parameters = new[]
                {
                    P("slot_index", "2"), P("item_id", itemId), P("qualified_item_id", qualifiedItemId),
                    P("secret_note_runtime_type", "StardewValley.Object"),
                    P("secret_note_stack_before", "1"), P("secret_note_stack_after", "0"),
                    P("secret_note_is_journal", journal ? "true" : "false"), P("secret_note_journal_index", "1000"),
                    P("secret_note_total_count", "2"), P("secret_note_unseen_ids_native_order_json", selectedNoteId == 10 ? "[10,23]" : $"[{selectedNoteId}]"),
                    P("secret_note_unseen_count", selectedNoteId == 10 ? "2" : "1"),
                    P("secret_note_selection_kind", selectionKind), P("secret_note_selected_id", selectedNoteId.ToString()),
                    P("secret_note_content_sha256", "note-sha"), P("secret_note_display_kind", "text"),
                    P("secret_note_expected_image", "-1"), P("secret_note_expected_which_bg", journal ? "0" : "1"),
                    P("secret_note_expected_quest_id", questId), P("secret_note_expected_quest_present_before", "false"),
                    P("secret_note_expected_quest_present_after", string.IsNullOrEmpty(questId) ? "false" : "true"),
                    P("secret_note_projection_fingerprint", "secret-note-fingerprint"),
                    P("secret_note_native_contract", NativeContract)
                }
            }
        }
    };

    private static SnapshotEnvelope Snapshot(
        string itemId,
        string qualifiedItemId,
        bool journal,
        string selectionKind,
        int selectedNoteId,
        string questId)
    {
        var unseenJson = selectedNoteId == 10 ? "[10,23]" : $"[{selectedNoteId}]";
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"FarmHouse","status":"available"},
            "inventory":{"value":[{"slot_index":2,"item_id":"{{{itemId}}}","qualified_item_id":"{{{qualifiedItemId}}}","stack":1}],"status":"available"},
            "secret_note_candidates":{"value":{"projection_fingerprint":"secret-note-fingerprint","rows":[{
              "slot_index":2,"item_id":"{{{itemId}}}","qualified_item_id":"{{{qualifiedItemId}}}","runtime_type":"StardewValley.Object",
              "stack_before":1,"stack_after":0,"available":true,"is_journal":{{{journal.ToString().ToLowerInvariant()}}},"journal_index":1000,
              "total_note_count":2,"unseen_note_ids_native_order_json":"{{{unseenJson}}}","unseen_note_count":{{{(selectedNoteId == 10 ? 2 : 1)}}},
              "selection_kind":"{{{selectionKind}}}","selected_note_id":{{{selectedNoteId}}},"selected_note_content_sha256":"note-sha",
              "display_kind":"text","expected_secret_note_image":-1,"expected_which_bg":{{{(journal ? 0 : 1)}}},
              "expected_quest_id":"{{{questId}}}","expected_quest_present_before":false,
              "expected_quest_present_after":{{{(!string.IsNullOrEmpty(questId)).ToString().ToLowerInvariant()}}},"native_contract":"{{{NativeContract}}}"
            }]} ,"status":"available"}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-29T00:00:00Z",
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
