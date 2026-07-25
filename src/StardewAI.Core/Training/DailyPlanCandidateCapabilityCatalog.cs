using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace StardewAI.Core.Training
{
    public sealed class DailyPlanCandidateCapability
    {
        public DailyPlanCandidateCapability(string kind, bool compilable, string blockReason = "")
        {
            Kind = kind;
            Compilable = compilable;
            BlockReason = blockReason;
        }

        public string Kind { get; }
        public bool Compilable { get; }
        public string BlockReason { get; }
    }

    public static class DailyPlanCandidateCapabilityCatalog
    {
        private static readonly IReadOnlyList<DailyPlanCandidateCapability> Catalog =
            new ReadOnlyCollection<DailyPlanCandidateCapability>(new[]
            {
                Supported("buy_shop_item"),
                Supported("catch_fish"),
                Supported("claim_mine_reward_chest"),
                Supported("clear_farm_resource_clump"),
                Supported("clear_green_rain_resource_clump"),
                Supported("clear_obstacle_tile"),
                Supported("collect_animal_product"),
                Supported("collect_crab_pot"),
                Supported("collect_fish_pond_output"),
                Supported("collect_machine_output_tile"),
                Supported("collect_spawned_object"),
                Supported("complete_fish_pond_request"),
                Supported("craft_machine_item"),
                Supported("donate_community_center_item"),
                Supported("donate_museum_item"),
                Supported("fill_pet_bowl"),
                Supported("harvest_bush"),
                Supported("harvest_crop_tile"),
                Supported("harvest_giant_crop_tile"),
                Supported("harvest_ginger"),
                Supported("interact_endpoint"),
                Supported("load_machine_input_tile"),
                Supported("mining_acquire_golden_scythe_plan_envelope"),
                Supported("mining_obtain_skull_key_plan_envelope"),
                Supported("mining_reach_depth_plan_envelope"),
                Supported("pan_ore_spot"),
                Supported("pet_daily_interaction"),
                Supported("pickup_debris_item"),
                Supported("plant_seed_tile"),
                Supported("purchase_farmhouse_expansion"),
                Supported("purchase_farmhouse_upgrade"),
                Supported("purchase_joja_membership"),
                Supported("purchase_joja_project"),
                Supported("read_inventory_book"),
                Supported("recovery_close_menu"),
                Supported("recovery_refresh_plan"),
                Supported("recovery_return_home"),
                Supported("recovery_sleep_before_collapse"),
                Supported("recovery_sleep_immediately"),
                Supported("route_connector_tile"),
                Supported("ship_inventory_item_to_bin"),
                Supported("social_continuation_retry_wait"),
                Supported("social_gift_current"),
                Supported("social_talk_current"),
                Supported("volcano_reach_caldera_plan_envelope"),
                Supported("water_crop_tile"),
                Supported("sell_shop_item"),
                Blocked("quest_candidate", "quest_native_executor_not_implemented"),
                Blocked("special_order_candidate", "quest_native_executor_not_implemented")
            });

        private static readonly IReadOnlyDictionary<string, DailyPlanCandidateCapability> ByKind =
            new ReadOnlyDictionary<string, DailyPlanCandidateCapability>(
                Catalog.ToDictionary(row => row.Kind, StringComparer.Ordinal));

        public static IReadOnlyList<DailyPlanCandidateCapability> All => Catalog;

        public static IReadOnlyCollection<string> CompilableKinds { get; } =
            new ReadOnlyCollection<string>(Catalog
                .Where(row => row.Compilable)
                .Select(row => row.Kind)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());

        public static bool TryGet(string kind, out DailyPlanCandidateCapability capability)
        {
            return ByKind.TryGetValue(kind, out capability!);
        }

        private static DailyPlanCandidateCapability Supported(string kind)
        {
            return new DailyPlanCandidateCapability(kind, true);
        }

        private static DailyPlanCandidateCapability Blocked(string kind, string reason)
        {
            return new DailyPlanCandidateCapability(kind, false, reason);
        }
    }
}
