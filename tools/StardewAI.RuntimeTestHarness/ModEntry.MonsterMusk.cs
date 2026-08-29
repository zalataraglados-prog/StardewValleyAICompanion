using System.Collections;
using System.Reflection;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeMonsterMuskNativeContract =
        "Object.performUseAction((O)879)->750ms_callback_Object.MonsterMusk->Farmer.applyBuff(24)->BuffManager.Apply_remove_then_replace";

    private sealed class ActiveMonsterMusk
    {
        public ActiveMonsterMusk(PendingExecution pending, int slot, NativeInventoryObjectUseResult nativeUse,
            Buff? buffBefore, string startedAt)
        {
            Pending = pending;
            Slot = slot;
            NativeUse = nativeUse;
            BuffBefore = buffBefore;
            StartedAt = startedAt;
        }

        public PendingExecution Pending { get; }
        public int Slot { get; }
        public NativeInventoryObjectUseResult NativeUse { get; }
        public Buff? BuffBefore { get; }
        public string StartedAt { get; }
        public int ElapsedTicks { get; set; }
    }

    private void StartUseMonsterMusk(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = "player.inventory[" + request.InventorySlotIndex + "].consume=(O)879;buff_id=24";
        if (!request.InventorySlotIndex.HasValue || !request.ExpectedStackBefore.HasValue ||
            request.ExpectedStackAfter != request.ExpectedStackBefore - 1 ||
            !request.MonsterMuskBuffActiveBefore.HasValue || !request.MonsterMuskBuffRemainingBeforeMs.HasValue ||
            !request.MonsterMuskBuffTotalBeforeMs.HasValue ||
            string.IsNullOrWhiteSpace(request.MonsterMuskProjectionFingerprint) ||
            !MonsterMuskRequestContractIsExact(request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_monster_musk", requested,
                "typed_contract=missing_or_invalid", "use_monster_musk_typed_fields_required"));
            return;
        }

        var slot = request.InventorySlotIndex.Value;
        var location = Game1.currentLocation;
        if (location is null || !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase) ||
            Game1.activeClickableMenu is not null || !Game1.player.canMove || Game1.eventUp || Game1.isFestival() ||
            Game1.fadeToBlack || Game1.player.swimming.Value || Game1.player.bathingClothes.Value ||
            Game1.player.onBridge.Value || slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot] is not StardewValley.Object musk || musk.GetType() != typeof(StardewValley.Object) ||
            musk.isTemporarilyInvisible || musk.Stack != request.ExpectedStackBefore ||
            !string.Equals(musk.ItemId, "879", StringComparison.Ordinal) ||
            !string.Equals(musk.QualifiedItemId, "(O)879", StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_monster_musk", requested,
                MonsterMuskObservedEffect(slot), "use_monster_musk_location_gate_or_inventory_drift"));
            return;
        }

        Game1.player.buffs.AppliedBuffs.TryGetValue("24", out var buffBefore);
        if (!MonsterMuskActiveBuffMatchesRequest(buffBefore, request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_monster_musk", requested,
                MonsterMuskObservedEffect(slot), "use_monster_musk_active_buff_drifted"));
            return;
        }

        var nativeUse = UseInventoryObjectNative(musk, slot);
        if (!nativeUse.Used || nativeUse.StackAfter != request.ExpectedStackAfter)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_monster_musk", requested,
                MonsterMuskObservedEffect(slot), "use_monster_musk_native_use_or_consumption_rejected"));
            return;
        }
        activeMonsterMusk = new ActiveMonsterMusk(pending, slot, nativeUse, buffBefore,
            DateTimeOffset.UtcNow.ToString("O"));
    }

    private static bool MonsterMuskActiveBuffMatchesRequest(Buff? current, TrainingExecutionRequest request)
    {
        if ((current is not null) != request.MonsterMuskBuffActiveBefore)
            return false;
        if (current is null)
            return request.MonsterMuskBuffRemainingBeforeMs == 0 && request.MonsterMuskBuffTotalBeforeMs == 0;
        var projectedRemaining = request.MonsterMuskBuffRemainingBeforeMs.GetValueOrDefault(-1);
        var elapsedSinceProjection = projectedRemaining - current.millisecondsDuration;
        return current.totalMillisecondsDuration == request.MonsterMuskBuffTotalBeforeMs &&
            projectedRemaining <= current.totalMillisecondsDuration && elapsedSinceProjection is >= 0 and <= 5000;
    }

    private static bool MonsterMuskRequestContractIsExact(TrainingExecutionRequest request)
    {
        if (!DataLoader.Buffs(Game1.content).TryGetValue("24", out var data))
            return false;
        return string.Equals(request.ItemId, "879", StringComparison.Ordinal) &&
            string.Equals(request.QualifiedItemId, "(O)879", StringComparison.Ordinal) &&
            string.Equals(request.MonsterMuskBuffId, "24", StringComparison.Ordinal) &&
            request.MonsterMuskBuffDurationMs == data.Duration && data.Duration == 600000 &&
            request.MonsterMuskBuffMaxDurationMs == data.MaxDuration && data.MaxDuration == -1 &&
            request.MonsterMuskBuffIsDebuff == data.IsDebuff && !data.IsDebuff &&
            request.MonsterMuskBuffIconSpriteIndex == data.IconSpriteIndex && data.IconSpriteIndex == 24 &&
            string.Equals(request.MonsterMuskBuffIconTexture, data.IconTexture, StringComparison.Ordinal) &&
            string.Equals(data.IconTexture, "TileSheets\\BuffsIcons", StringComparison.Ordinal) &&
            string.Equals(request.MonsterMuskBuffGlowColor, data.GlowColor, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(data.GlowColor, "#2000203F", StringComparison.OrdinalIgnoreCase) &&
            request.MonsterMuskBuffEffectsEmpty == RuntimeBuffAttributesAreEmpty(data.Effects) &&
            request.MonsterMuskBuffEffectsEmpty == true &&
            request.MonsterMuskBuffActionsOnApplyCount == (data.ActionsOnApply?.Count ?? 0) &&
            request.MonsterMuskBuffActionsOnApplyCount == 0 &&
            string.Equals(request.MonsterMuskBuffReapplySemantics, "remove_same_id_then_replace", StringComparison.Ordinal) &&
            request.MonsterMuskOrdinaryMineSpawnMultiplier == 2 && request.MonsterMuskVolcanoSpawnMultiplier == 2 &&
            string.Equals(request.MonsterMuskRepellentBuffId, "23", StringComparison.Ordinal) &&
            request.MonsterMuskFacingDirection == 2 && request.MonsterMuskFreezePauseMs == 1750 &&
            request.MonsterMuskCallbackDelayMs == 750 && request.MonsterMuskFollowupAnimationMs == 1400 &&
            request.MonsterMuskSpriteCount == 3 && string.Equals(request.MonsterMuskSpriteDelaysMs, "0,100,200", StringComparison.Ordinal) &&
            string.Equals(request.MonsterMuskSpriteMotionXDomain, "random_float[-1,1]", StringComparison.Ordinal) &&
            string.Equals(request.MonsterMuskInitialSound, "steam", StringComparison.Ordinal) &&
            string.Equals(request.MonsterMuskCallbackSound, "croak", StringComparison.Ordinal) &&
            string.Equals(request.NativeContract, RuntimeMonsterMuskNativeContract, StringComparison.Ordinal);
    }

    private static bool RuntimeBuffAttributesAreEmpty(object? attributes)
    {
        if (attributes is null)
            return true;
        return attributes.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public).All(property =>
        {
            var value = property.GetValue(attributes);
            if (value is null) return true;
            if (value is string text) return string.IsNullOrEmpty(text);
            if (value is IEnumerable values) return !values.Cast<object>().Any();
            if (value is bool flag) return !flag;
            if (value.GetType().IsEnum) return Convert.ToInt64(value) == 0;
            return value is IConvertible convertible && Math.Abs(convertible.ToDouble(null)) < double.Epsilon;
        });
    }

    private void TickMonsterMusk()
    {
        var active = activeMonsterMusk;
        if (active is null)
            return;
        active.ElapsedTicks++;
        Game1.player.buffs.AppliedBuffs.TryGetValue("24", out var buffAfter);
        if (buffAfter is not null && !ReferenceEquals(buffAfter, active.BuffBefore) &&
            Game1.player.canMove && Game1.player.freezePause <= 0)
        {
            CompleteMonsterMusk(active, buffAfter);
            return;
        }
        if (active.ElapsedTicks > 300)
        {
            activeMonsterMusk = null;
            active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "use_monster_musk",
                "buff_id=24;native_callback_completed=true", MonsterMuskObservedEffect(active.Slot),
                "use_monster_musk_native_callback_timeout"));
        }
    }

    private void CompleteMonsterMusk(ActiveMonsterMusk active, Buff buffAfter)
    {
        var request = active.Pending.Request;
        var stackVerified = active.NativeUse.StackBefore == request.ExpectedStackBefore &&
            active.NativeUse.StackAfter == request.ExpectedStackAfter;
        var buffVerified = string.Equals(buffAfter.id, "24", StringComparison.Ordinal) &&
            buffAfter.totalMillisecondsDuration == 600000 && buffAfter.millisecondsDuration <= 600000 &&
            buffAfter.millisecondsDuration >= 580000 &&
            Game1.player.buffs.AppliedBuffs.Keys.Count(id => string.Equals(id, "24", StringComparison.Ordinal)) == 1;
        var replacementVerified = active.BuffBefore is null || !ReferenceEquals(active.BuffBefore, buffAfter);
        var facingVerified = Game1.player.FacingDirection == 2;
        var verified = stackVerified && buffVerified && replacementVerified && facingVerified;
        activeMonsterMusk = null;

        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = active.StartedAt, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "use_monster_musk",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_Object_performUseAction_succeeded", "exactly_one_monster_musk_consumed", "native_750ms_callback_observed", "buff_24_new_instance_and_duration_verified", "native_facing_verified" }
                : new[] { stackVerified ? "inventory_stack_verified" : "inventory_stack_mismatch", buffVerified ? "buff_24_verified" : "buff_24_mismatch", replacementVerified ? "buff_replacement_verified" : "buff_replacement_mismatch", facingVerified ? "native_facing_verified" : "native_facing_mismatch" },
            RequestedEffect = "inventory_stack=" + request.ExpectedStackAfter + ";buff_id=24;duration_ms=600000",
            ObservedEffect = MonsterMuskObservedEffect(active.Slot) + ";elapsed_ticks=" + active.ElapsedTicks,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "use_monster_musk_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "player.inventory[" + active.Slot + "]", Before = "(O)879x" + request.ExpectedStackBefore, After = "(O)879x" + request.ExpectedStackAfter },
                    new SimulatedFactChange { Path = "player.buffs[24]", Before = request.MonsterMuskBuffActiveBefore == true ? request.MonsterMuskBuffRemainingBeforeMs + "ms" : "absent", After = buffAfter.millisecondsDuration + "ms" }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private static string MonsterMuskObservedEffect(int slot)
    {
        var item = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] : null;
        Game1.player.buffs.AppliedBuffs.TryGetValue("24", out var buff);
        return "slot=" + slot + ";qualified_item_id=" + (item?.QualifiedItemId ?? "null") +
            ";stack=" + (item?.Stack ?? 0) + ";buff_active=" + (buff is not null).ToString().ToLowerInvariant() +
            ";buff_remaining_ms=" + (buff?.millisecondsDuration ?? 0) +
            ";buff_total_ms=" + (buff?.totalMillisecondsDuration ?? 0) +
            ";facing_direction=" + Game1.player.FacingDirection;
    }
}
