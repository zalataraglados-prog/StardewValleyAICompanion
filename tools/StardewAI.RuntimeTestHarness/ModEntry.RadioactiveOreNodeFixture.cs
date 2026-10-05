using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupRadioactiveOreNode(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_radioactive_ore_node",
                "mining.breakable_stones[target].item_id=95",
                "location=not_mineshaft",
                "radioactive_ore_node_fixture_requires_loaded_mine");
        }

        var target = FindMiningCombatFixtureTarget(
            mine,
            requireClearProjectilePath: false,
            requireBombEscape: false);
        if (!target.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_radioactive_ore_node",
                "mining.breakable_stones[target].item_id=95",
                "target_tile=missing",
                "radioactive_ore_node_fixture_no_reachable_tile");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        mine.objects.Clear();
        mine.resourceClumps.Clear();
        ClearMiningFixtureArea(mine, target.Value, radius: 2);
        foreach (var monster in mine.characters.OfType<Monster>().ToArray())
        {
            mine.characters.Remove(monster);
        }

        EnsureFixtureInventoryCapacity(Game1.player);
        var pickaxe = Game1.player.Items
            .OfType<Pickaxe>()
            .OrderByDescending(tool => tool.UpgradeLevel)
            .FirstOrDefault();
        if (pickaxe is null)
        {
            pickaxe = new Pickaxe();
            InstallFixtureItem(Game1.player, pickaxe);
        }
        pickaxe.UpgradeLevel = Math.Max(pickaxe.UpgradeLevel, 4);
        pickaxe.additionalPower.Value = 0;
        var pickaxeSlot = Game1.player.Items.IndexOf(pickaxe);
        Game1.player.CurrentToolIndex = pickaxeSlot;
        Game1.player.Stamina = Math.Max(Game1.player.Stamina, 200f);

        var tile = target.Value.ToVector2();
        var oreNode = ItemRegistry.Create<StardewValley.Object>("(O)95");
        oreNode.TileLocation = tile;
        oreNode.Location = mine;
        mine.objects[tile] = oreNode;

        var verified = mine.objects.TryGetValue(tile, out var observed) &&
            ReferenceEquals(observed, oreNode) &&
            observed.GetType() == typeof(StardewValley.Object) &&
            observed.QualifiedItemId == "(O)95" &&
            pickaxeSlot >= 0;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = mine.NameOrUniqueName,
            TargetTileX = target.Value.X,
            TargetTileY = target.Value.Y,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_radioactive_ore_node",
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "exact_native_radioactive_ore_node_present",
                    "reachable_native_mine_tile_present",
                    "native_pickaxe_present"
                }
                : new[] { "radioactive_ore_node_fixture_state_mismatch" },
            RequestedEffect =
                "mining.breakable_stones[target].item_id=95;" +
                "authoritative_drop=(O)909",
            ObservedEffect =
                "location=" + mine.NameOrUniqueName +
                ";target=" + target.Value.X + "," + target.Value.Y +
                ";node=" + (observed?.QualifiedItemId ?? "missing") +
                ";pickaxe_slot=" + pickaxeSlot,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "radioactive_ore_node_fixture_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "mining.breakable_stones[" +
                            target.Value.X + "," + target.Value.Y + "].item_id",
                        Before = "absent",
                        After = "95"
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }
}
