using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class FarmReadAdapter
{
    private static HarvestExperienceProjection ReadCropHarvestExperience(Crop crop)
    {
        if (crop.GetType() != typeof(Crop))
        {
            return HarvestExperienceProjection.Unavailable("unavailable_custom_crop_runtime_type");
        }

        if (crop.forageCrop.Value)
        {
            return crop.whichForageCrop.Value switch
            {
                "1" => HarvestExperienceProjection.Exact(
                    "foraging",
                    Farmer.foragingSkill,
                    3,
                    "native_player_harvest_of_spring_onion_crop"),
                "2" => HarvestExperienceProjection.Unavailable("unavailable_ginger_requires_hoe_branch"),
                _ => HarvestExperienceProjection.Unavailable("unavailable_unknown_forage_crop_id")
            };
        }

        if (string.IsNullOrWhiteSpace(crop.indexOfHarvest.Value))
        {
            return HarvestExperienceProjection.Exact(
                "farming",
                Farmer.farmingSkill,
                0,
                "native_crop_harvest_has_no_item_identity");
        }

        try
        {
            var experienceItemId = crop.indexOfHarvest.Value == "421" ? "431" : crop.indexOfHarvest.Value;
            if (ItemRegistry.Create(experienceItemId) is not StardewValley.Object harvest)
            {
                return HarvestExperienceProjection.Unavailable("unavailable_harvest_item_is_not_object");
            }

            var experience = (int)Math.Round(16d * Math.Log(0.018d * harvest.Price + 1d));
            return HarvestExperienceProjection.Exact(
                "farming",
                Farmer.farmingSkill,
                experience,
                crop.indexOfHarvest.Value == "421"
                    ? "native_player_sunflower_harvest_from_replaced_seed_item_base_price"
                    : "native_player_crop_harvest_from_harvest_item_base_price");
        }
        catch
        {
            return HarvestExperienceProjection.Unavailable("unavailable_harvest_item_resolution_failed");
        }
    }

    private static HarvestExperienceProjection ReadGiantCropExperience(ResourceClump clump, Farmer player)
    {
        if (clump is not GiantCrop)
        {
            return HarvestExperienceProjection.NotApplicable();
        }
        if (clump.GetType() != typeof(GiantCrop))
        {
            return HarvestExperienceProjection.Unavailable("unavailable_custom_giant_crop_runtime_type");
        }

        var permanentLuckLevel = player.GetUnmodifiedSkillLevel(Farmer.luckSkill);
        var experience = 50 * ((permanentLuckLevel + 1) / 2);
        return HarvestExperienceProjection.Exact(
            "luck",
            Farmer.luckSkill,
            experience,
            "native_giant_crop_destroyed_by_player_axe");
    }
}

internal sealed class HarvestExperienceProjection
{
    public string SkillId { get; init; } = string.Empty;

    public int? SkillIndex { get; init; }

    public int? Minimum { get; init; }

    public int? Maximum { get; init; }

    public string Condition { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public static HarvestExperienceProjection Exact(string skillId, int skillIndex, int value, string condition)
    {
        return new HarvestExperienceProjection
        {
            SkillId = skillId,
            SkillIndex = skillIndex,
            Minimum = value,
            Maximum = value,
            Condition = condition,
            Status = "exact_from_decompiled_native_harvest"
        };
    }

    public static HarvestExperienceProjection Unavailable(string status)
    {
        return new HarvestExperienceProjection { Status = status };
    }

    public static HarvestExperienceProjection NotApplicable()
    {
        return new HarvestExperienceProjection { Status = "not_applicable" };
    }
}
