using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.State;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    internal const string PlayerEmoteNativeContract =
        "EmoteMenu.ConfirmSelection->ChatBox.textBoxEnter('/emote '+key)->ChatCommands.Emote->Farmer.CanEmote->Farmer.netDoEmote->doEmoteEvent->Farmer.performPlayerEmote->performedEmotes_and_native_icon_or_animation";

    private static PlayerEmoteProjectionRef? ReadPlayerEmote(Farmer? player)
    {
        if (player is null || !Context.IsWorldReady) return null;
        var rawFavorites = player.emoteFavorites.ToArray();
        var effectiveFavorites = rawFavorites.Length == 0
            ? PlayerEmoteIdentity.LockedBaseDefaultFavoriteKeys.ToArray()
            : rawFavorites;
        var performedKeys = player.performedEmotes.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
        var options = Farmer.EMOTES.Select((emote, index) => ReadPlayerEmoteOption(
            player, emote, index, effectiveFavorites)).ToArray();
        var chat = Game1.chatBox;
        var menuClear = Game1.activeClickableMenu is null;
        var minigame = Game1.currentMinigame?.GetType().FullName ?? "none";
        var canEmote = player.CanEmote();
        var reasons = new List<string>();
        if (!PlayerEmoteIdentity.IsCompleteLockedBaseCatalog(options))
            reasons.Add("emote_locked_base_catalog_mismatch");
        if (chat is null) reasons.Add("emote_native_chat_box_unavailable");
        if (!menuClear) reasons.Add("emote_active_menu_conflict");
        if (Game1.dialogueUp) reasons.Add("emote_dialogue_conflict");
        if (minigame != "none") reasons.Add("emote_active_minigame_conflict");
        if (chat?.isActive() == true) reasons.Add("emote_chat_box_already_active");
        if (!canEmote) reasons.Add("emote_native_CanEmote_false");
        if (player.isEmoting || player.isEmoteAnimating) reasons.Add("emote_previous_emote_still_active");

        var projection = new PlayerEmoteProjectionRef
        {
            ProjectionStatus = PlayerEmoteIdentity.IsCompleteLockedBaseCatalog(options)
                ? "complete_locked_base_1.6.15"
                : "runtime_catalog_mismatch",
            NativeContract = PlayerEmoteNativeContract,
            ServiceStatus = reasons.Count == 0 ? "ready" : "blocked",
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
            PerformedEmoteKeys = performedKeys,
            Emotes = options,
            BlockedDiagnostics = reasons.ToArray()
        };
        projection.ProjectionFingerprint = PlayerEmoteIdentity.ComputeProjectionFingerprint(projection);
        return projection;
    }

    private static PlayerEmoteOptionRef ReadPlayerEmoteOption(
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
        var performedEntryPresent = player.performedEmotes.ContainsKey(emote.emoteString);
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
            PerformedEntryPresent = performedEntryPresent,
            PerformedValue = performedEntryPresent && player.performedEmotes[emote.emoteString],
            SelectorVisible = !emote.hidden || performedEntryPresent,
            FavoriteSlots = effectiveFavorites.Select((key, slot) => new { key, slot })
                .Where(row => row.key == emote.emoteString).Select(row => row.slot).ToArray(),
            NativeCommandAccepted = emote.emoteString.Length is > 0 and <= 16
        };
        option.OptionFingerprint = PlayerEmoteIdentity.ComputeOptionFingerprint(option);
        return option;
    }
}
