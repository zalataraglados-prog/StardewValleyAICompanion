using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;

namespace StardewAI.Core.Infrastructure;

public sealed class FishingTerminalProbabilityProjectionContext
{
    internal FishingTerminalProbabilityProjectionContext(
        string targetLocationId,
        int rodSlotIndex,
        JsonElement[] tiles,
        JsonElement[] rules,
        HashSet<int>[] eligibleTileIndices,
        FishingTerminalRulePosition[] playerPositions,
        int? fishingLevel,
        int? baseFishingLevel,
        bool repeatedCastEquipmentStable,
        bool ruleInventoryComplete,
        string[] blockingReasons)
    {
        TargetLocationId = targetLocationId;
        RodSlotIndex = rodSlotIndex;
        Tiles = tiles;
        Rules = rules;
        EligibleTileIndices = eligibleTileIndices;
        PlayerPositions = playerPositions;
        FishingLevel = fishingLevel;
        BaseFishingLevel = baseFishingLevel;
        RepeatedCastEquipmentStable = repeatedCastEquipmentStable;
        RuleInventoryComplete = ruleInventoryComplete;
        BlockingReasons = blockingReasons;
    }

    internal string TargetLocationId { get; }
    internal int RodSlotIndex { get; }
    internal JsonElement[] Tiles { get; }
    internal JsonElement[] Rules { get; }
    internal HashSet<int>[] EligibleTileIndices { get; }
    internal FishingTerminalRulePosition[] PlayerPositions { get; }
    internal int? FishingLevel { get; }
    internal int? BaseFishingLevel { get; }
    internal bool RepeatedCastEquipmentStable { get; }
    internal bool RuleInventoryComplete { get; }
    internal string[] BlockingReasons { get; }
    internal ConcurrentDictionary<
        FishingTerminalProjectionCacheKey,
        FishingTerminalProbabilityResult> ProbabilityCache { get; } = new();
}

internal readonly record struct FishingTerminalProjectionCacheKey(
    string TargetQualifiedItemId,
    int WaterDepth,
    string EligibleRuleMask,
    string StandAllowedRuleMask);

internal readonly record struct FishingTerminalRulePosition(
    bool Restricted,
    int? X,
    int? Y,
    int? Width,
    int? Height)
{
    public bool Allows(int x, int y) => !Restricted ||
        X.HasValue && Y.HasValue && Width > 0 && Height > 0 &&
        x >= X.Value && y >= Y.Value &&
        x < X.Value + Width.Value &&
        y < Y.Value + Height.Value;
}
