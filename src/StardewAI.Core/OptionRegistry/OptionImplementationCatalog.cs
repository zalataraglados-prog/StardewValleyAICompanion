using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using StardewAI.Contracts.Capabilities;

namespace StardewAI.Core.OptionRegistry;

public static class ImplementationEngineIds
{
    public const string StrategyOrchestration = "engine.strategy_orchestration";
    public const string MovementNavigation = "engine.movement_navigation";
    public const string InteractionMenu = "engine.interaction_menu";
    public const string RecoveryTiming = "engine.recovery_timing";
    public const string DungeonTraversal = "engine.dungeon_traversal";
    public const string Combat = "engine.combat";
    public const string Fishing = "engine.fishing";
    public const string InventoryTransfer = "engine.inventory_transfer";
    public const string ToolHarvest = "engine.tool_harvest";
    public const string FarmMachine = "engine.farm_machine";
    public const string AnimalManagement = "engine.animal_management";
    public const string CraftingProcessing = "engine.crafting_processing";
    public const string Minigame = "engine.minigame";
}

public sealed class OptionImplementationBinding
{
    public string OptionId { get; init; } = string.Empty;
    public string PrimaryEngineId { get; init; } = string.Empty;
    public string AdapterId { get; init; } = "vanilla_native";
    public string CandidateBinding { get; init; } = string.Empty;
    public string CompilerBinding { get; init; } = string.Empty;
    public string RuntimeBinding { get; init; } = string.Empty;
    public string VerifierBinding { get; init; } = string.Empty;
    public string EvidenceBinding { get; init; } = string.Empty;
}

public static class OptionImplementationCatalog
{
    private static readonly HashSet<string> MovementOptions = Set(
        "executor.move_to_tile",
        "executor.traverse_connector",
        "executor.face_direction");

    private static readonly HashSet<string> InteractionOptions = Set(
        "executor.interact",
        "executor.accept_daily_quest",
        "executor.accept_special_order",
        "executor.claim_quest_reward",
        "executor.buy_shop_item",
        "executor.sell_shop_item",
        "executor.choose_dialogue_response",
        "executor.choose_animal_purchase_response",
        "executor.purchase_animal",
        "executor.manage_animal",
        "executor.close_menu",
        "executor.social_interact",
        "executor.quest_npc_interact",
        "executor.quest_drop_box_donate",
        "executor.donate_museum_item",
        "executor.donate_community_center_item",
        "executor.purchase_joja_membership",
        "executor.purchase_joja_project",
        "executor.purchase_farmhouse_upgrade",
        "executor.construct_building",
        "executor.change_building_skin",
        "executor.read_book");

    private static readonly HashSet<string> RecoveryOptions = Set(
        "executor.sleep",
        "executor.wait_ticks",
        "executor.consume_food");

    private static readonly HashSet<string> DungeonOptions = Set(
        "executor.claim_mine_reward_chest",
        "executor.mine_stone",
        "executor.break_container",
        "executor.break_resource_clump",
        "executor.place_staircase",
        "executor.descend_ladder",
        "executor.descend_shaft",
        "executor.exit_mine",
        "executor.cool_volcano_lava",
        "executor.break_volcano_stone",
        "executor.break_volcano_container");

    private static readonly HashSet<string> CombatOptions = Set(
        "executor.combat_monster",
        "executor.shoot_monster",
        "executor.place_bomb",
        "executor.combat_volcano_monster");

    private static readonly HashSet<string> FishingOptions = Set(
        "executor.catch_fish",
        "executor.collect_crab_pot",
        "executor.collect_fish_pond_output",
        "executor.complete_fish_pond_request");

    private static readonly HashSet<string> MinigameOptions = Set(
        "executor.play_junimo_kart");

    private static readonly HashSet<string> InventoryOptions = Set(
        "executor.ship_inventory_item_to_bin",
        "executor.transfer_material",
        "executor.select_safe_item_slot");

    private static readonly HashSet<string> ToolOptions = Set(
        "executor.clear_obstacle",
        "executor.break_farm_resource_clump",
        "executor.break_current_location_resource_clump",
        "executor.water_crop",
        "executor.apply_fertilizer",
        "executor.plant_seed",
        "executor.till_soil",
        "executor.harvest_crop",
        "executor.harvest_giant_crop",
        "executor.pickup_debris",
        "executor.collect_spawned_object",
        "executor.harvest_ginger",
        "executor.harvest_bush",
        "executor.collect_animal_product",
        "executor.pet_interact",
        "executor.fill_pet_bowl",
        "executor.pan_ore_spot");

    private static readonly HashSet<string> FarmMachineOptions = Set(
        "executor.collect_machine_output",
        "executor.load_machine_input",
        "executor.name_hatched_animal",
        "executor.craft_machine_item",
        "executor.craft_storage_item",
        "executor.craft_quest_item",
        "executor.place_machine",
        "executor.remove_machine",
        "executor.place_storage");

    private static readonly HashSet<string> CraftingOptions = Set(
        "executor.cook_recipe");

    private static readonly IReadOnlyList<OptionImplementationBinding> Bindings = Build();
    private static readonly IReadOnlyDictionary<string, OptionImplementationBinding> ByOptionId =
        new ReadOnlyDictionary<string, OptionImplementationBinding>(
            Bindings.ToDictionary(row => row.OptionId, StringComparer.Ordinal));

    public static IReadOnlyList<OptionImplementationBinding> All => Bindings;

    public static OptionImplementationBinding GetRequired(string optionId)
    {
        if (!ByOptionId.TryGetValue(optionId, out var binding))
            throw new KeyNotFoundException($"No implementation ownership binding for '{optionId}'.");
        return binding;
    }

    private static IReadOnlyList<OptionImplementationBinding> Build()
    {
        var rows = OptionCapabilityRegistrySource.All
            .Select(capability => new OptionImplementationBinding
            {
                OptionId = capability.OptionId,
                PrimaryEngineId = ResolveEngine(capability.OptionId),
                CandidateBinding = ResolveCandidateBinding(capability.OptionId),
                CompilerBinding = ResolveCompilerBinding(capability.OptionId),
                RuntimeBinding = capability.ProductExecutorSupported
                    ? "product_executor"
                    : capability.HarnessDispatchSupported
                        ? "runtime_test_harness"
                        : capability.InternalExecutionPipelineSupported
                            ? "internal_execution_pipeline"
                            : "unbound",
                VerifierBinding = capability.AfterVerifierStatus == CapabilityVerifierStatus.RuntimeVerified
                    ? "runtime_before_after_verifier"
                    : "pending_runtime_postcondition",
                EvidenceBinding = capability.RuntimeEvidenceIds.Length == 0
                    ? "pending"
                    : string.Join(",", capability.RuntimeEvidenceIds)
            })
            .OrderBy(row => row.OptionId, StringComparer.Ordinal)
            .ToArray();

        if (rows.Select(row => row.OptionId).Distinct(StringComparer.Ordinal).Count() != rows.Length)
            throw new InvalidOperationException("Implementation ownership contains duplicate option IDs.");
        if (rows.Any(row => string.IsNullOrWhiteSpace(row.PrimaryEngineId)))
            throw new InvalidOperationException("Every option must have exactly one primary implementation engine.");

        return new ReadOnlyCollection<OptionImplementationBinding>(rows);
    }

    private static string ResolveEngine(string optionId)
    {
        if (optionId is "animals.manage_animal" or "executor.manage_animal")
            return ImplementationEngineIds.AnimalManagement;
        if (optionId is "crafting.cook_recipe" or "executor.cook_recipe")
            return ImplementationEngineIds.CraftingProcessing;
        if (optionId == "inventory.transfer_item")
            return ImplementationEngineIds.InventoryTransfer;
        if (!optionId.StartsWith("executor.", StringComparison.Ordinal))
            return ImplementationEngineIds.StrategyOrchestration;
        if (MovementOptions.Contains(optionId))
            return ImplementationEngineIds.MovementNavigation;
        if (InteractionOptions.Contains(optionId))
            return ImplementationEngineIds.InteractionMenu;
        if (RecoveryOptions.Contains(optionId))
            return ImplementationEngineIds.RecoveryTiming;
        if (DungeonOptions.Contains(optionId))
            return ImplementationEngineIds.DungeonTraversal;
        if (CombatOptions.Contains(optionId))
            return ImplementationEngineIds.Combat;
        if (FishingOptions.Contains(optionId))
            return ImplementationEngineIds.Fishing;
        if (MinigameOptions.Contains(optionId))
            return ImplementationEngineIds.Minigame;
        if (InventoryOptions.Contains(optionId))
            return ImplementationEngineIds.InventoryTransfer;
        if (ToolOptions.Contains(optionId))
            return ImplementationEngineIds.ToolHarvest;
        if (FarmMachineOptions.Contains(optionId))
            return ImplementationEngineIds.FarmMachine;
        if (CraftingOptions.Contains(optionId))
            return ImplementationEngineIds.CraftingProcessing;

        throw new InvalidOperationException(
            $"Executor option '{optionId}' has no explicit primary implementation engine.");
    }

    private static string ResolveCandidateBinding(string optionId)
    {
        if (optionId.StartsWith("executor.", StringComparison.Ordinal))
            return "not_applicable";
        if (optionId == "inventory.transfer_item")
            return "StardewAI.Core.OptionRegistry.CandidateOptionAvailabilityEvaluator.MaterialTransfer";
        if (optionId == "quest.advance")
            return "StardewAI.Core.OptionRegistry.QuestCandidateBuilder";
        if (optionId == "strategy.grandpa_progress")
            return "StardewAI.Core.Training.GrandpaDirectionCatalog";
        if (optionId.StartsWith("mining.", StringComparison.Ordinal))
            return "StardewAI.Core.OptionRegistry.MiningCandidateBuilders";
        if (optionId == "volcano.reach_caldera")
            return "StardewAI.Core.OptionRegistry.VolcanoReachCalderaCandidateBuilder";
        if (optionId.StartsWith("social.", StringComparison.Ordinal))
            return "StardewAI.Core.OptionRegistry.SocialCandidateBuilder";
        return "StardewAI.Core.OptionRegistry.CandidateOptionAvailabilityEvaluator";
    }

    private static string ResolveCompilerBinding(string optionId)
    {
        var step = Execution.ActionQueueCompiler.HasStepCompiler(optionId) ||
            Training.DailyPlanCompiler.HasOptionCompiler(optionId);
        var parameter = Execution.ActionQueueCompiler.HasParameterCompiler(optionId);
        return step && parameter
            ? "step+parameter"
            : step
                ? "step"
                : parameter
                    ? "parameter"
                    : "unbound";
    }

    private static HashSet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);
}
