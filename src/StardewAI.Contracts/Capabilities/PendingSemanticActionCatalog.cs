using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StardewAI.Contracts.Capabilities;

public sealed class PendingSemanticActionDeclaration
{
    public string ActionId { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string SemanticKind { get; set; } = string.Empty;
    public string PrimaryEngineId { get; set; } = string.Empty;
    public string CatalogStatus { get; set; } = "catalogued_blocked";
    public string BlockReason { get; set; } = "option_spec_not_declared";
    public string[] NativeRuntimeTypes { get; set; } = Array.Empty<string>();
}

public static class PendingSemanticActionCatalog
{
    private static readonly IReadOnlyList<PendingSemanticActionDeclaration> Rows =
        new ReadOnlyCollection<PendingSemanticActionDeclaration>(new[]
        {
            C("animals.manage_animal", "animals", "composite", "engine.animal_management", "AnimalQueryMenu"),
            C("buildings.change_skin", "buildings", "composite", "engine.building_management", "BuildingSkinMenu"),
            C("buildings.construct", "buildings", "composite", "engine.building_management", "CarpenterMenu"),
            C("buildings.paint", "buildings", "composite", "engine.building_management", "BuildingPaintMenu"),
            C("crafting.cook_recipe", "crafting", "composite", "engine.crafting_processing", "GameLocation"),
            C("crafting.forge_item", "crafting", "composite", "engine.crafting_processing", "ForgeMenu"),
            P("executor.apply_tree_treatment", "foraging", "engine.tool_harvest", "Object"),
            P("executor.place_cookout_kit", "crafting", "engine.placement_layout", "Object"),
            P("executor.place_crab_pot", "fishing", "engine.placement_layout", "Object"),
            P("executor.place_fence", "farming", "engine.placement_layout", "Object"),
            P("executor.place_flooring", "farming", "engine.placement_layout", "Object"),
            P("executor.place_furniture", "housing", "engine.placement_layout", "Object"),
            P("executor.place_sign", "farming", "engine.placement_layout", "Object"),
            P("executor.place_tent", "recovery", "engine.placement_layout", "Object"),
            P("executor.plant_grass", "farming", "engine.placement_layout", "Object"),
            P("executor.toggle_lantern", "tool", "engine.tool_harvest", "Lantern"),
            P("executor.read_secret_note", "inventory", "engine.interaction_menu", "Object"),
            P("executor.use_firework", "inventory", "engine.inventory_transfer", "Object"),
            P("executor.use_horse_flute", "movement", "engine.movement_navigation", "Object"),
            P("executor.use_monster_musk", "combat", "engine.inventory_transfer", "Object"),
            P("executor.use_rain_totem", "farming", "engine.inventory_transfer", "Object"),
            P("executor.use_raft", "movement", "engine.movement_navigation", "Raft"),
            P("executor.use_return_scepter", "movement", "engine.movement_navigation", "Wand"),
            P("executor.use_treasure_totem", "foraging", "engine.tool_harvest", "Object"),
            P("executor.use_warp_totem", "movement", "engine.movement_navigation", "Object"),
            C("festival.manage_grange_display", "festival", "composite", "engine.inventory_transfer", "Event"),
            C("festival.play_fishing_game", "festival", "composite", "engine.minigame", "FishingGame"),
            C("festival.play_slingshot_game", "festival", "composite", "engine.minigame", "TargetGame"),
            C("festival.play_strength_game", "festival", "composite", "engine.minigame", "StrengthGame"),
            C("festival.spin_wheel", "festival", "composite", "engine.minigame", "WheelSpinGame"),
            C("fishing.manage_fish_pond", "fishing", "composite", "engine.farm_machine", "PondQueryMenu"),
            C("foraging.harvest_fruit_tree", "foraging", "composite", "engine.tool_harvest", "FruitTree"),
            C("foraging.harvest_tree_product", "foraging", "composite", "engine.tool_harvest", "Tree"),
            C("foraging.rummage_garbage", "foraging", "composite", "engine.interaction_menu", "GameLocation"),
            C("housing.renovate", "housing", "composite", "engine.building_management", "RenovateMenu"),
            C("island.field_office_donate", "island", "composite", "engine.interaction_menu", "FieldOfficeMenu"),
            C("minigame.play_calico_jack", "minigame", "composite", "engine.minigame", "CalicoJack"),
            C("minigame.play_crane_game", "minigame", "composite", "engine.minigame", "CraneGame"),
            C("minigame.play_darts", "minigame", "composite", "engine.minigame", "Darts"),
            C("minigame.play_junimo_kart", "minigame", "composite", "engine.minigame", "MineCart"),
            C("minigame.play_prairie_king", "minigame", "composite", "engine.minigame", "AbigailGame"),
            C("minigame.play_slots", "minigame", "composite", "engine.minigame", "Slots"),
            C("mining.activate_calico_statue", "mining", "composite", "engine.interaction_menu", "MineShaft"),
            C("multiplayer.manage_wallet", "multiplayer", "composite", "engine.interaction_menu", "ManorHouse"),
            C("multiplayer.send_chat", "multiplayer", "composite", "engine.interaction_menu", "ChatBox"),
            C("player.choose_bobber", "player", "composite", "engine.interaction_menu", "GameLocation"),
            C("player.choose_jukebox_track", "player", "composite", "engine.interaction_menu", "GameLocation"),
            C("player.customize", "player", "composite", "engine.interaction_menu", "CharacterCustomization"),
            C("processing.crack_geode", "processing", "composite", "engine.crafting_processing", "GeodeMenu"),
            C("quest.cancel", "quest", "composite", "engine.interaction_menu", "QuestLog"),
            C("recovery.sleep_in_tent", "recovery", "composite", "engine.recovery_timing", "Tent"),
            C("rewards.claim_adventure_guild_reward", "rewards", "composite", "engine.interaction_menu", "AdventureGuild"),
            C("rewards.claim_prize_ticket", "rewards", "composite", "engine.interaction_menu", "PrizeTicketMenu"),
            C("skills.claim_mastery", "skills", "composite", "engine.interaction_menu", "MasteryTrackerMenu"),
            C("social.emote", "social", "composite", "engine.interaction_menu", "EmoteMenu", "EmoteSelector"),
            C("social.watch_movie", "social", "composite", "engine.interaction_menu", "MovieTheater"),
            C("story.advance_event", "story", "composite", "engine.interaction_menu", "Event", "GameLocation"),
            C("story.advance_event_minigame", "story", "composite", "engine.minigame",
                "BoatJourney", "FantasyBoardGame", "GrandpaStory", "HaleyCowPictures", "Intro",
                "MaruComet", "PlaneFlyBy", "RobotBlastoff", "TelescopeScene"),
            C("tailoring.dye_item", "tailoring", "composite", "engine.crafting_processing", "DyeMenu"),
            C("tailoring.sew_item", "tailoring", "composite", "engine.crafting_processing", "TailoringMenu")
        });

    private static readonly IReadOnlyDictionary<string, PendingSemanticActionDeclaration> ById =
        new ReadOnlyDictionary<string, PendingSemanticActionDeclaration>(
            Rows.ToDictionary(row => row.ActionId, StringComparer.Ordinal));

    static PendingSemanticActionCatalog()
    {
        var overlap = Rows
            .Where(row => OptionCapabilityRegistrySource.TryGet(row.ActionId, out _))
            .Select(row => row.ActionId)
            .ToArray();
        if (overlap.Length > 0)
            throw new InvalidOperationException(
                "Pending semantic actions overlap registered OptionSpecs: " +
                string.Join(",", overlap));
    }

    public static IReadOnlyList<PendingSemanticActionDeclaration> All => Rows;

    public static bool TryGet(string actionId, out PendingSemanticActionDeclaration declaration) =>
        ById.TryGetValue(actionId, out declaration!);

    private static PendingSemanticActionDeclaration C(
        string id,
        string domain,
        string kind,
        string engine,
        params string[] runtimeTypes) =>
        Create(id, domain, kind, engine, runtimeTypes);

    private static PendingSemanticActionDeclaration P(
        string id,
        string domain,
        string engine,
        params string[] runtimeTypes) =>
        Create(id, domain, "primitive", engine, runtimeTypes);

    private static PendingSemanticActionDeclaration Create(
        string id,
        string domain,
        string kind,
        string engine,
        string[] runtimeTypes)
    {
        if (string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrWhiteSpace(domain) ||
            string.IsNullOrWhiteSpace(engine) ||
            runtimeTypes.Length == 0)
        {
            throw new InvalidOperationException("Pending semantic action declarations must be complete.");
        }

        return new PendingSemanticActionDeclaration
        {
            ActionId = id,
            Domain = domain,
            SemanticKind = kind,
            PrimaryEngineId = engine,
            NativeRuntimeTypes = runtimeTypes
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray()
        };
    }
}
