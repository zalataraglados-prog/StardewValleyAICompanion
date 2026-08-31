using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string PlayerEmoteRuntimeNativeContract =
        "EmoteMenu.ConfirmSelection->ChatBox.textBoxEnter('/emote '+key)->ChatCommands.Emote->Farmer.CanEmote->Farmer.netDoEmote->doEmoteEvent->Farmer.performPlayerEmote->performedEmotes_and_native_icon_or_animation";

    private void StartPlayerEmote(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (string.IsNullOrWhiteSpace(request.EmoteKey) || string.IsNullOrWhiteSpace(request.EmoteReason) ||
            request.ConfirmEmote != true || request.EmoteProjectionFingerprint.Length != 64 ||
            request.EmoteOptionFingerprint.Length != 64 || !request.EmoteIndex.HasValue ||
            !request.EmoteIconIndex.HasValue || !request.EmoteHasAnimation.HasValue ||
            !request.EmoteAnimationFacingDirection.HasValue || !request.EmoteAnimationDurationMilliseconds.HasValue ||
            !request.EmoteHidden.HasValue || !request.EmotePerformedEntryBefore.HasValue ||
            !request.EmotePerformedValueBefore.HasValue || !request.EmotePlayerId.HasValue ||
            !request.EmoteLanguageCode.HasValue || !request.EmoteChatInputWidthPixels.HasValue ||
            !request.EmoteChatInputContentWidthPixels.HasValue || request.EmoteNativeInput != "/emote " + request.EmoteKey ||
            request.NativeContract != PlayerEmoteRuntimeNativeContract)
            reasons.Add("emote_complete_typed_request_required");

        var live = ReadLivePlayerEmoteProjection();
        var option = live?.Emotes.FirstOrDefault(row => row.EmoteKey == request.EmoteKey);
        if (live is null)
            reasons.Add("emote_live_projection_unavailable");
        else if (live.ProjectionFingerprint != request.EmoteProjectionFingerprint)
            reasons.Add("emote_projection_fingerprint_drifted");
        if (option is null)
            reasons.Add("emote_selected_option_unavailable");
        else if (option.OptionFingerprint != request.EmoteOptionFingerprint)
            reasons.Add("emote_option_fingerprint_drifted");
        if (live is not null && option is not null && !PlayerEmoteRequestMatches(request, live, option))
            reasons.Add("emote_typed_state_drifted");

        var chat = Game1.chatBox;
        if (reasons.Count > 0 || live is null || option is null || chat is null)
        {
            pending.Completion.SetResult(PlayerEmoteBlocked(request, reasons.ToArray()));
            return;
        }

        var beforeCount = chat.messages.Count;
        chat.activate();
        foreach (var character in request.EmoteNativeInput)
            chat.chatBox.RecieveTextInput(character);
        var typed = ChatMessage.makeMessagePlaintext(chat.chatBox.finalText, include_color_information: false);
        if (typed != request.EmoteNativeInput)
        {
            chat.clickAway();
            pending.Completion.SetResult(PlayerEmoteBlocked(request,
                "emote_native_input_width_or_strict_platform_filter_rejected_command"));
            return;
        }

        chat.textBoxEnter(chat.chatBox);
        var inputReset = !chat.isActive() && chat.chatBox.currentWidth == 0f &&
            ChatMessage.makeMessagePlaintext(chat.chatBox.finalText, include_color_information: false).Length == 0;
        if (!inputReset)
        {
            chat.clickAway();
            pending.Completion.SetResult(PlayerEmoteBlocked(request, "emote_native_chat_input_did_not_reset"));
            return;
        }
        activePlayerEmote = new ActivePlayerEmote(pending, beforeCount);
    }

    private void TickPlayerEmoteSafely()
    {
        var active = activePlayerEmote;
        if (active is null) return;
        try
        {
            active.ElapsedTicks++;
            var request = active.Pending.Request;
            if (Game1.player.UniqueMultiplayerID != request.EmotePlayerId)
            {
                CompletePlayerEmote(active, false, "emote_player_identity_drifted");
                return;
            }
            var entry = Game1.player.performedEmotes.ContainsKey(request.EmoteKey);
            var value = entry && Game1.player.performedEmotes[request.EmoteKey];
            var iconObserved = request.EmoteIconIndex < 0 ||
                Game1.player.isEmoting && Game1.player.CurrentEmote == request.EmoteIconIndex;
            var animationObserved = request.EmoteHasAnimation != true ||
                Game1.player.isEmoteAnimating &&
                Game1.player.emoteFacingDirection == request.EmoteAnimationFacingDirection;
            if (entry && value && iconObserved && animationObserved)
            {
                CompletePlayerEmote(active, true);
                return;
            }
            if (active.ElapsedTicks > 120)
                CompletePlayerEmote(active, false, "emote_native_event_receipt_timeout");
        }
        catch (Exception ex)
        {
            Monitor.Log($"Player emote execution failed and was blocked: {ex}", LogLevel.Error);
            CompletePlayerEmote(active, false, "emote_executor_exception:" + ex.GetType().Name);
        }
    }

    private void CompletePlayerEmote(ActivePlayerEmote active, bool verified, params string[] reasons)
    {
        activePlayerEmote = null;
        var request = active.Pending.Request;
        var entry = Game1.player.performedEmotes.ContainsKey(request.EmoteKey);
        var value = entry && Game1.player.performedEmotes[request.EmoteKey];
        var iconObserved = request.EmoteIconIndex < 0 ||
            Game1.player.isEmoting && Game1.player.CurrentEmote == request.EmoteIconIndex;
        var animationObserved = request.EmoteHasAnimation != true ||
            Game1.player.isEmoteAnimating && Game1.player.emoteFacingDirection == request.EmoteAnimationFacingDirection;
        var verification = verified
            ? new[]
            {
                "native_ChatTextBox_exact_emote_command_input_verified",
                "native_ChatBox_textBoxEnter_ChatCommands_Emote_route_invoked",
                "native_doEmoteEvent_local_performed_entry_observed",
                request.EmoteIconIndex >= 0 ? "exact_native_emote_icon_observed" : "no_icon_branch_verified",
                request.EmoteHasAnimation == true ? "exact_native_emote_animation_observed" : "no_animation_branch_verified",
                "remote_visibility_owned_by_native_net_event"
            }
            : reasons.Length == 0 ? new[] { "emote_native_receipt_mismatch" } : reasons;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "player_command_executor_verification_only",
            PrimitiveKind = "perform_emote",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verification,
            RequestedEffect = "emote=" + request.EmoteKey + ";native_command_receipt=true",
            ObservedEffect = "emote=" + request.EmoteKey + ";performed_entry=" + entry.ToString().ToLowerInvariant() +
                ";performed_value=" + value.ToString().ToLowerInvariant() +
                ";icon_observed=" + iconObserved.ToString().ToLowerInvariant() +
                ";animation_observed=" + animationObserved.ToString().ToLowerInvariant() +
                ";chat_messages_delta=" + (Game1.chatBox.messages.Count - active.ChatMessageCountBefore),
            EmoteKey = request.EmoteKey,
            EmoteIndex = request.EmoteIndex,
            EmoteIconIndex = request.EmoteIconIndex,
            EmotePerformedEntryAfter = entry,
            EmotePerformedValueAfter = value,
            EmoteIconReceiptObserved = iconObserved,
            EmoteAnimationReceiptObserved = animationObserved,
            EmoteCurrentIconIndexAfter = Game1.player.CurrentEmote,
            EmoteNetworkRole = request.EmoteNetworkRole,
            EmoteNativeCommandReceiptVerified = verified,
            BlockReasons = verified ? Array.Empty<string>() : verification,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.emote.performed_emotes[" + request.EmoteKey + "]", Before = request.EmotePerformedEntryBefore?.ToString().ToLowerInvariant() ?? "unknown", After = entry.ToString().ToLowerInvariant() },
                new SimulatedFactChange { Path = "player.emote.native_icon_or_animation_active", Before = "false", After = (iconObserved || animationObserved).ToString().ToLowerInvariant() }
            }
        });
    }

    private static bool PlayerEmoteRequestMatches(
        TrainingExecutionRequest request,
        PlayerEmoteProjectionRef live,
        PlayerEmoteOptionRef option) =>
        live.ServiceStatus == "ready" && live.CanEmoteNative && live.MenuClear && live.ChatBoxPresent &&
        !live.ChatBoxActive && !live.IsEmoting && !live.IsEmoteAnimating && option.NativeCommandAccepted &&
        live.PlayerId == request.EmotePlayerId && live.LanguageCode == request.EmoteLanguageCode &&
        live.NetworkRole == request.EmoteNetworkRole && live.ChatInputWidthPixels == request.EmoteChatInputWidthPixels &&
        live.ChatInputContentWidthPixels == request.EmoteChatInputContentWidthPixels &&
        option.EmoteIndex == request.EmoteIndex && option.IconIndex == request.EmoteIconIndex &&
        option.HasAnimation == request.EmoteHasAnimation &&
        option.AnimationFacingDirection == request.EmoteAnimationFacingDirection &&
        option.AnimationDurationMilliseconds == request.EmoteAnimationDurationMilliseconds &&
        option.Hidden == request.EmoteHidden && option.PerformedEntryPresent == request.EmotePerformedEntryBefore &&
        option.PerformedValue == request.EmotePerformedValueBefore;

    private static PlayerEmoteProjectionRef? ReadLivePlayerEmoteProjection()
    {
        var player = Game1.player;
        var chat = Game1.chatBox;
        if (player is null) return null;
        var rawFavorites = player.emoteFavorites.ToArray();
        var effectiveFavorites = rawFavorites.Length == 0
            ? PlayerEmoteIdentity.LockedBaseDefaultFavoriteKeys.ToArray()
            : rawFavorites;
        var options = Farmer.EMOTES.Select((emote, index) => RuntimePlayerEmoteOption(player, emote, index, effectiveFavorites)).ToArray();
        var menuClear = Game1.activeClickableMenu is null;
        var minigame = Game1.currentMinigame?.GetType().FullName ?? "none";
        var canEmote = player.CanEmote();
        var blocked = chat is null || !menuClear || Game1.dialogueUp || minigame != "none" ||
            chat.isActive() || !canEmote || player.isEmoting || player.isEmoteAnimating;
        var catalogComplete = PlayerEmoteIdentity.IsCompleteLockedBaseCatalog(options);
        var projection = new PlayerEmoteProjectionRef
        {
            ProjectionStatus = catalogComplete ? "complete_locked_base_1.6.15" : "runtime_catalog_mismatch",
            NativeContract = PlayerEmoteRuntimeNativeContract,
            ServiceStatus = blocked || !catalogComplete ? "blocked" : "ready",
            PlayerId = player.UniqueMultiplayerID,
            LanguageCode = (int)LocalizedContentManager.CurrentLanguageCode,
            NetworkRole = Game1.IsServer ? "server" : Game1.IsClient ? "client" : "singleplayer",
            CanEmoteNative = canEmote,
            ChatBoxPresent = chat is not null,
            ChatBoxActive = chat?.isActive() == true,
            ChatInputWidthPixels = chat?.chatBox.Width ?? 0,
            ChatInputContentWidthPixels = chat is null ? 0 : chat.chatBox.Width - 16,
            MenuClear = menuClear,
            DialogueUp = Game1.dialogueUp,
            ActiveMinigameType = minigame,
            IsEmoting = player.isEmoting,
            IsEmoteAnimating = player.isEmoteAnimating,
            CurrentEmoteIconIndex = player.CurrentEmote,
            CurrentEmoteFrameIndex = player.CurrentEmoteIndex,
            FacingDirection = player.FacingDirection,
            RawFavorites = rawFavorites,
            EffectiveFavorites = effectiveFavorites,
            PerformedEmoteKeys = player.performedEmotes.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            Emotes = options
        };
        projection.ProjectionFingerprint = PlayerEmoteIdentity.ComputeProjectionFingerprint(projection);
        return projection;
    }

    private static PlayerEmoteOptionRef RuntimePlayerEmoteOption(
        Farmer player,
        Farmer.EmoteType emote,
        int index,
        string[] effectiveFavorites)
    {
        var frames = emote.animationFrames?.Select(frame => new PlayerEmoteAnimationFrameRef
        {
            FrameIndex = frame.frame,
            DurationMilliseconds = frame.milliseconds,
            PositionOffset = frame.positionOffset,
            ArmOffset = frame.armOffset,
            XOffset = frame.xOffset,
            Flip = frame.flip,
            HasStartCallback = frame.frameStartBehavior is not null,
            HasEndCallback = frame.frameEndBehavior is not null
        }).ToArray() ?? Array.Empty<PlayerEmoteAnimationFrameRef>();
        var performedEntry = player.performedEmotes.ContainsKey(emote.emoteString);
        var option = new PlayerEmoteOptionRef
        {
            EmoteIndex = index,
            EmoteKey = emote.emoteString,
            DisplayNameKey = emote.displayNameKey,
            DisplayName = emote.displayName,
            IconIndex = emote.emoteIconIndex,
            Hidden = emote.hidden,
            HasAnimation = frames.Length > 0,
            AnimationFacingDirection = emote.facingDirection,
            AnimationFrames = frames,
            AnimationDurationMilliseconds = frames.Sum(frame => frame.DurationMilliseconds),
            PerformedEntryPresent = performedEntry,
            PerformedValue = performedEntry && player.performedEmotes[emote.emoteString],
            SelectorVisible = !emote.hidden || performedEntry,
            FavoriteSlots = effectiveFavorites.Select((key, slot) => new { key, slot })
                .Where(row => row.key == emote.emoteString).Select(row => row.slot).ToArray(),
            NativeCommandAccepted = emote.emoteString.Length is > 0 and <= 16
        };
        option.OptionFingerprint = PlayerEmoteIdentity.ComputeOptionFingerprint(option);
        return option;
    }

    private static TrainingExecutionResult PlayerEmoteBlocked(TrainingExecutionRequest request, params string[] reasons)
    {
        var result = BlockedWithPrimitive(request, "perform_emote", "emote=" + request.EmoteKey,
            "emote=" + request.EmoteKey + ";status=not_started_or_unverified", reasons.Distinct(StringComparer.Ordinal).ToArray());
        result.EmoteKey = request.EmoteKey;
        result.EmoteIndex = request.EmoteIndex;
        result.EmoteIconIndex = request.EmoteIconIndex;
        result.EmoteNetworkRole = request.EmoteNetworkRole;
        result.EmoteNativeCommandReceiptVerified = false;
        return result;
    }

    private sealed class ActivePlayerEmote
    {
        public ActivePlayerEmote(PendingExecution pending, int chatMessageCountBefore)
        {
            Pending = pending;
            ChatMessageCountBefore = chatMessageCountBefore;
        }

        public PendingExecution Pending { get; }
        public int ChatMessageCountBefore { get; }
        public int ElapsedTicks { get; set; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
    }
}
