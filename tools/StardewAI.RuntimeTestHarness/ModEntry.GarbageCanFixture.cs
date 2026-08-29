using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.GameData.GarbageCans;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupGarbageCanFixture(TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        var profile = string.IsNullOrWhiteSpace(request.FixtureGarbageCanProfile)
            ? "ordinary_output"
            : request.FixtureGarbageCanProfile;
        if (profile is not ("ordinary_output" or "no_output" or "direct_inventory_hat" or
            "desert_multiple" or "already_checked" or "negative_witness" or "linus_witness"))
            return GarbageCanFixtureBlocked(request, "fixture_garbage_can_profile_unknown:" + profile);
        if (!TryResolveForageFixtureLocation(request, out var location, out var target, out var reason))
            return GarbageCanFixtureBlocked(request, reason);

        ClearForageFixtureArea(location, target, 1, 1);
        var buildings = location.Map.GetLayer("Buildings");
        if (buildings is null || target.X < 0 || target.Y < 0 || target.X >= buildings.LayerWidth || target.Y >= buildings.LayerHeight ||
            buildings.Tiles[target.X, target.Y] is null)
            return GarbageCanFixtureBlocked(request, "fixture_garbage_can_buildings_tile_missing");

        location.characters.Clear();
        Game1.netWorldState.Value.CheckedGarbage.Clear();
        Game1.player.stats.Set("Book_Trash", 0u);
        Game1.stats.Set("trashCansChecked", profile == "direct_inventory_hat" ? 20u : 0u);
        var originalDaysPlayed = Game1.stats.DaysPlayed;
        var ids = profile == "desert_multiple"
            ? new[] { "DesertFestival" }
            : new[] { "Blacksmith", "EmilyAndHaley", "Evelyn", "JodiAndKent", "JojaMart", "Mayor", "Museum", "Saloon" };
        var wantedOutput = profile is not ("no_output" or "already_checked");
        var wantedDirect = profile == "direct_inventory_hat";
        var selectedId = string.Empty;
        Item? selectedItem = null;
        GarbageCanItemData? selectedEntry = null;
        var searchWindow = wantedDirect ? 50000u : 512u;
        var maxDays = (uint)Math.Min(uint.MaxValue, (ulong)originalDaysPlayed + searchWindow);
        for (var days = Math.Max(1u, originalDaysPlayed); days <= maxDays && string.IsNullOrWhiteSpace(selectedId); days++)
        {
            Game1.stats.DaysPlayed = days;
            foreach (var id in ids)
            {
                var errors = new List<string>();
                var produced = location.TryGetGarbageItem(id, Game1.player.DailyLuck, out var item, out var entry, out _, errors.Add);
                var matches = errors.Count == 0 && produced == wantedOutput &&
                    (!wantedDirect || entry?.Id == "Base_GarbageHat") &&
                    (profile != "desert_multiple" || entry?.CreateMultipleDebris == true) &&
                    (profile is not ("ordinary_output" or "negative_witness" or "linus_witness") || entry?.AddToInventoryDirectly != true);
                if (!matches) continue;
                selectedId = id;
                selectedItem = item;
                selectedEntry = entry;
                break;
            }
        }
        if (string.IsNullOrWhiteSpace(selectedId))
        {
            Game1.stats.DaysPlayed = originalDaysPlayed;
            return GarbageCanFixtureBlocked(request, "fixture_garbage_can_deterministic_profile_not_found:" + profile);
        }

        buildings.Tiles[target.X, target.Y].Properties["Action"] = "Garbage " + selectedId;
        if (profile == "already_checked") Game1.netWorldState.Value.CheckedGarbage.Add(selectedId);
        if (profile is "negative_witness" or "linus_witness")
        {
            var npcName = profile == "linus_witness" ? "Linus" : "Abigail";
            var npc = Game1.getCharacterFromName(npcName, mustBeVillager: false);
            if (npc is null) return GarbageCanFixtureBlocked(request, "fixture_garbage_can_npc_missing:" + npcName);
            npc.currentLocation?.characters.Remove(npc);
            npc.currentLocation = location;
            npc.Position = new Vector2((target.X + 2) * Game1.tileSize, target.Y * Game1.tileSize);
            location.characters.Add(npc);
            if (!Game1.player.friendshipData.ContainsKey(npc.Name))
                Game1.player.friendshipData[npc.Name] = new Friendship();
        }

        var emptySlot = Enumerable.Range(0, Math.Min(12, Game1.player.Items.Count)).FirstOrDefault(index => Game1.player.Items[index] is null);
        if (Game1.player.Items[emptySlot] is not null) Game1.player.Items[emptySlot] = null;
        Game1.player.CurrentToolIndex = emptySlot == 0 && Game1.player.Items.Count > 1 ? 1 : 0;
        var moved = MoveFixtureFarmerToLocationAdjacent(location, target, out var stand, out var moveReason);
        var projection = ProjectRuntimeGarbageCan(location, target);
        var expectedStatus = profile switch
        {
            "already_checked" => true,
            "negative_witness" => projection.Reaction?.Status != "exact_linus_non_negative",
            "linus_witness" => projection.Reaction?.Status == "exact_linus_non_negative",
            _ => true
        };
        var verified = moved && projection.Id == selectedId && projection.Errors.Length == 0 &&
            projection.Produced == wantedOutput && expectedStatus && AreAdjacent(stand, target) &&
            (selectedItem is null) == (projection.Output is null) &&
            string.Equals(selectedEntry?.Id ?? string.Empty, projection.SelectedEntryId, StringComparison.Ordinal);
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            ActualTicks = 1,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "debug_fixture_only",
            PrimitiveKind = "debug_setup_garbage_can_fixture",
            PrimitiveVerificationStatus = verified ? "verified" : "blocked",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_native_garbage_can_" + profile, "deterministic_native_prediction_bound", "map_Garbage_action_bound" }
                : new[] { moved ? "fixture_garbage_can_projection_mismatch" : moveReason },
            RequestedEffect = "current_location.garbage_cans[target].fixture_profile=" + profile,
            ObservedEffect = "location=" + location.NameOrUniqueName + ";target=" + target.X + "," + target.Y +
                ";stand=" + stand.X + "," + stand.Y + ";id=" + projection.Id + ";produced=" + projection.Produced.ToString().ToLowerInvariant() +
                ";entry=" + projection.SelectedEntryId + ";days_played=" + Game1.stats.DaysPlayed
        };
    }

    private static TrainingExecutionResult GarbageCanFixtureBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "debug_setup_garbage_can_fixture", "current_location.garbage_cans[target].ready=true",
            "fixture_garbage_can_profile=" + request.FixtureGarbageCanProfile, reasons);
}
