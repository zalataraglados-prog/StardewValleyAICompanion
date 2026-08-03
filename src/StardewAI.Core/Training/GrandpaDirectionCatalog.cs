using System;

namespace StardewAI.Core.Training
{
    public sealed class GrandpaDirectionCatalogEntry
    {
        public string DirectionId { get; set; } = string.Empty;

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
            CreateDirect("earn_money",
                "grandpa.direct.earn_money",
                new[] { "economy.sell_items", "economy.ship_items" },
                new[] { "sell_shop_item", "ship_inventory_item_to_bin" },
                "Cannot bind sell/ship candidates because required transparent sell/ship fields are unavailable."),
            CreateDirect("raise_friendships",
                "grandpa.direct.raise_friendships",
                new[] { "social.talk_npc", "social.gift_npc" },
                new[] { "social_talk_current", "social_gift_current" },
                "Cannot bind social talk/gift candidates because required transparent social fields are unavailable."),
            CreateDirect("complete_master_angler",
                "grandpa.direct.complete_master_angler",
                new[] { "fishing.catch_fish" },
                new[] { "catch_fish" },
                "Cannot bind catch_fish candidates because required transparent fishing fields are unavailable."),
            CreateDirect("complete_full_shipment",
                "grandpa.direct.complete_full_shipment",
                new[] { "economy.ship_items" },
                new[] { "ship_inventory_item_to_bin" },
                "Cannot bind full-shipment candidates because exact transparent contribution evidence is unavailable.",
                new[] { "world_progress.shipping_collection", "world_progress.full_shipment_progress" }),
            CreateDirect("obtain_skull_key",
                "grandpa.direct.obtain_skull_key",
                new[] { "mining.obtain_skull_key" },
                new[] { "mining_obtain_skull_key_plan_envelope" },
                "Cannot bind Skull Key candidates because the ordinary-mine floor-120 reward contract is incomplete.",
                new[] { "player.has_skull_key", "mining.current_mine", "mining.floor_objectives" }),
            CreateDirect("raise_skill_levels",
                "grandpa.direct.raise_skill_levels",
                new[]
                {
                    "farm.maintain_crops",
                    "farm.collect_machine_outputs",
                    "farm.process_machines",
                    "skills.read_books",
                    "farm.collect_animal_products",
                    "foraging.collect_spawned_objects",
                    "foraging.harvest_ginger",
                    "foraging.harvest_bushes",
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
                    "clear_obstacle_tile",
                    "clear_farm_resource_clump"
                },
                "Cannot bind skill-growth candidates because no current candidate has complete positive skill-experience evidence.",
                new[] { "player.level", "player.skills_detail", "event_candidates.skill_experience" }),
            CreateDirect("complete_museum_collection",
                "grandpa.direct.complete_museum_collection",
                new[] { "museum.donate_items" },
                new[] { "donate_museum_item" },
                "Cannot bind museum donation candidates because exact collection-progress evidence is unavailable.",
                new[] { "world_progress.museum", "player.inventory", "locations.collision_grid", "menus.active_menu" }),
            CreateDirect("obtain_rusty_key",
                "grandpa.direct.obtain_rusty_key",
                new[] { "museum.donate_items" },
                new[] { "donate_museum_item" },
                "Cannot bind museum donation candidates because exact progress toward the 60-donation Rusty Key threshold is unavailable.",
                new[] { "player.has_rusty_key", "world_progress.museum", "player.inventory", "locations.collision_grid", "menus.active_menu" }),
            CreateDirect("complete_community_center",
                "grandpa.direct.complete_community_center",
                new[] { "community_center.donate_bundle_items" },
                new[] { "donate_community_center_item" },
                "Cannot bind Community Center donation candidates because route or exact bundle evidence is unavailable.",
                new[] { "world_progress.community_center.route_state", "world_progress.community_center.bundle_rows", "player.inventory", "locations.collision_grid", "menus.active_menu" },
                true),
            CreateDirect("marriage_and_house_upgrade",
                "grandpa.partial.marriage_and_house_upgrade.house_axis",
                new[] { "housing.advance_farmhouse" },
                new[] { "purchase_farmhouse_upgrade" },
                "Cannot bind marriage_and_house_upgrade: the farmhouse axis has no ready native upgrade candidate, and the partnership completion chain remains fail-closed.",
                new[] { "player.married_or_roommate", "player.engaged", "player.spouse", "player.farmhouse_upgrade_level", "player.days_until_farmhouse_upgrade", "npcs.friendships", "world_progress.marriage_house" }),
            CreateDirect("earn_pet_love",
                "grandpa.direct.earn_pet_love",
                new[] { "farm.care_for_pets" },
                new[] { "pet_daily_interaction", "fill_pet_bowl" },
                "Cannot bind pet-care candidates because exact current or delayed friendship evidence is unavailable.",
                new[] { "quests.mail_received", "farm.pets", "farm.pet_bowls" })
        };

        private static GrandpaDirectionCatalogEntry CreateDirect(
            string directionId,
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

        private static GrandpaDirectionCatalogEntry CreateBlocked(
            string directionId,
            string bindingRuleId,
            string[] requiredTransparentFields,
            string[] requiredCapabilities,
            string blockReasonTemplate,
            bool ccJojaSensitive,
            string[]? coveredTransparentFields = null)
        {
            return new GrandpaDirectionCatalogEntry
            {
                DirectionId = directionId,
                BindingRuleId = bindingRuleId,
                DirectBindingEnabled = false,
                PermittedOptionIds = Array.Empty<string>(),
                PermittedCandidateKinds = Array.Empty<string>(),
                RequiredTransparentFields = requiredTransparentFields,
                CoveredTransparentFields = coveredTransparentFields ?? Array.Empty<string>(),
                RequiredCapabilities = requiredCapabilities,
                BlockReasonTemplate = blockReasonTemplate,
                CcJojaSensitive = ccJojaSensitive
            };
        }
    }
}
