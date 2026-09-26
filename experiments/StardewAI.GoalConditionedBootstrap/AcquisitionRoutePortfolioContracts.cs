using System.Text.Json.Serialization;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class AcquisitionRoutePortfolioInputs
{
    public string RequirementInventoryPath { get; init; } = string.Empty;
    public string AcquisitionLoweringPath { get; init; } = string.Empty;
    public string MasterAnglerWindowsPath { get; init; } = string.Empty;
    public string CalendarResolutionPath { get; init; } = string.Empty;
    public string TargetDateCalendarPath { get; init; } = string.Empty;
    public string TargetDateUnlockPath { get; init; } = string.Empty;
    public string TargetDateFestivalPath { get; init; } = string.Empty;
    public string TargetDateLocationPath { get; init; } = string.Empty;
    public string TargetDateFacilityPath { get; init; } = string.Empty;
    public string TargetDateResourcePath { get; init; } = string.Empty;
    public string TargetDateCurrencyPath { get; init; } = string.Empty;
    public string TargetDateReservationPath { get; init; } = string.Empty;
    public string TargetDateProcessingPath { get; init; } = string.Empty;
    public string TargetDateFishingProbabilityPath { get; init; } = string.Empty;
    public string TargetDateStochasticRetryPath { get; init; } = string.Empty;
    public string TargetDateDailyTimeEnergyPath { get; init; } = string.Empty;
    public string TargetDateOpportunityCostPath { get; init; } = string.Empty;
    public string FishingForecastManifestPath { get; init; } = string.Empty;
    public string StrategyLedgerPath { get; init; } = string.Empty;
    public string SnapshotPath { get; init; } = string.Empty;
    public string RouteTimingCalibrationPath { get; init; } = string.Empty;
    public string ProposalPath { get; init; } = string.Empty;
}

internal static class AcquisitionRoutePortfolioInputAdapter
{
    public static AcquisitionRoutePortfolioInputs FromExecutionBinding(
        AcquisitionRouteExecutionBindingInputs value) => new()
    {
        RequirementInventoryPath = value.RequirementInventoryPath,
        AcquisitionLoweringPath = value.AcquisitionLoweringPath,
        MasterAnglerWindowsPath = value.MasterAnglerWindowsPath,
        CalendarResolutionPath = value.CalendarResolutionPath,
        TargetDateCalendarPath = value.TargetDateCalendarPath,
        TargetDateUnlockPath = value.TargetDateUnlockPath,
        TargetDateFestivalPath = value.TargetDateFestivalPath,
        TargetDateLocationPath = value.TargetDateLocationPath,
        TargetDateFacilityPath = value.TargetDateFacilityPath,
        TargetDateResourcePath = value.TargetDateResourcePath,
        TargetDateCurrencyPath = value.TargetDateCurrencyPath,
        TargetDateReservationPath = value.TargetDateReservationPath,
        TargetDateProcessingPath = value.TargetDateProcessingPath,
        TargetDateFishingProbabilityPath =
            value.TargetDateFishingProbabilityPath,
        TargetDateStochasticRetryPath = value.TargetDateStochasticRetryPath,
        TargetDateDailyTimeEnergyPath = value.TargetDateDailyTimeEnergyPath,
        TargetDateOpportunityCostPath = value.TargetDateOpportunityCostPath,
        FishingForecastManifestPath = value.FishingForecastManifestPath,
        StrategyLedgerPath = value.StrategyLedgerPath,
        SnapshotPath = value.BeforeSnapshotPath,
        RouteTimingCalibrationPath = value.RouteTimingCalibrationPath,
        ProposalPath = value.PortfolioProposalPath
    };
}

public sealed class AcquisitionRoutePortfolioProposal
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_proposal.v1";

    [JsonPropertyName("proposal_id")]
    public string ProposalId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("expected_ledger_revision")]
    public int ExpectedLedgerRevision { get; set; }

    [JsonPropertyName("prior_rollout_checkpoint_sha256")]
    public string PriorRolloutCheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("prior_supporting_transition_replan_sha256")]
    public string PriorSupportingTransitionReplanSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("completed_alternatives")]
    public AcquisitionRoutePortfolioCompletedAlternatives[]
        CompletedAlternatives
    { get; set; } =
        Array.Empty<AcquisitionRoutePortfolioCompletedAlternatives>();

    [JsonPropertyName("scoped_requirements")]
    public AcquisitionRoutePortfolioRequirementScope[] ScopedRequirements
    {
        get;
        set;
    } = Array.Empty<AcquisitionRoutePortfolioRequirementScope>();

    [JsonPropertyName("selected_route_occurrence_ids")]
    public string[] SelectedRouteOccurrenceIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("replaced_route_occurrence_ids")]
    public string[] ReplacedRouteOccurrenceIds { get; set; } =
        Array.Empty<string>();
}

public sealed record AcquisitionRoutePortfolioRequirementScope(
    [property: JsonPropertyName("requirement_set_id")]
    string RequirementSetId,
    [property: JsonPropertyName("requirement_id")]
    string RequirementId);

public sealed class AcquisitionRoutePortfolioAdmission
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } =
        "acquisition_route_portfolio_admission.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("proposal_id")]
    public string ProposalId { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_state_hash")]
    public string SnapshotStateHash { get; set; } = string.Empty;

    [JsonPropertyName("community_center_provenance")]
    public AcquisitionRouteCommunityCenterProvenance
        CommunityCenterProvenance { get; set; } = new();

    [JsonPropertyName("strategy_ledger_revision")]
    public int StrategyLedgerRevision { get; set; }

    [JsonPropertyName("prior_rollout_checkpoint_sha256")]
    public string PriorRolloutCheckpointSha256 { get; set; } = string.Empty;

    [JsonPropertyName("prior_supporting_transition_replan_sha256")]
    public string PriorSupportingTransitionReplanSha256 { get; set; } =
        string.Empty;

    [JsonPropertyName("completed_alternatives")]
    public AcquisitionRoutePortfolioCompletedAlternatives[]
        CompletedAlternatives
    { get; set; } =
        Array.Empty<AcquisitionRoutePortfolioCompletedAlternatives>();

    [JsonPropertyName("target_total_day")]
    public int TargetTotalDay { get; set; }

    [JsonPropertyName("requirement_inventory_sha256")]
    public string RequirementInventorySha256 { get; set; } = string.Empty;

    [JsonPropertyName("opportunity_cost_sha256")]
    public string OpportunityCostSha256 { get; set; } = string.Empty;

    [JsonPropertyName("proposal_sha256")]
    public string ProposalSha256 { get; set; } = string.Empty;

    [JsonPropertyName("strategy_ledger_sha256")]
    public string StrategyLedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("snapshot_sha256")]
    public string SnapshotSha256 { get; set; } = string.Empty;

    [JsonPropertyName("selected_route_occurrence_ids")]
    public string[] SelectedRouteOccurrenceIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("replaced_route_occurrence_ids")]
    public string[] ReplacedRouteOccurrenceIds { get; set; } =
        Array.Empty<string>();

    [JsonPropertyName("selection_rules_satisfied")]
    public bool SelectionRulesSatisfied { get; set; }

    [JsonPropertyName("all_routes_on_complete_pareto_frontier")]
    public bool AllRoutesOnCompleteParetoFrontier { get; set; }

    [JsonPropertyName("aggregate_cost_vector")]
    public AcquisitionOpportunityCostVector? AggregateCostVector { get; set; }

    [JsonPropertyName("atomic_commit_required")]
    public bool AtomicCommitRequired { get; set; }

    [JsonPropertyName("atomic_commit_preflight_passed")]
    public bool AtomicCommitPreflightPassed { get; set; }

    [JsonPropertyName("atomic_commit_request")]
    public ReservationPortfolioCommitRequest? AtomicCommitRequest { get; set; }

    [JsonPropertyName("portfolio_admission_ready")]
    public bool PortfolioAdmissionReady { get; set; }

    [JsonPropertyName("formal_training_authorized")]
    public bool FormalTrainingAuthorized { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } =
        "A caller proposes an explicit requirement-group scope and exact route occurrences. Every scoped group must satisfy its authoritative all-required or choose-at-least rule, exactly one route may serve each selected alternative, and every route must remain on the complete target-date Pareto frontier. Costs remain a non-scalar aggregate vector. Material and native-currency claims are preflighted together and emitted as one reservation portfolio request for one locked save and one ledger revision. A claimless selection still commits one portfolio ownership marker so its later settlement cannot borrow another portfolio identity. This validates a proposal; it does not invent a trade-off preference, prove execution, or authorize formal training.";
}
