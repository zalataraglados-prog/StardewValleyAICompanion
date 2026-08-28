using StardewAI.Contracts.Training;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupDrumBlockFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return BlockedWithPrimitive(request, "debug_setup_drum_block", "isolated_drum_block=ready", "drum_block=unverified", reasons.ToArray());
        var farm = Game1.getFarm();
        foreach (var tile in farm.objects.Pairs.Where(pair => pair.Value.GetType() == typeof(StardewObject) && pair.Value.QualifiedItemId == "(O)463").Select(pair => pair.Key).ToArray())
            farm.objects.Remove(tile);
        var target = FindHousePlantFixtureTile(farm);
        if (target is null)
            return BlockedWithPrimitive(request, "debug_setup_drum_block", "isolated_drum_block=ready", "drum_block=missing", "drum_block_fixture_tile_unavailable");
        var emptySlot = Enumerable.Range(0, Math.Min(12, Game1.player.Items.Count)).FirstOrDefault(index => Game1.player.Items[index] is null, -1);
        if (emptySlot < 0)
            return BlockedWithPrimitive(request, "debug_setup_drum_block", "isolated_drum_block=ready", "drum_block=missing", "drum_block_fixture_empty_toolbar_slot_unavailable");
        Game1.exitActiveMenu();
        var block = ItemRegistry.Create<StardewObject>("(O)463");
        block.TileLocation = target.Value.ToVector2();
        block.preservedParentSheetIndex.Value = "6";
        farm.objects[target.Value.ToVector2()] = block;
        var stand = Neighbors(target.Value).FirstOrDefault(tile => IsTileOnMap(farm, tile) && IsTileWalkable(farm, tile) && !IsTileOccupiedByCharacter(farm, tile) && !IsDestructiveObjectTrap(farm, tile));
        if (stand == default)
        {
            farm.objects.Remove(target.Value.ToVector2());
            return BlockedWithPrimitive(request, "debug_setup_drum_block", "isolated_drum_block=ready", "drum_block=missing", "drum_block_fixture_stand_unavailable");
        }
        Game1.warpFarmer(farm.NameOrUniqueName, stand.X, stand.Y, false);
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;
        var verified = farm.objects.TryGetValue(target.Value.ToVector2(), out var current) && ReferenceEquals(current, block) &&
            current.GetType() == typeof(StardewObject) && !current.bigCraftable.Value && current.QualifiedItemId == "(O)463" &&
            current.preservedParentSheetIndex.Value == "6" && Game1.player.Items[emptySlot] is null && Game1.player.TilePoint == stand;
        return new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId, BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId, Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"), CompletedAt = DateTimeOffset.UtcNow.ToString("O"), PrimitiveKind = "debug_setup_drum_block",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? new[] { "isolated_exact_base_drum_block_tone_6_and_empty_toolbar_slot_installed" } : new[] { "drum_block_fixture_setup_mismatch" },
            RequestedEffect = "qualified_item_id=(O)463;tone=6", ObservedEffect = verified ? "qualified_item_id=(O)463;tone=6" : "drum_block=missing",
            BlockReasons = verified ? Array.Empty<string>() : new[] { "drum_block_fixture_setup_mismatch" }
        };
    }
}
