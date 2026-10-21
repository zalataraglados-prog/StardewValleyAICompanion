using System.Text.Json;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static class MasterAnglerFreshSavePrerequisiteAuditBuilder
{
    private const string RodEquipmentFamily = "rod_and_fishing_equipment_acquisition";
    private const string FishingSkillFamily = "fishing_skill_progression";
    private const string WeatherFamily = "weather_reserve_or_control";
    private const string TrapLifecycleFamily = "crab_pot_lifecycle";
    private const string TrapSupplyFamily = "crab_pot_recipe_material_and_bait_supply";

    private static readonly HashSet<string> RainModes = new(StringComparer.Ordinal)
    {
        "rain", "storm", "green_rain"
    };

    private static readonly Dictionary<string, string> LocationPrerequisites =
        new(StringComparer.Ordinal)
        {
            ["Backwoods"] = "ordinary_world_access",
            ["Beach"] = "ordinary_world_access",
            ["BugLand"] = "mutant_bug_lair_story_access",
            ["Caldera"] = "ginger_island_access",
            ["Desert"] = "desert_bus_access",
            ["Farm_Beach"] = "beach_farm_layout",
            ["Farm_Forest"] = "forest_farm_layout",
            ["fishingGame"] = "festival_instance_access",
            ["Forest"] = "ordinary_world_access",
            ["IslandNorth"] = "ginger_island_access",
            ["IslandSouth"] = "ginger_island_access",
            ["IslandSouthEast"] = "ginger_island_access",
            ["IslandSouthEastCave"] = "ginger_island_access",
            ["IslandWest"] = "ginger_island_access",
            ["Mountain"] = "ordinary_world_access",
            ["Sewer"] = "sewer_rusty_key_access",
            ["Submarine"] = "festival_instance_access",
            ["Temp"] = "festival_instance_access",
            ["Town"] = "ordinary_world_access",
            ["UndergroundMine"] = "mine_access_and_depth",
            ["WitchSwamp"] = "witch_swamp_story_access",
            ["Woods"] = "secret_woods_access"
        };

    public static MasterAnglerFreshSavePrerequisiteAuditReport Build(
        string catalogPath,
        string windowIndexPath,
        string dependencyExpansionPath)
    {
        var catalogFullPath = Path.GetFullPath(catalogPath);
        var windowFullPath = Path.GetFullPath(windowIndexPath);
        var dependencyFullPath = Path.GetFullPath(dependencyExpansionPath);
        var catalog = Read<MasterAnglerOpportunityCatalogReport>(
            catalogFullPath,
            "Master Angler opportunity catalog");
        var windows = Read<MasterAnglerStageOneWindowIndex>(
            windowFullPath,
            "Master Angler window index");
        var graph = ReadDependencyGraph(dependencyFullPath, catalog.GoalId);

        ValidateAuthority(catalogFullPath, catalog, windows);
        var catalogById = catalog.Species.ToDictionary(
            value => value.QualifiedItemId,
            StringComparer.Ordinal);
        var unknownLocations = windows.Species
            .SelectMany(value => RelevantWindows(value))
            .Select(value => value.LocationId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Where(value => !LocationPrerequisites.ContainsKey(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(unknownLocations.Length == 0,
            "Unknown Master Angler location prerequisite mappings: " +
            string.Join(",", unknownLocations));

        var species = windows.Species
            .OrderBy(value => value.QualifiedItemId, StringComparer.Ordinal)
            .Select(value => BuildSpecies(value, catalogById[value.QualifiedItemId]))
            .ToArray();
        Require(species.All(value => value.StaticOpportunityDayCount >= 2),
            "At least one Master Angler species has fewer than two static opportunity days.");

        var families = BuildFamilies(species, graph);
        var missing = families
            .Where(value => value.ProducerStatus != "implemented_and_bound")
            .Select(value => value.FamilyId)
            .ToArray();
        return new MasterAnglerFreshSavePrerequisiteAuditReport
        {
            Status = "partial_fresh_save_prerequisite_inventory",
            GoalId = catalog.GoalId,
            GameVersion = catalog.GameVersion,
            TrainingLabelEligible = false,
            CatalogPath = catalogFullPath,
            CatalogSha256 = CurrentTeacherFrontierSupport.HashFile(catalogFullPath),
            WindowIndexPath = windowFullPath,
            WindowIndexSha256 = CurrentTeacherFrontierSupport.HashFile(windowFullPath),
            DependencyExpansionPath = dependencyFullPath,
            DependencyExpansionSha256 = CurrentTeacherFrontierSupport.HashFile(dependencyFullPath),
            DeadlineTotalDayExclusive = windows.DeadlineTotalDayExclusive,
            NativeDenominatorCount = species.Length,
            RodLocationSpeciesCount = catalog.RodLocationSpeciesCount,
            MineOverrideOnlySpeciesCount = catalog.MineOverrideOnlySpeciesCount,
            TrapSpeciesCount = catalog.TrapSpeciesCount,
            SpeciesWithTwoOrMoreStaticOpportunityDays =
                species.Count(value => value.StaticOpportunityDayCount >= 2),
            RainOrStormRequiredSpeciesCount =
                species.Count(value => value.WeatherRequirement == "rain_or_storm_required"),
            DynamicConditionSpeciesCount =
                species.Count(value => value.DynamicConditions.Length > 0),
            MaximumRequiredFishingLevel =
                species.Max(value => value.MaximumSourceRequiredFishingLevel),
            StaticOpportunityInventoryComplete = true,
            PrerequisiteFamilyInventoryComplete = true,
            Species = species,
            PrerequisiteFamilies = families,
            MissingOrUnboundProducerFamilies = missing,
            UnknownLocationIds = unknownLocations
        };
    }

    private static MasterAnglerFreshSaveSpeciesPrerequisite BuildSpecies(
        MasterAnglerStageOneSpeciesWindow windows,
        MasterAnglerSpeciesOpportunity catalog)
    {
        Require(windows.Windows.Length > 0,
            "Master Angler species has no static source windows: " + windows.QualifiedItemId);
        var relevantWindows = RelevantWindows(windows).ToArray();
        Require(relevantWindows.Length > 0,
            "Master Angler species has no relevant source windows: " + windows.QualifiedItemId);
        foreach (var window in relevantWindows)
        {
            Require(window.FirstTotalDay >= 0 &&
                    window.LastTotalDay >= window.FirstTotalDay &&
                    window.LastTotalDay < 224,
                "Master Angler static source window falls outside the Stage 1 deadline: " +
                windows.QualifiedItemId);
        }

        var staticDays = relevantWindows
            .SelectMany(value => Enumerable.Range(
                value.FirstTotalDay,
                value.LastTotalDay - value.FirstTotalDay + 1))
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        var sources = relevantWindows
            .GroupBy(value => new
            {
                value.SourceKind,
                value.SourceKey,
                value.LocationId,
                value.MineArea,
                value.WaterType
            })
            .Select(group => new MasterAnglerFreshSaveSourceAlternative
            {
                SourceKind = group.Key.SourceKind,
                SourceKey = group.Key.SourceKey,
                LocationId = group.Key.LocationId,
                MineArea = group.Key.MineArea,
                WaterType = group.Key.WaterType,
                PrerequisiteFamily = SourcePrerequisite(group.First())
            })
            .OrderBy(value => value.PrerequisiteFamily, StringComparer.Ordinal)
            .ThenBy(value => value.SourceKey, StringComparer.Ordinal)
            .ToArray();
        var prerequisiteAlternatives = sources
            .Select(value => value.PrerequisiteFamily)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var weatherModes = relevantWindows
            .SelectMany(value => value.WeatherModes)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var dynamicConditions = relevantWindows
            .SelectMany(value => value.DynamicConditions)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var trapWindows = windows.Windows
            .Where(value => value.SourceKind == "crab_pot")
            .ToArray();
        var availableTrapCycles = trapWindows.Length == 0
            ? (int?)null
            : trapWindows
                .SelectMany(value => Enumerable.Range(
                    value.FirstTotalDay,
                    value.LastTotalDay - value.FirstTotalDay + 1))
                .Distinct()
                .Count();
        var requiredPotDays = catalog.TrapDailyOutputChance.HasValue
            ? StochasticRetryPolicy.RequiredIndependentAttemptCount(
                1,
                catalog.TrapDailyOutputChance.Value)
            : null;
        return new MasterAnglerFreshSaveSpeciesPrerequisite
        {
            QualifiedItemId = windows.QualifiedItemId,
            DisplayName = windows.DisplayName,
            AcquisitionClass = windows.AcquisitionClass,
            RoutePrerequisiteExpression = RouteExpression(prerequisiteAlternatives),
            RoutePrerequisiteAlternatives = prerequisiteAlternatives,
            SourceAlternatives = sources,
            MinimumRequiredFishingLevel = relevantWindows.Min(value => value.MinimumFishingLevel),
            MaximumSourceRequiredFishingLevel = relevantWindows.Max(value => value.MinimumFishingLevel),
            WeatherRequirement = WeatherRequirement(relevantWindows),
            WeatherModes = weatherModes,
            DynamicConditions = dynamicConditions,
            StaticOpportunityDayCount = staticDays.Length,
            FirstStaticOpportunityTotalDay = staticDays[0],
            FinalStaticReserveTotalDay = staticDays[^1],
            TrapDailyOutputChance = catalog.TrapDailyOutputChance,
            TrapRequiredIndependentPotDaysAt95Percent = requiredPotDays,
            TrapStaticMaximumServicedCycleCountFromDayOne = availableTrapCycles,
            TrapTheoreticalMinimumParallelPotCapacityFromDayOne =
                requiredPotDays.HasValue && availableTrapCycles > 0
                    ? (int)Math.Ceiling(
                        requiredPotDays.Value / (double)availableTrapCycles.Value)
                    : null,
            RodRetryBudgetOwner = windows.AcquisitionClass == "trap_crab_pot"
                ? string.Empty
                : "live_target_date_stochastic_retry_budget"
        };
    }

    private static AuthoritativeCalendarSourceWindow[] RelevantWindows(
        MasterAnglerStageOneSpeciesWindow species) =>
        species.AcquisitionClass == "trap_crab_pot"
            ? species.Windows.Where(value => value.SourceKind == "crab_pot").ToArray()
            : species.Windows;

    private static string SourcePrerequisite(AuthoritativeCalendarSourceWindow window)
    {
        if (window.SourceKind == "crab_pot")
            return TrapLifecycleFamily;
        if (window.SourceKind == "mine_override")
            return "mine_access_and_depth";
        Require(window.SourceKind == "location_rule",
            "Unknown Master Angler source kind: " + window.SourceKind);
        Require(LocationPrerequisites.TryGetValue(window.LocationId, out var family),
            "Unknown Master Angler location prerequisite: " + window.LocationId);
        return family!;
    }

    private static string RouteExpression(string[] alternatives)
    {
        if (alternatives.Contains("ordinary_world_access", StringComparer.Ordinal))
            return "ordinary_world_access";
        if (alternatives.SequenceEqual(new[] { "ginger_island_access", "mine_access_and_depth" }))
            return "mine_depth_or_ginger_island_caldera";
        if (alternatives.SequenceEqual(new[] { "forest_farm_layout", "secret_woods_access" }))
            return "secret_woods_access_or_forest_farm_layout";
        if (alternatives.Length > 2)
            return "one_of_advanced_location_access";
        return alternatives.Length == 1
            ? alternatives[0]
            : string.Join("_or_", alternatives);
    }

    private static string WeatherRequirement(
        IReadOnlyCollection<AuthoritativeCalendarSourceWindow> windows)
    {
        var modes = windows.Select(value => value.WeatherModes).ToArray();
        if (modes.All(value => value.Length > 0 && value.All(RainModes.Contains)))
            return "rain_or_storm_required";
        if (modes.All(value => value.Length > 0 &&
                               value.All(mode => mode == "sun")))
            return "sun_required";
        return "weather_flexible_across_sources";
    }

    private static MasterAnglerFreshSavePrerequisiteFamily[] BuildFamilies(
        MasterAnglerFreshSaveSpeciesPrerequisite[] species,
        DependencyGraphInventory graph)
    {
        var required = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        void Add(string family, IEnumerable<string> ids)
        {
            if (!required.TryGetValue(family, out var values))
            {
                values = new HashSet<string>(StringComparer.Ordinal);
                required.Add(family, values);
            }
            values.UnionWith(ids);
        }

        var rodSpecies = species.Where(value => value.AcquisitionClass != "trap_crab_pot").ToArray();
        var trapSpecies = species.Where(value => value.AcquisitionClass == "trap_crab_pot").ToArray();
        Add(RodEquipmentFamily, rodSpecies.Select(value => value.QualifiedItemId));
        Add(FishingSkillFamily, rodSpecies
            .Where(value => value.MaximumSourceRequiredFishingLevel > 0)
            .Select(value => value.QualifiedItemId));
        Add(WeatherFamily, rodSpecies
            .Where(value => value.WeatherRequirement == "rain_or_storm_required")
            .Select(value => value.QualifiedItemId));
        Add(TrapLifecycleFamily, trapSpecies.Select(value => value.QualifiedItemId));
        Add(TrapSupplyFamily, trapSpecies.Select(value => value.QualifiedItemId));
        foreach (var row in species)
        {
            if (row.RoutePrerequisiteExpression is "ordinary_world_access" or TrapLifecycleFamily)
                continue;
            Add(row.RoutePrerequisiteExpression, new[] { row.QualifiedItemId });
        }

        return required
            .Where(pair => pair.Value.Count > 0)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => Family(pair.Key, pair.Value, graph))
            .ToArray();
    }

    private static MasterAnglerFreshSavePrerequisiteFamily Family(
        string family,
        IEnumerable<string> species,
        DependencyGraphInventory graph)
    {
        var (status, evidence, work) = family switch
        {
            TrapLifecycleFamily when graph.HasNode(
                "dependency.complete_master_angler.trap_lifecycle_intent") =>
                ("implemented_and_bound",
                    new[] { "dependency.complete_master_angler.trap_lifecycle_intent" },
                    "No lifecycle implementation is missing; fresh-save supply remains separate."),
            FishingSkillFamily when graph.HasOption("raise_skill_levels", "fishing.catch_fish") =>
                ("reusable_but_unbound",
                    new[] { "raise_skill_levels:fishing.catch_fish" },
                    "Bind fishing-level progression and its time cost into the Master Angler deadline recurrence."),
            "mine_access_and_depth" when graph.HasOption("raise_skill_levels", "mining.reach_depth") =>
                ("reusable_but_unbound",
                    new[] { "raise_skill_levels:mining.reach_depth" },
                    "Bind the existing mine-depth producer to each required fishing floor and deadline."),
            "mine_depth_or_ginger_island_caldera" when graph.HasOption("raise_skill_levels", "mining.reach_depth") =>
                ("reusable_branch_but_disjunction_unbound",
                    new[] { "raise_skill_levels:mining.reach_depth" },
                    "Prove and schedule either mine area 80 or the complete Ginger Island/Caldera access chain."),
            "one_of_advanced_location_access" when
                graph.HasOption("raise_skill_levels", "mining.reach_depth") =>
                ("reusable_branch_but_disjunction_unbound",
                    graph.HasDirection("obtain_rusty_key")
                        ? new[] { "raise_skill_levels:mining.reach_depth", "obtain_rusty_key" }
                        : new[] { "raise_skill_levels:mining.reach_depth" },
                    "Select and bind one exact sufficient route; the current baseline can reuse mine depth while sewer, Mutant Bug Lair and Witch's Swamp remain alternatives."),
            "sewer_rusty_key_access" when graph.HasDirection("obtain_rusty_key") =>
                ("reusable_but_unbound",
                    new[] { "obtain_rusty_key" },
                    "Bind the Rusty Key recurrence and sewer ingress to Master Angler before Mutant Carp reserve."),
            "desert_bus_access" when graph.HasDirection("complete_community_center") =>
                ("upstream_root_present_composed_producer_missing",
                    new[] { "complete_community_center" },
                    "Bind the exact bus-repair branch or equivalent Joja unlock plus bus travel; full CC completion alone is not an access proof."),
            "secret_woods_access_or_forest_farm_layout" =>
                ("alternative_selection_missing",
                    Array.Empty<string>(),
                    "Select the actual farm layout; otherwise schedule axe progression and Secret Woods access."),
            TrapSupplyFamily =>
                ("runtime_consumer_present_supply_producer_missing",
                    new[] { "dependency.complete_master_angler.craft_trap_infrastructure" },
                    "Schedule recipe or profession unlock, exact crafting materials, bait supply and their time/reservations."),
            RodEquipmentFamily =>
                ("current_state_gate_present_future_producer_missing",
                    new[] { "dependency.complete_master_angler.target_date_opportunity_producer" },
                    "Schedule Willy's rod acquisition and any required rod, bait or tackle upgrades before their target windows."),
            WeatherFamily =>
                ("current_state_gate_present_future_producer_missing",
                    new[] { "dependency.complete_master_angler.target_date_opportunity_producer" },
                    "Reserve natural rainy opportunities or bind the existing native Rain Totem primitive with exact acquisition and next-day effect."),
            "ginger_island_access" =>
                ("composed_producer_missing",
                    graph.HasDirection("complete_community_center")
                        ? new[] { "complete_community_center" }
                        : Array.Empty<string>(),
                    "Schedule Community Center or Joja completion, Willy's boat repair, ticket travel and island sub-area ingress."),
            "witch_swamp_story_access" =>
                ("producer_missing",
                    Array.Empty<string>(),
                    "Schedule Dark Talisman and Goblin Problem progression through Witch's Swamp access."),
            "mutant_bug_lair_story_access" =>
                ("producer_missing",
                    Array.Empty<string>(),
                    "Schedule sewer access and Dark Talisman progression through Mutant Bug Lair ingress."),
            _ =>
                ("producer_missing",
                    Array.Empty<string>(),
                    "Add one exact fresh-save producer and bind it to the Master Angler deadline recurrence.")
        };
        return new MasterAnglerFreshSavePrerequisiteFamily
        {
            FamilyId = family,
            ProducerStatus = status,
            RequiredBySpeciesIds = species.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ExistingEvidence = evidence,
            RemainingWork = work
        };
    }

    private static void ValidateAuthority(
        string catalogPath,
        MasterAnglerOpportunityCatalogReport catalog,
        MasterAnglerStageOneWindowIndex windows)
    {
        Require(catalog.SchemaVersion == "master_angler_opportunity_catalog.v1" &&
                catalog.Status == "complete" &&
                catalog.SourceInventoryComplete &&
                catalog.StaticCalendarConstraintComplete &&
                catalog.LocationRuleSpawnChanceInputInventoryComplete,
            "Master Angler opportunity catalog is incomplete.");
        Require(catalog.NativeDenominatorCount == 72 &&
                catalog.Species.Length == 72 &&
                catalog.RodLocationSpeciesCount == 60 &&
                catalog.MineOverrideOnlySpeciesCount == 2 &&
                catalog.TrapSpeciesCount == 10,
            "Master Angler source partition drifted from 60/2/10 over 72 species.");
        Require(windows.SchemaVersion == "master_angler_stage_one_window_index.v1" &&
                windows.Status == "complete_static_windows_dynamic_execution_pending" &&
                windows.StaticWindowCoverageComplete &&
                !windows.TrainingLabelEligible &&
                windows.DeadlineTotalDayExclusive == 224 &&
                windows.NativeDenominatorCount == 72 &&
                windows.Species.Length == 72 &&
                windows.UnresolvedSpeciesIds.Length == 0,
            "Master Angler Stage 1 window index is incomplete or changed deadline semantics.");
        Require(windows.GoalId == catalog.GoalId && windows.GameVersion == catalog.GameVersion,
            "Master Angler catalog and window authority disagree.");
        Require(string.Equals(
                windows.CatalogSha256,
                CurrentTeacherFrontierSupport.HashFile(catalogPath),
                StringComparison.OrdinalIgnoreCase),
            "Master Angler window index does not hash-bind the supplied catalog.");
        var catalogIds = catalog.Species.Select(value => value.QualifiedItemId)
            .OrderBy(value => value, StringComparer.Ordinal);
        var windowIds = windows.Species.Select(value => value.QualifiedItemId)
            .OrderBy(value => value, StringComparer.Ordinal);
        Require(catalogIds.SequenceEqual(windowIds, StringComparer.Ordinal),
            "Master Angler catalog and window species denominators disagree.");
    }

    private static DependencyGraphInventory ReadDependencyGraph(
        string path,
        string goalId)
    {
        using var document = JsonDocument.Parse(
            CurrentTeacherFrontierSupport.ReadArtifactText(path));
        var root = document.RootElement;
        Require(root.GetProperty("schema_version").GetString() ==
                "goal_method_dependency_expansions.v1" &&
                root.GetProperty("goal_id").GetString() == goalId,
            "Unsupported or mismatched goal-method dependency expansion.");
        var directions = root.GetProperty("directions").EnumerateArray()
            .ToDictionary(
                value => value.GetProperty("direction_id").GetString()!,
                value => new DependencyDirectionInventory(
                    value.GetProperty("nodes").EnumerateArray()
                        .Select(node => node.GetProperty("id").GetString()!)
                        .ToHashSet(StringComparer.Ordinal),
                    value.GetProperty("nodes").EnumerateArray()
                        .Where(node => node.TryGetProperty("option_id", out var option) &&
                                       option.ValueKind == JsonValueKind.String)
                        .Select(node => node.GetProperty("option_id").GetString()!)
                        .ToHashSet(StringComparer.Ordinal)),
                StringComparer.Ordinal);
        return new DependencyGraphInventory(directions);
    }

    private static T Read<T>(string path, string label) =>
        JsonSerializer.Deserialize<T>(
            CurrentTeacherFrontierSupport.ReadArtifactText(path),
            JsonDefaults.Options) ??
        throw new InvalidDataException(label + " is null.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record DependencyDirectionInventory(
        HashSet<string> NodeIds,
        HashSet<string> OptionIds);

    private sealed record DependencyGraphInventory(
        Dictionary<string, DependencyDirectionInventory> Directions)
    {
        public bool HasDirection(string direction) => Directions.ContainsKey(direction);

        public bool HasNode(string node) => Directions.Values.Any(value => value.NodeIds.Contains(node));

        public bool HasOption(string direction, string option) =>
            Directions.TryGetValue(direction, out var value) &&
            value.OptionIds.Contains(option);
    }
}
