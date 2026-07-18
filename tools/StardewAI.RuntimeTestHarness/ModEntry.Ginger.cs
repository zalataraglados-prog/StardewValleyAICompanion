using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string GingerQualifiedItemId = "(O)829";

    private void StartHarvestGinger(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "harvest_ginger", "current_location.ginger[target].present=false", GingerHarvestObservedEffect(Game1.currentLocation, null), "target_tile_required"));
            return;
        }

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX.Value, request.TargetTileY.Value);
        var hoe = FindTool<Hoe>();
        var started = DateTimeOffset.UtcNow.ToString("O");
        var staminaBefore = Game1.player.Stamina;
        var estimatedTicks = EstimateRuntimeToolTicks(target);
        var requested = GingerHarvestRequestedEffect(target);
        var precheck = ValidateGingerHarvestTarget(location, target, hoe, request);
        if (precheck.Length > 0)
        {
            pending.Completion.SetResult(NativeToolBlocked(request, "harvest_ginger", target, hoe, null, staminaBefore, started, estimatedTicks, precheck[0], requested, GingerHarvestObservedEffect(location, target), precheck));
            return;
        }

        var path = BuildAdjacentToolPath(location, target, request.MaxMovementTiles ?? 512, out var moveReason);
        if (path is null)
        {
            pending.Completion.SetResult(NativeToolBlocked(request, "harvest_ginger", target, hoe, null, staminaBefore, started, estimatedTicks, moveReason, requested, GingerHarvestObservedEffect(location, target)));
            return;
        }

        var tile = new Vector2(target.X, target.Y);
        var dirt = (HoeDirt)location.terrainFeatures[tile];
        var expectedEnergyCost = GingerHoeEnergyCost(hoe!);
        activeNativeTool = ActiveNativeTool.Ginger(
            pending,
            location.NameOrUniqueName,
            target,
            path,
            hoe!,
            staminaBefore,
            started,
            estimatedTicks,
            requested,
            CountGingerOutputDebris(location),
            CountInventoryItemAtQuality(GingerQualifiedItemId, 0),
            Game1.player.experiencePoints[Farmer.foragingSkill],
            dirt.state.Value,
            expectedEnergyCost);
    }

    private static string[] ValidateGingerHarvestTarget(GameLocation location, Point target, Hoe? hoe, TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        var tile = new Vector2(target.X, target.Y);
        if (!IsTileOnMap(location, target))
        {
            reasons.Add("harvest_ginger_invalid_tile");
        }
        if (Game1.activeClickableMenu is not null)
        {
            reasons.Add("harvest_ginger_menu_must_be_clear");
        }
        if (hoe is null)
        {
            reasons.Add("harvest_ginger_hoe_missing");
        }
        else
        {
            if (hoe.GetType() != typeof(Hoe))
            {
                reasons.Add("harvest_ginger_custom_hoe_runtime_type");
            }
            var actualSlot = Game1.player.Items.IndexOf(hoe);
            if (!request.ToolSlotIndex.HasValue || request.ToolSlotIndex.Value != actualSlot)
            {
                reasons.Add("harvest_ginger_tool_slot_drifted");
            }
            if (Game1.player.Stamina < GingerHoeEnergyCost(hoe))
            {
                reasons.Add("harvest_ginger_insufficient_energy");
            }
        }
        if (!IsExactGinger(location, tile, out _))
        {
            reasons.Add("harvest_ginger_target_not_exact_ginger");
        }
        if (!string.Equals(request.RequiredToolKind, "Hoe", StringComparison.Ordinal))
        {
            reasons.Add("harvest_ginger_required_tool_kind_drifted");
        }
        if (request.ExpectedForagingExperienceDelta != 7)
        {
            reasons.Add("harvest_ginger_experience_projection_drifted");
        }
        if (!string.Equals(request.QualifiedItemId, GingerQualifiedItemId, StringComparison.Ordinal) ||
            request.Quantity != 1 || request.ExpectedOutputQuality != 0)
        {
            reasons.Add("harvest_ginger_output_projection_drifted");
        }

        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void CompleteHarvestGingerNativeTool(ActiveNativeTool tool)
    {
        var location = Game1.currentLocation;
        var tile = new Vector2(tool.Target.X, tool.Target.Y);
        var hasHoeDirt = location.terrainFeatures.TryGetValue(tile, out var feature) && feature is HoeDirt;
        var hasGinger = IsExactGinger(location, tile, out var dirt);
        var debrisAfter = CountGingerOutputDebris(location);
        var inventoryAfter = CountInventoryItemAtQuality(GingerQualifiedItemId, 0);
        var foragingAfter = Game1.player.experiencePoints[Farmer.foragingSkill];
        var expectedStateAfter = location.IsRainingHere() ? 1 : 0;
        var energyDelta = tool.StaminaBefore - Game1.player.Stamina;
        var verified = tool.BeforeGinger &&
            hasHoeDirt && !hasGinger &&
            dirt?.state.Value == expectedStateAfter &&
            debrisAfter + inventoryAfter == tool.BeforeGingerDebrisCount + tool.BeforeGingerInventoryCount + 1 &&
            foragingAfter == tool.BeforeForagingExperience + 7 &&
            Math.Abs(energyDelta - tool.ExpectedEnergyCost) <= 0.001d;
        var failureReasons = new List<string>();
        if (!hasHoeDirt || hasGinger) failureReasons.Add("ginger_crop_not_removed_or_hoe_dirt_missing");
        if (dirt?.state.Value != expectedStateAfter) failureReasons.Add("ginger_hoe_dirt_state_after_mismatch");
        if (debrisAfter + inventoryAfter != tool.BeforeGingerDebrisCount + tool.BeforeGingerInventoryCount + 1) failureReasons.Add("ginger_output_total_delta_mismatch");
        if (foragingAfter != tool.BeforeForagingExperience + 7) failureReasons.Add("ginger_foraging_experience_delta_mismatch");
        if (Math.Abs(energyDelta - tool.ExpectedEnergyCost) > 0.001d) failureReasons.Add("ginger_energy_delta_mismatch");

        tool.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = tool.Pending.Request.RunId,
            QueueId = tool.Pending.Request.QueueId,
            QueueItemId = tool.Pending.Request.QueueItemId,
            BeforeStateHash = tool.Pending.Request.BeforeStateHash,
            OptionId = tool.Pending.Request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            EnergyBefore = tool.StaminaBefore,
            EnergyAfter = Game1.player.Stamina,
            TargetLocation = location.NameOrUniqueName,
            TargetTileX = tool.Target.X,
            TargetTileY = tool.Target.Y,
            ToolQualifiedItemId = tool.Tool.QualifiedItemId,
            ToolUpgradeLevel = tool.Tool.UpgradeLevel,
            ToolPower = Game1.player.toolPower.Value,
            EstimatedTicks = tool.EstimatedTicks,
            ActualTicks = tool.ElapsedTicks,
            FailureCategory = verified ? string.Empty : "ginger_native_postcondition_mismatch",
            TrainingImpactScope = "executor_calibration",
            StartedAt = tool.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "harvest_ginger",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified ? NativeToolVerifiedReasons(tool) : failureReasons.ToArray(),
            RequestedEffect = tool.RequestedEffect,
            ObservedEffect = GingerHarvestObservedEffect(location, tool.Target) + ";move_ticks=" + Math.Min(tool.ElapsedTicks, tool.MaxMovementTicks),
            BlockReasons = verified ? Array.Empty<string>() : failureReasons.ToArray(),
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "current_location.terrain_features[" + tool.Target.X + "," + tool.Target.Y + "].crop", Before = "ginger", After = hasGinger ? "ginger" : "none" },
                new SimulatedFactChange { Path = "current_location.terrain_features[" + tool.Target.X + "," + tool.Target.Y + "].hoe_dirt_state", Before = tool.BeforeHoeDirtState?.ToString() ?? "missing", After = dirt?.state.Value.ToString() ?? "missing" },
                new SimulatedFactChange { Path = "current_location.debris.count[(O)829]", Before = tool.BeforeGingerDebrisCount.ToString(), After = debrisAfter.ToString() },
                new SimulatedFactChange { Path = "player.inventory.count[(O)829,quality=0]", Before = tool.BeforeGingerInventoryCount.ToString(), After = inventoryAfter.ToString() },
                new SimulatedFactChange { Path = "player.skills.foraging.experience", Before = tool.BeforeForagingExperience.ToString(), After = foragingAfter.ToString() },
                new SimulatedFactChange { Path = "player.energy", Before = tool.StaminaBefore.ToString("0.###"), After = Game1.player.Stamina.ToString("0.###") }
            }
        });
    }

    private static bool IsExactGinger(GameLocation location, Vector2 tile, out HoeDirt? dirt)
    {
        dirt = location.terrainFeatures.TryGetValue(tile, out var feature) ? feature as HoeDirt : null;
        return dirt?.GetType() == typeof(HoeDirt) &&
            dirt.crop is { } crop &&
            crop.GetType() == typeof(Crop) &&
            crop.forageCrop.Value &&
            crop.whichForageCrop.Value == Crop.forageCrop_gingerID;
    }

    private static double GingerHoeEnergyCost(Hoe hoe)
    {
        return hoe.isEfficient.Value ? 0d : Math.Max(0d, 2d - Game1.player.FarmingLevel * 0.1d);
    }

    private static int CountGingerOutputDebris(GameLocation location)
    {
        return location.debris.Count(debris =>
            string.Equals(DebrisQualifiedItemId(debris), GingerQualifiedItemId, StringComparison.OrdinalIgnoreCase) &&
            (debris.item?.Quality ?? debris.itemQuality) == 0);
    }

    private static string GingerHarvestRequestedEffect(Point target)
    {
        return "current_location.terrain_features[" + target.X + "," + target.Y + "].crop=none;current_location.debris[(O)829].count_increases=1;player.skills.foraging.experience_delta=7;native_tool=Hoe";
    }

    private static string GingerHarvestObservedEffect(GameLocation location, Point? target)
    {
        if (!target.HasValue)
        {
            return "location=" + location.NameOrUniqueName + ";target=missing";
        }

        var tile = new Vector2(target.Value.X, target.Value.Y);
        var hasGinger = IsExactGinger(location, tile, out var dirt);
        return "location=" + location.NameOrUniqueName +
            ";target=" + target.Value.X + "," + target.Value.Y +
            ";has_hoe_dirt=" + (dirt is not null).ToString().ToLowerInvariant() +
            ";has_ginger=" + hasGinger.ToString().ToLowerInvariant() +
            ";hoe_dirt_state=" + (dirt?.state.Value.ToString() ?? "missing") +
            ";ginger_quality_zero_debris_count=" + CountGingerOutputDebris(location) +
            ";ginger_quality_zero_inventory_count=" + CountInventoryItemAtQuality(GingerQualifiedItemId, 0) +
            ";foraging_xp=" + Game1.player.experiencePoints[Farmer.foragingSkill];
    }
}
