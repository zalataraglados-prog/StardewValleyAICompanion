using System.Collections;
using System.Reflection;
using System.Text.Json;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    // Locked consumers: MineShaft.AnyOnlineFarmerHasBuff("24") and VolcanoDungeon onlineFarmer.hasBuff("24").
    private const string MonsterMuskNativeContract =
        "Object.performUseAction((O)879)->750ms_callback_Object.MonsterMusk->Farmer.applyBuff(24)->BuffManager.Apply_remove_then_replace";

    private static object ReadMonsterMuskContext(Farmer? player)
    {
        if (player is null || player.currentLocation is not { } location)
            return new { projection_status = "unavailable_world_player_or_location", rows = Array.Empty<object>() };

        DataLoader.Buffs(Game1.content).TryGetValue("24", out var buffData);
        player.buffs.AppliedBuffs.TryGetValue("24", out var activeBuff);
        var rows = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(entry => entry.item?.GetType() == typeof(StardewValley.Object) &&
                string.Equals(entry.item.QualifiedItemId, "(O)879", StringComparison.Ordinal))
            .Select(entry => new
            {
                inventory_slot_index = entry.slot,
                item_id = entry.item!.ItemId,
                qualified_item_id = entry.item.QualifiedItemId,
                display_name = entry.item.DisplayName,
                inventory_runtime_type = entry.item.GetType().FullName,
                stack_before = entry.item.Stack,
                stack_after = Math.Max(0, entry.item.Stack - 1),
                temporarily_invisible = ((StardewValley.Object)entry.item).isTemporarilyInvisible
            })
            .ToArray();
        var visibleItem = rows.Any(row => !row.temporarily_invisible && row.stack_before > 0);
        var nativeBaseGate = player.canMove && visibleItem && !Game1.eventUp && !Game1.isFestival() &&
            !Game1.fadeToBlack && !player.swimming.Value && !player.bathingClothes.Value &&
            !player.onBridge.Value && Game1.activeClickableMenu is null;
        var buffContractComplete = buffData is not null && buffData.Duration == 600000 &&
            buffData.MaxDuration == -1 && !buffData.IsDebuff && buffData.IconSpriteIndex == 24 &&
            string.Equals(buffData.IconTexture, "TileSheets\\BuffsIcons", StringComparison.Ordinal) &&
            string.Equals(buffData.GlowColor, "#2000203F", StringComparison.OrdinalIgnoreCase) &&
            BuffAttributesAreEmpty(buffData.Effects) &&
            (buffData.ActionsOnApply is null || buffData.ActionsOnApply.Count == 0);
        var fingerprint = Sha256(JsonSerializer.Serialize(new
        {
            schema = "monster_musk.v1",
            location = location.NameOrUniqueName,
            player.FacingDirection,
            nativeBaseGate,
            active = activeBuff is not null,
            remaining = activeBuff?.millisecondsDuration ?? 0,
            total = activeBuff?.totalMillisecondsDuration ?? 0,
            buffContractComplete,
            rows
        }));

        return new
        {
            schema_version = "monster_musk.v1",
            projection_status = "complete_current_native_monster_musk_context",
            projection_fingerprint = fingerprint,
            projection_tick = unchecked((long)Game1.ticks),
            native_use_gate_status = nativeBaseGate && buffContractComplete ? "ready" :
                rows.Length == 0 ? "blocked_no_inventory_monster_musk" :
                !nativeBaseGate ? "blocked_base_object_use_gate" : "blocked_buff_data_contract",
            native_base_use_gate = new
            {
                can_move = player.canMove,
                visible_monster_musk_available = visibleItem,
                event_up = Game1.eventUp,
                festival = Game1.isFestival(),
                fade_to_black = Game1.fadeToBlack,
                swimming = player.swimming.Value,
                bathing_clothes = player.bathingClothes.Value,
                on_bridge = player.onBridge.Value,
                active_menu_clear = Game1.activeClickableMenu is null,
                passed = nativeBaseGate
            },
            buff_contract = new
            {
                id = "24",
                duration_ms = buffData?.Duration ?? -1,
                max_duration_ms = buffData?.MaxDuration ?? -1,
                is_debuff = buffData?.IsDebuff ?? false,
                icon_sprite_index = buffData?.IconSpriteIndex ?? -1,
                icon_texture = buffData?.IconTexture ?? string.Empty,
                glow_color = buffData?.GlowColor ?? string.Empty,
                effects_empty = BuffAttributesAreEmpty(buffData?.Effects),
                actions_on_apply_count = buffData?.ActionsOnApply?.Count ?? 0,
                reapply_semantics = "remove_same_id_then_replace"
            },
            active_buff = new
            {
                active = activeBuff is not null,
                remaining_ms = activeBuff?.millisecondsDuration ?? 0,
                total_ms = activeBuff?.totalMillisecondsDuration ?? 0
            },
            spawn_semantics = new
            {
                ordinary_mine_multiplier = 2,
                volcano_multiplier = 2,
                repellent_buff_id = "23",
                ordinary_mine_contract = "MineShaft.AnyOnlineFarmerHasBuff(\"24\")->monsterChance*=2_unless_native_buff23_branch",
                volcano_contract = "VolcanoDungeon onlineFarmer.hasBuff(\"24\")->monsterChance*=2"
            },
            animation_contract = new
            {
                facing_direction = 2,
                freeze_pause_ms = 1750,
                callback_delay_ms = 750,
                followup_animation_ms = 1400,
                sprite_count = 3,
                sprite_delays_ms = "0,100,200",
                sprite_motion_x_domain = "random_float[-1,1]",
                initial_sound = "steam",
                callback_sound = "croak"
            },
            native_contract = MonsterMuskNativeContract,
            rows
        };
    }

    private static bool BuffAttributesAreEmpty(object? attributes)
    {
        if (attributes is null)
            return true;
        return attributes.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .All(property => BuffAttributeValueIsEmpty(property.GetValue(attributes)));
    }

    private static bool BuffAttributeValueIsEmpty(object? value)
    {
        if (value is null)
            return true;
        if (value is string text)
            return string.IsNullOrEmpty(text);
        if (value is IEnumerable values)
            return !values.Cast<object>().Any();
        if (value is bool flag)
            return !flag;
        if (value.GetType().IsEnum)
            return Convert.ToInt64(value) == 0;
        return value is IConvertible convertible && Math.Abs(convertible.ToDouble(null)) < double.Epsilon;
    }
}
