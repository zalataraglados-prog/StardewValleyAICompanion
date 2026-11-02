using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecutePrepareFullShipmentTerminalSleep(
        TrainingExecutionRequest request)
    {
        pendingEndOfNightNegativeProfile = string.Empty;
        pendingEndOfNightNegativeProfileRunId = string.Empty;
        var ordinaryLevelCount =
            request.DebugEndOfNightOrdinaryLevelCount ?? 0;
        var farmEventFixture =
            request.DebugEndOfNightFarmEvent ?? string.Empty;
        var negativeProfile =
            request.DebugEndOfNightNegativeProfile ?? string.Empty;
        if (ordinaryLevelCount is < 0 or > 2)
        {
            return BlockedWithPrimitive(
                request,
                "debug_prepare_full_shipment_terminal_sleep",
                "player.native_sleep_path=ready;pending_ordinary_levels=0..2",
                SleepObservedEffect(),
                "debug_end_of_night_ordinary_level_count_0_2_required");
        }
        if (!string.IsNullOrEmpty(farmEventFixture) &&
            !string.Equals(
                farmEventFixture,
                "day_31_earthquake",
                StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(
                request,
                "debug_prepare_full_shipment_terminal_sleep",
                "player.native_sleep_path=ready;farm_event=day_31_earthquake",
                SleepObservedEffect(),
                "debug_end_of_night_farm_event_unsupported");
        }
        if (!string.IsNullOrEmpty(negativeProfile) &&
            !string.Equals(
                negativeProfile,
                EndOfNightUnknownMenuTransitionTimeoutProfile,
                StringComparison.Ordinal) &&
            !string.Equals(
                negativeProfile,
                EndOfNightOverallTimeoutProfile,
                StringComparison.Ordinal))
        {
            return BlockedWithPrimitive(
                request,
                "debug_prepare_full_shipment_terminal_sleep",
                "player.native_sleep_path=ready;negative_profile=known",
                SleepObservedEffect(),
                "debug_end_of_night_negative_profile_unsupported");
        }
        if (!string.IsNullOrEmpty(negativeProfile) &&
            (ordinaryLevelCount > 0 ||
             !string.IsNullOrEmpty(farmEventFixture) ||
             request.ProfessionChoiceId.HasValue))
        {
            return BlockedWithPrimitive(
                request,
                "debug_prepare_full_shipment_terminal_sleep",
                "player.native_sleep_path=ready;negative_profile=isolated",
                SleepObservedEffect(),
                "debug_end_of_night_negative_profile_requires_isolation");
        }
        if (request.ProfessionChoiceId is < 0 or > 29)
        {
            return BlockedWithPrimitive(
                request,
                "debug_prepare_full_shipment_terminal_sleep",
                "player.native_sleep_path=ready;pending_profession_level=ready",
                SleepObservedEffect(),
                "profession_choice_id_0_29_required");
        }
        if (string.Equals(
                farmEventFixture,
                "day_31_earthquake",
                StringComparison.Ordinal) &&
            HasDay31WeddingConflict())
        {
            return BlockedWithPrimitive(
                request,
                "debug_prepare_full_shipment_terminal_sleep",
                "player.native_sleep_path=ready;farm_event=day_31_earthquake",
                SleepObservedEffect(),
                "day_31_earthquake_wedding_conflict");
        }
        var result = ExecutePrepareNativeSleepFixture(
            request,
            "debug_prepare_full_shipment_terminal_sleep",
            "full_shipment_terminal_native_sleep_path_ready");
        if (result.Status != "applied")
        {
            return result;
        }

        int? skill = null;
        int? level = null;
        if (request.ProfessionChoiceId.HasValue || ordinaryLevelCount > 0)
        {
            Game1.player.newLevels.Clear();
        }
        if (request.ProfessionChoiceId.HasValue)
        {
            if (!TryPreparePendingProfessionLevel(
                    request.ProfessionChoiceId.Value,
                    out var preparedSkill,
                    out var preparedLevel))
            {
                throw new InvalidOperationException(
                    "Validated profession choice could not be prepared.");
            }

            skill = preparedSkill;
            level = preparedLevel;
        }

        var ordinaryLevels = new[]
        {
            Farmer.foragingSkill,
            Farmer.miningSkill,
            Farmer.fishingSkill,
            Farmer.combatSkill,
            Farmer.farmingSkill
        }
            .Where(candidateSkill => candidateSkill != skill)
            .Take(ordinaryLevelCount)
            .Select(candidateSkill =>
                new Microsoft.Xna.Framework.Point(candidateSkill, 2))
            .ToArray();
        foreach (var pendingLevel in ordinaryLevels)
        {
            PreparePendingSkillLevel(
                Game1.player,
                pendingLevel.X,
                pendingLevel.Y);
        }
        if (string.IsNullOrEmpty(farmEventFixture))
        {
            // Formal Stage-1 settlement evidence must not inherit an
            // arbitrary source-save year. Year 2 Fall 1 is a plausible
            // pre-Grandpa boundary and remains below the day-224 deadline.
            Game1.year = 2;
            Game1.currentSeason = "fall";
            Game1.dayOfMonth = 1;
            Game1.stats.DaysPlayed = 169;
            result.PrimitiveVerificationReasons =
                result.PrimitiveVerificationReasons
                    .Append("native_stage_one_settlement_date_prepared")
                    .ToArray();
            result.RequestedEffect += ";settlement_date=2:fall:1";
            result.ObservedEffect +=
                ";date_before_sleep=2:fall:1" +
                ";stats_days_played_before_sleep=169" +
                ";total_days_before_sleep=" + Game1.Date.TotalDays;
        }
        if (string.Equals(
                farmEventFixture,
                "day_31_earthquake",
                StringComparison.Ordinal))
        {
            // Vanilla increments DaysPlayed before Utility.pickFarmEvent().
            // Year 1 Summer 2 is TotalDays 29 and DaysPlayed 30; the locked
            // 1.6.15 new-day path advances both before selecting the event.
            Game1.year = 1;
            Game1.currentSeason = "summer";
            Game1.dayOfMonth = 2;
            Game1.stats.DaysPlayed = 30;
        }
        if (result.Status == "applied" && skill.HasValue && level.HasValue)
        {
            result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
                .Append("native_end_of_night_profession_level_queued")
                .ToArray();
            result.RequestedEffect +=
                ";pending_profession_skill=" + skill.Value +
                ";pending_profession_level=" + level.Value;
            result.ObservedEffect +=
                ";pending_profession_level_present=" +
                Game1.player.newLevels.Contains(
                    new Microsoft.Xna.Framework.Point(
                        skill.Value,
                        level.Value))
                    .ToString()
                    .ToLowerInvariant();
        }
        if (result.Status == "applied" && ordinaryLevels.Length > 0)
        {
            result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
                .Append("native_end_of_night_ordinary_levels_queued:" +
                    ordinaryLevels.Length)
                .ToArray();
            result.RequestedEffect += ";pending_ordinary_levels=" +
                string.Join(",", ordinaryLevels.Select(point =>
                    point.X + ":" + point.Y));
            result.ObservedEffect += ";pending_ordinary_levels_present=" +
                ordinaryLevels.All(Game1.player.newLevels.Contains)
                    .ToString()
                    .ToLowerInvariant() +
                ";pending_ordinary_levels_state_coherent=" +
                ordinaryLevels.All(point =>
                    Game1.player.GetUnmodifiedSkillLevel(point.X) == point.Y &&
                    Game1.player.experiencePoints[point.X] ==
                        Farmer.getBaseExperienceForLevel(point.Y))
                    .ToString()
                    .ToLowerInvariant();
        }
        if (result.Status == "applied" &&
            string.Equals(
                farmEventFixture,
                "day_31_earthquake",
                StringComparison.Ordinal))
        {
            result.PrimitiveVerificationReasons = result.PrimitiveVerificationReasons
                .Append("native_day_31_earthquake_farm_event_scheduled")
                .ToArray();
            result.RequestedEffect +=
                ";farm_event_source=Utility.pickFarmEvent_day_31";
            result.ObservedEffect +=
                ";stats_days_played_before_sleep=" + Game1.stats.DaysPlayed +
                ";date_before_sleep=" + Game1.year + ":" +
                Game1.currentSeason + ":" + Game1.dayOfMonth +
                ";total_days_before_sleep=" + Game1.Date.TotalDays;
        }
        if (result.Status == "applied")
        {
            pendingEndOfNightNegativeProfile = negativeProfile;
            pendingEndOfNightNegativeProfileRunId =
                string.IsNullOrEmpty(negativeProfile)
                    ? string.Empty
                    : request.RunId;
            if (!string.IsNullOrEmpty(negativeProfile))
            {
                result.PrimitiveVerificationReasons =
                    result.PrimitiveVerificationReasons
                        .Append(
                            "native_end_of_night_negative_profile_armed:" +
                            negativeProfile)
                        .ToArray();
            }
        }
        return result;
    }

    private static bool HasDay31WeddingConflict() =>
        Game1.getOnlineFarmers().Any(farmer =>
        {
            var spouseFriendship = farmer.GetSpouseFriendship();
            return spouseFriendship is not null &&
                spouseFriendship.WeddingDate is not null &&
                ((farmer.isEngaged() &&
                  spouseFriendship.WeddingDate.TotalDays <= 30) ||
                 (spouseFriendship.IsMarried() &&
                  spouseFriendship.WeddingDate.TotalDays == 30));
        });

    private TrainingExecutionResult ExecuteSetupFullShipmentTerminal(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        const string primitive = "debug_setup_full_shipment_terminal";
        const string requested =
            "world_progress.full_shipment_progress=single_missing_item;" +
            "farm.shipping_bins.contents=[];achievement_34=false";
        var farm = Game1.getFarm();
        if (farm is null)
        {
            return BlockedWithPrimitive(
                request,
                primitive,
                requested,
                "farm=missing",
                "full_shipment_fixture_farm_missing");
        }
        if (!request.FullShipmentExpectedEligibleItemCount.HasValue ||
            request.FullShipmentExpectedEligibleItemCount.Value <= 0)
        {
            return BlockedWithPrimitive(
                request,
                primitive,
                requested,
                "eligible_item_count=unresolved",
                "full_shipment_fixture_expected_denominator_missing");
        }
        if (string.IsNullOrWhiteSpace(request.QualifiedItemId))
        {
            return BlockedWithPrimitive(
                request,
                primitive,
                requested,
                "terminal_item=unresolved",
                "full_shipment_fixture_terminal_item_missing");
        }

        var eligible = Game1.objectData
            .Where(pair =>
                pair.Value is not null &&
                pair.Value.Category != -7 &&
                pair.Value.Category != -2 &&
                StardewValley.Object.isPotentialBasicShipped(
                    pair.Key,
                    pair.Value.Category,
                    pair.Value.Type))
            .Select(pair => new
            {
                ItemId = pair.Key,
                QualifiedItemId =
                    ItemRegistry.QualifyItemId(pair.Key) ?? "(O)" + pair.Key
            })
            .OrderBy(pair => pair.ItemId, StringComparer.Ordinal)
            .ToArray();
        if (eligible.Length !=
            request.FullShipmentExpectedEligibleItemCount.Value)
        {
            return BlockedWithPrimitive(
                request,
                primitive,
                requested,
                "eligible_item_count=" + eligible.Length,
                "full_shipment_fixture_native_denominator_drift");
        }

        var terminal = eligible.SingleOrDefault(pair => string.Equals(
            pair.QualifiedItemId,
            request.QualifiedItemId,
            StringComparison.Ordinal));
        if (terminal is null)
        {
            return BlockedWithPrimitive(
                request,
                primitive,
                requested,
                "terminal_item=" + request.QualifiedItemId,
                "full_shipment_fixture_terminal_item_not_native_eligible");
        }
        var completedBin = farm.buildings
            .OfType<StardewValley.Buildings.ShippingBin>()
            .FirstOrDefault(bin => bin.daysOfConstructionLeft.Value <= 0);
        if (completedBin is null)
        {
            return BlockedWithPrimitive(
                request,
                primitive,
                requested,
                "shipping_bin=missing",
                "full_shipment_fixture_completed_bin_missing");
        }

        var startedAt = DateTimeOffset.UtcNow.ToString("O");
        var master = Game1.MasterPlayer;
        Game1.exitActiveMenu();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        foreach (var item in eligible)
        {
            if (string.Equals(
                    item.QualifiedItemId,
                    terminal.QualifiedItemId,
                    StringComparison.Ordinal))
            {
                master.basicShipped.Remove(item.ItemId);
            }
            else
            {
                master.basicShipped[item.ItemId] = 1;
            }
        }
        master.achievements.Remove(34);
        farm.getShippingBin(Game1.player).Clear();
        Game1.player.displayedShippedItems.Clear();
        farm.lastItemShipped = null;
        for (var index = 0; index < Game1.player.Items.Count; index++)
        {
            if (string.Equals(
                    Game1.player.Items[index]?.QualifiedItemId,
                    terminal.QualifiedItemId,
                    StringComparison.Ordinal))
            {
                Game1.player.Items[index] = null;
            }
        }
        var terminalSlot = EnsureInventoryItem(terminal.QualifiedItemId, 1);
        var placement = ExecuteSetupShippingTarget(request);

        var shippedCount = eligible.Count(item =>
            master.basicShipped.TryGetValue(item.ItemId, out var count) &&
            count > 0);
        var terminalCount = master.basicShipped.TryGetValue(
            terminal.ItemId,
            out var existingTerminalCount)
            ? existingTerminalCount
            : 0;
        var terminalInventoryCount = Game1.player.Items
            .Where(item => string.Equals(
                item?.QualifiedItemId,
                terminal.QualifiedItemId,
                StringComparison.Ordinal))
            .Sum(item => item?.Stack ?? 0);
        var verified =
            terminalSlot >= 0 &&
            terminalInventoryCount == 1 &&
            shippedCount == eligible.Length - 1 &&
            terminalCount == 0 &&
            farm.getShippingBin(Game1.player).Count == 0 &&
            Game1.player.displayedShippedItems.Count == 0 &&
            !master.achievements.Contains(34) &&
            placement.Status == "applied" &&
            placement.PrimitiveVerificationStatus == "verified";
        var observed =
            "eligible_item_count=" + eligible.Length +
            ";shipped_eligible_item_count=" + shippedCount +
            ";terminal_qualified_item_id=" + terminal.QualifiedItemId +
            ";terminal_shipped_count=" + terminalCount +
            ";terminal_inventory_slot=" + terminalSlot +
            ";terminal_inventory_count=" + terminalInventoryCount +
            ";shipping_bin_count=" + farm.getShippingBin(Game1.player).Count +
            ";displayed_shipped_item_count=" +
                Game1.player.displayedShippedItems.Count +
            ";achievement_34=" +
                master.achievements.Contains(34).ToString().ToLowerInvariant() +
            ";shipping_endpoint_fixture=" + placement.Status;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = primitive,
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_full_shipment_denominator_matched",
                    "single_terminal_item_missing",
                    "terminal_copy_installed",
                    "shipping_bin_cleared",
                    "displayed_shipped_items_cleared",
                    "achievement_34_cleared"
                }
                : new[] { "full_shipment_terminal_fixture_projection_mismatch" },
            RequestedEffect = requested,
            ObservedEffect = observed,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "world_progress.full_shipment_progress.shipped_eligible_item_count",
                    Before = string.Empty,
                    After = shippedCount.ToString()
                },
                new SimulatedFactChange
                {
                    Path = "world_progress.full_shipment_progress.missing_item_ids",
                    Before = string.Empty,
                    After = terminal.ItemId
                },
                new SimulatedFactChange
                {
                    Path = "player.inventory.slot_index",
                    Before = string.Empty,
                    After = terminalSlot.ToString()
                }
            }
        };
    }
}
