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
        var isHollowLog = isExactVanilla &&
            clump.parentSheetIndex.Value == 602 &&
            clump.width.Value == 2 &&
            clump.height.Value == 2;
        var isGiantCrop = clump is GiantCrop;
        var giantCropOutputs = clump is GiantCrop giantCrop
            ? FarmReadAdapter.ReadGuaranteedGiantCropOutputs(giantCrop)
            : Array.Empty<GiantCropOutputProjection>();
        var giantCropExperience = FarmReadAdapter.ReadGiantCropExperience(clump, player);
        var axe = player.Items
            .Select((item, index) => new { Tool = item as Axe, SlotIndex = index })
            .Where(row => row.Tool is not null)
            .OrderByDescending(row =>
                NativeToolPowerProjection.EffectiveUpgradeLevel(row.Tool!))
            .ThenBy(row => row.SlotIndex)
            .FirstOrDefault();
        var additionalPower = axe is null
            ? (int?)null
            : NativeToolPowerProjection.AdditionalPower(axe.Tool!);
        var effectiveUpgradeLevel = axe is null
            ? (int?)null
            : NativeToolPowerProjection.EffectiveUpgradeLevel(axe.Tool!);
        var damagePerHit = axe is null
            ? (float?)null
            : NativeToolPowerProjection.ResourceClumpDamage(axe.Tool!);
        var expectedHits = damagePerHit.HasValue
            ? Math.Max(1, (int)Math.Ceiling(Math.Max(0f, clump.health.Value) / damagePerHit.Value))
            : (int?)null;
        var outputs = isGreenRainBush
            ? ProjectGreenRainBushCoreOutputs(clump)
            : isHollowLog
                ? ProjectHollowLogCoreOutputs(player)
                : Array.Empty<ClearanceOutputItemProjection>();
        var outputTagSets = outputs
            .Select(output => ReadResourceClumpOutputTags(output.QualifiedItemId))
            .Where(output => output is not null)
            .ToArray();
        var secretNote = isGreenRainBush || isHollowLog
            ? ProjectGreenRainBushSecretNote(location, player)
            : GreenRainSecretNoteProjection.NotApplicable();
        var status = !isGreenRainBush && !isHollowLog
            ? "not_supported_current_location_resource_clump"
            : !isExactVanilla
                ? "blocked_custom_resource_clump_runtime_type"
                : clump.width.Value != 2 || clump.height.Value != 2
                    ? "blocked_non_vanilla_resource_clump_shape"
                    : axe is null
                        ? "blocked_required_axe_missing"
                        : isHollowLog && effectiveUpgradeLevel < 2
                            ? "blocked_required_axe_upgrade_missing"
                            : "ready";
        var harvestExperienceSkillId = isGreenRainBush || isHollowLog
            ? "foraging"
            : giantCropExperience.SkillId;
        var harvestExperienceSkillIndex = isGreenRainBush || isHollowLog
            ? 2
            : giantCropExperience.SkillIndex;
        var harvestExperienceMinimum = isHollowLog
            ? 25
            : isGreenRainBush
                ? 15
                : giantCropExperience.Minimum;
        var harvestExperienceMaximum = isHollowLog
            ? 25
            : isGreenRainBush
                ? 15
                : giantCropExperience.Maximum;
        var harvestExperienceCondition = isHollowLog
            ? "native_axe_destroys_exact_hollow_log"
            : isGreenRainBush
                ? "native_axe_destroys_exact_green_rain_resource_clump"
                : giantCropExperience.Condition;
        var harvestExperienceStatus = isGreenRainBush || isHollowLog
            ? "exact"
            : giantCropExperience.Status;

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
            is_giant_crop = isGiantCrop,
            giant_crop_id = clump is GiantCrop giant ? giant.Id : string.Empty,
            giant_crop_guaranteed_outputs = giantCropOutputs,
            giant_crop_guaranteed_outputs_json = JsonSerializer.Serialize(giantCropOutputs),
            giant_crop_output_projection_status = isGiantCrop
                ? giantCropOutputs.Length > 0
                    ? "exact_unconditional_direct_outputs"
                    : "blocked_no_unconditional_direct_output"
                : "not_applicable",
            required_tool = isGiantCrop ? "axe" : string.Empty,
            executor_status = isGiantCrop ? "runtime_verified" : string.Empty,
            harvest_experience_skill_id = harvestExperienceSkillId,
            harvest_experience_skill_index = harvestExperienceSkillIndex,
            harvest_experience_on_success_min = harvestExperienceMinimum,
            harvest_experience_on_success_max = harvestExperienceMaximum,
            harvest_experience_condition = harvestExperienceCondition,
            harvest_experience_projection_status = harvestExperienceStatus,
            clear_kind = isGreenRainBush
                ? "green_rain_bush"
                : isHollowLog
                    ? "hollow_log"
                    : string.Empty,
            clear_obstacle_executor_status = status,
            required_tool_kind = isGreenRainBush || isHollowLog ? "axe" : string.Empty,
            minimum_tool_upgrade_level = isHollowLog
                ? 2
                : isGreenRainBush
                    ? 0
                    : (int?)null,
            tool_slot_index = axe?.SlotIndex,
            tool_upgrade_level = axe?.Tool?.UpgradeLevel,
            tool_additional_power = additionalPower,
            tool_effective_upgrade_level = effectiveUpgradeLevel,
            damage_per_hit = damagePerHit,
            expected_tool_hits_to_clear = expectedHits,
            expected_foraging_experience_delta = isHollowLog
                ? 25
                : isGreenRainBush
                    ? 15
                    : (int?)null,
            foraging_experience_projection_status = isGreenRainBush || isHollowLog ? "exact_from_resource_clump_destroy" : "not_applicable",
            core_output_projection_status = isHollowLog
                ? "exact_vanilla_hollow_log_output"
                : isGreenRainBush
                    ? "exact_from_day_save_coordinate_rng"
                    : "not_applicable",
            expected_core_output_items = outputs,
            expected_core_output_items_json = JsonSerializer.Serialize(outputs),
            expected_core_output_context_tag_sets = outputTagSets,
            expected_core_output_context_tag_sets_json = JsonSerializer.Serialize(outputTagSets),
            possible_secret_note_qualified_item_id = secretNote.QualifiedItemId,
            unseen_secret_note_count = secretNote.UnseenCount,
            total_secret_note_count = secretNote.TotalCount,
            secret_note_outer_roll_probability = secretNote.OuterProbability,
            secret_note_inner_roll_probability = secretNote.InnerProbability,
            secret_note_combined_probability = secretNote.CombinedProbability,
            secret_note_projection_status = secretNote.Status,
            output_distribution_status = secretNote.CombinedProbability > 0
                ? "exact_seeded_core_plus_bounded_secret_note_probability"
                : isGreenRainBush || isHollowLog
                    ? "exact_seeded_core_no_secret_note_possible"
                    : "not_applicable",
            native_contract = isGreenRainBush || isHollowLog
                ? "axe_DoFunction_to_GameLocation.performToolAction_then_ResourceClump.destroy"
                : string.Empty
        };
    }

    private static ClearanceOutputItemProjection[] ProjectHollowLogCoreOutputs(
        Farmer player) =>
    new[]
    {
        ClearanceOutputItemProjection.FromStandard(
            "(O)709",
            player.professions.Contains(12) ? 10 : 8)
    };

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

    private static object? ReadResourceClumpOutputTags(string qualifiedItemId)
    {
        try
        {
            return new
            {
                qualified_item_id = qualifiedItemId,
                context_tags = ItemRegistry.Create(qualifiedItemId)
                    .GetContextTags()
                    .OrderBy(tag => tag, StringComparer.Ordinal)
                    .ToArray()
            };
        }
        catch
        {
            return null;
        }
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
