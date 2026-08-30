using System.Globalization;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.Mining;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string CalicoStatueNativeContract =
        "MineShaft_Buildings_284_checkAction_then_recentlyActivatedCalicoStatue_event_then_master_seeded_effect_rating_and_native_side_effect_receipt";

    private void StartCalicoStatue(PendingExecution pending)
    {
        var request = pending.Request;
        var genericReasons = ValidateExecutionRequest(request);
        if (genericReasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, genericReasons.ToArray()));
            return;
        }
        if (!CalicoStatueTypedFieldsPresent(request))
        {
            pending.Completion.SetResult(CalicoStatueBlocked(request, "calico_statue_typed_fields_required"));
            return;
        }
        if (!Game1.IsMasterGame)
        {
            pending.Completion.SetResult(CalicoStatueBlocked(request, "calico_statue_host_authority_required"));
            return;
        }
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
        {
            pending.Completion.SetResult(CalicoStatueBlocked(request, "calico_statue_player_or_menu_not_ready"));
            return;
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            pending.Completion.SetResult(CalicoStatueBlocked(request, "calico_statue_current_location_not_mineshaft"));
            return;
        }

        var target = new Point(request.TargetTileX!.Value, request.TargetTileY!.Value);
        var stand = new Point(request.StandTileX!.Value, request.StandTileY!.Value);
        var reasons = ValidateCalicoStatueTarget(mine, target, stand, request, out var expectedEffect);
        if (reasons.Length > 0)
        {
            pending.Completion.SetResult(CalicoStatueBlocked(request, reasons));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(mine, Game1.player.TilePoint, stand, maxMovementTiles,
            out var pathReason, avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(CalicoStatueBlocked(request, "calico_statue_path_unavailable:" + pathReason));
            return;
        }

        activeCalicoStatue = new ActiveCalicoStatue(
            pending,
            mine,
            target,
            stand,
            path,
            maxMovementTiles,
            request.CalicoStatueTotalActivatedBefore!.Value,
            request.CalicoStatueRatingBefore!.Value,
            request.CalicoStatueEggsBefore!.Value,
            request.CalicoStatueCurrentEffectsCsv,
            request.CalicoStatueExpectedEffectsAfterCsv,
            expectedEffect!);
    }

    private static bool CalicoStatueTypedFieldsPresent(TrainingExecutionRequest request) =>
        request.TargetTileX.HasValue && request.TargetTileY.HasValue &&
        request.StandTileX.HasValue && request.StandTileY.HasValue &&
        request.CalicoStatueAcceptedEffectId.HasValue && request.CalicoStatueCalicoEggReward.HasValue &&
        request.CalicoStatueTotalActivatedBefore.HasValue && request.CalicoStatueNextActivationNumber.HasValue &&
        request.CalicoStatueRatingBefore.HasValue && request.CalicoStatueExpectedRatingAfter.HasValue &&
        request.CalicoStatueAverageDailyLuck.HasValue && request.CalicoStatueDaysPlayed.HasValue &&
        request.CalicoStatueUseLegacyRandom.HasValue && request.CalicoStatueMineLevel.HasValue &&
        request.CalicoStatueFestivalDay.HasValue && request.CalicoStatueTileIndexBefore.HasValue &&
        request.CalicoStatueTileIndexAfter.HasValue && request.CalicoStatueEggsBefore.HasValue &&
        request.CalicoStatueHealthBefore.HasValue && request.CalicoStatueMaxHealth.HasValue &&
        request.CalicoStatueStaminaBefore.HasValue && request.CalicoStatueMaxStamina.HasValue &&
        !string.IsNullOrWhiteSpace(request.CalicoStatueProjectionFingerprint) &&
        !string.IsNullOrWhiteSpace(request.CalicoStatueEffectKey) &&
        !string.IsNullOrWhiteSpace(request.CalicoStatueExactEffect) &&
        !string.IsNullOrWhiteSpace(request.CalicoStatueUniqueGameIdHalf);

    private static string[] ValidateCalicoStatueTarget(
        MineShaft mine,
        Point target,
        Point stand,
        TrainingExecutionRequest request,
        out CalicoStatueEffectDefinition? expectedEffect)
    {
        var reasons = new List<string>();
        expectedEffect = null;
        if (mine.getMineArea() != MineShaft.desertArea || mine.mineLevel <= 120 ||
            Utility.GetDayOfPassiveFestival("DesertFestival") <= 0)
        {
            reasons.Add("calico_statue_not_desert_festival_skull_cavern");
        }
        if (mine.calicoStatueSpot.Value != target || mine.recentlyActivatedCalicoStatue.Value != Point.Zero ||
            mine.getTileIndexAt(target.X, target.Y, "Buildings", "mine") != 284)
        {
            reasons.Add("calico_statue_exact_native_target_drifted");
        }
        if (!AreAdjacent(target, stand) || !IsTileOnMap(mine, stand) ||
            !IsTileWalkable(mine, stand) || IsTileOccupiedByCharacter(mine, stand))
        {
            reasons.Add("calico_statue_interaction_geometry_drifted");
        }

        var effects = CalicoStatueEffects();
        var effectId = request.CalicoStatueAcceptedEffectId ?? -1;
        if (effectId is >= 0 and <= 17)
        {
            expectedEffect = CalicoStatueEffectModel.GetRequired(effectId);
        }
        else
        {
            reasons.Add("calico_statue_effect_id_out_of_range");
        }
        var projectedId = CalicoStatueEffectModel.SelectEffect(
            Utility.CreateDaySaveRandom(MineShaft.totalCalicoStatuesActivatedToday + 1),
            Game1.player.team.AverageDailyLuck(mine),
            effects);
        if (projectedId != effectId)
        {
            reasons.Add("calico_statue_projected_effect_changed_replan_required");
        }

        if (!string.Equals(request.LocationId, mine.NameOrUniqueName, StringComparison.Ordinal) ||
            request.CalicoStatueMineLevel != mine.mineLevel ||
            request.CalicoStatueFestivalDay != Utility.GetDayOfPassiveFestival("DesertFestival") ||
            request.CalicoStatueTileIndexBefore != 284 || request.CalicoStatueTileIndexAfter != 285 ||
            request.CalicoStatueTotalActivatedBefore != MineShaft.totalCalicoStatuesActivatedToday ||
            request.CalicoStatueNextActivationNumber != MineShaft.totalCalicoStatuesActivatedToday + 1 ||
            request.CalicoStatueRatingBefore != Game1.player.team.calicoEggSkullCavernRating.Value ||
            request.CalicoStatueExpectedRatingAfter != Game1.player.team.calicoEggSkullCavernRating.Value + 1 ||
            request.CalicoStatueDaysPlayed != (int)Game1.stats.DaysPlayed ||
            !string.Equals(request.CalicoStatueUniqueGameIdHalf,
                (Game1.uniqueIDForThisGame / 2).ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal) ||
            request.CalicoStatueUseLegacyRandom != Game1.UseLegacyRandom ||
            !Approximately(request.CalicoStatueAverageDailyLuck!.Value, Game1.player.team.AverageDailyLuck(mine)) ||
            !string.Equals(request.CalicoStatueCurrentEffectsCsv, CalicoStatueEffectsCsv(effects), StringComparison.Ordinal) ||
            request.CalicoStatueEggsBefore != CountCalicoStatueEggs(mine) ||
            request.CalicoStatueHealthBefore != Game1.player.health ||
            request.CalicoStatueMaxHealth != Game1.player.maxHealth ||
            !Approximately(request.CalicoStatueStaminaBefore!.Value, Game1.player.Stamina) ||
            !Approximately(request.CalicoStatueMaxStamina!.Value, Game1.player.MaxStamina) ||
            !string.Equals(request.InteractionKind, "mineshaft_buildings_tile", StringComparison.Ordinal) ||
            !string.Equals(request.ExpectedActionType, "CalicoStatue", StringComparison.Ordinal) ||
            !string.Equals(request.NativeContract, CalicoStatueNativeContract, StringComparison.Ordinal))
        {
            reasons.Add("calico_statue_projection_drifted");
        }
        if (expectedEffect is not null &&
            (!string.Equals(request.CalicoStatueEffectKey, expectedEffect.EffectKey, StringComparison.Ordinal) ||
             !string.Equals(request.CalicoStatueStrategyPolarity, expectedEffect.StrategyPolarity, StringComparison.Ordinal) ||
             !string.Equals(request.CalicoStatueExactEffect, expectedEffect.ExactEffect, StringComparison.Ordinal) ||
             request.CalicoStatueCalicoEggReward != expectedEffect.CalicoEggReward ||
             !string.Equals(request.CalicoStatueExpectedEffectsAfterCsv,
                 CalicoStatueEffectsAfterCsv(effects, effectId), StringComparison.Ordinal)))
        {
            reasons.Add("calico_statue_effect_contract_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private void TickCalicoStatue()
    {
        var active = activeCalicoStatue;
        if (active is null)
        {
            return;
        }
        if (active.Stage == CalicoStatueStage.Move)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "calico_statue", out var failure);
            if (movement == NativeObjectMovementStatus.Failed)
            {
                CompleteCalicoStatue(active, false, failure);
                return;
            }
            if (movement == NativeObjectMovementStatus.Moving)
            {
                return;
            }

            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
            active.NativeHandled = active.Mine.checkAction(
                new TileLocation(active.Target.X, active.Target.Y),
                new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                Game1.player);
            active.ActionIssuedAtTick = active.ElapsedTicks;
            active.Stage = CalicoStatueStage.WaitReceipt;
            if (!active.NativeHandled)
            {
                CompleteCalicoStatue(active, false, "calico_statue_native_action_not_handled");
            }
            return;
        }

        active.ElapsedTicks++;
        if (!Context.IsWorldReady || !ReferenceEquals(Game1.currentLocation, active.Mine))
        {
            CompleteCalicoStatue(active, false, "calico_statue_location_changed_while_waiting_receipt");
            return;
        }
        if (CalicoStatueReceiptMatches(active))
        {
            CompleteCalicoStatue(active, true);
        }
        else if (active.ElapsedTicks - active.ActionIssuedAtTick > 180)
        {
            CompleteCalicoStatue(active, false, "calico_statue_native_receipt_mismatch");
        }
    }

    private static bool CalicoStatueReceiptMatches(ActiveCalicoStatue active)
    {
        if (active.Mine.recentlyActivatedCalicoStatue.Value != active.Target ||
            active.Mine.getTileIndexAt(active.Target.X, active.Target.Y, "Buildings", "mine") != 285 ||
            MineShaft.totalCalicoStatuesActivatedToday != active.TotalBefore + 1 ||
            Game1.player.team.calicoEggSkullCavernRating.Value != active.RatingBefore + 1 ||
            !string.Equals(CalicoStatueEffectsCsv(CalicoStatueEffects()), active.EffectsAfter, StringComparison.Ordinal) ||
            CountCalicoStatueEggs(active.Mine) != active.EggsBefore + active.ExpectedEffect.CalicoEggReward)
        {
            return false;
        }
        if (active.ExpectedEffect.EffectId == 10 && !Game1.player.hasBuff("CalicoStatueSpeed"))
        {
            return false;
        }
        return active.ExpectedEffect.EffectId != 11 ||
            (Game1.player.health == Game1.player.maxHealth && Approximately(Game1.player.Stamina, Game1.player.MaxStamina));
    }

    private void CompleteCalicoStatue(ActiveCalicoStatue active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        activeCalicoStatue = null;
        var request = active.Pending.Request;
        var observedEffects = CalicoStatueEffectsCsv(CalicoStatueEffects());
        var verificationReasons = verified
            ? new[]
            {
                "shared_bfs_reached_exact_adjacent_stand",
                "native_MineShaft_checkAction_raised_calico_statue_event",
                "host_seeded_effect_and_rating_receipt_matched_exact_projection",
                "native_effect_specific_side_effect_receipt_verified"
            }
            : reasons.Length == 0 ? new[] { "calico_statue_post_state_mismatch" } : reasons;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "strategy_value_and_executor_calibration",
            PrimitiveKind = "activate_calico_statue",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verificationReasons,
            RequestedEffect = CalicoStatueRequestedEffect(request),
            ObservedEffect = CalicoStatueObservedEffect(active.Mine) +
                ";native_handled=" + active.NativeHandled.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : verificationReasons,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "mining.calico_statue.is_activated", Before = "false", After = (active.Mine.recentlyActivatedCalicoStatue.Value == active.Target).ToString().ToLowerInvariant() },
                new SimulatedFactChange { Path = "team.calico_egg_skull_cavern_rating", Before = active.RatingBefore.ToString(), After = Game1.player.team.calicoEggSkullCavernRating.Value.ToString() },
                new SimulatedFactChange { Path = "team.calico_statue_effects", Before = active.EffectsBefore, After = observedEffects }
            }
        });
    }

    private static TrainingExecutionResult CalicoStatueBlocked(TrainingExecutionRequest request, params string[] reasons) =>
        BlockedWithPrimitive(request, "activate_calico_statue", CalicoStatueRequestedEffect(request),
            Game1.currentLocation is MineShaft mine ? CalicoStatueObservedEffect(mine) : "location_not_mineshaft", reasons);

    private static string CalicoStatueRequestedEffect(TrainingExecutionRequest request) =>
        "team.calico_egg_skull_cavern_rating=" + request.CalicoStatueExpectedRatingAfter +
        ";calico_statue_effect_id=" + request.CalicoStatueAcceptedEffectId +
        ";effects_after=" + request.CalicoStatueExpectedEffectsAfterCsv;

    private static string CalicoStatueObservedEffect(MineShaft mine) =>
        "mine_level=" + mine.mineLevel +
        ";target=" + mine.calicoStatueSpot.Value.X + "," + mine.calicoStatueSpot.Value.Y +
        ";activated=" + mine.recentlyActivatedCalicoStatue.Value.X + "," + mine.recentlyActivatedCalicoStatue.Value.Y +
        ";total=" + MineShaft.totalCalicoStatuesActivatedToday +
        ";rating=" + Game1.player.team.calicoEggSkullCavernRating.Value +
        ";effects=" + CalicoStatueEffectsCsv(CalicoStatueEffects()) +
        ";eggs=" + CountCalicoStatueEggs(mine);

    private static Dictionary<int, int> CalicoStatueEffects() =>
        Game1.player.team.calicoStatueEffects.Pairs.ToDictionary(pair => pair.Key, pair => pair.Value);

    private static string CalicoStatueEffectsCsv(IReadOnlyDictionary<int, int> effects) =>
        string.Join(",", effects.OrderBy(pair => pair.Key).Select(pair => pair.Key + ":" + pair.Value));

    private static string CalicoStatueEffectsAfterCsv(IReadOnlyDictionary<int, int> effects, int effectId)
    {
        var expected = new Dictionary<int, int>(effects);
        expected[effectId] = expected.TryGetValue(effectId, out var count) ? count + 1 : 1;
        return CalicoStatueEffectsCsv(expected);
    }

    private static int CountCalicoStatueEggs(MineShaft mine) =>
        Game1.player.Items.CountId("(O)CalicoEgg") + mine.debris.Sum(debris =>
            debris.item?.QualifiedItemId == "(O)CalicoEgg" ? debris.item.Stack : 0);

    private static bool Approximately(double left, double right) => Math.Abs(left - right) <= 0.0001d;
}
