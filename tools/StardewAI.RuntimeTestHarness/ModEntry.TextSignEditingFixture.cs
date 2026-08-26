using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupTextSignEditTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        Game1.eventUp = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        Game1.activeClickableMenu = null;

        var requested = request.TargetTileX.HasValue && request.TargetTileY.HasValue
            ? new Point(request.TargetTileX.Value, request.TargetTileY.Value)
            : new Point(-1, -1);
        var layers = farm.map?.Layers?.Cast<xTile.Layers.Layer>().ToArray() ?? Array.Empty<xTile.Layers.Layer>();
        var width = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerWidth);
        var height = layers.Length == 0 ? 0 : layers.Max(layer => layer.LayerHeight);
        var candidates = new[] { requested }
            .Concat(Enumerable.Range(0, height).SelectMany(y => Enumerable.Range(0, width).Select(x => new Point(x, y))))
            .Where(tile => tile.X >= 0 && tile.Y >= 0)
            .Distinct()
            .OrderBy(tile => requested.X >= 0 ? ManhattanDistance(tile, requested) : ManhattanDistance(tile, Game1.player.TilePoint));

        Point target = Point.Zero;
        Point stand = Point.Zero;
        StardewObject? sign = null;
        var blockReason = "fixture_no_text_sign_target";
        foreach (var candidate in candidates)
        {
            var key = candidate.ToVector2();
            if (farm.objects.TryGetValue(key, out var existing) &&
                (existing.GetType() != typeof(StardewObject) || !existing.IsTextSign()))
            {
                continue;
            }
            if (existing is null && farm.terrainFeatures.ContainsKey(key))
            {
                continue;
            }
            if (!MoveFixtureFarmerToLocationAdjacent(farm, candidate, out var candidateStand, out var moveReason))
            {
                blockReason = moveReason;
                continue;
            }
            sign = existing;
            if (sign is null)
            {
                sign = ItemRegistry.Create<StardewObject>("(BC)TextSign");
                sign.Location = farm;
                sign.TileLocation = key;
                farm.objects.Add(key, sign);
            }
            sign.signText.Value = request.TextSignFixtureInitialText.Trim();
            sign.showNextIndex.Value = string.IsNullOrEmpty(sign.SignText);
            target = candidate;
            stand = candidateStand;
            break;
        }

        var verified = sign?.GetType() == typeof(StardewObject) && sign.IsTextSign() &&
            Game1.player.TilePoint == stand &&
            string.Equals(sign.signText.Value ?? string.Empty, request.TextSignFixtureInitialText.Trim(), StringComparison.Ordinal);
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            TargetTileX = target.X,
            TargetTileY = target.Y,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_text_sign_edit_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "exact_base_TextSign_ready", "adjacent_fixture_stand_ready", "initial_raw_text_and_showNextIndex_ready" }
                : new[] { blockReason },
            RequestedEffect = "text_sign_edit_fixture=ready",
            ObservedEffect = "location_id=" + farm.NameOrUniqueName + ";target_tile=" + target.X + "," + target.Y +
                ";stand_tile=" + stand.X + "," + stand.Y + ";raw_sign_text=" + (sign?.signText.Value ?? string.Empty) +
                ";display_sign_text=" + (sign?.SignText ?? string.Empty) +
                ";show_next_index=" + (sign?.showNextIndex.Value.ToString().ToLowerInvariant() ?? "missing"),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "text_sign_edit_fixture_not_ready" }
        };
    }
}
