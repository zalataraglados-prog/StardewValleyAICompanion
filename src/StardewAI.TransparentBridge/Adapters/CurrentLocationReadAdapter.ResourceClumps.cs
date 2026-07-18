using System.Text.Json;
using StardewValley;
using StardewValley.Extensions;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static object[] ReadCurrentLocationResourceClumps(GameLocation location, Farmer player)
    {
        return location.resourceClumps
            .OrderBy(clump => clump.Tile.Y)
            .ThenBy(clump => clump.Tile.X)
            .Select(clump => ReadCurrentLocationResourceClump(location, clump, player))
            .ToArray();
    }

    private static object ReadCurrentLocationResourceClump(GameLocation location, ResourceClump clump, Farmer player)
    {
        var isExactVanilla = clump.GetType() == typeof(ResourceClump);
        var isGreenRainBush = clump.parentSheetIndex.Value is ResourceClump.greenRainBush1Index or ResourceClump.greenRainBush2Index;
        var axe = player.Items
            .Select((item, index) => new { Tool = item as Axe, SlotIndex = index })
            .Where(row => row.Tool is not null)
            .OrderByDescending(row => row.Tool!.UpgradeLevel)
            .ThenBy(row => row.SlotIndex)
            .FirstOrDefault();
        var damagePerHit = axe is null ? (float?)null : Math.Max(1f, (axe.Tool!.UpgradeLevel + 1) * 0.75f);
        var expectedHits = damagePerHit.HasValue
            ? Math.Max(1, (int)Math.Ceiling(Math.Max(0f, clump.health.Value) / damagePerHit.Value))
            : (int?)null;
        var outputs = isGreenRainBush ? ProjectGreenRainBushCoreOutputs(clump) : Array.Empty<ClearanceOutputItemProjection>();
        var secretNote = isGreenRainBush ? ProjectGreenRainBushSecretNote(location, player) : GreenRainSecretNoteProjection.NotApplicable();
        var status = !isGreenRainBush
            ? "not_green_rain_bush"
            : !isExactVanilla
                ? "blocked_custom_resource_clump_runtime_type"
                : clump.width.Value != 2 || clump.height.Value != 2
                    ? "blocked_non_vanilla_green_rain_bush_shape"
                    : axe is null
                        ? "blocked_required_axe_missing"
                        : "ready";

        return new
        {
            location_id = location.NameOrUniqueName,
            tile_x = (int)clump.Tile.X,
            tile_y = (int)clump.Tile.Y,
            runtime_type = clump.GetType().FullName,
            parent_sheet_index = clump.parentSheetIndex.Value,
            width = clump.width.Value,
            height = clump.height.Value,
            health = clump.health.Value,
            clear_kind = isGreenRainBush ? "green_rain_bush" : string.Empty,
            clear_obstacle_executor_status = status,
            required_tool_kind = isGreenRainBush ? "axe" : string.Empty,
            minimum_tool_upgrade_level = isGreenRainBush ? 0 : (int?)null,
            tool_slot_index = axe?.SlotIndex,
            tool_upgrade_level = axe?.Tool?.UpgradeLevel,
            damage_per_hit = damagePerHit,
            expected_tool_hits_to_clear = expectedHits,
            expected_foraging_experience_delta = isGreenRainBush ? 15 : (int?)null,
            foraging_experience_projection_status = isGreenRainBush ? "exact_from_resource_clump_destroy" : "not_applicable",
            core_output_projection_status = isGreenRainBush ? "exact_from_day_save_coordinate_rng" : "not_applicable",
            expected_core_output_items = outputs,
            expected_core_output_items_json = JsonSerializer.Serialize(outputs),
            possible_secret_note_qualified_item_id = secretNote.QualifiedItemId,
            unseen_secret_note_count = secretNote.UnseenCount,
            total_secret_note_count = secretNote.TotalCount,
            secret_note_outer_roll_probability = secretNote.OuterProbability,
            secret_note_inner_roll_probability = secretNote.InnerProbability,
            secret_note_combined_probability = secretNote.CombinedProbability,
            secret_note_projection_status = secretNote.Status,
            output_distribution_status = secretNote.CombinedProbability > 0
                ? "exact_seeded_core_plus_bounded_secret_note_probability"
                : isGreenRainBush
                    ? "exact_seeded_core_no_secret_note_possible"
                    : "not_applicable",
            native_contract = isGreenRainBush
                ? "axe_DoFunction_to_GameLocation.performToolAction_then_ResourceClump.destroy"
                : string.Empty
        };
    }

    private static ClearanceOutputItemProjection[] ProjectGreenRainBushCoreOutputs(ResourceClump clump)
    {
        var random = Utility.CreateRandom(
            Game1.uniqueIDForThisGame,
            Game1.stats.DaysPlayed,
            clump.Tile.X * 7.0,
            clump.Tile.Y * 11.0);
        var outputs = new List<ClearanceOutputItemProjection>
        {
            ClearanceOutputItemProjection.FromStandard("(O)Moss", random.Next(2, 4)),
            ClearanceOutputItemProjection.FromStandard("(O)771", random.Next(2, 4))
        };
        if (random.NextDouble() < 0.05)
        {
            outputs.Add(ClearanceOutputItemProjection.FromStandard("(O)MossySeed"));
        }
        return outputs.ToArray();
    }

    private static GreenRainSecretNoteProjection ProjectGreenRainBushSecretNote(GameLocation location, Farmer player)
    {
        var island = location.InIslandContext();
        if (!location.HasUnlockedAreaSecretNotes(player))
        {
            return GreenRainSecretNoteProjection.Unavailable(island ? "(O)842" : "(O)79", "secret_notes_not_unlocked");
        }

        var qualifiedItemId = island ? "(O)842" : "(O)79";
        var unseen = Utility.GetUnseenSecretNotes(player, island, out var total).Length - player.Items.CountId(qualifiedItemId);
        unseen = Math.Max(0, unseen);
        if (unseen == 0)
        {
            return new GreenRainSecretNoteProjection(qualifiedItemId, 0, total, 0.05, 0, 0, "exact_no_unseen_secret_note");
        }

        if (location.currentEvent?.isFestival == true)
        {
            return new GreenRainSecretNoteProjection(
                qualifiedItemId,
                unseen,
                total,
                0,
                0,
                0,
                "exact_blocked_by_festival_event");
        }

        var ratio = (float)(unseen - 1) / Math.Max(1, total - 1);
        var inner = GameLocation.LAST_SECRET_NOTE_CHANCE +
            (GameLocation.FIRST_SECRET_NOTE_CHANCE - GameLocation.LAST_SECRET_NOTE_CHANCE) * ratio;
        return new GreenRainSecretNoteProjection(
            qualifiedItemId,
            unseen,
            total,
            0.05,
            inner,
            0.05 * inner,
            "bounded_probability_global_rng_not_consumed");
    }

    private sealed record GreenRainSecretNoteProjection(
        string QualifiedItemId,
        int UnseenCount,
        int TotalCount,
        double OuterProbability,
        double InnerProbability,
        double CombinedProbability,
        string Status)
    {
        public static GreenRainSecretNoteProjection NotApplicable() => new(string.Empty, 0, 0, 0, 0, 0, "not_applicable");

        public static GreenRainSecretNoteProjection Unavailable(string qualifiedItemId, string status) =>
            new(qualifiedItemId, 0, 0, 0, 0, 0, status);
    }
}
