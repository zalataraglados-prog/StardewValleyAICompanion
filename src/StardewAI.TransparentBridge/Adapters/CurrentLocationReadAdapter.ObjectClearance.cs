using StardewValley;
using StardewValley.Tools;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    private static ObjectClearanceProjection ReadObjectClearance(StardewObject item, Farmer player)
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
