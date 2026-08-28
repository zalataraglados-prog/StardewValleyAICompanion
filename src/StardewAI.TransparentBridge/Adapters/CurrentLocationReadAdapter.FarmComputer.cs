using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using System.Security.Cryptography;
using System.Text;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    internal const string FarmComputerNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(BC)239->CheckForActionOnFarmComputer->delay_500ms->ShowFarmComputerReport->Game1.multipleDialogues";

    private static object? ReadFarmComputerReport(
        GameLocation location,
        Vector2 tile,
        StardewObject item)
    {
        if (item.GetType() != typeof(StardewObject) ||
            !item.bigCraftable.Value ||
            !string.Equals(item.Name, "Farm Computer", StringComparison.Ordinal) ||
            !string.Equals(item.Type, "Crafting", StringComparison.Ordinal) ||
            !string.Equals(item.ItemId, "239", StringComparison.Ordinal) ||
            !string.Equals(item.QualifiedItemId, "(BC)239", StringComparison.Ordinal))
        {
            return null;
        }

        var rootLocation = location.GetRootLocation();
        var farm = rootLocation as Farm;
        var includesHay = rootLocation.IsBuildableLocation() || rootLocation.buildings.Any();
        var totalCrops = rootLocation.getTotalCrops();
        var totalOpenHoeDirt = rootLocation.getTotalOpenHoeDirt();
        var cropsReady = rootLocation.getTotalCropsReadyForHarvest();
        var unwateredCrops = rootLocation.getTotalUnwateredCrops();
        var greenhouseCropsReady = rootLocation.HasMinBuildings("Greenhouse", 1)
            ? rootLocation.getTotalGreenhouseCropsReadyForHarvest()
            : null;
        var totalForage = rootLocation.getTotalForageItems();
        var machinesReady = rootLocation.getNumberOfMachinesReadyForHarvest();
        var farmCaveReady = farm?.doesFarmCaveNeedHarvesting();
        var includesForage = farm is null || farm.SpawnsForage();
        var reportText = BuildFarmComputerReportText(
            rootLocation, farm, includesHay, totalCrops, totalOpenHoeDirt, cropsReady,
            unwateredCrops, greenhouseCropsReady, totalForage, includesForage,
            machinesReady, farmCaveReady);
        var reportSha256 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(reportText))).ToLowerInvariant();
        var stands = ReadSafeObjectInteractionStands(location, tile.ToPoint());

        return new
        {
            status = stands.Any(stand => stand.available) ? "ready" : "blocked_no_adjacent_stand",
            canonical_item_id = item.ItemId,
            canonical_qualified_item_id = item.QualifiedItemId,
            target_runtime_type = item.GetType().FullName,
            root_location_id = rootLocation.NameOrUniqueName,
            root_location_display_name = rootLocation.GetDisplayName(),
            root_location_runtime_type = rootLocation.GetType().FullName,
            includes_hay = includesHay,
            pieces_of_hay = includesHay ? rootLocation.piecesOfHay.Value : (int?)null,
            hay_capacity = includesHay ? rootLocation.GetHayCapacity() : (int?)null,
            total_crops = totalCrops,
            crops_ready_for_harvest = cropsReady,
            unwatered_crops = unwateredCrops,
            includes_greenhouse_line = greenhouseCropsReady.HasValue,
            greenhouse_crops_ready_for_harvest = greenhouseCropsReady,
            total_open_hoe_dirt = totalOpenHoeDirt,
            includes_forage_line = includesForage,
            total_forage_items = includesForage ? totalForage : (int?)null,
            machines_ready_for_harvest = machinesReady,
            includes_farm_cave_line = farmCaveReady.HasValue,
            farm_cave_needs_harvesting = farmCaveReady,
            report_text = reportText,
            report_sha256 = reportSha256,
            expected_delay_milliseconds = 500,
            expected_shake_timer_immediately_after_action = 500,
            expected_player_freeze_milliseconds = 500,
            expected_native_location_action_return = true,
            stand_tiles = stands,
            has_available_adjacent_stand = stands.Any(stand => stand.available),
            interaction_kind = "location_object",
            expected_action_type = "FarmComputer",
            native_contract = FarmComputerNativeContract
        };
    }

    private static string BuildFarmComputerReportText(
        GameLocation rootLocation,
        Farm? farm,
        bool includesHay,
        int totalCrops,
        int totalOpenHoeDirt,
        int cropsReady,
        int unwateredCrops,
        int? greenhouseCropsReady,
        int totalForage,
        bool includesForage,
        int machinesReady,
        bool? farmCaveReady)
    {
        var report = new StringBuilder();
        var displayName = rootLocation.GetDisplayName();
        if (rootLocation is Farm)
            report.Append(Game1.content.LoadString("Strings\\StringsFromCSFiles:FarmComputer_Intro_Farm", Game1.player.farmName.Value));
        else if (!string.IsNullOrWhiteSpace(displayName))
            report.Append(Game1.content.LoadString("Strings\\StringsFromCSFiles:FarmComputer_Intro_NamedLocation", displayName));
        else
            report.Append(Game1.content.LoadString("Strings\\StringsFromCSFiles:FarmComputer_Intro_Generic"));

        report.Append("^--------------^");
        if (includesHay)
            report.Append(Game1.content.LoadString("Strings\\StringsFromCSFiles:FarmComputer_PiecesHay", rootLocation.piecesOfHay, rootLocation.GetHayCapacity())).Append(" ^");
        report.Append(Game1.content.LoadString("Strings\\StringsFromCSFiles:FarmComputer_TotalCrops", totalCrops)).Append("  ^")
            .Append(Game1.content.LoadString("Strings\\StringsFromCSFiles:FarmComputer_CropsReadyForHarvest", cropsReady)).Append("  ^")
            .Append(Game1.content.LoadString("Strings\\StringsFromCSFiles:FarmComputer_CropsUnwatered", unwateredCrops)).Append("  ^");
        if (greenhouseCropsReady.HasValue)
            report.Append(Game1.content.LoadString("Strings\\StringsFromCSFiles:FarmComputer_CropsReadyForHarvest_Greenhouse", greenhouseCropsReady)).Append("  ^");
        report.Append(Game1.content.LoadString("Strings\\StringsFromCSFiles:FarmComputer_TotalOpenHoeDirt", totalOpenHoeDirt)).Append("  ^");
        if (includesForage)
            report.Append(Game1.content.LoadString("Strings\\StringsFromCSFiles:FarmComputer_TotalForage", totalForage)).Append("  ^");
        report.Append(Game1.content.LoadString("Strings\\StringsFromCSFiles:FarmComputer_MachinesReady", machinesReady)).Append("  ^");
        if (farmCaveReady.HasValue)
        {
            report.Append(Game1.content.LoadString(
                "Strings\\StringsFromCSFiles:FarmComputer_FarmCave",
                farmCaveReady.Value
                    ? Game1.content.LoadString("Strings\\Lexicon:QuestionDialogue_Yes")
                    : Game1.content.LoadString("Strings\\Lexicon:QuestionDialogue_No")));
        }
        return report.ToString();
    }
}
