using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupSecretNoteFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0 || !request.SecretNoteFixtureTargetId.HasValue ||
            request.QualifiedItemId is not ("(O)79" or "(O)842"))
        {
            return Blocked(request, reasons.Concat(new[] { "secret_note_fixture_request_invalid" }).ToArray());
        }
        var targetId = request.SecretNoteFixtureTargetId.Value;
        var journal = request.QualifiedItemId == "(O)842";
        var allNormalNotesUnseen = targetId == 0 && !journal;
        var data = DataLoader.SecretNotes(Game1.content);
        if (!allNormalNotesUnseen &&
            (!data.ContainsKey(targetId) || (targetId >= GameLocation.JOURNAL_INDEX) != journal))
        {
            return BlockedWithPrimitive(request, "debug_setup_secret_note_fixture", "secret_note_id=" + targetId, "secret_note=unresolved", "secret_note_fixture_target_invalid");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        Game1.exitActiveMenu();
        Game1.player.canMove = true;
        Game1.eventUp = false;
        EnsureFixtureInventoryCapacity(Game1.player);
        for (var index = 0; index < Game1.player.Items.Count; index++)
        {
            if (Game1.player.Items[index]?.QualifiedItemId is "(O)79" or "(O)842")
            {
                Game1.player.Items[index] = null;
            }
        }
        foreach (var id in data.Keys.Where(id => (id >= GameLocation.JOURNAL_INDEX) == journal))
        {
            if (allNormalNotesUnseen || id == targetId)
            {
                Game1.player.secretNotesSeen.Remove(id);
            }
            else
            {
                Game1.player.secretNotesSeen.Add(id);
            }
        }
        if (allNormalNotesUnseen)
        {
            Game1.player.mailReceived.Add("qiCave");
            Game1.player.eventsSeen.Add("2120303");
        }
        if (targetId == 10)
        {
            Game1.player.mailReceived.Remove("qiCave");
            Game1.player.questLog.RemoveWhere(quest => quest.id.Value == "30");
        }
        if (targetId == 23)
        {
            Game1.player.eventsSeen.Remove("2120303");
            Game1.player.questLog.RemoveWhere(quest => quest.id.Value == "29");
        }

        var note = ItemRegistry.Create(request.QualifiedItemId) as StardewValley.Object;
        if (note is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_secret_note_fixture", "secret_note_id=" + targetId, "secret_note=unresolved", "secret_note_fixture_item_unavailable");
        }
        note.Stack = 1;
        var slot = InstallFixtureItem(Game1.player, note);
        var unseen = Utility.GetUnseenSecretNotes(Game1.player, journal, out _);
        var verified = slot >= 0 && (allNormalNotesUnseen
            ? unseen.Length > 1
            : unseen.SequenceEqual(new[] { targetId }));

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
            PrimitiveKind = "debug_setup_secret_note_fixture",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_secret_note_fixture_installed", allNormalNotesUnseen ? "multiple_normal_notes_unseen" : "only_target_note_unseen", "secret_note_slot=" + slot }
                : new[] { "secret_note_fixture_projection_mismatch" },
            RequestedEffect = "secret_note_id=" + targetId,
            ObservedEffect = "slot_index=" + slot + ";qualified_item_id=" + note.QualifiedItemId + ";unseen_json=" + System.Text.Json.JsonSerializer.Serialize(unseen),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "secret_note_fixture_projection_mismatch" }
        };
    }
}
