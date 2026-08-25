using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Objects;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupSignDisplayItemTarget(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        var fixtureSource = ResolveSignDisplayFixtureSource(request.SignDisplayFixtureFamily, request.QualifiedItemId);
        if (fixtureSource is null)
        {
            return BlockedWithPrimitive(request, "debug_setup_sign_display_item_target",
                "sign_display_fixture=ready", "source_item=missing", "fixture_source_family_or_qid_required");
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var farm = Game1.getFarm();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        Game1.eventUp = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        Game1.activeClickableMenu = null;

        var slot = EnsureInventoryItem(fixtureSource.QualifiedItemId, Math.Max(1, request.Quantity ?? 1));
        var source = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] : null;
        if (source is null || ItemRegistry.GetDataOrErrorItem(source.QualifiedItemId).IsErrorItem)
        {
            return BlockedWithPrimitive(request, "debug_setup_sign_display_item_target",
                "sign_display_fixture=ready", "source_item=unavailable", "fixture_source_item_required");
        }

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
        var found = false;
        var blockReason = "fixture_no_sign_target";
        foreach (var candidate in candidates)
        {
            var key = candidate.ToVector2();
            if (farm.objects.TryGetValue(key, out var existing) && existing is not Sign)
            {
                continue;
            }
            if (existing is Sign typed && typed.GetType() != typeof(Sign))
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
            if (existing is null)
            {
                var sign = new Sign(key, "37") { Location = farm, TileLocation = key };
                farm.objects.Add(key, sign);
            }
            target = candidate;
            stand = candidateStand;
            found = true;
            break;
        }

        var targetSign = found && farm.objects.TryGetValue(target.ToVector2(), out var resultObject)
            ? resultObject as Sign
            : null;
        var verified = found && targetSign?.GetType() == typeof(Sign) && Game1.player.TilePoint == stand;
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
            PrimitiveKind = "debug_setup_sign_display_item_target",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "exact_base_display_sign_ready",
                    "source_inventory_slot=" + slot,
                    "source_runtime_type=" + source.GetType().FullName,
                    "expected_display_type=" + RuntimeSignDisplayType(source),
                    "existing_payload_preserved_for_replacement_case=" + (targetSign!.displayItem.Value is not null).ToString().ToLowerInvariant()
                }
                : new[] { blockReason },
            RequestedEffect = "sign_display_fixture=ready",
            ObservedEffect = "location_id=" + farm.NameOrUniqueName + ";target_tile=" + target.X + "," + target.Y +
                ";stand_tile=" + stand.X + "," + stand.Y + ";inventory_slot_index=" + slot +
                ";qualified_item_id=" + source.QualifiedItemId + ";source_runtime_type=" + source.GetType().FullName +
                ";expected_display_type=" + RuntimeSignDisplayType(source) +
                ";previous_display_item=" + (targetSign?.displayItem.Value?.QualifiedItemId ?? "null"),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "sign_display_item_fixture_not_ready" }
        };
    }

    private static Item? ResolveSignDisplayFixtureSource(string family, string fallbackQualifiedItemId)
    {
        Item? Exact(Func<Item, bool> predicate, IEnumerable<string> ids)
        {
            foreach (var id in ids.OrderBy(value => value, StringComparer.Ordinal))
            {
                var candidate = ItemRegistry.Create(id);
                if (!ItemRegistry.GetDataOrErrorItem(candidate.QualifiedItemId).IsErrorItem && predicate(candidate))
                {
                    return candidate;
                }
            }
            return null;
        }

        return family switch
        {
            "ordinary_object" => Exact(
                item => item.GetType() == typeof(StardewValley.Object) && item is StardewValley.Object { bigCraftable.Value: false },
                Game1.objectData.Keys.Select(id => "(O)" + id)),
            "big_object" => Exact(
                item => item.GetType() == typeof(StardewValley.Object) && item is StardewValley.Object { bigCraftable.Value: true },
                Game1.bigCraftableData.Keys.Select(id => "(BC)" + id)),
            "hat" => Exact(item => item.GetType() == typeof(Hat),
                Game1.content.Load<Dictionary<string, string>>("Data\\hats").Keys.Select(id => "(H)" + id)),
            "ring" => Exact(item => item.GetType() == typeof(Ring), Game1.objectData.Keys.Select(id => "(O)" + id)),
            "furniture" => Exact(item => item is Furniture,
                Game1.content.Load<Dictionary<string, string>>("Data\\Furniture").Keys.Select(id => "(F)" + id)),
            "tool_default" => Game1.player.Items.OfType<Tool>().FirstOrDefault() ?? new Axe(),
            _ when !string.IsNullOrWhiteSpace(fallbackQualifiedItemId) => ItemRegistry.Create(fallbackQualifiedItemId),
            _ => null
        };
    }
}
