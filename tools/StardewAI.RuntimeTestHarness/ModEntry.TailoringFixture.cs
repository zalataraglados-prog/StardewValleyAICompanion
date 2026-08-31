using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupTailoringFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());
        if (request.TailoringRecipeId is not ("deterministic_recipe" or "random_recipe" or "boots_stat_transfer"))
            return BlockedWithPrimitive(
                request,
                "debug_setup_tailoring_fixture",
                "tailoring_fixture=ready",
                "tailoring_fixture=blocked",
                "tailoring_fixture_mode_unsupported");

        var player = Game1.player;
        for (var slot = 0; slot < player.Items.Count; slot++)
            player.Items[slot] = null;
        Game1.activeClickableMenu = null;
        switch (request.TailoringRecipeId)
        {
            case "deterministic_recipe":
                player.Items[0] = ItemRegistry.Create("(O)428");
                player.Items[1] = ItemRegistry.Create("(O)388");
                break;
            case "random_recipe":
                player.Items[0] = ItemRegistry.Create("(O)428");
                player.Items[1] = ItemRegistry.Create("(O)74");
                break;
            case "boots_stat_transfer":
                player.Items[0] = ItemRegistry.Create("(B)504");
                player.Items[1] = ItemRegistry.Create("(B)514");
                break;
        }

        var farm = Game1.getFarm();
        foreach (var pair in farm.objects.Pairs.Where(pair => pair.Value.QualifiedItemId == "(BC)247").ToArray())
            farm.objects.Remove(pair.Key);
        var tile = FindOpenFixtureInteractionTile(farm);
        if (!tile.HasValue)
            return BlockedWithPrimitive(
                request,
                "debug_setup_tailoring_fixture",
                "tailoring_fixture=ready",
                "tailoring_fixture=blocked",
                "tailoring_fixture_tile_missing");
        farm.objects[tile.Value.ToVector2()] = ItemRegistry.Create<StardewValley.Object>("(BC)247");
        var moved = MoveFixtureFarmerToLocationAdjacent(farm, tile.Value, out var stand, out var moveReason);
        var verified = moved && player.Items[0] is not null && player.Items[1] is not null &&
            farm.objects.TryGetValue(tile.Value.ToVector2(), out var machine) && machine.QualifiedItemId == "(BC)247";
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            PrimitiveKind = "debug_setup_tailoring_fixture",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_save_tailoring_fixture_ready", "exact_inputs_and_placed_sewing_machine_ready" }
                : new[] { moveReason, "tailoring_fixture_post_state_mismatch" },
            RequestedEffect = "tailoring_fixture=ready;mode=" + request.TailoringRecipeId,
            ObservedEffect = "location=" + farm.NameOrUniqueName + ";target=" + tile.Value.X + "," + tile.Value.Y +
                ";stand=" + stand.X + "," + stand.Y,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "tailoring_fixture_post_state_mismatch" }
        };
    }
}
