using StardewValley;
using StardewValley.Characters;
using StardewValley.Network;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class WorldProgressReadAdapter
{
    private static object? ReadRaccoonRequest(NetWorldState? world)
    {
        if (world is null)
        {
            return null;
        }

        var cooldownDaysRemaining = Math.Max(
            0,
            7 - (world.Date.TotalDays - world.DaysPlayedWhenLastRaccoonBundleWasFinished));
        var requestSeason = world.SeasonOfCurrentRacconBundle;
        if (requestSeason < 0 || requestSeason > 3)
        {
            return new
            {
                projection_status = "unavailable_request_not_materialized_by_native_raccoon_interaction",
                times_fed = world.TimesFedRaccoons,
                request_season_index = requestSeason,
                cooldown_days_remaining = cooldownDaysRemaining,
                ingredients = Array.Empty<object>()
            };
        }

        var bundle = Raccoon.GetBundle(world.TimesFedRaccoons);
        return new
        {
            projection_status = "exact_native_Raccoon.GetBundle",
            times_fed = world.TimesFedRaccoons,
            request_season_index = requestSeason,
            cooldown_days_remaining = cooldownDaysRemaining,
            request_available = cooldownDaysRemaining == 0,
            ingredients = bundle.ingredients.Select((ingredient, index) => new
            {
                ingredient_index = index,
                item_id = ingredient.id ?? string.Empty,
                preserves_item_id = ingredient.preservesId ?? string.Empty,
                category = ingredient.category,
                required_stack = ingredient.stack,
                minimum_quality = ingredient.quality,
                completed = ingredient.completed
            }).ToArray()
        };
    }
}
