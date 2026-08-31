using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.Contracts.State;

public static class PlayerEmoteIdentity
{
    public static IReadOnlyList<string> LockedBaseEmoteKeys { get; } = Array.AsReadOnly(new[]
    {
        "happy", "sad", "heart", "exclamation", "note", "sleep", "game", "question", "x", "pause",
        "blush", "angry", "yes", "no", "sick", "laugh", "surprised", "hi", "taunt", "uh", "music", "jar"
    });

    public static IReadOnlyList<string> LockedBaseHiddenEmoteKeys { get; } =
        Array.AsReadOnly(new[] { "blush", "taunt", "music", "jar" });

    public static IReadOnlyList<string> LockedBaseDefaultFavoriteKeys { get; } =
        Array.AsReadOnly(new[] { "question", "heart", "yes", "happy", "pause", "sad", "no", "angry" });

    public static bool IsCompleteLockedBaseCatalog(PlayerEmoteOptionRef[] options) =>
        options.Select(option => option.EmoteKey).SequenceEqual(LockedBaseEmoteKeys, StringComparer.Ordinal) &&
        options.Where(option => option.Hidden).Select(option => option.EmoteKey)
            .SequenceEqual(LockedBaseHiddenEmoteKeys, StringComparer.Ordinal);

    public static string ComputeOptionFingerprint(PlayerEmoteOptionRef option) => Hash(new
    {
        option.EmoteIndex,
        option.EmoteKey,
        option.DisplayNameKey,
        option.DisplayName,
        option.IconIndex,
        option.Hidden,
        option.HasAnimation,
        option.AnimationFacingDirection,
        option.AnimationFrames,
        option.AnimationDurationMilliseconds,
        option.PerformedEntryPresent,
        option.PerformedValue,
        option.SelectorVisible,
        option.FavoriteSlots,
        option.NativeCommandAccepted
    });

    public static string ComputeProjectionFingerprint(PlayerEmoteProjectionRef projection) => Hash(new
    {
        projection.PlayerId,
        projection.LanguageCode,
        projection.NetworkRole,
        projection.CanEmoteNative,
        projection.ChatBoxPresent,
        projection.ChatBoxActive,
        projection.ChatInputWidthPixels,
        projection.ChatInputContentWidthPixels,
        projection.MenuClear,
        projection.DialogueUp,
        projection.ActiveMinigameType,
        projection.IsEmoting,
        projection.IsEmoteAnimating,
        projection.CurrentEmoteIconIndex,
        projection.CurrentEmoteFrameIndex,
        projection.FacingDirection,
        projection.RawFavorites,
        projection.EffectiveFavorites,
        projection.PerformedEmoteKeys,
        Options = Array.ConvertAll(projection.Emotes, option => option.OptionFingerprint)
    });

    private static string Hash<T>(T value)
    {
        var canonical = JsonSerializer.Serialize(value);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical)))
            .Replace("-", string.Empty).ToLowerInvariant();
    }
}
