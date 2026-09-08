using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MasterAnglerTargetDateIntentBuilder
{
    public static MasterAnglerTargetDateIntentSet Build(
        string windowIndexPath,
        string snapshotPath,
        string routeTimingCalibrationPath)
    {
        var fullIndexPath = Path.GetFullPath(windowIndexPath);
        var fullSnapshotPath = Path.GetFullPath(snapshotPath);
        var fullTimingPath = Path.GetFullPath(routeTimingCalibrationPath);
        var index = JsonSerializer.Deserialize<MasterAnglerStageOneWindowIndex>(
            File.ReadAllText(fullIndexPath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("Master Angler Stage 1 window index is null.");
        Require(index.SchemaVersion == "master_angler_stage_one_window_index.v1" &&
                index.Status == "complete_static_windows_dynamic_execution_pending" &&
                index.StaticWindowCoverageComplete && index.NativeDenominatorCount == 72 &&
                index.Species.Length == 72 && index.UnresolvedSpeciesIds.Length == 0,
            "Master Angler Stage 1 window index is not complete.");

        using var snapshot = JsonDocument.Parse(File.ReadAllText(fullSnapshotPath));
        var root = snapshot.RootElement;
        var state = RequiredObject(root, "state");
        var currentTotalDay = RequiredFieldInt(state, "time", "total_days");
        var currentTime = RequiredFieldInt(state, "time", "time");
        var currentLocation = RequiredFieldString(state, "player", "location_id");
        var currentTileX = RequiredFieldInt(state, "player", "tile_x");
        var currentTileY = RequiredFieldInt(state, "player", "tile_y");
        var sourceStateHash = RequiredString(root, "state_hash");
        var gameVersion = RequiredString(root, "game_version");
        Require(currentTotalDay >= 0 && currentTotalDay < index.DeadlineTotalDayExclusive,
            "Snapshot is outside the Stage 1 Master Angler deadline horizon.");

        var missing = ReadExactMissingSpecies(state, index.NativeDenominatorCount);
        var fishingLevel = ReadFishingLevel(state);
        var hasMagicBait = AnyRodContextFlag(state, "has_magic_bait");
        var hasNonTrainingRod = AnyRodContextFlag(state, "uses_training_rod", expected: false);
        var routeGraph = RequiredFieldValue(state, "locations", "route_graph");
        var routeDateEvidence = RequiredFieldValue(
            state,
            "locations",
            "social_route_date_evidence");
        var movementTimingContext = RequiredFieldValue(
            state,
            "player",
            "movement_timing_context");
        var timingJson = File.ReadAllText(fullTimingPath);
        var timingHash = ContentInventoryVerifier.HashFile(fullTimingPath);
        var timingLoad = new FutureRouteTimingCalibrationLoader().Load(
            timingJson,
            movementTimingContext,
            gameVersion,
            currentTotalDay);
        Require(
            timingLoad.Status == FutureRouteTimingCalibrationLoadStatus.Loaded &&
            timingLoad.Calibration is not null,
            "Master Angler route timing calibration does not match the current snapshot: " +
            string.Join(",", timingLoad.BlockingReasons));
        var readyCrabPotOutputs = ReadReadyCrabPotOutputs(state);
        var runtimeEligibleLocationRules = ReadRuntimeEligibleLocationRules(state);

        var eligibleWindows = index.Species
            .Where(species => missing.Contains(species.QualifiedItemId))
            .SelectMany(species => species.Windows.Select(window => new
            {
                Species = species,
                Window = window
            }))
            .Where(candidate => candidate.Window.SourceKind is
                "location_rule" or "mine_override" or "crab_pot")
            .Where(candidate => currentTotalDay >= candidate.Window.FirstTotalDay &&
                currentTotalDay <= candidate.Window.LastTotalDay)
            .Where(candidate => candidate.Window.MinimumFishingLevel <= fishingLevel)
            .Where(candidate => !candidate.Window.RequireMagicBait || hasMagicBait)
            .Where(candidate => candidate.Window.TrainingRodAllowed != false || hasNonTrainingRod)
            .Where(candidate => candidate.Window.TimeWindows.Any(value =>
                currentTime < value.EndTime &&
                Math.Max(currentTime, value.StartTime) < value.EndTime))
            .Where(candidate => candidate.Window.DynamicConditions.Length == 0 ||
                (candidate.Window.SourceKind == "location_rule" &&
                 runtimeEligibleLocationRules.Contains(RuntimeLocationRuleKey(
                     candidate.Window.SourceKey,
                     candidate.Species.QualifiedItemId))))
            .Where(candidate => candidate.Window.SourceKind != "crab_pot" ||
                readyCrabPotOutputs.Contains(candidate.Species.QualifiedItemId))
            .Where(candidate => candidate.Window.SourceKind == "crab_pot" ||
                !string.IsNullOrWhiteSpace(candidate.Window.LocationId))
            .ToArray();
        var targetLocations = eligibleWindows
            .Select(candidate => string.Equals(
                    candidate.Window.SourceKind,
                    "crab_pot",
                    StringComparison.Ordinal)
                ? currentLocation
                : candidate.Window.LocationId)
            .Where(location => !string.IsNullOrWhiteSpace(location))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(location => location, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var routeRequests = targetLocations
            .Select(location => new FutureLocationRouteDateEvidenceRequest
            {
                TotalDays = currentTotalDay,
                StartLocation = currentLocation,
                StartTileX = currentTileX,
                StartTileY = currentTileY,
                EarliestDepartureTime = currentTime,
                TargetLocation = location
            })
            .ToArray();
        var routeProductions = new FutureRouteDateEvidenceProducer()
            .ProduceLocationArrivals(
                routeGraph,
                routeDateEvidence,
                routeRequests,
                timingLoad.Calibration);
        var routeByLocation = targetLocations
            .Select((location, index) => new
            {
                Location = location,
                Production = routeProductions[index]
            })
            .ToDictionary(
                value => value.Location,
                value => value.Production,
                StringComparer.OrdinalIgnoreCase);

        var indexHash = ContentInventoryVerifier.HashFile(fullIndexPath);
        var candidates = eligibleWindows
            .Select(candidate => BuildCandidate(
                candidate.Species,
                candidate.Window,
                index,
                fullIndexPath,
                indexHash,
                currentTotalDay,
                currentTime,
                currentLocation,
                routeByLocation,
                fullTimingPath,
                timingHash))
            .Where(candidate => candidate is not null)
            .Cast<MasterAnglerTargetDateIntent>()
            .GroupBy(candidate => string.Join("|", candidate.TargetQualifiedItemId,
                candidate.TargetLocation, candidate.SourceKind, candidate.SourceKey), StringComparer.Ordinal)
            .Select(group => group
                .OrderBy(candidate => candidate.LastCastTimeExclusive)
                .ThenBy(candidate => candidate.RouteEdgeCount)
                .First())
            .OrderBy(candidate => candidate.DeadlineSlackDays)
            .ThenBy(candidate => candidate.LastCastTimeExclusive)
            .ThenBy(candidate => candidate.RouteEdgeCount)
            .ThenBy(candidate => candidate.TargetQualifiedItemId, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.SourceKey, StringComparer.Ordinal)
            .ToArray();

        return new MasterAnglerTargetDateIntentSet
        {
            Status = candidates.Length > 0
                ? "validated_current_date_full_route_intents_runtime_terminal_pending"
                : "no_authoritative_static_rod_intent_available_now",
            SourceSnapshotPath = fullSnapshotPath,
            SourceStateHash = sourceStateHash,
            WindowIndexPath = fullIndexPath,
            WindowIndexSha256 = indexHash,
            RouteTimingCalibrationPath = fullTimingPath,
            RouteTimingCalibrationSha256 = timingHash,
            CurrentTotalDay = currentTotalDay,
            CurrentTime = currentTime,
            CurrentLocationId = currentLocation,
            MissingSpeciesCount = missing.Count,
            CandidateCount = candidates.Length,
            TrainingLabelEligible = false,
            Candidates = candidates,
            BlockingReasons = candidates.Length > 0
                ? new[] { "fresh_native_target_catch_receipt_required_before_training_admission" }
                : new[] { "no_missing_species_has_a_current_full_route_time_feasible_runtime_source" }
        };
    }

    private static MasterAnglerTargetDateIntent? BuildCandidate(
        MasterAnglerStageOneSpeciesWindow species,
        MasterAnglerStageOneSourceWindow window,
        MasterAnglerStageOneWindowIndex index,
        string indexPath,
        string indexHash,
        int currentTotalDay,
        int currentTime,
        string currentLocation,
        IReadOnlyDictionary<string, FutureLocationRouteDateEvidenceProduction>
            routeByLocation,
        string timingCalibrationPath,
        string timingCalibrationSha256)
    {
        var usableTimes = window.TimeWindows
            .Where(value => currentTime < value.EndTime)
            .Select(value => new
            {
                Start = Math.Max(currentTime, value.StartTime),
                End = value.EndTime
            })
            .Where(value => value.Start < value.End)
            .OrderBy(value => value.End)
            .ThenBy(value => value.Start)
            .ToArray();
        if (usableTimes.Length == 0)
            return null;

        var isCrabPot = string.Equals(window.SourceKind, "crab_pot", StringComparison.Ordinal);
        var targetLocation = isCrabPot ? currentLocation : window.LocationId;
        if (!routeByLocation.TryGetValue(
                targetLocation,
                out var route) ||
            route.Status != FutureRouteDateEvidenceProductionStatus.Produced ||
            !route.GuaranteedArrivalByTime.HasValue)
            return null;

        var terminalReserveMinutes = isCrabPot
            ? 0
            : GameClockBudgetPolicy.TicksToGameMinutes(1800);
        usableTimes = usableTimes
            .Where(value => GameClockBudgetPolicy.ClockMinutesBetween(
                    Math.Max(
                        value.Start,
                        route.GuaranteedArrivalByTime.Value),
                    value.End) >= terminalReserveMinutes)
            .ToArray();
        if (usableTimes.Length == 0)
            return null;

        var selectedTime = usableTimes[0];
        var intentId = "master-angler:" + currentTotalDay.ToString(CultureInfo.InvariantCulture) + ":" +
            species.QualifiedItemId + ":" + window.SourceKey;
        var parameters = new[]
        {
            Parameter("master_angler_target_qualified_item_id", species.QualifiedItemId),
            Parameter("master_angler_target_location", targetLocation),
            Parameter("master_angler_source_kind", window.SourceKind),
            Parameter("master_angler_source_key", window.SourceKey),
            Parameter("master_angler_window_first_total_day", window.FirstTotalDay),
            Parameter("master_angler_window_last_total_day", window.LastTotalDay),
            Parameter("master_angler_target_total_day", currentTotalDay),
            Parameter("master_angler_effective_start_time", selectedTime.Start),
            Parameter("master_angler_last_cast_time_exclusive", selectedTime.End),
            Parameter("master_angler_stage_one_deadline_total_day_exclusive", index.DeadlineTotalDayExclusive),
            Parameter("master_angler_window_index_path", indexPath),
            Parameter("master_angler_window_index_sha256", indexHash),
            Parameter("master_angler_validation_status", "authoritative_stage_one_window_match"),
            Parameter("master_angler_runtime_terminal_validation_required", "true"),
            Parameter(
                MasterAnglerRouteTimingValidator.CalibrationPathParameter,
                timingCalibrationPath),
            Parameter(
                MasterAnglerRouteTimingValidator.CalibrationSha256Parameter,
                timingCalibrationSha256),
            Parameter(
                MasterAnglerRouteTimingValidator.ValidationRequiredParameter,
                "true")
        };
        return new MasterAnglerTargetDateIntent
        {
            IntentId = intentId,
            OptionId = isCrabPot ? "fishing.collect_crab_pots" : "fishing.catch_fish",
            TargetQualifiedItemId = species.QualifiedItemId,
            DisplayName = species.DisplayName,
            TargetLocation = targetLocation,
            SourceKind = window.SourceKind,
            SourceKey = window.SourceKey,
            FirstTotalDay = window.FirstTotalDay,
            LastTotalDay = window.LastTotalDay,
            EffectiveStartTime = selectedTime.Start,
            LastCastTimeExclusive = selectedTime.End,
            RouteEdgeCount = route.Path.Length,
            RouteGuaranteedArrivalTime =
                route.GuaranteedArrivalByTime.Value,
            RouteTimingStatus = route.Scope,
            RouteTimingEvidenceId = route.TimingEvidenceId,
            DeadlineSlackDays = window.LastTotalDay - currentTotalDay,
            Parameters = parameters
        };
    }

}
