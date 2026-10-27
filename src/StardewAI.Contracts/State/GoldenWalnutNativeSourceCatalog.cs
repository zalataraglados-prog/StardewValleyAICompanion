using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewAI.Contracts.State;

public sealed class GoldenWalnutNativeSourceDefinition
{
    public GoldenWalnutNativeSourceDefinition(
        string sourceKey,
        string trackingKind,
        int nativeQuantity,
        string locationId,
        int? tileX,
        int? tileY,
        string acquisitionOptionId,
        string accessPhase,
        string requiredMailKey = "",
        bool resortOnly = false,
        int optionalUnlockWalnutCost = 0,
        string probabilityStatus = "exact_native_one_time",
        int? requiredSecretNoteId = null)
    {
        SourceKey = sourceKey;
        TrackingKind = trackingKind;
        NativeQuantity = nativeQuantity;
        LocationId = locationId;
        TileX = tileX;
        TileY = tileY;
        AcquisitionOptionId = acquisitionOptionId;
        AccessPhase = accessPhase;
        RequiredMailKey = requiredMailKey;
        ResortOnly = resortOnly;
        OptionalUnlockWalnutCost = optionalUnlockWalnutCost;
        ProbabilityStatus = probabilityStatus;
        RequiredSecretNoteId = requiredSecretNoteId;
    }

    public string SourceKey { get; }
    public string TrackingKind { get; }
    public int NativeQuantity { get; }
    public string LocationId { get; }
    public int? TileX { get; }
    public int? TileY { get; }
    public string AcquisitionOptionId { get; }
    public string AccessPhase { get; }
    public string RequiredMailKey { get; }
    public bool ResortOnly { get; }
    public int OptionalUnlockWalnutCost { get; }
    public string ProbabilityStatus { get; }
    public int? RequiredSecretNoteId { get; }
}

public static class GoldenWalnutNativeSourceCatalog
{
    public const string SchemaVersion = "golden_walnut_sources.v1";
    public const string ProjectionStatus = "complete_locked_base_1.6.15_island_hut_denominator";
    public const int NativeSourceKeyCount = 69;
    public const int NativeWalnutTotal = 130;

    public static IReadOnlyList<GoldenWalnutNativeSourceDefinition> Sources { get; } = BuildSources();

    private static GoldenWalnutNativeSourceDefinition[] BuildSources()
    {
        var rows = new List<GoldenWalnutNativeSourceDefinition>();

        AddCoordinateSources(rows, "tracker", "foraging.harvest_bushes", "north_after_first_parrot", "Island_FirstParrot",
            "Bush_IslandNorth_13_33", "Bush_IslandNorth_5_30", "Bush_IslandNorth_4_42",
            "Bush_IslandNorth_45_38", "Bush_IslandNorth_47_40", "Bush_IslandNorth_20_26",
            "Bush_IslandNorth_9_84", "Bush_IslandNorth_56_27");
        AddCoordinateSources(rows, "tracker", "executor.till_soil", "north_after_first_parrot", "Island_FirstParrot",
            "Buried_IslandNorth_19_13", "Buried_IslandNorth_57_79", "Buried_IslandNorth_54_21",
            "Buried_IslandNorth_42_77", "Buried_IslandNorth_62_54", "Buried_IslandNorth_26_81");
        AddCoordinateSources(rows, "tracker", "foraging.harvest_bushes", "island_south_initial", "",
            "Bush_IslandSouth_31_5");
        rows.Add(Coordinate("Buried_IslandNorth_19_39", "tracker", 1, "executor.till_soil",
            "field_office_after_optional_bridge", "Island_UpgradeBridge", optionalUnlockWalnutCost: 10));

        rows.Add(Source("IslandLeftPlantRestored", "tracker", 1, "IslandFieldOffice", "island.field_office_survey",
            "field_office_after_optional_bridge", "Island_UpgradeBridge", optionalUnlockWalnutCost: 10));
        rows.Add(Source("IslandRightPlantRestored", "tracker", 1, "IslandFieldOffice", "island.field_office_survey",
            "field_office_after_optional_bridge", "Island_UpgradeBridge", optionalUnlockWalnutCost: 10));
        rows.Add(Source("IslandBatRestored", "tracker", 1, "IslandFieldOffice", "island.field_office_donate",
            "field_office_after_optional_bridge", "Island_UpgradeBridge", optionalUnlockWalnutCost: 10));
        rows.Add(Source("IslandFrogRestored", "tracker", 1, "IslandFieldOffice", "island.field_office_donate",
            "field_office_after_optional_bridge", "Island_UpgradeBridge", optionalUnlockWalnutCost: 10));
        rows.Add(Source("IslandCenterSkeletonRestored", "tracker", 6, "IslandFieldOffice", "island.field_office_donate",
            "field_office_after_optional_bridge", "Island_UpgradeBridge", optionalUnlockWalnutCost: 10));
        rows.Add(Source("IslandSnakeRestored", "tracker", 3, "IslandFieldOffice", "island.field_office_donate",
            "field_office_after_optional_bridge", "Island_UpgradeBridge", optionalUnlockWalnutCost: 10));

        rows.Add(Source("TreeNut", "tracker", 1, "IslandHut", "island.collect_hut_tree_nut",
            "north_after_first_parrot", "Island_FirstParrot"));
        rows.Add(Source("IslandWestCavePuzzle", "tracker", 3, "IslandWestCave1", "minigame.play_simon_says",
            "west_after_turtle", "Island_Turtle"));
        rows.Add(Source("SandDuggy", "tracker", 1, "IslandWest", "island.defeat_sand_duggy",
            "west_after_turtle", "Island_Turtle"));
        rows.Add(Limited("TigerSlimeNut", 1, "IslandWest", "executor.combat_monster",
            "west_after_turtle", "Island_Turtle", "bounded_native_drop_one"));

        AddCoordinateSources(rows, "tracker", "executor.till_soil", "west_after_turtle", "Island_Turtle",
            "Buried_IslandWest_21_81", "Buried_IslandWest_62_76", "Buried_IslandWest_39_24",
            "Buried_IslandWest_88_14", "Buried_IslandWest_43_74", "Buried_IslandWest_30_75");
        AddCoordinateSources(rows, "tracker", "foraging.harvest_bushes", "west_after_turtle", "Island_Turtle",
            "Bush_IslandWest_104_3", "Bush_IslandWest_31_24", "Bush_IslandWest_38_56",
            "Bush_IslandWest_75_29", "Bush_IslandWest_64_30", "Bush_IslandWest_54_18",
            "Bush_IslandWest_25_30", "Bush_IslandWest_15_3");
        rows.Add(Limited("MusselStone", 5, "IslandWest", "executor.clear_obstacle",
            "west_after_turtle", "Island_Turtle", "bounded_native_rng_0.10_per_mussel_node"));
        rows.Add(Limited("IslandFarming", 5, "IslandWest", "farm.maintain_crops",
            "west_after_turtle", "Island_Turtle", "bounded_native_rng_0.05_per_island_crop_harvest"));
        rows.Add(Limited("IslandFishing", 5, "IslandSouth", "fishing.catch_fish",
            "island_south_initial", "", "bounded_native_rng_0.15_per_successful_catch"));

        rows.Add(Limited("VolcanoNormalChest", 1, "VolcanoDungeon0", "island.open_volcano_reward_chest",
            "volcano_after_first_parrot", "Island_FirstParrot", "bounded_native_volcano_chest_pool"));
        rows.Add(Limited("VolcanoRareChest", 1, "VolcanoDungeon0", "island.open_volcano_reward_chest",
            "volcano_after_first_parrot", "Island_FirstParrot", "bounded_native_volcano_chest_pool"));
        rows.Add(Limited("VolcanoBarrel", 5, "VolcanoDungeon0", "executor.break_volcano_container",
            "volcano_after_first_parrot", "Island_FirstParrot", "bounded_native_volcano_container_rng"));
        rows.Add(Limited("VolcanoMining", 5, "VolcanoDungeon0", "executor.break_volcano_stone",
            "volcano_after_first_parrot", "Island_FirstParrot", "bounded_native_volcano_mining_rng"));
        rows.Add(Limited("VolcanoMonsterDrop", 5, "VolcanoDungeon0", "executor.combat_volcano_monster",
            "volcano_after_first_parrot", "Island_FirstParrot", "bounded_native_volcano_monster_rng"));

        rows.Add(Source("Island_N_BuriedTreasureNut", "limited_drop", 1, "IslandNorth", "executor.till_soil",
            "field_office_after_optional_bridge", "Island_UpgradeBridge", optionalUnlockWalnutCost: 10,
            probabilityStatus: "exact_native_journal_tile", tileX: 27, tileY: 28, requiredSecretNoteId: 1010));
        rows.Add(Source("Island_W_BuriedTreasureNut", "limited_drop", 1, "IslandWest", "executor.till_soil",
            "west_after_turtle", "Island_Turtle", probabilityStatus: "exact_native_journal_tile",
            tileX: 18, tileY: 42, requiredSecretNoteId: 1004));
        rows.Add(Source("Island_W_BuriedTreasureNut2", "limited_drop", 1, "IslandWest", "executor.till_soil",
            "west_after_turtle", "Island_Turtle", probabilityStatus: "exact_native_journal_tile",
            tileX: 104, tileY: 74, requiredSecretNoteId: 1006));
        rows.Add(Source("Mermaid", "tracker", 5, "IslandSouthEast", "island.complete_mermaid_puzzle",
            "resort_only", "Island_Resort", resortOnly: true));
        rows.Add(Source("TreeNutShot", "tracker", 1, "IslandNorth", "island.shoot_tree_nut",
            "north_after_first_parrot", "Island_FirstParrot"));

        AddCoordinateSources(rows, "tracker", "executor.till_soil", "resort_only", "Island_Resort", true,
            "Buried_IslandSouthEastCave_36_26", "Buried_IslandSouthEast_25_17");
        rows.Add(Source("StardropPool", "tracker", 1, "IslandSouthEast", "fishing.catch_fish",
            "resort_only", "Island_Resort", resortOnly: true));
        AddCoordinateSources(rows, "tracker", "foraging.harvest_bushes", "caldera_after_volcano", "Island_FirstParrot",
            "Bush_Caldera_28_36", "Bush_Caldera_9_34");
        AddCoordinateSources(rows, "tracker", "foraging.harvest_bushes", "boat_repaired", "willyBoatFixed",
            "Bush_CaptainRoom_2_4");
        rows.Add(Source("BananaShrine", "tracker", 3, "IslandEast", "island.complete_banana_shrine",
            "east_after_first_parrot", "Island_FirstParrot"));
        AddCoordinateSources(rows, "tracker", "foraging.harvest_bushes", "east_after_first_parrot", "Island_FirstParrot",
            "Bush_IslandEast_17_37");
        rows.Add(Limited("Darts", 3, "IslandSouthEastCave", "minigame.play_darts",
            "resort_only", "Island_Resort", "bounded_native_three_reward_stages", resortOnly: true));
        rows.Add(Source("IslandGourmand1", "tracker", 5, "IslandFarmCave", "island.complete_gourmand_request",
            "house_after_upgrade", "Island_UpgradeHouse"));
        rows.Add(Source("IslandGourmand2", "tracker", 5, "IslandFarmCave", "island.complete_gourmand_request",
            "house_after_upgrade", "Island_UpgradeHouse"));
        rows.Add(Source("IslandGourmand3", "tracker", 5, "IslandFarmCave", "island.complete_gourmand_request",
            "house_after_upgrade", "Island_UpgradeHouse"));
        rows.Add(Source("IslandShrinePuzzle", "tracker", 5, "IslandShrine", "island.complete_gem_bird_shrine",
            "east_after_first_parrot", "Island_FirstParrot"));
        AddCoordinateSources(rows, "tracker", "foraging.harvest_bushes", "east_after_first_parrot", "Island_FirstParrot",
            "Bush_IslandShrine_23_34");

        rows.Add(Source("GoldenCoconutCracked", "world_flag", 1, "Blacksmith", "processing.crack_geode",
            "island_south_initial", ""));
        rows.Add(Source("gotBirdieReward", "master_mail", 5, "IslandWest", "quest.complete_birdie",
            "west_after_turtle", "Island_Turtle"));

        var result = rows
            .OrderBy(row => row.SourceKey, StringComparer.Ordinal)
            .ToArray();
        if (result.Length != NativeSourceKeyCount ||
            result.Sum(row => row.NativeQuantity) != NativeWalnutTotal ||
            result.Select(row => row.SourceKey).Distinct(StringComparer.Ordinal).Count() != result.Length)
        {
            throw new InvalidOperationException("Golden Walnut native source catalog denominator drifted.");
        }
        return result;
    }

    private static void AddCoordinateSources(
        ICollection<GoldenWalnutNativeSourceDefinition> rows,
        string trackingKind,
        string acquisitionOptionId,
        string accessPhase,
        string requiredMailKey,
        params string[] keys) =>
        AddCoordinateSources(rows, trackingKind, acquisitionOptionId, accessPhase, requiredMailKey, false, keys);

    private static void AddCoordinateSources(
        ICollection<GoldenWalnutNativeSourceDefinition> rows,
        string trackingKind,
        string acquisitionOptionId,
        string accessPhase,
        string requiredMailKey,
        bool resortOnly,
        params string[] keys)
    {
        foreach (var key in keys)
            rows.Add(Coordinate(key, trackingKind, 1, acquisitionOptionId, accessPhase, requiredMailKey, resortOnly));
    }

    private static GoldenWalnutNativeSourceDefinition Coordinate(
        string key,
        string trackingKind,
        int quantity,
        string acquisitionOptionId,
        string accessPhase,
        string requiredMailKey,
        bool resortOnly = false,
        int optionalUnlockWalnutCost = 0)
    {
        var parts = key.Split('_');
        if (parts.Length < 4 ||
            !int.TryParse(parts[^2], out var tileX) ||
            !int.TryParse(parts[^1], out var tileY))
        {
            throw new InvalidOperationException("Invalid coordinate Golden Walnut source key: " + key);
        }
        return Source(
            key,
            trackingKind,
            quantity,
            string.Join("_", parts.Skip(1).Take(parts.Length - 3)),
            acquisitionOptionId,
            accessPhase,
            requiredMailKey,
            resortOnly,
            optionalUnlockWalnutCost,
            "exact_native_one_time",
            tileX,
            tileY);
    }

    private static GoldenWalnutNativeSourceDefinition Limited(
        string key,
        int quantity,
        string locationId,
        string acquisitionOptionId,
        string accessPhase,
        string requiredMailKey,
        string probabilityStatus,
        bool resortOnly = false,
        int optionalUnlockWalnutCost = 0) =>
        Source(key, "limited_drop", quantity, locationId, acquisitionOptionId, accessPhase,
            requiredMailKey, resortOnly, optionalUnlockWalnutCost, probabilityStatus);

    private static GoldenWalnutNativeSourceDefinition Source(
        string key,
        string trackingKind,
        int quantity,
        string locationId,
        string acquisitionOptionId,
        string accessPhase,
        string requiredMailKey,
        bool resortOnly = false,
        int optionalUnlockWalnutCost = 0,
        string probabilityStatus = "exact_native_one_time",
        int? tileX = null,
        int? tileY = null,
        int? requiredSecretNoteId = null) =>
        new(
            key,
            trackingKind,
            quantity,
            locationId,
            tileX,
            tileY,
            acquisitionOptionId,
            accessPhase,
            requiredMailKey,
            resortOnly,
            optionalUnlockWalnutCost,
            probabilityStatus,
            requiredSecretNoteId);
}
