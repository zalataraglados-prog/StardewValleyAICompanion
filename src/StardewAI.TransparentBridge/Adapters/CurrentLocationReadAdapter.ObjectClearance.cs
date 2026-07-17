using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
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
            OutputQuantity = outputComplete ? 1 : null
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
            DefenseBookMailExpectedAfter = defenseBookMailBefore || defenseBookDropped
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
