using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeSecretNoteNativeContract =
        "Object.performUseAction((O)79|(O)842)->Utility.GetUnseenSecretNotes->Farmer.secretNotesSeen.Add->LetterViewerMenu;on_true->Farmer.reduceActiveItemByOne";

    private TrainingExecutionResult ExecuteReadSecretNote(TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        var requested = "player.inventory[" + request.SlotIndex + "].read_secret_note=" + request.SecretNoteSelectedId;
        if (ValidateExecutionRequest(request).Count > 0 || !request.SlotIndex.HasValue ||
            request.SlotIndex.Value < 0 || request.SlotIndex.Value >= Game1.player.Items.Count ||
            !request.SecretNoteSelectedId.HasValue)
        {
            return BlockedWithPrimitive(request, "read_secret_note", requested, "secret_note=unresolved", "read_secret_note_request_invalid");
        }
        var slot = request.SlotIndex.Value;
        if (Game1.activeClickableMenu is not null || !Game1.player.canMove)
        {
            return BlockedWithPrimitive(request, "read_secret_note", requested, SecretNoteObservedEffect(slot), "read_secret_note_product_safety_gate_blocked");
        }
        if (Game1.player.Items[slot] is not StardewValley.Object note ||
            note.GetType() != typeof(StardewValley.Object) ||
            note.QualifiedItemId is not ("(O)79" or "(O)842") ||
            !string.Equals(note.QualifiedItemId, request.QualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(note.ItemId, request.ItemId, StringComparison.Ordinal) ||
            !string.Equals(note.GetType().FullName, request.SecretNoteRuntimeType, StringComparison.Ordinal) ||
            note.Stack != request.SecretNoteStackBefore || note.isTemporarilyInvisible)
        {
            return BlockedWithPrimitive(request, "read_secret_note", requested, SecretNoteObservedEffect(slot), "read_secret_note_inventory_identity_drifted");
        }

        var projection = ProjectSecretNoteRuntime(note);
        if (!SecretNoteRequestMatchesProjection(request, projection) ||
            string.IsNullOrWhiteSpace(request.SecretNoteProjectionFingerprint))
        {
            return BlockedWithPrimitive(request, "read_secret_note", requested, SecretNoteObservedEffect(slot), "read_secret_note_projection_drifted");
        }

        var seenBefore = Game1.player.secretNotesSeen.ToHashSet();
        var questBefore = SecretNoteQuestPresent(projection.ExpectedQuestId);
        var nativeUse = UseInventoryObjectNative(note, slot);
        var seenAfter = Game1.player.secretNotesSeen.ToHashSet();
        var newlySeen = seenAfter.Except(seenBefore).OrderBy(id => id).ToArray();
        var questAfter = SecretNoteQuestPresent(projection.ExpectedQuestId);
        var menu = Game1.activeClickableMenu as LetterViewerMenu;
        var menuVerified = menu is not null &&
            (projection.DisplayKind == "image"
                ? menu.secretNoteImage == projection.ExpectedImage
                : menu.secretNoteImage == -1 && menu.whichBG == projection.ExpectedWhichBackground && menu.mailMessage.Count > 0);
        var stackVerified = nativeUse.StackBefore == request.SecretNoteStackBefore &&
            nativeUse.StackAfter == request.SecretNoteStackAfter;
        var questVerified = questBefore == projection.ExpectedQuestPresentBefore &&
            questAfter == projection.ExpectedQuestPresentAfter;
        var verified = nativeUse.Used && stackVerified && menuVerified && questVerified &&
            newlySeen.SequenceEqual(new[] { projection.SelectedNoteId });

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "read_secret_note",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_object_performUseAction_succeeded", "exact_selected_note_marked_seen", "native_letter_viewer_verified", "one_note_item_consumed", "native_quest_side_effect_verified" }
                : new[] { nativeUse.Used ? "performUseAction_returned_true" : "performUseAction_returned_false", stackVerified ? "note_stack_verified" : "note_stack_mismatch", menuVerified ? "letter_viewer_verified" : "letter_viewer_mismatch", questVerified ? "quest_state_verified" : "quest_state_mismatch" },
            RequestedEffect = requested,
            ObservedEffect = SecretNoteObservedEffect(slot) +
                ";newly_seen_json=" + JsonSerializer.Serialize(newlySeen) +
                ";quest_before=" + questBefore.ToString().ToLowerInvariant() +
                ";quest_after=" + questAfter.ToString().ToLowerInvariant() +
                ";menu_type=" + (Game1.activeClickableMenu?.GetType().FullName ?? "none"),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "read_secret_note_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "player.secret_notes_seen[" + projection.SelectedNoteId + "]",
                        Before = "false",
                        After = "true"
                    },
                    new SimulatedFactChange
                    {
                        Path = "player.inventory[" + slot + "]",
                        Before = request.QualifiedItemId + "x" + request.SecretNoteStackBefore,
                        After = request.SecretNoteStackAfter == 0 ? "null" : request.QualifiedItemId + "x" + request.SecretNoteStackAfter
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }

    private static SecretNoteRuntimeProjection ProjectSecretNoteRuntime(StardewValley.Object note)
    {
        var journal = note.QualifiedItemId == "(O)842";
        var unseen = Utility.GetUnseenSecretNotes(Game1.player, journal, out var totalNotes);
        var selected = unseen.Length == 0
            ? -1
            : journal
                ? unseen.Min()
                : unseen[Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.player.UniqueMultiplayerID, unseen.Length * 777).Next(unseen.Length)];
        var content = selected >= 0 && DataLoader.SecretNotes(Game1.content).TryGetValue(selected, out var raw)
            ? raw
            : string.Empty;
        var displayKind = content.StartsWith("!", StringComparison.Ordinal) ? "image" : "text";
        var image = -1;
        if (displayKind == "image")
        {
            var tokens = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length > 1)
            {
                int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out image);
            }
        }
        var questId = selected switch
        {
            23 when !Game1.player.eventsSeen.Contains("2120303") => "29",
            10 when !Game1.player.mailReceived.Contains("qiCave") => "30",
            _ => string.Empty
        };
        var questBefore = SecretNoteQuestPresent(questId);
        return new SecretNoteRuntimeProjection(
            journal,
            totalNotes,
            unseen,
            selected,
            SecretNoteSha256(content),
            displayKind,
            image,
            displayKind == "image" ? 0 : selected <= GameLocation.JOURNAL_INDEX ? 1 : 0,
            questId,
            questBefore,
            !string.IsNullOrEmpty(questId) || questBefore);
    }

    private static bool SecretNoteRequestMatchesProjection(TrainingExecutionRequest request, SecretNoteRuntimeProjection projection)
    {
        return request.SecretNoteIsJournal == projection.IsJournal &&
            request.SecretNoteJournalIndex == GameLocation.JOURNAL_INDEX &&
            request.SecretNoteTotalCount == projection.TotalNoteCount &&
            request.SecretNoteUnseenCount == projection.UnseenIds.Length &&
            string.Equals(request.SecretNoteUnseenIdsNativeOrderJson, JsonSerializer.Serialize(projection.UnseenIds), StringComparison.Ordinal) &&
            string.Equals(request.SecretNoteSelectionKind, projection.IsJournal ? "minimum_unseen_id" : "seeded_choose_from_unseen_native_order", StringComparison.Ordinal) &&
            request.SecretNoteSelectedId == projection.SelectedNoteId &&
            string.Equals(request.SecretNoteContentSha256, projection.ContentSha256, StringComparison.Ordinal) &&
            string.Equals(request.SecretNoteDisplayKind, projection.DisplayKind, StringComparison.Ordinal) &&
            request.SecretNoteExpectedImage == projection.ExpectedImage &&
            request.SecretNoteExpectedWhichBackground == projection.ExpectedWhichBackground &&
            string.Equals(request.SecretNoteExpectedQuestId, projection.ExpectedQuestId, StringComparison.Ordinal) &&
            request.SecretNoteExpectedQuestPresentBefore == projection.ExpectedQuestPresentBefore &&
            request.SecretNoteExpectedQuestPresentAfter == projection.ExpectedQuestPresentAfter &&
            request.SecretNoteStackAfter == Math.Max(0, request.SecretNoteStackBefore.GetValueOrDefault() - 1) &&
            string.Equals(request.SecretNoteNativeContract, RuntimeSecretNoteNativeContract, StringComparison.Ordinal);
    }

    private static bool SecretNoteQuestPresent(string questId) => !string.IsNullOrEmpty(questId) &&
        Game1.player.questLog.Any(quest => string.Equals(quest.id.Value, questId, StringComparison.Ordinal));

    private static string SecretNoteSha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string SecretNoteObservedEffect(int slotIndex)
    {
        var item = slotIndex >= 0 && slotIndex < Game1.player.Items.Count ? Game1.player.Items[slotIndex] : null;
        return "slot_index=" + slotIndex +
            ";qualified_item_id=" + (item?.QualifiedItemId ?? "null") +
            ";stack=" + (item?.Stack ?? 0) +
            ";seen_count=" + Game1.player.secretNotesSeen.Count;
    }

    private sealed record SecretNoteRuntimeProjection(
        bool IsJournal,
        int TotalNoteCount,
        int[] UnseenIds,
        int SelectedNoteId,
        string ContentSha256,
        string DisplayKind,
        int ExpectedImage,
        int ExpectedWhichBackground,
        string ExpectedQuestId,
        bool ExpectedQuestPresentBefore,
        bool ExpectedQuestPresentAfter);
}
