using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    private const string SecretNoteNativeContract =
        "Object.performUseAction((O)79|(O)842)->Utility.GetUnseenSecretNotes->Farmer.secretNotesSeen.Add->LetterViewerMenu;on_true->Farmer.reduceActiveItemByOne";

    private static object ReadSecretNoteCandidates(Farmer? player)
    {
        if (player is null)
        {
            return new
            {
                projection_fingerprint = string.Empty,
                journal_index = GameLocation.JOURNAL_INDEX,
                seen_note_ids = Array.Empty<int>(),
                note_catalog = Array.Empty<object>(),
                rows = Array.Empty<object>()
            };
        }

        var noteData = DataLoader.SecretNotes(Game1.content);
        var catalog = noteData
            .OrderBy(pair => pair.Key)
            .Select(pair =>
            {
                var visual = ReadSecretNoteVisual(pair.Key, pair.Value);
                return new
                {
                    note_id = pair.Key,
                    is_journal = pair.Key >= GameLocation.JOURNAL_INDEX,
                    raw_content = pair.Value,
                    content_sha256 = Sha256(pair.Value),
                    display_kind = visual.DisplayKind,
                    expected_secret_note_image = visual.SecretNoteImage,
                    expected_which_bg = visual.WhichBackground
                };
            })
            .ToArray();
        var rows = player.Items
            .Select((item, slotIndex) => item is StardewValley.Object note &&
                note.QualifiedItemId is "(O)79" or "(O)842"
                    ? ReadSecretNoteCandidate(player, note, slotIndex, noteData)
                    : null)
            .Where(row => row is not null)
            .Cast<object>()
            .ToArray();
        var seen = player.secretNotesSeen.OrderBy(id => id).ToArray();
        var fingerprintSource = JsonSerializer.Serialize(new
        {
            game_id = Game1.uniqueIDForThisGame,
            player_id = player.UniqueMultiplayerID,
            seen,
            catalog,
            rows
        });

        return new
        {
            projection_fingerprint = Sha256(fingerprintSource),
            journal_index = GameLocation.JOURNAL_INDEX,
            game_unique_id = Game1.uniqueIDForThisGame.ToString(CultureInfo.InvariantCulture),
            player_unique_multiplayer_id = player.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture),
            random_unseen_count_multiplier = 777,
            seen_note_ids = seen,
            note_catalog = catalog,
            rows
        };
    }

    private static object ReadSecretNoteCandidate(
        Farmer player,
        StardewValley.Object note,
        int slotIndex,
        IDictionary<int, string> noteData)
    {
        var journal = note.QualifiedItemId == "(O)842";
        var unseen = Utility.GetUnseenSecretNotes(player, journal, out var totalNotes);
        var selected = unseen.Length == 0
            ? -1
            : journal
                ? unseen.Min()
                : unseen[Utility.CreateRandom(
                    Game1.uniqueIDForThisGame,
                    player.UniqueMultiplayerID,
                    unseen.Length * 777).Next(unseen.Length)];
        var rawContent = selected >= 0 && noteData.TryGetValue(selected, out var content)
            ? content
            : string.Empty;
        var visual = ReadSecretNoteVisual(selected, rawContent);
        var questId = selected switch
        {
            23 when !player.eventsSeen.Contains("2120303") => "29",
            10 when !player.mailReceived.Contains("qiCave") => "30",
            _ => string.Empty
        };
        var questBefore = !string.IsNullOrEmpty(questId) &&
            player.questLog.Any(quest => string.Equals(quest.id.Value, questId, StringComparison.Ordinal));
        var exactBaseObject = note.GetType() == typeof(StardewValley.Object);
        var available = unseen.Length > 0 && exactBaseObject && player.canMove && !note.isTemporarilyInvisible &&
            Game1.activeClickableMenu is null;
        var reasons = new List<string>();
        if (unseen.Length == 0) reasons.Add("no_unseen_secret_note");
        if (!exactBaseObject) reasons.Add("custom_object_runtime_type_not_verified");
        if (!player.canMove) reasons.Add("player_cannot_move");
        if (note.isTemporarilyInvisible) reasons.Add("item_temporarily_invisible");
        if (Game1.activeClickableMenu is not null) reasons.Add("active_menu_open_product_safety_gate");

        return new
        {
            slot_index = slotIndex,
            item_id = note.ItemId,
            qualified_item_id = note.QualifiedItemId,
            runtime_type = note.GetType().FullName ?? note.GetType().Name,
            stack_before = note.Stack,
            stack_after = Math.Max(0, note.Stack - 1),
            temporarily_invisible = note.isTemporarilyInvisible,
            is_journal = journal,
            journal_index = GameLocation.JOURNAL_INDEX,
            total_note_count = totalNotes,
            unseen_note_ids_native_order = unseen,
            unseen_note_ids_native_order_json = JsonSerializer.Serialize(unseen),
            unseen_note_count = unseen.Length,
            selection_kind = journal ? "minimum_unseen_id" : "seeded_choose_from_unseen_native_order",
            random_seed_inputs = new
            {
                game_unique_id = Game1.uniqueIDForThisGame.ToString(CultureInfo.InvariantCulture),
                player_unique_multiplayer_id = player.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture),
                unseen_count_times_777 = unseen.Length * 777
            },
            selected_note_id = selected,
            selected_note_raw_content = rawContent,
            selected_note_content_sha256 = Sha256(rawContent),
            display_kind = visual.DisplayKind,
            expected_secret_note_image = visual.SecretNoteImage,
            expected_which_bg = visual.WhichBackground,
            event_2120303_seen = player.eventsSeen.Contains("2120303"),
            mail_qi_cave_received = player.mailReceived.Contains("qiCave"),
            expected_quest_id = questId,
            expected_quest_present_before = questBefore,
            expected_quest_present_after = !string.IsNullOrEmpty(questId) || questBefore,
            player_can_move = player.canMove,
            active_menu_clear = Game1.activeClickableMenu is null,
            available,
            block_reasons = reasons,
            native_contract = SecretNoteNativeContract
        };
    }

    private static SecretNoteVisual ReadSecretNoteVisual(int noteId, string rawContent)
    {
        if (rawContent.StartsWith("!", StringComparison.Ordinal))
        {
            var tokens = rawContent.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var image = tokens.Length > 1 && int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : -1;
            return new SecretNoteVisual("image", image, 0);
        }
        return new SecretNoteVisual("text", -1, noteId <= GameLocation.JOURNAL_INDEX ? 1 : 0);
    }

    private static string Sha256(string value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record SecretNoteVisual(string DisplayKind, int SecretNoteImage, int WhichBackground);
}
