using System;

namespace StardewAI.Core.Training
{
    public sealed class GrandpaDirectionCatalogEntry
    {
        public string DirectionId { get; set; } = string.Empty;

        public string Domain { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string FeedbackKey { get; set; } = string.Empty;

        public string[] CriterionIds { get; set; } = Array.Empty<string>();

        public string EffectiveGoalId { get; set; } = string.Empty;

        public string DemandFamily { get; set; } = string.Empty;

        public string BindingRuleId { get; set; } = string.Empty;

        public bool DirectBindingEnabled { get; set; }

        public string[] PermittedOptionIds { get; set; } = Array.Empty<string>();

        public string[] PermittedCandidateKinds { get; set; } = Array.Empty<string>();

        public string[] RequiredTransparentFields { get; set; } = Array.Empty<string>();

        public string[] CoveredTransparentFields { get; set; } = Array.Empty<string>();

        public string[] RequiredCapabilities { get; set; } = Array.Empty<string>();

        public string BlockReasonTemplate { get; set; } = string.Empty;

        public bool CcJojaSensitive { get; set; }
    }

    public sealed class GrandpaDirectionCatalog
    {
        public static readonly GrandpaDirectionCatalogEntry[] Entries = new[]
        {
            CreateDirect(
                "earn_money",
                "economy",
                "Increase total money earned",
                "grandpa.money",
                new[] { "money_50000", "money_100000", "money_200000", "money_300000", "money_500000", "money_1000000" },
                "goal.economy.earn_money",
                "economy",
                "grandpa.direct.earn_money",
                new[] { "economy.sell_items", "economy.ship_items", "farm.establish_supported_machine_capacity" },
                new[] { "sell_shop_item", "ship_inventory_item_to_bin", "craft_machine_item", "place_machine_item", "load_machine_input_tile" },
                "Cannot bind sell/ship candidates because required transparent sell/ship fields are unavailable."),
            CreateDirect(
                "complete_museum_collection",
                "world_progress",
                "Complete museum collection achievement",
                "grandpa.achievement.5",
                new[] { "achievement_complete_collection" },
                "goal.world_progress.complete_museum_collection",
                "world_progress",
                "grandpa.direct.complete_museum_collection",
                new[] { "museum.donate_items" },
                new[] { "donate_museum_item" },
                "Cannot bind museum donation candidates because exact collection-progress evidence is unavailable.",
                new[] { "world_progress.museum", "player.inventory", "locations.collision_grid", "menus.active_menu" }),
            CreateDirect(
                "obtain_skull_key",
                "exploration",
                "Obtain Skull Key",
                "grandpa.skull_key",
                new[] { "skull_key" },
                "goal.combat_progress.obtain_skull_key",
                "combat_progress",
                "grandpa.direct.obtain_skull_key",
                new[] { "mining.obtain_skull_key" },
                new[] { "mining_obtain_skull_key_plan_envelope" },
                "Cannot bind Skull Key candidates because the ordinary-mine floor-120 reward contract is incomplete.",
                new[] { "player.has_skull_key", "mining.current_mine", "mining.floor_objectives" }),
            CreateDirect(
                "complete_community_center",
                "world_progress",
                "Complete Community Center",
                "grandpa.community_center",
                new[] { "community_center_access_or_completion", "community_center_accessible_bonus" },
                "goal.world_progress.complete_community_center",
                "world_progress",
                "grandpa.direct.complete_community_center",
                new[] { "community_center.donate_bundle_items" },
                new[] { "donate_community_center_item" },
                "Cannot bind Community Center donation candidates because route or exact bundle evidence is unavailable.",
                new[] { "world_progress.community_center.route_state", "world_progress.community_center.bundle_rows", "player.inventory", "locations.collision_grid", "menus.active_menu" },
                true),
            CreateDirect(
                "marriage_and_house_upgrade",
                "social",
                "Marry or get roommate and upgrade farmhouse",
                "grandpa.marriage_house",
                new[] { "married_or_roommate_house_2" },
                "goal.social.marriage_and_house_upgrade",
                "social",
                "grandpa.direct.marriage_and_house_upgrade",
                new[] { "housing.advance_farmhouse", "social.advance_partnership" },
                new[] { "purchase_farmhouse_upgrade", "partnership_bouquet_current", "partnership_propose_marriage_current", "partnership_propose_roommate_current" },
                "Cannot bind marriage_and_house_upgrade: no ready native farmhouse or partnership transition candidate exists; an already scheduled wedding settles only through the normal cross-day lifecycle.",
                new[] { "player.married_or_roommate", "player.engaged", "player.spouse", "player.farmhouse_upgrade_level", "player.days_until_farmhouse_upgrade", "npcs.friendships", "world_progress.marriage_house" }),
            CreateDirect(
                "obtain_rusty_key",
                "world_progress",
                "Obtain Rusty Key",
                "grandpa.rusty_key",
                new[] { "rusty_key" },
                "goal.world_progress.obtain_rusty_key",
                "world_progress",
                "grandpa.direct.obtain_rusty_key",
                new[] { "museum.donate_items" },
                new[] { "donate_museum_item" },
                "Cannot bind museum donation candidates because exact progress toward the 60-donation Rusty Key threshold is unavailable.",
                new[] { "player.has_rusty_key", "world_progress.museum", "player.inventory", "locations.collision_grid", "menus.active_menu" }),
            CreateDirect(
                "complete_master_angler",
                "world_progress",
                "Complete Master Angler achievement",
                "grandpa.achievement.26",
                new[] { "achievement_master_angler" },
                "goal.fishing.complete_master_angler",
                "fishing",
                "grandpa.direct.complete_master_angler",
                new[] { "fishing.catch_fish", "fishing.collect_crab_pots" },
                new[] { "route_connector_tile", "clear_obstacle_tile", "catch_fish", "collect_crab_pot", "load_crab_pot_bait", "place_crab_pot" },
                "Cannot bind a fishing acquisition or crab-pot lifecycle candidate because no exact catch or complete production domain advances the native missing-fish denominator.",
                new[] { "world_progress.fish_collection_progress", "fishing.rod_contexts", "fishing.fishable_tiles", "player.crab_pot_placement", "player.crab_pot_network", "current_location.objects" }),
            CreateDirect(
                "complete_full_shipment",
                "economy",
                "Complete Full Shipment achievement",
                "grandpa.achievement.34",
                new[] { "achievement_full_shipment" },
                "goal.economy.complete_full_shipment",
                "economy",
                "grandpa.direct.complete_full_shipment",
                new[] { "economy.ship_items" },
                new[] { "ship_inventory_item_to_bin" },
                "Cannot bind full-shipment candidates because exact transparent contribution evidence is unavailable.",
                new[] { "world_progress.shipping_collection", "world_progress.full_shipment_progress" }),
            CreateDirect(
                "raise_friendships",
                "social",
                "Raise NPC friendships",
                "grandpa.friendships",
                new[] { "friendships_5", "friendships_10" },
                "goal.social.raise_friendships",
                "social",
                "grandpa.direct.raise_friendships",
                new[] { "social.talk_npc", "social.gift_npc" },
                new[] { "social_talk_current", "social_gift_current", "route_connector_tile" },
                "Cannot bind social talk/gift candidates because required transparent social fields are unavailable."),
            CreateDirect(
                "raise_skill_levels",
                "skills",
                "Raise total skill level",
                "grandpa.level",
                new[] { "player_level_15", "player_level_25" },
                "goal.skills.raise_skill_levels",
                "skills",
                "grandpa.direct.raise_skill_levels",
                new[]
                {
                    "farm.maintain_crops",
                    "farm.collect_machine_outputs",
                    "farm.load_supported_machine_input",
                    "farm.process_machines",
                    "skills.read_books",
                    "farm.collect_animal_products",
                    "foraging.collect_spawned_objects",
                    "foraging.harvest_spring_onions",
                    "foraging.harvest_ginger",
                    "foraging.harvest_bushes",
                    "foraging.chop_wild_tree",
                    "foraging.harvest_tree_moss",
                    "foraging.clear_green_rain_bushes",
                    "foraging.pan_ore_spot",
                    "fishing.catch_fish",
                    "fishing.collect_crab_pots",
                    "fishing.service_fish_ponds",
                    "mining.reach_depth",
                    "executor.clear_obstacle",
                    "executor.break_farm_resource_clump",
                    "executor.break_current_location_resource_clump"
                },
                new[]
                {
                    "harvest_crop_tile",
                    "harvest_giant_crop_tile",
                    "collect_machine_output_tile",
                    "read_inventory_book",
                    "collect_animal_product",
                    "collect_spawned_object",
                    "harvest_ginger",
                    "harvest_bush",
                    "clear_green_rain_resource_clump",
                    "pan_ore_spot",
                    "catch_fish",
                    "collect_crab_pot",
                    "collect_fish_pond_output",
                    "complete_fish_pond_request",
                    "mining_reach_depth_plan_envelope",
                    "mining_combat_training_plan_envelope",
                    "clear_obstacle_tile",
                    "clear_farm_resource_clump"
                },
                "Cannot bind skill-growth candidates because no current candidate has complete positive skill-experience evidence.",
                new[] { "player.level", "player.skills_detail", "event_candidates.skill_experience" }),
            CreateDirect(
                "earn_pet_love",
                "farm",
                "Earn pet love",
                "grandpa.pet_love",
                new[] { "pet_love" },
                "goal.farm.earn_pet_love",
                "farm",
                "grandpa.direct.earn_pet_love",
                new[] { "farm.care_for_pets" },
                new[] { "pet_daily_interaction", "fill_pet_bowl" },
                "Cannot bind pet-care candidates because exact current or delayed friendship evidence is unavailable.",
                new[] { "quests.mail_received", "farm.pets", "farm.pet_bowls" })
        };

        private static GrandpaDirectionCatalogEntry CreateDirect(
            string directionId,
            string domain,
            string label,
            string feedbackKey,
            string[] criterionIds,
            string effectiveGoalId,
            string demandFamily,
            string bindingRuleId,
            string[] permittedOptionIds,
            string[] permittedCandidateKinds,
            string blockReasonTemplate,
            string[]? coveredTransparentFields = null,
            bool ccJojaSensitive = false)
        {
            return new GrandpaDirectionCatalogEntry
            {
                DirectionId = directionId,
                Domain = domain,
                Label = label,
                FeedbackKey = feedbackKey,
                CriterionIds = criterionIds,
                EffectiveGoalId = effectiveGoalId,
                DemandFamily = demandFamily,
                BindingRuleId = bindingRuleId,
                DirectBindingEnabled = true,
                PermittedOptionIds = permittedOptionIds,
                PermittedCandidateKinds = permittedCandidateKinds,
                RequiredTransparentFields = Array.Empty<string>(),
                CoveredTransparentFields = coveredTransparentFields ?? Array.Empty<string>(),
                RequiredCapabilities = Array.Empty<string>(),
                BlockReasonTemplate = blockReasonTemplate,
                CcJojaSensitive = ccJojaSensitive
            };
        }
    }
}
