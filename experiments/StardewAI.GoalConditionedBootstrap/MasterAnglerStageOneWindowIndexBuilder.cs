using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

public static class MasterAnglerStageOneWindowIndexBuilder
{
    private static readonly string[] Seasons = { "spring", "summer", "fall", "winter" };

    public static MasterAnglerStageOneWindowIndex Build(string catalogPath, int deadlineYear)
    {
        var fullPath = Path.GetFullPath(catalogPath);
        var catalog = JsonSerializer.Deserialize<MasterAnglerOpportunityCatalogReport>(
            File.ReadAllText(fullPath),
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidDataException("Master Angler opportunity catalog is null.");
        Require(catalog.Status == "complete" && catalog.SourceInventoryComplete &&
                catalog.StaticCalendarConstraintComplete,
            "Master Angler opportunity catalog is not complete.");
        Require(catalog.NativeDenominatorCount == 72 && catalog.Species.Length == 72,
            "Master Angler opportunity catalog does not contain the exact native denominator.");
        Require(deadlineYear >= 2, "Stage 1 deadline year must be at least two.");

        var deadlineTotalDay = TotalDay(deadlineYear, "spring", 1);
        var species = catalog.Species
            .OrderBy(value => value.QualifiedItemId, StringComparer.Ordinal)
            .Select(value => BuildSpecies(value, deadlineYear, deadlineTotalDay))
            .ToArray();
        var unresolved = species
            .Where(value => value.Windows.Length == 0)
            .Select(value => value.QualifiedItemId)
            .ToArray();
        Require(unresolved.Length == 0,
            "Stage 1 Master Angler windows are missing: " + string.Join(",", unresolved));

        return new MasterAnglerStageOneWindowIndex
        {
            Status = "complete_static_windows_dynamic_execution_pending",
            GoalId = catalog.GoalId,
            GameVersion = catalog.GameVersion,
            CatalogPath = fullPath,
            CatalogSha256 = ContentInventoryVerifier.HashFile(fullPath),
            DeadlineYear = deadlineYear,
            DeadlineSeason = "spring",
            DeadlineDayOfMonth = 1,
            DeadlineTotalDayExclusive = deadlineTotalDay,
            NativeDenominatorCount = species.Length,
            StaticWindowCoverageComplete = true,
            TrainingLabelEligible = false,
            Species = species,
            UnresolvedSpeciesIds = unresolved
        };
    }

    private static MasterAnglerStageOneSpeciesWindow BuildSpecies(
        MasterAnglerSpeciesOpportunity species,
        int deadlineYear,
        int deadlineTotalDay)
    {
        var windows = new List<AuthoritativeCalendarSourceWindow>();
        foreach (var rule in species.LocationRules)
        {
            for (var year = Math.Max(1, rule.Calendar.MinimumYear); year < deadlineYear; year++)
            {
                if (rule.Calendar.MaximumYear.HasValue && year > rule.Calendar.MaximumYear.Value)
                    continue;
                foreach (var season in rule.Calendar.Seasons)
                {
                    var first = TotalDay(year, season, 1);
                    var last = Math.Min(TotalDay(year, season, 28), deadlineTotalDay - 1);
                    if (first >= deadlineTotalDay || last < first)
                        continue;
                    windows.Add(new AuthoritativeCalendarSourceWindow
                    {
                        SourceKind = "location_rule",
                        SourceKey = $"{rule.LocationId}:{rule.RuleIndex}",
                        LocationId = rule.LocationId,
                        RuleId = rule.RuleId,
                        Year = year,
                        Season = season,
                        FirstTotalDay = first,
                        LastTotalDay = last,
                        TimeWindows = rule.Calendar.TimeWindows,
                        WeatherModes = rule.Calendar.WeatherModes,
                        DynamicConditions = rule.Calendar.DynamicConditions,
                        MinimumFishingLevel = rule.IgnoreFishDataRequirements
                            ? rule.MinimumFishingLevel
                            : Math.Max(rule.MinimumFishingLevel, species.FishData.MinimumFishingLevel ?? 0),
                        RequireMagicBait = rule.RequireMagicBait,
                        RequiresLocationAccessEvidence = true,
                        RequiresRouteAndFishableTileEvidence = true,
                        RequiresExistingLiveCandidateMatch = true,
                        StochasticOutcome = true
                    });
                }
            }
        }

        foreach (var mine in species.MineOverrides)
        {
            windows.Add(new AuthoritativeCalendarSourceWindow
            {
                SourceKind = "mine_override",
                SourceKey = "MineShaft.getFish:area:" + mine.MineArea,
                LocationId = "UndergroundMine",
                MineArea = mine.MineArea,
                FirstTotalDay = 0,
                LastTotalDay = deadlineTotalDay - 1,
                TimeWindows = new[] { new MasterAnglerTimeWindow { StartTime = 600, EndTime = 2600 } },
                WeatherModes = new[] { "sun", "rain", "storm", "green_rain" },
                MinimumFishingLevel = 0,
                TrainingRodAllowed = false,
                RequiresLocationAccessEvidence = true,
                RequiresRouteAndFishableTileEvidence = true,
                RequiresExistingLiveCandidateMatch = true,
                StochasticOutcome = true
            });
        }

        if (species.AcquisitionClass == "trap_crab_pot")
        {
            windows.Add(new AuthoritativeCalendarSourceWindow
            {
                SourceKind = "crab_pot",
                SourceKey = "CrabPot.DayUpdate:" + species.TrapWaterType,
                WaterType = species.TrapWaterType,
                FirstTotalDay = 1,
                LastTotalDay = deadlineTotalDay - 1,
                TimeWindows = new[] { new MasterAnglerTimeWindow { StartTime = 600, EndTime = 2600 } },
                WeatherModes = new[] { "sun", "rain", "storm", "green_rain" },
                MinimumFishingLevel = 0,
                RequiresTrapInfrastructure = true,
                RequiresLocationAccessEvidence = true,
                RequiresRouteAndFishableTileEvidence = true,
                RequiresExistingLiveCandidateMatch = true,
                StochasticOutcome = true
            });
        }

        var ordered = windows
            .OrderBy(value => value.LastTotalDay)
            .ThenBy(value => value.FirstTotalDay)
            .ThenBy(value => value.SourceKey, StringComparer.Ordinal)
            .ToArray();
        return new MasterAnglerStageOneSpeciesWindow
        {
            QualifiedItemId = species.QualifiedItemId,
            DisplayName = species.DisplayName,
            AcquisitionClass = species.AcquisitionClass,
            EarliestStaticTotalDay = ordered.Length > 0
                ? ordered.Min(value => (int?)value.FirstTotalDay)
                : null,
            LatestStaticTotalDay = ordered.Length > 0
                ? ordered.Max(value => (int?)value.LastTotalDay)
                : null,
            HasDynamicConditions = ordered.Any(value => value.DynamicConditions.Length > 0),
            Windows = ordered
        };
    }

    private static int TotalDay(int year, string season, int dayOfMonth)
    {
        var seasonIndex = Array.FindIndex(Seasons, value =>
            string.Equals(value, season, StringComparison.OrdinalIgnoreCase));
        Require(seasonIndex >= 0, "Unknown season in Master Angler calendar: " + season);
        Require(dayOfMonth is >= 1 and <= 28, "Invalid Stardew day of month: " + dayOfMonth);
        return (year - 1) * 112 + seasonIndex * 28 + dayOfMonth - 1;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
