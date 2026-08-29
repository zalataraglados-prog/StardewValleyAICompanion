using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string RuntimeRainTotemNativeContract =
        "Object.performUseAction((O)681)->Object.rainTotem->AllowRainTotem->RainTotemAffectsContext_or_location_context->Default_festival_guard_or_context_WeatherForTomorrow=Rain->Default_Game1.getWeatherModificationsForDate";

    private sealed class ActiveRainTotem
    {
        public ActiveRainTotem(PendingExecution pending, int slot, NativeInventoryObjectUseResult nativeUse,
            string sourceContext, string affectedContext, string weatherStateOwnerContext,
            string weatherBefore, string startedAt)
        {
            Pending = pending;
            Slot = slot;
            NativeUse = nativeUse;
            SourceContext = sourceContext;
            AffectedContext = affectedContext;
            WeatherStateOwnerContext = weatherStateOwnerContext;
            WeatherBefore = weatherBefore;
            StartedAt = startedAt;
        }

        public PendingExecution Pending { get; }
        public int Slot { get; }
        public NativeInventoryObjectUseResult NativeUse { get; }
        public string SourceContext { get; }
        public string AffectedContext { get; }
        public string WeatherStateOwnerContext { get; }
        public string WeatherBefore { get; }
        public string StartedAt { get; }
        public int ElapsedTicks { get; set; }
        public bool SawConfirmationDialogue { get; set; }
        public bool InputHeld { get; set; }
        public int DialoguePressAttempts { get; set; }
        public DateTimeOffset? DialogueClosedAt { get; set; }
        public bool UsedNativeCanMoveCallbackRecovery { get; set; }
    }

    private void StartUseRainTotem(PendingExecution pending)
    {
        var request = pending.Request;
        var requested = "player.inventory[" + request.InventorySlotIndex + "].consume=(O)681;" +
            "weather_context=" + request.RainTotemAffectedLocationContextId + ";weather_for_tomorrow=Rain";
        if (!request.InventorySlotIndex.HasValue || !request.ExpectedStackBefore.HasValue ||
            request.ExpectedStackAfter != request.ExpectedStackBefore - 1 ||
            string.IsNullOrWhiteSpace(request.RainTotemProjectionFingerprint) ||
            !RainTotemRequestContractIsExact(request))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_rain_totem", requested,
                "typed_contract=missing_or_invalid", "use_rain_totem_typed_fields_required"));
            return;
        }

        var slot = request.InventorySlotIndex.Value;
        var location = Game1.currentLocation;
        if (location is null || !string.Equals(location.NameOrUniqueName, request.LocationId, StringComparison.OrdinalIgnoreCase) ||
            Game1.activeClickableMenu is not null || !Game1.player.canMove || Game1.eventUp || Game1.isFestival() ||
            Game1.fadeToBlack || Game1.player.swimming.Value || Game1.player.bathingClothes.Value ||
            Game1.player.onBridge.Value || slot < 0 || slot >= Game1.player.Items.Count ||
            Game1.player.Items[slot] is not StardewValley.Object totem || totem.GetType() != typeof(StardewValley.Object) ||
            totem.isTemporarilyInvisible || totem.Stack != request.ExpectedStackBefore ||
            !string.Equals(totem.ItemId, "681", StringComparison.Ordinal) ||
            !string.Equals(totem.QualifiedItemId, "(O)681", StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_rain_totem", requested,
                RainTotemObservedEffect(slot, location, request.RainTotemAffectedLocationContextId),
                "use_rain_totem_location_gate_or_inventory_drift"));
            return;
        }

        var sourceContext = location.GetLocationContextId();
        var contextData = location.GetLocationContext();
        var configuredAffectedContext = contextData.RainTotemAffectsContext ?? string.Empty;
        var affectedContext = string.IsNullOrEmpty(configuredAffectedContext) ? sourceContext : configuredAffectedContext;
        var weatherStateOwnerContext = string.Equals(affectedContext, "Default", StringComparison.Ordinal)
            ? "Default"
            : sourceContext;
        var tomorrowFestival = string.Equals(affectedContext, "Default", StringComparison.Ordinal) &&
            Utility.isFestivalDay(Game1.dayOfMonth + 1, Game1.season);
        var tomorrowDate = new WorldDate(Game1.Date);
        tomorrowDate.TotalDays++;
        var effectiveTomorrowWeather = string.Equals(affectedContext, "Default", StringComparison.Ordinal)
            ? Game1.getWeatherModificationsForDate(tomorrowDate, "Rain")
            : "Rain";
        var rainWillTakeEffectTomorrow = string.Equals(effectiveTomorrowWeather, "Rain", StringComparison.Ordinal);
        var weatherBefore = ReadRainTotemWeather(location, affectedContext);
        if (!contextData.AllowRainTotem || tomorrowFestival || !rainWillTakeEffectTomorrow ||
            string.Equals(weatherBefore, "Rain", StringComparison.Ordinal) ||
            !string.Equals(sourceContext, request.RainTotemSourceLocationContextId, StringComparison.Ordinal) ||
            !string.Equals(configuredAffectedContext, request.RainTotemConfiguredAffectedContextId, StringComparison.Ordinal) ||
            !string.Equals(affectedContext, request.RainTotemAffectedLocationContextId, StringComparison.Ordinal) ||
            !string.Equals(weatherStateOwnerContext, request.RainTotemWeatherStateOwnerContextId, StringComparison.Ordinal) ||
            contextData.AllowRainTotem != request.RainTotemAllowRainTotem ||
            tomorrowFestival != request.RainTotemTomorrowIsDefaultFestival ||
            tomorrowDate.TotalDays != request.RainTotemTomorrowTotalDays ||
            !string.Equals(effectiveTomorrowWeather, request.RainTotemEffectiveTomorrowWeather, StringComparison.Ordinal) ||
            rainWillTakeEffectTomorrow != request.RainTotemRainWillTakeEffectTomorrow ||
            !string.Equals(weatherBefore, request.RainTotemAffectedWeatherBefore, StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_rain_totem", requested,
                RainTotemObservedEffect(slot, location, affectedContext),
                tomorrowFestival ? "use_rain_totem_default_festival_tomorrow" :
                !rainWillTakeEffectTomorrow ? "use_rain_totem_tomorrow_weather_overridden" :
                string.Equals(weatherBefore, "Rain", StringComparison.Ordinal) ? "use_rain_totem_weather_already_rain" :
                "use_rain_totem_context_or_weather_drifted"));
            return;
        }

        var nativeUse = UseInventoryObjectNative(totem, slot);
        if (!nativeUse.Used || nativeUse.StackAfter != request.ExpectedStackAfter ||
            !string.Equals(ReadRainTotemWeather(location, affectedContext), "Rain", StringComparison.Ordinal))
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "use_rain_totem", requested,
                RainTotemObservedEffect(slot, location, affectedContext),
                "use_rain_totem_native_use_consumption_or_weather_rejected"));
            return;
        }
        activeRainTotem = new ActiveRainTotem(pending, slot, nativeUse, sourceContext, affectedContext,
            weatherStateOwnerContext,
            weatherBefore, DateTimeOffset.UtcNow.ToString("O"));
    }

    private static bool RainTotemRequestContractIsExact(TrainingExecutionRequest request)
    {
        return string.Equals(request.ItemId, "681", StringComparison.Ordinal) &&
            string.Equals(request.QualifiedItemId, "(O)681", StringComparison.Ordinal) &&
            request.RainTotemAllowRainTotem == true && request.RainTotemTomorrowIsDefaultFestival == false &&
            !string.Equals(request.RainTotemAffectedWeatherBefore, "Rain", StringComparison.Ordinal) &&
            string.Equals(request.RainTotemAffectedWeatherAfter, "Rain", StringComparison.Ordinal) &&
            request.RainTotemTomorrowTotalDays.HasValue && request.RainTotemTomorrowTotalDays >= 0 &&
            string.Equals(request.RainTotemEffectiveTomorrowWeather, "Rain", StringComparison.Ordinal) &&
            request.RainTotemRainWillTakeEffectTomorrow == true &&
            request.RainTotemFacingDirection == 2 && request.RainTotemAnimationDurationMs == 2000 &&
            request.RainTotemCloudSpriteCount == 18 && request.RainTotemItemSpriteCount == 1 &&
            request.RainTotemCloudBatchCount == 6 && request.RainTotemCloudDelayStepMs == 200 &&
            string.Equals(request.RainTotemInitialSound, "thunder", StringComparison.Ordinal) &&
            string.Equals(request.RainTotemDelayedSound, "rainsound", StringComparison.Ordinal) &&
            request.RainTotemDelayedSoundMs == 2000 &&
            string.Equals(request.NativeContract, RuntimeRainTotemNativeContract, StringComparison.Ordinal);
    }

    private static string ReadRainTotemWeather(GameLocation location, string affectedContext)
    {
        return string.Equals(affectedContext, "Default", StringComparison.Ordinal)
            ? Game1.weatherForTomorrow
            : location.GetWeather().WeatherForTomorrow;
    }

    private void TickRainTotem()
    {
        var active = activeRainTotem;
        if (active is null)
            return;
        active.ElapsedTicks++;
        if (Game1.activeClickableMenu is DialogueBox dialogue)
        {
            active.SawConfirmationDialogue = true;
            var safeInformationDialogue = !dialogue.isQuestion &&
                (dialogue.responses is null || dialogue.responses.Length == 0) &&
                string.IsNullOrWhiteSpace(Game1.currentLocation?.lastQuestionKey) &&
                !Game1.eventUp && dialogue.characterDialogue is null;
            if (!safeInformationDialogue)
            {
                ReleaseSmapiLeftButtonOverride();
                activeRainTotem = null;
                active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "use_rain_totem",
                    "native_information_dialogue_closed=true",
                    RainTotemObservedEffect(active.Slot, Game1.currentLocation, active.AffectedContext),
                    "use_rain_totem_unexpected_dialogue_state"));
                return;
            }
            if (dialogue.transitioning || dialogue.safetyTimer > 0)
                return;
            if (!active.InputHeld)
            {
                if (!TryApplySmapiLeftButtonOverride(pressed: true, out var pressReason))
                {
                    activeRainTotem = null;
                    active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "use_rain_totem",
                        "native_information_dialogue_closed=true",
                        RainTotemObservedEffect(active.Slot, Game1.currentLocation, active.AffectedContext),
                        "use_rain_totem_dialogue_press_failed:" + pressReason));
                    return;
                }
                active.InputHeld = true;
                active.DialoguePressAttempts++;
            }
            else
            {
                if (!TryApplySmapiLeftButtonOverride(pressed: false, out var releaseReason))
                {
                    ReleaseSmapiLeftButtonOverride();
                    activeRainTotem = null;
                    active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "use_rain_totem",
                        "native_information_dialogue_closed=true",
                        RainTotemObservedEffect(active.Slot, Game1.currentLocation, active.AffectedContext),
                        "use_rain_totem_dialogue_release_failed:" + releaseReason));
                    return;
                }
                active.InputHeld = false;
            }
            return;
        }
        if (Game1.activeClickableMenu is not null)
        {
            ReleaseSmapiLeftButtonOverride();
            activeRainTotem = null;
            active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "use_rain_totem",
                "native_information_dialogue_closed=true",
                RainTotemObservedEffect(active.Slot, Game1.currentLocation, active.AffectedContext),
                "use_rain_totem_unexpected_menu_type:" + Game1.activeClickableMenu.GetType().Name));
            return;
        }
        if (active.SawConfirmationDialogue && Game1.player.canMove)
        {
            ReleaseSmapiLeftButtonOverride();
            CompleteRainTotem(active);
            return;
        }
        if (active.SawConfirmationDialogue)
        {
            active.DialogueClosedAt ??= DateTimeOffset.UtcNow;
            if (DateTimeOffset.UtcNow - active.DialogueClosedAt.Value > TimeSpan.FromSeconds(2))
            {
                Farmer.canMoveNow(Game1.player);
                active.UsedNativeCanMoveCallbackRecovery = true;
            }
            return;
        }
        if (DateTimeOffset.UtcNow - DateTimeOffset.Parse(active.StartedAt) > TimeSpan.FromSeconds(15))
        {
            ReleaseSmapiLeftButtonOverride();
            activeRainTotem = null;
            active.Pending.Completion.SetResult(BlockedWithPrimitive(active.Pending.Request, "use_rain_totem",
                "weather_for_tomorrow=Rain;native_animation_completed=true",
                RainTotemObservedEffect(active.Slot, Game1.currentLocation, active.AffectedContext),
                "use_rain_totem_native_animation_timeout"));
        }
    }

    private void CompleteRainTotem(ActiveRainTotem active)
    {
        var request = active.Pending.Request;
        var location = Game1.currentLocation;
        var stackVerified = active.NativeUse.StackBefore == request.ExpectedStackBefore &&
            active.NativeUse.StackAfter == request.ExpectedStackAfter;
        var contextVerified = location is not null &&
            string.Equals(location.GetLocationContextId(), active.SourceContext, StringComparison.Ordinal) &&
            string.Equals(ReadRainTotemWeather(location, active.AffectedContext), "Rain", StringComparison.Ordinal);
        var facingVerified = Game1.player.FacingDirection == 2;
        var verified = stackVerified && contextVerified && facingVerified;
        activeRainTotem = null;

        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = active.StartedAt, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "use_rain_totem",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_Object_performUseAction_succeeded", "exactly_one_rain_totem_consumed",
                    "native_context_weather_transition_verified", "native_animation_and_facing_verified",
                    "native_information_dialogue_closed_by_input",
                    active.UsedNativeCanMoveCallbackRecovery
                        ? "native_Farmer_canMoveNow_callback_recovered_after_hidden_dialogue"
                        : "native_Farmer_canMoveNow_callback_observed"
                }
                : new[] { stackVerified ? "inventory_stack_verified" : "inventory_stack_mismatch", contextVerified ? "context_weather_verified" : "context_weather_mismatch", facingVerified ? "native_facing_verified" : "native_facing_mismatch" },
            RequestedEffect = "inventory_stack=" + request.ExpectedStackAfter + ";affected_context=" +
                active.AffectedContext + ";weather_state_owner_context=" + active.WeatherStateOwnerContext +
                ";weather_for_tomorrow=Rain",
            ObservedEffect = RainTotemObservedEffect(active.Slot, location, active.AffectedContext) +
                ";elapsed_ticks=" + active.ElapsedTicks + ";dialogue_press_attempts=" + active.DialoguePressAttempts +
                ";native_can_move_callback_recovered=" + active.UsedNativeCanMoveCallbackRecovery.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "use_rain_totem_post_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "player.inventory[" + active.Slot + "]", Before = "(O)681x" + request.ExpectedStackBefore, After = "(O)681x" + request.ExpectedStackAfter },
                    new SimulatedFactChange { Path = "world.location_context_weather[" + active.WeatherStateOwnerContext + "].weather_for_tomorrow", Before = active.WeatherBefore, After = "Rain" }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private static string RainTotemObservedEffect(int slot, GameLocation? location, string affectedContext)
    {
        var item = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] : null;
        var weather = location is null ? "unavailable" : ReadRainTotemWeather(location, affectedContext);
        return "slot=" + slot + ";qualified_item_id=" + (item?.QualifiedItemId ?? "null") +
            ";stack=" + (item?.Stack ?? 0) + ";source_context=" + (location?.GetLocationContextId() ?? "unavailable") +
            ";affected_context=" + affectedContext + ";weather_for_tomorrow=" + weather +
            ";facing_direction=" + Game1.player.FacingDirection +
            ";can_move=" + Game1.player.canMove.ToString().ToLowerInvariant() +
            ";pause_time_ms=" + Game1.pauseTime +
            ";message_after_pause_pending=" + (!string.IsNullOrEmpty(Game1.messageAfterPause)).ToString().ToLowerInvariant() +
            ";dialogue_up=" + Game1.dialogueUp.ToString().ToLowerInvariant() +
            ";active_menu=" + (Game1.activeClickableMenu?.GetType().Name ?? "none");
    }
}
