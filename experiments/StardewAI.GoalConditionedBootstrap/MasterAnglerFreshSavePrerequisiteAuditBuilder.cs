using System.Text.Json;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

public static class MasterAnglerFreshSavePrerequisiteAuditBuilder
{
    private const string MasterAnglerDirection = "complete_master_angler";
    private const string FishingSkillProducerNode =
        "dependency.complete_master_angler.fishing_skill_progression";
    private const string FishingSkillSourceDirection = "raise_skill_levels";
    private const string FishingSkillSourceNode =
        "dependency.raise_skill_levels.acquire_fishing_xp";
    private const string MineAccessProducerNode =
        "dependency.complete_master_angler.ordinary_mine_fishing_access";
    private const string MineAccessSourceDirection = "raise_skill_levels";
    private const string MineAccessSourceNode =
        "dependency.raise_skill_levels.acquire_mining_xp";
    private const string RodEquipmentProducerNode =
        "dependency.complete_master_angler.rod_equipment_acquisition";
    private const string DesertBusProducerNode =
        "dependency.complete_master_angler.desert_bus_access";
    private const string CommunityCenterDirection = "complete_community_center";
    private const string CommunityCenterCompletionNode =
        "dependency.complete_community_center.native_completion";
    private const string SewerAccessProducerNode =
        "dependency.complete_master_angler.sewer_rusty_key_access";
    private const string TrapSupplyProducerNode =
        "dependency.complete_master_angler.trap_supply_acquisition";
    private const string TrapInfrastructureGateNode =
        "dependency.complete_master_angler.trap_infrastructure_gate";
    private const string RustyKeyDirection = "obtain_rusty_key";
    private const string RustyKeyCompletionNode =
        "dependency.obtain_rusty_key.has_rusty_key";
    private const string RustyKeyRecurrenceNode =
        "dependency.obtain_rusty_key.threshold_recurrence";
    private const string RustyKeyEventNode =
        "dependency.obtain_rusty_key.native_key_event";
    private const string TargetDateOpportunityNode =
        "dependency.complete_master_angler.target_date_opportunity_producer";
    private const string MasterAnglerRecurrenceNode =
        "dependency.complete_master_angler.acquisition_recurrence";
    private const string FreshSaveInventoryNode =
        "dependency.complete_master_angler.fresh_save_prerequisite_inventory";
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
            FishingSkillFamily when graph.HasBoundMasterAnglerFishingSkillProgression() =>
                ("implemented_and_bound",
                    new[] { FishingSkillProducerNode, FishingSkillSourceNode },
                    "The Master Angler recurrence reuses the existing exact fishing-XP producer, deadline accounting and fresh native receipt."),
            FishingSkillFamily when graph.HasOption(FishingSkillSourceDirection, "fishing.catch_fish") =>
                ("reusable_but_unbound",
                    new[] { FishingSkillSourceNode },
                    "Bind fishing-level progression and its time cost into the Master Angler deadline recurrence."),
            "mine_access_and_depth" when graph.HasBoundMasterAnglerMineAccessProgression() =>
                ("implemented_and_bound",
                    new[] { MineAccessProducerNode, MineAccessSourceNode },
                    "The selected ordinary-mine producer enters floor one natively, reaches the exact sufficient fishing floor and replans from fresh receipts."),
            "mine_access_and_depth" when graph.HasOption("raise_skill_levels", "mining.reach_depth") =>
                ("reusable_but_unbound",
                    new[] { "raise_skill_levels:mining.reach_depth" },
                    "Bind the existing mine-depth producer to each required fishing floor and deadline."),
            "mine_depth_or_ginger_island_caldera" when graph.HasBoundMasterAnglerMineAccessProgression() =>
                ("implemented_and_bound",
                    new[] { MineAccessProducerNode, MineAccessSourceNode },
                    "The selected ordinary-mine area-80 branch is sufficient; Ginger Island and Caldera remain valid but unnecessary alternatives."),
            "mine_depth_or_ginger_island_caldera" when graph.HasOption("raise_skill_levels", "mining.reach_depth") =>
                ("reusable_branch_but_disjunction_unbound",
                    new[] { "raise_skill_levels:mining.reach_depth" },
                    "Prove and schedule either mine area 80 or the complete Ginger Island/Caldera access chain."),
            "one_of_advanced_location_access" when graph.HasBoundMasterAnglerMineAccessProgression() =>
                ("implemented_and_bound",
                    new[] { MineAccessProducerNode, MineAccessSourceNode },
                    "The selected ordinary-mine branch is sufficient; sewer, Mutant Bug Lair and Witch's Swamp remain valid but unnecessary alternatives."),
            "one_of_advanced_location_access" when
                graph.HasOption("raise_skill_levels", "mining.reach_depth") =>
                ("reusable_branch_but_disjunction_unbound",
                    graph.HasDirection("obtain_rusty_key")
                        ? new[] { "raise_skill_levels:mining.reach_depth", "obtain_rusty_key" }
                        : new[] { "raise_skill_levels:mining.reach_depth" },
                    "Select and bind one exact sufficient route; the current baseline can reuse mine depth while sewer, Mutant Bug Lair and Witch's Swamp remain alternatives."),
            "sewer_rusty_key_access" when graph.HasBoundMasterAnglerSewerAccess() =>
                ("implemented_and_bound",
                    new[] { SewerAccessProducerNode, RustyKeyCompletionNode },
                    "The Master Angler recurrence composes the separately governed Rusty Key root with the exact one-or-two-interaction native sewer ingress and a fresh route receipt."),
            "sewer_rusty_key_access" when graph.HasDirection(RustyKeyDirection) =>
                ("reusable_but_unbound",
                    new[] { RustyKeyDirection },
                    "Bind the Rusty Key recurrence and sewer ingress to Master Angler before Mutant Carp reserve."),
            "desert_bus_access" when graph.HasBoundMasterAnglerDesertBusAccess() =>
                ("implemented_and_bound",
                    new[] { DesertBusProducerNode, CommunityCenterCompletionNode },
                    "The recurrence composes the existing Community Center completion proof with the locked native bus ticket and return transports, then replans from a fresh receipt."),
            "desert_bus_access" when graph.HasDirection(CommunityCenterDirection) =>
                ("upstream_root_present_composed_producer_missing",
                    new[] { CommunityCenterDirection },
                    "Bind the exact bus-repair branch or equivalent Joja unlock plus bus travel; full CC completion alone is not an access proof."),
            "secret_woods_access_or_forest_farm_layout" =>
                ("alternative_selection_missing",
                    Array.Empty<string>(),
                    "Select the actual farm layout; otherwise schedule axe progression and Secret Woods access."),
            TrapSupplyFamily when graph.HasBoundMasterAnglerTrapSupply() =>
                ("implemented_and_bound",
                    new[] { TrapSupplyProducerNode, FishingSkillProducerNode },
                    "The lifecycle reuses exact one-unit FishShop purchases for Crab Pot capacity and bait deficits after the native fishing-level stock unlock, then replans from a fresh receipt."),
            TrapSupplyFamily =>
                ("runtime_consumer_present_supply_producer_missing",
                    new[] { "dependency.complete_master_angler.craft_trap_infrastructure" },
                    "Bind an exact fresh-save Crab Pot and bait supply producer, including unlock, money, route and native purchase receipts."),
            RodEquipmentFamily when graph.HasBoundMasterAnglerRodEquipmentAcquisition() =>
                ("implemented_and_bound",
                    new[] { RodEquipmentProducerNode },
                    "The recurrence reuses the exact FishShop Bamboo Pole purchase chain; later rods, bait and tackle remain optional efficiency improvements."),
            RodEquipmentFamily =>
                ("current_state_gate_present_future_producer_missing",
                    new[] { "dependency.complete_master_angler.target_date_opportunity_producer" },
                    "Bind one sufficient rod acquisition before target windows; optional equipment upgrades must not become hard prerequisites."),
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
                        .Select(node => new DependencyNodeInventory(
                            node.GetProperty("id").GetString()!,
                            node.GetProperty("kind").GetString()!,
                            node.TryGetProperty("option_id", out var option) &&
                            option.ValueKind == JsonValueKind.String
                                ? option.GetString() ?? string.Empty
                                : string.Empty,
                            node.TryGetProperty("candidate_kinds", out var candidateKinds) &&
                            candidateKinds.ValueKind == JsonValueKind.Array
                                ? candidateKinds.EnumerateArray()
                                    .Select(candidateKind => candidateKind.GetString() ?? string.Empty)
                                    .ToHashSet(StringComparer.Ordinal)
                                : new HashSet<string>(StringComparer.Ordinal),
                            node.TryGetProperty("required_field_paths", out var fields) &&
                            fields.ValueKind == JsonValueKind.Array
                                ? fields.EnumerateArray()
                                    .Select(field => field.GetString() ?? string.Empty)
                                    .ToHashSet(StringComparer.Ordinal)
                                : new HashSet<string>(StringComparer.Ordinal),
                            node.TryGetProperty("rule", out var rule)
                                ? rule.Clone()
                                : default))
                        .ToDictionary(node => node.Id, StringComparer.Ordinal),
                    value.GetProperty("edges").EnumerateArray()
                        .Select(edge => new DependencyEdgeInventory(
                            edge.GetProperty("from").GetString()!,
                            edge.GetProperty("to").GetString()!,
                            edge.GetProperty("kind").GetString()!))
                        .ToHashSet()),
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

    private sealed record DependencyNodeInventory(
        string Id,
        string Kind,
        string OptionId,
        HashSet<string> CandidateKinds,
        HashSet<string> RequiredFieldPaths,
        JsonElement Rule);

    private sealed record DependencyEdgeInventory(
        string From,
        string To,
        string Kind);

    private sealed record DependencyDirectionInventory(
        Dictionary<string, DependencyNodeInventory> Nodes,
        HashSet<DependencyEdgeInventory> Edges);

    private sealed record DependencyGraphInventory(
        Dictionary<string, DependencyDirectionInventory> Directions)
    {
        public bool HasDirection(string direction) => Directions.ContainsKey(direction);

        public bool HasNode(string node) => Directions.Values.Any(value => value.Nodes.ContainsKey(node));

        public bool HasOption(string direction, string option) =>
            Directions.TryGetValue(direction, out var value) &&
            value.Nodes.Values.Any(node => node.OptionId == option);

        public bool HasBoundMasterAnglerFishingSkillProgression()
        {
            if (!Directions.TryGetValue(MasterAnglerDirection, out var masterAngler) ||
                !Directions.TryGetValue(FishingSkillSourceDirection, out var skills) ||
                !masterAngler.Nodes.TryGetValue(FishingSkillProducerNode, out var producer) ||
                !skills.Nodes.TryGetValue(FishingSkillSourceNode, out var source) ||
                producer.Kind != "recurrence_rule" ||
                source.Kind != "policy_option_transition" ||
                source.OptionId != "fishing.catch_fish" ||
                producer.Rule.ValueKind != JsonValueKind.Object)
                return false;

            var requiredFields = new[]
            {
                "world_progress.fish_collection_progress",
                "player.skills_detail",
                "time.total_days",
                "time.time",
                "fishing.location_context",
                "fishing.fishable_tiles",
                "fishing.rod_contexts"
            };
            return requiredFields.All(producer.RequiredFieldPaths.Contains) &&
                   RuleString(producer.Rule, "reuses_direction_id") == FishingSkillSourceDirection &&
                   RuleString(producer.Rule, "reuses_node_id") == FishingSkillSourceNode &&
                   RuleString(producer.Rule, "reused_option_id") == "fishing.catch_fish" &&
                   RuleBool(producer.Rule, "fresh_native_receipt_required") &&
                   RuleBool(producer.Rule, "recompute_after_every_native_receipt") &&
                   RuleInt(producer.Rule, "deadline_total_day_exclusive") == 224 &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       FreshSaveInventoryNode,
                       FishingSkillProducerNode,
                       "selects_required_fishing_level")) &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       FishingSkillProducerNode,
                       MasterAnglerRecurrenceNode,
                       "supplies_required_fishing_level"));
        }

        public bool HasBoundMasterAnglerMineAccessProgression()
        {
            if (!Directions.TryGetValue(MasterAnglerDirection, out var masterAngler) ||
                !Directions.TryGetValue(MineAccessSourceDirection, out var skills) ||
                !masterAngler.Nodes.TryGetValue(MineAccessProducerNode, out var producer) ||
                !skills.Nodes.TryGetValue(MineAccessSourceNode, out var source) ||
                producer.Kind != "recurrence_rule" ||
                source.Kind != "policy_option_transition" ||
                source.OptionId != "mining.reach_depth" ||
                producer.Rule.ValueKind != JsonValueKind.Object)
                return false;

            var requiredFields = new[]
            {
                "world_progress.fish_collection_progress",
                "time.total_days",
                "time.time",
                "player.location_id",
                "player.tile_x",
                "player.tile_y",
                "player.deepest_mine_level",
                "locations.route_graph",
                "locations.route_connectors",
                "locations.route_action_branch_coverage",
                "locations.collision_grid",
                "mining.current_mine",
                "mining.tiles",
                "mining.objects",
                "mining.resource_clumps",
                "mining.monsters",
                "mining.floor_objectives",
                "mining.reward_chests",
                "mining.player_resources"
            };
            var expectedFloors = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["(O)156"] = 20,
                ["(O)157"] = 20,
                ["(O)158"] = 20,
                ["(O)161"] = 60,
                ["(O)162"] = 100,
                ["(O)CaveJelly"] = 100
            };
            var expectedAreas = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                ["20"] = 10,
                ["60"] = 40,
                ["100"] = 80
            };
            return requiredFields.All(producer.RequiredFieldPaths.Contains) &&
                   RuleString(producer.Rule, "reuses_direction_id") == MineAccessSourceDirection &&
                   RuleString(producer.Rule, "reuses_node_id") == MineAccessSourceNode &&
                   RuleString(producer.Rule, "reused_option_id") == "mining.reach_depth" &&
                   RuleBool(producer.Rule, "ordinary_mine_only") &&
                   RuleBool(producer.Rule, "native_lobby_ingress_required") &&
                   RuleString(producer.Rule, "native_lobby_ingress_action") == "Mine 1" &&
                   RuleString(producer.Rule, "native_lobby_ingress_target") == "UndergroundMine1" &&
                   RuleObjectIntsEqual(producer.Rule, "target_floor_by_species", expectedFloors) &&
                   RuleObjectIntsEqual(producer.Rule, "target_area_by_floor", expectedAreas) &&
                   RuleBool(producer.Rule, "fresh_snapshot_after_every_native_primitive") &&
                   RuleBool(producer.Rule, "fresh_native_receipt_required") &&
                   RuleInt(producer.Rule, "deadline_total_day_exclusive") == 224 &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       FreshSaveInventoryNode,
                       MineAccessProducerNode,
                       "selects_required_mine_fishing_floor")) &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       MineAccessProducerNode,
                       TargetDateOpportunityNode,
                       "supplies_ordinary_mine_location_access")) &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       MineAccessProducerNode,
                       MasterAnglerRecurrenceNode,
                       "replans_after_native_mine_primitive"));
        }

        public bool HasBoundMasterAnglerRodEquipmentAcquisition()
        {
            if (!Directions.TryGetValue(MasterAnglerDirection, out var masterAngler) ||
                !masterAngler.Nodes.TryGetValue(
                    RodEquipmentProducerNode,
                    out var producer) ||
                producer.Kind != "recurrence_rule" ||
                producer.OptionId != "economy.buy_supplies" ||
                producer.Rule.ValueKind != JsonValueKind.Object)
                return false;

            var requiredFields = new[]
            {
                "world_progress.fish_collection_progress",
                "time.total_days",
                "time.time",
                "player.inventory",
                "player.money",
                "player.location_id",
                "locations.shops",
                "locations.route_graph",
                "locations.route_connectors",
                "menus.active_menu"
            };
            return requiredFields.All(producer.RequiredFieldPaths.Contains) &&
                   RuleString(producer.Rule, "reused_option_id") ==
                       "economy.buy_supplies" &&
                   RuleString(producer.Rule, "required_qualified_item_id") ==
                       "(T)BambooPole" &&
                   RuleString(producer.Rule, "required_shop_id") == "FishShop" &&
                   RuleInt(producer.Rule, "required_quantity") == 1 &&
                   RuleInt(producer.Rule, "maximum_unit_price") == 500 &&
                   RuleBool(producer.Rule, "fresh_snapshot_after_every_native_primitive") &&
                   RuleBool(producer.Rule, "fresh_native_receipt_required") &&
                   RuleInt(producer.Rule, "deadline_total_day_exclusive") == 224 &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       FreshSaveInventoryNode,
                       RodEquipmentProducerNode,
                       "selects_required_rod_equipment")) &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       RodEquipmentProducerNode,
                       TargetDateOpportunityNode,
                       "supplies_required_rod_equipment")) &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       RodEquipmentProducerNode,
                       MasterAnglerRecurrenceNode,
                       "replans_after_native_purchase"));
        }

        public bool HasBoundMasterAnglerTrapSupply()
        {
            if (!Directions.TryGetValue(MasterAnglerDirection, out var masterAngler) ||
                !masterAngler.Nodes.TryGetValue(
                    TrapSupplyProducerNode,
                    out var producer) ||
                !masterAngler.Nodes.TryGetValue(
                    FishingSkillProducerNode,
                    out var skillProducer) ||
                producer.Kind != "recurrence_rule" ||
                producer.OptionId != "fishing.collect_crab_pots" ||
                skillProducer.Kind != "recurrence_rule" ||
                producer.Rule.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var requiredFields = new[]
            {
                "world_progress.fish_collection_progress",
                "player.skills_detail",
                "player.inventory",
                "player.money",
                "player.location_id",
                "player.crab_pot_crafting",
                "player.crab_pot_placement",
                "player.crab_pot_network",
                "current_location.objects",
                "locations.shops",
                "locations.route_graph",
                "locations.route_connectors",
                "menus.active_menu",
                "time.total_days",
                "time.time"
            };
            return producer.CandidateKinds.SetEquals(new[]
                   {
                       "route_connector_tile",
                       "interact_endpoint",
                       "buy_shop_item"
                   }) &&
                   requiredFields.All(producer.RequiredFieldPaths.Contains) &&
                   RuleString(producer.Rule, "reused_option_id") ==
                       "economy.buy_supplies" &&
                   RuleString(producer.Rule, "required_shop_id") ==
                       "FishShop" &&
                   RuleString(producer.Rule, "crab_pot_qualified_item_id") ==
                       "(O)710" &&
                   RuleInt(producer.Rule, "crab_pot_minimum_fishing_level") == 3 &&
                   RuleInt(producer.Rule, "crab_pot_maximum_unit_price") == 1500 &&
                   RuleString(producer.Rule, "bait_qualified_item_id") ==
                       "(O)685" &&
                   RuleInt(producer.Rule, "bait_minimum_fishing_level") == 2 &&
                   RuleInt(producer.Rule, "bait_maximum_unit_price") == 5 &&
                   RuleInt(producer.Rule, "required_quantity_per_purchase") == 1 &&
                   RuleBool(producer.Rule, "fresh_snapshot_after_every_native_primitive") &&
                   RuleBool(producer.Rule, "fresh_native_receipt_required") &&
                   RuleInt(producer.Rule, "deadline_total_day_exclusive") == 224 &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       FreshSaveInventoryNode,
                       TrapSupplyProducerNode,
                       "selects_trap_supply_requirement")) &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       FishingSkillProducerNode,
                       TrapSupplyProducerNode,
                       "unlocks_fish_shop_trap_stock")) &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       TrapSupplyProducerNode,
                       TrapInfrastructureGateNode,
                       "supplies_exact_pot_and_bait")) &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       TrapSupplyProducerNode,
                       MasterAnglerRecurrenceNode,
                       "replans_after_native_purchase"));
        }

        public bool HasBoundMasterAnglerDesertBusAccess()
        {
            if (!Directions.TryGetValue(MasterAnglerDirection, out var masterAngler) ||
                !Directions.TryGetValue(CommunityCenterDirection, out var communityCenter) ||
                !masterAngler.Nodes.TryGetValue(DesertBusProducerNode, out var producer) ||
                !communityCenter.Nodes.TryGetValue(
                    CommunityCenterCompletionNode,
                    out var completion) ||
                producer.Kind != "recurrence_rule" ||
                completion.Kind != "terminal_fact" ||
                producer.Rule.ValueKind != JsonValueKind.Object)
                return false;

            var requiredFields = new[]
            {
                "world_progress.fish_collection_progress",
                "quests.mail_received",
                "time.total_days",
                "time.time",
                "player.money",
                "player.location_id",
                "player.tile_x",
                "player.tile_y",
                "locations.route_graph",
                "locations.route_connectors",
                "locations.route_gate_context",
                "locations.collision_grid",
                "menus.active_menu"
            };
            return producer.CandidateKinds.SetEquals(new[] { "route_connector_tile" }) &&
                   requiredFields.All(producer.RequiredFieldPaths.Contains) &&
                   RuleString(producer.Rule, "reuses_direction_id") ==
                       CommunityCenterDirection &&
                   RuleString(producer.Rule, "reuses_node_id") ==
                       CommunityCenterCompletionNode &&
                   RuleString(producer.Rule, "reused_option_id") ==
                       "fishing.catch_fish" &&
                   RuleString(producer.Rule, "connector_kind") ==
                       "native_bus_transport" &&
                   RuleString(producer.Rule, "forward_source_location") == "BusStop" &&
                   RuleString(producer.Rule, "forward_target_location") == "Desert" &&
                   RuleString(producer.Rule, "return_source_location") == "Desert" &&
                   RuleString(producer.Rule, "return_target_location") == "BusStop" &&
                   RuleString(producer.Rule, "required_mail_key") == "ccVault" &&
                   RuleInt(producer.Rule, "maximum_ticket_price") == 500 &&
                   RuleBool(producer.Rule, "fresh_snapshot_after_every_native_primitive") &&
                   RuleBool(producer.Rule, "fresh_native_receipt_required") &&
                   RuleInt(producer.Rule, "deadline_total_day_exclusive") == 224 &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       FreshSaveInventoryNode,
                       DesertBusProducerNode,
                       "selects_desert_access_requirement")) &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       DesertBusProducerNode,
                       TargetDateOpportunityNode,
                       "supplies_desert_location_access")) &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       DesertBusProducerNode,
                       MasterAnglerRecurrenceNode,
                       "replans_after_native_bus_transport"));
        }

        public bool HasBoundMasterAnglerSewerAccess()
        {
            if (!Directions.TryGetValue(MasterAnglerDirection, out var masterAngler) ||
                !Directions.TryGetValue(RustyKeyDirection, out var rustyKey) ||
                !masterAngler.Nodes.TryGetValue(SewerAccessProducerNode, out var producer) ||
                !rustyKey.Nodes.TryGetValue(RustyKeyCompletionNode, out var completion) ||
                !rustyKey.Nodes.TryGetValue(RustyKeyRecurrenceNode, out var recurrence) ||
                !rustyKey.Nodes.TryGetValue(RustyKeyEventNode, out var keyEvent) ||
                producer.Kind != "recurrence_rule" ||
                completion.Kind != "terminal_fact" ||
                recurrence.Kind != "recurrence_rule" ||
                keyEvent.Kind != "policy_option_transition" ||
                keyEvent.OptionId != "story.advance_event" ||
                producer.Rule.ValueKind != JsonValueKind.Object)
                return false;

            var requiredFields = new[]
            {
                "world_progress.fish_collection_progress",
                "player.has_rusty_key",
                "quests.mail_received",
                "time.total_days",
                "time.time",
                "player.location_id",
                "player.tile_x",
                "player.tile_y",
                "locations.route_graph",
                "locations.route_connectors",
                "locations.route_gate_context",
                "locations.collision_grid",
                "menus.active_menu"
            };
            return producer.CandidateKinds.SetEquals(new[] { "route_connector_tile" }) &&
                   requiredFields.All(producer.RequiredFieldPaths.Contains) &&
                   RuleString(producer.Rule, "reuses_direction_id") == RustyKeyDirection &&
                   RuleString(producer.Rule, "reuses_node_id") == RustyKeyCompletionNode &&
                   RuleString(producer.Rule, "reused_option_id") == "fishing.catch_fish" &&
                   RuleString(producer.Rule, "connector_kind") == "action_warp" &&
                   RuleStringSetEquals(
                       producer.Rule,
                       "source_locations",
                       new[] { "Forest", "Town" }) &&
                   RuleString(producer.Rule, "native_action") == "EnterSewer" &&
                   RuleString(producer.Rule, "target_location") == "Sewer" &&
                   RuleInt(producer.Rule, "target_x") == 16 &&
                   RuleInt(producer.Rule, "target_y") == 11 &&
                   RuleString(producer.Rule, "unlock_mail_key") == "OpenedSewer" &&
                   RuleInt(producer.Rule, "locked_action_count") == 2 &&
                   RuleInt(producer.Rule, "opened_action_count") == 1 &&
                   RuleBool(producer.Rule, "fresh_snapshot_after_every_native_primitive") &&
                   RuleBool(producer.Rule, "fresh_native_receipt_required") &&
                   RuleInt(producer.Rule, "deadline_total_day_exclusive") == 224 &&
                   rustyKey.Edges.Contains(new DependencyEdgeInventory(
                       RustyKeyRecurrenceNode,
                       RustyKeyEventNode,
                       "enables_after_threshold")) &&
                   rustyKey.Edges.Contains(new DependencyEdgeInventory(
                       RustyKeyEventNode,
                       RustyKeyCompletionNode,
                       "settles")) &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       FreshSaveInventoryNode,
                       SewerAccessProducerNode,
                       "selects_sewer_access_requirement")) &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       SewerAccessProducerNode,
                       TargetDateOpportunityNode,
                       "supplies_sewer_location_access")) &&
                   masterAngler.Edges.Contains(new DependencyEdgeInventory(
                       SewerAccessProducerNode,
                       MasterAnglerRecurrenceNode,
                       "replans_after_native_sewer_primitive"));
        }

        private static string RuleString(JsonElement rule, string name) =>
            rule.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;

        private static bool RuleBool(JsonElement rule, string name) =>
            rule.TryGetProperty(name, out var value) &&
            value.ValueKind is JsonValueKind.True;

        private static int RuleInt(JsonElement rule, string name) =>
            rule.TryGetProperty(name, out var value) && value.TryGetInt32(out var result)
                ? result
                : int.MinValue;

        private static bool RuleObjectIntsEqual(
            JsonElement rule,
            string name,
            IReadOnlyDictionary<string, int> expected)
        {
            if (!rule.TryGetProperty(name, out var value) ||
                value.ValueKind != JsonValueKind.Object)
                return false;
            var actual = value.EnumerateObject().ToArray();
            return actual.Length == expected.Count &&
                   actual.All(property =>
                       expected.TryGetValue(property.Name, out var expectedValue) &&
                       property.Value.TryGetInt32(out var actualValue) &&
                       actualValue == expectedValue);
        }

        private static bool RuleStringSetEquals(
            JsonElement rule,
            string name,
            IEnumerable<string> expected)
        {
            if (!rule.TryGetProperty(name, out var value) ||
                value.ValueKind != JsonValueKind.Array)
                return false;
            return value.EnumerateArray()
                .Select(item => item.GetString() ?? string.Empty)
                .ToHashSet(StringComparer.Ordinal)
                .SetEquals(expected);
        }
    }
}
