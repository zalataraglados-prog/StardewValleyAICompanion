using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using StardewValley;
using StardewValley.Locations;
using StardewValley.SaveSerialization;
using StardewValley.Tools;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static ObjectClearanceProjection ReadObjectClearance(GameLocation location, Vector2 tile, StardewObject item, Farmer player)
    {
        var clearKind = item.IsTwig()
            ? "twig"
            : item.QualifiedItemId is "(O)590" or "(O)SeedSpot"
                ? "artifact_spot"
                : string.Empty;
        if (string.IsNullOrWhiteSpace(clearKind))
        {
            return ObjectClearanceProjection.NotApplicable();
        }
        if (item.GetType() != typeof(StardewObject))
        {
            return ObjectClearanceProjection.Blocked(clearKind, "blocked_custom_clearable_object_runtime_type");
        }

        var requiredToolKind = clearKind == "twig" ? "axe" : "hoe";
        var selected = player.Items
            .Select((candidate, index) => new { Tool = candidate as Tool, SlotIndex = index })
            .OrderByDescending(row => row.SlotIndex == player.CurrentToolIndex)
            .FirstOrDefault(row =>
                (requiredToolKind == "axe" && row.Tool is Axe) ||
                (requiredToolKind == "hoe" && row.Tool is Hoe));
        if (selected?.Tool is null)
        {
            return ObjectClearanceProjection.Blocked(clearKind, "blocked_required_" + requiredToolKind + "_missing", requiredToolKind);
        }

        if (clearKind == "artifact_spot" && item.QualifiedItemId == "(O)SeedSpot")
        {
            return ProjectSeedSpotClearance(location, tile, player, selected.SlotIndex, requiredToolKind);
        }
        if (clearKind == "artifact_spot")
        {
            return ProjectArtifactSpotClearance(location, tile, player, (Hoe)selected.Tool, selected.SlotIndex, requiredToolKind);
        }

        var outputComplete = clearKind == "twig";
        return new ObjectClearanceProjection
        {
            ClearKind = clearKind,
            Status = outputComplete ? "ready" : "blocked_artifact_spot_output_projection_incomplete",
            RequiredToolKind = requiredToolKind,
            ToolSlotIndex = selected.SlotIndex,
            ExpectedToolHits = 1,
            SkillId = "foraging",
            SkillIndex = Farmer.foragingSkill,
            Experience = clearKind == "twig" ? 1 : 15,
            ExperienceCondition = clearKind == "twig"
                ? "native_axe_removes_twig"
                : "native_hoe_digs_artifact_or_seed_spot",
            ExperienceStatus = "exact",
            OutputStatus = outputComplete ? "exact" : "incomplete",
            OutputQualifiedItemId = outputComplete ? "(O)388" : string.Empty,
            OutputQuantity = outputComplete ? 1 : null,
            OutputItems = outputComplete
                ? new[] { ClearanceOutputItemProjection.FromStandard("(O)388") }
                : Array.Empty<ClearanceOutputItemProjection>()
        };
    }

    private static ObjectClearanceProjection ProjectSeedSpotClearance(
        GameLocation location,
        Vector2 tile,
        Farmer player,
        int toolSlotIndex,
        string requiredToolKind)
    {
        var random = Utility.CreateDaySaveRandom(
            (0f - tile.X) * 7f,
            tile.Y * 777f,
            Game1.netWorldState.Value.TreasureTotemsUsed * 777);
        var artifactSpotsDugBefore = player.stats.Get("ArtifactSpotsDug");
        if (artifactSpotsDugBefore >= int.MaxValue)
        {
            return ObjectClearanceProjection.Blocked("artifact_spot", "blocked_artifact_spot_stat_projection_overflow", requiredToolKind);
        }
        var artifactSpotsDugAfter = artifactSpotsDugBefore + 1;
        var defenseBookMailBefore = player.mailReceived.Contains("DefenseBookDropped");
        var defenseBookDropped = artifactSpotsDugAfter > 2 &&
            random.NextDouble() < 0.008 + (!defenseBookMailBefore ? artifactSpotsDugAfter * 0.002 : 0.005);
        var seed = Utility.getRaccoonSeedForCurrentTimeOfYear(player, random);
        var terrainFeatureExpectedAfter = location.terrainFeatures.TryGetValue(tile, out var existingFeature)
            ? existingFeature.GetType().Name
            : location is MineShaft mine && mine.getMineArea() == 77377
                ? "none"
                : "HoeDirt";

        return new ObjectClearanceProjection
        {
            ClearKind = "artifact_spot",
            Status = "ready",
            RequiredToolKind = requiredToolKind,
            ToolSlotIndex = toolSlotIndex,
            ExpectedToolHits = 1,
            SkillId = "foraging",
            SkillIndex = Farmer.foragingSkill,
            Experience = 15,
            ExperienceCondition = "native_hoe_digs_seed_spot",
            ExperienceStatus = "exact",
            OutputStatus = "exact",
            OutputQualifiedItemId = seed.QualifiedItemId,
            OutputQuantity = seed.Stack,
            BonusOutputQualifiedItemId = "(O)Book_Defense",
            BonusOutputQuantity = defenseBookDropped ? 1 : 0,
            ArtifactSpotsDugBefore = (int)artifactSpotsDugBefore,
            ArtifactSpotsDugDelta = 1,
            ArtifactSpotsDugExpectedAfter = (int)artifactSpotsDugAfter,
            TerrainFeatureExpectedAfter = terrainFeatureExpectedAfter,
            DefenseBookMailBefore = defenseBookMailBefore,
            DefenseBookMailExpectedAfter = defenseBookMailBefore || defenseBookDropped,
            OutputItems = new[] { ClearanceOutputItemProjection.FromStandard(seed.QualifiedItemId, seed.Stack) }
                .Concat(defenseBookDropped
                    ? new[] { ClearanceOutputItemProjection.FromStandard("(O)Book_Defense") }
                    : Array.Empty<ClearanceOutputItemProjection>())
                .OrderBy(output => output.QualifiedItemId, StringComparer.Ordinal)
                .ThenBy(output => output.RuntimeType, StringComparer.Ordinal)
                .ThenBy(output => output.Quality)
                .ToArray()
        };
    }
}

internal sealed class ObjectClearanceProjection
{
    public string ClearKind { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string RequiredToolKind { get; init; } = string.Empty;
    public int? ToolSlotIndex { get; init; }
    public int? ExpectedToolHits { get; init; }
    public string SkillId { get; init; } = string.Empty;
    public int? SkillIndex { get; init; }
    public int? Experience { get; init; }
    public string ExperienceCondition { get; init; } = string.Empty;
    public string ExperienceStatus { get; init; } = string.Empty;
    public string OutputStatus { get; init; } = string.Empty;
    public string OutputQualifiedItemId { get; init; } = string.Empty;
    public int? OutputQuantity { get; init; }
    public string BonusOutputQualifiedItemId { get; init; } = string.Empty;
    public int? BonusOutputQuantity { get; init; }
    public int? ArtifactSpotsDugBefore { get; init; }
    public int? ArtifactSpotsDugDelta { get; init; }
    public int? ArtifactSpotsDugExpectedAfter { get; init; }
    public string TerrainFeatureExpectedAfter { get; init; } = string.Empty;
    public bool? DefenseBookMailBefore { get; init; }
    public bool? DefenseBookMailExpectedAfter { get; init; }
    public ClearanceOutputItemProjection[] OutputItems { get; init; } = Array.Empty<ClearanceOutputItemProjection>();

    public static ObjectClearanceProjection Blocked(string clearKind, string status, string requiredToolKind = "")
    {
        return new ObjectClearanceProjection
        {
            ClearKind = clearKind,
            Status = status,
            RequiredToolKind = requiredToolKind,
            ExperienceStatus = "unavailable",
            OutputStatus = "unavailable"
        };
    }

    public static ObjectClearanceProjection NotApplicable()
    {
        return new ObjectClearanceProjection
        {
            Status = "not_applicable",
            ExperienceStatus = "not_applicable",
            OutputStatus = "not_applicable"
        };
    }
}

internal sealed record ClearanceOutputItemProjection(
    string RuntimeType,
    string QualifiedItemId,
    int Quality,
    string UnitStateSha256,
    int Quantity)
{
    private static readonly ConcurrentDictionary<string, ClearanceOutputItemProjection> StandardUnitCache = new(StringComparer.Ordinal);

    public static ClearanceOutputItemProjection FromStandard(string qualifiedItemId, int quantity = 1)
    {
        var unit = StandardUnitCache.GetOrAdd(
            qualifiedItemId,
            static id => From(ItemRegistry.Create(id)) with { Quantity = 1 });
        return unit with { Quantity = quantity };
    }

    public static ClearanceOutputItemProjection From(Item item)
    {
        var unit = item.getOne();
        unit.Stack = 1;
        using var stream = new MemoryStream();
        SaveSerializer.GetSerializer(unit.GetType()).Serialize(stream, unit);
        var stateHash = Convert.ToHexString(SHA256.HashData(stream.ToArray())).ToLowerInvariant();
        return new ClearanceOutputItemProjection(
            unit.GetType().FullName ?? unit.GetType().Name,
            unit.QualifiedItemId,
            unit.Quality,
            stateHash,
            item.Stack);
    }
}
