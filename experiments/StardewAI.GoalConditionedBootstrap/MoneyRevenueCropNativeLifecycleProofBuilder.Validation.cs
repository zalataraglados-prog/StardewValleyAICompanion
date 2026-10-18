using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCropNativeLifecycleProofBuilder
{
    private static void ValidateTransitionChain(
        LoadedTransition[] transitions,
        SnapshotEnvelope source,
        MoneyRevenueCropPlantingLayoutProof layout,
        MoneyRevenueCropProductionCommitmentPlan commitment,
        CropCatalogRow catalog)
    {
        var sourceCalendar = ReadCalendar(source);
        var firstDay = commitment.PlantingTotalDay;
        var lastDay = commitment.HarvestWaves.Max(value =>
            value.HarvestTotalDay);
        Require(transitions.Select(value => value.QueuePath)
                    .Distinct(PathComparer).Count() == transitions.Length &&
                transitions.Select(value => value.ReceiptPath)
                    .Distinct(PathComparer).Count() == transitions.Length,
            "Money Revenue crop lifecycle queue or receipt paths are duplicated.");
        Require(transitions[0].Before.StateHash ==
                    layout.PlantingStateHash,
            "Money Revenue crop lifecycle does not begin at the planting layout snapshot.");

        for (var index = 0; index < transitions.Length; index++)
        {
            var transition = transitions[index];
            ValidateSnapshotIdentity(
                transition.Before,
                source,
                sourceCalendar);
            ValidateSnapshotIdentity(
                transition.After,
                source,
                sourceCalendar);
            Require(index == 0 ||
                    transitions[index - 1].After.StateHash ==
                        transition.Before.StateHash &&
                    transitions[index - 1].After.GameTick ==
                        transition.Before.GameTick,
                "Money Revenue crop lifecycle state-hash chain is discontinuous at transition " +
                index + ".");
            var receiptReasons = QueueExecutionReceiptValidator.Validate(
                transition.Queue,
                transition.Before,
                transition.Receipt,
                transition.After,
                transition.Request.RunId,
                transition.Request.ExecutorVersion,
                transition.Request.SelectedCandidateId,
                transition.Before.StateHash,
                requireTeacherPreferenceStateRebound: false);
            Require(receiptReasons.Length == 0,
                "Money Revenue crop lifecycle queue receipt was rejected: " +
                string.Join(",", receiptReasons));
        }

        var cursor = 0;
        var layoutTiles = layout.Tiles
            .Select(value => (value.TileX, value.TileY))
            .ToHashSet();
        for (var day = firstDay; day <= lastDay; day++)
        {
            Require(cursor < transitions.Length &&
                    transitions[cursor].Request.TotalDay == day,
                "Money Revenue crop lifecycle is missing day " + day + ".");
            var dayStart = transitions[cursor].Before;
            var dayActions = new List<LoadedTransition>();
            while (cursor < transitions.Length &&
                transitions[cursor].Request.TotalDay == day &&
                transitions[cursor].Request.TransitionKind == CropActionsKind)
            {
                dayActions.Add(transitions[cursor++]);
            }
            ValidateDayActions(
                day,
                dayActions,
                dayStart,
                layout,
                commitment,
                catalog,
                layoutTiles);

            if (day < lastDay)
            {
                Require(cursor < transitions.Length &&
                        transitions[cursor].Request.TotalDay == day &&
                        transitions[cursor].Request.TransitionKind ==
                            DayRolloverKind,
                    "Money Revenue crop lifecycle is missing the native day rollover on day " +
                    day + ".");
                ValidateDayRollover(
                    transitions[cursor++],
                    layout,
                    commitment,
                    catalog);
            }
        }
        Require(cursor == transitions.Length,
            "Money Revenue crop lifecycle contains transitions outside the exact day denominator.");
    }

    private static void ValidateDayActions(
        int totalDay,
        List<LoadedTransition> transitions,
        SnapshotEnvelope dayStart,
        MoneyRevenueCropPlantingLayoutProof layout,
        MoneyRevenueCropProductionCommitmentPlan commitment,
        CropCatalogRow catalog,
        HashSet<(int X, int Y)> layoutTiles)
    {
        var actual = new List<(string OptionId, int X, int Y)>();
        var previousRank = -1;
        foreach (var transition in transitions)
        {
            var beforeCalendar = ReadCalendar(transition.Before);
            var afterCalendar = ReadCalendar(transition.After);
            Require(beforeCalendar.TotalDay == totalDay &&
                    afterCalendar.TotalDay == totalDay,
                "Money Revenue crop action crossed a day boundary.");
            var items = transition.Queue.Items ?? Array.Empty<ActionQueueItem>();
            var optionIds = items.Select(value => value.OptionId)
                .Distinct(StringComparer.Ordinal).ToArray();
            Require(optionIds.Length == 1 &&
                    optionIds[0] is PlantOption or WaterOption or HarvestOption,
                "Money Revenue crop action queue is not homogeneous.");
            var rank = ActionRank(optionIds[0]);
            Require(rank >= previousRank,
                "Money Revenue crop action queues are out of native lifecycle order.");
            previousRank = rank;
            var batchTiles = items.Select(item =>
                ReadTarget(item, layout.LocationId, transition.Before)).ToArray();
            if (optionIds[0] == PlantOption)
            {
                Require(items.All(item => NormalizeObjectId(
                        UniqueParameter(
                            item.NormalizedCommand?.Parameters ??
                                Array.Empty<SmallModelActionParameter>(),
                            "seed_id")) == commitment.SeedQualifiedItemId),
                    "Money Revenue crop planting queue seed identity drifted.");
                var beforeSeeds = SeedQuantity(
                    transition.Before,
                    commitment.SeedQualifiedItemId);
                var afterSeeds = SeedQuantity(
                    transition.After,
                    commitment.SeedQualifiedItemId);
                Require(beforeSeeds >= batchTiles.Length &&
                        afterSeeds == beforeSeeds - batchTiles.Length,
                    "Money Revenue native planting queue did not consume its exact seed count.");
            }
            Require(batchTiles.Distinct().Count() == batchTiles.Length &&
                    batchTiles.All(layoutTiles.Contains),
                "Money Revenue crop action queue contains duplicate or foreign tiles.");
            foreach (var tile in batchTiles)
            {
                ValidateCropActionTransition(
                    optionIds[0],
                    tile,
                    transition.Before,
                    transition.After,
                    layout,
                    commitment,
                    catalog);
                actual.Add((optionIds[0], tile.X, tile.Y));
            }
        }

        Require(actual.Distinct().Count() == actual.Count,
            "Money Revenue crop lifecycle repeats an action on the same tile and day.");
        Require(actual.Where(value => value.OptionId == WaterOption)
                    .All(_ => commitment.RequiredWateringTotalDays.Contains(
                        totalDay)),
            "Money Revenue crop lifecycle watered outside the obligation schedule.");
        var expected = new List<(string OptionId, int X, int Y)>();
        foreach (var tile in layoutTiles.OrderBy(value => value.Y)
                     .ThenBy(value => value.X))
        {
            if (totalDay == commitment.PlantingTotalDay)
                expected.Add((PlantOption, tile.X, tile.Y));
            if (commitment.HarvestWaves.Any(value =>
                    value.HarvestTotalDay == totalDay))
                expected.Add((HarvestOption, tile.X, tile.Y));
        }
        Require(actual.Where(value => value.OptionId != WaterOption)
                    .OrderBy(value => value.OptionId, StringComparer.Ordinal)
                    .ThenBy(value => value.Y).ThenBy(value => value.X)
                    .SequenceEqual(expected.OrderBy(
                            value => value.OptionId,
                            StringComparer.Ordinal)
                        .ThenBy(value => value.Y).ThenBy(value => value.X)),
            "Money Revenue crop lifecycle day " + totalDay +
            " does not exactly cover its scheduled tile actions.");

        var final = transitions.Count > 0
            ? transitions[^1].After
            : dayStart;
        foreach (var tile in layoutTiles)
        {
            var crop = ReadCrop(final, layout.LocationId, tile.X, tile.Y);
            var harvested = commitment.HarvestWaves.Any(value =>
                value.HarvestTotalDay == totalDay);
            if (harvested && !catalog.RegrowDays.HasValue)
            {
                Require(crop is null,
                    "A one-shot crop remained after native harvest.");
                continue;
            }
            Require(crop is not null &&
                    CropIdentityMatches(crop, commitment) &&
                    !crop.Dead,
                "Money Revenue crop lifecycle lost a committed crop tile.");
            var verifiedCrop = crop ?? throw new InvalidDataException(
                "Money Revenue crop lifecycle lost a committed crop tile.");
            if (commitment.RequiredWateringTotalDays.Contains(totalDay))
            {
                Require(verifiedCrop.Watered,
                    "Money Revenue crop lifecycle left a required crop tile unwatered.");
            }
            if (harvested)
            {
                Require(!verifiedCrop.ReadyForHarvest &&
                        verifiedCrop.FullyGrown &&
                        verifiedCrop.DayOfCurrentPhase == catalog.RegrowDays,
                    "Money Revenue regrow crop did not enter its exact post-harvest countdown.");
            }
        }
    }

    private static void ValidateCropActionTransition(
        string optionId,
        (int X, int Y) tile,
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        MoneyRevenueCropPlantingLayoutProof layout,
        MoneyRevenueCropProductionCommitmentPlan commitment,
        CropCatalogRow catalog)
    {
        var beforeCrop = ReadCrop(before, layout.LocationId, tile.X, tile.Y);
        var afterCrop = ReadCrop(after, layout.LocationId, tile.X, tile.Y);
        if (optionId == PlantOption)
        {
            Require(beforeCrop is null && afterCrop is not null &&
                    CropIdentityMatches(afterCrop, commitment) &&
                    !afterCrop.Dead && !afterCrop.ReadyForHarvest &&
                    afterCrop.CurrentPhase == 0 &&
                    afterCrop.DayOfCurrentPhase == 0 &&
                    afterCrop.PhaseCount == afterCrop.PhaseDays.Length &&
                    afterCrop.PhaseDays.Where(value => value < 99999).Sum() ==
                        commitment.BaseGrowDays,
                "Money Revenue native planting transition is not exact.");
            return;
        }
        Require(beforeCrop is not null &&
                CropIdentityMatches(beforeCrop, commitment) &&
                !beforeCrop.Dead,
            "Money Revenue crop action target identity drifted.");
        var verifiedBefore = beforeCrop ?? throw new InvalidDataException(
            "Money Revenue crop action target identity drifted.");
        if (optionId == HarvestOption && !catalog.RegrowDays.HasValue)
        {
            Require(verifiedBefore.ReadyForHarvest && afterCrop is null,
                "Money Revenue native one-shot harvest transition is not exact.");
            return;
        }
        Require(afterCrop is not null &&
                CropIdentityMatches(afterCrop, commitment) &&
                !afterCrop.Dead,
            "Money Revenue crop action target identity drifted.");
        var verifiedAfter = afterCrop ?? throw new InvalidDataException(
            "Money Revenue crop action target identity drifted.");
        if (optionId == WaterOption)
        {
            Require(verifiedAfter.Watered &&
                    SameGrowthState(verifiedBefore, verifiedAfter),
                "Money Revenue native watering transition is not exact.");
            return;
        }
        Require(verifiedBefore.ReadyForHarvest,
            "Money Revenue crop harvest was not performed on a ready crop.");
        if (catalog.RegrowDays.HasValue)
        {
            Require(verifiedAfter.FullyGrown &&
                    !verifiedAfter.ReadyForHarvest &&
                    verifiedAfter.DayOfCurrentPhase == catalog.RegrowDays &&
                    verifiedAfter.CurrentPhase == verifiedBefore.CurrentPhase,
                "Money Revenue native regrow harvest transition is not exact.");
        }
    }

    private static void ValidateDayRollover(
        LoadedTransition transition,
        MoneyRevenueCropPlantingLayoutProof layout,
        MoneyRevenueCropProductionCommitmentPlan commitment,
        CropCatalogRow catalog)
    {
        var beforeCalendar = ReadCalendar(transition.Before);
        var afterCalendar = ReadCalendar(transition.After);
        Require(beforeCalendar.TotalDay == transition.Request.TotalDay &&
                afterCalendar.TotalDay == beforeCalendar.TotalDay + 1 &&
                beforeCalendar.WorldDateOffset == afterCalendar.WorldDateOffset &&
                NativeDaySettlementSupport.IsSingleNativeSleepQueue(
                    transition.Queue),
            "Money Revenue crop lifecycle day rollover is not one native sleep transition.");
        foreach (var tile in layout.Tiles)
        {
            var before = ReadCrop(
                transition.Before,
                layout.LocationId,
                tile.TileX,
                tile.TileY);
            var after = ReadCrop(
                transition.After,
                layout.LocationId,
                tile.TileX,
                tile.TileY);
            Require(before is not null && after is not null &&
                    CropIdentityMatches(before, commitment) &&
                    CropIdentityMatches(after, commitment),
                "Money Revenue crop disappeared across native sleep.");
            var verifiedBefore = before ?? throw new InvalidDataException(
                "Money Revenue crop disappeared across native sleep.");
            var verifiedAfter = after ?? throw new InvalidDataException(
                "Money Revenue crop disappeared across native sleep.");
            var projected = ProjectNativeDay(
                verifiedBefore,
                catalog.NeedsWatering);
            Require(SameNativeDayState(projected, verifiedAfter),
                "Money Revenue crop state did not follow Crop.newDay on tile " +
                tile.TileX + "," + tile.TileY + ".");
        }
    }

    private static CropState ProjectNativeDay(
        CropState before,
        bool needsWatering)
    {
        var phase = before.CurrentPhase;
        var day = before.DayOfCurrentPhase;
        var shouldGrow = before.Watered || !needsWatering;
        if (shouldGrow)
        {
            if (!before.FullyGrown)
            {
                day = Math.Min(
                    day + 1,
                    before.PhaseDays[Math.Min(
                        before.PhaseDays.Length - 1,
                        phase)]);
            }
            else
            {
                day--;
            }
            if (day >= before.PhaseDays[Math.Min(
                    before.PhaseDays.Length - 1,
                    phase)] &&
                phase < before.PhaseDays.Length - 1)
            {
                phase++;
                day = 0;
            }
            while (phase < before.PhaseDays.Length - 1 &&
                before.PhaseDays[phase] <= 0)
            {
                phase++;
            }
        }
        var ready = !before.Dead &&
            (!before.FullyGrown || day <= 0) &&
            phase >= before.PhaseDays.Length - 1;
        var remaining = DaysUntilHarvest(
            before.PhaseDays,
            phase,
            day,
            before.FullyGrown,
            ready);
        return before with
        {
            CurrentPhase = phase,
            DayOfCurrentPhase = day,
            ReadyForHarvest = ready,
            DaysUntilNextHarvest = remaining
        };
    }

    private static int DaysUntilHarvest(
        int[] phaseDays,
        int phase,
        int day,
        bool fullyGrown,
        bool ready)
    {
        if (ready)
            return 0;
        if (fullyGrown)
            return Math.Max(0, day);
        var remaining = 0;
        for (var index = phase; index < phaseDays.Length - 1; index++)
        {
            var duration = phaseDays[index];
            if (duration <= 0 || duration >= 99999)
                continue;
            remaining += index == phase
                ? Math.Max(0, duration - day)
                : duration;
        }
        return remaining;
    }

    private static bool SameNativeDayState(CropState expected, CropState actual) =>
        expected.SeedId == actual.SeedId &&
        expected.HarvestQualifiedItemId == actual.HarvestQualifiedItemId &&
        expected.PhaseDays.SequenceEqual(actual.PhaseDays) &&
        expected.CurrentPhase == actual.CurrentPhase &&
        expected.PhaseCount == actual.PhaseCount &&
        expected.DayOfCurrentPhase == actual.DayOfCurrentPhase &&
        expected.Dead == actual.Dead &&
        expected.FullyGrown == actual.FullyGrown &&
        expected.ReadyForHarvest == actual.ReadyForHarvest &&
        expected.DaysUntilNextHarvest == actual.DaysUntilNextHarvest &&
        expected.RegrowDays == actual.RegrowDays;

    private static bool SameGrowthState(CropState left, CropState right) =>
        left.SeedId == right.SeedId &&
        left.PhaseDays.SequenceEqual(right.PhaseDays) &&
        left.CurrentPhase == right.CurrentPhase &&
        left.PhaseCount == right.PhaseCount &&
        left.DayOfCurrentPhase == right.DayOfCurrentPhase &&
        left.Dead == right.Dead &&
        left.FullyGrown == right.FullyGrown &&
        left.ReadyForHarvest == right.ReadyForHarvest &&
        left.DaysUntilNextHarvest == right.DaysUntilNextHarvest &&
        left.RegrowDays == right.RegrowDays;

    private static bool CropIdentityMatches(
        CropState crop,
        MoneyRevenueCropProductionCommitmentPlan commitment) =>
        NormalizeObjectId(crop.SeedId) == commitment.SeedQualifiedItemId &&
        crop.HarvestQualifiedItemId == commitment.HarvestItemQualifiedId;

    private static int ActionRank(string optionId) => optionId switch
    {
        PlantOption => 0,
        WaterOption => 1,
        HarvestOption => 2,
        _ => int.MaxValue
    };
}
