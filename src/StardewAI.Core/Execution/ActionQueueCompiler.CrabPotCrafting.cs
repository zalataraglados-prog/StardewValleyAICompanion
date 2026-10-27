using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private static CompiledActionStep[] CompileCraftCrabPotStep(
        SmallModelAction action)
        => CompileExactSupportCraftingStep(
            action,
            "craft_crab_pot",
            "(O)710",
            "crab_pot_lifecycle_replan_for_placement=true");

    private static string[] ValidateCraftCrabPotPlan(
        SmallModelAction action,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger? commitmentLedger)
    {
        if (action.OptionId != "executor.craft_crab_pot")
            return Array.Empty<string>();

        var reasons = new List<string>();
        var binding = ValidateExactSupportCraftingBinding(
            action,
            snapshot,
            commitmentLedger,
            "crab_pot_crafting",
            "(O)710",
            "craft_crab_pot",
            reasons);
        if (binding is null)
            return reasons.ToArray();

        var placement = ReadStateFieldValue(
            snapshot,
            "player",
            "crab_pot_placement");
        if (!placement.HasValue ||
            placement.Value.ValueKind != JsonValueKind.Object ||
            ReadInt(placement.Value, "inventory_crab_pot_count") != 0 ||
            ReadInt(
                placement.Value,
                "potential_static_legal_tile_count") <= 0)
        {
            reasons.Add("craft_crab_pot_capacity_demand_drifted");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
